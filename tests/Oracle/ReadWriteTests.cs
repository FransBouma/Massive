using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive.Tests.Oracle.TableClasses;
using NUnit.Framework;
using SD.Tools.OrmProfiler.Interceptor;

namespace Massive.Tests.Oracle
{
	/// <summary>
	/// Specific tests for code which is specific to Oracle. This means there are fewer tests than for SQL Server, as logic that's covered there already doesn't have to be
	/// retested again here, as the tests are meant to see whether a feature works. Tests are designed to touch the code in Massive.Oracle. 
	/// </summary>
	/// <remarks>These tests run on x64 by default, as by default ODP.NET installs x64 only. If you have x86 ODP.NET installed, change the build directive to AnyCPU
	/// in the project settings.<br/>
	/// These tests use the SCOTT test DB shipped by Oracle. Your values may vary though. </remarks>
	[TestFixture]
	public class ReadWriteTests
	{
		[TestFixtureSetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive Oracle read/write tests .NET 4.0");
		}


		[Test]
		public void All_NoParameters()
		{
			var depts = new Department();
			var allRows = depts.All().ToList();
			Assert.AreEqual(60, allRows.Count);
			foreach(var d in allRows)
			{
				Console.WriteLine("{0} {1} {2}", d.DEPTNO, d.DNAME, d.LOC);
			}
		}


		[Test]
		public void All_LimitSpecification()
		{
			var depts = new Department();
			var allRows = depts.All(limit: 10).ToList();
			Assert.AreEqual(10, allRows.Count);
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification()
		{
			var depts = new Department();
			var allRows = depts.All(orderBy: "DEPTNO DESC", where: "WHERE LOC=:0", args: "Nowhere").ToList();
			Assert.AreEqual(9, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.DEPTNO;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification_LimitSpecification()
		{
			var depts = new Department();
			var allRows = depts.All(limit: 6, orderBy: "DEPTNO DESC", where: "WHERE LOC=:0", args: "Nowhere").ToList();
			Assert.AreEqual(6, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.DEPTNO;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void Paged_NoSpecification()
		{
			var depts = new Department();
			// no order by, so in theory this is useless. It will order on PK though
			var page2 = depts.Paged(currentPage: 2, pageSize: 10);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(10, pageItems.Count);
			Assert.AreEqual(60, page2.TotalRecords);
		}


		[Test]
		public void Paged_OrderBySpecification()
		{
			var depts = new Department();
			var page2 = depts.Paged(orderBy: "DEPTNO DESC", currentPage: 2, pageSize: 10);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(10, pageItems.Count);
			Assert.AreEqual(60, page2.TotalRecords);
		}

		[Test]
		public void Paged_SqlSpecification()
		{
			var depts = new Department();
			var page2 = depts.Paged(sql: "SELECT * FROM DEPT", primaryKey: "DEPTNO", pageSize: 10, currentPage: 2);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(10, pageItems.Count);
			Assert.AreEqual(60, page2.TotalRecords);
		}

		[Test]
		public void Insert_SingleRow()
		{
			var depts = new Department();
			var inserted = depts.Insert(new { DNAME = "Massive Dep", LOC = "Beach" });
			Assert.IsTrue(inserted.DEPTNO > 0);
			Assert.AreEqual(1, depts.Delete(inserted.DEPTNO));
		}

		[Test]
		public void Save_SingleRow()
		{
			var depts = new Department();
			dynamic toSave = new {DNAME = "Massive Dep", LOC = "Beach"}.ToExpando();
			var result = depts.Save(toSave);
			Assert.AreEqual(1, result);
			Assert.IsTrue(toSave.DEPTNO > 0);
			Assert.AreEqual(1, depts.Delete(toSave.DEPTNO));
		}

		[Test]
		public void Save_MultipleRows()
		{
			var depts = new Department();
			object[] toSave = new object[]
								   {
									   new {DNAME = "Massive Dep", LOC = "Beach"}.ToExpando(),
									   new {DNAME = "Massive Dep", LOC = "DownTown"}.ToExpando()
								   };
			var result = depts.Save(toSave);
			Assert.AreEqual(2, result);
			foreach(dynamic o in toSave)
			{
				Assert.IsTrue(o.DEPTNO > 0);
			}

			// read them back, update them, save them again, 
			var savedDeps = depts.All(where: "WHERE DEPTNO=:0 OR DEPTNO=:1", args: new object[] {((dynamic)toSave[0]).DEPTNO, ((dynamic)toSave[1]).DEPTNO}).ToList();
			Assert.AreEqual(2, savedDeps.Count);
			savedDeps[0].LOC += "C";
			savedDeps[1].LOC += "C";
			result = depts.Save(toSave);
			Assert.AreEqual(2, result);
			Assert.AreEqual(1, depts.Delete(savedDeps[0].DEPTNO));
			Assert.AreEqual(1, depts.Delete(savedDeps[1].DEPTNO));
		}


		[TestFixtureTearDown]
		public void CleanUp()
		{
			// delete all rows with department name 'Massive Dep'. 
			var depts = new Department();
			depts.Delete(null, "DNAME=:0", "Massive Dep");
		}
	}
}
