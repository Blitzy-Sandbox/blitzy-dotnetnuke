// =============================================================================
//  CustomWebApplicationFactory
//  -----------------------------------------------------------------------------
//  The central, shared in-process integration-test fixture for the
//  DotNetNuke 4.x -> .NET 8 migration backend. It subclasses
//  Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> to boot the
//  REAL DnnMigration.Api host in-process (the genuine middleware + DI pipeline,
//  AutoMapper profiles, FluentValidation validators, JWT/BCrypt identity
//  concretes and the RFC 7807 exception middleware), while:
//
//    (a) swapping the SQL Server DnnDbContext for an EF Core InMemory database so
//        the suite is hermetic and never contacts a real SQL Server,
//    (b) seeding deterministic test data through the REAL host provider, and
//    (c) exposing helpers that mint a JWT-authenticated HttpClient for the
//        seeded administrator.
//
//  MIGRATION: the legacy DNN 4.9.0.85 codebase shipped zero automated tests, so
//  this is a CREATE / from-scratch fixture with no legacy equivalent. Test
//  classes under ApiTests/ consume it via IClassFixture<CustomWebApplicationFactory>.
// =============================================================================

using System.Net.Http.Headers;                       // AuthenticationHeaderValue
using DnnMigration.Application.Interfaces;            // IJwtService, IPasswordHasher
using DnnMigration.Domain.Entities;                  // User, Portal, Role, Tab, DesktopModule, ModuleDefinition
using DnnMigration.Infrastructure.Persistence;       // DnnDbContext
using Microsoft.AspNetCore.Hosting;                  // IWebHostBuilder
using Microsoft.AspNetCore.Mvc.Testing;              // WebApplicationFactory<>
using Microsoft.AspNetCore.TestHost;                 // ConfigureTestServices
using Microsoft.EntityFrameworkCore;                 // UseInMemoryDatabase, DbContextOptions, Database.EnsureCreated
using Microsoft.Extensions.DependencyInjection;      // AddDbContext, CreateScope, GetRequiredService, Remove
using Microsoft.Extensions.Hosting;                  // IHost, IHostBuilder

namespace DnnMigration.IntegrationTests;

