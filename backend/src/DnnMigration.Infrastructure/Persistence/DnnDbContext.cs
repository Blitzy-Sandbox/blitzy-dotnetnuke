// MIGRATION: Replaces the legacy ADO.NET data layer — SqlDataProvider.vb (Microsoft.ApplicationBlocks.Data
// SqlHelper stored-procedure calls) and CBO.vb reflection-based IDataReader->object hydration — with EF Core 8
// entity materialization. The existing DotNetNuke 4.9.0.85 database schema is mapped UNCHANGED (ADR-002):
// no EF migrations, no schema generation, no data migration. All table/column/key/relationship mapping detail
// lives in Persistence/Configurations/ and is applied here via a single ApplyConfigurationsFromAssembly scan.

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core 8 unit-of-work / persistence composition point for the DotNetNuke migration backend.
/// </summary>
/// <remarks>
/// <para>
/// This context exposes one <see cref="DbSet{TEntity}"/> per Domain aggregate member and wires up every
/// <see cref="IEntityTypeConfiguration{TEntity}"/> defined in this (Infrastructure) assembly. It is intentionally
/// <strong>thin</strong>: it carries no table/column names, keys, relationships, soft-delete columns or
/// <c>Ignore()</c> rules — that mapping detail is owned exclusively by the sibling classes under
/// <c>Persistence/Configurations/</c> and discovered by reflection via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly, System.Func{System.Type, bool})"/>.
/// </para>
/// <para>
/// The provider (SQL Server in production, the EF Core InMemory provider in integration tests) and the
/// connection string are supplied externally through the injected <see cref="DbContextOptions{TContext}"/>
/// — by <c>DnnMigration.Api</c>'s <c>Program.cs</c> via <c>AddDbContext&lt;DnnDbContext&gt;(...)</c> and by the
/// integration-test host respectively. Consequently this class deliberately does NOT override
/// <c>OnConfiguring</c>, never reads <c>IConfiguration</c>/environment variables, and contains nothing
/// provider-specific, keeping it fully provider-agnostic.
/// </para>
/// <para>
/// Per ADR-002 the schema is mapped <strong>unchanged</strong>: this context never calls
/// <c>Database.Migrate()</c>, <c>Database.EnsureCreated()</c> or <c>Database.EnsureDeleted()</c>, and no EF
/// migrations exist. Callers persist work asynchronously through the inherited
/// <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>; reads in the repositories use
/// <c>AsNoTracking()</c>.
/// </para>
/// </remarks>
public class DnnDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DnnDbContext"/> class using the fully-configured options
    /// supplied by the dependency-injection container (or the integration-test host).
    /// </summary>
    /// <param name="options">
    /// The options describing the database provider and connection to use. These are configured externally
    /// (e.g. <c>AddDbContext&lt;DnnDbContext&gt;(o =&gt; o.UseSqlServer(...))</c>); this context performs no
    /// configuration of its own.
    /// </param>
    public DnnDbContext(DbContextOptions<DnnDbContext> options)
        : base(options)
    {
    }

    // ----- Portal aggregate -----

    /// <summary>Gets or sets the set of <see cref="Portal"/> entities (DNN <c>Portals</c> table).</summary>
    public DbSet<Portal> Portals { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="PortalAlias"/> entities (DNN <c>PortalAlias</c> table).</summary>
    public DbSet<PortalAlias> PortalAliases { get; set; } = null!;

    // ----- Module aggregate -----

    /// <summary>Gets or sets the set of <see cref="Module"/> entities (DNN <c>Modules</c> table).</summary>
    public DbSet<Module> Modules { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="DesktopModule"/> entities (DNN <c>DesktopModules</c> table).</summary>
    public DbSet<DesktopModule> DesktopModules { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="ModuleDefinition"/> entities (DNN <c>ModuleDefinitions</c> table).</summary>
    public DbSet<ModuleDefinition> ModuleDefinitions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of <see cref="TabModule"/> placement entities (DNN <c>TabModules</c> table).
    /// MIGRATION: this is the physical home of the module-placement columns (TabID, ModuleOrder, PaneName,
    /// Visibility, ...) that the legacy flattened ModuleInfo denormalized onto the module object and that
    /// CP2 ModuleConfiguration Ignore()s on <see cref="Module"/>; the repository joins it to <c>Modules</c>
    /// to reproduce the legacy GetTabModules without querying those ignored members (ADR-002).
    /// </summary>
    public DbSet<TabModule> TabModules { get; set; } = null!;

    // ----- User aggregate -----

    /// <summary>Gets or sets the set of <see cref="User"/> entities (DNN <c>Users</c> table).</summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="UserRole"/> join entities (DNN <c>UserRoles</c> table).</summary>
    public DbSet<UserRole> UserRoles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of <see cref="UserPortal"/> membership entities (DNN <c>UserPortals</c> table).
    /// MIGRATION: this is the physical home of the user-to-portal association that the legacy flattened
    /// UserInfo denormalized onto its PortalID property and that CP2 UserConfiguration Ignore()s on
    /// <see cref="User"/> (the <c>Users</c> table has no PortalID column); the repository joins it to
    /// <c>Users</c> to resolve portal-scoped user queries without filtering the ignored member (ADR-002).
    /// </summary>
    public DbSet<UserPortal> UserPortals { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of <see cref="AspNetUser"/> entities (ASP.NET Membership <c>aspnet_Users</c> table).
    /// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): the bridge from a DNN username to the
    /// membership <c>UserId</c> (a <c>uniqueidentifier</c>) under which credentials live in
    /// <c>aspnet_Membership</c>. The repository matches on its lowered user name, then joins to
    /// <see cref="AspNetMemberships"/> on <c>UserId</c> to source the password hash (ADR-002 — schema mapped
    /// unchanged; CP2 <c>UserConfiguration</c> correctly Ignore()s the non-physical <c>User.Password</c>).
    /// </summary>
    public DbSet<AspNetUser> AspNetUsers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of <see cref="AspNetMembership"/> entities (ASP.NET Membership
    /// <c>aspnet_Membership</c> table) — the physical home of the credential hash, keyed by the membership
    /// <c>UserId</c> shared 1:1 with <see cref="AspNetUsers"/>. MIGRATION (Finding CP5 MAJOR): joined from
    /// <see cref="AspNetUsers"/> to source the BCrypt password hash for <c>AuthService.LoginAsync</c> (ADR-002).
    /// </summary>
    public DbSet<AspNetMembership> AspNetMemberships { get; set; } = null!;

    // ----- Role aggregate -----

    /// <summary>Gets or sets the set of <see cref="Role"/> entities (DNN <c>Roles</c> table).</summary>
    public DbSet<Role> Roles { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="RoleGroup"/> entities (DNN <c>RoleGroups</c> table).</summary>
    public DbSet<RoleGroup> RoleGroups { get; set; } = null!;

    // ----- Tab aggregate -----

    /// <summary>Gets or sets the set of <see cref="Tab"/> entities (DNN <c>Tabs</c> table).</summary>
    public DbSet<Tab> Tabs { get; set; } = null!;

    // ----- Permission aggregate (base + 3 junction types) -----
    // The base Permission plus the FolderPermission/ModulePermission/TabPermission derived types are each
    // exposed as a separate DbSet. They map to SEPARATE DNN tables with DIFFERENT primary keys (so this is
    // NOT a TPH/TPT hierarchy); PermissionConfiguration.cs breaks the EF inheritance and maps each as an
    // independent table.

    /// <summary>Gets or sets the set of <see cref="Permission"/> entities (DNN <c>Permission</c> table).</summary>
    public DbSet<Permission> Permissions { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="FolderPermission"/> entities (DNN <c>FolderPermission</c> table).</summary>
    public DbSet<FolderPermission> FolderPermissions { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="ModulePermission"/> entities (DNN <c>ModulePermission</c> table).</summary>
    public DbSet<ModulePermission> ModulePermissions { get; set; } = null!;

    /// <summary>Gets or sets the set of <see cref="TabPermission"/> entities (DNN <c>TabPermission</c> table).</summary>
    public DbSet<TabPermission> TabPermissions { get; set; } = null!;

    /// <summary>
    /// Configures the EF Core model by applying every <see cref="IEntityTypeConfiguration{TEntity}"/> declared
    /// in the Infrastructure assembly.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies every IEntityTypeConfiguration<T> defined under Persistence/Configurations/
        // (PortalConfiguration, ModuleConfiguration, UserConfiguration, RoleConfiguration,
        //  TabConfiguration, PermissionConfiguration). Table/column names, keys, relationships,
        // soft-delete columns, Ignore()'d non-column members (e.g. User.Roles, User.FullName, Tab.TabType),
        // and the Permission inheritance reconciliation are all defined there — never via attributes on the
        // entities and never inline in this context. typeof(DnnDbContext).Assembly targets the Infrastructure
        // assembly so the scan discovers those sibling configurations regardless of their access modifier.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly);
    }
}
