using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Infrastructure.Data.Configurations;

// =====================================================================================================
// MIGRATION: This file replaces the ADO.NET / SqlDataProvider stored-procedure mapping for the user
// identity aggregate (legacy Library/Components/Users/UserInfo.vb + Membership/UserMembership.vb +
// Profile/UserProfile.vb) with EF Core 8 Fluent (IEntityTypeConfiguration<T>) mappings against the
// EXISTING legacy schema. It is auto-discovered by DnnDbContext.OnModelCreating via
// ApplyConfigurationsFromAssembly. No table structures are altered; every mapped column preserves its
// legacy name verbatim (DATA MODEL FIDELITY).
//
// MIGRATION — SCHEMA FIDELITY (review finding #1): the user aggregate is now modelled against the REAL
// legacy tables, with NO invented columns:
//
//   * [Users] (this configuration) maps ONLY the columns that physically exist on the DNN domain
//     "Users" table (int IDENTITY UserID PK; Username, FirstName, LastName, DisplayName, Email,
//     IsSuperUser, AffiliateId). It NO LONGER maps a scalar [Users].[PortalID] column — the legacy
//     schema has no such column. A user's portal association is the dedicated [UserPortals] junction
//     table (composite key UserId+PortalId), mapped by UserPortalConfiguration; UserRepository writes a
//     junction row on create and projects User.PortalID from it on read. The CLR User.PortalID property
//     is therefore a TRANSIENT (unmapped) carrier that preserves the UserDto.PortalID API contract.
//
//   * [aspnet_Membership] is now a STANDALONE, GUID-keyed entity (UserMembershipConfiguration), NOT an
//     EF owned type of the int-keyed User. The real table keys on a uniqueidentifier [UserId] and
//     carries required NOT NULL columns (ApplicationId, Password, PasswordFormat, PasswordSalt,
//     IsApproved, IsLockedOut, the four date columns, and the four failed-attempt columns). Modelling
//     it as an owned type of the int UserID invented an integer key column and omitted those required
//     columns — generating SQL that cannot run against the existing schema (the finding). User.Membership
//     is therefore .Ignore()d here, and UserRepository bridges the credential row (a VALID read/write
//     projection using a deterministic UserId) so the BCrypt password UserService writes at creation is
//     persisted and the hash AuthService verifies at login is loaded — preserving the credential
//     round-trip WITHOUT the invalid owned mapping. See UserRepository and MIGRATION_NOTES.md.
//
// MIGRATION — PROFILE REMAINS A STANDALONE ENTITY (unchanged):
//   User.Profile is still .Ignore()d and UserProfile is still mapped as a STANDALONE keyed entity (its
//   own DbSet, shadow Guid "UserId" key) exactly as before — this was already schema-faithful.
// =====================================================================================================

/// <summary>
/// EF Core Fluent configuration for the <see cref="User"/> domain identity entity. Maps it to the DNN
/// domain <c>Users</c> table (int <c>UserID</c> primary key), which is DISTINCT from the ASP.NET
/// provider <c>aspnet_Users</c> table (Guid <c>UserId</c>). Only the columns that physically exist on
/// <c>Users</c> are mapped: the invented <c>PortalID</c> scalar is removed (portal association is the
/// <c>UserPortals</c> junction — see <see cref="UserPortalConfiguration"/>), and <c>Membership</c> is
/// <c>Ignore()</c>d here and mapped as a STANDALONE GUID-keyed <c>aspnet_Membership</c> entity (see
/// <see cref="UserMembershipConfiguration"/>). The <c>Profile</c> navigation remains ignored and is
/// mapped as a standalone entity below. See the file header for the full schema-fidelity rationale.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Applies the <see cref="User"/> mapping to the model.</summary>
    /// <param name="builder">The entity type builder for <see cref="User"/>.</param>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // MIGRATION: DNN domain Users table (int UserID) — DISTINCT from aspnet_Users (Guid UserId).
        builder.ToTable("Users");

        // MIGRATION: Users.UserID is an IDENTITY(1,1) int primary key in the legacy schema.
        builder.HasKey(e => e.UserID);
        builder.Property(e => e.UserID).HasColumnName("UserID").ValueGeneratedOnAdd();

        // MIGRATION: DNN denormalizes some name/email fields directly onto the Users table (rather than
        // keeping them solely on aspnet_Users/aspnet_Profile), so these are mapped as physical Users
        // columns. Legacy column names are preserved verbatim via HasColumnName.
        builder.Property(e => e.Username).HasColumnName("Username");
        builder.Property(e => e.FirstName).HasColumnName("FirstName");
        builder.Property(e => e.LastName).HasColumnName("LastName");
        builder.Property(e => e.DisplayName).HasColumnName("DisplayName");
        builder.Property(e => e.Email).HasColumnName("Email");
        builder.Property(e => e.IsSuperUser).HasColumnName("IsSuperUser");

        // MIGRATION: entity property AffiliateID -> legacy column "AffiliateId" (casing divergence).
        builder.Property(e => e.AffiliateID).HasColumnName("AffiliateId");

        // MIGRATION (SCHEMA FIDELITY — finding #1): the legacy DNN schema has NO [Users].[PortalID]
        // column. A user's portal association is expressed through the dedicated [UserPortals] junction
        // table (composite key UserId+PortalId; see UserPortalConfiguration). The previous mapping
        // invented a [Users].[PortalID] column, which cannot exist against the real schema. PortalID is
        // therefore IGNORED as a physical column here; it remains a TRANSIENT CLR carrier on the User
        // entity that UserRepository projects from the [UserPortals] junction on read (and writes a
        // junction row for on create), preserving the UserDto.PortalID API contract end-to-end without
        // inventing a column.
        builder.Ignore(e => e.PortalID);

        // MIGRATION: computed get-only property ("FirstName LastName"), backed by no column.
        builder.Ignore(e => e.FullName);

        // MIGRATION: string[] convenience list, hydrated from the Roles/UserRoles join in the legacy
        // model. Ignored so EF Core 8 does not auto-map it as a primitive collection to a nonexistent
        // column; role membership is a repository/service concern.
        builder.Ignore(e => e.Roles);

        // MIGRATION (SCHEMA FIDELITY — finding #1): Membership is NO LONGER an EF owned type of User.
        // The real [aspnet_Membership] table keys on a uniqueidentifier [UserId] (FK -> aspnet_Users)
        // and carries required NOT NULL columns; modelling it as an owned type of the int-keyed [Users]
        // row derived an integer key column and omitted the required columns, generating SQL that cannot
        // run against the existing schema. It is instead mapped as a STANDALONE GUID-keyed entity by
        // UserMembershipConfiguration. The navigation is IGNORED here (no invalid int-owned relationship);
        // UserRepository bridges the credential row (persist on create/update, hydrate on read) using a
        // deterministic UserId projection, so the credential round-trip that login depends on is
        // preserved WITHOUT the invalid mapping.
        builder.Ignore(e => e.Membership);

        // MIGRATION: Profile navigation stays IGNORED (no EF relationship). UserProfile is mapped as a
        // STANDALONE entity by UserProfileConfiguration below (its own DbSet, shadow Guid "UserId" key),
        // exactly as before — the authentication flow never touches Profile, and the aspnet_Profile
        // name/value-blob remodel is a distinct concern outside this credential-persistence fix.
        builder.Ignore(e => e.Profile);
    }
}

