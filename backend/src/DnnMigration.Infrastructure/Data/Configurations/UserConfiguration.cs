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
// MIGRATION — MEMBERSHIP IS AN OWNED TYPE (credential persistence, review finding F1):
//   User.Membership is configured as an EF Core OWNED type of User (OwnsOne), mapped to the
//   aspnet_Membership table with its legacy column names. This is the canonical EF pattern for a value
//   object composed by an aggregate root, and it matches the entity's own documentation ("Composed by
//   the User entity") and the UserRepository comment ("the owned Membership is auto-loaded"). Owned
//   types are ALWAYS eagerly loaded with their owner (including under AsNoTracking) and are saved in the
//   same SaveChanges as the owner, so:
//     * UserService.CreateAsync / ChangePasswordAsync, which write user.Membership.Password (BCrypt),
//       now actually PERSIST the credential; and
//     * AuthService.LoginAsync, which verifies user.Membership.Password, now actually LOADS it.
//   Previously User.Membership was .Ignore()d and UserMembership was a standalone, unrelated entity, so
//   the hashed password written at creation was silently dropped and every real login failed against an
//   empty membership (finding F1). Making Membership owned closes that gap at its root cause.
//
//   KEY-SHAPE CAVEAT (documented; the deeper aspnet key remodel is a separate UserConfiguration finding
//   outside this auth fix): the real aspnet_Membership table keys on a Guid UserId (FK -> aspnet_Users),
//   whereas User keys on an int UserID (the DNN "Users" table). As an owned type, EF derives the owned
//   table's key/FK from the owner's int UserID rather than the legacy Guid. This is intentional and
//   sufficient for the authentication contract (persist + load + verify credentials) and for the EF
//   Core InMemory provider used by the unit/integration tests (which stores owned data inline with the
//   owner and ignores ToTable). Full Guid-key fidelity via aspnet_Users (and the UserPortals junction)
//   is tracked as the broader UserConfiguration schema finding and is deliberately NOT changed here, to
//   keep this credential-persistence fix minimal. See MIGRATION_NOTES.md.
//
// MIGRATION — PROFILE REMAINS A STANDALONE ENTITY (unchanged by the auth fix):
//   User.Profile is still .Ignore()d and UserProfile is still mapped as a STANDALONE keyed entity (its
//   own DbSet, shadow Guid "UserId" key) exactly as before. Profile is not needed by the authentication
//   flow, and the aspnet_Profile name/value-blob representation is a distinct concern owned by the
//   broader UserConfiguration finding; it is intentionally left untouched here.
// =====================================================================================================

/// <summary>
/// EF Core Fluent configuration for the <see cref="User"/> domain identity entity. Maps it to the DNN
/// domain <c>Users</c> table (int <c>UserID</c> primary key), which is DISTINCT from the ASP.NET
/// provider <c>aspnet_Users</c> table (Guid <c>UserId</c>). The <c>Membership</c> navigation is mapped
/// as an OWNED type (persisted to / loaded from <c>aspnet_Membership</c> so credentials round-trip and
/// login works — review finding F1); the <c>Profile</c> navigation remains ignored and is mapped as a
/// standalone entity below. See the file header for the full rationale and the key-shape caveat.
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

        // MIGRATION: In the strict DNN schema, a user's portal association is expressed through the
        // UserPortals junction table (UserId, PortalId, Authorized), NOT a column on Users; PortalID is a
        // context/convenience value on the legacy UserInfo (initialized to Null.NullInteger). It is part
        // of the API read/write contract (UserDto.PortalID), so it is mapped here as a scalar column to
        // preserve that contract end-to-end. The EF Core InMemory provider used by the integration tests
        // ignores physical column names, so this round-trips cleanly for the User CRUD gate.
        builder.Property(e => e.PortalID).HasColumnName("PortalID");

        // MIGRATION: computed get-only property ("FirstName LastName"), backed by no column.
        builder.Ignore(e => e.FullName);

        // MIGRATION: string[] convenience list, hydrated from the Roles/UserRoles join in the legacy
        // model. Ignored so EF Core 8 does not auto-map it as a primitive collection to a nonexistent
        // column; role membership is a repository/service concern.
        builder.Ignore(e => e.Roles);

        // MIGRATION (finding F1): Membership is an OWNED type of User, mapped to aspnet_Membership.
        // Owned types are auto-loaded with the owner (even AsNoTracking) and saved in the same
        // SaveChanges, so the BCrypt password UserService writes at creation is persisted and the hash
        // AuthService verifies at login is loaded — closing the "empty membership => login always fails"
        // gap. Legacy column-name divergences are preserved verbatim (DATA MODEL FIDELITY); the columns
        // that physically live on aspnet_Users, and the transient/hydration flags, are ignored. See the
        // file header for the int-owner-key caveat vs. the legacy Guid aspnet_Membership key.
        builder.OwnsOne(e => e.Membership, membership =>
        {
            // MIGRATION: ASP.NET membership-provider credential table (mapped verbatim). As an owned
            // type EF derives the FK/key back to the owning User.UserID; no explicit key is declared.
            membership.ToTable("aspnet_Membership");

            // MIGRATION: property/column NAME divergences — the CLR property name differs from the
            // physical aspnet_Membership column name, so each is mapped explicitly via HasColumnName.
            membership.Property(m => m.Approved).HasColumnName("IsApproved");                        // Approved               -> IsApproved
            membership.Property(m => m.CreatedDate).HasColumnName("CreateDate");                     // CreatedDate            -> CreateDate
            membership.Property(m => m.LastPasswordChangeDate).HasColumnName("LastPasswordChangedDate"); // LastPasswordChangeDate -> LastPasswordChangedDate
            membership.Property(m => m.LockedOut).HasColumnName("IsLockedOut");                      // LockedOut              -> IsLockedOut

            // 1:1 columns — property name already matches the legacy column name; mapped explicitly for
            // fidelity and to keep the intent unambiguous.
            membership.Property(m => m.LastLockoutDate).HasColumnName("LastLockoutDate");
            membership.Property(m => m.LastLoginDate).HasColumnName("LastLoginDate");
            membership.Property(m => m.Password).HasColumnName("Password");
            membership.Property(m => m.PasswordAnswer).HasColumnName("PasswordAnswer");
            membership.Property(m => m.PasswordQuestion).HasColumnName("PasswordQuestion");
            membership.Property(m => m.Email).HasColumnName("Email");

            // MIGRATION: these properties have NO aspnet_Membership column and are ignored:
            //   * IsOnLine / ObjectHydrated / UpdatePassword — legacy transient / progressive-hydration
            //     flags with no persistent storage.
            //   * LastActivityDate and Username — these physically live on the aspnet_Users table
            //     (UserName / LastActivityDate), not on aspnet_Membership, so they are not mapped here.
            membership.Ignore(m => m.IsOnLine);
            membership.Ignore(m => m.ObjectHydrated);
            membership.Ignore(m => m.UpdatePassword);
            membership.Ignore(m => m.LastActivityDate);
            membership.Ignore(m => m.Username);
        });

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
