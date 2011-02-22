using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Massive {

    public static class ObjectExtensions {
        /// <summary>
        /// Extension method for adding in a bunch of parameters
        /// </summary>
        public static void AddParams(this DbCommand cmd, IEnumerable<object> args) {
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
            if (item == null) {
                p.Value = DBNull.Value;
            } else {
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
                //from DataChomp
                if (item.GetType() == typeof(string))
                    p.Size = 4000;
            }
            cmd.Parameters.Add(p);
        }
        /// <summary>
        /// Turns an IDataReader to a Dynamic list of things
        /// </summary>
        public static List<dynamic> ToExpandoList(this IDataReader rdr) {
            var result = new List<dynamic>();
            while (rdr.Read()) {
                dynamic e = new ExpandoObject();
                var d = (IDictionary<string, object>)e;
                for (int i = 0; i < rdr.FieldCount; i++)
                    d.Add(rdr.GetName(i), rdr[i]);
                result.Add(e);
            }
            return result;
        }
        /// <summary>
        /// Turns the object into an ExpandoObject
        /// </summary>
        public static dynamic ToExpando(this object o) {
            var result = new ExpandoObject();
            var d = result as IDictionary<string, object>; //work with the Expando as a Dictionary
            if (o.GetType() == typeof(ExpandoObject)) return o; //shouldn't have to... but just in case
            if (o.GetType() == typeof(NameValueCollection)) {
                var nv = (NameValueCollection)o;
                nv.Cast<string>().Select(key => new KeyValuePair<string, object>(key, nv[key])).ToList().ForEach(i => d.Add(i));
            } else {
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

        public DynamicModel(string connectionStringName= "", string tableName = "", string primaryKeyField ="") {
            TableName = tableName == "" ?  this.GetType().Name : tableName;
            PrimaryKeyField = string.IsNullOrEmpty(primaryKeyField) ? "ID" : primaryKeyField;
            if (connectionStringName == "")
                connectionStringName = ConfigurationManager.ConnectionStrings[0].Name;
            var _providerName = "System.Data.SqlClient";
            if (ConfigurationManager.ConnectionStrings[connectionStringName] != null) {
                if (!string.IsNullOrEmpty(ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName))
                    _providerName = ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName;
            } else {
                throw new InvalidOperationException("Can't find a connection string with the name '" + connectionStringName + "'");
            }
            _factory = DbProviderFactories.GetFactory(_providerName);
            _connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }
        /// <summary>
        /// Enumerates the reader yielding the result - thanks to Jeroen Haegebaert
        /// </summary>
        public IEnumerable<dynamic> Query(string sql, params object[] args) {
            using (var conn = OpenConnection()) {
                var rdr = CreateCommand(sql, conn, args).ExecuteReader(CommandBehavior.CloseConnection);
                while (rdr.Read()) {
                    var e = new ExpandoObject();
                    var d = (IDictionary<string, object>)e;
                    for (var i = 0; i < rdr.FieldCount; i++)
                        d.Add(rdr.GetName(i), rdr[i]);
                    yield return e;
                }
            }
        }
        /// <summary>
        /// Runs a query against the database
        /// </summary>
        public IList<dynamic> Fetch(string sql, params object[] args) {
            return Query(sql, args).ToList<dynamic>();
        }
        /// <summary>
        /// Returns a single result
        /// </summary>
        public object Scalar(string sql, params object[] args) {
            using (var conn = OpenConnection()) {
                return CreateCommand(sql, conn, args).ExecuteScalar();
            }
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
            return BuildCommandsWithWhitelist(null, things);
        }
        /// <summary>
        /// Builds a set of Insert and Update commands based on the passed-on objects.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public List<DbCommand> BuildCommandsWithWhitelist(object whitelist, params object[] things) {
            return things.Select(item => HasPrimaryKey(item) ? CreateUpdateCommand(item,GetPrimaryKey(item),whitelist) : CreateInsertCommand(item,whitelist)).ToList();
        }
        /// <summary>
        /// Executes a set of objects as Insert or Update commands based on their property settings, within a transaction.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public int Save(params object[] things) {
            return SaveWithWhitelist(null, things);
        }
        public int SaveWithWhitelist(object whitelist, params object[] things) {
            return Execute(BuildCommandsWithWhitelist(whitelist, things));
        }
        public int Execute(params DbCommand[] command) {
            return Execute((IEnumerable<DbCommand>)command);
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
        public string PrimaryKeyField { get; set; }
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
            object result;
            o.ToDictionary().TryGetValue(PrimaryKeyField, out result);
            return result;
        }
        public string TableName { get; set; }
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DbCommand CreateInsertCommand(object o, object whitelist = null) {
            const string stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2})";
            var result = CreateCommand(stub,null);
            var items = FilterItems(o, whitelist);
            if (items.Any()) {
                var keys = string.Join(",", items.Select(item => item.Key).ToArray());
                var vals = string.Join(",", items.Select((_, i) => "@" + i.ToString()).ToArray());
                result.AddParams(items.Select(item => item.Value));
                result.CommandText = string.Format(stub, TableName, keys, vals);
            } else throw new InvalidOperationException("Can't parse this object to the database - there are no properties set");
            return result;
        }
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DbCommand CreateUpdateCommand(object o, object key, object whitelist = null) {
            const string stub = "UPDATE {0} SET {1} WHERE {2} = @{3}";
            var result = CreateCommand(stub,null);
            var items = FilterItems(o, whitelist);
            if (items.Any()) {
                var keys = string.Join(",", items.Select((item, i) => string.Format("{0} = @{1} \r\n", item.Key, i)).ToArray());
                result.AddParams(items.Select(item => item.Value).Concat(new[]{key}));
                result.CommandText = string.Format(stub, TableName, keys, PrimaryKeyField, items.Count);
            } else throw new InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs");
            return result;
        }
        private IList<KeyValuePair<string,object>> FilterItems(object o, object whitelist) {
            IEnumerable<KeyValuePair<string, object>> settings = o.ToDictionary();
            var whitelistValues = GetColumns(whitelist).Select(s => s.Trim());
            if (!string.Equals("*", whitelistValues.FirstOrDefault(), StringComparison.Ordinal))
                settings = settings.Join(whitelistValues, s => s.Key.Trim(), w => w, (s,_) => s, StringComparer.OrdinalIgnoreCase);
            return settings.Where(item => !item.Key.Equals(PrimaryKeyField, StringComparison.CurrentCultureIgnoreCase) && item.Value != null).ToList();
        }
        private static IEnumerable<string> GetColumns(object columns) {
            return (columns == null)   ? new[]{"*"} :
                   (columns is string) ? ((string)columns).Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries) : 
                   (columns is Type)   ? ((Type)columns).GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance).Select(prop => prop.Name)
                                       : (columns as IEnumerable<string>) ?? columns.ToDictionary().Select(kvp => kvp.Key);
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
        public object Insert(object o, object whitelist = null) {
            dynamic result = 0;
            using (var conn = OpenConnection()) {
                var cmd = CreateInsertCommand(o, whitelist);
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
        public int Update(object o, object key, object whitelist = null) {
            return Execute(CreateUpdateCommand(o, key, whitelist));
        }
        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public int Delete(object key = null, string where = "", params object[] args) {
            return Execute(CreateDeleteCommand(where: where, key:key, args: args));
        }
        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public IEnumerable<dynamic> All(string where = "", string orderBy = "", int limit = 0, object columns = null, params object[] args) {
            string sql = limit > 0 ? "SELECT TOP " + limit + " {0} FROM {1} " : "SELECT {0} FROM {1} ";
            if (!string.IsNullOrEmpty(where))
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            if (!String.IsNullOrEmpty(orderBy))
                sql += orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase) ? orderBy : " ORDER BY " + orderBy;
            return Query(string.Format(sql, string.Join(",", GetColumns(columns)), TableName), args);
        }

        /// <summary>
        /// Returns a dynamic PagedResult. Result properties are Items, TotalPages, and TotalRecords.
        /// </summary>
        public dynamic Paged(string where = "", string orderBy = "", object columns = null, int pageSize = 20, int currentPage =1, params object[] args) {
            dynamic result = new ExpandoObject();
            var countSQL = string.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName);
            if (String.IsNullOrEmpty(orderBy))
                orderBy = PrimaryKeyField;
            var sql = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {1}) AS Row, {0} FROM {2}) AS Paged ", string.Join(",", GetColumns(columns)), orderBy, TableName);
            var pageStart = (currentPage -1) * pageSize;
            sql+= string.Format(" WHERE Row >={0} AND Row <={1}",pageStart, (pageStart + pageSize));
            var pagedWhere = "";
            if (!string.IsNullOrEmpty(where)) {
                if (where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase)) {
                    pagedWhere = Regex.Replace(where, "where ", "and ", RegexOptions.IgnoreCase);
                }
            }
            sql += pagedWhere;
            countSQL += where;
            result.TotalRecords = Scalar(countSQL,args);
            result.TotalPages = result.TotalRecords / pageSize;
            if (result.TotalRecords % pageSize > 0)
                result.TotalPages += 1;
            result.Items = Query(sql, args);
            return result;
        }
        /// <summary>
        /// Returns a single row from the database
        /// </summary>
        public dynamic Single(object key, object columns = null) {
            var sql = string.Format("SELECT {0} FROM {1} WHERE {2} = @0", string.Join(",", GetColumns(columns)), TableName, PrimaryKeyField);
            return Fetch(sql, key).FirstOrDefault();
        }
    }
}