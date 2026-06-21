using System.Net.Http.Headers;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// Central in-process integration-test fixture for the <c>DnnMigration.Api</c> host.
/// </summary>
/// <remarks>
/// <para>
/// This factory boots the REAL API host in-process via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> — exercising the full middleware and
/// dependency-injection pipeline exactly as production does — while applying three
/// test-only adaptations:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     It swaps the SQL Server <see cref="DnnDbContext"/> registration for an EF Core
///     <c>InMemory</c> database, so the suite NEVER contacts a real database
///     (Validation Gate 5 runs without SQL Server).
///     </description>
///   </item>
///   <item>
///     <description>
///     It seeds deterministic test data (three portals, an admin user plus its ASP.NET
///     membership credential rows carrying a real BCrypt hash, a portal membership, a role,
///     a tab, and the desktop-module/module-definition chain) through the real host provider
///     so the rows are visible to request-scoped <see cref="DnnDbContext"/> instances created
///     per HTTP request.
///     </description>
///   </item>
///   <item>
///     <description>
///     It mints JWTs for the seeded admin via the real <see cref="IJwtService"/>, exposing
///     <see cref="CreateAuthenticatedClient"/> so <c>[Authorize]</c> endpoints are reachable
///     without driving the rate-limited <c>/api/auth/login</c> endpoint.
///     </description>
///   </item>
/// </list>
/// <para>
/// Test classes consume this fixture through <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c>;
/// each fixture instance owns an isolated InMemory store keyed by a unique database name.
/// </para>
/// <para>
/// MIGRATION: there is no legacy equivalent — DotNetNuke 4.9.0.85 shipped zero automated
/// tests, so this is a CREATE-from-scratch fixture.
/// </para>
/// </remarks>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // -------------------------------------------------------------------------------------
    // Seeded identity — reused by resource API tests and the auth tests so credentials and
    // ids are a single source of truth across the suite.
    // -------------------------------------------------------------------------------------

    /// <summary>Primary key of the seeded administrator user.</summary>
    public const int AdminUserId = 1;

    /// <summary>Login name of the seeded administrator user.</summary>
    public const string AdminUsername = "admin";

    /// <summary>
    /// Plaintext password of the seeded administrator. Used by login-flow tests; persisted as a
    /// one-way BCrypt hash via <see cref="IPasswordHasher.Hash(string)"/> during seeding.
    /// </summary>
    public const string AdminPassword = "Admin123!$";

    /// <summary>Email address of the seeded administrator user.</summary>
    public const string AdminEmail = "admin@dnnmigration.local";

    /// <summary>Name of the seeded administrators role; emitted as a role claim on minted tokens.</summary>
    public const string AdminRoleName = "Administrators";

    /// <summary>
    /// Membership identifier (<c>aspnet_Users</c> / <c>aspnet_Membership</c> primary key) for the seeded
    /// administrator. MIGRATION (Finding CP5 MAJOR — membership-password sourcing): the admin's BCrypt password
    /// hash is sourced from the physical <c>aspnet_Membership</c> table keyed by this <see cref="Guid"/>, reached
    /// from the lowered username via <c>aspnet_Users</c> (the InstallMembership.sql JOIN bridge). A fixed,
    /// deterministic Guid keeps the seed reproducible across host construction.
    /// </summary>
    public static readonly Guid AdminMembershipUserId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Primary key of a NON-administrator principal used by authorization tests. No database row is seeded for
    /// this id — the value exists only to mint a valid-but-unprivileged token, because
    /// <c>PermissionAuthorizationHandler</c> decides 403 purely from JWT claims and never performs a user lookup.
    /// </summary>
    public const int NonAdminUserId = 99;

    /// <summary>Login name of the non-administrator principal used by authorization tests.</summary>
    public const string NonAdminUsername = "standarduser";

    /// <summary>Email address of the non-administrator principal used by authorization tests.</summary>
    public const string NonAdminEmail = "standarduser@dnnmigration.local";

    // -------------------------------------------------------------------------------------
    // Seeded portal ids — portal 0 is a valid scope for the Users/Roles list endpoints, and
    // three portals guarantee a Portal hard-delete always leaves at least one remaining
    // (the legacy last-portal guard in PortalController.DeletePortal returns 409 otherwise).
    // -------------------------------------------------------------------------------------

    /// <summary>Primary key of the default (host) portal.</summary>
    public const int DefaultPortalId = 0;

    /// <summary>Primary key of the secondary seeded portal.</summary>
    public const int SecondaryPortalId = 1;

    /// <summary>Primary key of the tertiary seeded portal.</summary>
    public const int TertiaryPortalId = 2;

    // -------------------------------------------------------------------------------------
    // Supporting ids so the Module create FK chain (PortalID, TabID, ModuleDefID,
    // DesktopModuleID) resolves against seeded rows.
    // -------------------------------------------------------------------------------------

    /// <summary>Primary key of the seeded administrators role.</summary>
    public const int SeededRoleId = 1;

    /// <summary>Primary key of the seeded tab/page.</summary>
    public const int SeededTabId = 1;

    /// <summary>Primary key of the seeded desktop module.</summary>
    public const int SeededDesktopModuleId = 1;

    /// <summary>Primary key of the seeded module definition.</summary>
    public const int SeededModuleDefId = 1;

    /// <summary>
    /// Unique InMemory database name for this factory instance. A fresh <see cref="Guid"/>
    /// guarantees that each <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c> instance gets its
    /// own isolated store, so test classes never bleed state into one another.
    /// </summary>
    private readonly string _databaseName = "DnnIntegrationTests_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Replaces the SQL Server <see cref="DnnDbContext"/> registration with the EF Core
    /// InMemory provider. This runs through <c>ConfigureTestServices</c>, which executes AFTER the
    /// application's <c>Program.cs</c> service registration — so the SQL Server registration already
    /// exists and can be removed cleanly before re-registering.
    /// </summary>
    /// <param name="builder">The web host builder supplied by the base factory.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // 1) Remove the SQL Server DnnDbContext registration added by Program.cs:
            //    builder.Services.AddDbContext<DnnDbContext>(o => o.UseSqlServer(connectionString)).
            //    We must drop the generic DbContextOptions<DnnDbContext>, the non-generic
            //    DbContextOptions, AND the DnnDbContext registration itself; leaving the SQL Server
            //    options in place triggers EF's "more than one provider configured" error.
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

            // 2) Re-register the context on EF Core InMemory so SQL Server is never contacted.
            services.AddDbContext<DnnDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>
    /// Builds the host and seeds deterministic data THROUGH the real host provider so the rows
    /// are visible to request-scoped <see cref="DnnDbContext"/> instances. Seeding via a separately
    /// <c>BuildServiceProvider()</c>-ed container would use a different InMemory store cache and the
    /// seed would be invisible to request handlers; overriding <see cref="CreateHost"/> avoids that.
    /// </summary>
    /// <param name="builder">The host builder supplied by the base factory.</param>
    /// <returns>The fully constructed and seeded <see cref="IHost"/>.</returns>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var db = provider.GetRequiredService<DnnDbContext>();
            var passwordHasher = provider.GetRequiredService<IPasswordHasher>();

            db.Database.EnsureCreated();
            SeedData(db, passwordHasher);
        }

        return host;
    }

    /// <summary>
    /// Inserts the deterministic baseline rows used by the integration suite. The seed is
    /// idempotent (guarded by an existence check) so repeated host construction is safe.
    /// </summary>
    /// <param name="db">The InMemory-backed database context to seed.</param>
    /// <param name="passwordHasher">The real BCrypt password hasher used to hash the admin password.</param>
    private static void SeedData(DnnDbContext db, IPasswordHasher passwordHasher)
    {
        if (db.Portals.Any())
        {
            return; // Idempotent: already seeded for this store.
        }

        // Three portals so a Portal hard-delete always leaves at least one remaining and never
        // trips the legacy last-portal guard (PortalController.DeletePortal -> 409 Conflict).
        // NOTE: PortalName, DefaultLanguage, and HomeDirectory are marked .IsRequired() in
        // PortalConfiguration; the EF Core InMemory provider enforces required (non-nullable)
        // properties at SaveChanges, so all three MUST be set or seeding throws DbUpdateException.
        AddSeedEntity(db, new Portal { PortalID = DefaultPortalId, PortalName = "Default Portal", Email = "host@dnnmigration.local", AdministratorId = AdminUserId, HomeDirectory = "Portals/0", DefaultLanguage = "en-US", HostFee = 0f, GUID = Guid.NewGuid() });
        AddSeedEntity(db, new Portal { PortalID = SecondaryPortalId, PortalName = "Secondary Portal", Email = "p1@dnnmigration.local", AdministratorId = AdminUserId, HomeDirectory = "Portals/1", DefaultLanguage = "en-US", HostFee = 0f, GUID = Guid.NewGuid() });
        AddSeedEntity(db, new Portal { PortalID = TertiaryPortalId, PortalName = "Tertiary Portal", Email = "p2@dnnmigration.local", AdministratorId = AdminUserId, HomeDirectory = "Portals/2", DefaultLanguage = "en-US", HostFee = 0f, GUID = Guid.NewGuid() });

        // Admin user in portal 0 with a REAL BCrypt hash so /api/auth/login can authenticate it.
        // NOTE: User.Roles is EF-ignored — it is NOT set here; it is set only on the in-memory User
        // passed to IJwtService in GenerateTokenForSeededAdmin so role claims are emitted.
        AddSeedEntity(db, new User
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

        // MIGRATION (schema fidelity, ADR-002): the seeded admin MUST have a physical [UserPortals] membership
        // row for (AdminUserId, DefaultPortalId). AuthService.LoginAsync resolves the account via
        // UserRepository.GetByUsernameAsync, which JOINs [Users] -> [UserPortals] and filters PortalID rather
        // than the User.PortalID property (CP2 UserConfiguration Ignore()s it because the legacy [Users] table
        // has no PortalID column). The user-to-portal association therefore lives ONLY in [UserPortals], so
        // seeding the User alone is insufficient: without this row the username lookup yields null and a VALID
        // login returns 401 (even though GetByIdAsync still finds the user by id, which is why minted-token
        // /api/auth/me succeeds). The surrogate UserPortalID is not part of the composite key and is not
        // value-generated, so it is set explicitly.
        AddSeedEntity(db, new UserPortal
        {
            UserID = AdminUserId,
            PortalID = DefaultPortalId,
            UserPortalID = 1,
            CreatedDate = DateTime.UtcNow,
            Authorised = true
        });

        // MIGRATION (Finding CP5 MAJOR — membership-password sourcing): seed the ASP.NET Membership credential
        // rows so the REAL /api/auth/login valid-credential flow can source and BCrypt-verify the admin password.
        // The hash is NOT a physical [Users] column (CP2 UserConfiguration Ignore()s User.Password); legacy DNN
        // stores it in [aspnet_Membership], reached from the lowered username via [aspnet_Users]
        // (InstallMembership.sql: "LOWER(@UserName) = u.LoweredUserName AND u.UserId = m.UserId").
        // UserRepository.GetByUsernameAsync performs exactly that JOIN, so BOTH rows are required for
        // AuthService.LoginAsync to return 200; without them the password lookup yields null and a valid login
        // would (incorrectly) 401. LoweredUserName MUST be the lower-cased AdminUsername so the repository's
        // username.ToLower() comparison matches. The Guid PK is shared 1:1 between the two rows and supplied
        // explicitly (AddSeedEntity pins it, defeating any client-side Guid generation).
        AddSeedEntity(db, new AspNetUser
        {
            UserId = AdminMembershipUserId,
            LoweredUserName = AdminUsername.ToLowerInvariant()
        });

        AddSeedEntity(db, new AspNetMembership
        {
            UserId = AdminMembershipUserId,
            Password = passwordHasher.Hash(AdminPassword),
            // MIGRATION (DEV-067): the seeded admin is an approved, never-locked-out membership so the new
            // GET {id}/membership read and the authorize/unlock transitions exercise a realistic baseline. The
            // lockout date is the legacy "never locked out" sentinel (1754-01-01, SQL datetime-range safe).
            IsApproved = true,
            IsLockedOut = false,
            FailedPasswordAttemptCount = 0,
            LastLockoutDate = AspNetMembership.NeverLockedOutDate
        });

        AddSeedEntity(db, new Role
        {
            RoleID = SeededRoleId,
            PortalID = DefaultPortalId,
            RoleName = AdminRoleName,
            ServiceFee = 0f,
            TrialFee = 0f
        });

        AddSeedEntity(db, new Tab
        {
            TabID = SeededTabId,
            PortalID = DefaultPortalId,
            TabName = "Home",
            IsVisible = true,
            IsDeleted = false
        });

        // Supporting rows so CreateModuleDto FKs (PortalID, TabID, ModuleDefID, DesktopModuleID) resolve.
        AddSeedEntity(db, new DesktopModule { DesktopModuleID = SeededDesktopModuleId });
        AddSeedEntity(db, new ModuleDefinition { ModuleDefID = SeededModuleDefId, DesktopModuleID = SeededDesktopModuleId });

        db.SaveChanges();
    }

    /// <summary>
    /// Adds a seed entity while pinning its explicit primary-key value, defeating EF Core InMemory
    /// store-generated keys.
    /// </summary>
    /// <remarks>
    /// All integer primary keys in the model are configured <c>ValueGeneratedOnAdd</c> (for SQL
    /// Server IDENTITY parity). Under the EF Core InMemory provider a <c>ValueGeneratedOnAdd</c> key
    /// left at the CLR default (<c>0</c>) — e.g. the default portal's <c>PortalID == 0</c>, since the
    /// schema models <c>Portals</c> as <c>IDENTITY(0,1)</c> — is treated as "unset" and the provider
    /// synthesizes a replacement value at add time, which then collides with the explicitly seeded
    /// <c>PortalID == 1</c> ("instance ... already being tracked"). This helper snapshots the intended
    /// key from the still-detached entity, transitions it to <see cref="EntityState.Added"/>, then
    /// restores the snapshot and clears the temporary-key flag so EF persists the deterministic ids
    /// (0, 1, 2, …) verbatim instead of generating them.
    /// </remarks>
    /// <typeparam name="TEntity">The entity CLR type being seeded.</typeparam>
    /// <param name="db">The context whose change tracker will own the entity.</param>
    /// <param name="entity">The fully-initialized entity to add with its explicit key preserved.</param>
    private static void AddSeedEntity<TEntity>(DnnDbContext db, TEntity entity)
        where TEntity : class
    {
        var entry = db.Entry(entity); // Detached: reflects the explicit CLR key, triggers no generation.
        var primaryKey = entry.Metadata.FindPrimaryKey();

        if (primaryKey is null)
        {
            entry.State = EntityState.Added;
            return;
        }

        // Snapshot the intended explicit key BEFORE the entity is tracked/added.
        var intendedKeyValues = primaryKey.Properties
            .ToDictionary(property => property.Name, property => entry.Property(property.Name).CurrentValue);

        entry.State = EntityState.Added; // Begins tracking; may overwrite a default key via value generation.

        // Restore the intended key and pin it as permanent so no store value is generated/kept.
        foreach (var property in primaryKey.Properties)
        {
            var keyProperty = entry.Property(property.Name);
            keyProperty.CurrentValue = intendedKeyValues[property.Name];
            keyProperty.IsTemporary = false;
        }
    }

    // -------------------------------------------------------------------------------------
    // Authentication helpers
    // -------------------------------------------------------------------------------------

    /// <summary>
    /// Mints a signed JWT access token for the seeded administrator by resolving the REAL
    /// <see cref="IJwtService"/> from the host container. This deliberately bypasses the
    /// rate-limited <c>/api/auth/login</c> endpoint and the username-to-portal lookup, so resource
    /// tests are deterministic and never throttled. The token carries the admin's identity and the
    /// <see cref="AdminRoleName"/> role claim.
    /// </summary>
    /// <returns>A signed JWT access token string for the seeded administrator.</returns>
    public string GenerateTokenForSeededAdmin()
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Roles is EF-ignored; it is populated here purely so the token emits role claims.
        var adminUser = new User
        {
            UserID = AdminUserId,
            PortalID = DefaultPortalId,
            Username = AdminUsername,
            Email = AdminEmail,
            IsSuperUser = true,
            Approved = true,
            Roles = new[] { AdminRoleName }
        };

        return jwt.GenerateAccessToken(adminUser);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-authenticated with a Bearer token for the seeded
    /// administrator, ready to call <c>[Authorize]</c>-protected endpoints.
    /// </summary>
    /// <returns>An <see cref="HttpClient"/> whose default request headers carry the admin Bearer token.</returns>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTokenForSeededAdmin());
        return client;
    }

    /// <summary>
    /// Mints a signed JWT access token for a NON-administrator principal: an authenticated user that is neither
    /// a SuperUser nor a member of any privileged role.
    /// </summary>
    /// <remarks>
    /// MIGRATION (Finding CP5 MAJOR — security test coverage): used to prove the authenticated-but-unauthorized
    /// path. The token is validly SIGNED by the real <see cref="IJwtService"/> (so authentication succeeds), but
    /// carries <c>IsSuperUser=False</c> and zero role claims, so <c>PermissionAuthorizationHandler</c> fails
    /// closed and a permission-protected endpoint returns <c>403 Forbidden</c>. No database row is required: the
    /// authorization handler decides purely from JWT claims and performs no user lookup.
    /// </remarks>
    /// <returns>A signed JWT access token for a non-privileged user.</returns>
    public string GenerateNonAdminToken()
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Roles is EF-ignored; an empty set here means the token emits NO role claims, and IsSuperUser=false
        // emits IsSuperUser="False", so the principal satisfies authentication but fails every permission policy.
        var standardUser = new User
        {
            UserID = NonAdminUserId,
            PortalID = DefaultPortalId,
            Username = NonAdminUsername,
            Email = NonAdminEmail,
            IsSuperUser = false,
            Approved = true,
            Roles = Array.Empty<string>()
        };

        return jwt.GenerateAccessToken(standardUser);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-authenticated as a NON-administrator (valid signature, no
    /// privileges), used to assert that permission-protected endpoints return <c>403 Forbidden</c> for an
    /// authenticated-but-unauthorized caller.
    /// </summary>
    /// <returns>An <see cref="HttpClient"/> whose Bearer token authenticates a non-privileged user.</returns>
    public HttpClient CreateNonAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateNonAdminToken());
        return client;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> carrying a structurally well-formed but cryptographically INVALID
    /// Bearer token (a real admin token whose signature segment has been corrupted), used to assert that
    /// protected endpoints reject a tampered/invalid token with <c>401 Unauthorized</c>.
    /// </summary>
    /// <remarks>
    /// MIGRATION (Finding CP5 MAJOR — security test coverage): appending to a real token corrupts its
    /// HMAC-SHA256 signature, so the JWT Bearer middleware's <c>ValidateToken</c> fails signature validation and
    /// short-circuits the request to <c>401</c> BEFORE authorization runs. This complements the existing
    /// "missing Bearer -> 401" coverage with explicit "invalid Bearer -> 401" coverage.
    /// </remarks>
    /// <returns>An <see cref="HttpClient"/> whose Bearer token is present but invalid.</returns>
    public HttpClient CreateInvalidTokenClient()
    {
        var client = CreateClient();
        var tamperedToken = GenerateTokenForSeededAdmin() + "CORRUPTED";
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tamperedToken);
        return client;
    }
}
