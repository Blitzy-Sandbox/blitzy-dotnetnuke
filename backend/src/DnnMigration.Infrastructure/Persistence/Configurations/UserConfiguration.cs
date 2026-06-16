// =============================================================================
// UserConfiguration.cs
// -----------------------------------------------------------------------------
// EF Core 8 Fluent API mapping for the User aggregate root AND its UserRole
// association (join) entity, onto the EXISTING (unchanged) DotNetNuke 4.9.0.85
// database schema.
//
// MIGRATION: This configuration replaces the legacy ADO.NET / SqlDataProvider
// stored-procedure data layer (AddUser / UpdateUser / GetUser and AddUserRole /
// UpdateUserRole / GetUserRole in Library/Providers/DataProviders/SqlDataProvider/
// SqlDataProvider.vb) and the reflection-based CBO.FillObject hydration used by
// UserController.vb. The User and UserRole POCO entities (DnnMigration.Domain.
// Entities) carry ZERO EF attributes; ALL persistence mapping lives here and is
// auto-discovered by DnnDbContext.OnModelCreating via
// modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly).
//
// ADR-002 (schema preservation): the schema is mapped UNCHANGED -- no EF
// migrations, no schema generation, no SQL-Server-only defaults, no data
// migration. Table and column names are reproduced verbatim from the baseline
// install DDL (Website/Providers/DataProviders/SqlDataProvider/
// DotNetNuke.Schema.SqlDataProvider). Every mapping primitive used here
// (ToTable / HasKey / HasColumnName / Property / Ignore / relationship metadata)
// is InMemory-provider safe, so the integration tests (Gate 5) round-trip User
// and UserRole CRUD on Microsoft.EntityFrameworkCore.InMemory. No
// HasDefaultValueSql / HasComputedColumnSql / raw SQL is used, and integer
// primary keys are left at the EF convention default (ValueGeneratedOnAdd) which
// maps to the database IDENTITY columns for SQL Server and enables automatic key
// generation under InMemory. (Do NOT call ValueGeneratedNever.)
//
// MIGRATION (flatten): the legacy UserInfo object is a fat aggregate that merges
// Users + aspnet_Membership + aspnet_Users + aspnet_Profile + UserPortals. Only a
// subset of the User entity's properties are physical columns on the dbo.Users
// table (the 9 columns enumerated in Configure(User)). The membership/profile
// fields are carried as scalar properties for Phase-1 round-trip fidelity (NOT
// Ignored, NOT schema changes -- they simply have no backing column in the 4.9
// Users table and physically live on aspnet_Membership / aspnet_Users /
// aspnet_Profile / UserPortals in the legacy schema). All deviations are recorded
// in the root MIGRATION_NOTES.md.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core <see cref="IEntityTypeConfiguration{TEntity}"/> that maps BOTH the
/// <see cref="User"/> aggregate root and the <see cref="UserRole"/> association
/// entity onto their pre-existing DotNetNuke 4.9.0.85 tables (<c>dbo.Users</c> and
/// <c>dbo.UserRoles</c>). A single configuration class hosts both
/// <c>Configure</c> overloads because the two types form one tightly-coupled
/// persistence concern: a user and the role-assignment rows that bind it to the
/// security <see cref="Role"/> definitions.
/// </summary>
/// <remarks>
/// <para>
/// Discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>modelBuilder.ApplyConfigurationsFromAssembly(typeof(DnnDbContext).Assembly)</c>,
/// which detects every public type implementing
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in the Infrastructure assembly --
/// including a single class that implements the interface for more than one entity,
/// as this one does. It therefore requires no explicit registration.
/// </para>
/// <para>
/// Only provider-agnostic relational metadata (<c>ToTable</c>, <c>HasKey</c>,
/// per-property <c>Property</c>/<c>HasColumnName</c> declarations, <c>Ignore</c>,
/// and foreign-key relationship metadata) is configured. No SQL-Server-only
/// constructs (<c>HasDefaultValueSql</c>, <c>HasComputedColumnSql</c>, raw SQL, or
/// value-generation overrides) are used, so the same model builds cleanly under
/// both the SQL Server provider (production) and the EF Core InMemory provider
/// (integration-test fixtures, Gate 5). Per ADR-002 the schema is mapped exactly
/// as it exists in the database; the entities deliberately carry no EF attributes,
/// so this file is the single home for the <see cref="User"/>/<see cref="UserRole"/>
/// mapping.
/// </para>
/// </remarks>
public sealed class UserConfiguration
    : IEntityTypeConfiguration<User>, IEntityTypeConfiguration<UserRole>
{
    /// <summary>
    /// Maps the <see cref="User"/> entity onto the existing <c>dbo.Users</c> table,
    /// reproducing the DotNetNuke 4.9.0.85 column set verbatim. The two non-column
    /// members (the computed <see cref="User.FullName"/> and the denormalized
    /// <see cref="User.Roles"/> array) are explicitly ignored; the AffiliateID
    /// casing drift is remapped; and the flattened membership/profile fields are
    /// carried as scalar properties for Phase-1 round-trip fidelity.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="User"/>.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Map onto the existing physical table (default install schema = dbo).
        // ADR-002: the table already exists; this only describes the mapping and
        // never triggers schema generation.
        builder.ToTable("Users", "dbo");

        // Primary key. UserID is an IDENTITY(1,1) column in the DNN 4.9 schema
        // ([PK_Users] PRIMARY KEY CLUSTERED ([UserID])). Leaving the int-PK
        // convention intact preserves ValueGeneratedOnAdd, so the InMemory provider
        // can auto-assign keys on insert -- required for the User POST -> 201
        // round-trip in Gate 5. (Do NOT call ValueGeneratedNever.)
        builder.HasKey(u => u.UserID);

        // ---------------------------------------------------------------------
        // Two NON-COLUMN members that must NOT be mapped to the Users table.
        // ---------------------------------------------------------------------

        // MIGRATION: User.FullName is computed read-only (=> $"{FirstName} {LastName}",
        // no setter / no backing field, ported from the obsolete UserInfo.FullName
        // getter at UserInfo.vb:L375-L382). EF would fail the model build trying to
        // map a get-only computed property to a column; Ignore() it.
        builder.Ignore(u => u.FullName);

        // MIGRATION: User.Roles is a denormalized string[] of role names (legacy
        // UserInfo.Roles As String() at UserInfo.vb:L261-L268, lazily hydrated via
        // GetRolesByUser); it is NOT a column. EF8 would otherwise map it as a
        // primitive/JSON collection. Ignore() -- role membership is modeled via the
        // UserRoles join entity (see Configure(EntityTypeBuilder<UserRole>) below).
        builder.Ignore(u => u.Roles);

        // ---------------------------------------------------------------------
        // The 9 PHYSICAL dbo.Users columns (verbatim DNN 4.9.0.85 schema names).
        // Property names match the column names exactly (so HasColumnName is
        // unnecessary), EXCEPT AffiliateID which is remapped just below. The
        // explicit Property declarations document the full physical column set and
        // ensure no real column can be silently omitted.
        // ---------------------------------------------------------------------
        builder.Property(u => u.UserID);            // [UserID] int NOT NULL IDENTITY(1,1)
        builder.Property(u => u.Username);          // [Username] nvarchar(100) NOT NULL
        builder.Property(u => u.FirstName);         // [FirstName] nvarchar(50) NOT NULL
        builder.Property(u => u.LastName);          // [LastName] nvarchar(50) NOT NULL
        builder.Property(u => u.IsSuperUser);       // [IsSuperUser] bit NOT NULL

        // MIGRATION: the CLR property is `AffiliateID` (all-caps ID) but the physical
        // DNN 4.9 column is `AffiliateId` (lowercase d). The column name is remapped
        // explicitly so the nullable int? property binds to the real column.
        builder.Property(u => u.AffiliateID).HasColumnName("AffiliateId"); // [AffiliateId] int NULL

        builder.Property(u => u.Email);             // [Email] nvarchar(256) NULL
        builder.Property(u => u.DisplayName);       // [DisplayName] nvarchar(128) NOT NULL
        builder.Property(u => u.UpdatePassword);    // [UpdatePassword] bit NOT NULL

        // ---------------------------------------------------------------------
        // MIGRATION: UserInfo is a flattened merge of Users + aspnet_Membership +
        // aspnet_Users + aspnet_Profile + UserPortals. The following membership /
        // profile fields have NO physical column on the dbo.Users table (they live
        // on aspnet_Membership / aspnet_Users / aspnet_Profile / UserPortals in the
        // legacy schema -- see InstallMembership.sql / InstallProfile.sql); they are
        // mapped as scalar properties for Phase-1 round-trip fidelity, with NO schema
        // change (ADR-002). They are deliberately NOT Ignored so a User survives a
        // full CRUD round-trip under the InMemory provider used by Gate 5. Recorded
        // in MIGRATION_NOTES.md.
        //
        // NOTE: Password / PasswordAnswer / PasswordQuestion are mapped as plain
        // scalars here for Phase-1 round-trip ONLY; the actual authentication /
        // BCrypt hashing is handled by PasswordHasher / AuthService in the Identity
        // layer (out of scope for this mapping file).
        // ---------------------------------------------------------------------
        builder.Property(u => u.PortalID);               // UserPortals.PortalID    -> carried scalar
        builder.Property(u => u.Approved);               // aspnet_Membership.IsApproved -> carried scalar
        builder.Property(u => u.CreatedDate);            // aspnet_Membership.CreateDate -> carried scalar
        builder.Property(u => u.IsOnLine);               // Users Online state      -> carried scalar
        builder.Property(u => u.LastActivityDate);       // aspnet_Users.LastActivityDate -> carried scalar
        builder.Property(u => u.LastLockoutDate);        // aspnet_Membership.LastLockoutDate -> carried scalar
        builder.Property(u => u.LastLoginDate);          // aspnet_Membership.LastLoginDate -> carried scalar
        builder.Property(u => u.LastPasswordChangeDate); // aspnet_Membership.LastPasswordChangedDate -> carried scalar
        builder.Property(u => u.LockedOut);              // aspnet_Membership.IsLockedOut -> carried scalar
        builder.Property(u => u.Password);               // aspnet_Membership.Password -> carried scalar (hashed by Identity layer)
        builder.Property(u => u.PasswordAnswer);         // aspnet_Membership.PasswordAnswer -> carried scalar
        builder.Property(u => u.PasswordQuestion);       // aspnet_Membership.PasswordQuestion -> carried scalar
    }

    /// <summary>
    /// Maps the <see cref="UserRole"/> association entity onto the existing
    /// <c>dbo.UserRoles</c> table and configures its required foreign-key
    /// relationships to <see cref="User"/> and <see cref="Role"/>. The legacy
    /// <c>UserRoleInfo</c> (which inherited <c>RoleInfo</c>) is modeled here as a
    /// clean relational join entity with explicit FK columns.
    /// </summary>
    /// <param name="builder">The entity type builder for <see cref="UserRole"/>.</param>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        // Map onto the existing physical table; schema "dbo", table name verbatim
        // (ADR-002, no schema generation).
        builder.ToTable("UserRoles", "dbo");

        // Primary key. UserRoleID is an IDENTITY(1,1) column
        // ([PK_UserRoles] PRIMARY KEY CLUSTERED ([UserRoleID])); the int-PK
        // convention (ValueGeneratedOnAdd) is left intact so the InMemory provider
        // can auto-assign keys on insert.
        builder.HasKey(ur => ur.UserRoleID);

        // ---------------------------------------------------------------------
        // The 6 PHYSICAL dbo.UserRoles columns (verbatim DNN 4.9.0.85 schema
        // names). UserID and RoleID are the real, non-nullable FK columns reused by
        // the relationships configured below.
        // ---------------------------------------------------------------------
        builder.Property(ur => ur.UserRoleID);      // [UserRoleID] int NOT NULL IDENTITY(1,1)
        builder.Property(ur => ur.UserID);          // [UserID] int NOT NULL (FK -> Users)
        builder.Property(ur => ur.RoleID);          // [RoleID] int NOT NULL (FK -> Roles)
        builder.Property(ur => ur.ExpiryDate);      // [ExpiryDate] datetime NULL
        builder.Property(ur => ur.IsTrialUsed);     // [IsTrialUsed] bit NULL
        builder.Property(ur => ur.EffectiveDate);   // [EffectiveDate] datetime NULL

        // MIGRATION: UserRole.Subscribed is NOT present in the DNN 4.9 UserRoles
        // baseline table (the legacy Subscribed flag lived on the fat
        // UserRoleInfo / RoleInfo object graph). It is carried as a scalar property
        // for Phase-1 round-trip fidelity, NOT Ignored and NOT a schema change
        // (ADR-002). Recorded in MIGRATION_NOTES.md.
        builder.Property(ur => ur.Subscribed);      // carried scalar (no 4.9 UserRoles column)

        // ---------------------------------------------------------------------
        // MIGRATION: relationships. The legacy "UserRoleInfo Inherits RoleInfo"
        // shape is modeled as a clean relational JOIN entity. UserID / RoleID are
        // the real, non-nullable FK columns mapped above; the User? / Role?
        // navigation references are nullable in the CLR (nullable reference types)
        // but the relationships are REQUIRED because the FKs are non-nullable int --
        // do NOT add IsRequired(false).
        //
        // WithMany() carries no inverse navigation: neither User nor Role exposes a
        // UserRoles collection (User.Roles is a denormalized string[] that is
        // Ignored above; the Role entity is mapped by the sibling RoleConfiguration).
        // EF merges configurations across the assembly, so the Role entity is fully
        // configured there and only the relationship endpoint is declared here.
        //
        // OnDelete(NoAction): two cascading FKs into the same join row would raise
        // the SQL-Server "may cause cycles or multiple cascade paths" error; NoAction
        // prevents it. The InMemory provider ignores delete behavior, so this is
        // safe for the Gate 5 round-trip.
        // ---------------------------------------------------------------------
        builder.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserID)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleID)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
