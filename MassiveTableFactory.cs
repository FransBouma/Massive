using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Massive
{
  public class MassiveTableFactory : DynamicObject
  {
    private const String BasicConnection = "CONNECTION";
    protected virtual String SqlTableScript { get { return "SELECT ccu.TABLE_NAME [Table], COLUMN_NAME [Key], Cast(COLUMNPROPERTY(object_id(tc.TABLE_NAME), COLUMN_NAME, 'IsIdentity') as bit) IsIdentity FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON tc.CONSTRAINT_NAME = ccu.Constraint_name WHERE tc.CONSTRAINT_TYPE = 'Primary Key' order by ccu.TABLE_NAME"; } }
    private IDictionary<String, dynamic> _tableDefinitions;
    private IDictionary<String, DynamicModel> _cachedDefinitions;
    private readonly String _connectionName;

    public MassiveTableFactory(String connectionName){
      _connectionName = connectionName;
      InitializeDatabase();
    }
    protected void InitializeDatabase(){
      _cachedDefinitions = new Dictionary<string, DynamicModel>();
      _tableDefinitions = new Dictionary<String, dynamic>();
      var massive = new DynamicModel(_connectionName);
      var tableDefinitions = massive.Query(SqlTableScript);
      foreach (dynamic definition in tableDefinitions){
        _tableDefinitions[definition.Table.ToUpper()] = definition;
      }
    }
    public override sealed bool TryGetMember(GetMemberBinder binder, out object result){
      var keyName = binder.Name.ToUpper();
      if (_cachedDefinitions.ContainsKey(keyName)){
        result = _cachedDefinitions[keyName];
        return true;
      }
      if (keyName == BasicConnection){
        result = _cachedDefinitions[keyName] = new DynamicModel(_connectionName);
        return true;
      }
      if (_tableDefinitions.ContainsKey(keyName)){
        var definition = _tableDefinitions[keyName];
        result = _cachedDefinitions[keyName] = new DynamicModel(_connectionName, definition.Table, definition.Key, "", definition.IsIdentity);
        return true;
      }
      throw new InvalidOperationException("Table, " + binder.Name + ", does not exist.");
    }
  }
}
