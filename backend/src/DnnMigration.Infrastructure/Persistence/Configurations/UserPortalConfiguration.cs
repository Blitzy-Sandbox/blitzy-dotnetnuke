// =============================================================================
// UserPortalConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the UserPortal entity — the record of which
// portal(s) a user belongs to — onto the EXISTING (unchanged) DotNetNuke 4.9.0.85
// dbo.UserPortals table.
//
// MIGRATION (CP3 schema-fidelity correction): the legacy UserInfo fat object
// carried PortalID, which DNN surfaced through the vw_Users view (dbo.Users LEFT
// OUTER JOIN dbo.UserPortals on UserId). PortalID is NOT a physical dbo.Users
// column (the table has exactly 9 columns), so it is Ignore()'d on the User entity
// in UserConfiguration.cs (ADR-002). It is a physical column of dbo.UserPortals,
// which THIS configuration maps. The UserRepository reproduces the legacy vw_Users
// join and the GetUserByUsername / GetUsersByEmail / GetUsers portal-scoping WHERE
// clauses via explicit queries against this entity, and rehydrates User.PortalID
// from the matching membership row. Recorded in MIGRATION_NOTES.md §4.2
// (Users / UserPortals) and Deviation Index D-018.
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED — no EF migrations,
// no schema generation, no SQL-Server-only defaults, no data migration. Table and
// column names are reproduced verbatim from the baseline install DDL
// (Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider,
// table UserPortals). Every mapping primitive used here (ToTable / HasKey / Property
// / ValueGeneratedOnAdd) is InMemory-provider safe; no HasDefaultValueSql /
// HasComputedColumnSql / raw SQL is used.
//
// KEY NOTE: the primary key is the COMPOSITE [UserId, PortalId] (PK_UserPortals
// PRIMARY KEY CLUSTERED ([UserId],[PortalId])). The surrogate [UserPortalId]
// IDENTITY column is NOT the key; it is mapped ValueGeneratedOnAdd so EF excludes
// it from INSERT (the SQL Server IDENTITY fills it; the InMemory integer value
// generator supplies it) and reads it back. The composite key is supplied by the
// caller on insert (UserId + PortalId), so it is not store-generated. Mapped as an
// INDEPENDENT entity: UserId/PortalId are plain scalar FK columns with NO
// navigation, so the model gains no cascade path and the repository performs
// explicit joins (mirroring the TabModule mapping style).
// =============================================================================

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps the
/// <see cref="UserPortal"/> entity onto the pre-existing DotNetNuke 4.9.0.85
/// <c>dbo.UserPortals</c> table. Discovered automatically by
/// <c>DnnDbContext.OnModelCreating</c> via <c>ApplyConfigurationsFromAssembly</c>,
/// so it requires no explicit registration.
/// </summary>
public sealed class UserPortalConfiguration : IEntityTypeConfiguration<UserPortal>
{
    /// <summary>Maps <see cref="UserPortal"/> onto <c>dbo.UserPortals</c>.</summary>
    /// <param name="builder">The entity type builder for <see cref="UserPortal"/>.</param>
    public void Configure(EntityTypeBuilder<UserPortal> builder)
    {
        // Map onto the existing physical table (default install schema = dbo).
        // ADR-002: the table already exists; this only describes the mapping and
        // never triggers schema generation.
        builder.ToTable("UserPortals", "dbo");

        // COMPOSITE primary key. The DNN 4.9 schema declares
        // PK_UserPortals PRIMARY KEY CLUSTERED ([UserId], [PortalId]); the surrogate
        // [UserPortalId] IDENTITY column is deliberately NOT the key. The composite
        // key values are supplied by the caller on insert (they are NOT
        // store-generated), so they round-trip cleanly under the InMemory provider.
        builder.HasKey(up => new { up.UserId, up.PortalId });

        // ---------------------------------------------------------------------
        // Physical dbo.UserPortals columns (verbatim DNN 4.9.0.85 schema names).
        // Property names already equal their column names (UserId / PortalId — note
        // the lowercase 'd', distinct from User.UserID), so HasColumnName is
        // unnecessary; the explicit declarations make this the single source of
        // truth for the column set.
        // ---------------------------------------------------------------------
        builder.Property(up => up.UserId);      // [UserId] int NOT NULL (composite PK part 1)
        builder.Property(up => up.PortalId);    // [PortalId] int NOT NULL (composite PK part 2)

        // MIGRATION: [UserPortalId] int NOT NULL IDENTITY(1,1) is a NON-KEY surrogate.
        // ValueGeneratedOnAdd makes EF exclude it from INSERT (the SQL Server IDENTITY
        // assigns it; the InMemory integer value generator assigns it) and read it
        // back. This is provider-agnostic (no UseIdentityColumn / HasDefaultValueSql),
        // so the same model builds under SQL Server and InMemory (Gate 5).
        builder.Property(up => up.UserPortalId) // [UserPortalId] int NOT NULL IDENTITY(1,1)
            .ValueGeneratedOnAdd();

        // MIGRATION: [CreatedDate]/[Authorised] carry SQL-Server DEFAULT constraints
        // (getdate() / 1) in the DDL. Those defaults are intentionally NOT declared
        // here (no HasDefaultValueSql) to stay InMemory-provider safe; the repository
        // always sets these explicitly when creating a membership row.
        builder.Property(up => up.CreatedDate); // [CreatedDate] datetime NOT NULL
        builder.Property(up => up.Authorised);  // [Authorised] bit NOT NULL
    }
}
