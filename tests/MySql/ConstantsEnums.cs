using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Massive.Tests.MySql
{
	public static class TestConstants
	{
#if COREFX
		public static readonly string ReadTestConnection = "data source=localhost;database=sakila;user id=Massive;password=mt123;persist security info=false;providerName={0}";
		public static readonly string WriteTestConnection = "data source=localhost;database=massivewritetests;user id=Massive;password=mt123;persist security info=false;providerName={0}";
#else
		public static readonly string ReadTestConnection = "Sakila.ConnectionString.MySql ({0})";
		public static readonly string WriteTestConnection = "MassiveWriteTests.ConnectionString.MySql ({0})";
#endif
	}
}
