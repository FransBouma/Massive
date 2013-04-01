using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace Massive
{
    public static class ObjectExtensions
    {
        /// <summary>
        /// Extension method for adding a bunch of parameters
        /// </summary>
        public static void AddParams(this DbCommand cmd, params object[] args)
        {
            foreach (var item in args)
            {
                AddParam(cmd, item);
            }
        }
        /// <summary>
        /// Extension for adding a single parameter
        /// </summary>
        public static void AddParam(this DbCommand cmd, object item)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = string.Format("@{0}", cmd.Parameters.Count);

            if (item == null)
            {
                p.Value = DBNull.Value;
            }
            else
            {
                if (item is Guid)
                {
                    p.Value = item.ToString();
                    p.DbType = DbType.String;
                    p.Size = 4000;
                }
                else if (item is ExpandoObject)
                {
                    var d = (IDictionary<string, object>)item;
                    p.Value = d.Values.FirstOrDefault();
                }
                else
                {
                    p.Value = item;
                }

                if (item is string)
                    p.Size = ((string)item).Length > 4000 ? -1 : 4000;
            }
            cmd.Parameters.Add(p);
        }
        /// <summary>
        /// Turns an IDataReader into a dynamic list of expando objects
        /// </summary>
        public static List<dynamic> ToExpandoList(this IDataReader rdr)
        {
            var result = new List<dynamic>();
            while (rdr.Read())
            {
                result.Add(rdr.RecordToExpando());
            }
            return result;
        }

        public static dynamic RecordToExpando(this IDataReader rdr)
        {
            dynamic e = new ExpandoObject();
            var d = e as IDictionary<string, object>;
            for (var i = 0; i < rdr.FieldCount; i++)
                d.Add(rdr.GetName(i), DBNull.Value.Equals(rdr[i]) ? null : rdr[i]);
            return e;
        }

        /// <summary>
        /// Turns an object into an ExpandoObject
        /// </summary>
        public static dynamic ToExpando(this object o)
        {
            var result = new ExpandoObject();
            var properties = result as IDictionary<string, object>;

            if (o is ExpandoObject)
                return o; //shouldn't have to... but just in case

            if (o.GetType() == typeof(NameValueCollection) || o.GetType().IsSubclassOf(typeof(NameValueCollection)))
            {
                var nv = (NameValueCollection)o;
                nv.Cast<string>().Select(key => new KeyValuePair<string, object>(key, nv[key])).ToList().ForEach(properties.Add);
            }
            else
            {
                var props = o.GetType().GetProperties();
                foreach (var item in props)
                {
                    properties.Add(item.Name, item.GetValue(o, null));
                }
            }
            return result;
        }
        /// <summary>
        /// Turns an object into a Dictionary
        /// </summary>
        public static IDictionary<string, object> ToDictionary(this object thingy)
        {
            return (IDictionary<string, object>)thingy.ToExpando();
        }
    }
    /// <summary>
    /// A class that wraps your database table in Dynamic Funtime
    /// </summary>
    public class DynamicModel : DynamicObject
    {
        const string ProviderName = "MySql.Data.MySqlClient";

        private readonly DbProviderFactory _factory;
        private readonly string _connectionString;

        public static DynamicModel Open(string connectionStringName)
        {
            dynamic dm = new DynamicModel(connectionStringName);
            return dm;
        }

        /// <summary>
        /// Create a dynamic model
        /// </summary>
        /// <param name="connectionStringName">the connection string name or the connection stirng itself</param>
        /// <param name="tableName">the table name</param>
        /// <param name="primaryKeyField">the primary key field name</param>
        public DynamicModel(string connectionStringName, string tableName = "", string primaryKeyField = "")
        {
            TableName = string.IsNullOrEmpty(tableName) ? GetType().Name : tableName;
            PrimaryKeyField = string.IsNullOrEmpty(primaryKeyField) ? "ID" : primaryKeyField;

            try
            {
                _factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (FileLoadException ex)
            {
                throw new MassiveException(string.Format("Could not load the specified provider: {0}. Have you added a reference to the correct assembly?", ProviderName), ex);
            }
            catch (ArgumentException e)
            {
                var foundClasses = "I did find these Factories:";
                var dt = DbProviderFactories.GetFactoryClasses();
                for (var i = 0; i < dt.Rows.Count; i++)
                    foundClasses += String.Format("|{0}|", dt.Rows[i][2]);

                throw new ArgumentException(String.Format("{0}{1}{2}", e.Message, Environment.NewLine, foundClasses));

            }

            var conString = ConfigurationManager.ConnectionStrings[connectionStringName];
            _connectionString = conString != null ? conString.ConnectionString : connectionStringName;
        }

        /// <summary>
        /// Creates a new Expando from a Form POST - white listed against the columns in the DB
        /// </summary>
        public dynamic CreateFrom(NameValueCollection coll)
        {
            dynamic result = new ExpandoObject();
            var dc = (IDictionary<string, object>)result;
            var schema = Schema;

            foreach (var item in coll.Keys)
            {
                var exists = schema.Any(x => x.COLUMN_NAME.ToLower() == item.ToString().ToLower());
                if (exists)
                {
                    var key = item.ToString();
                    var val = coll[key];
                    if (!String.IsNullOrEmpty(val))
                    {
                        //what to do here? If it's empty... set it to NULL?
                        //if it's a string value - let it go through if it's NULLABLE?
                        //Empty? WTF?
                        dc.Add(key, val);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Gets a default value for the column
        /// </summary>
        public dynamic DefaultValue(dynamic column)
        {
            dynamic result;

            var defaultValue = column.COLUMN_DEFAULT;
            if (string.IsNullOrEmpty(defaultValue))
            {
                result = null;
            }
            else if (defaultValue == "getdate()" || defaultValue == "(getdate())")
            {
                result = DateTime.Now.ToShortDateString();
            }
            else if (defaultValue == "newid()")
            {
                result = Guid.NewGuid().ToString();
            }
            else
            {
                result = defaultValue.Replace("(", "").Replace(")", "");
            }

            return result;
        }

        /// <summary>
        /// Creates an empty Expando set with defaults from the DB
        /// </summary>
        public dynamic Prototype
        {
            get
            {
                dynamic result = new ExpandoObject();
                var schema = Schema;
                foreach (var column in schema)
                {
                    var dc = (IDictionary<string, object>)result;
                    dc.Add(column.COLUMN_NAME, DefaultValue(column));
                }
                result._Table = this;
                return result;
            }
        }
        /// <summary>
        /// List out all the schema bits for use with ... whatever
        /// </summary>
        IEnumerable<dynamic> _schema;
        public IEnumerable<dynamic> Schema
        {
            get
            {
                return _schema ??
                    (_schema = Query("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @0", TableName));
            }
        }

        /// <summary>
        /// Enumerates the reader yielding the result - thanks to Jeroen Haegebaert
        /// </summary>
        public virtual object QueryScalar(string sql, params object[] args)
        {
            using (var conn = OpenConnection())
            {
                var rdr = CreateCommand(sql, conn, args).ExecuteScalar();
                return rdr;
            }
        }

        /// <summary>
        /// Enumerates the reader yielding the result - thanks to Jeroen Haegebaert
        /// </summary>
        public virtual IEnumerable<dynamic> Query(string sql, params object[] args)
        {
            using (var conn = OpenConnection())
            {
                var rdr = CreateCommand(sql, conn, args).ExecuteReader();
                while (rdr.Read())
                {
                    yield return rdr.RecordToExpando();
                }
            }
        }
        /// <summary>
        /// Executes the reader using SQL async API - thanks to Damian Edwards
        /// </summary>
        public void QueryAsync(string sql, Action<List<dynamic>> callback, params object[] args)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var cmd = new SqlCommand(sql, conn);
                cmd.AddParams(args);
                conn.Open();

                var task = Task.Factory.FromAsync<IDataReader>(cmd.BeginExecuteReader, cmd.EndExecuteReader, null);
                task.ContinueWith(x => callback.Invoke(x.Result.ToExpandoList()));
            }
        }

        public virtual IEnumerable<dynamic> Query(string sql, DbConnection connection, params object[] args)
        {
            using (var rdr = CreateCommand(sql, connection, args).ExecuteReader())
            {
                while (rdr.Read())
                {
                    yield return rdr.RecordToExpando();
                }
            }
        }
        /// <summary>
        /// Returns a single result
        /// </summary>
        public virtual object Scalar(string sql, params object[] args)
        {
            object result;
            using (var conn = OpenConnection())
            {
                result = CreateCommand(sql, conn, args).ExecuteScalar();
            }
            return result;
        }

        /// <summary>
        /// Creates a DBCommand that you can use for loving your database.
        /// </summary>
        private DbCommand CreateCommand(string sql, DbConnection conn, params object[] args)
        {
            using (var result = _factory.CreateCommand())
            {
                result.Connection = conn;
                result.CommandText = sql;
                if (args.Length > 0)
                    result.AddParams(args);
                return result;
            }
        }

        /// <summary>
        /// Returns and OpenConnection
        /// </summary>
        public virtual DbConnection OpenConnection()
        {
            var result = _factory.CreateConnection();
            result.ConnectionString = _connectionString;
            result.Open();
            return result;
        }

        /// <summary>
        /// Builds a set of Insert and Update commands based on the passed-on objects.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public virtual List<DbCommand> BuildCommands(params object[] things)
        {
            var commands = new List<DbCommand>();
            foreach (var item in things)
            {
                if (HasPrimaryKey(item))
                {
                    commands.Add(CreateUpdateCommand(item, GetPrimaryKey(item)));
                }
                else
                {
                    commands.Add(CreateInsertCommand(item));
                }
            }
            return commands;
        }
        /// <summary>
        /// Executes a set of objects as Insert or Update commands based on their property settings, within a transaction.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public virtual int Save(params object[] things)
        {
            var commands = BuildCommands(things);
            return Execute(commands);
        }

        /// <summary>
        /// Execute a single command
        /// </summary>
        public virtual int Execute(DbCommand command)
        {
            return Execute(new[] { command });
        }

        public virtual int Execute(string sql, params object[] args)
        {
            return Execute(CreateCommand(sql, null, args));
        }
        /// <summary>
        /// Executes a series of DBCommands in a transaction
        /// </summary>
        public virtual int Execute(IEnumerable<DbCommand> commands)
        {
            var result = 0;
            using (var conn = OpenConnection())
            {
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var cmd in commands)
                    {
                        cmd.Connection = conn;
                        cmd.Transaction = tx;
                        result += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            return result;
        }
        public virtual string PrimaryKeyField { get; set; }

        /// <summary>
        /// Conventionally introspects the object passed in for a field that 
        /// looks like a PK. If you've named your PrimaryKeyField, this becomes easy
        /// </summary>
        public virtual bool HasPrimaryKey(object o)
        {
            return o.ToDictionary().ContainsKey(PrimaryKeyField);
        }

        /// <summary>
        /// If the object passed in has a property with the same name as your PrimaryKeyField
        /// it is returned here.
        /// </summary>
        public virtual object GetPrimaryKey(object o)
        {
            object result;
            o.ToDictionary().TryGetValue(PrimaryKeyField, out result);
            return result;
        }

        public virtual string TableName { get; set; }

        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public virtual DbCommand CreateInsertCommand(object o)
        {
            var expando = o.ToExpando();
            var settings = (IDictionary<string, object>)expando;
            var sbKeys = new StringBuilder();
            var sbVals = new StringBuilder();

            var stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2})";
            var result = CreateCommand(stub, null);
            var counter = 0;

            foreach (var item in settings)
            {
                sbKeys.AppendFormat("{0},", item.Key);
                sbVals.AppendFormat("@{0},", counter);
                result.AddParam(item.Value);
                counter++;
            }

            if (counter > 0)
            {
                var keys = sbKeys.ToString().Substring(0, sbKeys.Length - 1);
                var values = sbVals.ToString().Substring(0, sbVals.Length - 1);
                var sql = string.Format(stub, TableName, keys, values);

                result.CommandText = sql;
            }
            else
            {
                throw new InvalidOperationException("Can't parse this object to the database - there are no properties set");
            }

            return result;
        }

        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public virtual DbCommand CreateUpdateCommand(object o, object key)
        {
            var expando = o.ToExpando();
            var settings = (IDictionary<string, object>)expando;
            var sbKeys = new StringBuilder();
            var stub = "UPDATE {0} SET {1} WHERE {2} = @{3}";

            var result = CreateCommand(stub, null);
            var counter = 0;
            foreach (var item in settings)
            {
                var val = item.Value;
                if (!item.Key.Equals(PrimaryKeyField, StringComparison.CurrentCultureIgnoreCase) && item.Value != null)
                {
                    result.AddParam(val);
                    sbKeys.AppendFormat("{0} = @{1}, \r\n", item.Key, counter);
                    counter++;
                }
            }

            if (counter > 0)
            {
                result.AddParam(key);
                var keys = sbKeys.ToString().Substring(0, sbKeys.Length - 4);//strip the last commas
                result.CommandText = string.Format(stub, TableName, keys, PrimaryKeyField, counter);
            }
            else
            {
                throw new InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs");
            }

            return result;
        }

        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public virtual DbCommand CreateDeleteCommand(string where = "", object key = null, params object[] args)
        {
            var sql = string.Format("DELETE FROM {0} ", TableName);
            if (key != null)
            {
                sql += string.Format("WHERE {0}=@0", PrimaryKeyField);
                args = new[] { key };
            }
            else if (!string.IsNullOrEmpty(where))
            {
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            }

            return CreateCommand(sql, null, args);
        }

        /// <summary>
        /// Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public virtual object Insert(object o)
        {
            dynamic result = 0;
            using (var conn = OpenConnection())
            {
                var cmd = CreateInsertCommand(o);
                cmd.Connection = conn;
                cmd.ExecuteNonQuery();
                cmd.CommandText = "SELECT @@IDENTITY as newID";
                result = cmd.ExecuteScalar();
            }

            return result;
        }

        /// <summary>
        /// Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString
        /// </summary>
        public virtual int Update(object o, object key)
        {
            return Execute(CreateUpdateCommand(o, key));
        }

        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public int Delete(object key = null, string where = "", params object[] args)
        {
            return Execute(CreateDeleteCommand(where: where, key: key, args: args));
        }

        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public virtual IEnumerable<dynamic> All(string where = "", string orderBy = "", int limit = 0, string columns = "*", params object[] args)
        {
            string sql = BuildSelect(where, orderBy, limit);
            return Query(string.Format(sql, columns, TableName), args);
        }

        /// <summary>
        /// Returns the count from this table, using optional where statement
        /// </summary>
        /// <param name="where">query parameters (eg: "id>=100")</param>
        /// <param name="args"></param>
        /// <returns></returns>
        public int Count(string where = "", params object[] args)
        {
            var sql = BuildCount(where);
            var ob = QueryScalar(string.Format(sql, TableName), args);
            return int.Parse(ob.ToString());
        }

        private static string BuildCount(string where)
        {
            var sql = "SELECT Count(*) FROM {0} ";
            if (!string.IsNullOrEmpty(where))
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            return sql;
        }

        private static string BuildSelect(string where, string orderBy, int limit)
        {
            var sql = "SELECT {0} FROM {1} ";
            if (!string.IsNullOrEmpty(where))
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;

            if (!string.IsNullOrEmpty(orderBy))
                sql += orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase) ? orderBy : " ORDER BY " + orderBy;

            if (limit > 0)
                sql += " LIMIT " + limit;

            return sql;
        }

        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public virtual void AllAsync(Action<List<dynamic>> callback, string where = "", string orderBy = "", int limit = 0, string columns = "*", params object[] args)
        {
            var sql = BuildSelect(where, orderBy, limit);
            QueryAsync(string.Format(sql, columns, TableName), callback, args);
        }

        /// <summary>
        /// Returns a dynamic PagedResult. Result properties are Items, TotalPages, and TotalRecords.
        /// </summary>
        public virtual dynamic Paged(string where = "", string orderBy = "", string columns = "*", int pageSize = 20, int currentPage = 1, params object[] args)
        {
            dynamic result = new ExpandoObject();
            var countQuery = string.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName);

            if (string.IsNullOrEmpty(orderBy))
                orderBy = PrimaryKeyField;

            if (!string.IsNullOrEmpty(where))
            {
                if (!where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase))
                {
                    where = "WHERE " + where;
                }
            }

            var sql = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {2}) AS Row, {0} FROM {3} {4}) AS Paged ", columns, pageSize, orderBy, TableName, where);
            var pageStart = (currentPage - 1) * pageSize;
            sql += string.Format(" WHERE Row > {0} AND Row <={1}", pageStart, (pageStart + pageSize));
            countQuery += where;
            result.TotalRecords = Scalar(countQuery, args);
            result.TotalPages = result.TotalRecords / pageSize;
            if (result.TotalRecords % pageSize > 0)
                result.TotalPages += 1;
            result.Items = Query(string.Format(sql, columns, TableName), args);
            return result;
        }

        /// <summary>
        /// Returns a single row from the database
        /// </summary>
        public virtual dynamic Single(string where, params object[] args)
        {
            var sql = string.Format("SELECT * FROM {0} WHERE {1}", TableName, where);
            return Query(sql, args).FirstOrDefault();
        }

        /// <summary>
        /// Returns a single row from the database
        /// </summary>
        public virtual dynamic Single(object key, string columns = "*")
        {
            var sql = string.Format("SELECT {0} FROM {1} WHERE {2} = @0", columns, TableName, PrimaryKeyField);
            return Query(sql, key).FirstOrDefault();
        }

        /// <summary>
        /// A helpful query tool
        /// </summary>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            //parse the method
            var constraints = new List<string>();
            var counter = 0;
            var info = binder.CallInfo;
            // accepting named args only... SKEET!
            if (info.ArgumentNames.Count != args.Length)
            {
                throw new InvalidOperationException("Please use named arguments for this type of query - the column name, orderby, columns, etc");
            }

            //first should be "FindBy, Last, Single, First"
            var op = binder.Name;
            var columns = " * ";
            var orderBy = string.Format(" ORDER BY {0}", PrimaryKeyField);
            var where = "";
            var whereArgs = new List<object>();

            //loop the named args - see if we have order, columns and constraints
            if (info.ArgumentNames.Count > 0)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    var name = info.ArgumentNames[i].ToLower();
                    switch (name)
                    {
                        case "orderby":
                            orderBy = " ORDER BY " + args[i];
                            break;
                        case "columns":
                            columns = args[i].ToString();
                            break;
                        default:
                            constraints.Add(string.Format(" {0} = @{1}", name, counter));
                            whereArgs.Add(args[i]);
                            counter++;
                            break;
                    }
                }
            }

            if (constraints.Count > 0)
            {
                where = " WHERE " + string.Join(" AND ", constraints.ToArray());
            }

            var sql = "SELECT TOP 1 " + columns + " FROM " + TableName + where;
            var singleResult = op.StartsWith("First") || op.StartsWith("Last") || op.StartsWith("Get");

            //Be sure to sort by DESC on the PK (PK Sort is the default)
            if (op.StartsWith("Last"))
            {
                orderBy = orderBy + " DESC ";
            }
            else
            {
                //default to multiple
                sql = "SELECT " + columns + " FROM " + TableName + where;
            }

            result = singleResult ? Query(sql + orderBy, whereArgs.ToArray()).FirstOrDefault() :
                               Query(sql + orderBy, whereArgs.ToArray());
            return true;
        }

        internal class MassiveException : Exception
        {
            public MassiveException(string message, Exception innerException = null)
                : base(message, innerException)
            {

            }
        }
    }
}