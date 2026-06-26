// MIGRATION: [QA-1 INFO-2 / AAP Gate 5] WebApplicationFactory bootstrap for the API integration-test suite.
// The QA-1 report flagged that DnnMigration.IntegrationTests contained ONLY TestAuthHandler.cs (a helper) and NO
// actual integration tests, so AAP Gate 5 (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204 for Portal/Module/User)
// was unimplemented. This factory supplies the shared host those tests run against. It performs three overrides on
// top of the real Program.cs pipeline so the CRUD contract can be exercised WITHOUT a SQL Server or a real JWT:
//   1. Jwt:Key injection  — the production appsettings.json ships Jwt:Key="" (an intentional security fail-fast,
//      QA-1 F-A). A valid >=32-byte key is injected here so Program.cs startup validation passes under test.
//   2. EF Core InMemory   — the production SqlServer DnnDbContext registration is removed and replaced with an
//      isolated InMemory store. This is provider-agnostic for the CRUD change-tracker paths (and is precisely the
//      store under which QA-1 Issue #2's nullable-PK change-tracker failure reproduced), so Gate 5 needs no database.
//   3. Test auth scheme   — JWT Bearer (the production default) is replaced by TestAuthHandler's always-authenticated
//      super-user scheme so [Authorize] controllers return 2xx instead of 401.

using DnnMigration.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> over the real API <c>Program</c> that swaps the SQL Server
/// <see cref="DnnDbContext"/> for an isolated EF Core InMemory store, injects a valid <c>Jwt:Key</c>, and promotes
/// <see cref="TestAuthHandler"/> to the default authentication scheme. Used by the Gate-5 CRUD integration tests.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // A per-factory-instance database name isolates each test class's data (the fixture is shared per class via
    // IClassFixture). InMemory databases with distinct names do not share state.
    private readonly string _databaseName = $"DnnIntegrationTests-{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Run under Development so the host uses the same environment QA-1 exercised (valid dev Jwt:Key present,
        // detailed errors). The in-memory configuration below still overrides Jwt:Key to make the suite independent
        // of which appsettings file is on disk in the test output.
        builder.UseEnvironment("Development");

        // ConfigureAppConfiguration callbacks registered here run AFTER the application's own configuration sources,
        // so this in-memory collection wins. The injected Jwt:Key satisfies the Program.cs JWT fail-fast (>=32 bytes).
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "DnnMigration-Integration-Test-Signing-Key-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                ["Jwt:Issuer"] = "DnnMigration",
                ["Jwt:Audience"] = "DnnMigrationClient"
            });
        });

        // ConfigureTestServices runs AFTER the application's ConfigureServices, so these registrations override the
        // production ones (this is the documented EF Core / Mvc.Testing provider-swap hook).
        builder.ConfigureTestServices(services =>
        {
            RemoveDnnDbContextRegistrations(services);

            // Provider-agnostic InMemory store: AddDbContext registers DnnDbContext as Scoped, matching production.
            services.AddDbContext<DnnDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Replace the production JWT default with the always-authenticated super-user Test scheme. Setting all
            // three default scheme properties (and registering last) guarantees the Test handler wins over the JWT
            // default that Program.cs configured.
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>
    /// Removes the service descriptors that wire the production SQL Server <see cref="DnnDbContext"/> so the InMemory
    /// provider can be registered cleanly. In EF Core 8 the provider is carried by the generic
    /// <c>DbContextOptions&lt;DnnDbContext&gt;</c> and the shared non-generic <c>DbContextOptions</c>; removing those
    /// plus the context registration lets the subsequent <c>AddDbContext</c> re-register the context against InMemory
    /// without a "multiple database providers configured" conflict.
    /// </summary>
    private static void RemoveDnnDbContextRegistrations(IServiceCollection services)
    {
        var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<DnnDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(DnnDbContext))
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }
    }
}
