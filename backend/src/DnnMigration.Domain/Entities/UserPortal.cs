namespace DnnMigration.Domain.Entities;

// MIGRATION: schema-faithful entity for the dbo.UserPortals join table — the record of WHICH PORTAL(S) a
// user belongs to. In legacy DotNetNuke the PortalID lived on the FAT, denormalized UserInfo object
// (Library/Components/Users/UserInfo.vb), which DNN surfaced through the vw_Users view — a LEFT OUTER JOIN
// of dbo.Users to dbo.UserPortals on UserId (PortalId / Authorised come from UserPortals). Per ADR-002
// schema fidelity, the dbo.Users table has NO PortalID column (it has exactly 9 physical columns), so the
// User entity Ignore()s PortalID (see UserConfiguration.cs) and this dedicated entity carries portal
// membership against its REAL table. The UserRepository reproduces the legacy vw_Users join (and the
// GetUserByUsername / GetUsersByEmail / GetUsers portal-scoping WHERE clauses) via explicit queries against
// this entity, and rehydrates User.PortalID from the matching membership row. Recorded in MIGRATION_NOTES.md
// §4.2 (Users / UserPortals) and Deviation Index D-018.
//
// Authoritative column set (verbatim from the DNN 4.9.0.85 install DDL,
// Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider, table UserPortals):
//   [UserId] int NOT NULL,                    -- composite PK part 1
//   [PortalId] int NOT NULL,                  -- composite PK part 2
//   [UserPortalId] int NOT NULL IDENTITY(1,1) -- surrogate identity (NOT the key)
//   [CreatedDate] datetime NOT NULL DEFAULT(getdate()),
//   [Authorised] bit NOT NULL DEFAULT(1)
//   PRIMARY KEY CLUSTERED ([UserId], [PortalId])
//
// Pure POCO: ZERO framework dependencies (no EF/DataAnnotation attributes); all persistence mapping lives in
// Infrastructure/Persistence/Configurations/UserPortalConfiguration.cs. Mapped as an INDEPENDENT entity with
// scalar UserId/PortalId foreign-key columns (no navigation to User/Portal), matching the join-table mapping
// style used by TabModule — the repository performs explicit joins. Property names match the physical column
// names verbatim (UserId / PortalId — note the lowercase 'd', distinct from User.UserID), so no HasColumnName
// remap is required.
public class UserPortal
{
    /// <summary>Member user (<c>UserPortals.[UserId]</c>); composite primary-key part 1.</summary>
    public int UserId { get; set; }

    /// <summary>Portal the user belongs to (<c>UserPortals.[PortalId]</c>); composite primary-key part 2.</summary>
    public int PortalId { get; set; }

    // MIGRATION: [UserPortalId] is a NOT NULL IDENTITY(1,1) surrogate column that is NOT the primary key
    // (the PK is the composite [UserId, PortalId]). It is mapped ValueGeneratedOnAdd in the configuration so
    // EF excludes it from INSERT (the SQL Server IDENTITY fills it; the InMemory provider auto-generates it)
    // and reads it back. It is not used for any business logic.
    /// <summary>Surrogate identity column (<c>UserPortals.[UserPortalId]</c>, IDENTITY(1,1); not the key).</summary>
    public int UserPortalId { get; set; }

    // MIGRATION: legacy DB DEFAULT(getdate()); the SQL-Server default constraint is NOT declared in the EF
    // mapping (InMemory-safe per ADR-002). The repository sets this explicitly when creating a membership row.
    /// <summary>When the membership was created (<c>UserPortals.[CreatedDate]</c>, NOT NULL).</summary>
    public DateTime CreatedDate { get; set; }

    // MIGRATION: legacy DB DEFAULT(1); the default is supplied by the CLR initializer below rather than a
    // SQL-Server default constraint (InMemory-safe per ADR-002).
    /// <summary>Whether the membership is authorised (<c>UserPortals.[Authorised]</c>, NOT NULL).</summary>
    public bool Authorised { get; set; } = true;
}
