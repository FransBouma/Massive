using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Tests.TableClasses;
using NUnit.Framework;
using SD.Tools.OrmProfiler.Interceptor;

namespace Massive.Tests
{
	[TestFixture]
	public class SPTests
	{
		[TestFixtureSetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive SqlServer stored procedure tests .NET 4.0");
		}


		[Test]
		public void SP_ReturnValue()
		{
			var db = new DynamicModel(TestConstants.SPTestConnectionStringName);
			var result = db.ExecuteSP("pr_Plus");
			Assert.AreEqual(result.returnValue, 0);
		}


		[Test]
		public void SP_NamedParams()
		{
			var db = new DynamicModel(TestConstants.SPTestConnectionStringName);
			var result = db.ExecuteSP("pr_Plus", new { FirstArg = 1, SecondArg = 5 });
			Assert.AreEqual(result.returnValue, 6);
		}


		[Test]
		/// <remarks>
		/// The type of each param must be implicitly specified by a value, even for output params (if the input object contains
		/// a typed field with value of null the type will be ignored - this is the same behaviour as in other parts of Massive).
		/// </remarks>
		public void SP_ParamDirections()
		{
			var db = new DynamicModel(TestConstants.SPTestConnectionStringName);
			var result = db.ExecuteSP("pr_Test",
									  inParams: new { MyInteger = 4 },
									  outParams: new { OneString = "", ThisDate = DateTime.Now },
									  ioParams: new { AnotherString = "hello" });
			Assert.AreEqual(5, result.returnValue);
			Assert.AreEqual("The result is 5", result.OneString);
			Assert.AreEqual(DateTime.Now.Date, result.ThisDate.Date);
			Assert.AreEqual("The input string was 'hello'", result.AnotherString);
		}
	}
}
