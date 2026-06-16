// =============================================================================
// DnnDbContext.cs
// -----------------------------------------------------------------------------
// MIGRATION: Replaces the legacy ADO.NET data layer — SqlDataProvider.vb
// (Microsoft.ApplicationBlocks.Data SqlHelper stored-procedure calls, with the
// ObjectQualifier/DatabaseOwner proc-name prefixing) and CBO.vb reflection-based
// IDataReader->object hydration — with EF Core 8 entity materialization. No manual
// IDataReader-to-object mapping survives (AAP §0.1.2, §0.6.1).
//
// ADR-002 (schema preservation): the existing DotNetNuke 4.9.0.85 database schema
// is mapped UNCHANGED — no EF migrations, no schema generation (no
// Database.Migrate / EnsureCreated / EnsureDeleted), and no data migration in
// Phase 1. This context reads NO configuration: the provider (SQL Server) and
// connection string are supplied externally via AddDbContext<DnnDbContext>(...)
// in DnnMigration.Api's Program.cs, and via the EF Core InMemory provider in the
// integration tests — so there is deliberately no OnConfiguring override here.
//
// All table/column/key/relationship/soft-delete/Ignore() mapping detail lives in
// Persistence/Configurations/ and is discovered by reflection through
// ApplyConfigurationsFromAssembly. This keeps the context thin and provider-
// agnostic: the same model builds cleanly under both the SQL Server provider
// (production) and the EF Core InMemory provider (integration-test fixtures,
// Gate 5).
// =============================================================================

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Persistence;

