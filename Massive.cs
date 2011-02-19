using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Massive {
    
    public static class ObjectExtensions {
        /// <summary>
        /// Extension method for adding in a bunch of parameters
        /// </summary>
        public static void AddParams(this DbCommand cmd, object[] args) {
            foreach (var item in args) {
                AddParam(cmd, item);
            }
        }
        /// <summary>
        /// Extension for adding single parameter
        /// </summary>
        public static void AddParam(this DbCommand cmd, object item) {
            var p = cmd.CreateParameter();
            p.ParameterName = string.Format("@{0}", cmd.Parameters.Count);
            //fix for NULLs as parameter values
            if (item == null) {
                p.Value = DBNull.Value;
            } else {
                //fix for Guids
                if (item.GetType() == typeof(Guid)) {
                    p.Value = item.ToString();
                    p.DbType = DbType.String;
                    p.Size = 4000;
                }else if(item.GetType()==typeof(ExpandoObject)){
                    var d = (IDictionary<string, object>)item;
                    p.Value = d.Values.FirstOrDefault();
                } else {
                    p.Value = item;
                }
            }

            //This will force the pre-compilation stuff to be honored
            //from DataChomp
            if (item.GetType() == typeof(string))
                p.Size = 4000;
            cmd.Parameters.Add(p);
        }
        /// <summary>
        /// Turns an IDataReader to a Dynamic list of things
        /// </summary>
        public static List<dynamic> ToExpandoList(this IDataReader rdr) {
            var result = new List<dynamic>();
            //work with the Expando as a Dictionary
            while (rdr.Read()) {
                dynamic e = new ExpandoObject();
                var d = e as IDictionary<string, object>;
                for (int i = 0; i < rdr.FieldCount; i++)
                    d.Add(rdr.GetName(i), rdr[i]);
                result.Add(e);
            }
            return result;
        }
        /// <summary>
        /// Turns the object into an ExpandoObject
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static dynamic ToExpando(this object o) {
            var result = new ExpandoObject();
            var d = result as IDictionary<string, object>; //work with the Expando as a Dictionary
            if (o.GetType() == typeof(ExpandoObject)) return o; //shouldn't have to... but just in case
            //special for form submissions
            if (o.GetType() == typeof(NameValueCollection)) {
                var nv = (NameValueCollection)o;
                nv.Cast<string>().Select(key => new KeyValuePair<string, object>(key, nv[key])).ToList().ForEach(i => d.Add(i));
            } else {
                //assume it's a regular lovely object
                var props = o.GetType().GetProperties();
                foreach (var item in props) {
                    d.Add(item.Name, item.GetValue(o, null));
                }
            }
            return result;
        }
        /// <summary>
        /// Turns the object into a Dictionary
        /// </summary>
        /// <param name="thingy"></param>
        /// <returns></returns>
        public static IDictionary<string, object> ToDictionary(this object thingy) {
            return (IDictionary<string, object>)thingy.ToExpando();
        }
    }
    /// <summary>
    /// A class that wraps your database table in Dynamic Funtime
    /// </summary>
    public  class DynamicModel {
        DbProviderFactory _factory;
        string _connectionString;
        /// <summary>
        /// A bit of convenience here
        /// </summary>
        public DynamicModel():this(ConfigurationManager.ConnectionStrings[0].Name) { }
        /// <summary>
        /// Creates a slick, groovy little wrapper for your action
        /// </summary>
        /// <param name="connectionStringName"></param>
        public DynamicModel(string connectionStringName) {
            //can be overridden by property setting
            TableName = this.GetType().Name;
            var _providerName = "System.Data.SqlClient";
            if (ConfigurationManager.ConnectionStrings[connectionStringName] != null) {
                _providerName = ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName ?? "System.Data.SqlClient";
            } else {
                throw new InvalidOperationException("Can't find a connection string with the name '" + connectionStringName + "'");
            }
            _factory = DbProviderFactories.GetFactory(_providerName);
            _connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }
        /// <summary>
        /// Runs a query against the database
        /// </summary>
        public IList<dynamic> Query(string sql, params object[] args) {
            var result = new List<dynamic>();
            using (var conn = OpenConnection()) {
                var cmd = CreateCommand(sql, conn, args);
                using (var rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection)) {
                    result = rdr.ToExpandoList();
                }
            }
            return result;
        }  
        /// <summary>
        /// Creates a DBCommand that you can use for loving your database.
        /// </summary>
        DbCommand CreateCommand(string sql, DbConnection conn, params object[] args) {
            DbCommand result = null;
            result = _factory.CreateCommand();
            result.Connection = conn;
            result.CommandText = sql;
            if (args.Length > 0)
                result.AddParams(args);
            return result;
        }
        /// <summary>
        /// Returns and OpenConnection
        /// </summary>
        public DbConnection OpenConnection() {
            var conn = _factory.CreateConnection();
            conn.ConnectionString = _connectionString;
            conn.Open();
            return conn;
        }
        /// <summary>
        /// Builds a set of Insert and Update commands based on the passed-on objects.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public List<DbCommand> BuildCommands(params object[] things) {
            var commands = new List<DbCommand>();
            foreach (var item in things) {
                if (HasPrimaryKey(item)) {
                    commands.Add(CreateUpdateCommand(item,GetPrimaryKey(item)));
                }else{
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
        public int Transact(params object[] things) {
            var commands = BuildCommands(things);
            return Execute(commands);
        }
        /// <summary>
        /// Executes a series of DBCommands in a transaction
        /// </summary>
        public int Execute(IEnumerable<DbCommand> commands) {
            var result = 0;
            using (var conn = OpenConnection()) {
                using (var tx = conn.BeginTransaction()) {
                    foreach (var cmd in commands) {
                        cmd.Connection = conn;
                        cmd.Transaction = tx;
                        result+=cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            return result;
        }
        string _primaryKeyField;
        /// <summary>
        /// Conventionally returns a PK field. The default is "ID" if you don't set one
        /// </summary>
        public string PrimaryKeyField {
            get { return string.IsNullOrEmpty(_primaryKeyField) ? /*a bit of convention here*/ "ID" : /*oh well - did our best*/ _primaryKeyField; }
            set { _primaryKeyField = value; }
        }
        /// <summary>
        /// Conventionally introspects the object passed in for a field that 
        /// looks like a PK. If you've named your PrimaryKeyField, this becomes easy
        /// </summary>
        public bool HasPrimaryKey(object o) {
            return o.ToDictionary().ContainsKey(PrimaryKeyField);
        }
        /// <summary>
        /// If the object passed in has a property with the same name as your PrimaryKeyField
        /// it is returned here.
        /// </summary>
        public object GetPrimaryKey(object o) {
            object result = null;
            o.ToDictionary().TryGetValue(PrimaryKeyField, out result);
            return result;
        }
        /// <summary>
        /// The name of the Database table we're working with. This defaults to 
        /// the class name - set this value if it's different
        /// </summary>
        public string TableName { get; set; }
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DbCommand CreateInsertCommand(object o) {
            DbCommand result = null;
            //turn this into an expando - we'll need that for the validators
            var expando = o.ToExpando();
            var settings = (IDictionary<string, object>)expando;
            var sbKeys = new StringBuilder();
            var sbVals = new StringBuilder();
            var stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2})";
            result = CreateCommand(stub,null);

            int counter = 0;
            foreach (var item in settings) {
                sbKeys.AppendFormat("{0},", item.Key);
                sbVals.AppendFormat("@{0},", counter.ToString());
                result.AddParam(item.Value);
                counter++;
            }
            if (counter > 0) {
                //strip off trailing commas
                var keys = sbKeys.ToString().Substring(0, sbKeys.Length - 1);
                var vals = sbVals.ToString().Substring(0, sbVals.Length - 1);
                var sql = string.Format(stub, TableName, keys, vals);
                result.CommandText = sql;
            } else throw new InvalidOperationException("Can't parse this object to the database - there are no properties set");
            return result;
        }
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DbCommand CreateUpdateCommand(object o, object key) {
            var expando = o.ToExpando();
            var settings = (IDictionary<string, object>)expando;
            var sbKeys = new StringBuilder();
            var stub = "UPDATE {0} SET {1} WHERE {2} = @{3}";
            var args = new List<object>();
            var result = CreateCommand(stub,null);
            int counter = 0;
            foreach (var item in settings) {
                var val = item.Value;
                if (!item.Key.Equals(PrimaryKeyField, StringComparison.CurrentCultureIgnoreCase) && item.Value != null) {
                    result.AddParam(val);
                    sbKeys.AppendFormat("{0} = @{1}, \r\n", item.Key, counter.ToString());
                    counter++;
                }
            }
            if (counter > 0) {
                //add the key
                result.AddParam(key);
                //strip the last commas
                var keys = sbKeys.ToString().Substring(0, sbKeys.Length - 4);
                result.CommandText = string.Format(stub, TableName, keys, PrimaryKeyField, counter);
            } else throw new InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs");
            return result;
        }
        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public DbCommand CreateDeleteCommand(string where = "", object key = null, params object[] args) {
            var sql = string.Format("DELETE FROM {0} ", TableName);
            if (key != null) {
                sql += string.Format("WHERE {0}=@0", PrimaryKeyField);
                args = new object[]{key};
            } else if (!string.IsNullOrEmpty(where)) {
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            } 
            return CreateCommand(sql, null, args);
        }
        /// <summary>
        /// Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public object Insert(object o) {
            dynamic result = 0;
            //this has to happen in the same connection or we won't get the @@IDENTITY stuff back
            using (var conn = OpenConnection()) {
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
        public int Update(object o, object key) {
            return Execute(new DbCommand[] { CreateUpdateCommand(o, key) });
        }
        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public int Delete(object key = null, string where = "", params object[] args) {
            return Execute(new DbCommand[] { CreateDeleteCommand(where: where, key:key, args: args) });
        }
        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public IEnumerable<dynamic> All(string where = "", string orderBy = "", int limit = 0, string columns = "*", params object[] args) {
            string sql = limit > 0 ? "SELECT TOP " + limit + " {0} FROM {1} " : "SELECT {0} FROM {1} ";
            if (!string.IsNullOrEmpty(where))
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            if (!String.IsNullOrEmpty(orderBy))
                sql += orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase) ? orderBy : " ORDER BY " + orderBy;
            return Query(string.Format(sql, columns,TableName), args);
        }
        /// <summary>
        /// Returns a single row from the database
        /// </summary>
        /// <returns>ExpandoObject</returns>
        public dynamic Single(object key, string columns = "*") {
            var sql = string.Format("SELECT {0} FROM {1} WHERE {2} = @0", columns,TableName, PrimaryKeyField);
            return Query(sql, key).FirstOrDefault();
        }
    }
}