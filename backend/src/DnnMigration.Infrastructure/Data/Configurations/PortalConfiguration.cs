// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration classes for Portal system
// replacing SqlDataProvider patterns.
//
// Source References:
// - Library/Components/Portal/PortalInfo.vb (Portal entity properties)
// - Library/Components/Portal/PortalAliasInfo.vb (PortalAlias entity properties)
// - Library/Components/Portal/PortalController.vb (data access patterns)
// - Library/Components/Portal/PortalAliasController.vb (alias management)
//
// Key transformations:
// 1) PortalConfiguration maps Portal entity to 'Portals' table with:
//    - Identity/branding: PortalName, LogoFile, FooterText, BackgroundFile, GUID
//    - Admin references: AdministratorId, AdministratorRoleId, RegisteredRoleId
//    - Quotas/billing: HostFee (money), HostSpace, PageQuota, UserQuota, Currency
//    - Content: Description, KeyWords, Email
//    - Navigation tabs: SplashTabId, HomeTabId, LoginTabId, UserTabId, AdminTabId, SuperTabId
//    - Settings: UserRegistration, BannerAdvertising, ExpiryDate, SiteLogHistory
//    - Localization: DefaultLanguage, TimeZoneOffset, HomeDirectory, Version
// 2) PortalAliasConfiguration maps PortalAlias entity to 'PortalAlias' table with:
//    - PortalAliasID primary key
//    - PortalID foreign key with cascade delete
//    - HTTPAlias unique constraint
// 3) Configures navigation properties for relationships
// 4) Creates indexes on PortalName and GUID for lookup optimization
// 5) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Portal"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the Portal entity to the existing 'Portals' table in the DNN database.
/// Portal is the foundational entity for DNN's multi-tenant architecture - every other entity
/// (User, Role, Tab, Module) references a Portal through PortalId.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where portal data was
/// accessed via stored procedures (dbo.GetPortal, dbo.AddPortal, dbo.UpdatePortal, dbo.DeletePortal)
/// through SqlHelper.ExecuteReader/ExecuteNonQuery calls.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar column definitions
/// - HostFee uses decimal(18,2) for SQL Server money type compatibility
/// - Nullable properties correspond to columns that allowed NULL in the legacy schema
/// </para>
/// </remarks>
public class PortalConfiguration : IEntityTypeConfiguration<Portal>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="Portal"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for Portal.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Portals' with PortalID primary key</item>
    /// <item>Identity and branding properties (PortalName, LogoFile, FooterText, BackgroundFile, GUID)</item>
    /// <item>Administrator references (AdministratorId, AdministratorRoleId, RegisteredRoleId)</item>
    /// <item>Quotas and billing (HostFee, HostSpace, PageQuota, UserQuota, Currency, PaymentProcessor)</item>
    /// <item>Content metadata (Description, KeyWords, Email)</item>
    /// <item>Navigation tab references (SplashTabId, HomeTabId, LoginTabId, UserTabId, AdminTabId, SuperTabId)</item>
    /// <item>Settings (UserRegistration, BannerAdvertising, ExpiryDate, SiteLogHistory)</item>
    /// <item>Localization (DefaultLanguage, TimeZoneOffset, HomeDirectory, Version)</item>
    /// <item>Navigation collections (Users, Modules, Tabs, Roles, PortalAliases)</item>
    /// <item>Indexes on PortalName and GUID for lookup optimization</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        // MIGRATION: Map to existing 'Portals' table in DNN database
        builder.ToTable("Portals");

        // MIGRATION: Primary key - maps to PortalID column (identity column in SQL Server)
        builder.HasKey(p => p.PortalId);
        builder.Property(p => p.PortalId)
            .HasColumnName("PortalID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Identity and Branding Properties
        // =====================================================================

        // MIGRATION: PortalName - required, nvarchar(128)
        builder.Property(p => p.PortalName)
            .HasColumnName("PortalName")
            .HasMaxLength(128)
            .IsRequired();

        // MIGRATION: LogoFile - nvarchar(100), nullable
        builder.Property(p => p.LogoFile)
            .HasColumnName("LogoFile")
            .HasMaxLength(100);

        // MIGRATION: FooterText - nvarchar(100), nullable
        builder.Property(p => p.FooterText)
            .HasColumnName("FooterText")
            .HasMaxLength(100);

        // MIGRATION: BackgroundFile - nvarchar(100), nullable
        builder.Property(p => p.BackgroundFile)
            .HasColumnName("BackgroundFile")
            .HasMaxLength(100);

        // MIGRATION: GUID - uniqueidentifier, used for cross-system identification
        builder.Property(p => p.GUID)
            .HasColumnName("GUID")
            .IsRequired();

        // =====================================================================
        // Administrator Reference Properties
        // =====================================================================

        // MIGRATION: AdministratorId - int, foreign key to Users table
        // Note: Not configuring as formal FK to avoid circular dependency issues
        builder.Property(p => p.AdministratorId)
            .HasColumnName("AdministratorId");

        // MIGRATION: AdministratorRoleId - int, reference to default admin role
        builder.Property(p => p.AdministratorRoleId)
            .HasColumnName("AdministratorRoleId");

        // MIGRATION: AdministratorRoleName - nvarchar(50), denormalized role name
        builder.Property(p => p.AdministratorRoleName)
            .HasColumnName("AdministratorRoleName")
            .HasMaxLength(50);

        // MIGRATION: RegisteredRoleId - int, reference to default registered users role
        builder.Property(p => p.RegisteredRoleId)
            .HasColumnName("RegisteredRoleId");

        // MIGRATION: RegisteredRoleName - nvarchar(50), denormalized role name
        builder.Property(p => p.RegisteredRoleName)
            .HasColumnName("RegisteredRoleName")
            .HasMaxLength(50);

        // =====================================================================
        // Quotas and Billing Properties
        // =====================================================================

        // MIGRATION: HostFee - money type, mapped to decimal(18,2) for precision
        // Original VB.NET used Single type; upgraded to decimal for currency accuracy
        builder.Property(p => p.HostFee)
            .HasColumnName("HostFee")
            .HasPrecision(18, 2);

        // MIGRATION: HostSpace - int, disk space allocation in MB
        builder.Property(p => p.HostSpace)
            .HasColumnName("HostSpace");

        // MIGRATION: PageQuota - int, maximum pages allowed (0 = unlimited)
        builder.Property(p => p.PageQuota)
            .HasColumnName("PageQuota");

        // MIGRATION: UserQuota - int, maximum users allowed (0 = unlimited)
        builder.Property(p => p.UserQuota)
            .HasColumnName("UserQuota");

        // MIGRATION: Currency - nvarchar(10), currency code (USD, EUR, etc.)
        builder.Property(p => p.Currency)
            .HasColumnName("Currency")
            .HasMaxLength(10);

        // MIGRATION: PaymentProcessor - nvarchar(256), payment processor name
        builder.Property(p => p.PaymentProcessor)
            .HasColumnName("PaymentProcessor")
            .HasMaxLength(256);

        // MIGRATION: ProcessorUserId - nvarchar(256), payment processor account ID
        builder.Property(p => p.ProcessorUserId)
            .HasColumnName("ProcessorUserId")
            .HasMaxLength(256);

        // MIGRATION: ProcessorPassword - nvarchar(256), payment processor password
        // Note: In production, consider encrypting this value at the application layer
        builder.Property(p => p.ProcessorPassword)
            .HasColumnName("ProcessorPassword")
            .HasMaxLength(256);

        // =====================================================================
        // Content Metadata Properties
        // =====================================================================

        // MIGRATION: Description - nvarchar(500), SEO meta description
        builder.Property(p => p.Description)
            .HasColumnName("Description")
            .HasMaxLength(500);

        // MIGRATION: KeyWords - nvarchar(500), SEO keywords
        builder.Property(p => p.KeyWords)
            .HasColumnName("KeyWords")
            .HasMaxLength(500);

        // MIGRATION: Email - nvarchar(256), portal administrator email
        builder.Property(p => p.Email)
            .HasColumnName("Email")
            .HasMaxLength(256);

        // =====================================================================
        // Navigation Tab Reference Properties
        // =====================================================================

        // MIGRATION: SplashTabId - int, splash/landing page tab ID
        builder.Property(p => p.SplashTabId)
            .HasColumnName("SplashTabId");

        // MIGRATION: HomeTabId - int, home page tab ID
        builder.Property(p => p.HomeTabId)
            .HasColumnName("HomeTabId");

        // MIGRATION: LoginTabId - int, login page tab ID
        builder.Property(p => p.LoginTabId)
            .HasColumnName("LoginTabId");

        // MIGRATION: UserTabId - int, user profile page tab ID
        builder.Property(p => p.UserTabId)
            .HasColumnName("UserTabId");

        // MIGRATION: AdminTabId - int, administration page tab ID
        builder.Property(p => p.AdminTabId)
            .HasColumnName("AdminTabId");

        // MIGRATION: SuperTabId - int, host/super user page tab ID
        builder.Property(p => p.SuperTabId)
            .HasColumnName("SuperTabId");

        // =====================================================================
        // Settings Properties
        // =====================================================================

        // MIGRATION: UserRegistration - int, registration mode (see UserRegistrationType enum)
        builder.Property(p => p.UserRegistration)
            .HasColumnName("UserRegistration");

        // MIGRATION: BannerAdvertising - int, banner mode (see BannerType enum)
        builder.Property(p => p.BannerAdvertising)
            .HasColumnName("BannerAdvertising");

        // MIGRATION: ExpiryDate - datetime, nullable for unlimited subscription
        builder.Property(p => p.ExpiryDate)
            .HasColumnName("ExpiryDate");

        // MIGRATION: SiteLogHistory - int, days to keep site log entries
        builder.Property(p => p.SiteLogHistory)
            .HasColumnName("SiteLogHistory");

        // =====================================================================
        // Localization Properties
        // =====================================================================

        // MIGRATION: DefaultLanguage - nvarchar(10), default culture code (en-US)
        builder.Property(p => p.DefaultLanguage)
            .HasColumnName("DefaultLanguage")
            .HasMaxLength(10);

        // MIGRATION: TimeZoneOffset - int, timezone offset in minutes from UTC
        builder.Property(p => p.TimeZoneOffset)
            .HasColumnName("TimeZoneOffset");

        // MIGRATION: HomeDirectory - nvarchar(100), portal home directory path
        builder.Property(p => p.HomeDirectory)
            .HasColumnName("HomeDirectory")
            .HasMaxLength(100);

        // MIGRATION: Version - nvarchar(20), DNN version for this portal
        builder.Property(p => p.Version)
            .HasColumnName("Version")
            .HasMaxLength(20);

        // =====================================================================
        // Navigation Properties - One-to-Many Relationships
        // =====================================================================

        // MIGRATION: Portal -> Users relationship (one portal has many users)
        // Configures the inverse of User.Portal navigation property
        builder.HasMany(p => p.Users)
            .WithOne(u => u.Portal)
            .HasForeignKey(u => u.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Portal -> Modules relationship (one portal has many modules)
        // Configures the inverse of Module.Portal navigation property
        builder.HasMany(p => p.Modules)
            .WithOne(m => m.Portal)
            .HasForeignKey(m => m.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Portal -> Tabs relationship (one portal has many tabs/pages)
        // Configures the inverse of Tab.Portal navigation property
        builder.HasMany(p => p.Tabs)
            .WithOne(t => t.Portal)
            .HasForeignKey(t => t.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Portal -> Roles relationship (one portal has many roles)
        // Configures the inverse of Role.Portal navigation property
        builder.HasMany(p => p.Roles)
            .WithOne(r => r.Portal)
            .HasForeignKey(r => r.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Portal -> PortalAliases relationship (one portal has many aliases)
        // Configures the inverse of PortalAlias.Portal navigation property
        builder.HasMany(p => p.PortalAliases)
            .WithOne(pa => pa.Portal)
            .HasForeignKey(pa => pa.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Index on PortalName for name-based lookups
        builder.HasIndex(p => p.PortalName)
            .HasDatabaseName("IX_Portals_PortalName");

        // MIGRATION: Unique index on GUID for cross-system identification
        builder.HasIndex(p => p.GUID)
            .IsUnique()
            .HasDatabaseName("IX_Portals_GUID");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="PortalAlias"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the PortalAlias entity to the existing 'PortalAlias' table in the DNN database.
/// Portal aliases enable multiple domain names or URLs to point to the same portal, supporting
/// multi-domain scenarios and vanity URLs.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where portal alias data was
/// accessed via stored procedures (dbo.GetPortalAlias, dbo.AddPortalAlias, dbo.UpdatePortalAlias,
/// dbo.DeletePortalAlias) through SqlHelper calls in PortalAliasController.
/// </para>
/// <para>
/// Key constraints:
/// - HTTPAlias must be unique across all portals (enforced by unique index)
/// - PortalId foreign key with cascade delete ensures aliases are removed when portal is deleted
/// </para>
/// </remarks>
public class PortalAliasConfiguration : IEntityTypeConfiguration<PortalAlias>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="PortalAlias"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for PortalAlias.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'PortalAlias' with PortalAliasID primary key</item>
    /// <item>PortalID foreign key with cascade delete behavior</item>
    /// <item>HTTPAlias required property with unique constraint</item>
    /// <item>IsPrimary flag for primary alias designation</item>
    /// <item>Unique index on HTTPAlias for domain lookup optimization</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<PortalAlias> builder)
    {
        // MIGRATION: Map to existing 'PortalAlias' table in DNN database
        builder.ToTable("PortalAlias");

        // MIGRATION: Primary key - maps to PortalAliasID column (identity column)
        builder.HasKey(pa => pa.PortalAliasId);
        builder.Property(pa => pa.PortalAliasId)
            .HasColumnName("PortalAliasID")
            .ValueGeneratedOnAdd();

        // MIGRATION: PortalId - int, foreign key to Portals table
        builder.Property(pa => pa.PortalId)
            .HasColumnName("PortalID")
            .IsRequired();

        // MIGRATION: HTTPAlias (renamed to HttpAlias in C#) - nvarchar(200), required, unique
        // This is the domain name or URL path that maps to the portal
        // Examples: "www.example.com", "example.com/portal1"
        builder.Property(pa => pa.HttpAlias)
            .HasColumnName("HTTPAlias")
            .HasMaxLength(200)
            .IsRequired();

        // MIGRATION: IsPrimary - bit, indicates if this is the primary/canonical alias
        // The primary alias is used for URL generation and redirects
        builder.Property(pa => pa.IsPrimary)
            .HasColumnName("IsPrimary")
            .HasDefaultValue(false);

        // MIGRATION: Foreign key relationship to Portal with cascade delete
        // When a portal is deleted, all its aliases are automatically removed
        builder.HasOne(pa => pa.Portal)
            .WithMany(p => p.PortalAliases)
            .HasForeignKey(pa => pa.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Unique index on HTTPAlias for domain lookup optimization
        // Ensures no two portals can share the same domain/URL alias
        builder.HasIndex(pa => pa.HttpAlias)
            .IsUnique()
            .HasDatabaseName("IX_PortalAlias_HTTPAlias");

        // MIGRATION: Index on PortalId for efficient portal-based queries
        builder.HasIndex(pa => pa.PortalId)
            .HasDatabaseName("IX_PortalAlias_PortalID");
    }
}
