using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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
	public class WriteTests
	{
		private string ProviderName;

		/// <summary>
		/// Initialise tests for given provider
		/// </summary>
		/// <param name="providerName">Provider name</param>
		public WriteTests(string providerName)
		{
			ProviderName = providerName;
		}

		[TestFixtureSetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Massive MySql write tests .NET 4.0");
		}


		[Test]
		public void Insert_SingleRow()
		{
			var categories = new Category(ProviderName);
			var inserted = categories.Insert(new { CategoryName = "Cool stuff", Description = "You know... cool stuff! Cool. n. stuff." });
			int insertedCategoryID = inserted.CategoryID;
			Assert.IsTrue(insertedCategoryID > 0);
		}


		[Test]
		public void Insert_MultipleRows()
		{
			var categories = new Category(ProviderName);
			var toInsert = new List<dynamic>();
			toInsert.Add(new { CategoryName = "Cat Insert_MR", Description = "cat 1 desc" });
			toInsert.Add(new { CategoryName = "Cat Insert_MR", Description = "cat 2 desc" });
			Assert.AreEqual(2, categories.SaveAsNew(toInsert.ToArray()));
			var inserted = categories.All(where: "CategoryName=@0", args: (string)toInsert[0].CategoryName).ToList();
			Assert.AreEqual(2, inserted.Count);
			foreach(var c in inserted)
			{
				Assert.IsTrue(c.CategoryID > 0);
				Assert.AreEqual("Cat Insert_MR", c.CategoryName);
			}
		}


		[Test]
		public void Update_SingleRow()
		{
			dynamic categories = new Category(ProviderName);
			// insert something to update first. 
			var inserted = categories.Insert(new { CategoryName = "Cool stuff", Description = "You know... cool stuff! Cool. n. stuff." });
			int insertedCategoryID = inserted.CategoryID;
			Assert.IsTrue(insertedCategoryID > 0);
			// update it, with a better description
			inserted.Description = "This is all jolly marvellous";
			Assert.AreEqual(1, categories.Update(inserted, inserted.CategoryID), "Update should have affected 1 row");
			var updatedRow = categories.Find(CategoryID: inserted.CategoryID);
			Assert.IsNotNull(updatedRow);
			Assert.AreEqual(inserted.CategoryID, updatedRow.CategoryID);
			Assert.AreEqual(inserted.Description, updatedRow.Description);
			// reset description to NULL
			updatedRow.Description = null;
			Assert.AreEqual(1, categories.Update(updatedRow, updatedRow.CategoryID), "Update should have affected 1 row");
			var newUpdatedRow = categories.Find(CategoryID: updatedRow.CategoryID);
			Assert.IsNotNull(newUpdatedRow);
			Assert.AreEqual(updatedRow.CategoryID, newUpdatedRow.CategoryID);
			Assert.AreEqual(updatedRow.Description, newUpdatedRow.Description);
		}


		[Test]
		public void Update_MultipleRows()
		{
			// first insert 2 categories and 4 products, one for each category
			var categories = new Category(ProviderName);
			var insertedCategory1 = categories.Insert(new { CategoryName = "Category 1", Description = "Cat 1 desc" });
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = categories.Insert(new { CategoryName = "Category 2", Description = "Cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			var products = new Product(ProviderName);
			for(int i = 0; i < 4; i++)
			{
				var category = i % 2 == 0 ? insertedCategory1 : insertedCategory2;
				var p = products.Insert(new { ProductName = "Prod" + i, CategoryID = category.CategoryID });
				Assert.IsTrue(p.ProductID > 0);
			}
			var allCat1Products = products.All(where: "WHERE CategoryID=@0", args: category1ID).ToArray();
			Assert.AreEqual(2, allCat1Products.Length);
			foreach(var p in allCat1Products)
			{
				Assert.AreEqual(category1ID, p.CategoryID);
				p.CategoryID = category2ID;
			}
			Assert.AreEqual(2, products.Save(allCat1Products));
		}


		[Test]
		public void Delete_SingleRow()
		{
			// first insert 2 categories
			var categories = new Category(ProviderName);
			var insertedCategory1 = categories.Insert(new { CategoryName = "Cat Delete_SR", Description = "cat 1 desc" });
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = categories.Insert(new { CategoryName = "Cat Delete_SR", Description = "cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			Assert.AreEqual(1, categories.Delete(category1ID), "Delete should affect 1 row");
			var categoriesFromDB = categories.All(where: "CategoryName=@0", args: (string)insertedCategory2.CategoryName).ToList();
			Assert.AreEqual(1, categoriesFromDB.Count);
			Assert.AreEqual(category2ID, categoriesFromDB[0].CategoryID);
		}


		[Test]
		public void Delete_MultiRow()
		{
			// first insert 2 categories
			var categories = new Category(ProviderName);
			var insertedCategory1 = categories.Insert(new { CategoryName = "Cat Delete_MR", Description = "cat 1 desc" });
			int category1ID = insertedCategory1.CategoryID;
			Assert.IsTrue(category1ID > 0);
			var insertedCategory2 = categories.Insert(new { CategoryName = "Cat Delete_MR", Description = "cat 2 desc" });
			int category2ID = insertedCategory2.CategoryID;
			Assert.IsTrue(category2ID > 0);

			Assert.AreEqual(2, categories.Delete(where: "CategoryName=@0", args: (string)insertedCategory1.CategoryName), "Delete should affect 2 rows");
			var categoriesFromDB = categories.All(where: "CategoryName=@0", args: (string)insertedCategory2.CategoryName).ToList();
			Assert.AreEqual(0, categoriesFromDB.Count);
		}


		[TestFixtureTearDown]
		public void CleanUp()
		{
			// no way to call a proc easily at the moment, which should change in the future. 
			var db = new DynamicModel(string.Format(TestConstants.WriteTestConnectionStringName, ProviderName));
			using(var conn = db.OpenConnection())
			{
				var cmd = conn.CreateCommand();
				cmd.CommandText = "pr_clearAll";
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.ExecuteNonQuery();
				conn.Close();
			}
		}
	}
}
