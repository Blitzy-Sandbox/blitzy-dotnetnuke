using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Data;

/// <summary>
/// The single Entity Framework Core 8 <see cref="DbContext"/> for the migrated solution and the
/// persistence root of the Infrastructure layer. It exposes one strongly typed
/// <see cref="DbSet{TEntity}"/> per Domain entity; the sibling <c>Repositories/</c> classes inject
/// this context and query those sets with LINQ (applying <c>AsNoTracking()</c> on read paths and
/// projecting to DTOs before anything leaves the Application boundary).
/// </summary>
// MIGRATION: This DbContext REPLACES the entire legacy ADO.NET data-access stack — the abstract
// singleton provider DotNetNuke.Data.DataProvider (Library/Components/Providers/Data/DataProvider.vb,
// whose generic surface was ExecuteNonQuery / ExecuteReader (-> IDataReader) / ExecuteScalar /
// ExecuteScalar(Of T) / ExecuteDataSet (-> DataSet) / ExecuteSQL, plus a large per-entity stored-proc
// surface) together with its concrete implementation SqlDataProvider
// (Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb), which dispatched every call to
// SqlHelper.Execute*(ConnectionString, DatabaseOwner & ObjectQualifier & <ProcedureName>, params).
// None of that stored-procedure dispatch is carried forward: the whole surface collapses into the
// typed DbSet<T> collections below, queried via EF Core LINQ inside the repositories. This file holds
// no mapping code, no repository logic, and no identity/business logic.
//
// MIGRATION: The context is intentionally PROVIDER-AGNOSTIC. It deliberately does NOT override
// OnConfiguring and bakes in no provider (no UseSqlServer) or connection string. The provider is
// supplied from the outside through the constructor's DbContextOptions<DnnDbContext>:
//   * the API composition root (backend/src/DnnMigration.Api/Program.cs) registers
//     AddDbContext<DnnDbContext>(o => o.UseSqlServer(connectionString)) against the EXISTING
//     SQL Server / aspnet_* schema (connection from appsettings.json ConnectionStrings:Default);
//   * the integration tests (Validation Gate 5: PortalApiTests, ModuleApiTests, UserApiTests) swap
//     in Microsoft.EntityFrameworkCore.InMemory via WebApplicationFactory. That provider override is
//     only possible because the sole constructor accepts DbContextOptions<DnnDbContext> and no
//     provider is hardcoded here.
public class DnnDbContext : DbContext
{
    /// <summary>
    /// Initializes a new <see cref="DnnDbContext"/> instance with externally supplied options. The
    /// database provider, connection string, and any behavioral options are configured by the
    /// composition root (or by the integration tests) and flow in through <paramref name="options"/>;
    /// this context never configures a provider itself.
    /// </summary>
    /// <param name="options">
    /// The context options carrying the injected database provider (SQL Server in the API host,
    /// EF Core InMemory in the integration tests).
    /// </param>
    public DnnDbContext(DbContextOptions<DnnDbContext> options)
        : base(options)
    {
    }

    // The DbSet properties below expose the aggregate-root Domain entities as sixteen sets. They are
    // declared public so the sibling Repositories/ classes can consume them (directly or via Set<T>()).
    // The expression-bodied "=> Set<T>()" style is used deliberately: it computes the set from the
    // context on each access, so these properties are never uninitialized auto-properties and therefore
    // never raise CS8618 — the context satisfies Gate 1's "0 warnings excluding CS8618" bar without
    // relying on that exclusion.
    //
    // MIGRATION (SCHEMA FIDELITY — finding #1): UserMembership is now a STANDALONE aggregate mapped to
    // the existing GUID-keyed [aspnet_Membership] table (see UserMembershipConfiguration), NOT an EF
    // Core owned type of the integer-keyed User. The legacy [aspnet_Membership] table is keyed by a
    // uniqueidentifier [UserId] and carries required NOT NULL columns (ApplicationId, Password,
    // PasswordFormat, PasswordSalt, IsApproved, IsLockedOut, the four date columns, and the four
    // failed-attempt columns); modelling it as an owned type of the int-keyed [Users] row invented an
    // integer UserID key column and omitted those required columns, generating SQL that could not run
    // against the existing schema. UserRepository bridges the int User to its aspnet_Membership row as a
    // VALID read/write projection (a deterministic UserId), inventing no column. Likewise the [UserPortals]
    // junction (composite key UserId+PortalId) now models the real portal association — the legacy schema
    // has no [Users].[PortalID] column. The model therefore maps SIXTEEN entity types in total: the
    // sixteen root sets below.
    //
    // The property names here do NOT determine table names. Every entity is mapped to its real
    // (legacy) table, columns, and foreign-key names by a dedicated IEntityTypeConfiguration<T> in the
    // sibling Configurations/ folder, discovered by OnModelCreating below. Pluralized property names
    // are therefore purely a naming convenience for the repositories.

