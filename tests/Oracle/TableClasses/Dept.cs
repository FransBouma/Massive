using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.Oracle.TableClasses
{
	public class Department : DynamicModel
	{
		public Department(string providerName) : this(providerName, true)
		{
		}


		public Department(string providerName, bool includeSchema) 
			: base(string.Format(TestConstants.ReadWriteTestConnectionStringName, providerName), includeSchema ? "SCOTT.DEPT" : "DEPT", "DEPTNO", string.Empty, includeSchema ? "SCOTT.DEPT_SEQ" : "DEPT_SEQ")
		{
			
		}
	}
}
