using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Massive.Tests.MySql.TableClasses;
using NUnit.Framework;
using SD.Tools.OrmProfiler.Interceptor;

namespace Massive.Tests.MySql
{
	[TestFixture("MySql.Data.MySqlClient")]
	[TestFixture("Devart.Data.MySql")]
	public class ReadTests
	{
		private string ProviderName;

		/// <summary>
		/// Initialise tests for given provider
		/// </summary>
		/// <param name="providerName">Provider name</param>
		public ReadTests(string providerName)
		{
			ProviderName = providerName;
		}

		[TestFixtureSetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive MySql read tests .NET 4.0");
		}


		[Test]
		public void MaxOnFilteredSet()
		{
			var soh = new Film(ProviderName);
			var result = ((dynamic)soh).Max(columns: "film_id", where: "rental_duration>6");
			Assert.AreEqual(988, result);
		}


		[Test]
		public void MaxOnFilteredSet2()
		{
			var film = new Film(ProviderName);
			var result = ((dynamic)film).Max(columns: "film_id", rental_duration: 6);
			Assert.AreEqual(998, result);
		}


		[Test]
		public void EmptyElement_ProtoType()
		{
			var film = new Film(ProviderName);
			dynamic defaults = film.Prototype;
			Assert.IsTrue(defaults.last_update > DateTime.MinValue);
		}


		[Test]
		public void SchemaMetaDataRetrieval()
		{
			var film = new Film(ProviderName);
			var schema = film.Schema;
			Assert.IsNotNull(schema);
			Assert.AreEqual(13, schema.Count());
			Assert.IsTrue(schema.All(v => v.TABLE_NAME == film.TableNameWithoutSchema));
		}


		[Test]
		public void All_NoParameters()
		{
			var film = new Film(ProviderName);
			var allRows = film.All().ToList();
			Assert.AreEqual(1000, allRows.Count);
		}


		[Test]
		public void All_NoParameters_Streaming()
		{
			var film = new Film(ProviderName);
			var allRows = film.All();
			var count = 0;
			foreach(var r in allRows)
			{
				count++;
				Assert.AreEqual(13, ((IDictionary<string, object>)r).Count);        // # of fields fetched should be 13
			}
			Assert.AreEqual(1000, count);
		}


		[Test]
		public void All_LimitSpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(limit: 10).ToList();
			Assert.AreEqual(10, allRows.Count);
		}


