///////////////////////////////////////////////////////////////////////////////////////////////////
// Massive v2.0. Async code. 
///////////////////////////////////////////////////////////////////////////////////////////////////
// Licensed to you under the New BSD License
// http://www.opensource.org/licenses/bsd-license.php
// Massive is copyright (c) 2009-2016 various contributors.
// All rights reserved.
// See for sourcecode, full history and contributors list: https://github.com/FransBouma/Massive
//
// Redistribution and use in source and binary forms, with or without modification, are permitted 
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions and the 
//   following disclaimer.
// - Redistributions in binary form must reproduce the above copyright notice, this list of conditions and 
//   the following disclaimer in the documentation and/or other materials provided with the distribution.
// - The names of its contributors may not be used to endorse or promote products derived from this software 
//   without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS 
// OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY 
// AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY 
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Massive
{
	/// <summary>
	/// Async / await specific code for Massive code. Can be used with all supported databases.
	/// </summary>
	/// <seealso cref="System.Dynamic.DynamicObject" />
	public partial class DynamicModel
	{
		/// <summary>
		/// Async variant of Query(). Executes the query and returns the results in a list. 
		/// </summary>
		/// <param name="sql">The SQL to execute as a command.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// List with the results returned by the database. 
		/// </returns>
		/// <remarks>This is different from the Query method which returns an IEnumerable. The reason is that async methods are reactive, while iterators are
		/// pull based. An async iterator would be an Observable, from the Rx library, but we didn't want to take a dependency on Rx in Massive.</remarks>
		public Task<List<dynamic>> QueryAsync(string sql, params object[] args)
		{
			return QueryAsync(sql, CancellationToken.None, args);
		}


		/// <summary>
		/// Async variant of Query(). Executes the query and returns the results in a list.
		/// </summary>
		/// <param name="sql">The SQL to execute as a command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// List with the results returned by the database.
		/// </returns>
		/// <remarks>
		/// This is different from the Query method which returns an IEnumerable. The reason is that async methods are reactive, while iterators are
		/// pull based. An async iterator would be an Observable, from the Rx library, but we didn't want to take a dependency on Rx in Massive.
		/// </remarks>
		public virtual async Task<List<dynamic>> QueryAsync(string sql, CancellationToken cancellationToken, params object[] args)
		{
			List<dynamic> toReturn;
			using(var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
			{
				toReturn = await QueryAsync(sql, connection, cancellationToken, args).ConfigureAwait(false);
				connection.Close();
			}
			return toReturn;
		}


		/// <summary>
		/// Async variant of Query(). Executes the query and returns the results in a list.
		/// </summary>
		/// <param name="sql">The SQL to execute as a command.</param>
		/// <param name="connection">The connection to use.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// List with the results returned by the database.
		/// </returns>
		/// <remarks>
		/// This is different from the Query method which returns an IEnumerable. The reason is that async methods are reactive, while iterators are
		/// pull based. An async iterator would be an Observable, from the Rx library, but we didn't want to take a dependency on Rx in Massive.
		/// </remarks>
		public Task<List<dynamic>> QueryAsync(string sql, DbConnection connection, params object[] args)
		{
			return QueryAsync(sql, connection, CancellationToken.None, args);
		}


		/// <summary>
		/// Async variant of Query(). Executes the query and returns the results in a list.
		/// </summary>
		/// <param name="sql">The SQL to execute as a command.</param>
		/// <param name="connection">The connection to use.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// List with the results returned by the database.
		/// </returns>
		/// <remarks>
		/// This is different from the Query method which returns an IEnumerable. The reason is that async methods are reactive, while iterators are
		/// pull based. An async iterator would be an Observable, from the Rx library, but we didn't want to take a dependency on Rx in Massive.
		/// </remarks>
		public virtual async Task<List<dynamic>> QueryAsync(string sql, DbConnection connection, CancellationToken cancellationToken, params object[] args)
		{
			var toReturn = new List<dynamic>();
			using(var rdr = await CreateCommand(sql, connection, args).ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
			{
				while(rdr.Read())
				{
					toReturn.Add(rdr.RecordToExpando());
				}
				rdr.Close();
			}
			return toReturn;
		}


		/// <summary>
		/// Async variant of Scalar(). Returns a single result by executing the passed in query + parameters as a scalar query.
		/// </summary>
		/// <param name="sql">The SQL to execute as a scalar command.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// first value returned from the query executed or null of no result was returned by the database.
		/// </returns>
		public Task<object> ScalarAsync(string sql, params object[] args)
		{
			return ScalarAsync(sql, CancellationToken.None, args);
		}
		

		/// <summary>
		/// Async variant of Scalar(). Returns a single result by executing the passed in query + parameters as a scalar query.
		/// </summary>
		/// <param name="sql">The SQL to execute as a scalar command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// first value returned from the query executed or null of no result was returned by the database.
		/// </returns>
		public virtual async Task<object> ScalarAsync(string sql, CancellationToken cancellationToken, params object[] args)
		{
			object result;
			using(var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
			{
				result = await CreateCommand(sql, conn, args).ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				conn.Close();
			}
			return result;
		}

		
		/// <summary>
		/// Async variant of Execute(). Executes the specified command using a new connection
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <returns>the value returned by the database after executing the command. </returns>
		public Task<int> ExecuteAsync(DbCommand command)
		{
			return ExecuteAsync(new[] { command }, CancellationToken.None);
		}


		/// <summary>
		/// Async variant of Execute(). Executes the specified command using a new connection
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// the value returned by the database after executing the command.
		/// </returns>
		public virtual Task<int> ExecuteAsync(DbCommand command, CancellationToken cancellationToken)
		{
			return ExecuteAsync(new[] { command }, cancellationToken);
		}


		/// <summary>
		/// Async variant of Execute(). Executes the specified SQL as a new command using a new connection.
		/// </summary>
		/// <param name="sql">The SQL statement to execute as a command.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// the value returned by the database after executing the command.
		/// </returns>
		public Task<int> ExecuteAsync(string sql, params object[] args)
		{
			return ExecuteAsync(CreateCommand(sql, null, args), CancellationToken.None);
		}


		/// <summary>
		/// Async variant of Execute(). Executes the specified SQL as a new command using a new connection.
		/// </summary>
		/// <param name="sql">The SQL statement to execute as a command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns>
		/// the value returned by the database after executing the command.
		/// </returns>
		public virtual Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken, params object[] args)
		{
			return ExecuteAsync(CreateCommand(sql, null, args), cancellationToken);
		}


		/// <summary>
		/// Async variant of Execute(). Executes a series of DBCommands in a new transaction using a new connection
		/// </summary>
		/// <param name="commands">The commands to execute.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		public Task<int> ExecuteAsync(IEnumerable<DbCommand> commands)
		{
			return ExecuteAsync(commands, CancellationToken.None);
		}


		/// <summary>
		/// Async variant of Execute(). Executes a series of DBCommands in a new transaction using a new connection
		/// </summary>
		/// <param name="commands">The commands to execute.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		public virtual async Task<int> ExecuteAsync(IEnumerable<DbCommand> commands, CancellationToken cancellationToken)
		{
			var result = 0;
			using(var connectionToUse = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
			{
				using(var transactionToUse = connectionToUse.BeginTransaction())
				{
					foreach(var cmd in commands)
					{
						result += await ExecuteDbCommandAsync(cmd, connectionToUse, transactionToUse, cancellationToken).ConfigureAwait(false);
					}
					transactionToUse.Commit();
				}
				connectionToUse.Close();
			}
			return result;
		}


		/// <summary>
		/// Async variant of All(). Returns all records complying with the passed-in WHERE clause and arguments, ordered as specified, limited by limit specified using the DB specific limit system.
		/// </summary>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="limit">The limit. Default is 0 (no limit).</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// streaming enumerable with expandos, one for each row read
		/// </returns>
		public Task<List<dynamic>> AllAsync(string where = "", string orderBy = "", int limit = 0, string columns = "*", params object[] args)
		{
			return AllAsync(CancellationToken.None, where, orderBy, limit, columns, args);
		}


		/// <summary>
		/// Async variant of All(). Returns all records complying with the passed-in WHERE clause and arguments, ordered as specified, limited by limit specified using the DB specific limit system.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="limit">The limit. Default is 0 (no limit).</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// streaming enumerable with expandos, one for each row read
		/// </returns>
		public virtual Task<List<dynamic>> AllAsync(CancellationToken cancellationToken, string where = "", string orderBy = "", int limit = 0, string columns = "*", params object[] args)
		{
			return QueryAsync(string.Format(BuildSelectQueryPattern(where, orderBy, limit), columns, TableName), cancellationToken, args);
		}


		/// <summary>
		/// Async variant of Paged(). Fetches a dynamic PagedResult.
		/// </summary>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.
		/// </returns>
		public Task<dynamic> PagedAsync(string where = "", string orderBy = "", string columns = "*", int pageSize = 20, int currentPage = 1, params object[] args)
		{
			return PagedAsync(CancellationToken.None, where, orderBy, columns, pageSize, currentPage, args);
		}


		/// <summary>
		/// Async variant of Paged(). Fetches a dynamic PagedResult.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.
		/// </returns>
		public virtual Task<dynamic> PagedAsync(CancellationToken cancellationToken, string where = "", string orderBy = "", string columns = "*", int pageSize = 20, 
												int currentPage = 1, params object[] args)
		{
			return BuildPagedResultAsync(cancellationToken, whereClause: where, orderByClause: orderBy, columns: columns, pageSize: pageSize, currentPage: currentPage, args: args);
		}


		/// <summary>
		/// Async variant of Paged(). Fetches a dynamic PagedResult.
		/// </summary>
		/// <param name="sql">The SQL statement to use as query over which resultset is paged.</param>
		/// <param name="primaryKey">The primary key to use for ordering. Can be left empty</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.
		/// </returns>
		public Task<dynamic> PagedAsync(string sql, string primaryKey, string where = "", string orderBy = "", string columns = "*", int pageSize = 20, int currentPage = 1, 
									    params object[] args)
		{
			return PagedAsync(CancellationToken.None, sql, primaryKey, where, orderBy, columns, pageSize, currentPage, args);
		}


		/// <summary>
		/// Async variant of Paged(). Fetches a dynamic PagedResult.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="sql">The SQL statement to use as query over which resultset is paged.</param>
		/// <param name="primaryKey">The primary key to use for ordering. Can be left empty</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="orderBy">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.
		/// </returns>
		public virtual Task<dynamic> PagedAsync(CancellationToken cancellationToken, string sql, string primaryKey, string where = "", string orderBy = "", string columns = "*", 
												int pageSize = 20, int currentPage = 1, params object[] args)
		{
			return BuildPagedResultAsync(cancellationToken, sql, primaryKey, where, orderBy, columns, pageSize, currentPage, args);
		}


		/// <summary>
		/// Async variant of Single(). Returns a single row from the database
		/// </summary>
		/// <param name="where">The where clause.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public Task<dynamic> SingleAsync(string where, params object[] args)
		{
			return SingleAsync(CancellationToken.None, where, args);
		}


		/// <summary>
		/// Async variant of Single(). Returns a single row from the database
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="where">The where clause.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		public virtual async Task<dynamic> SingleAsync(CancellationToken cancellationToken, string where, params object[] args)
		{
			var singleValueSet = await AllAsync(cancellationToken, where, limit: 1, args: args).ConfigureAwait(false);
			return singleValueSet.FirstOrDefault();
		}


		/// <summary>
		/// Async variant of Single(). Returns a single row from the database
		/// </summary>
		/// <param name="key">The pk value.</param>
		/// <param name="columns">The columns.</param>
		/// <returns></returns>
		public Task<dynamic> SingleAsync(object key, string columns = "*")
		{
			return SingleAsync(CancellationToken.None, key, columns);
		}


		/// <summary>
		/// Async variant of Single(). Returns a single row from the database
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="key">The pk value.</param>
		/// <param name="columns">The columns.</param>
		/// <returns></returns>
		public virtual async Task<dynamic> SingleAsync(CancellationToken cancellationToken, object key, string columns = "*")
		{
			var singleValueSet = await AllAsync(cancellationToken, this.GetPkComparisonPredicateQueryFragment(), limit: 1, columns: columns, args: new[] { key }).ConfigureAwait(false);
			return singleValueSet.FirstOrDefault();
		}


		/// <summary>
		/// Async variant of Save(). Executes a set of objects as Insert or Update commands based on their property settings, within a transaction. These objects can be POCOs,
		/// Anonymous, NameValueCollections, or Expandos. Objects with a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
		/// </summary>
		/// <param name="things">The objects to save within a single transaction.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		public Task<int> SaveAsync(params object[] things)
		{
			return SaveAsync(CancellationToken.None, things);
		}


		/// <summary>
		/// Async variant of Save(). Executes a set of objects as Insert or Update commands based on their property settings, within a transaction. These objects can be POCOs,
		/// Anonymous, NameValueCollections, or Expandos. Objects with a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="things">The objects to save within a single transaction.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		public virtual Task<int> SaveAsync(CancellationToken cancellationToken, params object[] things)
		{
			if(things.Any(item => !IsValid(item)))
			{
				throw new InvalidOperationException("Can't save this item: " + string.Join("; ", this.Errors.ToArray()));
			}
			return PerformSaveAsync(false, cancellationToken, things);
		}


		/// <summary>
		/// Async variant of SaveAsNew(). Executes a set of objects as Insert commands, within a transaction. These objects can be POCOs, Anonymous, NameValueCollections, or Expandos.
		/// </summary>
		/// <param name="things">The objects to save within a single transaction.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		/// <remarks>
		/// Sequenced PK fields aren't read back from the DB, meaning the objects in 'things' aren't updated with the sequenced PK values.
		/// </remarks>
		public Task<int> SaveAsNewAsync(params object[] things)
		{
			return SaveAsNewAsync(CancellationToken.None, things);
		}


		/// <summary>
		/// Async variant of SaveAsNew(). Executes a set of objects as Insert commands, within a transaction. These objects can be POCOs, Anonymous, NameValueCollections, or Expandos.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="things">The objects to save within a single transaction.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		/// <remarks>
		/// Sequenced PK fields aren't read back from the DB, meaning the objects in 'things' aren't updated with the sequenced PK values.
		/// </remarks>
		public virtual Task<int> SaveAsNewAsync(CancellationToken cancellationToken, params object[] things)
		{
			if(things.Any(item => !IsValid(item)))
			{
				throw new InvalidOperationException("Can't save this item: " + string.Join("; ", this.Errors.ToArray()));
			}
			return PerformSaveAsync(true, cancellationToken, things);
		}


		/// <summary>
		/// Async variant of InsertAsync(). Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject, a regular old POCO, or a NameValueColletion from a 
		/// Request.Form or Request.QueryString
		/// </summary>
		/// <param name="o">The object to insert.</param>
		/// <returns>
		/// the object inserted as expando. If the PrimaryKeyField is an identity field, it's set in the returned object to the value it received at insert.
		/// </returns>
		public Task<dynamic> InsertAsync(object o)
		{
			return InsertAsync(o, CancellationToken.None);
		}


		/// <summary>
		/// Async variant of InsertAsync(). Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject, a regular old POCO, or a NameValueColletion from a 
		/// Request.Form or Request.QueryString
		/// </summary>
		/// <param name="o">The object to insert.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// the object inserted as expando. If the PrimaryKeyField is an identity field, it's set in the returned object to the value it received at insert.
		/// </returns>
		public virtual async Task<dynamic> InsertAsync(object o, CancellationToken cancellationToken)
		{
			var oAsExpando = o.ToExpando();
			if(!IsValid(oAsExpando))
			{
				throw new InvalidOperationException("Can't insert: " + string.Join("; ", Errors.ToArray()));
			}
			if(BeforeSave(oAsExpando))
			{
				using(var conn = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
				{
					await PerformInsertAsync(conn, oAsExpando, cancellationToken).ConfigureAwait(false);
					Inserted(oAsExpando);
					conn.Close();
				}
				return oAsExpando;
			}
			return null;
		}


		/// <summary>
		/// Async variant of PerformInsert. Performs the insert action of the dynamic specified using the connection specified. Expects the connection to be open.
		/// </summary>
		/// <param name="conn">The connection. Has to be open</param>
		/// <param name="toInsert">The dynamic to insert. Is used to create the sql queries</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>nothing</returns>
		private async Task PerformInsertAsync(DbConnection conn, dynamic toInsert, CancellationToken cancellationToken)
		{
			if(_sequenceValueCallsBeforeMainInsert && !string.IsNullOrEmpty(_primaryKeyFieldSequence))
			{
				var sequenceCmd = CreateCommand(this.GetIdentityRetrievalScalarStatement(), conn);
				((IDictionary<string, object>)toInsert)[this.PrimaryKeyField] = Convert.ToInt32(await sequenceCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			}
			DbCommand cmd = CreateInsertCommand(toInsert);
			cmd.Connection = conn;
			if(_sequenceValueCallsBeforeMainInsert || string.IsNullOrEmpty(_primaryKeyFieldSequence))
			{
				await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// simply batch the identity scalar query to the main insert query and execute them as one scalar query. This will both execute the statement and 
				// return the sequence value
				cmd.CommandText += ";" + this.GetIdentityRetrievalScalarStatement();
				((IDictionary<string, object>)toInsert)[this.PrimaryKeyField] = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
			}
		}


		/// <summary>
		/// Async variant of Update(). Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject, a regular old POCO, or a NameValueCollection from a 
		/// Request.Form or Request.QueryString
		/// </summary>
		/// <param name="o">The object to update</param>
		/// <param name="key">The key value to compare against PrimaryKeyField.</param>
		/// <returns>
		/// the number returned by the database after executing the update command
		/// </returns>
		public Task<int> UpdateAsync(object o, object key)
		{
			return UpdateAsync(o, key, CancellationToken.None);
		}


		/// <summary>
		/// Async variant of Update(). Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject, a regular old POCO, or a NameValueCollection from a 
		/// Request.Form or Request.QueryString
		/// </summary>
		/// <param name="o">The object to update</param>
		/// <param name="key">The key value to compare against PrimaryKeyField.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>
		/// the number returned by the database after executing the update command
		/// </returns>
		public virtual async Task<int> UpdateAsync(object o, object key, CancellationToken cancellationToken)
		{
			var ex = o.ToExpando();
			if(!IsValid(ex))
			{
				throw new InvalidOperationException("Can't Update: " + string.Join("; ", Errors.ToArray()));
			}
			var result = 0;
			if(BeforeSave(ex))
			{
				result = await ExecuteAsync(CreateUpdateCommand(ex, key), cancellationToken).ConfigureAwait(false);
				Updated(ex);
			}
			return result;
		}


		/// <summary>
		/// Async variant of Update(). Updates a all records in the database that match where clause. You can pass in an Anonymous object, an ExpandoObject,
		/// A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString. Where works same same as in All().
		/// </summary>
		/// <param name="o">The object to update</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="args">The parameters used in the where clause.</param>
		/// <returns>
		/// the number returned by the database after executing the update command
		/// </returns>
		public virtual Task<int> UpdateAsync(object o, string where = "1=1", params object[] args)
		{
			return UpdateAsync(CancellationToken.None, o, where, args);
		}


		/// <summary>
		/// Async variant of Update(). Updates a all records in the database that match where clause. You can pass in an Anonymous object, an ExpandoObject,
		/// A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString. Where works same same as in All().
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="o">The object to update</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="args">The parameters used in the where clause.</param>
		/// <returns>
		/// the number returned by the database after executing the update command
		/// </returns>
		public virtual async Task<int> UpdateAsync(CancellationToken cancellationToken, object o, string where = "1=1", params object[] args)
		{
			if(string.IsNullOrWhiteSpace(where))
			{
				return 0;
			}
			var ex = o.ToExpando();
			if(!IsValid(ex))
			{
				throw new InvalidOperationException("Can't Update: " + string.Join("; ", Errors.ToArray()));
			}
			var result = 0;
			if(BeforeSave(ex))
			{
				result = await ExecuteAsync(CreateUpdateWhereCommand(ex, where, args), cancellationToken).ConfigureAwait(false);
				Updated(ex);
			}
			return result;
		}


		/// <summary>
		/// Async variant of Delete(). Deletes one or more records from the DB according to the passed-in where clause/key value.
		/// </summary>
		/// <param name="key">The key. Value to compare with the PrimaryKeyField. If null, <see cref="where" /> is used as the where clause.</param>
		/// <param name="where">The where clause. Can be empty. Ignored if key is set.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns></returns>
		public Task<int> DeleteAsync(object key = null, string where = "", params object[] args)
		{
			return DeleteAsync(CancellationToken.None, key, where, args);
		}


		/// <summary>
		/// Async variant of Delete(). Deletes one or more records from the DB according to the passed-in where clause/key value.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="key">The key. Value to compare with the PrimaryKeyField. If null, <see cref="where" /> is used as the where clause.</param>
		/// <param name="where">The where clause. Can be empty. Ignored if key is set.</param>
		/// <param name="args">The parameter values.</param>
		/// <returns></returns>
		public virtual async Task<int> DeleteAsync(CancellationToken cancellationToken, object key = null, string where = "", params object[] args)
		{
			if(key == null)
			{
				// directly delete on the DB, no fetch of individual element
				return await ExecuteAsync(CreateDeleteCommand(where, null, args), cancellationToken).ConfigureAwait(false);
			}
			var deleted = await SingleAsync(cancellationToken, key).ConfigureAwait(false);
			var result = 0;
			if(BeforeDelete(deleted))
			{
				result = await ExecuteAsync(CreateDeleteCommand(where, key, args), cancellationToken).ConfigureAwait(false);
				Deleted(deleted);
			}
			return result;
		}


		/// <summary>
		/// Executes a Count(*) query on the Table
		/// </summary>
		/// <returns>number of rows returned after executing the count query</returns>
		public Task<int> CountAsync()
		{
			return CountAsync(CancellationToken.None, TableName);
		}


		/// <summary>
		/// Executes a Count(*) query on the Tablename specified using the where clause specified
		/// </summary>
		/// <param name="tableName">Name of the table to execute the count query on. By default it's this table's name</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="args">The parameters used in the where clause.</param>
		/// <returns>
		/// number of rows returned after executing the count query
		/// </returns>
		public Task<int> CountAsync(string tableName = "", string where = "", params object[] args)
		{
			return CountAsync(CancellationToken.None, tableName, where, args);
		}


		/// <summary>
		/// Executes a Count(*) query on the Tablename specified using the where clause specified
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="tableName">Name of the table to execute the count query on. By default it's this table's name</param>
		/// <param name="where">The where clause. Default is empty string. Parameters have to be numbered starting with 0, for each value in args.</param>
		/// <param name="args">The parameters used in the where clause.</param>
		/// <returns>
		/// number of rows returned after executing the count query
		/// </returns>
		public async Task<int> CountAsync(CancellationToken cancellationToken, string tableName = "", string where = "", params object[] args)
		{
			var scalarQueryPattern = this.GetCountRowQueryPattern();
			scalarQueryPattern += ReadifyWhereClause(where);
			var toReturn = await ScalarAsync(string.Format(scalarQueryPattern, string.IsNullOrEmpty(tableName) ? this.TableName : tableName), cancellationToken, args).ConfigureAwait(false);
			return (int)toReturn;
		}


		/// <summary>
		/// Async variant of <see cref="OpenConnection"/>
		/// </summary>
		public Task<DbConnection> OpenConnectionAsync()
		{
			return OpenConnectionAsync(CancellationToken.None);
		}


		/// <summary>
		/// Async variant of <see cref="OpenConnection" />
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns></returns>
		public virtual async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
		{
			var result = _factory.CreateConnection();
			if(result != null)
			{
				result.ConnectionString = _connectionString;
				await result.OpenAsync(cancellationToken).ConfigureAwait(false);
			}
			return result;
		}

		
		/// <summary>
		/// Async variant of PerformSave. Performs the save of the elements in toSave for the Save() and SaveAsNew() methods.
		/// </summary>
		/// <param name="allSavesAreInserts">if set to <c>true</c> it will simply save all elements in toSave using insert queries</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="toSave">The elements to save.</param>
		/// <returns>
		/// the sum of the values returned by the database when executing each command.
		/// </returns>
		private async Task<int> PerformSaveAsync(bool allSavesAreInserts, CancellationToken cancellationToken, params object[] toSave)
		{
			var result = 0;
			using(var connectionToUse = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
			{
				using(var transactionToUse = connectionToUse.BeginTransaction())
				{
					foreach(var o in toSave)
					{
						var oAsExpando = o.ToExpando();
						if(BeforeSave(oAsExpando))
						{
							if(!allSavesAreInserts && HasPrimaryKey(o))
							{
								// update
								result += await ExecuteDbCommandAsync(CreateUpdateCommand(oAsExpando, GetPrimaryKey(o)), connectionToUse, transactionToUse, cancellationToken).ConfigureAwait(false);
								Updated(oAsExpando);
							}
							else
							{
								// insert
								await PerformInsertAsync(connectionToUse, transactionToUse, oAsExpando).ConfigureAwait(false);
								Inserted(oAsExpando);
								result++;
							}
						}
					}
					transactionToUse.Commit();
				}
				connectionToUse.Close();
			}
			return result;
		}


		/// <summary>
		/// Async variant of ExecuteDbCommand. Executes the database command specified
		/// </summary>
		/// <param name="cmd">The command.</param>
		/// <param name="connectionToUse">The connection to use, has to be open.</param>
		/// <param name="transactionToUse">The transaction to use, can be null.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns></returns>
		private Task<int> ExecuteDbCommandAsync(DbCommand cmd, DbConnection connectionToUse, DbTransaction transactionToUse, CancellationToken cancellationToken)
		{
			cmd.Connection = connectionToUse;
			cmd.Transaction = transactionToUse;
			return cmd.ExecuteNonQueryAsync(cancellationToken);
		}


		/// <summary>
		/// Builds the paged result.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="sql">The SQL statement to build the query pair for. Can be left empty, in which case the table name from the schema is used</param>
		/// <param name="primaryKeyField">The primary key field. Used for ordering. If left empty the defined PK field is used</param>
		/// <param name="whereClause">The where clause. Default is empty string.</param>
		/// <param name="orderByClause">The order by clause. Default is empty string.</param>
		/// <param name="columns">The columns to use in the project. Default is '*' (all columns, in table defined order).</param>
		/// <param name="pageSize">Size of the page. Default is 20</param>
		/// <param name="currentPage">The current page. 1-based. Default is 1.</param>
		/// <param name="args">The values to use as parameters.</param>
		/// <returns>
		/// The result of the paged query. Result properties are Items, TotalPages, and TotalRecords.
		/// </returns>
		private async Task<dynamic> BuildPagedResultAsync(CancellationToken cancellationToken, string sql = "", string primaryKeyField = "", string whereClause = "", 
														  string orderByClause = "", string columns = "*", int pageSize = 20, int currentPage = 1, params object[] args)
		{
			var queryPair = this.BuildPagingQueryPair(sql, primaryKeyField, whereClause, orderByClause, columns, pageSize, currentPage);
			dynamic result = new ExpandoObject();
			result.TotalRecords = await ScalarAsync(queryPair.CountQuery, cancellationToken, args).ConfigureAwait(false);
			result.TotalPages = result.TotalRecords / pageSize;
			if(result.TotalRecords % pageSize > 0)
			{
				result.TotalPages += 1;
			}
			result.Items = await QueryAsync(string.Format(queryPair.MainQuery, columns, TableName), cancellationToken, args).ConfigureAwait(false);
			return result;
		}

	}
}