/// <summary>
/// The Entity Framework Core 8 <see cref="DbContext"/> for the DotNetNuke
/// 4.9.0.85 → .NET 8 rewrite. It is the single composition point of the
/// persistence layer: it exposes one <see cref="DbSet{TEntity}"/> per Domain
/// entity and applies every Fluent API <see cref="IEntityTypeConfiguration{TEntity}"/>
/// found in the Infrastructure assembly.
/// </summary>
/// <remarks>
/// <para>
/// This context wholesale replaces the legacy ADO.NET pipeline — the
/// <c>SqlDataProvider.vb</c> <c>SqlHelper</c> stored-procedure layer and the
/// reflection-based <c>CBO.vb</c> hydration — with EF Core entity
/// materialization. Repositories (in <c>Infrastructure/Repositories/</c>) issue
/// async LINQ queries against these <see cref="DbSet{TEntity}"/> accessors:
/// reads use <c>AsNoTracking()</c> and writes flow through the inherited
/// <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>.
/// </para>
/// <para>
/// The context is intentionally thin. It contains no mapping detail, no business
/// logic, and no query methods. Per ADR-002 the schema is mapped exactly as it
/// exists in the database: there is no <c>OnConfiguring</c> override, no call to
/// <c>Database.Migrate</c>/<c>EnsureCreated</c>/<c>EnsureDeleted</c>, and no
/// SQL-Server-only construct in this file. All table names, column names, keys,
/// relationships and inheritance reconciliation are defined in
/// <c>Persistence/Configurations/</c> and are discovered automatically by
/// <see cref="OnModelCreating(ModelBuilder)"/>, so the same model builds under
/// both SQL Server (production) and the EF Core InMemory provider used by the
/// integration tests.
/// </para>
/// </remarks>
public class DnnDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DnnDbContext"/> class using a
    /// fully-configured <see cref="DbContextOptions{TContext}"/> supplied by the
    /// caller (the Api composition root via <c>AddDbContext&lt;DnnDbContext&gt;</c>,
    /// or the EF Core InMemory provider in the integration tests).
    /// </summary>
    /// <param name="options">
    /// The options that configure the provider, connection string, and any other
    /// behavior. The context never reads configuration itself; everything is
    /// provided through these externally-built options.
    /// </param>
    public DnnDbContext(DbContextOptions<DnnDbContext> options)
        : base(options)
    {
    }

    // -------------------------------------------------------------------------
    // DbSets — one accessor per Domain entity. The DbSet property names are
    // code-level accessors ONLY; the real (often singular) DotNetNuke table names
    // are set by ToTable(...) inside the Persistence/Configurations/ classes.
    // The `= null!;` initializer suppresses CS8618 on these non-nullable
    // navigation properties (EF assigns them at runtime); it is the EF Core 8
    // idiom and adds zero runtime behavior.
    // -------------------------------------------------------------------------

    // Portal aggregate
    public DbSet<Portal> Portals { get; set; } = null!;
    public DbSet<PortalAlias> PortalAliases { get; set; } = null!;

    // Module aggregate
    public DbSet<Module> Modules { get; set; } = null!;
    public DbSet<DesktopModule> DesktopModules { get; set; } = null!;
    public DbSet<ModuleDefinition> ModuleDefinitions { get; set; } = null!;

    // MIGRATION (CP3 schema-fidelity): the per-tab module PLACEMENT lives in dbo.TabModules, not
    // dbo.Modules. The TabModules-sourced fields are Ignore()'d on the Module entity (ModuleConfiguration.cs);
    // this DbSet exposes the placement rows so ModuleRepository can read placement via a TabModules->Modules
    // join and persist it (AddTabModule/UpdateTabModule semantics). See MIGRATION_NOTES.md §4.2 / D-030/D-031.
    public DbSet<TabModule> TabModules { get; set; } = null!;

    // User aggregate
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;

    // MIGRATION (CP3 schema-fidelity): PortalID is NOT a dbo.Users column — portal membership lives in
    // dbo.UserPortals (the legacy vw_Users view LEFT JOINs Users to UserPortals on UserId to surface
    // PortalId). PortalID is Ignore()'d on the User entity (UserConfiguration.cs); this DbSet exposes the
    // membership rows so UserRepository can reproduce the vw_Users join + the GetUserByUsername superuser
    // bypass and rehydrate User.PortalID. See MIGRATION_NOTES.md §4.2 (Users / UserPortals) / D-018.
    public DbSet<UserPortal> UserPortals { get; set; } = null!;

    // Role aggregate
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<RoleGroup> RoleGroups { get; set; } = null!;

    // Tab aggregate
    public DbSet<Tab> Tabs { get; set; } = null!;

    // Permission aggregate (base + 3 junction types). All four are exposed as
    // separate DbSets; the inheritance-vs-separate-table reconciliation (the DNN
    // schema stores each in its own table with its own primary key) is performed
    // in PermissionConfiguration.cs, never here.
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<FolderPermission> FolderPermissions { get; set; } = null!;
    public DbSet<ModulePermission> ModulePermissions { get; set; } = null!;
    public DbSet<TabPermission> TabPermissions { get; set; } = null!;

    /// <summary>
    /// Builds the EF Core model by applying every
    /// <see cref="IEntityTypeConfiguration{TEntity}"/> defined in the
    /// Infrastructure assembly.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model for this context.</param>
    /// <remarks>
    /// The single <c>ApplyConfigurationsFromAssembly</c> call discovers the
    /// sibling configuration classes in <c>Persistence/Configurations/</c>
    /// (PortalConfiguration, ModuleConfiguration, UserConfiguration,
    /// RoleConfiguration, TabConfiguration, PermissionConfiguration) by reflection,
    /// regardless of their access modifier — so this context has no compile-time
    /// dependency on them. Table/column names, keys, relationships, soft-delete
    /// columns, <c>Ignore()</c>'d non-column members, and the Permission
    /// inheritance reconciliation are all defined there, never via attributes on
    /// the entities and never inline in this method.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies every IEntityTypeConfiguration<T> defined in Persistence/Configurations/.
        // typeof(DnnDbContext).Assembly resolves to the Infrastructure assembly so the
        // scan picks up the sibling configuration classes.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly);
    }
}
