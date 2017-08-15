using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Massive.Tests.Sqlite.TableClasses;
using SD.Tools.OrmProfiler.Interceptor;

namespace Massive.Tests.Sqlite
{
	/// <summary>
	/// Specific tests for code which is specific to Sqlite. This means there are fewer tests than for SQL Server, as logic that's covered there already doesn't have to be
	/// retested again here, as the tests are meant to see whether a feature works. Tests are designed to touch the code in Massive.Sqlite. 
	/// </summary>
	/// <remarks>Tests use the Chinook example DB (https://chinookdatabase.codeplex.com/releases/view/55681), autonumber variant. 
	/// Writes are done on Playlist, reads on other tables.</remarks>
	[TestFixture]
    public class ReadWriteTests
    {
		[SetUp]
		public void Setup()
		{
			InterceptorCore.Initialize("Sqlite Read/Write tests");
		}


		[Test]
		public void All_NoParameters()
		{
			var albums = new Album();
			var allRows = albums.All().ToList();
			Assert.AreEqual(347, allRows.Count);
			foreach(var a in allRows)
			{
				Console.WriteLine("{0} {1}", a.AlbumId, a.Title);
			}
		}

		[Test]
		public void All_LimitSpecification()
		{
			var albums = new Album();
			var allRows = albums.All(limit: 10).ToList();
			Assert.AreEqual(10, allRows.Count);
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification()
		{
			var albums = new Album();
			var allRows = albums.All(orderBy: "Title DESC", where: "WHERE ArtistId=@0", args: 90).ToList();
			Assert.AreEqual(21, allRows.Count);
			string previous = string.Empty;
			foreach(var r in allRows)
			{
				string current = r.Title;
				Assert.IsTrue(string.IsNullOrEmpty(previous) || string.Compare(previous, current) > 0);
				previous = current;
			}
		}


		[Test]
		public void All_WhereSpecification_OrderBySpecification_LimitSpecification()
		{
			var albums = new Album();
			var allRows = albums.All(limit: 6, orderBy: "Title DESC", where: "ArtistId=@0", args: 90).ToList();
			Assert.AreEqual(6, allRows.Count);
			string previous = string.Empty;
			foreach(var r in allRows)
			{
				string current = r.Title;
				Assert.IsTrue(string.IsNullOrEmpty(previous) || string.Compare(previous, current) > 0);
				previous = current;
			}
		}


		[Test]
		public void Paged_NoSpecification()
		{
			var albums = new Album();
			// no order by, so in theory this is useless. It will order on PK though
			var page2 = albums.Paged(currentPage: 3, pageSize: 13);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(13, pageItems.Count);
			Assert.AreEqual(27, pageItems[0].AlbumId);
			Assert.AreEqual(347, page2.TotalRecords);
		}


		[Test]
		public void Paged_OrderBySpecification()
		{
			var albums = new Album();
			var page2 = albums.Paged(orderBy: "Title DESC", currentPage: 3, pageSize: 13);
			var pageItems = ((IEnumerable<dynamic>)page2.Items).ToList();
			Assert.AreEqual(13, pageItems.Count);
			Assert.AreEqual(174, pageItems[0].AlbumId);
			Assert.AreEqual(347, page2.TotalRecords);
		}


		[Test]
		public void Insert_SingleRow()
		{
			var playlists = new Playlist();
			var inserted = playlists.Insert(new { Name = "MassivePlaylist" });
			Assert.IsTrue(inserted.PlaylistId > 0);
		}


		[TearDown]
		public void CleanUp()
		{
			// delete all rows with ProductName 'Massive Product'. 
			var playlists = new Playlist();
			playlists.Delete(null, "Name=@0", "MassivePlaylist");
		}
		

	    [Test]
	    public void SchemaMetaDataRetrieval()
	    {
		    var pl = new Playlist();
		    var schema = pl.Schema;
		    Assert.IsNotNull(schema);
		    Assert.AreEqual(2, schema.Count());
	    }
	}
}