    /// <summary>Portal (site) settings — mapped to the existing schema by PortalConfiguration.</summary>
    public DbSet<Portal> Portals => Set<Portal>();

    /// <summary>Module instances placed on tabs (pages) — mapped by ModuleConfiguration.</summary>
    public DbSet<Module> Modules => Set<Module>();

    /// <summary>Module PLACEMENT rows (a module on a tab) — mapped to the existing TabModules table by TabModuleConfiguration.</summary>
    public DbSet<TabModule> TabModules => Set<TabModule>();

    /// <summary>Users (identity core) — mapped to the existing aspnet_Users/Users schema by UserConfiguration.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Security roles — mapped by RoleConfiguration.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Tabs (pages) in the site hierarchy — mapped by TabConfiguration.</summary>
    public DbSet<Tab> Tabs => Set<Tab>();

    /// <summary>Base permission definitions — mapped by PermissionConfiguration.</summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    // MIGRATION: ModulePermission, TabPermission, and FolderPermission are C# subclasses of Permission.
    // Each still gets its own DbSet (and its own real table): PermissionConfiguration opts these derived
    // types out of a single-table hierarchy and maps each to its dedicated legacy table, so exposing an
    // independent DbSet per derived type is correct rather than a table-per-hierarchy artifact.

    /// <summary>Module-scoped permission entries — mapped by PermissionConfiguration.</summary>
    public DbSet<ModulePermission> ModulePermissions => Set<ModulePermission>();

    /// <summary>Tab-scoped permission entries — mapped by PermissionConfiguration.</summary>
    public DbSet<TabPermission> TabPermissions => Set<TabPermission>();

    /// <summary>Folder-scoped permission entries — mapped by PermissionConfiguration.</summary>
    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();

    // MIGRATION (SCHEMA FIDELITY — finding #1): UserMembership is a STANDALONE entity mapped to the
    // GUID-keyed [aspnet_Membership] table (UserMembershipConfiguration), reached by UserRepository via a
    // deterministic UserId projection rather than an EF owned relationship (the int [Users].[UserID]
    // cannot key the uniqueidentifier [aspnet_Membership].[UserId]).

    /// <summary>User credential/membership rows — mapped to the existing aspnet_Membership table by UserMembershipConfiguration.</summary>
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();

    /// <summary>User-to-portal junction rows — mapped to the existing UserPortals table by UserPortalConfiguration.</summary>
    public DbSet<UserPortal> UserPortals => Set<UserPortal>();

    /// <summary>User profile values — mapped to aspnet_Profile by UserConfiguration.</summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>Portal HTTP aliases (host headers) — mapped by PortalConfiguration.</summary>
    public DbSet<PortalAlias> PortalAliases => Set<PortalAlias>();

    /// <summary>Module definitions (registrations) — mapped by ModuleConfiguration.</summary>
    public DbSet<ModuleDefinition> ModuleDefinitions => Set<ModuleDefinition>();

    /// <summary>Desktop module registrations — mapped by ModuleConfiguration.</summary>
    public DbSet<DesktopModule> DesktopModules => Set<DesktopModule>();

    /// <summary>
    /// Configures the model by applying every per-entity Fluent mapping in the Infrastructure assembly.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MIGRATION: A single reflection-based call auto-discovers and applies every
        // IEntityTypeConfiguration<T> implementation defined in this (Infrastructure) assembly — i.e.
        // the per-entity classes in the Configurations/ folder that map each entity to its existing
        // table, columns, and FK constraint names via the Fluent API. Wiring the mappings this way
        // keeps the context fully decoupled from the individual configurations; no per-entity
        // modelBuilder.Entity<...>() calls belong here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly);
    }
}
