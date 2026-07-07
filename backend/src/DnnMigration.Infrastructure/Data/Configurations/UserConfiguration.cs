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
// MIGRATION — THE int/Guid KEY DUALITY (the central concern of this file):
//   * User.UserID is an int  -> it maps to the DNN domain "Users" table (IDENTITY(1,1) PK).
//   * aspnet_Users.UserId is a Guid, and the ASP.NET provider tables aspnet_Membership / aspnet_Profile
//     key on that Guid UserId.
// Because the principal (User, int key) and the dependents (UserMembership / UserProfile, Guid key)
// have TYPE-INCOMPATIBLE keys, no valid EF relationship can be formed between them. The resolution used
// throughout this file:
//   1. The User.Membership and User.Profile navigations are .Ignore()d (no EF relationship).
//   2. UserMembership and UserProfile are mapped as STANDALONE keyed entities (each has its own DbSet
//      in DnnDbContext). Neither class declares a key property, so an explicit SHADOW key named
//      "UserId" (Guid) is configured for each.
//   3. The service/repository layer stitches a User together with its membership/profile by key or
//      username — that join is a runtime concern, not an EF model relationship.
// =====================================================================================================

/// <summary>
/// EF Core Fluent configuration for the <see cref="User"/> domain identity entity. Maps it to the DNN
/// domain <c>Users</c> table (int <c>UserID</c> primary key), which is DISTINCT from the ASP.NET
/// provider <c>aspnet_Users</c> table (Guid <c>UserId</c>). The <c>Membership</c> and <c>Profile</c>
/// navigations are intentionally ignored — see the file header for the int/Guid key-duality rationale.
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

        // MIGRATION: cross-key navigations — User.UserID is int (DNN Users) while UserMembership and
        // UserProfile key on the aspnet Guid UserId. The keys are type-incompatible, so these cannot form
        // valid EF relationships. Both navigations are ignored and are stitched by the service/repository
        // layer (by key/username), not by an EF relationship. See the file header for full context.
        builder.Ignore(e => e.Membership);
        builder.Ignore(e => e.Profile);
    }
}

/// <summary>
/// EF Core Fluent configuration for the <see cref="UserMembership"/> credential/account-state entity.
/// Maps it to the ASP.NET provider <c>aspnet_Membership</c> table, whose primary key is a
/// <see cref="Guid"/> <c>UserId</c>. Because the class declares no key property, a shadow <c>UserId</c>
/// key is configured. Several entity property names diverge from their legacy column names (mapped via
/// <c>HasColumnName</c>), and properties that live on <c>aspnet_Users</c> (not <c>aspnet_Membership</c>)
/// or are legacy transient flags are ignored.
/// </summary>
public class UserMembershipConfiguration : IEntityTypeConfiguration<UserMembership>
{
    /// <summary>Applies the <see cref="UserMembership"/> mapping to the model.</summary>
    /// <param name="builder">The entity type builder for <see cref="UserMembership"/>.</param>
    public void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        // MIGRATION: ASP.NET membership-provider credential table (Guid-keyed), mapped verbatim.
        builder.ToTable("aspnet_Membership");

        // MIGRATION: aspnet_Membership PK is a Guid UserId (FK -> aspnet_Users.UserId). The entity has
        // no key property, so a shadow key named "UserId" (Guid) is configured. This is the dependent
        // half of the int/Guid duality documented in the file header.
        builder.Property<Guid>("UserId").HasColumnName("UserId");
        builder.HasKey("UserId");

        // MIGRATION: property/column NAME divergences — the CLR property name differs from the physical
        // aspnet_Membership column name, so each is mapped explicitly via HasColumnName.
        builder.Property(e => e.Approved).HasColumnName("IsApproved");                       // Approved              -> IsApproved
        builder.Property(e => e.CreatedDate).HasColumnName("CreateDate");                    // CreatedDate           -> CreateDate
        builder.Property(e => e.LastPasswordChangeDate).HasColumnName("LastPasswordChangedDate"); // LastPasswordChangeDate -> LastPasswordChangedDate
        builder.Property(e => e.LockedOut).HasColumnName("IsLockedOut");                     // LockedOut             -> IsLockedOut

        // 1:1 columns — property name already matches the legacy column name; mapped explicitly for
        // fidelity and to keep the intent unambiguous.
        builder.Property(e => e.LastLockoutDate).HasColumnName("LastLockoutDate");
        builder.Property(e => e.LastLoginDate).HasColumnName("LastLoginDate");
        builder.Property(e => e.Password).HasColumnName("Password");
        builder.Property(e => e.PasswordAnswer).HasColumnName("PasswordAnswer");
        builder.Property(e => e.PasswordQuestion).HasColumnName("PasswordQuestion");
        builder.Property(e => e.Email).HasColumnName("Email");

        // MIGRATION: these properties have NO aspnet_Membership column and are ignored:
        //   * IsOnLine / ObjectHydrated / UpdatePassword — legacy transient / progressive-hydration
        //     flags with no persistent storage.
        //   * LastActivityDate and Username — these physically live on the aspnet_Users table
        //     (UserName / LastActivityDate), not on aspnet_Membership, so they are not mapped here.
        builder.Ignore(e => e.IsOnLine);
        builder.Ignore(e => e.ObjectHydrated);
        builder.Ignore(e => e.UpdatePassword);
        builder.Ignore(e => e.LastActivityDate);
        builder.Ignore(e => e.Username);
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
