using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;
using SimpleContainer.Infection;

namespace SimpleContainer.Implementation
{
	internal class Implementation
	{
		public readonly ConstructorInfo[] publicConstructors;
		public readonly Type type;
		private ImplementationConfiguration implementationConfiguration;
		private ImplementationConfiguration definitionConfiguration;

		public Implementation(Type type)
		{
			this.type = type;
			publicConstructors = type.GetConstructors().Where(x => x.IsPublic).ToArray();
		}

		public bool TryGetConstructor(out ConstructorInfo constructor)
		{
			return publicConstructors.SafeTrySingle(out constructor) ||
					publicConstructors.SafeTrySingle(IsContainerConstructor, out constructor);
		}

		private static bool IsContainerConstructor(ConstructorInfo ctor)
		{
			return ctor.GetCustomAttributes().Any(a => a.GetType().Name == "ContainerConstructorAttribute");
		}

		public void SetConfiguration(IContainerConfiguration configuration)
		{
			implementationConfiguration = configuration.GetOrNull<ImplementationConfiguration>(type);
			definitionConfiguration = type.IsGenericType
				? configuration.GetOrNull<ImplementationConfiguration>(type.GetGenericTypeDefinition())
				: null;
		}

		public void SetContext(ResolutionContext context)
		{
			implementationConfiguration = context.GetConfiguration<ImplementationConfiguration>(type);
			definitionConfiguration = type.IsGenericType
				? context.GetConfiguration<ImplementationConfiguration>(type.GetGenericTypeDefinition())
				: null;
		}

		public ImplentationDependencyConfiguration GetDependencyConfiguration(ParameterInfo formalParameter)
		{
			ImplentationDependencyConfiguration dependencyConfiguration = null;
			if (implementationConfiguration != null)
				dependencyConfiguration = implementationConfiguration.GetOrNull(formalParameter);
			if (dependencyConfiguration == null && definitionConfiguration != null)
				dependencyConfiguration = definitionConfiguration.GetOrNull(formalParameter);
			return dependencyConfiguration;
		}

		public IEnumerable<string> GetUnusedDependencyConfigurationNames()
		{
			return implementationConfiguration != null
				? implementationConfiguration.GetUnusedDependencyConfigurationKeys()
				: Enumerable.Empty<string>();
		}
	}
}