using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Massive
{
	/// <summary>
	/// Class which provides extension methods for various ADO.NET objects.
	/// </summary>
	public static partial class ObjectExtensions
	{
		/// <summary>
		/// Extension for adding single parameter. 
		/// </summary>
		/// <param name="cmd">The command to add the parameter to.</param>
		/// <param name="value">The value to add as a parameter to the command.</param>
		public static void AddParam(this DbCommand cmd, object value)
		{
			var p = cmd.CreateParameter();
			p.ParameterName = string.Format("@{0}", cmd.Parameters.Count);
			if(value == null)
			{
				p.Value = DBNull.Value;
			}
			else
			{
				var o = value as ExpandoObject;
				if(o == null)
				{
					p.Value = value;
					var s = value as string;
					if(s != null)
					{
						p.Size = s.Length > 4000 ? -1 : 4000;
					}
				}
				else
				{
					p.Value = ((IDictionary<string, object>)value).Values.FirstOrDefault();
				}
			}
			cmd.Parameters.Add(p);
		}
	}


	/// <summary>
	/// A class that wraps your database table in Dynamic Funtime
	/// </summary>
	public partial class DynamicModel
	{
		/// <summary>
		/// Gets a default value for the column as defined in the schema.
		/// </summary>
		/// <param name="column">The column.</param>
		/// <returns></returns>
		private dynamic GetDefaultValue(dynamic column)
		{
			string defaultValue = column.COLUMN_DEFAULT;
			if(String.IsNullOrEmpty(defaultValue))
			{
				return null;
			}
			dynamic result;
			switch(defaultValue)
			{
				case "getdate()":
				case "(getdate())":
					result = DateTime.Now;
					break;
				case "newid()":
					result = Guid.NewGuid().ToString();
					break;
				default:
					result = defaultValue.Replace("(", "").Replace(")", "");
					break;
			}
			return result;
		}


		/// <summary>
		/// Builds a paging query and count query pair. 
		/// </summary>
		/// <param name="sql">The SQL statement to build the query pair for. Can be left empty, in which case the table name from the schema is used</param>
		/// <param name="primaryKeyField">The primary key field. Used for ordering. If left empty the defined PK field is used</param>
		/// <param name="where">The where clause. Default is empty string.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>ExpandoObject with two properties: MainQuery for fetching the specified page and CountQuery for determining the total number of rows in the resultset</returns>
		private dynamic BuildPagingQueryPair(string sql = "", string primaryKeyField = "", string where = "", string orderBy = "", string columns = "*", int pageSize = 20, 
											  int currentPage = 1, params object[] args)
		{
			var countSQL = string.IsNullOrEmpty(sql) ? string.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName) 
													 : string.Format("SELECT COUNT({0}) FROM ({1}) AS PagedTable", primaryKeyField, sql);
			if(String.IsNullOrEmpty(orderBy))
			{
				orderBy = string.IsNullOrEmpty(primaryKeyField) ? PrimaryKeyField : primaryKeyField;
			}
			if(!string.IsNullOrEmpty(where))
			{
				if(!where.Trim().StartsWith("WHERE", StringComparison.CurrentCultureIgnoreCase))
				{
					where = " WHERE " + where;
				}
			}
			var query = string.Empty;
			if(!string.IsNullOrEmpty(sql))
			{
				query = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {1}) AS Row, {0} FROM ({2}) AS PagedTable {3}) AS Paged ", columns, orderBy, sql, where);
			}
			else
			{
				query = string.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {1}) AS Row, {0} FROM {2} {3}) AS Paged ", columns, orderBy, TableName, where);
			}
			var pageStart = (currentPage - 1) * pageSize;
			query += string.Format(" WHERE Row > {0} AND Row <={1}", pageStart, (pageStart + pageSize));
			countSQL += where;
			dynamic toReturn = new ExpandoObject();
			toReturn.MainQuery = query;
			toReturn.CountQuery = countSQL;
			return toReturn;
		}


		/// <summary>
		/// Gets the aggregate function to use in a scalar query for the fragment specified
		/// </summary>
		/// <param name="aggregateCalled">The aggregate called on the dynamicmodel, which should be converted to a DB function. Expected to be lower case</param>
		/// <returns>the aggregate function to use, or null if no aggregate function is supported for aggregateCalled</returns>
		protected virtual string GetAggregateFunction(string aggregateCalled)
		{
			switch(aggregateCalled)
			{
				case "sum":
					return "SUM";
				case "max":
					return "MAX";
				case "min":
					return "MIN";
				case "avg":
					return "AVG";
				default:
					return null;
			}
		}


		/// <summary>
		/// Gets the name of the sequence to use for the PrimaryKeyField set. If PrimaryKeyFieldIsSequenced is set to true (default), this sequence is used in the Identity retrieval
		/// scalar statement. By default it's 'SCOPE_IDENTITY()'. If you want to use @@IDENTITY, override this method and return "@@IDENTITY" instead.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetSequenceName()
		{
			return "SCOPE_IDENTITY()";
		}


		/// <summary>
		/// Gets the sql statement to use for obtaining the identity value of the last insert.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetIdentityRetrievalScalarStatement()
		{
			return string.Format("SELECT {0} as newID", GetSequenceName());
		}


		/// <summary>
		/// Gets the sql statement pattern for a count row query (count(*)). The pattern should include as place holders: {0} for source (FROM clause).
		/// </summary>
		/// <returns></returns>
		protected virtual string GetCountRowQueryPattern()
		{
			return "SELECT COUNT(*) FROM {0} ";
		}


		/// <summary>
		/// Gets the name of the parameter with the prefix to use in a query, e.g. @rawName or :rawName
		/// </summary>
		/// <param name="rawName">raw name of the parameter, without parameter prefix</param>
		/// <returns>rawName prefixed with the db specific prefix (if any)</returns>
		protected virtual string PrefixParameterName(string rawName)
		{
			return "@" + rawName;
		}

		
		/// <summary>
		/// Gets the select query pattern, to use for building select queries. The pattern should include as place holders: {0} for project list, {1} for the source (FROM clause).
		/// </summary>
		/// <param name="limit">The limit for the resultset. 0 means no limit.</param>
		/// <returns>string pattern which is usable to build select queries.</returns>
		protected virtual string GetSelectQueryPattern(int limit)
		{
			return limit > 0 ? "SELECT TOP " + limit + " {0} FROM {1} " : "SELECT {0} FROM {1} ";
		}


		/// <summary>
		/// Gets the insert query pattern, to use for building insert queries. The pattern should include as place holders: {0} for target, {1} for field list, {2} for parameter list
		/// </summary>
		/// <returns></returns>
		protected virtual string GetInsertQueryPattern()
		{
			return "INSERT INTO {0} ({1}) VALUES ({2})";
		}


		/// <summary>
		/// Gets the update query pattern, to use for building update queries. The pattern should include as placeholders: {0} for target, {1} for field list with sets. Has to have
		/// trailing space
		/// </summary>
		/// <returns></returns>
		protected virtual string GetUpdateQueryPattern()
		{
			return "UPDATE {0} SET {1} ";
		}


		/// <summary>
		/// Gets the delete query pattern, to use for building delete queries. The pattern should include as placeholders: {0} for the target. Has to have trailing space
		/// </summary>
		/// <returns></returns>
		protected virtual string GetDeleteQueryPattern()
		{
			return "DELETE FROM {0} ";
		}


		/// <summary>
		/// Gets the name of the column using the expando object representing the column from the schema
		/// </summary>
		/// <param name="columnFromSchema">The column from schema in the form of an expando.</param>
		/// <returns>the name of the column as defined in the schema</returns>
		protected virtual string GetColumnName(dynamic columnFromSchema)
		{
			return columnFromSchema.COLUMN_NAME;
		}

		
		/// <summary>
		/// Provides the default DbProviderFactoryName to the core to create a factory on the fly in generic code.
		/// </summary>
		private string DbProviderFactoryName
		{
			get { return "System.Data.SqlClient"; }
		}

		/// <summary>
		/// Gets the table schema query to use to obtain meta-data for a given table which is specified as the single parameter.
		/// </summary>
		private string TableSchemaQuery
		{
			get { return "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @0"; }
		}
	}
}
