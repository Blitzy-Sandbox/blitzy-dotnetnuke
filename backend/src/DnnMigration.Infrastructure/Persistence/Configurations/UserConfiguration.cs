using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Persistence.Configurations;

// MIGRATION: EF Core 8 Fluent API mapping for the User aggregate (User + UserRole), replacing the legacy
// ADO.NET / SqlDataProvider stored-procedure layer (AddUser / UpdateUser / GetUser / AddUserRole /
// UpdateUserRole / GetUserRole in Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb)
// together with the reflection-based CBO.FillObject hydration. Per ADR-002 the existing DotNetNuke
// 4.9.0.85 schema is mapped UNCHANGED: no migrations, no schema generation, no EnsureCreated, no data
// migration.
//
// The legacy UserInfo.vb value object is a FLATTENED MERGE of the physical [Users] table plus the ASP.NET
// membership/profile/portal tables ([aspnet_Membership], [aspnet_Users], [aspnet_Profile] and
// [UserPortals]) — see Website/Providers/DataProviders/SqlDataProvider/InstallMembership.sql and
// InstallProfile.sql for where those fields physically live. Only nine of the User entity's properties are
// real [Users] columns (per the DotNetNuke.Schema.SqlDataProvider [Users] DDL); the remaining
// membership/profile/portal properties have NO physical column on [Users] and are carried as plain scalar
// properties purely for Phase-1 round-trip fidelity on the in-memory provider (Gate 5) — a deliberate
// decision recorded in the root MIGRATION_NOTES.md, NOT a schema change. Two members are excluded from the
// model entirely: UserInfo.FullName (computed read-only, no backing column) and UserInfo.Roles (a
// denormalized String() array whose relational form is the UserRoles join entity). The single casing drift
// in the legacy schema — entity property AffiliateID vs physical column AffiliateId — is corrected with
// HasColumnName; all other names map verbatim.

