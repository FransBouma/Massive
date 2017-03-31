using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests
{
	public static class TestConstants
	{
#if COREFX
		public static readonly string ReadTestConnection = "data source=thor.sd.local;initial catalog=AdventureWorks;integrated security=SSPI;persist security info=False;packet size=4096;ProviderName=System.Data.SqlClient;";
		public static readonly string WriteTestConnection = "data source=thor.sd.local;initial catalog=MassiveWriteTests;integrated security=SSPI;persist security info=False;packet size=4096;ProviderName=System.Data.SqlClient;";
#else
		public static readonly string ReadTestConnection = "AdventureWorks.ConnectionString.SQL Server (SqlClient)";
		public static readonly string WriteTestConnection = "MassiveWriteTests.ConnectionString.SQL Server (SqlClient)";
#endif
	}
}
