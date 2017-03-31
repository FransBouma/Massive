using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Massive.Tests.TableClasses;
using NUnit.Framework;
#if !COREFX
using SD.Tools.OrmProfiler.Interceptor;
#endif

namespace Massive.Tests
{
	[TestFixture]
    public class AsyncReadTests
    {
		[TestFixtureSetUp]
		public void Setup()
		{
#if !COREFX
			InterceptorCore.Initialize("Massive SqlServer async read tests .NET 4.0");
#endif
		}


		[Test]
		public async Task AllAsync_NoParameters()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync();
			Assert.AreEqual(31465, allRows.Count);
		}


		[Test]
		public async Task AllAsync_NoParameters_Streaming()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync();
			var count = 0;
			foreach(var r in allRows)
			{
				count++;
				Assert.AreEqual(26, ((IDictionary<string, object>)r).Count);		// # of fields fetched should be 26
			}
			Assert.AreEqual(31465, count);
		}


		[Test]
		public async Task AllAsync_LimitSpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(limit: 10);
			Assert.AreEqual(10, allRows.Count);
		}
		

		[Test]
		public async Task AllAsync_ColumnSpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(columns: "SalesOrderID as SOID, Status, SalesPersonID");
			Assert.AreEqual(31465, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("SOID"));
			Assert.IsTrue(firstRow.ContainsKey("Status"));
			Assert.IsTrue(firstRow.ContainsKey("SalesPersonID"));
		}


		[Test]
		public async Task AllAsync_OrderBySpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(orderBy: "CustomerID DESC");
			Assert.AreEqual(31465, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.CustomerID;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public async Task AllAsync_WhereSpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(where: "WHERE CustomerId=@0", args: 30052);
			Assert.AreEqual(4, allRows.Count);
		}


		[Test]
		public async Task AllAsync_WhereSpecification_OrderBySpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(orderBy: "SalesOrderID DESC", where: "WHERE CustomerId=@0", args: 30052);
			Assert.AreEqual(4, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.SalesOrderID;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}
		

		[Test]
		public async Task AllAsync_WhereSpecification_ColumnsSpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(columns: "SalesOrderID as SOID, Status, SalesPersonID", where: "WHERE CustomerId=@0", args: 30052);
			Assert.AreEqual(4, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("SOID"));
			Assert.IsTrue(firstRow.ContainsKey("Status"));
			Assert.IsTrue(firstRow.ContainsKey("SalesPersonID"));
		}


		[Test]
		public async Task AllAsync_WhereSpecification_ColumnsSpecification_LimitSpecification()
		{
			var soh = new SalesOrderHeader();
			var allRows = await soh.AllAsync(limit: 2, columns: "SalesOrderID as SOID, Status, SalesPersonID", where: "WHERE CustomerId=@0", args: 30052);
			Assert.AreEqual(2, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("SOID"));
			Assert.IsTrue(firstRow.ContainsKey("Status"));
			Assert.IsTrue(firstRow.ContainsKey("SalesPersonID"));
		}

		[Test]
		public async Task QueryAsync_AllRows()
		{
			// I have no idea what Conery was drinking at the time, must have been strong stuff ;) Anyway, 'Query' is only useful
			// on a direct derived class of DynamicModel without any table specification. 
			var soh = new SalesOrderHeader();
			var allRows = await soh.QueryAsync("SELECT * FROM Sales.SalesOrderHeader");
			Assert.AreEqual(31465, allRows.Count);
		}


		[Test]
		public async Task QueryAsync_Filter()
		{
			var soh = new SalesOrderHeader();
			var filteredRows = await soh.QueryAsync("SELECT * FROM Sales.SalesOrderHeader WHERE CustomerID=@0", 30052);
			Assert.AreEqual(4, filteredRows.Count);
		}


		[Test]
		public async Task PagedAsync_NoSpecification()
		{
			var soh = new SalesOrderHeader();
			// no order by, so in theory this is useless. It will order on PK though
			var page2 = await soh.PagedAsync(currentPage:2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(31465, page2.TotalRecords);
		}


		[Test]
		public async Task PagedAsync_OrderBySpecification()
		{
			var soh = new SalesOrderHeader();
			var page2 = await soh.PagedAsync(orderBy: "CustomerID DESC", currentPage: 2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(31465, page2.TotalRecords);

			int previous = int.MaxValue;
			foreach(var r in pageItems)
			{
				int current = r.CustomerID;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public async Task PagedAsync_OrderBySpecification_ColumnsSpecification()
		{
			var soh = new SalesOrderHeader();
			var page2 = await soh.PagedAsync(columns: "CustomerID, SalesOrderID", orderBy: "CustomerID DESC", currentPage: 2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(31465, page2.TotalRecords);
			var firstRow = (IDictionary<string, object>)pageItems[0];
			Assert.AreEqual(2, firstRow.Count);
			int previous = int.MaxValue;
			foreach(var r in pageItems)
			{
				int current = r.CustomerID;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public async Task CountAsync_NoSpecification()
		{
			var soh = new SalesOrderHeader();
			var total = await soh.CountAsync();
			Assert.AreEqual(31465, total);
		}


		[Test]
		public async Task CountAsync_WhereSpecification()
		{
			var soh = new SalesOrderHeader();
			var total = await soh.CountAsync(where: "WHERE CustomerId=@0", args:30052);
			Assert.AreEqual(4, total);
		}
	}
}
