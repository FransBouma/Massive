using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;

namespace Massive {

    /// <summary>
    /// This class makes the mundane tasks of converting dynamic data into some of the 
    /// common types a little easier to bear.  
    /// </summary>

    public static class ConversionHelper {
    
        #region Conversion to DataTable

        /// <summary>
        /// Returns an empty data table based on the underlying table
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static DataTable ToDataTable(this DynamicModel model) {
            var missingTable = "Model must point to valid underlying table";
            if (model == null || string.IsNullOrWhiteSpace(model.TableName) || model.Schema.Count() == 0) {
                throw new Exception(missingTable);
            }

            var schema = model.Schema;

            // if we have made it this far then we should have a valid table name attached to the first
            // item in the schema
            var dataTable = new DataTable(model.TableName);
            foreach (var item in schema) {
                dataTable.Columns.Add(new DataColumn(item.COLUMN_NAME, GetDBType(item.DATA_TYPE)));
            }
            return dataTable;
        }


        /// <summary>
        /// Converts a list of objects to a data table representing the underlying database table
        /// Null objects are mapped to DBNull.Value as required by the DataTable object 
        /// </summary>
        /// <param name="model">DynamicModel object with TableName property mapped correctly</param>
        /// <param name="data">List of objects that need to be mapped to the DataTable</param>
        /// <returns></returns>
        public static DataTable ToDataTable(this DynamicModel model, IEnumerable<object> data) {
            var dataTable = model.ToDataTable();

            foreach (var item in data) {
                IDictionary<string, object> dataDictionary = null;
                if (item is NameValueCollection || item is ExpandoObject) {
                    dataDictionary = (IDictionary<string, object>)item;
                } else {
                    dataDictionary = item.ToDictionary();
                }
                var keys = dataDictionary.Keys;
                var row = dataTable.NewRow();
                foreach (var key in keys) {
                    row[key] = dataDictionary[key] != null ? dataDictionary[key] : DBNull.Value;
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <summary>
        /// Converts a list of dynamics into a DataTable
        /// </summary>
        /// <param name="expandos"></param>
        /// <returns></returns>
        public static DataTable ToDataTable(this IEnumerable<dynamic> expandos) {
            var dt = new DataTable();

            if (expandos != null && expandos.Count() > 0) {
                foreach (var expando in expandos) {
                    IDictionary<string, object> dataDictionary = null;

                    if (expando is NameValueCollection || expando is ExpandoObject) {
                        dataDictionary = (IDictionary<string, object>)expando;
                    } else {
                        dataDictionary = ((object)expando).ToDictionary();
                    }

                    var row = dt.NewRow();
                    foreach (var key in dataDictionary.Keys) {
                        if (!dt.Columns.Contains(key)) {
                            dt.Columns.Add(key, typeof(object));
                        }
                        row[key] = dataDictionary[key] != null ? dataDictionary[key] : DBNull.Value;
                    }

                    dt.Rows.Add(row);
                }

            }
            return dt;
        }

        // there could be more data types but these are the ones used the most
        private static Type GetDBType(string dataType) {
            if (dataType == null) {
                throw new Exception("Must be a valid underlying data type");
            }
            switch (dataType) {
                case "varchar":
                    return typeof(string);
                case "char":
                    return typeof(string);
                case "int":
                    return typeof(int);
                case "bigint":
                    return typeof(long);
                case "smallint":
                    return typeof(short);
                case "decimal":
                    return typeof(decimal);
                case "datetime":
                    return typeof(DateTime);
                case "bit":
                    return typeof(bool);
                case "varbinary":
                    return typeof(byte[]);
                case "uniqueidentifier":
                    return typeof(Guid);
                default:
                    return typeof(object);
            }

        }

        #endregion

        #region Conversion to concrete types

        public static IEnumerable<T> Cast<T>(this IEnumerable<dynamic> expandos) {
            if (expandos != null && expandos.Count() > 0) {
                var list = new List<T>(expandos.Count());

                var type = typeof(T);
                var props = type.GetProperties();

                IDictionary<string, object> kv = null;
                //need to iterate and Convert to Dictionary 
                foreach (dynamic expando in expandos) {
                    var obj = Activator.CreateInstance<T>();
                    //					var obj = FormatterServices.GetUninitializedObject(type);					
                    kv = (IDictionary<string, object>)expando;
                    foreach (var p in props) {
                        if (kv.ContainsKey(p.Name)) {
                            p.SetValue(obj, kv[p.Name], null);
                        }
                    }
                    list.Add(obj);
                }
                return list;
            }

            return null;
        }

        #endregion

        #region SQL Server Bulk Inserts - Should be in a SQL Server specific class
        /// <summary>
        /// Bulk inserts data into SQL Server data using the native SQL Server capabilities.  
        /// If the primary key is identity then that value does not need to be set as the server
        /// handles that behind the scenes.  On the other hand, if the value is set manually then
        /// it is expected that the data will be pre-populated before calling this method
        /// </summary>
        /// <param name="model">This is the DynamicModel based on the database table</param>
        /// <param name="data">Collection of data that will be mapped to database table</param>
        public static void BulkInsert(this DynamicModel model, IEnumerable<object> data) {
            if (model == null || string.IsNullOrWhiteSpace(model.TableName)) {
                throw new Exception("Model must point to valid underlying table for bulk inserts");
            }
            var dataTable = model.ToDataTable(data);
            using (var con = DB.Current.OpenConnection()) {
                if (con.State != ConnectionState.Open) {
                    con.Open();
                }
                using (var bulkCopy = new SqlBulkCopy((SqlConnection)con)) {
                    bulkCopy.DestinationTableName = model.TableName;
                    bulkCopy.WriteToServer(dataTable);
                }
            }
        }

        #endregion
    }
}
