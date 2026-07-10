using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: This file replaces the hand-written ADO.NET column/parameter plumbing that the legacy
// DotNetNuke SqlDataProvider stored procedures used to move Portal / PortalAlias rows in and out of
// SQL Server (Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb dispatching to the
// GetPortals / AddPortalInfo / UpdatePortalInfo / *PortalAlias* procedures). Instead of mapping columns
// by ordinal inside SqlDataReader loops, the mapping is now declared once, up front, via the EF Core 8
// Fluent API. Both configuration classes below are auto-discovered and applied by
// DnnDbContext.OnModelCreating through modelBuilder.ApplyConfigurationsFromAssembly(...); they are never
// referenced directly.
//
// DATA MODEL FIDELITY IS MANDATORY: the legacy database is the system of record and its structure is NOT
// changed. Every table name, column name, and foreign-key constraint name below is reproduced VERBATIM
// from the existing DNN 4.9 schema (authoritatively: the consolidated CREATE TABLE in
// Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider, the cumulative
// version ALTERs in 01.00.00 -> 04.09.00, and the vw_Portals view in 04.05.00). Any Portal member that
// is NOT a physical column of the Portals table (a value the legacy code computed via a view join,
// subquery, lazy count, or in-memory getter) is explicitly Ignore()d so EF never invents a phantom
// column against the real schema.

/// <summary>
/// EF Core 8 Fluent API configuration for the <see cref="Portal"/> entity, mapping it to the existing
/// legacy DNN <c>Portals</c> table without any schema modification.
/// </summary>
public class PortalConfiguration : IEntityTypeConfiguration<Portal>
{
    /// <summary>
    /// Configures the <see cref="Portal"/> entity: table name, primary key, physical-column mappings,
    /// the two integer-backed enum columns, and the non-column properties that must be excluded from the
    /// model to preserve fidelity with the legacy schema.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Portal"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        // Legacy table name preserved verbatim.
        builder.ToTable("Portals");

        // PortalID is the primary key. In the legacy schema it is an IDENTITY column
        // (IDENTITY(-1,1) in the base v1 script, IDENTITY(0,1) in the consolidated schema); either way
        // the store generates the value on insert, so it is store-generated on add.
        builder.HasKey(e => e.PortalID);
        builder.Property(e => e.PortalID)
               .HasColumnName("PortalID")
               .ValueGeneratedOnAdd();

        // --- Physical Portals columns (name preserved verbatim from the legacy schema) -------------
        // Property names were carried over 1:1 from the VB PortalInfo class, so for these the column
        // name equals the property name. Casing is reproduced exactly as it exists in the database.
        builder.Property(e => e.PortalName).HasColumnName("PortalName");
        builder.Property(e => e.LogoFile).HasColumnName("LogoFile");
        builder.Property(e => e.FooterText).HasColumnName("FooterText");
        builder.Property(e => e.ExpiryDate).HasColumnName("ExpiryDate");
        builder.Property(e => e.AdministratorId).HasColumnName("AdministratorId");
        builder.Property(e => e.Currency).HasColumnName("Currency");
        builder.Property(e => e.HostFee).HasColumnName("HostFee");
        builder.Property(e => e.HostSpace).HasColumnName("HostSpace");
        builder.Property(e => e.PageQuota).HasColumnName("PageQuota");
        builder.Property(e => e.UserQuota).HasColumnName("UserQuota");
        builder.Property(e => e.AdministratorRoleId).HasColumnName("AdministratorRoleId");
        builder.Property(e => e.RegisteredRoleId).HasColumnName("RegisteredRoleId");
        builder.Property(e => e.Description).HasColumnName("Description");
        // MIGRATION: preserve the legacy column spelling "KeyWords" (capital W) verbatim.
        builder.Property(e => e.KeyWords).HasColumnName("KeyWords");
        builder.Property(e => e.BackgroundFile).HasColumnName("BackgroundFile");
        // MIGRATION: preserve the legacy all-caps column name "GUID" verbatim.
        builder.Property(e => e.GUID).HasColumnName("GUID");
        builder.Property(e => e.PaymentProcessor).HasColumnName("PaymentProcessor");
        builder.Property(e => e.ProcessorPassword).HasColumnName("ProcessorPassword");
        // MIGRATION: preserve the legacy column spelling "ProcessorUserId" verbatim.
        builder.Property(e => e.ProcessorUserId).HasColumnName("ProcessorUserId");
        builder.Property(e => e.SiteLogHistory).HasColumnName("SiteLogHistory");
        builder.Property(e => e.AdminTabId).HasColumnName("AdminTabId");
        builder.Property(e => e.SplashTabId).HasColumnName("SplashTabId");
        builder.Property(e => e.HomeTabId).HasColumnName("HomeTabId");
        builder.Property(e => e.LoginTabId).HasColumnName("LoginTabId");
        builder.Property(e => e.UserTabId).HasColumnName("UserTabId");
        builder.Property(e => e.DefaultLanguage).HasColumnName("DefaultLanguage");
        // MIGRATION: the CLR property is "TimeZoneOffset" (capital Z), but the physical Portals column
        // is spelled "TimezoneOffset" (lowercase z) in the DNN schema (added in 02.02.00; confirmed in
        // vw_Portals and the consolidated CREATE TABLE). Map to the real column name for fidelity.
        builder.Property(e => e.TimeZoneOffset).HasColumnName("TimezoneOffset");
        builder.Property(e => e.HomeDirectory).HasColumnName("HomeDirectory");