/// <summary>
/// Entity Framework Core configuration that maps the <see cref="User"/> and <see cref="UserRole"/> POCO
/// entities onto the pre-existing, unchanged DotNetNuke <c>dbo.Users</c> and <c>dbo.UserRoles</c> tables.
/// A single configuration class hosts both entities because they form one cohesive aggregate: a user is
/// associated with roles through the <see cref="UserRole"/> join entity.
/// </summary>
/// <remarks>
/// <para>
/// The class is discovered automatically by <c>DnnDbContext.OnModelCreating</c> through
/// <c>ModelBuilder.ApplyConfigurationsFromAssembly</c>, which reflects over the Infrastructure assembly for
/// <see cref="IEntityTypeConfiguration{TEntity}"/> implementations; the compiler-generated public
/// parameterless constructor lets EF instantiate it. Configurations for distinct entity types are merged
/// across the assembly, so the <see cref="UserRole"/>&#8594;<see cref="Role"/> relationship declared here
/// composes with the <c>Role</c> mapping owned by <c>RoleConfiguration</c>.
/// </para>
/// <para>
/// Schema-fidelity rules (ADR-002): table and column names are preserved verbatim, integer primary keys
/// retain their database <c>IDENTITY</c> semantics via the EF <c>ValueGeneratedOnAdd</c> convention (which
/// also enables key generation under the in-memory provider used by the integration-test fixtures), and no
/// SQL-Server-specific defaults, computed columns, or raw SQL are configured so the model builds cleanly
/// against <c>Microsoft.EntityFrameworkCore.InMemory</c> as well as SQL Server.
/// </para>
/// </remarks>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>, IEntityTypeConfiguration<UserRole>
{
    /// <summary>
    /// Configures the <see cref="User"/> entity against the physical <c>dbo.Users</c> table: declares the
    /// <c>UserID</c> primary key, ignores the two non-column members (<c>FullName</c>, <c>Roles</c>),
    /// corrects the <c>AffiliateID</c> casing drift, maps the nine real <c>Users</c> columns, and carries
    /// the flattened membership/profile/portal fields as scalar properties for Phase-1 fidelity.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="User"/> entity type.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Physical table: dbo.Users (CREATE TABLE in DotNetNuke.Schema.SqlDataProvider). Schema-qualified
        // and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("Users", "dbo");

        // Primary key: [UserID] (int NOT NULL IDENTITY(1, 1), PK CLUSTERED). Left at the EF
        // ValueGeneratedOnAdd convention (NO ValueGeneratedNever) so the database IDENTITY is honored on
        // SQL Server and key values are still generated under the in-memory provider used by the gates.
        builder.HasKey(u => u.UserID);

        // MIGRATION: User.FullName is computed read-only (no column); Ignore().
        // Legacy UserInfo.FullName = FirstName & " " & LastName; it has no backing field and no physical
        // column, so attempting to map it would fail the model build.
        builder.Ignore(u => u.FullName);

        // MIGRATION: User.Roles is a denormalized string[] of role names (not a column); Ignore() — role
        // membership is modeled via the UserRoles join entity. Legacy UserInfo.Roles As String() was
        // auto-hydrated from RoleController; without this Ignore, EF8 would map the array as a primitive
        // collection, which is not the physical data shape.
        builder.Ignore(u => u.Roles);

        // --- The 9 real, physical [Users] columns (mapped verbatim from the schema DDL) ---
        builder.Property(u => u.UserID).HasColumnName("UserID");                  // [UserID]         int           NOT NULL IDENTITY(1,1) PK
        builder.Property(u => u.Username).HasColumnName("Username");              // [Username]       nvarchar(100) NOT NULL
        builder.Property(u => u.FirstName).HasColumnName("FirstName");            // [FirstName]      nvarchar(50)  NOT NULL
        builder.Property(u => u.LastName).HasColumnName("LastName");              // [LastName]       nvarchar(50)  NOT NULL
        builder.Property(u => u.IsSuperUser).HasColumnName("IsSuperUser");        // [IsSuperUser]    bit           NOT NULL

        // MIGRATION: casing remap — the entity property AffiliateID (capital D) maps to the physical column
        // AffiliateId (lowercase d) as declared in the DotNetNuke.Schema.SqlDataProvider [Users] DDL. The
        // property is int? (nullable column).
        builder.Property(u => u.AffiliateID).HasColumnName("AffiliateId");        // [AffiliateId]    int           NULL

        builder.Property(u => u.Email).HasColumnName("Email");                    // [Email]          nvarchar(256) NULL
        builder.Property(u => u.DisplayName).HasColumnName("DisplayName");        // [DisplayName]    nvarchar(128) NOT NULL
        builder.Property(u => u.UpdatePassword).HasColumnName("UpdatePassword");  // [UpdatePassword] bit           NOT NULL

        // MIGRATION: UserInfo is a flattened merge of Users+aspnet_Membership+aspnet_Profile+UserPortals.
        // These membership/profile fields have no physical column on the Users table; mapped as scalar
        // properties for Phase-1 fidelity, no schema change (ADR-002). Recorded in MIGRATION_NOTES.md.
        // Mapped by EF convention (column name == property name); NOT pointed at the differently-named
        // aspnet_* columns because they belong to other tables. Password/PasswordAnswer/PasswordQuestion are
        // carried as plain scalars here for the Phase-1 round-trip ONLY; the actual authentication/BCrypt
        // hashing is handled by PasswordHasher/AuthService in the Identity layer (out of scope for this file).
        builder.Property(u => u.PortalID);                // flattened from UserPortals
        builder.Property(u => u.Approved);                // flattened from aspnet_Membership
        builder.Property(u => u.CreatedDate);             // flattened from aspnet_Membership
        builder.Property(u => u.IsOnLine);                // flattened activity/online flag (UsersOnline)
        builder.Property(u => u.LastActivityDate);        // flattened from aspnet_Users
        builder.Property(u => u.LastLockoutDate);         // flattened from aspnet_Membership
        builder.Property(u => u.LastLoginDate);           // flattened from aspnet_Membership
        builder.Property(u => u.LastPasswordChangeDate);  // flattened from aspnet_Membership
        builder.Property(u => u.LockedOut);               // flattened from aspnet_Membership
        builder.Property(u => u.Password);                // flattened from aspnet_Membership (hashing in Identity layer)
        builder.Property(u => u.PasswordAnswer);          // flattened from aspnet_Membership
        builder.Property(u => u.PasswordQuestion);        // flattened from aspnet_Membership
    }

    /// <summary>
    /// Configures the <see cref="UserRole"/> join entity against the physical <c>dbo.UserRoles</c> table:
    /// declares the <c>UserRoleID</c> primary key, maps the six real <c>UserRoles</c> columns, carries the
    /// non-baseline <c>Subscribed</c> flag as a scalar, and wires the required
    /// <see cref="UserRole"/>&#8594;<see cref="User"/> and <see cref="UserRole"/>&#8594;<see cref="Role"/>
    /// relationships via their foreign keys.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="UserRole"/> entity type.</param>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        // Physical table: dbo.UserRoles (CREATE TABLE in DotNetNuke.Schema.SqlDataProvider). Schema-
        // qualified and named verbatim per ADR-002 — the table already exists and must not be regenerated.
        builder.ToTable("UserRoles", "dbo");

        // Primary key: [UserRoleID] (int NOT NULL IDENTITY(1, 1), PK CLUSTERED). Left at the EF
        // ValueGeneratedOnAdd convention (NO ValueGeneratedNever) for SQL Server IDENTITY parity and
        // in-memory key generation.
        builder.HasKey(ur => ur.UserRoleID);

        // --- The 6 real, physical [UserRoles] columns (mapped verbatim from the schema DDL) ---
        builder.Property(ur => ur.UserRoleID).HasColumnName("UserRoleID");        // [UserRoleID]    int      NOT NULL IDENTITY(1,1) PK
        builder.Property(ur => ur.UserID).HasColumnName("UserID");                // [UserID]        int      NOT NULL  (FK -> Users)
        builder.Property(ur => ur.RoleID).HasColumnName("RoleID");                // [RoleID]        int      NOT NULL  (FK -> Roles)
        builder.Property(ur => ur.ExpiryDate).HasColumnName("ExpiryDate");        // [ExpiryDate]    datetime NULL
        builder.Property(ur => ur.IsTrialUsed).HasColumnName("IsTrialUsed");      // [IsTrialUsed]   bit      NULL
        builder.Property(ur => ur.EffectiveDate).HasColumnName("EffectiveDate");  // [EffectiveDate] datetime NULL

        // MIGRATION: UserRole.Subscribed has no column in the DotNetNuke 4.9.0.85 [UserRoles] baseline;
        // carried as a scalar property (NOT Ignored) for Phase-1 round-trip fidelity, no schema change
        // (ADR-002). Mapped by convention; InMemory-safe. Recorded in MIGRATION_NOTES.md.
        builder.Property(ur => ur.Subscribed);

        // MIGRATION: the UserRole -> User / UserRole -> Role relationships replace the legacy
        // "UserRoleInfo Inherits RoleInfo" inheritance and the co-mingled controller joins. Both are
        // required relationships because the FK columns UserID/RoleID are non-nullable int, even though the
        // CLR navigation reference types (User?/Role?) are nullable — so do NOT add IsRequired(false).
        // WithMany() carries no inverse collection: neither User nor Role exposes a UserRoles collection
        // (User.Roles is the Ignored denormalized string[]). The Role principal's table/key are configured
        // in RoleConfiguration and merged across the Infrastructure assembly. OnDelete(NoAction) avoids
        // SQL-Server multiple-cascade-path warnings under Gate 1 (--warnaserror); the in-memory provider
        // ignores delete behavior, so Gate 5 is unaffected.
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
