// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration classes for Role system
// replacing RoleProvider/SqlDataProvider patterns.
//
// Source References:
// - Library/Components/Security/Roles/RoleInfo.vb (Role entity properties)
// - Library/Components/Security/Roles/RoleGroupInfo.vb (RoleGroup entity properties)
// - Library/Components/Security/Roles/RoleController.vb (data access patterns)
//
// Key transformations:
// 1) RoleConfiguration maps Role entity to 'Roles' table with:
//    - Primary key: RoleID
//    - Foreign keys: PortalID (required), RoleGroupID (optional)
//    - Identity: RoleName (required), Description
//    - Billing: ServiceFee, BillingFrequency, BillingPeriod, TrialFee, TrialFrequency, TrialPeriod
//    - Behavior: IsPublic, AutoAssignment
//    - Other: RSVPCode, IconFile
// 2) RoleGroupConfiguration maps RoleGroup entity to 'RoleGroups' table with:
//    - RoleGroupID primary key
//    - PortalID foreign key
//    - RoleGroupName (required), Description
// 3) Configures navigation properties for relationships
// 4) Creates composite unique index on (PortalID, RoleName) for role lookup
// 5) Creates index on RoleGroupID for group filtering
// 6) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Role"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the Role entity to the existing 'Roles' table in the DNN database.
/// Roles are used for permission grouping and can be assigned to users for access control.
/// Each role belongs to a specific portal and optionally to a <see cref="RoleGroup"/>.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the RoleProvider/SqlDataProvider pattern where role data
/// was accessed via stored procedures (dbo.GetRole, dbo.AddRole, dbo.UpdateRole, dbo.DeleteRole,
/// dbo.GetRoles, dbo.GetRolesByGroup) through SqlHelper.ExecuteReader/ExecuteNonQuery calls
/// in RoleController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar column definitions
/// - ServiceFee and TrialFee use decimal(18,2) for SQL Server money type compatibility
/// - BillingFrequency and TrialFrequency use nchar(1) for single-character frequency codes
/// - Nullable properties correspond to columns that allowed NULL in the legacy schema
/// </para>
/// </remarks>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="Role"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for Role.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Roles' with RoleID primary key</item>
    /// <item>Foreign key to Portal via PortalID (required)</item>
    /// <item>Optional foreign key to RoleGroup via RoleGroupID</item>
    /// <item>Identity properties (RoleName required, Description optional)</item>
    /// <item>Billing properties (ServiceFee, BillingFrequency, BillingPeriod, TrialFee, TrialFrequency, TrialPeriod)</item>
    /// <item>Behavior flags (IsPublic, AutoAssignment)</item>
    /// <item>Additional properties (RSVPCode, IconFile)</item>
    /// <item>Navigation properties (Portal, RoleGroup, UserRoles collection)</item>
    /// <item>Composite unique index on (PortalID, RoleName) for role lookup</item>
    /// <item>Index on RoleGroupID for group filtering</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // MIGRATION: Map to existing 'Roles' table in DNN database
        builder.ToTable("Roles");

        // MIGRATION: Primary key - maps to RoleID column (identity column in SQL Server)
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.RoleId)
            .HasColumnName("RoleID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: PortalID - int, required foreign key to Portals table
        builder.Property(r => r.PortalId)
            .HasColumnName("PortalID")
            .IsRequired();

        // MIGRATION: RoleGroupID - int, optional foreign key to RoleGroups table
        // A value of null indicates the role is not assigned to a specific group
        // Legacy DNN used -1 for no group, but we use nullable int for cleaner semantics
        builder.Property(r => r.RoleGroupId)
            .HasColumnName("RoleGroupID");

        // =====================================================================
        // Identity Properties
        // =====================================================================

        // MIGRATION: RoleName - nvarchar(50), required
        // Maps from VB.NET _RoleName field with <XmlElement("rolename")> attribute
        builder.Property(r => r.RoleName)
            .HasColumnName("RoleName")
            .HasMaxLength(50)
            .IsRequired();

        // MIGRATION: Description - nvarchar(1000), nullable
        // Maps from VB.NET _Description field with <XmlElement("description")> attribute
        builder.Property(r => r.Description)
            .HasColumnName("Description")
            .HasMaxLength(1000);

        // =====================================================================
        // Billing Properties
        // =====================================================================

        // MIGRATION: ServiceFee - money type, mapped to decimal(18,2) for precision
        // Original VB.NET used Single type; upgraded to decimal for currency accuracy
        // Maps from VB.NET _ServiceFee field with <XmlElement("servicefee")> attribute
        builder.Property(r => r.ServiceFee)
            .HasColumnName("ServiceFee")
            .HasPrecision(18, 2);

        // MIGRATION: BillingFrequency - nchar(1), single character frequency code
        // Valid values: N (None), O (One time), D (Daily), W (Weekly), M (Monthly), Y (Yearly)
        // Maps from VB.NET _BillingFrequency field with <XmlElement("billingfrequency")> attribute
        builder.Property(r => r.BillingFrequency)
            .HasColumnName("BillingFrequency")
            .HasColumnType("nchar(1)");

        // MIGRATION: BillingPeriod - int, number of billing frequency units
        // Maps from VB.NET _BillingPeriod field with <XmlElement("billingperiod")> attribute
        builder.Property(r => r.BillingPeriod)
            .HasColumnName("BillingPeriod");

        // MIGRATION: TrialFee - money type, mapped to decimal(18,2) for precision
        // Original VB.NET used Single type; upgraded to decimal for currency accuracy
        // Maps from VB.NET _TrialFee field with <XmlElement("trialfee")> attribute
        builder.Property(r => r.TrialFee)
            .HasColumnName("TrialFee")
            .HasPrecision(18, 2);

        // MIGRATION: TrialFrequency - nchar(1), single character frequency code
        // Valid values: N (None), O (One time), D (Daily), W (Weekly), M (Monthly), Y (Yearly)
        // Maps from VB.NET _TrialFrequency field with <XmlElement("trialfrequency")> attribute
        builder.Property(r => r.TrialFrequency)
            .HasColumnName("TrialFrequency")
            .HasColumnType("nchar(1)");

        // MIGRATION: TrialPeriod - int, number of trial frequency units
        // Maps from VB.NET _TrialPeriod field with <XmlElement("trialperiod")> attribute
        builder.Property(r => r.TrialPeriod)
            .HasColumnName("TrialPeriod");

        // =====================================================================
        // Behavior Flag Properties
        // =====================================================================

        // MIGRATION: IsPublic - bit, indicates if users can self-subscribe to the role
        // Maps from VB.NET _IsPublic field with <XmlElement("ispublic")> attribute
        builder.Property(r => r.IsPublic)
            .HasColumnName("IsPublic");

        // MIGRATION: AutoAssignment - bit, indicates if new users are automatically assigned
        // Maps from VB.NET _AutoAssignment field with <XmlElement("autoassignment")> attribute
        builder.Property(r => r.AutoAssignment)
            .HasColumnName("AutoAssignment");

        // =====================================================================
        // Additional Properties
        // =====================================================================

        // MIGRATION: RSVPCode - nvarchar(50), code users can use to self-subscribe
        // Maps from VB.NET _RSVPCode field with <XmlElement("rsvpcode")> attribute
        builder.Property(r => r.RSVPCode)
            .HasColumnName("RSVPCode")
            .HasMaxLength(50);

        // MIGRATION: IconFile - nvarchar(100), relative path to role icon
        // Maps from VB.NET _IconFile field with <XmlElement("iconfile")> attribute
        builder.Property(r => r.IconFile)
            .HasColumnName("IconFile")
            .HasMaxLength(100);

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: Role -> Portal relationship (many roles belong to one portal)
        // Configures the foreign key relationship to Portal entity
        builder.HasOne(r => r.Portal)
            .WithMany(p => p.Roles)
            .HasForeignKey(r => r.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Role -> RoleGroup relationship (many roles can belong to one role group)
        // This is an optional relationship - roles can exist without a group
        builder.HasOne(r => r.RoleGroup)
            .WithMany(rg => rg.Roles)
            .HasForeignKey(r => r.RoleGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // MIGRATION: Role -> UserRoles relationship (one role has many user-role assignments)
        // Configures the inverse of UserRole.Role navigation property
        builder.HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite unique index on (PortalID, RoleName) for role lookup
        // Ensures role names are unique within a portal (matches legacy constraint)
        builder.HasIndex(r => new { r.PortalId, r.RoleName })
            .IsUnique()
            .HasDatabaseName("IX_Roles_PortalID_RoleName");

        // MIGRATION: Index on RoleGroupID for filtering roles by group
        builder.HasIndex(r => r.RoleGroupId)
            .HasDatabaseName("IX_Roles_RoleGroupID");

        // MIGRATION: Index on PortalID for portal-based role queries
        builder.HasIndex(r => r.PortalId)
            .HasDatabaseName("IX_Roles_PortalID");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="RoleGroup"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the RoleGroup entity to the existing 'RoleGroups' table in the DNN database.
/// Role groups provide a way to organize and categorize roles within a portal for administrative
/// purposes and cleaner user interface grouping.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the RoleProvider/SqlDataProvider pattern where role group
/// data was accessed via stored procedures (dbo.GetRoleGroup, dbo.AddRoleGroup, dbo.UpdateRoleGroup,
/// dbo.DeleteRoleGroup, dbo.GetRoleGroups) through SqlHelper calls in RoleController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar column definitions
/// - RoleGroupName is required as it identifies the group
/// - Each role group belongs to a specific portal
/// </para>
/// </remarks>
public class RoleGroupConfiguration : IEntityTypeConfiguration<RoleGroup>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="RoleGroup"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for RoleGroup.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'RoleGroups' with RoleGroupID primary key</item>
    /// <item>Foreign key to Portal via PortalID (required)</item>
    /// <item>Identity properties (RoleGroupName required, Description optional)</item>
    /// <item>Navigation properties (Portal, Roles collection)</item>
    /// <item>Composite unique index on (PortalID, RoleGroupName) for group lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<RoleGroup> builder)
    {
        // MIGRATION: Map to existing 'RoleGroups' table in DNN database
        builder.ToTable("RoleGroups");

        // MIGRATION: Primary key - maps to RoleGroupID column (identity column in SQL Server)
        builder.HasKey(rg => rg.RoleGroupId);
        builder.Property(rg => rg.RoleGroupId)
            .HasColumnName("RoleGroupID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: PortalID - int, required foreign key to Portals table
        // Maps from VB.NET _PortalID field
        builder.Property(rg => rg.PortalId)
            .HasColumnName("PortalID")
            .IsRequired();

        // =====================================================================
        // Identity Properties
        // =====================================================================

        // MIGRATION: RoleGroupName - nvarchar(50), required
        // Maps from VB.NET _RoleGroupName field
        builder.Property(rg => rg.RoleGroupName)
            .HasColumnName("RoleGroupName")
            .HasMaxLength(50)
            .IsRequired();

        // MIGRATION: Description - nvarchar(1000), nullable
        // Maps from VB.NET _Description field
        builder.Property(rg => rg.Description)
            .HasColumnName("Description")
            .HasMaxLength(1000);

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: RoleGroup -> Portal relationship (many role groups belong to one portal)
        // Configures the foreign key relationship to Portal entity
        builder.HasOne(rg => rg.Portal)
            .WithMany()
            .HasForeignKey(rg => rg.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: RoleGroup -> Roles relationship (one role group has many roles)
        // Configures the inverse of Role.RoleGroup navigation property
        // Note: This is configured in RoleConfiguration, but we ensure the navigation property is set
        builder.Navigation(rg => rg.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Composite unique index on (PortalID, RoleGroupName) for group lookup
        // Ensures role group names are unique within a portal
        builder.HasIndex(rg => new { rg.PortalId, rg.RoleGroupName })
            .IsUnique()
            .HasDatabaseName("IX_RoleGroups_PortalID_RoleGroupName");

        // MIGRATION: Index on PortalID for portal-based role group queries
        builder.HasIndex(rg => rg.PortalId)
            .HasDatabaseName("IX_RoleGroups_PortalID");
    }
}
