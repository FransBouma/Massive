using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Specialized;
using System.Data;
using System.Configuration;
using System.Data.Common;

namespace Massive{


    public static class ObjectExtensions{

        /// <summary>
        /// Extension method for adding in a bunch of parameters
        /// </summary>
        public static void AddParams(this DbCommand cmd, object[] args) {
            foreach (var item in args) {
                AddParam(cmd,item);
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
                } else {
                    p.Value = item;
                }
            }
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
                for (int i = 0; i < rdr.FieldCount; i++) {
                    d.Add(rdr.GetName(i), rdr[i]);
                }
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

            //work with the Expando as a Dictionary
            var d = result as IDictionary<string, object>;
            //shouldn't have to... but just in case
            if (o.GetType() == typeof(ExpandoObject)) {
                return o;
            }
            
            //special for form submissions
            if (o.GetType() == typeof(NameValueCollection)) {
                var nv = (NameValueCollection)o;
                //there's a better way to do this... I just don't know what it is...
                foreach (var item in nv.Keys) {
                    var key = item.ToString();
                    d.Add(key, nv.Get(key));
                }

            } else {
                //assume it's a regular lovely object
                var props = o.GetType().GetProperties();
                foreach (var item in props) {
                    var val = item.GetValue(o, null);
                    d.Add(item.Name, val);
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
            var expando = thingy.ToExpando();
            return (IDictionary<string, object>)expando;
        }

        
    }


    /// <summary>
    /// A class that wraps your database table in Dynamic Funtime
    /// </summary>
    public abstract class DynamicModel:DynamicObject {

        DbProviderFactory _factory;
        string _connectionStringName;
        string _connectionString;

        public IList<dynamic> Query(string sql, params object[] args) {
            var result = new List<dynamic>();
            var cmd = CreateCommand(sql, args);
            using (var conn = OpenConnection()) {
                cmd.Connection= conn;
                var rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                result = rdr.ToExpandoList();

                //can't help it - like being explicit
                rdr.Dispose();
                cmd.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Creates a DBCommand that you can use for loving your database.
        /// </summary>
        DbCommand CreateCommand(string sql, params object[] args) {
            DbCommand result = null;
            result = _factory.CreateCommand();
            result.CommandText = sql;
            if(args.Length > 0)
                result.AddParams(args);
            return result;
        }
        DbConnection GetConnection() {
            var connection = _factory.CreateConnection();
            connection.ConnectionString = _connectionString;
            return connection;
        }
        DbConnection OpenConnection() {
            var conn = GetConnection();
            conn.Open();
            return conn;
        }
        /// <summary>
        /// Creates a slick, groovy little wrapper for your action
        /// </summary>
        /// <param name="connectionStringName"></param>
        public DynamicModel(string connectionStringName) {
            //can be overridden by property setting
            TableName = this.GetType().Name;
            _connectionStringName = connectionStringName;

            var providerName = "System.Data.SqlClient";
            if (ConfigurationManager.ConnectionStrings[_connectionStringName] != null) {
                providerName = ConfigurationManager.ConnectionStrings[_connectionStringName].ProviderName ?? "System.Data.SqlClient";
            } else {
                throw new InvalidOperationException("Can't find a connection string with the name '" + _connectionStringName + "'");
            }
            _factory = DbProviderFactories.GetFactory(providerName);
            _connectionString = ConfigurationManager.ConnectionStrings[_connectionStringName].ConnectionString;
        }

        string _primaryKeyField;
        /// <summary>
        /// Conventionally returns a PK field. The default is "ID" if you don't set one
        /// </summary>
        public string PrimaryKeyField {
            get {
                //a bit of convention here
                if (!string.IsNullOrEmpty(_primaryKeyField))
                    return _primaryKeyField;
                
                //oh well - did our best
                return "ID";
            }
            set {
                _primaryKeyField = value;
            }
        }

        /// <summary>
        /// Conventionally introspects the object passed in for a field that 
        /// looks like a PK. If you've named your PrimaryKeyField, this becomes easy
        /// </summary>
        public bool HasPrimaryKey(object o) {

            var result = o.ToDictionary().ContainsKey(PrimaryKeyField);
            return result;
        }
        
        /// <summary>
        /// If the object passed in has a property with the same name as your PrimaryKeyField
        /// it is returned here.
        /// </summary>
        public object GetPrimaryKey(object o) {

            var d = o.ToDictionary();
            object result = null;
            d.TryGetValue(PrimaryKeyField, out result);
            return result;
        }
        
        /// <summary>
        /// The name of the Database table we're working with. This defaults to 
        /// the class name - set this value if it's different
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public dynamic Insert(object o) {
            dynamic result = 0;
            
            if (BeforeInsert(o)) {
                var cmd = CreateInsertCommand(o);
                using (var conn = OpenConnection()) {
                    cmd.Connection = conn;
                    result = cmd.ExecuteScalar();
                    cmd.Dispose();
                    AfterInsert(o);
                }
            }
            return result;
        }

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
            var stub = "INSERT INTO {0} ({1}) \r\n VALUES ({2}); \r\nSELECT SCOPE_IDENTITY()";
            result = CreateCommand(stub);

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
            } else {
                throw new InvalidOperationException("Can't parse this object to the database - there are no properties set");
            }
           
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
            var result = CreateCommand(stub);
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
                var sql = string.Format(stub, TableName, keys, PrimaryKeyField, counter);

                result.CommandText = sql;
            } else {
                //throw
                throw new InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs");
            }
            return result;
        }
        /// <summary>
        /// Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public int Update(object o, object key) {
            //turn this into an expando - we'll need that for the validators
            int result = 0;
            if (BeforeUpdate(o)) {
                using (var conn = OpenConnection()) {
                    var cmd = CreateUpdateCommand(o, key);
                    result = cmd.ExecuteNonQuery();
                    AfterUpdate(o);
                }

            }
            return result;

        }
        /// <summary>
        /// Updates a bunch of records in the database within a transaction. You can pass Anonymous objects, ExpandoObjects,
        /// Regular old POCOs - these all have to have a PK set
        /// </summary>
        public int InsertMany(IEnumerable<object> things) {
            int result = 0;

            using (var conn = OpenConnection()) {
                var tx = conn.BeginTransaction();
                foreach (var item in things) {
                    if (BeforeInsert(item)) {
                        var cmd = CreateInsertCommand(item);
                        cmd.Connection = conn;
                        cmd.Transaction = tx;
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                        AfterInsert(item);
                    }
                    result++;
                }
                tx.Commit();
                conn.Dispose();
                tx.Dispose();
            }

            return result;

        }
        /// <summary>
        /// Updates a bunch of records in the database within a transaction. You can pass Anonymous objects, ExpandoObjects,
        /// Regular old POCOs - these all have to have a PK set
        /// </summary>
        public int UpdateMany(IEnumerable<object> things) {
            //turn this into an expando - we'll need that for the validators
            int result = 0;
            
            using (var conn = OpenConnection()) {
                var tx = conn.BeginTransaction();
                foreach (var item in things) {
                    var pk = GetPrimaryKey(item);
                    if (pk == null)
                        throw new InvalidOperationException("Please be sure to set a value for the primary key");
                    if (BeforeUpdate(item)) {
                        var cmd = CreateUpdateCommand(item, pk);
                        cmd.Connection = conn;
                        cmd.Transaction = tx;
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                        AfterUpdate(item);
                        result++;
                    }

                }
                tx.Commit();
                conn.Dispose();
                tx.Dispose();
            }

            return result;

        }
        /// <summary>
        /// If you're feeling lazy, or are just unsure about whether to use Update or Insert you can use
        /// this method. It will look for a PrimaryKeyField with a set value to determine if this should
        /// be an Insert or Save. You can pass in an Anonymous object, an ExpandoObject,
        /// A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
        /// </summary>
        public dynamic Save(object o) {
            dynamic result = 0;
            if (BeforeSave(o)) {
                var expando = o.ToExpando();
                //decide insert or update
                if (HasPrimaryKey(expando)) {
                    result = Update(expando, GetPrimaryKey(o));

                } else {
                    result = Insert(expando);
                }
                AfterSave(o);
            }
            return result;
        }

        

