// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration class for Tab system
// replacing SqlDataProvider patterns.
//
// Source References:
// - Library/Components/Tabs/TabInfo.vb (Tab entity properties)
// - Library/Components/Tabs/TabController.vb (data access patterns)
//
// Key transformations:
// 1) TabConfiguration maps Tab entity to 'Tabs' table with:
//    - TabID primary key (identity)
//    - PortalID foreign key to Portals table
//    - Self-referencing ParentID for hierarchical parent-child structure
//    - Navigation: TabOrder, TabName (required), Level, TabPath
//    - Visibility: IsVisible, IsDeleted, DisableLink
//    - Content: Title, Description, KeyWords, Url, PageHeadText
//    - Skinning: IconFile, SkinSrc, ContainerSrc
//    - Date range: StartDate, EndDate
//    - Security: AuthorizedRoles, AdministratorRoles, IsSecure
//    - Metadata: HasChildren, RefreshInterval
// 2) Configures self-referencing parent-child relationship:
//    - ParentId nullable (root tabs have NULL)
//    - Parent navigation property to parent Tab
//    - Children navigation collection for child tabs
// 3) Configures navigation properties for relationships:
//    - Portal (many-to-one)
//    - Modules (one-to-many collection)
//    - TabPermissions (one-to-many collection)
// 4) Creates indexes on PortalID, ParentID, TabPath for hierarchy traversal
// 5) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Tab"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the Tab entity to the existing 'Tabs' table in the DNN database.
/// Tab represents a page within a portal's navigation hierarchy. Tabs form a hierarchical
/// tree structure through the self-referencing ParentId relationship, where root-level tabs
/// have a null ParentId and child tabs reference their parent.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where tab data was
/// accessed via stored procedures (dbo.GetTab, dbo.GetTabs, dbo.GetTabsByPortal, dbo.AddTab,
/// dbo.UpdateTab, dbo.DeleteTab) through SqlHelper.ExecuteReader/ExecuteNonQuery calls.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// <list type="bullet">
/// <item>String lengths match existing nvarchar column definitions</item>
/// <item>Nullable properties correspond to columns that allowed NULL in the legacy schema</item>
/// <item>The self-referencing ParentId relationship supports unlimited nesting depth</item>
/// </list>
/// </para>
/// <para>
/// Key relationships:
/// <list type="bullet">
/// <item>Portal (many-to-one): Each tab belongs to exactly one portal</item>
/// <item>Parent/Children (self-referencing): Hierarchical tab structure</item>
/// <item>Modules (one-to-many): Modules placed on this tab</item>
/// <item>TabPermissions (one-to-many): Permission assignments for this tab</item>
/// </list>
/// </para>
/// </remarks>
public class TabConfiguration : IEntityTypeConfiguration<Tab>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="Tab"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for Tab.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Tabs' with TabID primary key</item>
    /// <item>PortalID foreign key to Portals table</item>
    /// <item>Self-referencing ParentID for hierarchical navigation</item>
    /// <item>Navigation properties (TabOrder, TabName, Level, TabPath)</item>
    /// <item>Visibility settings (IsVisible, IsDeleted, DisableLink)</item>
    /// <item>Content properties (Title, Description, KeyWords, Url, PageHeadText)</item>
    /// <item>Skinning properties (IconFile, SkinSrc, ContainerSrc)</item>
    /// <item>Date range properties (StartDate, EndDate)</item>
    /// <item>Security properties (AuthorizedRoles, AdministratorRoles, IsSecure)</item>
    /// <item>Metadata properties (HasChildren, RefreshInterval)</item>
    /// <item>Navigation collections (Portal, Parent, Children, Modules, TabPermissions)</item>
    /// <item>Indexes on PortalID, ParentID, TabPath for hierarchy traversal</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<Tab> builder)
    {
        // MIGRATION: Map to existing 'Tabs' table in DNN database
        builder.ToTable("Tabs");

        // MIGRATION: Primary key - maps to TabID column (identity column in SQL Server)
        builder.HasKey(t => t.TabId);
        builder.Property(t => t.TabId)
            .HasColumnName("TabID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: PortalID - int, foreign key to Portals table
        // In legacy DNN, PortalID = Null.NullInteger (-1) indicated a super/host tab
        // For migrated system, we use proper foreign key relationship
        builder.Property(t => t.PortalId)
            .HasColumnName("PortalID");

        // MIGRATION: ParentId - int nullable, self-referencing foreign key
        // Root-level tabs have NULL ParentId
        // Original VB.NET initialized to Null.NullInteger (-1)
        builder.Property(t => t.ParentId)
            .HasColumnName("ParentId");

        // =====================================================================
        // Navigation Properties
        // =====================================================================

        // MIGRATION: TabOrder - int, display order among sibling tabs
        builder.Property(t => t.TabOrder)
            .HasColumnName("TabOrder");

        // MIGRATION: TabName - required, nvarchar(50)
        // The display name for the tab in navigation menus
        builder.Property(t => t.TabName)
            .HasColumnName("TabName")
            .HasMaxLength(50)
            .IsRequired();

        // MIGRATION: Level - int, nesting level in hierarchy (0 = root)
        builder.Property(t => t.Level)
            .HasColumnName("Level");

        // MIGRATION: TabPath - nvarchar(255), hierarchical path (e.g., "//Home//About")
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.TabPath)
            .HasColumnName("TabPath")
            .HasMaxLength(255);

        // =====================================================================
        // Visibility Properties
        // =====================================================================

        // MIGRATION: IsVisible - bit, whether tab appears in navigation
        builder.Property(t => t.IsVisible)
            .HasColumnName("IsVisible");

        // MIGRATION: IsDeleted - bit, soft-delete flag
        builder.Property(t => t.IsDeleted)
            .HasColumnName("IsDeleted");

        // MIGRATION: DisableLink - bit, whether tab link is disabled (container only)
        builder.Property(t => t.DisableLink)
            .HasColumnName("DisableLink");

        // =====================================================================
        // Content Properties
        // =====================================================================

        // MIGRATION: Title - nvarchar(200), HTML page title
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.Title)
            .HasColumnName("Title")
            .HasMaxLength(200);

        // MIGRATION: Description - nvarchar(500), SEO meta description
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.Description)
            .HasColumnName("Description")
            .HasMaxLength(500);

        // MIGRATION: KeyWords - nvarchar(500), SEO meta keywords
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.KeyWords)
            .HasColumnName("KeyWords")
            .HasMaxLength(500);

        // MIGRATION: Url - nvarchar(255), external link or redirect URL
        // Can contain file IDs, tab IDs, or external URLs
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.Url)
            .HasColumnName("Url")
            .HasMaxLength(255);

        // MIGRATION: PageHeadText - ntext, custom HTML for page head section
        // Use HasColumnType for ntext compatibility
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.PageHeadText)
            .HasColumnName("PageHeadText")
            .HasColumnType("ntext");

        // =====================================================================
        // Skinning Properties
        // =====================================================================

        // MIGRATION: IconFile - nvarchar(100), relative path to tab icon
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.IconFile)
            .HasColumnName("IconFile")
            .HasMaxLength(100);

        // MIGRATION: SkinSrc - nvarchar(200), path to skin template
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.SkinSrc)
            .HasColumnName("SkinSrc")
            .HasMaxLength(200);

        // MIGRATION: ContainerSrc - nvarchar(200), path to container template
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.ContainerSrc)
            .HasColumnName("ContainerSrc")
            .HasMaxLength(200);

        // =====================================================================
        // Date Range Properties
        // =====================================================================

        // MIGRATION: StartDate - datetime nullable, when tab becomes visible
        // Original VB.NET initialized to Null.NullDate
        builder.Property(t => t.StartDate)
            .HasColumnName("StartDate");

        // MIGRATION: EndDate - datetime nullable, when tab becomes hidden
        // Original VB.NET initialized to Null.NullDate
        builder.Property(t => t.EndDate)
            .HasColumnName("EndDate");

        // =====================================================================
        // Security Properties
        // =====================================================================

        // MIGRATION: AuthorizedRoles - nvarchar(max), semicolon-separated role IDs
        // Legacy property for basic authorization
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.AuthorizedRoles)
            .HasColumnName("AuthorizedRoles");

        // MIGRATION: AdministratorRoles - nvarchar(max), semicolon-separated role IDs
        // Legacy property for administrative access
        // Original VB.NET initialized to Null.NullString
        builder.Property(t => t.AdministratorRoles)
            .HasColumnName("AdministratorRoles");

        // MIGRATION: IsSecure - bit, whether HTTPS is required
        builder.Property(t => t.IsSecure)
            .HasColumnName("IsSecure");

        // =====================================================================
        // Metadata Properties
        // =====================================================================

        // MIGRATION: HasChildren - bit, denormalized flag for performance
        builder.Property(t => t.HasChildren)
            .HasColumnName("HasChildren");

        // MIGRATION: RefreshInterval - int nullable, auto-refresh in seconds
        // Original VB.NET initialized to Null.NullInteger
        builder.Property(t => t.RefreshInterval)
            .HasColumnName("RefreshInterval");

        // =====================================================================
        // Relationships - Portal (Many-to-One)
        // =====================================================================

        // MIGRATION: Tab -> Portal relationship (many tabs belong to one portal)
        // Each tab exists within a specific portal's context
        builder.HasOne(t => t.Portal)
            .WithMany(p => p.Tabs)
            .HasForeignKey(t => t.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Self-Referencing Relationship - Parent/Children (Hierarchical)
        // =====================================================================

        // MIGRATION: Self-referencing parent-child relationship for tab hierarchy
        // ParentId is nullable - root tabs have NULL, child tabs reference parent
        // This enables unlimited nesting depth for navigation structures
        builder.HasOne(t => t.Parent)
            .WithMany(t => t.Children)
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // =====================================================================
        // Relationships - Modules (One-to-Many)
        // =====================================================================

        // MIGRATION: Tab -> Modules relationship (one tab has many modules)
        // Modules are placed on tabs within panes
        builder.HasMany(t => t.Modules)
            .WithOne(m => m.Tab)
            .HasForeignKey(m => m.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Relationships - TabPermissions (One-to-Many)
        // =====================================================================

        // MIGRATION: Tab -> TabPermissions relationship (one tab has many permissions)
        // Enables fine-grained access control at the page level
        builder.HasMany(t => t.TabPermissions)
            .WithOne(tp => tp.Tab)
            .HasForeignKey(tp => tp.TabId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Index on PortalID for portal-scoped tab queries
        // Essential for multi-tenant isolation - most queries filter by portal
        builder.HasIndex(t => t.PortalId)
            .HasDatabaseName("IX_Tabs_PortalID");

        // MIGRATION: Index on ParentId for hierarchy traversal
        // Used when building navigation trees and finding child tabs
        builder.HasIndex(t => t.ParentId)
            .HasDatabaseName("IX_Tabs_ParentID");

        // MIGRATION: Index on TabPath for path-based lookups
        // Used for URL routing and friendly URL resolution
        builder.HasIndex(t => t.TabPath)
            .HasDatabaseName("IX_Tabs_TabPath");

        // MIGRATION: Composite index on PortalID and ParentId for hierarchy queries
        // Optimizes the common query pattern: get all tabs in portal at a specific level
        builder.HasIndex(t => new { t.PortalId, t.ParentId })
            .HasDatabaseName("IX_Tabs_PortalID_ParentID");

        // MIGRATION: Composite index on PortalID and TabName for name lookups
        // Used when searching for tabs by name within a portal
        builder.HasIndex(t => new { t.PortalId, t.TabName })
            .HasDatabaseName("IX_Tabs_PortalID_TabName");
    }
}
