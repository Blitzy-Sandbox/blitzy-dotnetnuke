// MIGRATION: [QA-1 INFO-2 / AAP Gate 5] Disable xUnit parallelization for the integration-test assembly.
// Each CRUD test class spins up its own WebApplicationFactory<Program> host via IClassFixture. When xUnit runs the
// classes in parallel (its default), multiple WebApplicationFactory instances build the SUT host concurrently and
// race inside HostFactoryResolver, surfacing as "The entry point exited without ever building an IHost" — because
// resolving a top-level-statement entry point (Program) relies on process-wide static interception state that is not
// safe to drive from several threads at once. Serializing the small integration suite removes the race entirely with
// negligible cost; the unit-test assembly is unaffected.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
