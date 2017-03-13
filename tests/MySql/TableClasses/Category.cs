using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.MySql.TableClasses
{
	public class Category : DynamicModel
	{
		public Category(string providerName) : this(providerName, true)
		{
		}


		public Category(string providerName, bool includeSchema) :
			base(string.Format(TestConstants.WriteTestConnectionStringName, providerName), includeSchema ? "MassiveWriteTests.Categories" : "Categories", "CategoryID")
		{
		}
	}
}
