using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive;
using Massive.Tests;

namespace PostgreSql.TableClasses
{
	public class Product : DynamicModel
	{
		public Product()
			: this(includeSchema: true)
		{
		}


		public Product(bool includeSchema) :
			base(TestConstants.ReadWriteTestConnection, includeSchema ? "public.products" : "products", "productid", string.Empty, "products_productid_seq")
		{
		}
	}
}
