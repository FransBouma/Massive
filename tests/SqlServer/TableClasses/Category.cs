using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.TableClasses
{
	public class Category : DynamicModel
	{
		public Category() : this(true)
		{
		}


		public Category(bool includeSchema) :
			base(TestConstants.WriteTestConnection, includeSchema ? "dbo.Categories" : "Categories", "CategoryID")
		{
		}
	}
}
