todo
	separate phase (and data structures) for dependency analysis => meta, contractUsage + factories, generics

	explicit stack machine instead of recursion

	refactor: get rid of stupid EndResolveDependencies

	speed up factories with arguments: should be no reflection at runtime

	used unioned contracts in construction log

	merge construction log when IContainer injected in ctor

	get rid of stupid AnalizeDependenciesOnly