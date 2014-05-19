using System;

using Massive;

namespace UnitTests
{
    class DummyTableContext : DynamicModel
    {
        public DummyTableContext() 
            : base("LocalConnection", "Employees", "EmployeeId") { }
    }
}