        // --- Integer-backed enum columns -----------------------------------------------------------
        // These columns are plain [int] in the schema; the CLR properties are strongly typed enums.
        // HasConversion<int>() makes the enum <-> int persistence contract explicit (EF's default for
        // enums is already int, but stating it guards against any future convention change).
        // MIGRATION: legacy int column "UserRegistration" -> UserRegistrationType enum.
        builder.Property(e => e.UserRegistration)
               .HasColumnName("UserRegistration")
               .HasConversion<int>();
        // MIGRATION: legacy int column "BannerAdvertising" -> BannerType enum.
        builder.Property(e => e.BannerAdvertising)
               .HasColumnName("BannerAdvertising")
               .HasConversion<int>();

        // --- Non-column properties: excluded from the model to preserve schema fidelity ------------
        // The legacy VB PortalInfo carried several members that are NOT physical Portals columns. In the
        // old stack these were populated by the vw_Portals view (join/subquery), by lazy count getters,
        // or computed in memory. Mapping any of them would make EF expect a column that does not exist,
        // so each is explicitly ignored.
        // MIGRATION: computed get-only property (relative home directory); not a column.
        builder.Ignore(e => e.HomeDirectoryMapPath);
        // MIGRATION: vw_Portals computes AdministratorRoleName via a subquery join to Roles.RoleName; not a physical column.
        builder.Ignore(e => e.AdministratorRoleName);
        // MIGRATION: vw_Portals computes RegisteredRoleName via a subquery join to Roles.RoleName; not a physical column.
        builder.Ignore(e => e.RegisteredRoleName);
        // MIGRATION: legacy lazy count (UserController.GetUserCountByPortal); not a column.
        builder.Ignore(e => e.Users);
        // MIGRATION: legacy lazy count (TabController.GetTabCount); not a column.
        builder.Ignore(e => e.Pages);
        // MIGRATION: vw_Portals surfaces Email from a LEFT JOIN to Users (the administrator's email); the
        // Portals table itself has no Email column, so it is not a physical column here.
        builder.Ignore(e => e.Email);
        // MIGRATION: vw_Portals computes SuperTabId via a subquery over Tabs (the host super-user tab);
        // it is not a physical Portals column.
        builder.Ignore(e => e.SuperTabId);
        // MIGRATION: Version was populated by the DNN framework at runtime, not stored on Portals; it is
        // absent from both vw_Portals and the consolidated Portals table, so it is not a column.
        builder.Ignore(e => e.Version);
    }
}

/// <summary>
/// EF Core 8 Fluent API configuration for the <see cref="PortalAlias"/> entity, mapping it to the
/// existing legacy DNN <c>PortalAlias</c> table (HTTP host-header aliases for a portal).
/// </summary>
public class PortalAliasConfiguration : IEntityTypeConfiguration<PortalAlias>
{
    /// <summary>
    /// Configures the <see cref="PortalAlias"/> entity: table name, primary key, physical-column
    /// mappings, and the foreign key back to <see cref="Portal"/> (owned from this side only).
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="PortalAlias"/> entity type.</param>
    public void Configure(EntityTypeBuilder<PortalAlias> builder)
    {
        // Legacy table name preserved verbatim.
        builder.ToTable("PortalAlias");

        // PortalAliasID is the IDENTITY(1,1) primary key; the store generates it on insert.
        builder.HasKey(e => e.PortalAliasID);
        builder.Property(e => e.PortalAliasID)
               .HasColumnName("PortalAliasID")
               .ValueGeneratedOnAdd();

        builder.Property(e => e.PortalID).HasColumnName("PortalID");
        // MIGRATION: preserve the legacy all-caps prefix column name "HTTPAlias" verbatim.
        builder.Property(e => e.HTTPAlias).HasColumnName("HTTPAlias");

        // Foreign key PortalAlias.PortalID -> Portals.PortalID. This is configured from the PortalAlias
        // side ONLY: the Portal entity intentionally exposes no PortalAliases collection navigation, so
        // the dependent-to-principal relationship uses the parameterless WithMany() (no inverse nav).
        // MIGRATION: the legacy FK is named "FK_PortalAlias_Portals" and declared ON DELETE CASCADE
        // (02.02.02.SqlDataProvider); both are reproduced verbatim to keep the existing schema intact.
        builder.HasOne<Portal>()
               .WithMany()
               .HasForeignKey(e => e.PortalID)
               .HasConstraintName("FK_PortalAlias_Portals")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