        /// <summary>
        /// Removes a record from the database
        /// </summary>
        public int Delete(object key) {
            //execute
            var sql = string.Format("DELETE FROM {0} WHERE {1} = @0", TableName, PrimaryKeyField);
            var result = 0;
            var cmd = CreateCommand(sql, key);
            using (var conn = OpenConnection()) {
                cmd.Connection = conn;
                result = cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            return result;
        }

        /// <summary>
        /// Removes one or more records from the DB according to the passed-in WHERE
        /// </summary>
        public dynamic Delete(string where, params object[] args) {
            //execute
            var sql = string.Format("DELETE FROM {0} ", TableName);
            if (!where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase)) {
                sql += "WHERE ";
            }
            sql += where;
            var result = 0;
            var cmd = CreateCommand(sql, args);
            using (var conn = OpenConnection()) {
                cmd.Connection = conn;
                result = cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            return result;
        }

        /// <summary>
        /// Returns all records complying with the passed-in WHERE clause and arguments, 
        /// ordered as specified, limited (TOP) by limit.
        /// </summary>
        public IEnumerable<dynamic> All(string where="", string orderBy="", int limit=0, params object[] args) {

            string sql = "SELECT * FROM {0} ";
            if (limit > 0) {
                sql = "SELECT TOP "+limit+" * FROM {0} ";
            }

            if (!string.IsNullOrEmpty(where)) {
                if (where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase)) {
                    sql += where;
                } else {
                    sql += "WHERE " + where;
                }
            }

            if (!String.IsNullOrEmpty(orderBy)) {
                if (!orderBy.Trim().StartsWith("order by", StringComparison.CurrentCultureIgnoreCase)) {
                    sql += " ORDER BY ";
                }
                sql += orderBy;
            }

            return Query(string.Format(sql, TableName), args);
            
        }

        /// <summary>
        /// Returns a single row from the database
        /// </summary>
        /// <returns>ExpandoObject</returns>
        public dynamic Single(object key) {

            var sql = string.Format("SELECT * FROM {0} WHERE {1} = @0", TableName, PrimaryKeyField);
            var single = Query(sql,key).FirstOrDefault();
            return single;            
        }
        #region hooks
        //hooks for save routines
        public virtual bool BeforeInsert(object o) {
            return true;
        }
        public virtual bool BeforeUpdate(object o) {
            return true;
        }
        public virtual bool BeforeSave(object o) {
            return true;
        }
        public virtual bool BeforeDelete(object key) {
            return true;
        }
        public virtual void AfterInsert(object o) {
        }
        public virtual void AfterUpdate(object o) {
        }
        public virtual void AfterSave(object o) {
        }

        public virtual void AfterDelete(object key) {

        }

        #endregion

    }
}
