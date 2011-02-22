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
        /// <summary> Extension method for performing an action against all elements. </summary>
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action) { foreach (var item in collection) action(item); }
        /// <summary> Extension method for adding in a bunch of parameters </summary>
        public static void AddParams(this DbCommand cmd, IEnumerable<object> args) { (args ?? Enumerable.Empty<object>()).ForEach(item => AddParam(cmd, item)); }
        /// <summary> Extension for adding single parameter </summary>
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
        /// <summary> Turns an IDataReader to a Dynamic list of things </summary>
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
        /// <summary> Turns the object into an ExpandoObject </summary>
        public static dynamic ToExpando(this object o) {
            if (o.GetType() == typeof(ExpandoObject)) return o; //shouldn't have to... but just in case
            var result = new ExpandoObject();
            var d = result as IDictionary<string, object>; //work with the Expando as a Dictionary
            if (o.GetType() == typeof(NameValueCollection)) {
                var nv = (NameValueCollection)o;
                nv.Cast<string>().Select(key => new KeyValuePair<string, object>(key, nv[key])).ForEach(d.Add);
            } else {
                var props = o.GetType().GetProperties();
                props.ForEach(item => d.Add(item.Name, item.GetValue(o, null)));
            }
            return result;
        }
        /// <summary> Turns the object into a Dictionary </summary>
        public static IDictionary<string, object> ToDictionary(this object thingy) { return (IDictionary<string, object>)thingy.ToExpando(); }
    }
    public interface IDynamicDatabase : IDisposable {
        /// <summary>
        /// Enumerates the reader yielding the result - thanks to Jeroen Haegebaert
        /// A new connection is created for this method.
        /// </summary>
        IEnumerable<object> Query(string sql, params object[] args);
        /// <summary>  Runs a query against the database. A new connection is created for this method. </summary>
        IList<object> Fetch(string sql, params object[] args);
        /// <summary> Returns a single result </summary>
        object Scalar(string sql, params object[] args);
        /// <summary> Executes a series of DBCommands in a transaction </summary>
        int Execute(params DynamicCommand[] commands);
        /// <summary> Executes a series of DBCommands in a transaction </summary>
        int Execute(IEnumerable<DynamicCommand> commands);
    }
    public class DynamicCommand {
        public string Sql { get; set; }
        public IEnumerable<object> Args { get; set; }
    }
    /// <summary> A class that wraps your connection in Dynamic Funtime </summary>
    public class DynamicConnection : IDynamicDatabase {
        private readonly string _connectionString;
        private readonly DbProviderFactory _factory;
        private readonly Lazy<DbConnection> _connection;
        public DynamicConnection(string connectionString, DbProviderFactory factory) {
            _connectionString = connectionString;
            _factory = factory;
            _connection = new Lazy<DbConnection>(OpenConnection);
        }
        private DbConnection Connection { get { return _connection.Value; } }
        private DbConnection OpenConnection() {
            var conn = _factory.CreateConnection();
            conn.ConnectionString = _connectionString;
            conn.Open();
            return conn;
        }
        /// <summary> Creates a DBCommand that you can use for loving your database. </summary>
        private DbCommand CreateCommand(string sql, IEnumerable<object> args, DbTransaction tx = null, DbConnection connection = null) {
            var result = _factory.CreateCommand();
            result.Connection = connection ?? Connection;
            result.CommandText = sql;
            result.Transaction = tx;
            result.AddParams(args);
            return result;
        }
        public IEnumerable<dynamic> Query(string sql, params object[] args) {
            using (var connection = OpenConnection())
            using (var rdr = CreateCommand(sql, args, connection: connection).ExecuteReader(CommandBehavior.CloseConnection)) {
                while (rdr.Read()) {
                    var e = new ExpandoObject();
                    var d = (IDictionary<string, object>) e;
                    for (var i = 0; i < rdr.FieldCount; i++)
                        d.Add(rdr.GetName(i), rdr[i]);
                    yield return e;
                }
            }
        }
        public IList<dynamic> Fetch(string sql, params object[] args) { return Query(sql, args).ToList(); }
        public object Scalar(string sql, params object[] args) { return CreateCommand(sql, args).ExecuteScalar(); }
        public int Execute(params DynamicCommand[] commands) { return this.Execute((IEnumerable<DynamicCommand>)commands); }
        public int Execute(IEnumerable<DynamicCommand> commands) {
            using (var tx = Connection.BeginTransaction()) {
                var result = commands.Select(cmd => CreateCommand(cmd.Sql, cmd.Args, tx)).Aggregate(0, (a, cmd) => a + cmd.ExecuteNonQuery());
                tx.Commit();
                return result;
            }
        }
        public void Dispose() { if(_connection.IsValueCreated) Connection.Dispose(); }
    }
    /// <summary> A class that wraps your database in Dynamic Funtime </summary>
    public class DynamicDatabase : IDynamicDatabase {
        readonly DbProviderFactory _factory;
        readonly string _connectionString;
        public DynamicDatabase(string connectionStringName= "") {
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
        public IEnumerable<dynamic> Query(string sql, params object[] args) { 
            using(var conn = OpenConnection()) { foreach (var item in conn.Query(sql, args)) yield return item; }
        }
        public IList<dynamic> Fetch(string sql, params object[] args) { return Do(conn => conn.Fetch(sql, args)); }
        public object Scalar(string sql, params object[] args) { return Do(conn => conn.Scalar(sql, args)); }
        public int Execute(params DynamicCommand[] commands) { return Execute((IEnumerable<DynamicCommand>)commands); }
        public int Execute(IEnumerable<DynamicCommand> commands) { return Do(conn => conn.Execute(commands)); }
        /// <summary> Gets a table in the database. </summary>
        public DynamicModel GetTable(string tableName, string primaryKeyField = "") { return new DynamicModel(this, tableName, primaryKeyField); }
        /// <summary> Returns a dynamic database scoped to a single connection. </summary>
        public IDynamicDatabase OpenConnection() {
            return new DynamicConnection(_connectionString, _factory);
        }
        private T Do<T>(Func<IDynamicDatabase, T> work) { using (var conn = OpenConnection()) { return work(conn); } }
        public void Dispose() {}
    }
    /// <summary> A class that wraps your database table in Dynamic Funtime </summary>
    public class DynamicModel : IDynamicDatabase {
        private readonly DynamicDatabase database;
        public DynamicModel(string connectionStringName = "", string tableName = "", string primaryKeyField = "") 
            : this(new DynamicDatabase(connectionStringName), tableName, primaryKeyField) { }
        public DynamicModel(DynamicDatabase database, string tableName = "", string primaryKeyField ="") {
            this.database = database ?? new DynamicDatabase();
            TableName = tableName == "" ? this.GetType().Name : tableName;
            PrimaryKeyField = string.IsNullOrEmpty(primaryKeyField) ? "ID" : primaryKeyField;
        }
        public IEnumerable<dynamic> Query(string sql, params object[] args) { return database.Query(sql, args); }
        public IList<dynamic> Fetch(string sql, params object[] args) { return database.Fetch(sql, args); }
        public object Scalar(string sql, params object[] args) { return database.Scalar(sql, args); }
        public int Execute(params DynamicCommand[] commands) { return database.Execute(commands); }
        public int Execute(IEnumerable<DynamicCommand> commands) { return database.Execute(commands); }
        public void Dispose() { database.Dispose(); }
        /// <summary>
        /// Builds a set of Insert and Update commands based on the passed-on objects.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public List<DynamicCommand> BuildCommands(params object[] things) { return BuildCommandsWithWhitelist(null, things); }
        /// <summary>
        /// Builds a set of Insert and Update commands based on the passed-on objects.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public List<DynamicCommand> BuildCommandsWithWhitelist(object whitelist, params object[] things) {
            return things.Select(item => HasPrimaryKey(item) ? CreateUpdateCommand(item,GetPrimaryKey(item),whitelist) : CreateInsertCommand(item,whitelist)).ToList();
        }
        /// <summary>
        /// Executes a set of objects as Insert or Update commands based on their property settings, within a transaction.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public int Save(params object[] things) { return SaveWithWhitelist(null, things); }
        /// <summary>
        /// Executes a set of objects as Insert or Update commands based on their property settings, within a transaction.
        /// These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
        /// With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
        /// </summary>
        public int SaveWithWhitelist(object whitelist, params object[] things) {
            return database.Execute(BuildCommandsWithWhitelist(whitelist, things));
        }
        public string PrimaryKeyField { get; set; }
        /// <summary>
        /// Conventionally introspects the object passed in for a field that 
        /// looks like a PK. If you've named your PrimaryKeyField, this becomes easy
        /// </summary>
        public bool HasPrimaryKey(object o) { return o.ToDictionary().ContainsKey(PrimaryKeyField); }
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
        public DynamicCommand CreateInsertCommand(object o, object whitelist = null) {
            const string stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2})";
            var items = FilterItems(o, whitelist).ToList();
            if (items.Any()) {
                var keys = string.Join(",", items.Select(item => item.Key).ToArray());
                var vals = string.Join(",", items.Select((_, i) => "@" + i.ToString()).ToArray());
                return new DynamicCommand { Sql = string.Format(stub, TableName, keys, vals), Args = items.Select(item => item.Value), };
            }
            throw new InvalidOperationException("Can't parse this object to the database - there are no properties set");
        }
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DynamicCommand CreateUpdateCommand(object o, object key, object whitelist = null) {
            const string stub = "UPDATE {0} SET {1} WHERE {2} = @{3}";
            var items = FilterItems(o, whitelist).Where(item => !item.Key.Equals(PrimaryKeyField, StringComparison.CurrentCultureIgnoreCase) && item.Value != null).ToList();
            if (items.Any()) {
                var keys = string.Join(",", items.Select((item, i) => string.Format("{0} = @{1} \r\n", item.Key, i)).ToArray());
                return new DynamicCommand {
                    Sql = string.Format(stub, TableName, keys, PrimaryKeyField, items.Count),
                    Args = items.Select(item => item.Value).Concat(new[] { key }),
                };
            }
            throw new InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs");
        }
        private static IEnumerable<KeyValuePair<string,object>> FilterItems(object o, object whitelist) {
            IEnumerable<KeyValuePair<string, object>> settings = o.ToDictionary();
            var whitelistValues = GetColumns(whitelist).Select(s => s.Trim());
            if (!string.Equals("*", whitelistValues.FirstOrDefault(), StringComparison.Ordinal))
                settings = settings.Join(whitelistValues, s => s.Key.Trim(), w => w, (s,_) => s, StringComparer.OrdinalIgnoreCase);
            return settings;
        }
        private static IEnumerable<string> GetColumns(object columns) {
            return (columns == null)   ? new[]{"*"} :
                   (columns is string) ? ((string)columns).Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries) : 
                   (columns is Type)   ? ((Type)columns).GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance).Select(prop => prop.Name)
                                       : (columns as IEnumerable<string>) ?? columns.ToDictionary().Select(kvp => kvp.Key);
        }
        /// <summary> Removes one or more records from the DB according to the passed-in WHERE </summary>
        public DynamicCommand CreateDeleteCommand(string where = "", object key = null, params object[] args) {
            var sql = string.Format("DELETE FROM {0} ", TableName);
            if (key != null) {
                sql += string.Format("WHERE {0}=@0", PrimaryKeyField);
            } else if (!string.IsNullOrEmpty(where)) {
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            } 
            return new DynamicCommand{ Sql = sql, Args = key == null ? null : new []{key} };
        }
        /// <summary>
        /// Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public object Insert(object o, object whitelist = null) {
            using (var conn = database.OpenConnection()) {
                conn.Execute(CreateInsertCommand(o, whitelist));
                return conn.Scalar("SELECT @@IDENTITY as newID");
            }
        }
        /// <summary>
        /// Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString
        /// </summary>
        public int Update(object o, object key, object whitelist = null) { return database.Execute(CreateUpdateCommand(o, key, whitelist)); }
        /// <summary> Removes one or more records from the DB according to the passed-in WHERE </summary>
        public int Delete(object key = null, string where = "", params object[] args) { return database.Execute(CreateDeleteCommand(where, key, args)); }
        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public IEnumerable<dynamic> All(string where = "", string orderBy = "", int limit = 0, object columns = null, params object[] args) {
            var sql = limit > 0 ? "SELECT TOP " + limit + " {0} FROM {1} " : "SELECT {0} FROM {1} ";
            if (!string.IsNullOrEmpty(where))
                sql += where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase) ? where : "WHERE " + where;
            if (!String.IsNullOrEmpty(orderBy))
                sql += orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase) ? orderBy : " ORDER BY " + orderBy;
            return database.Query(string.Format(sql, string.Join(",", GetColumns(columns)), TableName), args);
        }
        /// <summary> Returns a dynamic PagedResult. Result properties are Items, TotalPages, and TotalRecords. </summary>
        public dynamic Paged(string where = "", string orderBy = "", object columns = null, int pageSize = 20, int currentPage =1, params object[] args) {
            dynamic result = new ExpandoObject();
            var countSQL = string.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName);
            if (String.IsNullOrEmpty(orderBy))
                orderBy = PrimaryKeyField;
            var sql = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {1}) AS Row, {0} FROM {2}) AS Paged ", string.Join(",", GetColumns(columns)), orderBy, TableName);
            var pageStart = (currentPage -1) * pageSize;
            sql+= string.Format(" WHERE Row >={0} AND Row <={1}",pageStart, (pageStart + pageSize));
            var pagedWhere = "";
            if (!string.IsNullOrEmpty(where)&& where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase)) {
                    pagedWhere = Regex.Replace(where, "where ", "and ", RegexOptions.IgnoreCase);
            }
            sql += pagedWhere;
            countSQL += where;
            result.TotalRecords = database.Scalar(countSQL,args);
            result.TotalPages = result.TotalRecords / pageSize;
            if (result.TotalRecords % pageSize > 0)
                result.TotalPages += 1;
            result.Items = database.Query(sql, args);
            return result;
        }
        /// <summary> Returns a single row from the database </summary>
        public dynamic Single(object key, object columns = null) {
            var sql = string.Format("SELECT {0} FROM {1} WHERE {2} = @0", string.Join(",", GetColumns(columns)), TableName, PrimaryKeyField);
            return database.Fetch(sql, key).FirstOrDefault();
        }
    }
}