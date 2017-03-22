using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.MySql.TableClasses
{
	public class Product : DynamicModel
	{
		public Product(string providerName) : this(providerName, true)
		{
		}


		public Product(string providerName, bool includeSchema) :
			base(string.Format(TestConstants.WriteTestConnectionStringName, providerName), includeSchema ? "MassiveWriteTests.Products" : "Products", "ProductID")
		{
		}
	}
}
