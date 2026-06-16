using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION: EF Core 8 Fluent mapping for the Role aggregate, replacing the legacy
// ADO.NET / SqlDataProvider stored-procedure layer (AddRole / UpdateRole / GetRoles /
// AddRoleGroup) and the reflection-based CBO.FillObject hydration used by
// RoleController.vb. The legacy RoleInfo (Library/Components/Security/Roles/RoleInfo.vb)
// and RoleGroupInfo (RoleGroupInfo.vb) value objects map 1:1 — with NO casing drift and
// NO non-column members — onto the pre-existing, UNCHANGED DotNetNuke 4.9.0.85 "Roles" and
// "RoleGroups" tables, so every property name equals its physical column name verbatim and
// neither HasColumnName nor Ignore is required. Per ADR-002 the schema is mapped as-is: no
// EF migrations, no schema generation, no EnsureCreated, and no data migration. The legacy
// Null.NullInteger / Null.NullString sentinels are represented in the Domain entities as
// plain C# values and nullable reference types (string?), so no special EF handling is
// needed here. Recorded in the root MIGRATION_NOTES.md.

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps BOTH the
/// <see cref="Role"/> and <see cref="RoleGroup"/> POCO entities onto their existing
/// DotNetNuke database tables (<c>dbo.Roles</c> and <c>dbo.RoleGroups</c>) using the
/// Fluent API only. The Domain entities deliberately carry zero EF attributes; this class
/// is the single home for their persistence metadata.
/// </summary>
/// <remarks>
/// <para>
/// Discovered automatically by <c>DnnDbContext.OnModelCreating</c> via
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>,
/// which detects every public type implementing <see cref="IEntityTypeConfiguration{TEntity}"/>
/// in the Infrastructure assembly — including a single class that implements the interface
/// for more than one entity, as this one does.
/// </para>
/// <para>
/// Only provider-agnostic relational metadata (<c>ToTable</c>, <c>HasKey</c>, and
/// per-property <c>Property</c> declarations) is configured. No SQL-Server-only constructs
/// (<c>HasDefaultValueSql</c>, <c>HasComputedColumnSql</c>, raw SQL, or value-generation
/// overrides) are used, so the same model builds cleanly under both the SQL Server provider
/// (production) and the EF Core InMemory provider (integration-test fixtures, Gate 5). The
/// integer primary keys are left at the EF convention default (<c>ValueGeneratedOnAdd</c>),
/// which maps to the database <c>IDENTITY(0,1)</c> columns for SQL Server and enables
/// automatic key generation under InMemory.
/// </para>
/// </remarks>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>, IEntityTypeConfiguration<RoleGroup>
{
    /// <summary>
    /// Configures the <see cref="Role"/> entity against the existing <c>dbo.Roles</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Role"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Map onto the pre-existing physical table; schema "dbo", table name verbatim (ADR-002).
        builder.ToTable("Roles", "dbo");

        // Primary key: physical PK is [PK_Roles] PRIMARY KEY NONCLUSTERED ([RoleID]).
        // Only the key column is declared here; the NONCLUSTERED physical detail is a
        // SQL-Server storage concern that is intentionally NOT configured (it is irrelevant
        // to EF query/save behavior and is not InMemory-safe). RoleID is an IDENTITY(0,1)
        // column; leaving it at the convention default keeps ValueGeneratedOnAdd in effect.
        builder.HasKey(r => r.RoleID);

        // Map every one of the 15 entity properties as a column. Each property name matches
        // its physical column name exactly, so HasColumnName is unnecessary; the explicit
        // Property declarations document the full column set and the schema-of-record types.
        // Ordered to mirror the physical CREATE TABLE definition.
        builder.Property(r => r.RoleID);                // [RoleID] int NOT NULL IDENTITY(0,1)
        builder.Property(r => r.PortalID);              // [PortalID] int NOT NULL (scalar FK; no navigation)
        // Lengths reproduce the [Roles] install DDL (ADR-002). HasMaxLength /
        // IsFixedLength are provider-agnostic metadata (InMemory-safe).
        builder.Property(r => r.RoleName).HasMaxLength(50);                       // [RoleName] nvarchar(50) NOT NULL
        builder.Property(r => r.Description).HasMaxLength(1000);                  // [Description] nvarchar(1000) NULL
        builder.Property(r => r.ServiceFee);                                     // [ServiceFee] money NULL (float? <-> money; default mapping)
        builder.Property(r => r.BillingFrequency).HasMaxLength(1).IsFixedLength(); // [BillingFrequency] char(1) NULL
        builder.Property(r => r.TrialPeriod);                                    // [TrialPeriod] int NULL
        builder.Property(r => r.TrialFrequency).HasMaxLength(1).IsFixedLength();  // [TrialFrequency] char(1) NULL
        builder.Property(r => r.BillingPeriod);                                  // [BillingPeriod] int NULL
        builder.Property(r => r.TrialFee);                                       // [TrialFee] money NULL (float? <-> money; default mapping)
        builder.Property(r => r.IsPublic);                                       // [IsPublic] bit NOT NULL
        builder.Property(r => r.AutoAssignment);                                 // [AutoAssignment] bit NOT NULL
        builder.Property(r => r.RoleGroupID);                                    // [RoleGroupID] int NULL (scalar FK; no navigation)
        builder.Property(r => r.RSVPCode).HasMaxLength(50);                      // [RSVPCode] nvarchar(50) NULL (note all-caps RSVP)
        builder.Property(r => r.IconFile).HasMaxLength(100);                     // [IconFile] nvarchar(100) NULL

        // PortalID and RoleGroupID are scalar foreign-key columns. The Role entity exposes no
        // Portal / RoleGroup navigation property, so NO EF relationship is configured here;
        // they remain plain int scalars, preserving the legacy flat value-object shape.
    }

    /// <summary>
    /// Configures the <see cref="RoleGroup"/> entity against the existing
    /// <c>dbo.RoleGroups</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="RoleGroup"/> entity type.</param>
    public void Configure(EntityTypeBuilder<RoleGroup> builder)
    {
        // Map onto the pre-existing physical table; schema "dbo", table name verbatim (ADR-002).
        builder.ToTable("RoleGroups", "dbo");

        // Primary key: physical PK is PRIMARY KEY NONCLUSTERED ([RoleGroupID]). RoleGroupID is
        // an IDENTITY(0,1) column; the convention default ValueGeneratedOnAdd is preserved.
        builder.HasKey(rg => rg.RoleGroupID);

        // Map all 4 entity properties as columns; every name matches its physical column
        // verbatim, so no HasColumnName / Ignore is required. Ordered to mirror the DDL.
        // Lengths reproduce the [RoleGroups] install DDL (ADR-002).
        builder.Property(rg => rg.RoleGroupID);                                  // [RoleGroupID] int NOT NULL IDENTITY(0,1)
        builder.Property(rg => rg.PortalID);                                     // [PortalID] int NOT NULL (scalar FK; no navigation)
        builder.Property(rg => rg.RoleGroupName).HasMaxLength(50);               // [RoleGroupName] nvarchar(50) NOT NULL
        builder.Property(rg => rg.Description).HasMaxLength(1000);               // [Description] nvarchar(1000) NULL

        // PortalID is a scalar foreign-key column; no EF relationship is configured (the
        // RoleGroup entity has no Portal navigation property).
    }
}
