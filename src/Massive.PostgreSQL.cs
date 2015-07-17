using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
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
			p.ParameterName = string.Format(":{0}", cmd.Parameters.Count);
			if(value == null)
			{
				p.Value = DBNull.Value;
			}
			else
			{
				if(value is Guid)
				{
					p.Value = value.ToString();
					p.DbType = DbType.String;
					p.Size = 36;
				}
				else if(value is ExpandoObject)
				{
					var d = (IDictionary<string, object>)value;
					p.Value = d.Values.FirstOrDefault();
				}
				else
				{
					p.Value = value;
				}
				var valueAsString = value as string;
				if(valueAsString != null)
				{
					p.Size = valueAsString.Length > 4000 ? -1 : 4000;
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
		#region Constants
		// Mandatory constants/variables every DB has to define. 
		/// <summary>
		/// The default sequence name for initializing the pk sequence name value in the ctor. 
		/// </summary>
		internal const string _defaultSequenceName = "";
		/// <summary>
		/// Flag to signal whether the sequence retrieval call (if any) is executed before the insert query (true) or after (false). Not a const, to avoid warnings. 
		/// </summary>
		private bool _sequenceValueCallsBeforeMainInsert = true;
		#endregion

		/// <summary>
		/// Gets a default value for the column as defined in the schema.
		/// </summary>
		/// <param name="column">The column.</param>
		/// <returns></returns>
		private dynamic GetDefaultValue(dynamic column)
		{
			string defaultValue = column.COLUMN_DEFAULT;
			if(string.IsNullOrEmpty(defaultValue))
			{
				return null;
			}
			dynamic result;
			switch(defaultValue)
			{
				case "current_date":
				case "(current_date)":
					result = DateTime.Now.Date;
					break;
				case "current_time":
				case "(current_time)":
					result = DateTime.Now.TimeOfDay;
					break;
				default:
					result = defaultValue.Replace("(", "").Replace(")", "");
					break;
			}
			return result;
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
		/// Gets the sql statement to use for obtaining the identity/sequenced value of the last insert.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetIdentityRetrievalScalarStatement()
		{
			return string.IsNullOrEmpty(_primaryKeyFieldSequence) ? string.Empty : string.Format("SELECT nextval('{0}')", _primaryKeyFieldSequence);
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
			return ":" + rawName;
		}


		/// <summary>
		/// Gets the select query pattern, to use for building select queries. The pattern should include as place holders: {0} for project list, {1} for the source (FROM clause).
		/// </summary>
		/// <param name="limit">The limit for the resultset. 0 means no limit.</param>
		/// <param name="whereClause">The where clause. Expected to have a prefix space if not empty</param>
		/// <param name="orderByClause">The order by clause. Expected to have a prefix space if not empty</param>
		/// <returns>
		/// string pattern which is usable to build select queries.
		/// </returns>
		protected virtual string GetSelectQueryPattern(int limit, string whereClause, string orderByClause)
		{
			var normalQuery = string.Format("SELECT {{0}} FROM {{1}}{0}{1}", whereClause, orderByClause);
			if(limit > 0)
			{
				// we have to wrap the query and then apply the rownum filter, as aggregates etc. otherwise aren't going to contain the right values, as they're then applied to the
				// limited set, not the complete set!
				return string.Format("{0} LIMIT {1}", normalQuery, limit);
			}
			return normalQuery;
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
		/// Builds a paging query and count query pair. 
		/// </summary>
		/// <param name="sql">The SQL statement to build the query pair for. Can be left empty, in which case the table name from the schema is used</param>
		/// <param name="primaryKeyField">The primary key field. Used for ordering. If left empty the defined PK field is used</param>
		/// <param name="whereClause">The where clause. Default is empty string.</param>
		/// <param name="orderByClause">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <returns>ExpandoObject with two properties: MainQuery for fetching the specified page and CountQuery for determining the total number of rows in the resultset</returns>
		private dynamic BuildPagingQueryPair(string sql = "", string primaryKeyField = "", string whereClause = "", string orderByClause = "", string columns = "*", int pageSize = 20,
											 int currentPage = 1)
		{
			var orderByClauseFragment = string.IsNullOrEmpty(orderByClause) ? string.Format(" ORDER BY {0}", string.IsNullOrEmpty(primaryKeyField) ? PrimaryKeyField : primaryKeyField)
																			: ReadifyOrderByClause(orderByClause);
			var coreQuery = string.Format(this.GetSelectQueryPattern(0, ReadifyWhereClause(whereClause), orderByClauseFragment), columns, string.IsNullOrEmpty(sql) ? this.TableName : sql);
			dynamic toReturn = new ExpandoObject();
			toReturn.CountQuery = string.Format("SELECT COUNT(*) FROM ({0}) q", coreQuery);
			var pageStart = (currentPage - 1) * pageSize;
			toReturn.MainQuery = string.Format("{0} LIMIT {1} OFFSET {2}", coreQuery, pageSize, (pageStart + pageSize));
			return toReturn;
		}


		#region Properties
		/// <summary>
		/// Provides the default DbProviderFactoryName to the core to create a factory on the fly in generic code.
		/// </summary>
		/// <remarks>If you're using an older version of Npgsql, be sure to manually register the DbProviderFactory in either the machine.config file or in your
		/// application's app/web.config file.</remarks>
		protected virtual string DbProviderFactoryName
		{
			get { return "Npgsql"; }
		}


		/// <summary>
		/// Gets the table schema query to use to obtain meta-data for a given table which is specified as the single parameter.
		/// </summary>
		protected virtual string TableSchemaQuery
		{
			get { return "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = :0"; }
		}
		#endregion
    }
}