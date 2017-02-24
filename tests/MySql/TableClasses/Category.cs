using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.MySql.TableClasses
{
	public class Category : DynamicModel
	{
		public Category() : this(true)
		{
		}


		public Category(bool includeSchema) :
			base(TestConstants.WriteTestConnectionStringName, includeSchema ? "MassiveWriteTests.Categories" : "Categories", "CategoryID")
		{
		}
	}
}
