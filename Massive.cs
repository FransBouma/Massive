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
        public static IDictionary<string, object> ToDictionary(this object thingy) { return (thingy as IDictionary<string, object>) ?? thingy.ToExpando(); }
    }
    /// <summary> A class that represents a sql command. </summary>
    public class DynamicCommand {
        public string Sql { get; set; }
        public IEnumerable<object> Args { get; set; }
    }
    public class DynamicPagedResult {
        public DynamicPagedResult(int totalRecords, int totalPages, dynamic items) {
            TotalRecords = totalRecords;
            TotalPages = totalPages;
            Items = items;
        }
        public int TotalRecords { get; private set; }
        public int TotalPages { get; private set; }
        public IEnumerable<dynamic> Items { get; private set; }
    }
    /// <summary> A class that wraps your database in Dynamic Funtime </summary>
    public class DynamicDatabase {
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
        private DbCommand CreateDbCommand(DynamicCommand command, DbTransaction tx = null, DbConnection connection = null) {
            var result = _factory.CreateCommand();
            result.Connection = connection;
            result.CommandText = command.Sql;
            result.Transaction = tx;
            result.AddParams(command.Args);
            return result;
        }
        /// <summary> Enumerates the reader yielding the result - thanks to Jeroen Haegebaert </summary>
        public IEnumerable<dynamic> Query(string sql, params object[] args) { return Query(new DynamicCommand { Sql = sql, Args = args, }); }
        /// <summary> Enumerates the reader yielding the result - thanks to Jeroen Haegebaert </summary>
        public IEnumerable<dynamic> Query(DynamicCommand command) {
            using(var conn = OpenConnection())
            using (var rdr = CreateDbCommand(command, connection: conn).ExecuteReader(CommandBehavior.CloseConnection)) {
                while (rdr.Read()) {
                    var d = (IDictionary<string, object>)new ExpandoObject();
                    for (var i = 0; i < rdr.FieldCount; i++) d.Add(rdr.GetName(i), rdr[i]);
                    yield return d;
                }
            }
        }
        /// <summary>  Runs a query against the database. </summary>
        public IList<dynamic> Fetch(string sql, params object[] args) { return Fetch(new DynamicCommand{Sql = sql, Args = args,}).ToList(); }
        /// <summary>  Runs a query against the database. </summary>
        public IList<dynamic> Fetch(DynamicCommand command) { return Query(command).ToList(); }
        /// <summary> Returns a single result </summary>
        public object Scalar(string sql, params object[] args) { return Scalar(new DynamicCommand{Sql = sql, Args = args,}); }
        /// <summary> Returns a single result </summary>
        public object Scalar(DynamicCommand command) {
            using(var conn = OpenConnection()) return CreateDbCommand(command, connection: conn).ExecuteScalar();
        }
        /// <summary> Executes a series of DBCommands in a transaction </summary>
        public int Execute(params DynamicCommand[] commands) { return this.Execute((IEnumerable<DynamicCommand>)commands); }
        /// <summary> Executes a series of DBCommands in a transaction </summary>
        public int Execute(IEnumerable<DynamicCommand> commands) {
            using(var connection = OpenConnection())
            using (var tx = connection.BeginTransaction()) {
                var result = commands.Select(cmd => CreateDbCommand(cmd, tx, connection)).Aggregate(0, (a, cmd) => a + cmd.ExecuteNonQuery());
                tx.Commit();
                return result;
            }
        }
        /// <summary> Gets a table in the database. </summary>
        public DynamicModel GetTable(string tableName, string primaryKeyField = "") { return new DynamicModel(this, tableName, primaryKeyField); }
        /// <summary> Returns a dynamic database scoped to a single connection. </summary>
        public DbConnection OpenConnection() {
            var conn = _factory.CreateConnection();
            conn.ConnectionString = _connectionString;
            conn.Open();
            return conn;
        }
    }
    /// <summary> A class that wraps your database table in Dynamic Funtime </summary>
    public class DynamicModel {
        public DynamicModel(string connectionStringName = "", string tableName = "", string primaryKeyField = "") 
            : this(new DynamicDatabase(connectionStringName), tableName, primaryKeyField) { }
        public DynamicModel(DynamicDatabase database, string tableName = "", string primaryKeyField ="") {
            this.Database = database ?? new DynamicDatabase();
            TableName = tableName == "" ? this.GetType().Name : tableName;
            PrimaryKeyField = string.IsNullOrEmpty(primaryKeyField) ? "ID" : primaryKeyField;
        }
        /// <summary> Gets the database for this model. </summary>
        public DynamicDatabase Database { get; private set; }
        /// <summary> Gets or sets the primary key for this model. </summary>
        public string PrimaryKeyField { get; set; }
        /// <summary> Gets or sets the table name for this model. </summary>
        public string TableName { get; set; }
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
            return Database.Execute(BuildCommandsWithWhitelist(whitelist, things));
        }
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
        /// <summary>
        /// Creates a command for use with transactions - internal stuff mostly, but here for you to play with
        /// </summary>
        public DynamicCommand CreateInsertCommand(object o, object whitelist = null) {
            const string stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2}); SELECT @@IDENTITY AS NewID";
            var items = FilterItems(o, whitelist).ToList();
            if (items.Any()) {
                var keys = string.Join(",", items.Select(item => item.Key));
                var vals = string.Join(",", items.Select((_, i) => "@" + i.ToString()));
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
                var keys = string.Join(",", items.Select((item, i) => string.Format("{0} = @{1} \r\n", item.Key, i)));
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
        private DynamicCommand BuildCommand(string sql, object key = null, object where = null, params object[] args) {
            var command = new DynamicCommand { Sql = sql };
            if (key != null) where = new Dictionary<string, object> {{PrimaryKeyField, key}};
            if(where == null) return command;
            var whereString = where as string;
            if (whereString != null) {
                var whereRegex = new Regex(@"^where ", RegexOptions.IgnoreCase);
                var keyword = whereRegex.IsMatch(sql.Trim()) ? " AND " : " WHERE ";
                command.Sql +=  keyword + whereString.Replace(whereString.Trim(), String.Empty);
                command.Args = (command.Args ?? Enumerable.Empty<object>()).Concat(args);
            } else {
                var dict = where.ToDictionary();
                command.Sql += " WHERE " + String.Join(" AND ", dict.Select((kvp, i) => String.Format("{0} = @{1}", kvp.Key, i)));
                command.Args = dict.Select(kvp => kvp.Value).ToArray();
            }
            return command;
        }
        /// <summary> Removes one or more records from the DB according to the passed-in WHERE </summary>
        public DynamicCommand CreateDeleteCommand(object key = null, object where = null, params object[] args) {
            return BuildCommand(string.Format("DELETE FROM {0}", TableName), key, where, args);
        }
        /// <summary>
        /// Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public object Insert(object o, object whitelist = null) { return Database.Scalar(CreateInsertCommand(o, whitelist)); }
        /// <summary>
        /// Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString
        /// </summary>
        public int Update(object o, object key, object whitelist = null) { return Database.Execute(CreateUpdateCommand(o, key, whitelist)); }
        /// <summary> Removes one or more records from the DB according to the passed-in WHERE </summary>
        public int Delete(object key = null, object where = null, params object[] args) { return Database.Execute(CreateDeleteCommand(key, where, args)); }
        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments,  ordered as specified, limited (TOP) by limit.
        /// </summary>
        public IEnumerable<dynamic> All(object where = null, string orderBy = "", int limit = 0, object columns = null, params object[] args) {
            var sql = String.Format(limit > 0 ? "SELECT TOP " + limit + " {0} FROM {1}" : "SELECT {0} FROM {1}", String.Join(",", GetColumns(columns)), TableName);
            var command = BuildCommand(sql, where: where, args: args);
            if (!String.IsNullOrEmpty(orderBy))
                command.Sql += orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase) ? " " + orderBy : " ORDER BY " + orderBy;
            return Database.Query(command);
        }
        /// <summary> Returns a dynamic PagedResult. Result properties are Items, TotalPages, and TotalRecords. </summary>
        public DynamicPagedResult Paged(object where = null, string orderBy = "", object columns = null, int pageSize = 20, int currentPage =1, params object[] args) {
            var countSql = string.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName);
            if (String.IsNullOrEmpty(orderBy)) orderBy = PrimaryKeyField;
            var sql = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {1}) AS Row, {0} FROM {2}) AS Paged", string.Join(",", GetColumns(columns)), orderBy, TableName);
            var pageStart = (currentPage -1) * pageSize;
            sql+= string.Format(" WHERE Row >={0} AND Row <={1}",pageStart, (pageStart + pageSize));
            var queryCommand = BuildCommand(sql, where: where, args: args);
            var whereCommand = BuildCommand(countSql, where: where, args: args);
            var totalRecords = (int)Database.Scalar(whereCommand);
            return new DynamicPagedResult(totalRecords, (totalRecords + (pageSize - 1)) / pageSize, Database.Query(queryCommand));
        }
        /// <summary> Returns a single row from the database </summary>
        public dynamic Single(object key = null, object where = null, object columns = null) {
            var sql = string.Format("SELECT {0} FROM {1}", string.Join(",", GetColumns(columns)), TableName);
            return Database.Fetch(BuildCommand(sql, key, where)).FirstOrDefault();
        }
    }
}