		[Test]
		public void All_ColumnSpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(columns: "film_id as FILMID, description, language_id").ToList();
			Assert.AreEqual(1000, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("FILMID"));
			Assert.IsTrue(firstRow.ContainsKey("description"));
			Assert.IsTrue(firstRow.ContainsKey("language_id"));
		}


		[Test]
		public void All_OrderBySpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(orderBy: "rental_duration DESC").ToList();
			Assert.AreEqual(1000, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.rental_duration;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void All_WhereSpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(where: "WHERE rental_duration=@0", args: 5).ToList();
			Assert.AreEqual(191, allRows.Count);
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(orderBy: "film_id DESC", where: "WHERE rental_duration=@0", args: 5).ToList();
			Assert.AreEqual(191, allRows.Count);
			int previous = int.MaxValue;
			foreach(var r in allRows)
			{
				int current = r.film_id;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void All_WhereSpecification_ColumnsSpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(columns: "film_id as FILMID, description, language_id", where: "WHERE rental_duration=@0", args: 5).ToList();
			Assert.AreEqual(191, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("FILMID"));
			Assert.IsTrue(firstRow.ContainsKey("description"));
			Assert.IsTrue(firstRow.ContainsKey("language_id"));
		}


		[Test]
		public void All_WhereSpecification_ColumnsSpecification_LimitSpecification()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(limit: 2, columns: "film_id as FILMID, description, language_id", where: "WHERE rental_duration=@0", args: 5).ToList();
			Assert.AreEqual(2, allRows.Count);
			var firstRow = (IDictionary<string, object>)allRows[0];
			Assert.AreEqual(3, firstRow.Count);
			Assert.IsTrue(firstRow.ContainsKey("FILMID"));
			Assert.IsTrue(firstRow.ContainsKey("description"));
			Assert.IsTrue(firstRow.ContainsKey("language_id"));
		}


		[Test]
		public void All_WhereSpecification_ToDataTable()
		{
			var film = new Film(ProviderName);
			var allRows = film.All(where: "WHERE rental_duration=@0", args: 5).ToList();
			Assert.AreEqual(191, allRows.Count);

			var allRowsAsDataTable = film.All(where: "WHERE rental_duration=@0", args: 5).ToDataTable();
			Assert.AreEqual(allRows.Count, allRowsAsDataTable.Rows.Count);
			for(int i = 0; i < allRows.Count; i++)
			{
				Assert.AreEqual(allRows[i].film_id, allRowsAsDataTable.Rows[i]["film_id"]);
				Assert.AreEqual(5, allRowsAsDataTable.Rows[i]["rental_duration"]);
			}
		}


		[Test]
		public void Find_AllColumns()
		{
			dynamic film = new Film(ProviderName);
			var singleInstance = film.Find(film_id: 43);
			Assert.AreEqual(43, singleInstance.film_id);
		}


		[Test]
		public void Find_OneColumn()
		{
			dynamic film = new Film(ProviderName);
			var singleInstance = film.Find(film_id: 43, columns: "film_id");
			Assert.AreEqual(43, singleInstance.film_id);
			var siAsDict = (IDictionary<string, object>)singleInstance;
			Assert.AreEqual(1, siAsDict.Count);
		}


		[Test]
		public void Get_AllColumns()
		{
			dynamic film = new Film(ProviderName);
			var singleInstance = film.Get(film_id: 43);
			Assert.AreEqual(43, singleInstance.film_id);
		}


		[Test]
		public void First_AllColumns()
		{
			dynamic film = new Film(ProviderName);
			var singleInstance = film.First(film_id: 43);
			Assert.AreEqual(43, singleInstance.film_id);
		}


		[Test]
		public void Single_AllColumns()
		{
			dynamic film = new Film(ProviderName);
			var singleInstance = film.Single(film_id: 43);
			Assert.AreEqual(43, singleInstance.film_id);
		}


		[Test]
		public void Query_AllRows()
		{
			// I have no idea what Conery was drinking at the time, must have been strong stuff ;) Anyway, 'Query' is only useful
			// on a direct derived class of DynamicModel without any table specification. 
			var film = new Film(ProviderName);
			var allRows = film.Query("SELECT * FROM sakila.film").ToList();
			Assert.AreEqual(1000, allRows.Count);
		}


		[Test]
		public void Query_Filter()
		{
			var film = new Film(ProviderName);
			var filteredRows = film.Query("SELECT * FROM sakila.film WHERE rental_duration=@0", 5).ToList();
			Assert.AreEqual(191, filteredRows.Count);
		}


		[Test]
		public void Paged_NoSpecification()
		{
			var film = new Film(ProviderName);
			// no order by, so in theory this is useless. It will order on PK though
			var page2 = film.Paged(currentPage: 2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(1000, page2.TotalRecords);
		}


		[Test]
		public void Paged_OrderBySpecification()
		{
			var film = new Film(ProviderName);
			var page2 = film.Paged(orderBy: "rental_duration DESC", currentPage: 2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(1000, page2.TotalRecords);

			int previous = int.MaxValue;
			foreach(var r in pageItems)
			{
				int current = r.rental_duration;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void Paged_OrderBySpecification_ColumnsSpecification()
		{
			var film = new Film(ProviderName);
			var page2 = film.Paged(columns: "rental_duration, film_id", orderBy: "rental_duration DESC", currentPage: 2, pageSize: 30);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(30, pageItems.Count);
			Assert.AreEqual(1000, page2.TotalRecords);
			var firstRow = (IDictionary<string, object>)pageItems[0];
			Assert.AreEqual(2, firstRow.Count);
			int previous = int.MaxValue;
			foreach(var r in pageItems)
			{
				int current = r.rental_duration;
				Assert.IsTrue(current <= previous);
				previous = current;
			}
		}


		[Test]
		public void Count_NoSpecification()
		{
			var film = new Film(ProviderName);
			var total = film.Count();
			Assert.AreEqual(1000, total);
		}


		[Test]
		public void Count_WhereSpecification()
		{
			var film = new Film(ProviderName);
			var total = film.Count(where: "WHERE rental_duration=@0", args: 5);
			Assert.AreEqual(191, total);
		}


		[Test]
		public void DefaultValue()
		{
			var film = new Film(ProviderName, false);
			var value = film.DefaultValue("last_update");
			Assert.AreEqual(typeof(DateTime), value.GetType());
		}


		[Test]
		public void IsValid_FilmIDCheck()
		{
			dynamic film = new Film(ProviderName);
			var toValidate = film.Find(film_id: 72);
			// is invalid
			Assert.IsFalse(film.IsValid(toValidate));
			Assert.AreEqual(1, film.Errors.Count);

			toValidate = film.Find(film_id: 2);
			// is valid
			Assert.IsTrue(film.IsValid(toValidate));
			Assert.AreEqual(0, film.Errors.Count);
		}


		[Test]
		public void PrimaryKey_Read_Check()
		{
			dynamic film = new Film(ProviderName);
			var toValidate = film.Find(film_id: 45);

			Assert.IsTrue(film.HasPrimaryKey(toValidate));

			var pkValue = film.GetPrimaryKey(toValidate);
			Assert.AreEqual(45, pkValue);
		}
	}
}
