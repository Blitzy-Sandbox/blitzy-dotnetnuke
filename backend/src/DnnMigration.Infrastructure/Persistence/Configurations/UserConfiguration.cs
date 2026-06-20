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
// membership/profile/portal properties have NO physical column on [Users].
//
// MIGRATION (DEV-031 — CP2 schema-fidelity correction): per ADR-002 every property without a physical
// [Users] column is EXCLUDED from the table mapping with Ignore() — they are NOT mapped (nor carried) as
// scalar columns. EF therefore never emits SQL referencing nonexistent [Users] columns (the prior "carry as
// scalar for in-memory round-trip" approach is removed — it violated ADR-002 and would fail against SQL
// Server). The CLR properties remain on the entity for DTO/AutoMapper projection; their values are populated
// by the repository/service layer (joins/projections against the membership/profile/portal source tables)
// when that layer is implemented in a later checkpoint — never by this physical mapping. UserInfo.FullName
// (computed read-only, no backing column) and UserInfo.Roles (a denormalized String() array whose relational
// form is the UserRoles join entity) are likewise excluded. The single casing drift in the legacy schema —
// entity property AffiliateID vs physical column AffiliateId — is corrected with HasColumnName; all other
// real-column names map verbatim.

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
    /// <c>UserID</c> primary key, maps the nine real <c>Users</c> columns (correcting the <c>AffiliateID</c>
    /// casing drift), and ignores every non-physical member — <c>FullName</c>, <c>Roles</c>, and the
    /// flattened membership/profile/portal fields — so EF maps the physical schema ONLY (ADR-002).
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

        // MIGRATION (DEV-031 — CP2 schema-fidelity correction): UserInfo is a flattened merge of
        // Users+aspnet_Membership+aspnet_Profile+UserPortals. The fields below have NO physical column on the
        // [Users] table, so per ADR-002 they are Ignore()d — NOT mapped (nor carried) as scalar columns —
        // ensuring EF never queries/inserts nonexistent [Users] columns (the prior scalar mapping violated
        // ADR-002 and would fail against SQL Server). The CLR properties remain on the entity for
        // DTO/AutoMapper projection and are populated by the repository/service layer (joins/projections
        // against the membership/profile/portal source tables) in a later checkpoint.
        // Password/PasswordAnswer/PasswordQuestion are likewise non-[Users] columns; actual
        // authentication/BCrypt hashing lives in PasswordHasher/AuthService (Identity layer).
        builder.Ignore(u => u.PortalID);                // no [Users] column (source: UserPortals)
        builder.Ignore(u => u.Approved);                // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.CreatedDate);             // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.IsOnLine);                // no [Users] column (source: UsersOnline activity)
        builder.Ignore(u => u.LastActivityDate);        // no [Users] column (source: aspnet_Users)
        builder.Ignore(u => u.LastLockoutDate);         // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.LastLoginDate);           // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.LastPasswordChangeDate);  // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.LockedOut);               // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.Password);                // no [Users] column (source: aspnet_Membership; hashing in Identity layer)
        builder.Ignore(u => u.PasswordAnswer);          // no [Users] column (source: aspnet_Membership)
        builder.Ignore(u => u.PasswordQuestion);        // no [Users] column (source: aspnet_Membership)
    }

    /// <summary>
    /// Configures the <see cref="UserRole"/> join entity against the physical <c>dbo.UserRoles</c> table:
    /// declares the <c>UserRoleID</c> primary key, maps the six real <c>UserRoles</c> columns, ignores the
    /// non-baseline <c>Subscribed</c> flag (no physical column), and wires the required
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

        // MIGRATION (DEV-031 — CP2 schema-fidelity correction): UserRole.Subscribed has no column in the
        // DotNetNuke 4.9.0.85 [UserRoles] baseline, so per ADR-002 it is Ignore()d — NOT carried as a scalar
        // column — so EF never references a nonexistent [UserRoles] column. The CLR property remains for
        // projection; it is populated by the repository/service layer if needed in a later checkpoint.
        builder.Ignore(ur => ur.Subscribed);

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
