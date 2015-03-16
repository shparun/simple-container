using System;
using System.Collections.Generic;
using System.Linq;
using SimpleContainer.Configuration;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ResolutionContext : IContainerConfigurationRegistry
	{
		private readonly IContainerConfiguration configuration;
		private readonly List<ContainerService> current = new List<ContainerService>();
		private readonly ISet<Type> currentTypes = new HashSet<Type>();
		private readonly List<ContractDeclaration> declaredContracts = new List<ContractDeclaration>();

		public ResolutionContext(IContainerConfiguration configuration, IEnumerable<string> contracts)
		{
			this.configuration = configuration;
			if (contracts == null)
				return;
			var contractsArray = contracts.ToArray();
			if (contractsArray.Length > 0)
			{
				string _;
				PushContractDeclarations(contractsArray, out _);
			}
		}

		public List<string> DeclaredContractNames()
		{
			return declaredContracts.Select(x => x.name).ToList();
		}

		public List<string> GetDeclaredContractsByNames(List<string> names)
		{
			return declaredContracts
				.Where(x => names.Contains(x.name, StringComparer.OrdinalIgnoreCase))
				.Select(x => x.name)
				.ToList();
		}

		public int DeclaredContractsCount()
		{
			return declaredContracts.Count;
		}

		public bool ContractDeclared(string name)
		{
			return declaredContracts.Any(x => x.name.EqualsIgnoringCase(name));
		}

		public string DeclaredContractsKey()
		{
			return InternalHelpers.FormatContractsKey(DeclaredContractNames());
		}

		public T GetOrNull<T>(Type type) where T : class
		{
			for (var i = declaredContracts.Count - 1; i >= 0; i--)
			{
				var declaration = declaredContracts[i];
				foreach (var definition in declaration.definitions)
				{
					var result = definition.GetOrNull<T>(type);
					if (result == null)
						continue;
					var containerService = GetTopService();
					containerService.UseContractWithName(declaration.name);
					foreach (var name in definition.RequiredContracts)
						containerService.UseContractWithName(name);
					return result;
				}
			}
			return configuration.GetOrNull<T>(type);
		}

		public void Instantiate(ContainerService containerService, string name,  SimpleContainer container)
		{
			var previous = current.Count == 0 ? null : current[current.Count - 1];
			var declaredContacts = DeclaredContractNames();
			containerService.declaredContracts = declaredContacts;
			containerService.isStatic = container.cacheLevel == CacheLevel.Static;
			containerService.name = name;
			current.Add(containerService);
			if (!currentTypes.Add(containerService.Type))
			{
				var message = string.Format("cyclic dependency {0} ...-> {1} -> {0}",
					containerService.Type.FormatName(), previous == null ? "null" : previous.Type.FormatName());
				containerService.EndResolveDependenciesWithFailure(message);
				return;
			}
			containerService.AttachToContext(this);
			container.Instantiate(containerService);
			current.RemoveAt(current.Count - 1);
			currentTypes.Remove(containerService.Type);
		}

		public ContainerService GetTopService()
		{
			return current.Count == 0 ? null : current[current.Count - 1];
		}

		public ContainerService GetPreviousService()
		{
			return current.Count <= 1 ? null : current[current.Count - 2];
		}

		public ContainerService Resolve(Type type, List<string> contractNames, string name, SimpleContainer container)
		{
			var internalContracts = InternalHelpers.ToInternalContracts(contractNames, type);
			if (internalContracts == null)
				return container.ResolveSingleton(type, name, this);

			var unioned = internalContracts
				.Select(delegate(string s)
				{
					var configurations = configuration.GetContractConfigurations(s).EmptyIfNull().ToArray();
					return configurations.Length == 1 ? configurations[0].UnionContractNames : null;
				})
				.ToArray();
			if (unioned.All(x => x == null))
				return ResolveUsingContracts(type, name, container, internalContracts);
			var source = new List<List<string>>();
			for (var i = 0; i < internalContracts.Count; i++)
				source.Add(unioned[i] ?? new List<string>(1) {internalContracts[i]});
			var result = new ContainerService(type);
			result.AttachToContext(this);
			foreach (var contracts in source.CartesianProduct())
			{
				var item = ResolveUsingContracts(type, name, container, contracts);
				result.UnionFrom(item, true);
				if (result.status == ServiceStatus.Failed)
					return result;
			}
			result.EndResolveDependencies();
			return result;
		}

		public ContainerService ResolveUsingContracts(Type type, string name, SimpleContainer container,
			List<string> contractNames)
		{
			string message;
			if (!PushContractDeclarations(contractNames, out message))
				return new ContainerService(type)
				{
					status = ServiceStatus.Failed,
					message = message
				};
			var result = container.ResolveSingleton(type, name, this);
			declaredContracts.RemoveRange(declaredContracts.Count - contractNames.Count, contractNames.Count);
			return result;
		}

		private bool PushContractDeclarations(IEnumerable<string> contractNames, out string message)
		{
			message = null;
			foreach (var c in contractNames)
			{
				var duplicate = declaredContracts.FirstOrDefault(x => x.name.EqualsIgnoringCase(c));
				if (duplicate != null)
				{
					const string messageFormat = "contract [{0}] already declared, all declared contracts [{1}]";
					message = string.Format(messageFormat, c, DeclaredContractsKey());
					return false;
				}
				var contractDeclaration = new ContractDeclaration
				{
					name = c,
					definitions = configuration.GetContractConfigurations(c)
						.EmptyIfNull()
						.Where(x => MatchWithDeclaredContracts(x.RequiredContracts))
						.ToList()
				};
				declaredContracts.Add(contractDeclaration);
			}
			return true;
		}

		private bool MatchWithDeclaredContracts(List<string> required)
		{
			for (int r = 0, d = 0; r < required.Count; d++)
			{
				if (d >= declaredContracts.Count)
					return false;
				if (required[r].EqualsIgnoringCase(declaredContracts[d].name))
					r++;
			}
			return true;
		}

		public void Comment(string message, params object[] args)
		{
			current[current.Count - 1].message = string.Format(message, args);
		}

		private class ContractDeclaration
		{
			public string name;
			public List<ContractConfiguration> definitions;
		}
	}
}