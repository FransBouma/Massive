using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive;
using Massive.Tests;

namespace Massive.Tests.Sqlite.TableClasses
{
	public class Album : DynamicModel
	{
		public Album()
			: this(includeSchema: false)
		{
		}


		public Album(bool includeSchema) :
			base(TestConstants.ReadWriteTestConnectionStringName, includeSchema ? "Album" : "Album", "AlbumId", string.Empty, "last_insert_rowid()")
		{
		}
	}
}
