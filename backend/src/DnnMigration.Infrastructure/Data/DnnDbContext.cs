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

    public DbSet<Module> Modules => Set<Module>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Tab> Tabs => Set<Tab>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<ModulePermission> ModulePermissions => Set<ModulePermission>();

    public DbSet<TabPermission> TabPermissions => Set<TabPermission>();

    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    // MIGRATION (CP2 review — DependencyInjection #1): credential and per-portal site-setting stores backing the
    // Application ICredentialStore / IPortalSettingsService ports.
    // - UserCredentials is the BCrypt credential store that REPLACES aspnet_Membership (AAP §0.5.2) — a documented
    //   schema-COMPATIBILITY addition (MIGRATION_NOTES.md), not an alteration of an existing legacy table.
    // - ModuleSettings is the EXISTING DNN [ModuleSettings] table; per-portal site settings physically live there,
    //   scoped to the portal's "Site Settings" module (the legacy PortalSettings indirection).
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();

    public DbSet<ModuleSetting> ModuleSettings => Set<ModuleSetting>();

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
