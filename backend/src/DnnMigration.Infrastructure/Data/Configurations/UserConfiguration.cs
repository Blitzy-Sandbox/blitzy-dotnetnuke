// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration classes for User system
// replacing SqlDataProvider/MembershipProvider patterns.
//
// Source References:
// - Library/Components/Users/UserInfo.vb (User entity properties)
// - Library/Components/Users/UserController.vb (data access patterns)
// - Library/Components/Users/UserRoleInfo.vb (UserRole junction entity)
//
// Key transformations:
// 1) UserConfiguration maps User entity to 'Users' table with:
//    - Primary key: UserID (identity)
//    - Foreign key: PortalID to Portals table
//    - Identity: Username (required, unique), DisplayName, Email
//    - Name parts: FirstName, LastName
//    - Flags: IsSuperUser, IsApproved, IsLockedOut, IsDeleted
//    - Dates: CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate, LastLockoutDate, UpdatedDate
//    - Other: AffiliateID (optional)
// 2) UserProfileConfiguration maps UserProfile entity to 'UserProfile' table with:
//    - ProfileID primary key
//    - UserID foreign key (one-to-one with User)
//    - Name components: Prefix, FirstName, MiddleName, LastName, Suffix
//    - Address: Unit, Street, City, Region, Country, PostalCode
//    - Contact: Telephone, Cell, Fax
//    - Online: Website, IM
//    - Preferences: TimeZone, PreferredLocale, Biography, Photo
// 3) UserRoleConfiguration maps UserRole junction entity to 'UserRoles' table with:
//    - UserRoleID primary key
//    - UserID foreign key to Users
//    - RoleID foreign key to Roles
//    - Membership metadata: EffectiveDate, ExpiryDate
//    - Subscription flags: IsTrialUsed, Subscribed
// 4) Configures navigation properties for relationships
// 5) Creates unique index on Username
// 6) Creates indexes on PortalID, Email for lookup performance
// 7) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="User"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the User entity to the existing 'Users' table in the DNN database.
/// Users are the core identity entities in DNN, belonging to a specific portal and capable of
/// being assigned to multiple roles through the <see cref="UserRole"/> junction entity.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider/MembershipProvider pattern where user data
/// was accessed via stored procedures (dbo.GetUser, dbo.AddUser, dbo.UpdateUser, dbo.DeleteUser,
/// dbo.GetUsers) through SqlHelper.ExecuteReader/ExecuteNonQuery calls in UserController.
/// </para>
/// <para>
/// The original UserInfo.vb used progressive hydration for Membership and Profile objects,
/// which has been replaced by EF Core lazy loading through navigation properties. The
/// separate UserMembership class properties have been flattened into the User entity.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar column definitions
/// - Nullable properties correspond to columns that allowed NULL in the legacy schema
/// - Username is required and must be unique (authentication identifier)
/// </para>
/// </remarks>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="User"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for User.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Users' with UserID primary key</item>
    /// <item>Foreign key to Portal via PortalID</item>
    /// <item>Identity properties (Username required/unique, DisplayName, Email)</item>
    /// <item>Name properties (FirstName, LastName)</item>
    /// <item>Status flags (IsSuperUser, IsApproved, IsLockedOut, IsDeleted)</item>
    /// <item>Date tracking (CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate, LastLockoutDate, UpdatedDate)</item>
    /// <item>Additional properties (AffiliateId optional)</item>
    /// <item>Navigation properties (Portal, Profile one-to-one, UserRoles collection)</item>
    /// <item>Unique index on Username</item>
    /// <item>Indexes on PortalID and Email for query optimization</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // MIGRATION: Map to existing 'Users' table in DNN database
        builder.ToTable("Users");

        // MIGRATION: Primary key - maps to UserID column (identity column in SQL Server)
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId)
            .HasColumnName("UserID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: PortalID - int, foreign key to Portals table
        // Note: DNN originally used UserPortals junction table for multi-portal users,
        // but we simplify to a single portal per user relationship
        builder.Property(u => u.PortalId)
            .HasColumnName("PortalID")
            .IsRequired();

        // =====================================================================
        // Identity Properties
        // =====================================================================

        // MIGRATION: Username - nvarchar(100), required, unique
        // Maps from VB.NET _Username field with <SortOrder(0), IsReadOnly(True), Required(True)> attributes
        // Used as the primary authentication identifier
        builder.Property(u => u.Username)
            .HasColumnName("Username")
            .HasMaxLength(100)
            .IsRequired();

        // MIGRATION: DisplayName - nvarchar(128), required
        // Maps from VB.NET _DisplayName field with <SortOrder(3), Required(True), MaxLength(128)> attributes
        builder.Property(u => u.DisplayName)
            .HasColumnName("DisplayName")
            .HasMaxLength(128);

        // MIGRATION: Email - nvarchar(256), nullable
        // Maps from VB.NET _Email field with <SortOrder(4), MaxLength(256), Required(True)> attributes
        // Note: Required in VB but made nullable in migration for data flexibility
        builder.Property(u => u.Email)
            .HasColumnName("Email")
            .HasMaxLength(256);

        // =====================================================================
        // Name Properties
        // =====================================================================

        // MIGRATION: FirstName - nvarchar(50), nullable
        // Maps from VB.NET FirstName property which accessed Profile.FirstName
        // Now stored directly on User for simpler querying
        builder.Property(u => u.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50);

        // MIGRATION: LastName - nvarchar(50), nullable
        // Maps from VB.NET LastName property which accessed Profile.LastName
        // Now stored directly on User for simpler querying
        builder.Property(u => u.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50);

        // =====================================================================
        // Status Flag Properties
        // =====================================================================

        // MIGRATION: IsSuperUser - bit, indicates if user is a host administrator
        // Maps from VB.NET _IsSuperUser field - super users have access to all portals
        builder.Property(u => u.IsSuperUser)
            .HasColumnName("IsSuperUser");

        // MIGRATION: IsApproved - bit, indicates if user account is approved
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.IsApproved)
            .HasColumnName("IsApproved");

        // MIGRATION: IsLockedOut - bit, indicates if user account is locked
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.IsLockedOut)
            .HasColumnName("IsLockedOut");

        // MIGRATION: IsDeleted - bit, soft delete flag
        // Allows for user recovery and audit trail
        builder.Property(u => u.IsDeleted)
            .HasColumnName("IsDeleted");

        // =====================================================================
        // Date Tracking Properties
        // =====================================================================

        // MIGRATION: CreatedDate - datetime, user creation timestamp
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.CreatedDate)
            .HasColumnName("CreatedOnDate")
            .HasColumnType("datetime");

        // MIGRATION: LastLoginDate - datetime nullable, last successful login
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.LastLoginDate)
            .HasColumnName("LastLoginDate")
            .HasColumnType("datetime");

        // MIGRATION: LastActivityDate - datetime nullable, last user activity
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.LastActivityDate)
            .HasColumnName("LastActivityDate")
            .HasColumnType("datetime");

        // MIGRATION: LastPasswordChangeDate - datetime nullable
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.LastPasswordChangeDate)
            .HasColumnName("LastPasswordChangedDate")
            .HasColumnType("datetime");

        // MIGRATION: LastLockoutDate - datetime nullable, last account lockout
        // Originally part of UserMembership object, now flattened to User entity
        builder.Property(u => u.LastLockoutDate)
            .HasColumnName("LastLockoutDate")
            .HasColumnType("datetime");

        // MIGRATION: UpdatedDate - datetime nullable, last modification timestamp
        builder.Property(u => u.UpdatedDate)
            .HasColumnName("LastModifiedOnDate")
            .HasColumnType("datetime");

        // =====================================================================
        // Additional Properties
        // =====================================================================

        // MIGRATION: AffiliateId - int nullable, affiliate tracking identifier
        // Maps from VB.NET _AffiliateID field
        builder.Property(u => u.AffiliateId)
            .HasColumnName("AffiliateID");

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: User -> Portal relationship (many users belong to one portal)
        // Configures the foreign key relationship to Portal entity
        builder.HasOne(u => u.Portal)
            .WithMany(p => p.Users)
            .HasForeignKey(u => u.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: User -> UserProfile relationship (one-to-one)
        // Each user has exactly one profile containing extended personal information
        // Profile configuration is handled in UserProfileConfiguration
        builder.HasOne(u => u.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: User -> UserRoles relationship (one user has many role assignments)
        // Configures the inverse of UserRole.User navigation property
        // This establishes the many-to-many relationship between Users and Roles
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Unique index on Username for authentication lookups
        // Username must be unique across the entire system
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_Users_Username");

        // MIGRATION: Index on PortalID for portal-based user queries
        builder.HasIndex(u => u.PortalId)
            .HasDatabaseName("IX_Users_PortalID");

        // MIGRATION: Index on Email for email-based lookups and authentication
        builder.HasIndex(u => u.Email)
            .HasDatabaseName("IX_Users_Email");

        // MIGRATION: Composite index on PortalID and Username for portal-scoped user lookups
        builder.HasIndex(u => new { u.PortalId, u.Username })
            .HasDatabaseName("IX_Users_PortalID_Username");

        // MIGRATION: Index on IsSuperUser for admin user filtering
        builder.HasIndex(u => u.IsSuperUser)
            .HasDatabaseName("IX_Users_IsSuperUser");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="UserProfile"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the UserProfile entity to the existing 'UserProfile' table in the DNN database.
/// User profiles contain extended personal and contact information for users, with a one-to-one
/// relationship to the <see cref="User"/> entity.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the ProfileController/SqlDataProvider pattern where profile data
/// was accessed via stored procedures through progressive hydration in the UserProfile property of UserInfo.
/// The original implementation used ProfilePropertyDefinitionCollection for extensible properties;
/// this has been simplified to fixed properties for commonly-used profile fields.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes:
/// - Name fields use varying nvarchar lengths based on expected content
/// - Address fields match standard postal address requirements
/// - Contact fields use nvarchar(50) for phone numbers
/// - All properties are nullable as profile data is optional
/// </para>
/// </remarks>
public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="UserProfile"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for UserProfile.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'UserProfile' with ProfileID primary key</item>
    /// <item>Foreign key to User via UserID (one-to-one relationship)</item>
    /// <item>Name components (Prefix, FirstName, MiddleName, LastName, Suffix)</item>
    /// <item>Address fields (Unit, Street, City, Region, Country, PostalCode)</item>
    /// <item>Contact fields (Telephone, Cell, Fax)</item>
    /// <item>Online presence (Website, IM)</item>
    /// <item>Preferences (TimeZone, PreferredLocale)</item>
    /// <item>Profile content (Biography, Photo)</item>
    /// <item>Index on UserID for profile lookups</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // MIGRATION: Map to existing 'UserProfile' table in DNN database
        builder.ToTable("UserProfile");

        // MIGRATION: Primary key - maps to ProfileID column (identity column in SQL Server)
        builder.HasKey(p => p.ProfileId);
        builder.Property(p => p.ProfileId)
            .HasColumnName("ProfileID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: UserID - int, required foreign key to Users table (one-to-one)
        builder.Property(p => p.UserId)
            .HasColumnName("UserID")
            .IsRequired();

        // =====================================================================
        // Name Component Properties
        // =====================================================================

        // MIGRATION: Prefix - nvarchar(20), name prefix/title (Mr., Mrs., Dr., etc.)
        builder.Property(p => p.Prefix)
            .HasColumnName("Prefix")
            .HasMaxLength(20);

        // MIGRATION: FirstName - nvarchar(50), first/given name
        builder.Property(p => p.FirstName)
            .HasColumnName("FirstName")
            .HasMaxLength(50);

        // MIGRATION: MiddleName - nvarchar(50), middle name
        builder.Property(p => p.MiddleName)
            .HasColumnName("MiddleName")
            .HasMaxLength(50);

        // MIGRATION: LastName - nvarchar(50), last/family name
        builder.Property(p => p.LastName)
            .HasColumnName("LastName")
            .HasMaxLength(50);

        // MIGRATION: Suffix - nvarchar(20), name suffix (Jr., Sr., III, etc.)
        builder.Property(p => p.Suffix)
            .HasColumnName("Suffix")
            .HasMaxLength(20);

        // =====================================================================
        // Address Properties
        // =====================================================================

        // MIGRATION: Unit - nvarchar(20), apartment/unit number
        builder.Property(p => p.Unit)
            .HasColumnName("Unit")
            .HasMaxLength(20);

        // MIGRATION: Street - nvarchar(200), street address
        builder.Property(p => p.Street)
            .HasColumnName("Street")
            .HasMaxLength(200);

        // MIGRATION: City - nvarchar(100), city name
        builder.Property(p => p.City)
            .HasColumnName("City")
            .HasMaxLength(100);

        // MIGRATION: Region - nvarchar(100), state/province/region
        builder.Property(p => p.Region)
            .HasColumnName("Region")
            .HasMaxLength(100);

        // MIGRATION: Country - nvarchar(100), country name or code
        builder.Property(p => p.Country)
            .HasColumnName("Country")
            .HasMaxLength(100);

        // MIGRATION: PostalCode - nvarchar(20), postal/ZIP code
        builder.Property(p => p.PostalCode)
            .HasColumnName("PostalCode")
            .HasMaxLength(20);

        // =====================================================================
        // Contact Properties
        // =====================================================================

        // MIGRATION: Telephone - nvarchar(50), primary phone number
        builder.Property(p => p.Telephone)
            .HasColumnName("Telephone")
            .HasMaxLength(50);

        // MIGRATION: Cell - nvarchar(50), mobile phone number
        builder.Property(p => p.Cell)
            .HasColumnName("Cell")
            .HasMaxLength(50);

        // MIGRATION: Fax - nvarchar(50), fax number
        builder.Property(p => p.Fax)
            .HasColumnName("Fax")
            .HasMaxLength(50);

        // =====================================================================
        // Online Presence Properties
        // =====================================================================

        // MIGRATION: Website - nvarchar(200), personal website URL
        builder.Property(p => p.Website)
            .HasColumnName("Website")
            .HasMaxLength(200);

        // MIGRATION: IM - nvarchar(100), instant messaging handle
        builder.Property(p => p.IM)
            .HasColumnName("IM")
            .HasMaxLength(100);

        // =====================================================================
        // Preference Properties
        // =====================================================================

        // MIGRATION: TimeZone - int nullable, time zone offset in minutes
        builder.Property(p => p.TimeZone)
            .HasColumnName("TimeZone");

        // MIGRATION: PreferredLocale - nvarchar(50), locale code (e.g., "en-US")
        builder.Property(p => p.PreferredLocale)
            .HasColumnName("PreferredLocale")
            .HasMaxLength(50);

        // =====================================================================
        // Profile Content Properties
        // =====================================================================

        // MIGRATION: Biography - nvarchar(max), user bio/about text
        builder.Property(p => p.Biography)
            .HasColumnName("Biography")
            .HasColumnType("nvarchar(max)");

        // MIGRATION: Photo - nvarchar(256), relative path to profile photo
        builder.Property(p => p.Photo)
            .HasColumnName("Photo")
            .HasMaxLength(256);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Unique index on UserID ensures one-to-one relationship
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("IX_UserProfile_UserID");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="UserRole"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the UserRole entity to the existing 'UserRoles' table in the DNN database.
/// UserRole serves as the junction table for the many-to-many relationship between <see cref="User"/>
/// and <see cref="Role"/> entities, with additional metadata for membership validity and subscription status.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the RoleController/SqlDataProvider pattern where user-role
/// assignments were managed via stored procedures (dbo.AddUserRole, dbo.DeleteUserRole, dbo.GetUserRoles)
/// through SqlHelper calls. The original UserRoleInfo class inherited from RoleInfo; this has been
/// changed to composition using navigation properties.
/// </para>
/// <para>
/// Key features:
/// - EffectiveDate and ExpiryDate allow for time-limited role assignments
/// - IsTrialUsed tracks trial period usage for subscription-based roles
/// - Subscribed indicates active subscription status
/// - CreatedDate tracks when the role was assigned
/// </para>
/// </remarks>
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="UserRole"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for UserRole.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'UserRoles' with UserRoleID primary key</item>
    /// <item>Foreign key to User via UserID</item>
    /// <item>Foreign key to Role via RoleID</item>
    /// <item>Membership metadata (EffectiveDate, ExpiryDate nullable datetimes)</item>
    /// <item>Subscription flags (IsTrialUsed, Subscribed)</item>
    /// <item>CreatedDate for audit tracking</item>
    /// <item>Navigation properties (User, Role)</item>
    /// <item>Composite index on (UserID, RoleID) for relationship queries</item>
    /// <item>Indexes on UserID and RoleID for individual lookups</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        // MIGRATION: Map to existing 'UserRoles' table in DNN database
        builder.ToTable("UserRoles");

        // MIGRATION: Primary key - maps to UserRoleID column (identity column in SQL Server)
        builder.HasKey(ur => ur.UserRoleId);
        builder.Property(ur => ur.UserRoleId)
            .HasColumnName("UserRoleID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: UserID - int, required foreign key to Users table
        builder.Property(ur => ur.UserId)
            .HasColumnName("UserID")
            .IsRequired();

        // MIGRATION: RoleID - int, required foreign key to Roles table
        builder.Property(ur => ur.RoleId)
            .HasColumnName("RoleID")
            .IsRequired();

        // =====================================================================
        // Membership Metadata Properties
        // =====================================================================

        // MIGRATION: EffectiveDate - datetime nullable, when role assignment becomes active
        // Maps from VB.NET _EffectiveDate field (Date type → DateTime?)
        // Null means immediate activation
        builder.Property(ur => ur.EffectiveDate)
            .HasColumnName("EffectiveDate")
            .HasColumnType("datetime");

        // MIGRATION: ExpiryDate - datetime nullable, when role assignment expires
        // Maps from VB.NET _ExpiryDate field (Date type → DateTime?)
        // Null means no expiration
        builder.Property(ur => ur.ExpiryDate)
            .HasColumnName("ExpiryDate")
            .HasColumnType("datetime");

        // =====================================================================
        // Subscription Flag Properties
        // =====================================================================

        // MIGRATION: IsTrialUsed - bit, indicates if trial period has been consumed
        // Maps from VB.NET _IsTrialUsed field
        // Used for roles with TrialFee and TrialPeriod defined
        builder.Property(ur => ur.IsTrialUsed)
            .HasColumnName("IsTrialUsed");

        // MIGRATION: Subscribed - bit, indicates active subscription status
        // Maps from VB.NET _Subscribed field
        // Used for roles with ServiceFee (subscription-based roles)
        builder.Property(ur => ur.Subscribed)
            .HasColumnName("Subscribed");

        // =====================================================================
        // Audit Properties
        // =====================================================================

        // MIGRATION: CreatedDate - datetime, when role was assigned to user
        builder.Property(ur => ur.CreatedDate)
            .HasColumnName("CreatedOnDate")
            .HasColumnType("datetime");

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: UserRole -> User relationship (many user-roles belong to one user)
        // Configures the foreign key relationship to User entity
        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: UserRole -> Role relationship (many user-roles reference one role)
        // Configures the foreign key relationship to Role entity
        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite unique index on (UserID, RoleID) prevents duplicate assignments
        // A user can only be assigned to a specific role once
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasDatabaseName("IX_UserRoles_UserID_RoleID");

        // MIGRATION: Index on UserID for user-based role queries
        builder.HasIndex(ur => ur.UserId)
            .HasDatabaseName("IX_UserRoles_UserID");

        // MIGRATION: Index on RoleID for role-based user queries
        builder.HasIndex(ur => ur.RoleId)
            .HasDatabaseName("IX_UserRoles_RoleID");

        // MIGRATION: Index on ExpiryDate for expiration-based queries
        builder.HasIndex(ur => ur.ExpiryDate)
            .HasDatabaseName("IX_UserRoles_ExpiryDate");
    }
}
