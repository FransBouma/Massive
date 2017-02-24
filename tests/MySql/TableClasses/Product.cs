using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.MySql.TableClasses
{
	public class Product : DynamicModel
	{
		public Product() : this(includeSchema:true)
		{
		}


		public Product(bool includeSchema) :
			base(TestConstants.WriteTestConnectionStringName, includeSchema ? "mwt.Products" : "Products", "ProductID")
		{
		}
	}
}
