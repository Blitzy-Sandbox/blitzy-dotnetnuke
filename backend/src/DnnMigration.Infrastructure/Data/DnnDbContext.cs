// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: SqlDataProvider.vb SqlHelper-based data access → EF Core 8 DbContext
// Source: Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb
//
// Key transformations:
// 1) Replace all SqlHelper.ExecuteReader/ExecuteNonQuery/ExecuteScalar calls with
//    EF Core DbSet operations and LINQ queries
// 2) Replace stored procedure calls with EF Core LINQ expressions
// 3) Add DbSet<T> properties for all domain entities
// 4) Implement OnModelCreating to apply Fluent API configurations from
//    IEntityTypeConfiguration classes
// 5) Use DbContextOptionsBuilder for SQL Server connection configuration
// 6) Apply connection string pattern similar to original _connectionString field
// 7) Use C# 12 features: file-scoped namespace, nullable reference types
// 8) Include XML documentation comments
// 9) Configure query tracking behavior for performance
// 10) Handle legacy database schema mapping through entity configurations
//
// Original VB.NET patterns replaced:
// - SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & ProcName, params)
// - SqlHelper.ExecuteNonQuery(ConnectionString, CommandType.StoredProcedure, sql)
// - SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & ProcName, params)
// - Direct ADO.NET SqlCommand/SqlConnection usage
// - Transaction management via SqlTransaction
//
// All these patterns are now replaced by:
// - DbSet<T>.Add/Update/Remove for CRUD operations
// - LINQ queries (Where, FirstOrDefault, ToListAsync, etc.) for data retrieval
// - EF Core change tracking for transaction management
// - SaveChangesAsync for persisting changes
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext for the DNN Migration application.
/// </summary>
/// <remarks>
/// <para>
/// This DbContext replaces the legacy SqlDataProvider pattern from DotNetNuke 4.x.
/// It provides DbSet properties for all domain entities and configures entity
/// mappings to the existing DNN database schema through Fluent API configurations.
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> The original SqlDataProvider.vb used SqlHelper
/// (Microsoft.ApplicationBlocks.Data) for all database operations with stored procedures.
/// This DbContext replaces that pattern with EF Core's built-in capabilities:
/// <list type="bullet">
/// <item><description>SqlHelper.ExecuteReader → DbSet&lt;T&gt;.Where().ToListAsync()</description></item>
/// <item><description>SqlHelper.ExecuteNonQuery → DbSet&lt;T&gt;.Add/Update/Remove + SaveChangesAsync()</description></item>
/// <item><description>SqlHelper.ExecuteScalar → DbSet&lt;T&gt;.CountAsync(), FirstOrDefaultAsync()</description></item>
/// <item><description>Stored procedures → LINQ expressions</description></item>
/// <item><description>SqlTransaction → EF Core change tracking</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Connection String:</strong> The connection string is configured through
/// DbContextOptions, typically injected via DI from appsettings.json. This replaces
/// the original pattern of reading from web.config via Config.GetConnectionString().
/// </para>
/// <para>
/// <strong>Performance Considerations:</strong>
/// <list type="bullet">
/// <item><description>Use AsNoTracking() for read-only queries via the provided extension methods</description></item>
/// <item><description>Entity configurations create appropriate indexes for query optimization</description></item>
/// <item><description>Lazy loading is disabled by default for explicit control</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // DI Registration in Program.cs:
/// builder.Services.AddDbContext&lt;DnnDbContext&gt;(options =>
///     options.UseSqlServer(connectionString));
///
/// // Usage in a repository:
/// public class PortalRepository : IPortalRepository
/// {
///     private readonly DnnDbContext _context;
///     
///     public async Task&lt;Portal?&gt; GetByIdAsync(int id, CancellationToken ct = default)
///     {
///         return await _context.Portals
///             .AsNoTracking()
///             .FirstOrDefaultAsync(p => p.PortalId == id, ct);
///     }
/// }
/// </code>
/// </example>
public class DnnDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DnnDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the DbContext.</param>
    /// <remarks>
    /// <para>
    /// The DbContextOptions should be configured with the SQL Server provider
    /// and the connection string pointing to the DNN database.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces the original SqlDataProvider constructor that read
    /// the connection string from web.config via Config.GetConnectionString() and
    /// stored it in the private _connectionString field.
    /// </para>
    /// </remarks>
    public DnnDbContext(DbContextOptions<DnnDbContext> options) : base(options)
    {
    }

    // =========================================================================
    // Portal Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetPortal, dbo.GetPortals,
    // dbo.AddPortal, dbo.UpdatePortal, dbo.DeletePortal
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="Portal"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="Portal"/> entities.</value>
    /// <remarks>
    /// <para>
    /// Portal is the foundational entity for DNN's multi-tenant architecture.
    /// Every other entity (User, Role, Tab, Module) references a Portal through PortalId.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for portal data access
    /// using stored procedures like dbo.GetPortal and dbo.GetPortals.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get portal by ID (replacing dbo.GetPortal @PortalID)
    /// var portal = await context.Portals
    ///     .FirstOrDefaultAsync(p => p.PortalId == portalId);
    ///
    /// // Get all portals (replacing dbo.GetPortals)
    /// var portals = await context.Portals.ToListAsync();
    /// </code>
    /// </example>
    public virtual DbSet<Portal> Portals { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="PortalAlias"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="PortalAlias"/> entities.</value>
    /// <remarks>
    /// <para>
    /// PortalAlias represents domain/URL aliases for portals, enabling multiple
    /// domain names to point to the same portal.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for portal alias data access
    /// using stored procedures like dbo.GetPortalAlias and dbo.GetPortalAliasByPortalID.
    /// </para>
    /// </remarks>
    public virtual DbSet<PortalAlias> PortalAliases { get; set; } = null!;

    // =========================================================================
    // User Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetUser, dbo.GetUsers,
    // dbo.AddUser, dbo.UpdateUser, dbo.DeleteUser
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="User"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="User"/> entities.</value>
    /// <remarks>
    /// <para>
    /// User represents an individual user account within a portal.
    /// Users can be assigned to multiple roles through the <see cref="UserRole"/> entity.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for user data access
    /// using stored procedures like dbo.GetUser, dbo.GetUsers, dbo.GetUserByUsername.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get user by ID (replacing dbo.GetUser @UserID)
    /// var user = await context.Users
    ///     .Include(u => u.Profile)
    ///     .FirstOrDefaultAsync(u => u.UserId == userId);
    ///
    /// // Get user by username within portal
    /// var user = await context.Users
    ///     .FirstOrDefaultAsync(u => u.Username == username &amp;&amp; u.PortalId == portalId);
    /// </code>
    /// </example>
    public virtual DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="UserProfile"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="UserProfile"/> entities.</value>
    /// <remarks>
    /// <para>
    /// UserProfile contains extended profile properties for a user including
    /// name components, address, contact information, and preferences.
    /// Has a one-to-one relationship with <see cref="User"/>.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for user profile data
    /// access using stored procedures related to profile properties.
    /// </para>
    /// </remarks>
    public virtual DbSet<UserProfile> UserProfiles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="UserRole"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="UserRole"/> entities.</value>
    /// <remarks>
    /// <para>
    /// UserRole is the junction entity representing the many-to-many relationship
    /// between users and roles. Contains role membership metadata like
    /// EffectiveDate and ExpiryDate for time-based role assignments.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for user role data
    /// using stored procedures like dbo.GetUserRole, dbo.GetUserRoles, dbo.AddUserRole.
    /// </para>
    /// </remarks>
    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;

    // =========================================================================
    // Role Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetRole, dbo.GetRoles,
    // dbo.AddRole, dbo.UpdateRole, dbo.DeleteRole
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="Role"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="Role"/> entities.</value>
    /// <remarks>
    /// <para>
    /// Role represents a permission grouping within a portal. Roles are used for
    /// access control and can be assigned to users through the <see cref="UserRole"/> entity.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for role data access
    /// using stored procedures like dbo.GetRole, dbo.GetRoles, dbo.GetRolesByGroup.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get role by ID (replacing dbo.GetRole @RoleID)
    /// var role = await context.Roles
    ///     .FirstOrDefaultAsync(r => r.RoleId == roleId);
    ///
    /// // Get roles by portal (replacing dbo.GetRoles @PortalID)
    /// var roles = await context.Roles
    ///     .Where(r => r.PortalId == portalId)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public virtual DbSet<Role> Roles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="RoleGroup"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="RoleGroup"/> entities.</value>
    /// <remarks>
    /// <para>
    /// RoleGroup represents a logical grouping for organizing roles within a portal.
    /// Roles can optionally belong to a RoleGroup for better organization.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for role group data
    /// using stored procedures like dbo.GetRoleGroup, dbo.GetRoleGroups.
    /// </para>
    /// </remarks>
    public virtual DbSet<RoleGroup> RoleGroups { get; set; } = null!;

    // =========================================================================
    // Module Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetModule, dbo.GetModules,
    // dbo.AddModule, dbo.UpdateModule, dbo.DeleteModule
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="Module"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="Module"/> entities.</value>
    /// <remarks>
    /// <para>
    /// Module represents an instance of a <see cref="ModuleDefinition"/> placed
    /// on a specific <see cref="Tab"/> within a <see cref="Portal"/>. Modules
    /// are the primary content containers in the DNN architecture.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for module data access
    /// using stored procedures like dbo.GetModule, dbo.GetModules, dbo.GetTabModules.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get module by ID (replacing dbo.GetModule @ModuleID)
    /// var module = await context.Modules
    ///     .Include(m => m.ModuleDefinition)
    ///     .FirstOrDefaultAsync(m => m.ModuleId == moduleId);
    ///
    /// // Get modules by tab (replacing dbo.GetTabModules @TabID)
    /// var modules = await context.Modules
    ///     .Where(m => m.TabId == tabId &amp;&amp; !m.IsDeleted)
    ///     .OrderBy(m => m.PaneName)
    ///     .ThenBy(m => m.ModuleOrder)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public virtual DbSet<Module> Modules { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="ModuleDefinition"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="ModuleDefinition"/> entities.</value>
    /// <remarks>
    /// <para>
    /// ModuleDefinition represents metadata about a module type. It defines the
    /// friendly name, default cache time, and links to the <see cref="DesktopModule"/>
    /// that provides the module's functionality.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for module definition
    /// data using stored procedures like dbo.GetModuleDefinition, dbo.GetModuleDefinitions.
    /// </para>
    /// </remarks>
    public virtual DbSet<ModuleDefinition> ModuleDefinitions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="DesktopModule"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="DesktopModule"/> entities.</value>
    /// <remarks>
    /// <para>
    /// DesktopModule represents a module type/definition that can be installed
    /// and used in portals. Contains metadata about the module package including
    /// name, version, description, and business controller class.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for desktop module
    /// data using stored procedures like dbo.GetDesktopModule, dbo.GetDesktopModules.
    /// </para>
    /// </remarks>
    public virtual DbSet<DesktopModule> DesktopModules { get; set; } = null!;

    // =========================================================================
    // Tab/Page Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetTab, dbo.GetTabs,
    // dbo.AddTab, dbo.UpdateTab, dbo.DeleteTab
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="Tab"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="Tab"/> entities.</value>
    /// <remarks>
    /// <para>
    /// Tab represents a page within a portal's navigation hierarchy. Tabs form
    /// a hierarchical tree structure through the self-referencing ParentId
    /// relationship, where root-level tabs have a null ParentId.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for tab data access
    /// using stored procedures like dbo.GetTab, dbo.GetTabs, dbo.GetTabsByPortal.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get tab by ID (replacing dbo.GetTab @TabID)
    /// var tab = await context.Tabs
    ///     .Include(t => t.Modules)
    ///     .FirstOrDefaultAsync(t => t.TabId == tabId);
    ///
    /// // Get portal tabs hierarchy (replacing dbo.GetTabs @PortalID)
    /// var tabs = await context.Tabs
    ///     .Where(t => t.PortalId == portalId &amp;&amp; !t.IsDeleted)
    ///     .OrderBy(t => t.Level)
    ///     .ThenBy(t => t.TabOrder)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public virtual DbSet<Tab> Tabs { get; set; } = null!;

    // =========================================================================
    // Permission Domain DbSets
    // MIGRATION: Replace stored procedures dbo.GetPermission, dbo.GetPermissions,
    // dbo.GetModulePermission, dbo.GetTabPermission, dbo.GetFolderPermission
    // =========================================================================

    /// <summary>
    /// Gets or sets the <see cref="Permission"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="Permission"/> entities.</value>
    /// <remarks>
    /// <para>
    /// Permission defines the available permission types (e.g., VIEW, EDIT, DELETE)
    /// that can be assigned to modules, tabs, or folders through the corresponding
    /// permission entities.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for permission data
    /// using stored procedures like dbo.GetPermission, dbo.GetPermissions.
    /// </para>
    /// </remarks>
    public virtual DbSet<Permission> Permissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="ModulePermission"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="ModulePermission"/> entities.</value>
    /// <remarks>
    /// <para>
    /// ModulePermission represents permission assignments at the module level.
    /// Grants or denies a specific permission to a role or user for a module.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for module permission
    /// data using stored procedures like dbo.GetModulePermission, dbo.GetModulePermissionsByModuleID.
    /// </para>
    /// </remarks>
    public virtual DbSet<ModulePermission> ModulePermissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="TabPermission"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="TabPermission"/> entities.</value>
    /// <remarks>
    /// <para>
    /// TabPermission represents permission assignments at the page/tab level.
    /// Grants or denies a specific permission to a role or user for a tab.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for tab permission
    /// data using stored procedures like dbo.GetTabPermission, dbo.GetTabPermissionsByTabID.
    /// </para>
    /// </remarks>
    public virtual DbSet<TabPermission> TabPermissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the <see cref="FolderPermission"/> entities.
    /// </summary>
    /// <value>A <see cref="DbSet{TEntity}"/> of <see cref="FolderPermission"/> entities.</value>
    /// <remarks>
    /// <para>
    /// FolderPermission represents permission assignments at the file system folder level.
    /// Grants or denies a specific permission to a role or user for a folder.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces SqlHelper.ExecuteReader calls for folder permission
    /// data using stored procedures like dbo.GetFolderPermission, dbo.GetFolderPermissionsByFolderID.
    /// </para>
    /// </remarks>
    public virtual DbSet<FolderPermission> FolderPermissions { get; set; } = null!;

    // =========================================================================
    // Model Configuration
    // =========================================================================

    /// <summary>
    /// Configures the entity mappings and relationships for the model.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to configure entities.</param>
    /// <remarks>
    /// <para>
    /// This method applies all IEntityTypeConfiguration implementations to configure
    /// entity mappings to the existing DNN database schema. Each configuration class
    /// handles table mapping, column configurations, relationships, and indexes.
    /// </para>
    /// <para>
    /// <strong>Applied Configurations:</strong>
    /// <list type="bullet">
    /// <item><description><see cref="PortalConfiguration"/> - Portal and PortalAlias entities</description></item>
    /// <item><description><see cref="UserConfiguration"/> - User, UserProfile, and UserRole entities</description></item>
    /// <item><description><see cref="RoleConfiguration"/> - Role and RoleGroup entities</description></item>
    /// <item><description><see cref="ModuleConfiguration"/> - Module, ModuleDefinition, and DesktopModule entities</description></item>
    /// <item><description><see cref="TabConfiguration"/> - Tab entity with hierarchical structure</description></item>
    /// <item><description><see cref="PermissionConfiguration"/> - Permission, ModulePermission, TabPermission, FolderPermission entities</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// MIGRATION: This replaces the implicit schema mapping that was embedded in
    /// SqlDataProvider stored procedure calls. EF Core Fluent API provides explicit,
    /// type-safe configuration of the legacy database schema.
    /// </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Portal domain configurations
        // MIGRATION: Replaces dbo.GetPortal, dbo.GetPortals, dbo.AddPortal, etc. schema mapping
        modelBuilder.ApplyConfiguration(new PortalConfiguration());

        // Apply User domain configurations
        // MIGRATION: Replaces dbo.GetUser, dbo.GetUsers, dbo.AddUser, etc. schema mapping
        modelBuilder.ApplyConfiguration(new UserConfiguration());

        // Apply Role domain configurations
        // MIGRATION: Replaces dbo.GetRole, dbo.GetRoles, dbo.AddRole, etc. schema mapping
        modelBuilder.ApplyConfiguration(new RoleConfiguration());

        // Apply Module domain configurations
        // MIGRATION: Replaces dbo.GetModule, dbo.GetModules, dbo.AddModule, etc. schema mapping
        modelBuilder.ApplyConfiguration(new ModuleConfiguration());

        // Apply Tab domain configurations
        // MIGRATION: Replaces dbo.GetTab, dbo.GetTabs, dbo.AddTab, etc. schema mapping
        modelBuilder.ApplyConfiguration(new TabConfiguration());

        // Apply Permission domain configurations
        // MIGRATION: Replaces dbo.GetPermission, dbo.GetModulePermission, etc. schema mapping
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
    }

    // =========================================================================
    // SaveChanges Overrides
    // =========================================================================

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    /// <remarks>
    /// <para>
    /// This method persists all tracked entity changes to the database in a single
    /// transaction. It replaces the individual SqlHelper.ExecuteNonQuery calls that
    /// were used in the legacy SqlDataProvider for Insert/Update/Delete operations.
    /// </para>
    /// <para>
    /// MIGRATION: The original SqlDataProvider used explicit SqlTransaction management
    /// for operations that required multiple database calls. EF Core's change tracking
    /// handles this automatically - all changes are persisted in a single transaction.
    /// </para>
    /// </remarks>
    public override int SaveChanges()
    {
        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronously saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    /// <remarks>
    /// <para>
    /// This is the preferred method for saving changes in ASP.NET Core applications
    /// as it doesn't block the calling thread during database operations.
    /// </para>
    /// <para>
    /// MIGRATION: The original SqlDataProvider used synchronous ADO.NET operations.
    /// This async version enables better scalability in the modern web API architecture.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Adding a new portal (replacing SqlHelper.ExecuteNonQuery for dbo.AddPortal)
    /// var portal = new Portal { PortalName = "New Site", ... };
    /// context.Portals.Add(portal);
    /// await context.SaveChangesAsync(cancellationToken);
    ///
    /// // Updating a portal (replacing SqlHelper.ExecuteNonQuery for dbo.UpdatePortal)
    /// var portal = await context.Portals.FindAsync(portalId);
    /// portal.PortalName = "Updated Name";
    /// await context.SaveChangesAsync(cancellationToken);
    ///
    /// // Deleting a portal (replacing SqlHelper.ExecuteNonQuery for dbo.DeletePortal)
    /// var portal = await context.Portals.FindAsync(portalId);
    /// context.Portals.Remove(portal);
    /// await context.SaveChangesAsync(cancellationToken);
    /// </code>
    /// </example>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Extension methods for <see cref="DnnDbContext"/> to support read-only queries.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods provide convenient access to DbSet properties with
/// AsNoTracking() applied for read-only query scenarios, improving performance
/// by bypassing EF Core's change tracking mechanism.
/// </para>
/// <para>
/// MIGRATION: The original SqlDataProvider always created new entity instances
/// from DataReader results without any tracking. These methods provide equivalent
/// behavior in EF Core for read-only queries where tracking is unnecessary.
/// </para>
/// </remarks>
public static class DnnDbContextReadOnlyExtensions
{
    /// <summary>
    /// Gets a read-only query for portals without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="Portal"/> entities without tracking.</returns>
    /// <remarks>
    /// Use this method when you only need to read portal data without making changes.
    /// The returned entities will not be tracked by the change tracker, improving performance.
    /// </remarks>
    public static IQueryable<Portal> PortalsReadOnly(this DnnDbContext context)
        => context.Portals.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for portal aliases without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="PortalAlias"/> entities without tracking.</returns>
    public static IQueryable<PortalAlias> PortalAliasesReadOnly(this DnnDbContext context)
        => context.PortalAliases.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for users without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="User"/> entities without tracking.</returns>
    /// <remarks>
    /// Use this method when you only need to read user data without making changes.
    /// Consider including Profile navigation property when user profile data is needed.
    /// </remarks>
    public static IQueryable<User> UsersReadOnly(this DnnDbContext context)
        => context.Users.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for user profiles without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="UserProfile"/> entities without tracking.</returns>
    public static IQueryable<UserProfile> UserProfilesReadOnly(this DnnDbContext context)
        => context.UserProfiles.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for user roles without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="UserRole"/> entities without tracking.</returns>
    public static IQueryable<UserRole> UserRolesReadOnly(this DnnDbContext context)
        => context.UserRoles.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for roles without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="Role"/> entities without tracking.</returns>
    public static IQueryable<Role> RolesReadOnly(this DnnDbContext context)
        => context.Roles.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for role groups without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="RoleGroup"/> entities without tracking.</returns>
    public static IQueryable<RoleGroup> RoleGroupsReadOnly(this DnnDbContext context)
        => context.RoleGroups.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for modules without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="Module"/> entities without tracking.</returns>
    /// <remarks>
    /// Use this method when you only need to read module data without making changes.
    /// Consider including ModuleDefinition navigation property when module type data is needed.
    /// </remarks>
    public static IQueryable<Module> ModulesReadOnly(this DnnDbContext context)
        => context.Modules.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for module definitions without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="ModuleDefinition"/> entities without tracking.</returns>
    public static IQueryable<ModuleDefinition> ModuleDefinitionsReadOnly(this DnnDbContext context)
        => context.ModuleDefinitions.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for desktop modules without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="DesktopModule"/> entities without tracking.</returns>
    public static IQueryable<DesktopModule> DesktopModulesReadOnly(this DnnDbContext context)
        => context.DesktopModules.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for tabs without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="Tab"/> entities without tracking.</returns>
    /// <remarks>
    /// Use this method when you only need to read tab data without making changes.
    /// For hierarchical queries, consider including Children or Parent navigation properties.
    /// </remarks>
    public static IQueryable<Tab> TabsReadOnly(this DnnDbContext context)
        => context.Tabs.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for permissions without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="Permission"/> entities without tracking.</returns>
    public static IQueryable<Permission> PermissionsReadOnly(this DnnDbContext context)
        => context.Permissions.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for module permissions without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="ModulePermission"/> entities without tracking.</returns>
    public static IQueryable<ModulePermission> ModulePermissionsReadOnly(this DnnDbContext context)
        => context.ModulePermissions.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for tab permissions without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="TabPermission"/> entities without tracking.</returns>
    public static IQueryable<TabPermission> TabPermissionsReadOnly(this DnnDbContext context)
        => context.TabPermissions.AsNoTracking();

    /// <summary>
    /// Gets a read-only query for folder permissions without change tracking.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <see cref="FolderPermission"/> entities without tracking.</returns>
    public static IQueryable<FolderPermission> FolderPermissionsReadOnly(this DnnDbContext context)
        => context.FolderPermissions.AsNoTracking();
}
