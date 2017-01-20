///////////////////////////////////////////////////////////////////////////////////////////////////
// Massive v2.0. Additional stored procedure support
///////////////////////////////////////////////////////////////////////////////////////////////////
// Licensed to you under the New BSD License
// http://www.opensource.org/licenses/bsd-license.php
// Massive is copyright (c) 2009-2017 various contributors.
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
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Data.Common;
using System.Dynamic;

namespace Massive
{
	/// <summary>
	/// Adds stored procedure return values, parameter names and directions to Massive
	/// </summary>
	public partial class DynamicModel
	{
		#region Constants
		/// <summary>
		/// Name for automatically added returnValue param
		/// </summary>
		private const string _returnValueParamName = "returnValue";
		#endregion


		/// <summary>
		/// Execute stored procedure with optional directional params.
		/// Does not process any result sets; for an SP with input params only, first result set may be read by using DynamicModel.Query instead.
		/// For each set of params, you can pass in an Anonymous object, an ExpandoObject, a regular old POCO, or a NameValueCollection e.g. from a Request.Form or Request.QueryString.
		/// </summary>
		/// <param name="spName">Stored procedure name</param>
		/// <param name="inParams">Input params (optional). Names and values are used.</param>
		/// <param name="outParams">Output params (optional). Names are used. Values are used to determine param type.</param>
		/// <param name="ioParams">Input-output params (optional). Names and values are used.</param>
		/// <param name="result">Dynamic holding 'returnValue' plus output values of any output and input-output params</param>
		public virtual dynamic ExecuteSP(string spName, object inParams = null, object outParams = null, object ioParams = null)
		{
			var iAsExpando = inParams.ToExpando();
			var oAsExpando = outParams.ToExpando();
			var ioAsExpando = ioParams.ToExpando();
			using(var conn = OpenConnection())
			{
				var result = PerformExecuteSP(conn, null, spName, iAsExpando, oAsExpando, ioAsExpando);
				conn.Close();
				return result;
			}
		}


		/// <summary>
		/// Executes stored procedure, with optional directional params from dynamics
		/// </summary>
		/// <param name="connectionToUse">The connection to use, has to be open.</param>
		/// <param name="transactionToUse">The transaction to use, can be null.</param>
		/// <param name="spName">Stored procedure name</param>
		/// <param name="inParams">Dynamic containing input params</param>
		/// <param name="outParams">Dynamic containing output params</param>
		/// <param name="ioParams">Dynamic containing input-output params</param>
		/// <param name="result">Dynamic holding 'returnValue' plus output values of output and input-output params</param>
		private dynamic PerformExecuteSP(DbConnection connectionToUse, DbTransaction transactionToUse, string spName, dynamic inParams, dynamic outParams, dynamic ioParams)
		{
			DbCommand cmd = CreateSPCommand(spName, inParams, outParams, ioParams);
			cmd.Connection = connectionToUse;
			cmd.Transaction = transactionToUse;
			dynamic result = new ExpandoObject();
			cmd.ExecuteNonQuery(); // return value of this call not worth returning to user, as per documentation always returns -1 when called on SP
			var dictionary = (IDictionary<string, object>)result;
			foreach(var item in (IDictionary<string, object>)outParams)
			{
				AddParamToExpando(cmd, item.Key, dictionary);
			}
			foreach(var item in (IDictionary<string, object>)ioParams)
			{
				AddParamToExpando(cmd, item.Key, dictionary);
			}
			AddParamToExpando(cmd, _returnValueParamName, dictionary, _returnValueParamName);
			return result;
		}


		/// <summary>
		/// Help put results of SP call into ExpandoObject
		/// </summary>
		/// <param name="cmd">Completed SP command</param>
		/// <param name="name">Unescaped SP param name</param>
		/// <param name="dictionary">Target dictionary</param>
		/// <param name="storeAs">Override default name</param>
		private void AddParamToExpando(DbCommand cmd, string name, IDictionary<string, object> dictionary, string storeAs = null)
		{
			object value = cmd.Parameters["@" + name].Value;
			dictionary.Add(storeAs ?? name, value == DBNull.Value ? null : value);
		}


		/// <summary>
		/// Creates DbCommand to execute stored procedure, with optional directional params from dynamics
		/// </summary>
		/// <param name="spName">Stored procedure name</param>
		/// <param name="inParams">Dynamic containing input params</param>
		/// <param name="outParams">Dynamic containing output params</param>
		/// <param name="ioParams">Dynamic containing input-output params</param>
		/// <returns>Ready to use DbCommand</returns>
		public virtual DbCommand CreateSPCommand(string spName, dynamic inParams = null, dynamic outParams = null, dynamic ioParams = null)
		{
			var cmd = CreateCommand(spName, null);
			cmd.CommandType = CommandType.StoredProcedure;
			foreach(var item in (IDictionary<string, object>)inParams)
			{
				cmd.AddParam(item.Value, item.Key);
			}
			foreach(var item in (IDictionary<string, object>)outParams)
			{
				cmd.AddParam(item.Value, item.Key, ParameterDirection.Output);
			}
			foreach(var item in (IDictionary<string, object>)ioParams)
			{
				cmd.AddParam(item.Value, item.Key, ParameterDirection.InputOutput);
			}
			cmd.AddParam(0, _returnValueParamName, ParameterDirection.ReturnValue);
			return cmd;
		}
	}
}