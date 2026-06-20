using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION: EF Core 8 Fluent API mapping for the Role aggregate (Role + RoleGroup), replacing the
// legacy ADO.NET / SqlDataProvider stored-procedure layer (AddRole / UpdateRole / GetRoles /
// AddRoleGroup in Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb) together with
// the reflection-based CBO.FillObject hydration. Per ADR-002 the existing DotNetNuke 4.9.0.85 schema
// is mapped UNCHANGED: no migrations, no schema generation, no EnsureCreated, no data migration.
// The legacy RoleInfo.vb / RoleGroupInfo.vb value objects map 1:1 onto the physical dbo.Roles and
// dbo.RoleGroups tables with NO casing drift and NO non-column members, so neither HasColumnName nor
// Ignore is required for any property. The legacy Null.NullInteger / Null.NullString sentinels became
// plain C# nullable reference/value types in the Domain entities, so no special EF value handling is
// needed here. This clean 1:1 mapping for the Role aggregate is mirrored in the root MIGRATION_NOTES.md.

/// <summary>
/// Entity Framework Core configuration that maps the <see cref="Role"/> and <see cref="RoleGroup"/>
/// POCO entities onto the pre-existing, unchanged DotNetNuke <c>dbo.Roles</c> and <c>dbo.RoleGroups</c>
/// tables. A single configuration class hosts both entities because they form one cohesive aggregate
/// (a role optionally belongs to a role group).
/// </summary>
/// <remarks>
/// <para>
/// The class is discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ModelBuilder.ApplyConfigurationsFromAssembly</c>, which reflects over the Infrastructure assembly
/// for <see cref="IEntityTypeConfiguration{TEntity}"/> implementations; the compiler-generated public
/// parameterless constructor lets EF instantiate it.
/// </para>
/// <para>
/// Schema-fidelity rules (ADR-002): table and column names are preserved verbatim, integer primary keys
/// retain their database <c>IDENTITY</c> semantics via the EF <c>ValueGeneratedOnAdd</c> convention
/// (which also enables key generation under the in-memory provider used by the integration-test
/// fixtures), and no SQL-Server-specific defaults, computed columns, or raw SQL are configured so the
/// model builds cleanly against <c>Microsoft.EntityFrameworkCore.InMemory</c> as well as SQL Server.
/// </para>
/// </remarks>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<RoleGroup>
{
    /// <summary>
    /// Configures the <see cref="Role"/> entity against the physical <c>dbo.Roles</c> table, mapping
    /// every property to its identically named column and declaring <c>RoleID</c> as the primary key.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Physical table: dbo.Roles (CREATE TABLE in DotNetNuke.Schema.SqlDataProvider). Schema-qualified
        // and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("Roles", "dbo");

        // Primary key: [RoleID] (int NOT NULL IDENTITY(0, 1), PK NONCLUSTERED). Left at the EF
        // ValueGeneratedOnAdd convention (NO ValueGeneratedNever) so the database IDENTITY is honored on
        // SQL Server and key values are still generated under the in-memory provider.
        builder.HasKey(r => r.RoleID);

        // All 15 columns are mapped by name (entity property name == physical column name, verbatim), so
        // no HasColumnName and no Ignore is required. Each property is declared explicitly below to serve
        // as the authoritative column manifest for the Roles table; EF default type mapping is used
        // throughout (no HasColumnType / HasMaxLength / HasDefaultValueSql) to keep the model InMemory-safe.
        builder.Property(r => r.RoleID);            // [RoleID]           int            NOT NULL IDENTITY(0,1)
        builder.Property(r => r.PortalID);          // [PortalID]         int            NOT NULL  (scalar FK -> Portals)
        builder.Property(r => r.RoleName);          // [RoleName]         nvarchar(50)   NOT NULL
        builder.Property(r => r.Description);       // [Description]      nvarchar(1000) NULL
        builder.Property(r => r.ServiceFee);        // [ServiceFee]       money          NULL  (float <- money: default mapping; no HasColumnType)
        builder.Property(r => r.BillingFrequency);  // [BillingFrequency] char(1)        NULL  (string? <- char(1): default mapping)
        builder.Property(r => r.TrialPeriod);       // [TrialPeriod]      int            NULL
        builder.Property(r => r.TrialFrequency);    // [TrialFrequency]   char(1)        NULL  (string? <- char(1): default mapping)
        builder.Property(r => r.BillingPeriod);     // [BillingPeriod]    int            NULL
        builder.Property(r => r.TrialFee);          // [TrialFee]         money          NULL  (float <- money: default mapping; no HasColumnType)
        builder.Property(r => r.IsPublic);          // [IsPublic]         bit            NOT NULL
        builder.Property(r => r.AutoAssignment);    // [AutoAssignment]   bit            NOT NULL
        builder.Property(r => r.RoleGroupID);       // [RoleGroupID]      int            NULL  (scalar FK -> RoleGroups)
        builder.Property(r => r.RSVPCode);          // [RSVPCode]         nvarchar(50)   NULL  (all-caps RSVP preserved verbatim)
        builder.Property(r => r.IconFile);          // [IconFile]         nvarchar(100)  NULL

        // MIGRATION: PortalID and RoleGroupID are plain scalar foreign-key columns in the legacy schema.
        // The Role entity intentionally exposes no Portal / RoleGroup navigation property, so NO EF
        // relationship is configured here — they remain simple int scalars, preserving the data shape and
        // behavior of the legacy RoleInfo.vb value object.
    }

    /// <summary>
    /// Configures the <see cref="RoleGroup"/> entity against the physical <c>dbo.RoleGroups</c> table,
    /// mapping every property to its identically named column and declaring <c>RoleGroupID</c> as the
    /// primary key.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="RoleGroup"/> entity type.</param>
    public void Configure(EntityTypeBuilder<RoleGroup> builder)
    {
        // Physical table: dbo.RoleGroups (CREATE TABLE in DotNetNuke.Schema.SqlDataProvider). Schema-
        // qualified and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("RoleGroups", "dbo");

        // Primary key: [RoleGroupID] (int NOT NULL IDENTITY(0, 1), PK NONCLUSTERED). Left at the EF
        // ValueGeneratedOnAdd convention (NO ValueGeneratedNever) for SQL Server IDENTITY parity and
        // in-memory key generation.
        builder.HasKey(rg => rg.RoleGroupID);

        // All 4 columns are mapped by name (entity property name == physical column name, verbatim), so
        // no HasColumnName and no Ignore is required. Declared explicitly as the authoritative column
        // manifest for the RoleGroups table; EF default type mapping is used throughout.
        builder.Property(rg => rg.RoleGroupID);     // [RoleGroupID]   int            NOT NULL IDENTITY(0,1)
        builder.Property(rg => rg.PortalID);        // [PortalID]      int            NOT NULL  (scalar FK -> Portals)
        builder.Property(rg => rg.RoleGroupName);   // [RoleGroupName] nvarchar(50)   NOT NULL
        builder.Property(rg => rg.Description);     // [Description]   nvarchar(1000) NULL

        // MIGRATION: PortalID is a plain scalar foreign-key column; the RoleGroup entity exposes no Portal
        // navigation property, so NO EF relationship is configured — preserving the data shape and behavior
        // of the legacy RoleGroupInfo.vb value object.
    }
}
