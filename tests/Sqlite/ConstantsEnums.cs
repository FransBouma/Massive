using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.Sqlite
{
	public static class TestConstants
	{
#if COREFX
		public static readonly string ReadWriteTestConnection = @"Data Source=C:\Users\frans\Documents\ChinookDatabase1.4_Sqlite\Chinook_Sqlite_AutoIncrementPKs.sqlite;providerName=Microsoft.Data.Sqlite";
#else
		public static readonly string ReadWriteTestConnection = "ReadWriteTests.ConnectionString.SQLite";
#endif
	}
}
