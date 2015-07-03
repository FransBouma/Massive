using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.TableClasses
{
	public class SalesOrderHeader : DynamicModel
	{
		public SalesOrderHeader() : this(true)
		{
		}


		public SalesOrderHeader(bool includeSchema) :
			base(TestConstants.ReadTestConnectionStringName, includeSchema ? "Sales.SalesOrderHeader" : "SalesOrderHeader", "SalesOrderID")
		{
		}


		/// <summary>
		/// Hook, called when IsValid is called
		/// </summary>
		/// <param name="item">The item to validate.</param>
		public override void Validate(dynamic item)
		{
			// bogus validation: isn't valid if sales person is null. 

			if(item.SalesPersonID == null)
			{
				Errors.Add("SalesPersonID is null");
			}
		}
	}
}
