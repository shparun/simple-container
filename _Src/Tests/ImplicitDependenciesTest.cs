using NUnit.Framework;
using SimpleContainer.Interface;
using SimpleContainer.Tests.Helpers;

namespace SimpleContainer.Tests
{
	public abstract class ImplicitDependenciesTest : SimpleContainerTestBase
	{
		public class Simple : ImplicitDependenciesTest
		{
			public class A
			{
			}

			public class B
			{
			}

			[Test]
			public void Test()
			{
				var container = Container(c => c.WithImplicitDependency<A>(new ServiceName(typeof (B), new string[0])));
				var resolvedService = container.Resolve<A>();
				Assert.That(resolvedService.GetConstructionLog(), Is.EqualTo("A\r\n\tB - implicit"));
			}
		}
	}
}