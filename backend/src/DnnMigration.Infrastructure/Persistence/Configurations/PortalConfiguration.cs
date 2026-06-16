// =============================================================================
// PortalConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the Portal aggregate root and its PortalAlias
// satellite, onto the EXISTING (unchanged) DotNetNuke 4.9.0.85 database schema.
//
// MIGRATION: This configuration replaces the legacy ADO.NET / SqlDataProvider
// stored-procedure data layer (Library/Providers/DataProviders/SqlDataProvider/
// SqlDataProvider.vb) and the reflection-based CBO hydration. The Portal and
// PortalAlias POCO entities (DnnMigration.Domain.Entities) carry ZERO EF
// attributes; ALL persistence mapping lives here and is auto-discovered by
// DnnDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED — no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider). Every mapping primitive used here
// (ToTable / HasKey / HasColumnName / Property) is InMemory-provider safe so the
// integration tests (Gate 5) round-trip Portal CRUD on
// Microsoft.EntityFrameworkCore.InMemory.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps both the
/// <see cref="Portal"/> aggregate root and the <see cref="PortalAlias"/> entity
/// onto their pre-existing DotNetNuke 4.9.0.85 tables (<c>dbo.Portals</c> and the
/// singular <c>dbo.PortalAlias</c>). A single configuration class hosts both
/// <c>Configure</c> overloads because the two types form one tightly-coupled
/// persistence concern: a portal and the HTTP aliases that resolve to it.
/// </summary>
/// <remarks>
/// This class is discovered automatically by <c>DnnDbContext.OnModelCreating</c>
/// through
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>;
/// it therefore requires no explicit registration. Per ADR-002 the schema is
/// mapped exactly as it exists in the database — there are no EF migrations and
/// no schema-generation side effects. The entities deliberately carry no EF Core
/// attributes, so this file is the single home for the Portal/PortalAlias
/// mapping.
/// </remarks>
public sealed class PortalConfiguration
    : IEntityTypeConfiguration<Portal>, IEntityTypeConfiguration<PortalAlias>
{
    /// <summary>
    /// Maps the <see cref="Portal"/> entity onto the existing <c>dbo.Portals</c>
    /// table, reproducing the DotNetNuke 4.9.0.85 column set verbatim.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="Portal"/>.</param>
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        // Map onto the existing physical table (default install schema = dbo).
        // ADR-002: the table already exists; this only describes the mapping and
        // never triggers schema generation.
        builder.ToTable("Portals", "dbo");

        // Primary key. PortalID is an IDENTITY(0,1) column in the DNN schema.
        // Leaving the int-PK convention intact preserves ValueGeneratedOnAdd, so
        // the InMemory provider can auto-assign keys on insert — required for the
        // Portal POST -> 201 round-trip in Gate 5. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(p => p.PortalID);
        builder.Property(p => p.PortalID).HasColumnName("PortalID");

        // MIGRATION: the CLR property is `TimeZoneOffset` (capital Z) but the
        // physical DNN 4.9 column is `TimezoneOffset` (lowercase z). The column
        // name is remapped explicitly so the property binds to the real column.
        builder.Property(p => p.TimeZoneOffset).HasColumnName("TimezoneOffset");

        // ---------------------------------------------------------------------
        // Physical Portals columns (verbatim DNN 4.9.0.85 schema names).
        // The property names already match the column names, but they are mapped
        // explicitly so this configuration is the unambiguous single source of
        // truth and no physical column can be silently omitted or renamed.
        // ---------------------------------------------------------------------
        builder.Property(p => p.PortalName).HasColumnName("PortalName");
        builder.Property(p => p.LogoFile).HasColumnName("LogoFile");
        builder.Property(p => p.FooterText).HasColumnName("FooterText");
        builder.Property(p => p.ExpiryDate).HasColumnName("ExpiryDate");
        builder.Property(p => p.UserRegistration).HasColumnName("UserRegistration");
        builder.Property(p => p.BannerAdvertising).HasColumnName("BannerAdvertising");
        builder.Property(p => p.AdministratorId).HasColumnName("AdministratorId");
        builder.Property(p => p.Currency).HasColumnName("Currency");

        // `HostFee` maps to a SQL `money` column. The default float<->money
        // mapping is used deliberately (NO HasColumnType("money")) to avoid
        // relational-only coupling and stay InMemory-provider safe.
        builder.Property(p => p.HostFee).HasColumnName("HostFee");

        builder.Property(p => p.HostSpace).HasColumnName("HostSpace");
        builder.Property(p => p.AdministratorRoleId).HasColumnName("AdministratorRoleId");
        builder.Property(p => p.RegisteredRoleId).HasColumnName("RegisteredRoleId");
        builder.Property(p => p.Description).HasColumnName("Description");
        builder.Property(p => p.KeyWords).HasColumnName("KeyWords");
        builder.Property(p => p.BackgroundFile).HasColumnName("BackgroundFile");

        // `GUID` maps to a `uniqueidentifier` column that carries a `newid()`
        // default in SQL Server. That default is intentionally NOT declared
        // (NO HasDefaultValueSql("newid()")) to keep the model InMemory-provider
        // safe for the integration tests.
        builder.Property(p => p.GUID).HasColumnName("GUID");

        builder.Property(p => p.PaymentProcessor).HasColumnName("PaymentProcessor");
        builder.Property(p => p.ProcessorUserId).HasColumnName("ProcessorUserId");
        builder.Property(p => p.ProcessorPassword).HasColumnName("ProcessorPassword");
        builder.Property(p => p.SiteLogHistory).HasColumnName("SiteLogHistory");
        builder.Property(p => p.HomeTabId).HasColumnName("HomeTabId");
        builder.Property(p => p.LoginTabId).HasColumnName("LoginTabId");
        builder.Property(p => p.UserTabId).HasColumnName("UserTabId");
        builder.Property(p => p.DefaultLanguage).HasColumnName("DefaultLanguage");
        builder.Property(p => p.AdminTabId).HasColumnName("AdminTabId");
        builder.Property(p => p.HomeDirectory).HasColumnName("HomeDirectory");
        builder.Property(p => p.SplashTabId).HasColumnName("SplashTabId");
        builder.Property(p => p.PageQuota).HasColumnName("PageQuota");
        builder.Property(p => p.UserQuota).HasColumnName("UserQuota");

        // MIGRATION: Email/SuperTabId/Users/Pages/AdministratorRoleName/
        // RegisteredRoleName/Version were not physical Portals columns in DNN 4.9
        // (they were aggregated/looked-up at runtime). Mapped as scalar properties
        // for Phase-1 round-trip fidelity; no schema change (ADR-002). Recorded in
        // MIGRATION_NOTES.md. They are deliberately NOT Ignored so the fat
        // PortalInfo-equivalent object preserves all fields across a CRUD
        // round-trip under the InMemory provider used by Gate 5.
        builder.Property(p => p.Email).HasColumnName("Email");
        builder.Property(p => p.SuperTabId).HasColumnName("SuperTabId");
        builder.Property(p => p.Users).HasColumnName("Users");
        builder.Property(p => p.Pages).HasColumnName("Pages");
        builder.Property(p => p.AdministratorRoleName).HasColumnName("AdministratorRoleName");
        builder.Property(p => p.RegisteredRoleName).HasColumnName("RegisteredRoleName");
        builder.Property(p => p.Version).HasColumnName("Version");
    }

    /// <summary>
    /// Maps the <see cref="PortalAlias"/> entity onto the SINGULAR
    /// <c>dbo.PortalAlias</c> table. The physical table is NOT pluralized even
    /// though the <c>DbSet</c> accessor is conventionally <c>PortalAliases</c>.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="PortalAlias"/>.</param>
    public void Configure(EntityTypeBuilder<PortalAlias> builder)
    {
        // MIGRATION: the physical table is the SINGULAR `PortalAlias` (DNN 4.9
        // schema), not the pluralized `PortalAliases` that an EF naming convention
        // would otherwise infer from the DbSet. It is mapped explicitly here.
        builder.ToTable("PortalAlias", "dbo");

        // Primary key. PortalAliasID is an IDENTITY(1,1) column; the int-PK
        // convention (ValueGeneratedOnAdd) is left intact so the InMemory provider
        // can auto-assign keys on insert.
        builder.HasKey(pa => pa.PortalAliasID);
        builder.Property(pa => pa.PortalAliasID).HasColumnName("PortalAliasID");

        // MIGRATION: PortalAlias.PortalID is a scalar FK to Portals; no navigation
        // property exists on either entity (matches legacy PortalAliasInfo), so no
        // EF relationship is configured — it stays a plain scalar int column.
        builder.Property(pa => pa.PortalID).HasColumnName("PortalID");

        // `HTTPAlias` casing is preserved verbatim (all-caps HTTP) per the DNN
        // schema and the legacy PortalAliasInfo contract.
        builder.Property(pa => pa.HTTPAlias).HasColumnName("HTTPAlias");
    }
}
