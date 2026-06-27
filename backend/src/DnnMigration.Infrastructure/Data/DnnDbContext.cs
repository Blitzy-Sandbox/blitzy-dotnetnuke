using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Data;

// MIGRATION: EF Core 8 DbContext replaces the legacy reflection-based ADO.NET provider stack.
// - DataProvider.Instance() (the singleton accessor, Library/Components/Providers/Data/DataProvider.vb L48),
//   built by reflection via Framework.Reflection.CreateObject (DataProvider.vb L44) and selected by the
//   <data defaultProvider="SqlDataProvider"> block in Website/release.config, is replaced by this
//   DI-registered DnnDbContext (see Infrastructure/DependencyInjection.cs AddDbContext<DnnDbContext>).
// - The abstract execution members ExecuteNonQuery/ExecuteReader/ExecuteScalar/ExecuteDataSet/ExecuteSQL
//   (DataProvider.vb L58-64), implemented over SqlHelper stored procedures in SqlDataProvider.vb, are
//   replaced by DbSet<T> + async LINQ in the repositories.
// - The transaction members CommitTransaction/GetTransaction/RollbackTransaction (DataProvider.vb L71-74)
//   are replaced by EF Core SaveChangesAsync / Database.BeginTransactionAsync in the IUnitOfWork implementation.
// Code-First mapped to the EXISTING DNN SQL Server schema; tables/columns are bound by the
// IEntityTypeConfiguration<T> classes in Configurations/ (the schema is NOT altered in this phase).
public class DnnDbContext : DbContext
{
    public DnnDbContext(DbContextOptions<DnnDbContext> options)
        : base(options)
    {
    }

    public DbSet<Portal> Portals => Set<Portal>();

    // MIGRATION: the legacy [PortalAlias] table (alias -> portal mapping resolved by
    // DataProvider.GetPortalByAlias). Exposed so PortalRepository.GetByAliasAsync can resolve a host alias to its
    // owning portal. Maps to the EXISTING table via PortalAliasConfiguration; no schema is added or altered.
    public DbSet<PortalAlias> PortalAliases => Set<PortalAlias>();

    public DbSet<Module> Modules => Set<Module>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Tab> Tabs => Set<Tab>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<ModulePermission> ModulePermissions => Set<ModulePermission>();

    public DbSet<TabPermission> TabPermissions => Set<TabPermission>();

    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    // MIGRATION (QA-FINAL Issue #1/#2, CRITICAL): the physical [UserPortals] membership table — the WRITE side of the
    // user<->portal association that vw_Users surfaces (PortalId/Authorised). User maps to [Users] (write) + vw_Users
    // (read); since [Users] has no PortalId column, UserRepository persists membership here.
    public DbSet<UserPortal> UserPortals => Set<UserPortal>();

    // MIGRATION (QA-FINAL Issue #3/#4, CRITICAL): the physical [TabModules] placement table — the WRITE side of the
    // per-page module placement that vw_Modules surfaces (TabId/PaneName/ModuleOrder/...). Module maps to [Modules]
    // (write) + vw_Modules (read); since those placement columns live on [TabModules], ModuleRepository persists them here.
    public DbSet<TabModule> TabModules => Set<TabModule>();

    // MIGRATION (CP-FINAL review - Critical #4): per-portal site-setting store backing the Application
    // IPortalSettingsService port. (The former [UserCredentials] DbSet was REMOVED here: migrated BCrypt credentials
    // now map onto the EXISTING legacy membership schema - aspnet_Applications / aspnet_Users / aspnet_Membership -
    // through the Infrastructure CredentialStore adapter and their IEntityTypeConfiguration<T> classes, so NO new
    // table and NO credential DbSet are required and the no-schema-alteration mandate holds. See CredentialStore.cs
    // and AspNetMembershipConfiguration.cs.)
    // - ModuleSettings is the EXISTING DNN [ModuleSettings] table; per-portal site settings physically live there,
    //   scoped to the portal's "Site Settings" module (the legacy PortalSettings indirection).
    public DbSet<ModuleSetting> ModuleSettings => Set<ModuleSetting>();

    // MIGRATION (CP-final review - profile workflow parity): the EXISTING DNN profile EAV tables. The DNN user
    // profile stores per-portal property DEFINITIONS in [ProfilePropertyDefinition] and per-user VALUES in
    // [UserProfile]; UserService.GetProfileAsync/UpdateProfileAsync read/upsert against these (replacing the legacy
    // ProfileController + UserProfile.vb GetPropertyValue/SetProfileProperty). Mapped to the EXISTING tables via
    // ProfilePropertyDefinitionConfiguration / UserProfileValueConfiguration; NO schema is added or altered.
    public DbSet<ProfilePropertyDefinition> ProfilePropertyDefinitions => Set<ProfilePropertyDefinition>();

    public DbSet<UserProfileValue> UserProfileValues => Set<UserProfileValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MIGRATION: Auto-discovers and applies every IEntityTypeConfiguration<T> in this assembly
        // (the Configurations/ folder). Those configs own ALL Fluent mapping: ToTable/HasKey/HasColumnName/
        // HasConstraintName, the 4 navigation relationships, and the Permission-hierarchy TPC mapping strategy.
        // Do NOT add inline modelBuilder.Entity<T>() calls here — keep this method limited to base + the scan
        // so every relationship/mapping is configured exactly once.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly);
    }
}
