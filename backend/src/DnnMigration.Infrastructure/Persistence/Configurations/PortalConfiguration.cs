using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core 8 Fluent API configuration that maps the <see cref="Portal"/> and
/// <see cref="PortalAlias"/> domain POCOs onto the pre-existing, UNCHANGED DotNetNuke
/// <c>4.9.0.85</c> <c>[dbo].[Portals]</c> and <c>[dbo].[PortalAlias]</c> tables.
/// </summary>
/// <remarks>
/// <para>
/// This is the SINGLE HOME for the EF mapping of both entities. The Domain entities carry zero
/// framework attributes (Clean Architecture inner ring); every table name, key, and column name
/// is described here and auto-discovered by <c>DnnDbContext.OnModelCreating</c> via
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>. A single
/// class intentionally implements two <see cref="IEntityTypeConfiguration{TEntity}"/> contracts --
/// the two <c>Configure</c> methods are distinct overloads (different builder types), and the
/// assembly scan applies both.
/// </para>
/// <para>
/// ADR-002 -- the schema is mapped UNCHANGED: no EF migrations, no schema generation, no
/// <c>EnsureCreated</c>, and no data migration. Mapping is purely descriptive. To keep the model
/// fully compatible with the <c>Microsoft.EntityFrameworkCore.InMemory</c> provider used by the
/// integration-test suite (Gate 5), NO relational-only constructs are configured: no
/// <c>HasDefaultValueSql</c>, no <c>HasComputedColumnSql</c>, no raw SQL, and no
/// <c>HasColumnType</c>. Integer identity primary keys are left at the EF convention default
/// (<c>ValueGeneratedOnAdd</c>) -- <c>ValueGeneratedNever()</c> is deliberately NOT called -- so the
/// InMemory provider can synthesize keys on insert and the Portal CRUD POST-201 path round-trips.
/// </para>
/// </remarks>
public sealed class PortalConfiguration
    : IEntityTypeConfiguration<Portal>, IEntityTypeConfiguration<PortalAlias>
{
    /// <summary>
    /// Maps the <see cref="Portal"/> aggregate root onto the existing DotNetNuke
    /// <c>[dbo].[Portals]</c> table (default install qualification of
    /// <c>{databaseOwner}[{objectQualifier}Portals]</c>). Column names are taken verbatim from the
    /// baseline schema DDL; no schema is generated or altered (ADR-002).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Portal"/>.</param>
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        // Map onto the pre-existing, unchanged DNN 4.9 table (ADR-002).
        builder.ToTable("Portals", "dbo");

        // PortalID is an IDENTITY(0,1) column. Key generation is intentionally left at the EF
        // convention default (ValueGeneratedOnAdd) -- NOT ValueGeneratedNever() -- so the InMemory
        // provider can auto-generate keys on insert, enabling the Portal POST->201 round-trip (Gate 5).
        builder.HasKey(p => p.PortalID);

        // ---- Physical [Portals] columns (verbatim DNN 4.9 schema names, DDL order) ----
        builder.Property(p => p.PortalID).HasColumnName("PortalID");
        builder.Property(p => p.PortalName).HasColumnName("PortalName");
        builder.Property(p => p.LogoFile).HasColumnName("LogoFile");
        builder.Property(p => p.FooterText).HasColumnName("FooterText");
        builder.Property(p => p.ExpiryDate).HasColumnName("ExpiryDate");
        builder.Property(p => p.UserRegistration).HasColumnName("UserRegistration");
        builder.Property(p => p.BannerAdvertising).HasColumnName("BannerAdvertising");
        builder.Property(p => p.AdministratorId).HasColumnName("AdministratorId");
        builder.Property(p => p.Currency).HasColumnName("Currency");

        // MIGRATION: [HostFee] is a SQL `money` column (legacy VB Single -> C# float). It is mapped
        // directly WITHOUT HasColumnType("money") to avoid relational-only coupling and keep the model
        // InMemory-safe for Gate 5; the provider's default CLR-to-store mapping is preserved.
        builder.Property(p => p.HostFee).HasColumnName("HostFee");

        builder.Property(p => p.HostSpace).HasColumnName("HostSpace");
        builder.Property(p => p.AdministratorRoleId).HasColumnName("AdministratorRoleId");
        builder.Property(p => p.RegisteredRoleId).HasColumnName("RegisteredRoleId");
        builder.Property(p => p.Description).HasColumnName("Description");
        builder.Property(p => p.KeyWords).HasColumnName("KeyWords");
        builder.Property(p => p.BackgroundFile).HasColumnName("BackgroundFile");

        // MIGRATION: [GUID] is a `uniqueidentifier` column whose legacy DDL default is `newid()`.
        // HasDefaultValueSql("newid()") is intentionally NOT configured (SQL-Server-only; would break
        // the InMemory provider in Gate 5). Per ADR-002 no default/value generation is introduced.
        builder.Property(p => p.GUID).HasColumnName("GUID");

        builder.Property(p => p.PaymentProcessor).HasColumnName("PaymentProcessor");
        builder.Property(p => p.ProcessorUserId).HasColumnName("ProcessorUserId");
        builder.Property(p => p.ProcessorPassword).HasColumnName("ProcessorPassword");
        builder.Property(p => p.SiteLogHistory).HasColumnName("SiteLogHistory");
        builder.Property(p => p.HomeTabId).HasColumnName("HomeTabId");
        builder.Property(p => p.LoginTabId).HasColumnName("LoginTabId");
        builder.Property(p => p.UserTabId).HasColumnName("UserTabId");
        builder.Property(p => p.DefaultLanguage).HasColumnName("DefaultLanguage");

        // MIGRATION: the entity property is `TimeZoneOffset` (Pascal-case `Z`) but the physical DNN
        // column is `TimezoneOffset` (lowercase `z`). Remap the column name so the property binds to
        // the real column and schema fidelity is preserved (ADR-002).
        builder.Property(p => p.TimeZoneOffset).HasColumnName("TimezoneOffset");

        builder.Property(p => p.AdminTabId).HasColumnName("AdminTabId");
        builder.Property(p => p.HomeDirectory).HasColumnName("HomeDirectory");
        builder.Property(p => p.SplashTabId).HasColumnName("SplashTabId");
        builder.Property(p => p.PageQuota).HasColumnName("PageQuota");
        builder.Property(p => p.UserQuota).HasColumnName("UserQuota");

        // MIGRATION: Email / SuperTabId / Users / Pages / AdministratorRoleName / RegisteredRoleName /
        // Version were NOT physical [Portals] columns in DNN 4.9 -- verified absent from the baseline
        // CREATE TABLE DDL and from the AddPortalInfo/UpdatePortalInfo procedures in SqlDataProvider.vb.
        // In legacy they were aggregated/looked-up at runtime: Users via UserController.GetUserCountByPortal,
        // Pages via TabController.GetTabCount, the role names via admin/registered role lookups, and Email
        // from the portal administrator account. For Phase 1 they are mapped as ordinary scalar properties
        // (deliberately NOT Ignored) so the fat PortalInfo-equivalent object round-trips intact under the
        // InMemory provider (Gate 5); no schema change is introduced (ADR-002). Recorded in MIGRATION_NOTES.md.
        builder.Property(p => p.Email).HasColumnName("Email");
        builder.Property(p => p.SuperTabId).HasColumnName("SuperTabId");
        builder.Property(p => p.Users).HasColumnName("Users");
        builder.Property(p => p.Pages).HasColumnName("Pages");
        builder.Property(p => p.AdministratorRoleName).HasColumnName("AdministratorRoleName");
        builder.Property(p => p.RegisteredRoleName).HasColumnName("RegisteredRoleName");
        builder.Property(p => p.Version).HasColumnName("Version");
    }

    /// <summary>
    /// Maps the <see cref="PortalAlias"/> entity onto the existing DotNetNuke
    /// <c>[dbo].[PortalAlias]</c> table. The physical table name is SINGULAR even though the
    /// conventional DbSet accessor is plural; the singular name is preserved verbatim (ADR-002).
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="PortalAlias"/>.</param>
    public void Configure(EntityTypeBuilder<PortalAlias> builder)
    {
        // MIGRATION: the physical DNN table is SINGULAR `PortalAlias`
        // (CREATE TABLE {databaseOwner}[{objectQualifier}PortalAlias]), NOT the pluralized
        // `PortalAliases` that the DbSet name / EF pluralization convention would imply. Map to the
        // singular table name to preserve schema fidelity (ADR-002). This is the single most common
        // mapping mistake for this table.
        builder.ToTable("PortalAlias", "dbo");

        // PortalAliasID is an IDENTITY(1,1) primary key; key generation is left at the EF convention
        // default (ValueGeneratedOnAdd) so the InMemory provider can synthesize keys on insert.
        builder.HasKey(pa => pa.PortalAliasID);

        builder.Property(pa => pa.PortalAliasID).HasColumnName("PortalAliasID");

        // MIGRATION: PortalAlias.PortalID is a scalar foreign key to [Portals]. No navigation property
        // exists on either entity (faithful to the legacy PortalAliasInfo value object), so NO EF
        // relationship is configured -- PortalID remains a plain scalar int column.
        builder.Property(pa => pa.PortalID).HasColumnName("PortalID");

        // All-caps `HTTP` casing is preserved verbatim from the DNN schema column name.
        builder.Property(pa => pa.HTTPAlias).HasColumnName("HTTPAlias");
    }
}
