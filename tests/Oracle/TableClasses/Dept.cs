using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.Oracle.TableClasses
{
	public class Department : DynamicModel
	{
		public Department() : this(includeSchema: true)
		{
		}


		public Department(bool includeSchema) 
			: base(TestConstants.ReadWriteTestConnectionStringName, includeSchema ? "SCOTT.DEPT" : "DEPT", "DEPTNO", string.Empty, includeSchema ? "SCOTT.DEPT_SEQ" : "DEPT_SEQ")
		{
			
		}
	}
}
