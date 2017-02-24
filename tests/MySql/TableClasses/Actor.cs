using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Massive.Tests.MySql.TableClasses
{
	public class Actor : DynamicModel
	{
		public Actor() : this(true)
		{
		}


		public Actor(bool includeSchema) :
			base(TestConstants.ReadTestConnectionStringName, includeSchema ? "sakila.actor" : "actor", "actor_id")
		{
		}
	}
}