/// <summary>
/// EF Core Fluent configuration for the <see cref="UserProfile"/> entity. Maps it to the ASP.NET
/// provider <c>aspnet_Profile</c> table (Guid <c>UserId</c> primary key). The legacy
/// <c>aspnet_Profile</c> table stores profile data as a serialized name/value blob
/// (<c>PropertyNames</c> / <c>PropertyValuesString</c> / <c>PropertyValuesBinary</c> +
/// <c>LastUpdatedDate</c>); the individual profile fields exposed by <see cref="UserProfile"/> are NOT
/// physical columns, so every scalar profile field (and the non-column helper members) is ignored.
/// Because the class declares no key property, a shadow <c>UserId</c> key is configured.
/// </summary>
public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    /// <summary>Applies the <see cref="UserProfile"/> mapping to the model.</summary>
    /// <param name="builder">The entity type builder for <see cref="UserProfile"/>.</param>
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // MIGRATION: ASP.NET profile-provider table (Guid-keyed), mapped verbatim.
        builder.ToTable("aspnet_Profile");

        // MIGRATION: aspnet_Profile PK is a Guid UserId (FK -> aspnet_Users.UserId). The entity has no
        // key property, so a shadow key named "UserId" (Guid) is configured — the dependent half of the
        // int/Guid duality documented in the file header.
        builder.Property<Guid>("UserId").HasColumnName("UserId");
        builder.HasKey("UserId");

        // MIGRATION: the legacy aspnet_Profile table does NOT expose one column per profile field.
        // Instead it persists a serialized name/value blob across PropertyNames / PropertyValuesString /
        // PropertyValuesBinary (with LastUpdatedDate). Consequently every individual profile field below
        // has NO physical column and is ignored — attempting to map any of them would target a
        // nonexistent column. Serializing/deserializing the blob into these fields is a
        // repository/service responsibility, not an EF column mapping.
        builder.Ignore(e => e.Cell);
        builder.Ignore(e => e.City);
        builder.Ignore(e => e.Country);
        builder.Ignore(e => e.Fax);
        builder.Ignore(e => e.FirstName);
        builder.Ignore(e => e.IM);
        builder.Ignore(e => e.LastName);
        builder.Ignore(e => e.PostalCode);
        builder.Ignore(e => e.PreferredLocale);
        builder.Ignore(e => e.Region);
        builder.Ignore(e => e.Street);
        builder.Ignore(e => e.Telephone);
        builder.Ignore(e => e.TimeZone);
        builder.Ignore(e => e.Unit);
        builder.Ignore(e => e.Website);

        // MIGRATION: non-column helper members — FullName is a computed get-only property; IsDirty and
        // ObjectHydrated are legacy change-tracking / progressive-hydration flags with no storage; and
        // ProfileProperties is a ProfilePropertyDefinitionCollection reference that EF would otherwise
        // attempt to treat as a navigation. All are ignored.
        builder.Ignore(e => e.FullName);
        builder.Ignore(e => e.IsDirty);
        builder.Ignore(e => e.ObjectHydrated);
        builder.Ignore(e => e.ProfileProperties);

        // MIGRATION: map the REAL aspnet_Profile columns as shadow properties (the entity has no CLR
        // members for them) so the SQL Server model faithfully mirrors the physical table. These are
        // harmless for the InMemory provider used by the integration tests. PropertyValuesBinary is an
        // "image" column -> byte[].
        builder.Property<string>("PropertyNames").HasColumnName("PropertyNames");
        builder.Property<string>("PropertyValuesString").HasColumnName("PropertyValuesString");
        builder.Property<byte[]>("PropertyValuesBinary").HasColumnName("PropertyValuesBinary");
        builder.Property<DateTime>("LastUpdatedDate").HasColumnName("LastUpdatedDate");
    }
}
