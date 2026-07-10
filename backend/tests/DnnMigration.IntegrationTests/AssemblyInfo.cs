using Xunit;

// The API integration tests each boot the ASP.NET Core 8 host in-process through
// WebApplicationFactory<Program>. WebApplicationFactory locates the host builder via the
// process-wide, static HostFactoryResolver, which cannot be driven safely from multiple
// threads simultaneously. Running the Portal/User/Module test classes as parallel xUnit
// collections therefore intermittently fails with
// "System.InvalidOperationException : The entry point exited without ever building an IHost."
// Disabling collection parallelization for this assembly serializes the host bootstraps so
// entry-point resolution is deterministic. Each class still receives its own
// IClassFixture<CustomWebApplicationFactory> instance; only the concurrent host builds are removed.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
