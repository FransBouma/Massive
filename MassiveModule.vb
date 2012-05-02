'This is a port of Rob Conery's Massive into VB.NET

'I know, your first question is “Why?” but I needed to do it because of how VS 2010 handles 64 bit dlls for Web Projects. 
'To use this you have to  install Microsoft’s Async CTP v3 for VS 2010, (http://www.microsoft.com/download/en/details.aspx?displaylang=en&id=9983) to support the yield results. 
'Be forewarned! 
'The only thing I feel squirrely about is the ToExpando() function, somewhere around line 90, the nv.cast() linq query didn’t work properly with the converted code, so I think I converted it right, but I might be wrong.
  

Imports System
Imports System.Collections.Generic
Imports System.Collections.Specialized
Imports System.Configuration
Imports System.Data
Imports System.Data.Common
Imports System.Object
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Data.SqlClient
Imports System.Text.RegularExpressions
Imports System.Dynamic


Namespace Massive
	Public Module ObjectExtensions
		Sub New()
		End Sub
		''' <summary>
		''' Extension method for adding in a bunch of parameters
		''' </summary>
		<System.Runtime.CompilerServices.Extension()> _
		Public Sub AddParams(cmd As DbCommand, Optional args As Object() = Nothing)
			For Each item As Object In args
				AddParam(cmd, item)
			Next
		End Sub
		''' <summary>
		''' Extension for adding single parameter
		''' </summary>
		<System.Runtime.CompilerServices.Extension()> _
		Public Sub AddParam(cmd As DbCommand, item As Object)
			Dim p = cmd.CreateParameter()
			p.ParameterName = String.Format("@{0}", cmd.Parameters.Count)
			If item Is Nothing Then
				p.Value = DBNull.Value
			Else
				If item.[GetType]() = GetType(Guid) Then
					p.Value = item.ToString()
					p.DbType = DbType.[String]
					p.Size = 4000
				ElseIf item.[GetType]() = GetType(ExpandoObject) Then
					Dim d = DirectCast(item, IDictionary(Of String, Object))
					p.Value = d.Values.FirstOrDefault()
				Else
					p.Value = item
				End If
				If item.[GetType]() = GetType(String) Then
					p.Size = If(DirectCast(item, String).Length > 4000, -1, 4000)
				End If
			End If
			cmd.Parameters.Add(p)
		End Sub
		''' <summary>
		''' Turns an IDataReader to a Object list of things
		''' </summary>
		<System.Runtime.CompilerServices.Extension()> _
		Public Function ToExpandoList(rdr As IDataReader) As List(Of Object)
			Dim result = New List(Of Object)()
			While rdr.Read()
				result.Add(rdr.RecordToExpando())
			End While
			Return result
		End Function
		<System.Runtime.CompilerServices.Extension()> _
		Public Function RecordToExpando(rdr As IDataReader) As Object
			Dim e As Object = New ExpandoObject()
			Dim d = TryCast(e, IDictionary(Of String, Object))
			For i As Integer = 0 To rdr.FieldCount - 1
				d.Add(rdr.GetName(i), If(DBNull.Value.Equals(rdr(i)), Nothing, rdr(i)))
			Next
			Return e
		End Function
		''' <summary>
		''' Turns the object into an ExpandoObject
		''' </summary>
		<System.Runtime.CompilerServices.Extension()> _
		Public Function ToExpando(o As Object) As Object
			Dim result = New ExpandoObject()
			Dim d = TryCast(result, IDictionary(Of String, Object))
			'work with the Expando as a Dictionary
			If o.[GetType]() Is GetType(ExpandoObject) Then
				Return o
			End If
			'shouldn't have to... but just in case
			If o.[GetType]() Is GetType(NameValueCollection) OrElse o.[GetType]().IsSubclassOf(GetType(NameValueCollection)) Then
				Dim nv = DirectCast(o, NameValueCollection)
				nv.Cast(Of String)().[Select](Function(key) New KeyValuePair(Of String, Object)(key, nv(key))).ToList()
				For Each i In nv
					d.Add(i)
				Next
			Else
				Dim props = o.[GetType]().GetProperties()
				For Each item As Object In props
					d.Add(item.Name, item.GetValue(o, Nothing))
				Next
			End If
			Return result
		End Function

		''' <summary>
		''' Turns the object into a Dictionary
		''' </summary>
		<System.Runtime.CompilerServices.Extension()> _
		Public Function ToDictionary(thingy As Object) As IDictionary(Of String, Object)
			Return DirectCast(thingy.ToExpando(), IDictionary(Of String, Object))
		End Function
	End Module


	''' <summary>
	''' Convenience class for opening/executing data
	''' </summary>
	Public NotInheritable Class DB
		Private Sub New()
		End Sub
		Public ReadOnly Property Current() As DynamicModel
			Get
				If ConfigurationManager.ConnectionStrings.Count > 1 Then
					Return New DynamicModel(ConfigurationManager.ConnectionStrings(1).Name)
				End If
				Throw New InvalidOperationException("Need a connection string name - can't determine what it is")
			End Get
		End Property
	End Class

	''' <summary>
	''' A class that wraps your database table in Object Funtime
	''' </summary>
	Public Class DynamicModel
		Inherits DynamicObject
		Private _factory As DbProviderFactory
		Private ConnectionString As String
		Public Function Open(connectionStringName As String) As DynamicModel
			Dim dm As Object = New DynamicModel(connectionStringName)
			Return dm
		End Function
		Public Sub New(connectionStringName As String, Optional tableName__1 As String = "", Optional primaryKeyField__2 As String = "", Optional descriptorField As String = "")
			TableName = If(tableName__1 = "", Me.[GetType]().Name, tableName__1)
			PrimaryKeyField = If(String.IsNullOrEmpty(primaryKeyField__2), "ID", primaryKeyField__2)
			Dim _providerName = "System.Data.SqlClient"
			_factory = DbProviderFactories.GetFactory(_providerName)
			ConnectionString = ConfigurationManager.ConnectionStrings(connectionStringName).ConnectionString
		End Sub

		''' <summary>
		''' Creates a new Expando from a Form POST - white listed against the columns in the DB
		''' </summary>
		Public Function CreateFrom(coll As NameValueCollection) As Object
			Dim result As Object = New ExpandoObject()
			Dim dc = DirectCast(result, IDictionary(Of String, Object))
			Dim schema__1 = Schema
			'loop the collection, setting only what's in the Schema
			For Each item As Object In coll.Keys
				Dim exists = schema__1.Any(Function(x) x.COLUMN_NAME.ToLower() = item.ToString().ToLower())
				If exists Then
					Dim key = item.ToString()
					Dim val = coll(key)
					dc.Add(key, val)
				End If
			Next
			Return result
		End Function
		''' <summary>
		''' Gets a default value for the column
		''' </summary>
		Public Function DefaultValue(column As Object) As Object
			Dim result As Object = Nothing
			Dim def As String = column.COLUMN_DEFAULT
			If [String].IsNullOrEmpty(def) Then
				result = Nothing
			ElseIf def = "getdate()" OrElse def = "(getdate())" Then
				result = DateTime.Now.ToShortDateString()
			ElseIf def = "newid()" Then
				result = Guid.NewGuid().ToString()
			Else
				result = def.Replace("(", "").Replace(")", "")
			End If
			Return result
		End Function
		''' <summary>
		''' Creates an empty Expando set with defaults from the DB
		''' </summary>
		Public ReadOnly Property Prototype() As Object
			Get
				Dim result As Object = New ExpandoObject()
				Dim schema__1 = Schema
				For Each column As Object In schema__1
					Dim dc = DirectCast(result, IDictionary(Of String, Object))
					dc.Add(column.COLUMN_NAME, DefaultValue(column))
				Next
				result._Table = Me
				Return result
			End Get
		End Property
		Private _descriptorField As String
		Public ReadOnly Property DescriptorField() As String
			Get
				Return _descriptorField
			End Get
		End Property
		''' <summary>
		''' List out all the schema bits for use with ... whatever
		''' </summary>
		Private _schema As IEnumerable(Of Object)
		Public ReadOnly Property Schema() As IEnumerable(Of Object)
			Get
				If _schema Is Nothing Then
					Dim args = New List(Of Object)()
					args.Add(TableName)

					_schema = Query("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @0", args)
				End If
				Return _schema
			End Get
		End Property

		''' <summary>
		''' Enumerates the reader yielding the result - thanks to Jeroen Haegebaert
		''' </summary>
		Public Overridable Iterator Function Query(sql As String, Optional args As IEnumerable(Of Object) = Nothing) As IEnumerable(Of Object)
			Using conn = OpenConnection()
				Dim rdr = CreateCommand(sql, conn, args).ExecuteReader()
				For Each o As Object In rdr

					Yield rdr.RecordToExpando()

				Next

			End Using
		End Function
		Public Overridable Iterator Function Query(sql As String, connection As DbConnection, Optional args As Object() = Nothing) As IEnumerable(Of Object)
			Using rdr = CreateCommand(sql, connection, args).ExecuteReader()
				While rdr.Read()
					Yield rdr.RecordToExpando()


				End While
			End Using
		End Function
		''' <summary>
		''' Returns a single result
		''' </summary>
		Public Overridable Function Scalar(sql As String, Optional args As Object() = Nothing) As Object
			Dim result As Object = Nothing
			Using conn = OpenConnection()
				result = CreateCommand(sql, conn, args).ExecuteScalar()
			End Using
			Return result
		End Function
		''' <summary>
		''' Creates a DBCommand that you can use for loving your database.
		''' </summary>
		Private Function CreateCommand(sql As String, conn As DbConnection, Optional args As Object() = Nothing) As DbCommand
			Dim result = _factory.CreateCommand()
			result.Connection = conn
			result.CommandText = sql
			If args.Length > 0 Then
				result.AddParams(args)
			End If
			Return result
		End Function
		''' <summary>
		''' Returns and OpenConnection
		''' </summary>
		Public Overridable Function OpenConnection() As DbConnection
			Dim result = _factory.CreateConnection()
			result.ConnectionString = ConnectionString
			result.Open()
			Return result
		End Function
		''' <summary>
		''' Builds a set of Insert and Update commands based on the passed-on objects.
		''' These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
		''' With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
		''' </summary>
		Public Overridable Function BuildCommands(ParamArray things As Object()) As List(Of DbCommand)
			Dim commands = New List(Of DbCommand)()
			For Each item As Object In things
				If HasPrimaryKey(item) Then
					commands.Add(CreateUpdateCommand(item, GetPrimaryKey(item)))
				Else
					commands.Add(CreateInsertCommand(item))
				End If
			Next
			Return commands
		End Function


		Public Overridable Function Execute(command As DbCommand) As Integer
			Return Execute(New DbCommand() {command})
		End Function

		Public Overridable Function Execute(sql As String, Optional args As Object() = Nothing) As Integer
			Return Execute(CreateCommand(sql, Nothing, args))
		End Function
		''' <summary>
		''' Executes a series of DBCommands in a transaction
		''' </summary>
		Public Overridable Function Execute(commands As IEnumerable(Of DbCommand)) As Integer
			Dim result = 0
			Using conn = OpenConnection()
				Using tx = conn.BeginTransaction()
					For Each cmd As Object In commands
						cmd.Connection = conn
						cmd.Transaction = tx
						result += cmd.ExecuteNonQuery()
					Next
					tx.Commit()
				End Using
			End Using
			Return result
		End Function
		Public Overridable Property PrimaryKeyField() As String
			Get
				Return m_PrimaryKeyField
			End Get
			Set(value As String)
				m_PrimaryKeyField = value
			End Set
		End Property
		Private m_PrimaryKeyField As String
		''' <summary>
		''' Conventionally introspects the object passed in for a field that 
		''' looks like a PK. If you've named your PrimaryKeyField, this becomes easy
		''' </summary>
		Public Overridable Function HasPrimaryKey(o As Object) As Boolean
			Return o.ToDictionary().ContainsKey(PrimaryKeyField)
		End Function
		''' <summary>
		''' If the object passed in has a property with the same name as your PrimaryKeyField
		''' it is returned here.
		''' </summary>
		Public Overridable Function GetPrimaryKey(o As Object) As Object
			Dim result As Object = Nothing
			o.ToDictionary().TryGetValue(PrimaryKeyField, result)
			Return result
		End Function
		Public Overridable Property TableName() As String
			Get
				Return m_TableName
			End Get
			Set(value As String)
				m_TableName = value
			End Set
		End Property
		Private m_TableName As String
		''' <summary>
		''' Returns all records complying with the passed-in WHERE clause and arguments, 
		''' ordered as specified, limited (TOP) by limit.
		''' </summary>
		Public Overridable Function All(Optional where As String = "", Optional orderBy As String = "", Optional limit As Integer = 0, _
		  Optional columns As String = "*", Optional args As Object() = Nothing) As IEnumerable(Of Object)
			Dim sql As String = BuildSelect(where, orderBy, limit)
			Return Query(String.Format(sql, columns, TableName), args)
		End Function
		Private Function BuildSelect(where As String, orderBy As String, limit As Integer) As String
			Dim sql As String = If(limit > 0, "SELECT TOP " + limit + " {0} FROM {1} ", "SELECT {0} FROM {1} ")
			If Not String.IsNullOrEmpty(where) Then
				sql += If(where.Trim().StartsWith("where", StringComparison.OrdinalIgnoreCase), where, "WHERE " + where)
			End If
			If Not [String].IsNullOrEmpty(orderBy) Then
				sql += If(orderBy.Trim().StartsWith("order by", StringComparison.OrdinalIgnoreCase), orderBy, " ORDER BY " + orderBy)
			End If
			Return sql
		End Function

		''' <summary>
		''' Returns a Object PagedResult. Result properties are Items, TotalPages, and TotalRecords.
		''' </summary>
		Public Overridable Function Paged(Optional where As String = "", Optional orderBy As String = "", _
		  Optional columns As String = "*", Optional pageSize As Integer = 20, _
		  Optional currentPage As Integer = 1, Optional args As Object() = Nothing) As Object
			Dim result As Object = New ExpandoObject()
			Dim countSQL = String.Format("SELECT COUNT({0}) FROM {1}", PrimaryKeyField, TableName)
			If [String].IsNullOrEmpty(orderBy) Then
				orderBy = PrimaryKeyField
			End If

			If Not String.IsNullOrEmpty(where) Then
				If Not where.Trim().StartsWith("where", StringComparison.OrdinalIgnoreCase) Then
					where = "WHERE " + where
				End If
			End If
			Dim sql = String.Format("SELECT {0} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {2}) AS Row, {0} FROM {3} {4}) AS Paged ", columns, pageSize, orderBy, TableName, where)
			Dim pageStart = (currentPage - 1) * pageSize
			sql += String.Format(" WHERE Row > {0} AND Row <={1}", pageStart, (pageStart + pageSize))
			countSQL += where
			result.TotalRecords = Scalar(countSQL, args)
			result.TotalPages = result.TotalRecords / pageSize
			If result.TotalRecords Mod pageSize > 0 Then
				result.TotalPages += 1
			End If
			result.Items = Query(String.Format(sql, columns, TableName), args)
			Return result
		End Function
		''' <summary>
		''' Returns a single row from the database
		''' </summary>
		Public Overridable Function [Single](where As String, Optional args As Object() = Nothing) As Object
			Dim sql = String.Format("SELECT * FROM {0} WHERE {1}", TableName, where)
			Return Query(sql, args).FirstOrDefault()
		End Function
		''' <summary>
		''' Returns a single row from the database
		''' </summary>
		Public Overridable Function [Single](key As Object, Optional columns As String = "*") As Object
			Dim sql = String.Format("SELECT {0} FROM {1} WHERE {2} = @0", columns, TableName, PrimaryKeyField)
			Return Query(sql, key).FirstOrDefault()
		End Function
		''' <summary>
		''' This will return a string/object dictionary for dropdowns etc
		''' </summary>
		Public Overridable Function KeyValues(Optional orderBy As String = "") As IDictionary(Of String, Object)
			If [String].IsNullOrEmpty(DescriptorField) Then
				Throw New InvalidOperationException("There's no DescriptorField set - do this in your constructor to describe the text value you want to see")
			End If
			Dim sql = String.Format("SELECT {0},{1} FROM {2} ", PrimaryKeyField, DescriptorField, TableName)
			If Not [String].IsNullOrEmpty(orderBy) Then
				sql += "ORDER BY " + orderBy
			End If
			Return DirectCast(Query(sql), IDictionary(Of String, Object))
		End Function

		''' <summary>
		''' This will return an Expando as a Dictionary
		''' </summary>
		Public Overridable Function ItemAsDictionary(item As ExpandoObject) As IDictionary(Of String, Object)
			Return DirectCast(item, IDictionary(Of String, Object))
		End Function
		'Checks to see if a key is present based on the passed-in value
		Public Overridable Function ItemContainsKey(key As String, item As ExpandoObject) As Boolean
			Dim dc = ItemAsDictionary(item)
			Return dc.ContainsKey(key)
		End Function
		''' <summary>
		''' Executes a set of objects as Insert or Update commands based on their property settings, within a transaction.
		''' These objects can be POCOs, Anonymous, NameValueCollections, or Expandos. Objects
		''' With a PK property (whatever PrimaryKeyField is set to) will be created at UPDATEs
		''' </summary>
		Public Overridable Function Save(ParamArray things As Object()) As Integer
			For Each item As Object In things
				If Not IsValid(item) Then
					Throw New InvalidOperationException("Can't save this item: " + [String].Join("; ", Errors.ToArray()))
				End If
			Next
			Dim commands = BuildCommands(things)
			Return Execute(commands)
		End Function
		Public Overridable Function CreateInsertCommand(expando As Object) As DbCommand
			Dim result As DbCommand = Nothing
			Dim settings = DirectCast(expando, IDictionary(Of String, Object))
			Dim sbKeys = New StringBuilder()
			Dim sbVals = New StringBuilder()
			Dim stub = "INSERT INTO {0} ({1}) " & vbCr & vbLf & " VALUES ({2})"
			result = CreateCommand(stub, Nothing)
			Dim counter As Integer = 0
			For Each item As Object In settings
				sbKeys.AppendFormat("{0},", item.Key)
				sbVals.AppendFormat("@{0},", counter.ToString())
				result.AddParam(item.Value)
				counter += 1
			Next
			If counter > 0 Then
				Dim keys = sbKeys.ToString().Substring(0, sbKeys.Length - 1)
				Dim vals = sbVals.ToString().Substring(0, sbVals.Length - 1)
				Dim sql = String.Format(stub, TableName, keys, vals)
				result.CommandText = sql
			Else
				Throw New InvalidOperationException("Can't parse this object to the database - there are no properties set")
			End If
			Return result
		End Function
		''' <summary>
		''' Creates a command for use with transactions - internal stuff mostly, but here for you to play with
		''' </summary>
		Public Overridable Function CreateUpdateCommand(expando As Object, key As Object) As DbCommand
			Dim settings = DirectCast(expando, IDictionary(Of String, Object))
			Dim sbKeys = New StringBuilder()
			Dim stub = "UPDATE {0} SET {1} WHERE {2} = @{3}"
			Dim args = New List(Of Object)()
			Dim result = CreateCommand(stub, Nothing)
			Dim counter As Integer = 0
			For Each item As Object In settings
				Dim val = item.Value
				If Not item.Key.Equals(PrimaryKeyField, StringComparison.OrdinalIgnoreCase) AndAlso item.Value IsNot Nothing Then
					result.AddParam(val)
					sbKeys.AppendFormat("{0} = @{1}, " & vbCr & vbLf, item.Key, counter.ToString())
					counter += 1
				End If
			Next
			If counter > 0 Then
				'add the key
				result.AddParam(key)
				'strip the last commas
				Dim keys = sbKeys.ToString().Substring(0, sbKeys.Length - 4)
				result.CommandText = String.Format(stub, TableName, keys, PrimaryKeyField, counter)
			Else
				Throw New InvalidOperationException("No parsable object was sent in - could not divine any name/value pairs")
			End If
			Return result
		End Function
		''' <summary>
		''' Removes one or more records from the DB according to the passed-in WHERE
		''' </summary>
		Public Overridable Function CreateDeleteCommand(Optional where As String = "", _
		 Optional key As Object = Nothing, Optional args As Object() = Nothing) As DbCommand
			Dim sql = String.Format("DELETE FROM {0} ", TableName)
			If key IsNot Nothing Then
				sql += String.Format("WHERE {0}=@0", PrimaryKeyField)
				args = New Object() {key}
			ElseIf Not String.IsNullOrEmpty(where) Then
				sql += If(where.Trim().StartsWith("where", StringComparison.OrdinalIgnoreCase), where, "WHERE " + where)
			End If
			Return CreateCommand(sql, Nothing, args)
		End Function

		Public Function IsValid(item As Object) As Boolean
			Errors.Clear()
			Validate(item)
			Return Errors.Count = 0
		End Function

		'Temporary holder for error messages
		Public Errors As IList(Of String) = New List(Of String)()
		''' <summary>
		''' Adds a record to the database. You can pass in an Anonymous object, an ExpandoObject,
		''' A regular old POCO, or a NameValueColletion from a Request.Form or Request.QueryString
		''' </summary>
		Public Overridable Function Insert(o As Object) As Object
			Dim ex = o.ToExpando()
			If Not IsValid(ex) Then
				Throw New InvalidOperationException("Can't insert: " + [String].Join("; ", Errors.ToArray()))
			End If
			If BeforeSave(ex) Then
				Using conn As Object = OpenConnection()
					Dim cmd = CreateInsertCommand(ex)
					cmd.Connection = conn
					cmd.ExecuteNonQuery()
					cmd.CommandText = "SELECT @@IDENTITY as newID"
					ex.ID = cmd.ExecuteScalar()
					Inserted(ex)
				End Using
				Return ex
			Else
				Return Nothing
			End If
		End Function
		''' <summary>
		''' Updates a record in the database. You can pass in an Anonymous object, an ExpandoObject,
		''' A regular old POCO, or a NameValueCollection from a Request.Form or Request.QueryString
		''' </summary>
		Public Overridable Function Update(o As Object, key As Object) As Integer
			Dim ex = o.ToExpando()
			If Not IsValid(ex) Then
				Throw New InvalidOperationException("Can't Update: " + [String].Join("; ", Errors.ToArray()))
			End If
			Dim result = 0
			If BeforeSave(ex) Then
				result = Execute(CreateUpdateCommand(ex, key))
				Updated(ex)
			End If
			Return result
		End Function
		''' <summary>
		''' Removes one or more records from the DB according to the passed-in WHERE
		''' </summary>
		Public Function Delete(Optional key As Object = Nothing, Optional where As String = "", Optional args As Object() = Nothing) As Integer
			Dim deleted__1 = Me.[Single](key)
			Dim result = 0
			If BeforeDelete(deleted__1) Then
				result = Execute(CreateDeleteCommand(where:=where, key:=key, args:=args))
				Deleted(deleted__1)
			End If
			Return result
		End Function

		Public Sub DefaultTo(key As String, value As Object, item As Object)
			If Not ItemContainsKey(key, item) Then
				Dim dc = DirectCast(item, IDictionary(Of String, Object))
				dc(key) = value
			End If
		End Sub

		'Hooks
		Public Overridable Sub Validate(item As Object)
		End Sub
		Public Overridable Sub Inserted(item As Object)
		End Sub
		Public Overridable Sub Updated(item As Object)
		End Sub
		Public Overridable Sub Deleted(item As Object)
		End Sub
		Public Overridable Function BeforeDelete(item As Object) As Boolean
			Return True
		End Function
		Public Overridable Function BeforeSave(item As Object) As Boolean
			Return True
		End Function

		'validation methods
		Public Overridable Sub ValidatesPresenceOf(value As Object, Optional message As String = "Required")
			If value Is Nothing Then
				Errors.Add(message)
			End If
			If [String].IsNullOrEmpty(value.ToString()) Then
				Errors.Add(message)
			End If
		End Sub
		'fun methods
		Public Overridable Sub ValidatesNumericalityOf(value As Object, Optional message As String = "Should be a number")
			Dim type = value.[GetType]().Name
			Dim numerics = New String() {"Int32", "Int16", "Int64", "Decimal", "Double", "Single", _
			 "Float"}
			If Not numerics.Contains(type) Then
				Errors.Add(message)
			End If
		End Sub
		Public Overridable Sub ValidateIsCurrency(value As Object, Optional message As String = "Should be money")
			If value Is Nothing Then
				Errors.Add(message)
			End If
			Dim val As Decimal = Decimal.MinValue
			Decimal.TryParse(value.ToString(), val)
			If val = Decimal.MinValue Then
				Errors.Add(message)
			End If


		End Sub
		Public Function Count() As Integer
			Return Count(TableName)
		End Function
		Public Function Count(tableName As String, Optional where As String = "") As Integer
			Return CInt(Scalar("SELECT COUNT(*) FROM " + tableName))
		End Function

		''' <summary>
		''' A helpful query tool
		''' </summary>
		Public Overrides Function TryInvokeMember(binder As InvokeMemberBinder, args As Object(), ByRef result As Object) As Boolean
			'parse the method
			Dim constraints = New List(Of String)()
			Dim counter = 0
			Dim info = binder.CallInfo
			' accepting named args only... SKEET!
			If info.ArgumentNames.Count <> args.Length Then
				Throw New InvalidOperationException("Please use named arguments for this type of query - the column name, orderby, columns, etc")
			End If
			'first should be "FindBy, Last, Single, First"
			Dim op = binder.Name
			Dim columns = " * "
			Dim orderBy As String = String.Format(" ORDER BY {0}", PrimaryKeyField)
			Dim sql As String = ""
			Dim where As String = ""
			Dim whereArgs = New List(Of Object)()

			'loop the named args - see if we have order, columns and constraints
			If info.ArgumentNames.Count > 0 Then

				For i As Integer = 0 To args.Length - 1
					Dim name = info.ArgumentNames(i).ToLower()
					Select Case name
						Case "orderby"
							orderBy = " ORDER BY " + args(i)
							Exit Select
						Case "columns"
							columns = args(i).ToString()
							Exit Select
						Case Else
							constraints.Add(String.Format(" {0} = @{1}", name, counter))
							whereArgs.Add(args(i))
							counter += 1
							Exit Select
					End Select
				Next
			End If

			'Build the WHERE bits
			If constraints.Count > 0 Then
				where = " WHERE " + String.Join(" AND ", constraints.ToArray())
			End If
			'probably a bit much here but... yeah this whole thing needs to be refactored...
			If op.ToLower() = "count" Then
				result = Scalar("SELECT COUNT(*) FROM " + TableName + where, whereArgs.ToArray())
			ElseIf op.ToLower() = "sum" Then
				result = Scalar("SELECT SUM(" + columns + ") FROM " + TableName + where, whereArgs.ToArray())
			ElseIf op.ToLower() = "max" Then
				result = Scalar("SELECT MAX(" + columns + ") FROM " + TableName + where, whereArgs.ToArray())
			ElseIf op.ToLower() = "min" Then
				result = Scalar("SELECT MIN(" + columns + ") FROM " + TableName + where, whereArgs.ToArray())
			ElseIf op.ToLower() = "avg" Then
				result = Scalar("SELECT AVG(" + columns + ") FROM " + TableName + where, whereArgs.ToArray())
			Else

				'build the SQL
				sql = "SELECT TOP 1 " + columns + " FROM " + TableName + where
				Dim justOne = op.StartsWith("First") OrElse op.StartsWith("Last") OrElse op.StartsWith("Get") OrElse op.StartsWith("Single")

				'Be sure to sort by DESC on the PK (PK Sort is the default)
				If op.StartsWith("Last") Then
					orderBy = orderBy + " DESC "
				Else
					'default to multiple
					sql = "SELECT " + columns + " FROM " + TableName + where
				End If

				If justOne Then
					'return a single record
					result = Query(sql + orderBy, whereArgs.ToArray()).FirstOrDefault()
				Else
					'return lots
					result = Query(sql + orderBy, whereArgs.ToArray())
				End If
			End If
			Return True
		End Function
	End Class
End Namespace