/// <summary>
/// In-process test host for <c>DnnMigration.Api</c>: swaps SQL Server for an EF Core
/// InMemory database, seeds deterministic data, and mints JWTs for the seeded admin so
/// <c>[Authorize]</c> endpoints are reachable from integration tests.
/// </summary>
/// <remarks>
/// <para>
/// The generic argument <c>Program</c> resolves to the API host's top-level-statements
/// entry point, exposed as <c>public partial class Program { }</c> in the GLOBAL namespace
/// by <c>DnnMigration.Api/Program.cs</c>. Because this test project ProjectReferences
/// <c>DnnMigration.Api</c>, the type is referenced simply as <c>Program</c> (no using).
/// </para>
/// <para>
/// The class is <c>sealed</c>, <c>public</c> and relies on its implicit public parameterless
/// constructor so it satisfies xUnit's <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c>
/// contract. Each <c>IClassFixture</c> consumer receives its own factory instance and therefore
/// its own uniquely-named InMemory store (see <see cref="_databaseName"/>), which isolates test
/// classes from one another.
/// </para>
/// </remarks>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // -------------------------------------------------------------------------
    // Seeded identity (reused by resource tests + AuthApiTests)
    // -------------------------------------------------------------------------

    /// <summary>Primary key of the seeded administrator user.</summary>
    public const int AdminUserId = 1;

    /// <summary>Login name of the seeded administrator.</summary>
    public const string AdminUsername = "admin";

    /// <summary>
    /// Plaintext password of the seeded administrator. Used as the credential at
    /// <c>/api/auth/login</c>; persisted only as a one-way BCrypt hash (never in plaintext).
    /// NOTE: this is a non-production test fixture value, not a real secret.
    /// </summary>
    public const string AdminPassword = "Admin123!$";

    /// <summary>Email address of the seeded administrator.</summary>
    public const string AdminEmail = "admin@dnnmigration.local";

    /// <summary>Name of the administrator role granted to the seeded admin (emitted as a role claim).</summary>
    public const string AdminRoleName = "Administrators";

    // -------------------------------------------------------------------------
    // Seeded portal ids (portal 0 is a valid scope for Users/Roles list endpoints)
    // -------------------------------------------------------------------------

    /// <summary>Identifier of the default seeded portal (a valid scope for Users/Roles list endpoints).</summary>
    public const int DefaultPortalId = 0;

    /// <summary>Identifier of the second seeded portal.</summary>
    public const int SecondaryPortalId = 1;

    /// <summary>Identifier of the third seeded portal.</summary>
    public const int TertiaryPortalId = 2;

    // -------------------------------------------------------------------------
    // Supporting ids for the Module create FK chain
    // -------------------------------------------------------------------------

    /// <summary>Identifier of the seeded administrator role.</summary>
    public const int SeededRoleId = 1;

    /// <summary>Identifier of the seeded tab/page.</summary>
    public const int SeededTabId = 1;

    /// <summary>Identifier of the seeded desktop module (parent of <see cref="SeededModuleDefId"/>).</summary>
    public const int SeededDesktopModuleId = 1;

    /// <summary>Identifier of the seeded module definition (used by the Module create FK chain).</summary>
    public const int SeededModuleDefId = 1;

    /// <summary>
    /// Unique InMemory database name per factory instance. The embedded <see cref="Guid"/> guarantees
    /// that each <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c> consumer gets its own isolated
    /// store, so test classes never bleed state into one another.
    /// </summary>
    private readonly string _databaseName = "DnnIntegrationTests_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Deterministic JWT signing key injected into the in-process test host. It is intentionally a
    /// non-production THROWAWAY value (not a real secret), comfortably exceeding the 32-byte / 256-bit
    /// minimum that <c>Program.cs</c> enforces at startup.
    /// </summary>
    /// <remarks>
    /// MIGRATION/TEST: the committed <c>appsettings.json</c> deliberately ships an EMPTY <c>Jwt:Key</c> —
    /// the production signing key is a secret supplied out-of-band via the <c>Jwt__Key</c> environment
    /// variable and is never committed. The hermetic integration host has no such variable, so without a
    /// key the API's fail-fast startup check ("Jwt:Key must be configured ...") would abort every test.
    /// We therefore publish this test key to the same <c>Jwt__Key</c> environment variable from the static
    /// constructor (below), which <c>WebApplication.CreateBuilder</c>'s <c>AddEnvironmentVariables()</c>
    /// source reads BEFORE <c>Program.cs</c> validates it. The identical key backs BOTH token issuance
    /// (<see cref="GenerateTokenForSeededAdmin"/> via <see cref="IJwtService"/>) and the JwtBearer
    /// validation pipeline, so minted tokens validate.
    /// </remarks>
    private const string TestJwtSigningKey =
        "dnn-migration-integration-test-signing-key-not-a-real-secret-0123456789";

    /// <summary>
    /// Guarantees the in-process API host can satisfy its fail-fast <c>Jwt:Key</c> startup check. The CLR
    /// runs this static constructor once, before the first factory instance is created and therefore before
    /// any host is built, so the signing key is present in the process environment by the time
    /// <c>WebApplication.CreateBuilder</c> reads environment variables.
    /// </summary>
    static CustomWebApplicationFactory()
    {
        // Honour an externally-supplied, already-valid Jwt__Key (for example one injected by CI); only
        // fall back to the deterministic test key when no usable key is present in the environment.
        var existingKey = Environment.GetEnvironmentVariable("Jwt__Key");
        if (string.IsNullOrWhiteSpace(existingKey) ||
            System.Text.Encoding.UTF8.GetByteCount(existingKey) < 32)
        {
            Environment.SetEnvironmentVariable("Jwt__Key", TestJwtSigningKey);
        }
    }

    /// <summary>
    /// Reconfigures the web host's service collection for testing. Runs AFTER the application's own
    /// <c>Program.cs</c> registrations, which lets us remove the SQL Server <see cref="DnnDbContext"/>
    /// registration and re-register it on the EF Core InMemory provider.
    /// </summary>
    /// <param name="builder">The web host builder supplied by <see cref="WebApplicationFactory{TEntryPoint}"/>.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ConfigureTestServices (Microsoft.AspNetCore.TestHost) runs AFTER the app's
        // Program.cs service registration, so the SQL Server DnnDbContext is already
        // registered and can be removed here before we substitute the InMemory provider.
        builder.ConfigureTestServices(services =>
        {
            // 1) Remove the SQL Server DnnDbContext registration added by Program.cs:
            //    AddDbContext<DnnDbContext>(o => o.UseSqlServer(connectionString)).
            //    All three descriptors MUST be removed -- the generic options
            //    (DbContextOptions<DnnDbContext>), the non-generic DbContextOptions, AND
            //    the DnnDbContext itself. Leaving the SqlServer options in place causes
            //    EF's "more than one provider is configured" failure.
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<DnnDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(DnnDbContext))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // 2) Re-register the context on EF Core InMemory so SQL Server is never
            //    contacted. The unique database name isolates this factory instance.
            services.AddDbContext<DnnDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>
    /// Builds the host and seeds deterministic data THROUGH the real host service provider so the
    /// seeded rows are visible to the request-scoped <see cref="DnnDbContext"/> instances that handle
    /// HTTP requests.
    /// </summary>
    /// <param name="builder">The host builder supplied by <see cref="WebApplicationFactory{TEntryPoint}"/>.</param>
    /// <returns>The fully constructed and seeded <see cref="IHost"/>.</returns>
    /// <remarks>
    /// Seeding via <c>host.Services</c> (rather than a separately <c>BuildServiceProvider()</c>-ed
    /// provider) is mandatory: EF InMemory keys its store cache by service provider, so a separate
    /// provider would own a different store and the seed would be invisible to request handlers.
    /// </remarks>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Export the deterministic test signing key as Jwt__Key (binds to Jwt:Key) BEFORE the host is built.
        // base.CreateHost(builder) below triggers Program.Main, whose WebApplication.CreateBuilder reads and
        // VALIDATES Jwt:Key during the builder phase (Program.cs fails fast on a missing/<32-byte key, and the
        // repository's appsettings.json ships an EMPTY Jwt:Key by design). The environment-variables provider
        // is consumed by CreateBuilder at that moment, so setting it here makes the in-process host boot and
        // ensures token issuance and JwtBearer validation share the same key. See TestJwtSigningKey.
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtSigningKey);

        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var db = provider.GetRequiredService<DnnDbContext>();
            var passwordHasher = provider.GetRequiredService<IPasswordHasher>();

            // EnsureCreated initializes the InMemory store; SeedData populates it idempotently.
            db.Database.EnsureCreated();
            SeedData(db, passwordHasher);
        }

        return host;
    }

    /// <summary>
    /// Populates the InMemory store with the deterministic fixture data consumed by the integration
    /// tests. The seed is idempotent: if portals already exist the method returns immediately, so it is
    /// safe even if invoked more than once for a given store.
    /// </summary>
    /// <param name="db">The request-scoped <see cref="DnnDbContext"/> resolved from the host provider.</param>
    /// <param name="passwordHasher">The real <see cref="IPasswordHasher"/> used to hash the admin password.</param>
    private static void SeedData(DnnDbContext db, IPasswordHasher passwordHasher)
    {
        if (db.Portals.Any())
        {
            return; // idempotent -- the store is already seeded
        }

        // Seed >= 3 portals so a Portal hard-delete always leaves at least one portal
        // behind and never trips the API's last-portal guard (which would yield a 409).
        //
        // MIGRATION: Portal.PortalID is ValueGeneratedOnAdd (identity) per
        // PortalConfiguration -- that convention is deliberately preserved so a Portal
        // POST round-trips to 201 in Gate 5. The consequence for SEEDING is that the DNN
        // default portal's id of 0 is indistinguishable from "key not set" (0 ==
        // default(int)); a naive AddRange therefore asks EF to GENERATE that key and the
        // generated value collides with the explicitly-keyed portals (an identity-map
        // conflict). To insert the seeded ids (0, 1, 2) verbatim, add each portal
        // individually and PIN its primary key as an explicit, permanent value. The
        // controller's POST path is unaffected -- it still relies on key generation.
        var portals = new[]
        {
            new Portal { PortalID = DefaultPortalId,   PortalName = "Default Portal",   Email = "host@dnnmigration.local", AdministratorId = AdminUserId, HomeDirectory = "Portals/0", HostFee = 0f, GUID = Guid.NewGuid() },
            new Portal { PortalID = SecondaryPortalId, PortalName = "Secondary Portal", Email = "p1@dnnmigration.local",   AdministratorId = AdminUserId, HomeDirectory = "Portals/1", HostFee = 0f, GUID = Guid.NewGuid() },
            new Portal { PortalID = TertiaryPortalId,  PortalName = "Tertiary Portal",  Email = "p2@dnnmigration.local",   AdministratorId = AdminUserId, HomeDirectory = "Portals/2", HostFee = 0f, GUID = Guid.NewGuid() }
        };

        foreach (var portal in portals)
        {
            var desiredPortalId = portal.PortalID;
            var entry = db.Portals.Add(portal);

            // Add() can stamp a temporary key when PortalID == 0; restore the intended id
            // and mark it permanent so EF inserts it as-is rather than generating one.
            entry.Property(p => p.PortalID).CurrentValue = desiredPortalId;
            entry.Property(p => p.PortalID).IsTemporary = false;
        }

        // Seed the administrator in portal 0 with a REAL BCrypt hash (never plaintext) so the
        // /api/auth/login flow can authenticate it. User.Roles is EF-ignored and is therefore NOT
        // set on this persisted entity -- role claims are emitted from the in-memory User built in
        // GenerateTokenForSeededAdmin().
        db.Users.Add(new User
        {
            UserID = AdminUserId,
            PortalID = DefaultPortalId,
            Username = AdminUsername,
            Email = AdminEmail,
            FirstName = "System",
            LastName = "Administrator",
            DisplayName = "System Administrator",
            Password = passwordHasher.Hash(AdminPassword),
            IsSuperUser = true,
            Approved = true
        });

        db.Roles.Add(new Role
        {
            RoleID = SeededRoleId,
            PortalID = DefaultPortalId,
            RoleName = AdminRoleName,
            ServiceFee = 0f,
            TrialFee = 0f
        });

        db.Tabs.Add(new Tab
        {
            TabID = SeededTabId,
            PortalID = DefaultPortalId,
            TabName = "Home",
            IsVisible = true,
            IsDeleted = false
        });

        // Supporting rows so the CreateModuleDto foreign keys
        // (PortalID, TabID, ModuleDefID, DesktopModuleID) resolve when a Module is created.
        db.DesktopModules.Add(new DesktopModule { DesktopModuleID = SeededDesktopModuleId });
        db.ModuleDefinitions.Add(new ModuleDefinition { ModuleDefID = SeededModuleDefId, DesktopModuleID = SeededDesktopModuleId });

        db.SaveChanges();
    }

    // -------------------------------------------------------------------------
    // Authentication helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mints a signed JWT access token for the seeded administrator by resolving the REAL
    /// <see cref="IJwtService"/> from the host container and calling
    /// <see cref="IJwtService.GenerateAccessToken(User)"/>.
    /// </summary>
    /// <returns>A signed JWT access token carrying the seeded admin's identity and role claims.</returns>
    /// <remarks>
    /// This bypasses the rate-limited <c>/api/auth/login</c> endpoint (<c>[EnableRateLimiting("auth")]</c>,
    /// ~5 requests/min) and the AuthService username-&gt;portal lookup, keeping resource tests deterministic
    /// and immune to throttling. The in-process host's <c>Jwt:Key</c> is supplied by
    /// <see cref="CreateHost(IHostBuilder)"/> (see <see cref="TestJwtSigningKey"/>) and is &gt;= 32 bytes,
    /// so token issuance and the JwtBearer validation pipeline both succeed.
    /// <see cref="User.Roles"/> is EF-ignored and is set ONLY on this transient in-memory instance so the
    /// service emits role claims.
    /// </remarks>
    public string GenerateTokenForSeededAdmin()
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();

        var adminUser = new User
        {
            UserID = AdminUserId,
            PortalID = DefaultPortalId,
            Username = AdminUsername,
            Email = AdminEmail,
            IsSuperUser = true,
            Approved = true,
            Roles = new[] { AdminRoleName } // EF-ignored; emits role claims on the minted token
        };

        return jwt.GenerateAccessToken(adminUser);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> against the in-process host, pre-authenticated with a Bearer
    /// token for the seeded administrator so <c>[Authorize]</c> endpoints are immediately reachable.
    /// </summary>
    /// <returns>An authenticated <see cref="HttpClient"/> with the <c>Authorization: Bearer ...</c> header set.</returns>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTokenForSeededAdmin());
        return client;
    }
}
