using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PostgreSql.TableClasses;
using SD.Tools.OrmProfiler.Interceptor;

namespace PostgreSql
{
	/// <summary>
	/// Specific tests for code which is specific to Postgresql. This means there are fewer tests than for SQL Server, as logic that's covered there already doesn't have to be
	/// retested again here, as the tests are meant to see whether a feature works. Tests are designed to touch the code in Massive.PostgreSql. 
	/// </summary>
	/// <remarks>Tests use the northwind DB clone for Postgresql. Writes are done on Product, reads on other tables. Tests are compiled against x64 as npgsql installs itself in 
	/// x64's machine.config file by default. Change if required for your setup. </remarks>
	[TestFixture]
    public class ReadWriteTests
    {
		[TestFixtureSetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive Postgresql Read/Write tests");
		}
		

		[Test]
		public void All_NoParameters()
		{
			var customers = new Customer();
			var allRows = customers.All().ToList();
			Assert.AreEqual(91, allRows.Count);
			foreach(var c in allRows)
			{
				Console.WriteLine("{0} {1}", c.customerid, c.companyname);
			}
		}

		[Test]
		public void All_LimitSpecification()
		{
			var customers = new Customer();
			var allRows = customers.All(limit: 10).ToList();
			Assert.AreEqual(10, allRows.Count);
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification()
		{
			var customers = new Customer();
			var allRows = customers.All(orderBy: "companyname DESC", where: "WHERE country=:0", args: "USA").ToList();
			Assert.AreEqual(13, allRows.Count);
			string previous = string.Empty;
			foreach(var r in allRows)
			{
				string current = r.companyname;
				Assert.IsTrue(string.IsNullOrEmpty(previous) || string.Compare(previous, current) > 0);
				previous = current;
			}
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification_LimitSpecification()
		{
			var customers = new Customer();
			var allRows = customers.All(limit: 6, orderBy: "companyname DESC", where: "WHERE country=:0", args: "USA").ToList();
			Assert.AreEqual(6, allRows.Count);
			string previous = string.Empty;
			foreach(var r in allRows)
			{
				string current = r.companyname;
				Assert.IsTrue(string.IsNullOrEmpty(previous) || string.Compare(previous, current) > 0);
				previous = current;
			}
		}


		[Test]
		public void Paged_NoSpecification()
		{
			var customers = new Customer();
			// no order by, so in theory this is useless. It will order on PK though
			var page2 = customers.Paged(currentPage: 2, pageSize: 10);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(10, pageItems.Count);
			Assert.AreEqual(91, page2.TotalRecords);
		}


		[Test]
		public void Paged_OrderBySpecification()
		{
			var customers = new Customer();
			var page2 = customers.Paged(orderBy: "companyname DESC", currentPage: 2, pageSize: 10);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(10, pageItems.Count);
			Assert.AreEqual(91, page2.TotalRecords);
		}


		[Test]
		public void Insert_SingleRow()
		{
			var products = new Product();
			var inserted = products.Insert(new { productname = "Massive Product" });
			Assert.IsTrue(inserted.productid > 0);
		}


		[TestFixtureTearDown]
		public void CleanUp()
		{
			// delete all rows with ProductName 'Massive Product'. 
			var products = new Product();
			products.Delete(null, "productname=:0", "Massive Product");
		}
    }
}
