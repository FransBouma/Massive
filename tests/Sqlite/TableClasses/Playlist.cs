using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive;
using Massive.Tests;

namespace Massive.Tests.Sqlite.TableClasses
{
	public class Playlist : DynamicModel
	{
		public Playlist()
			: this(includeSchema: false)
		{
		}


		public Playlist(bool includeSchema) :
			base(TestConstants.ReadWriteTestConnection, includeSchema ? "Playlist" : "Playlist", "PlaylistId", string.Empty, "last_insert_rowid()")
		{
		}
	}
}
