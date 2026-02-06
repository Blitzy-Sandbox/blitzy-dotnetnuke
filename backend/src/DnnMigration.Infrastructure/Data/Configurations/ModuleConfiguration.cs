// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Create EF Core IEntityTypeConfiguration classes for Module system
// replacing SqlDataProvider patterns.
//
// Source References:
// - Library/Components/Modules/ModuleInfo.vb (Module entity properties)
// - Library/Components/Modules/ModuleDefinitionInfo.vb (ModuleDefinition properties)
// - Library/Components/Modules/DesktopModuleInfo.vb (DesktopModule properties)
// - Library/Components/Modules/ModuleController.vb (data access patterns)
// - Library/Components/Modules/ModuleDefinitionController.vb (definition patterns)
// - Library/Components/Modules/DesktopModuleController.vb (desktop module patterns)
//
// Key transformations:
// 1) ModuleConfiguration maps Module entity to 'Modules' table with:
//    - Primary key: ModuleID
//    - Foreign keys: PortalID, TabID, ModuleDefID
//    - Display properties: ModuleTitle, PaneName, ModuleOrder
//    - Styling: Alignment, Color, Border, IconFile, ContainerSrc
//    - Content: Header, Footer
//    - Behavior flags: AllTabs, IsDeleted, DisplayTitle, DisplayPrint, DisplaySyndicate
//    - Permissions: InheritViewPermissions, IsDefaultModule, AllModules
//    - Scheduling: StartDate, EndDate, CacheTime
//    - State: Visibility, TabModuleId
// 2) ModuleDefinitionConfiguration maps ModuleDefinition to 'ModuleDefinitions' table with:
//    - Primary key: ModuleDefID
//    - Foreign key: DesktopModuleID
//    - Properties: FriendlyName, TempModuleID, DefaultCacheTime
// 3) DesktopModuleConfiguration maps DesktopModule to 'DesktopModules' table with:
//    - Primary key: DesktopModuleID
//    - Identification: ModuleName, FolderName, FriendlyName
//    - Metadata: Description, Version, BusinessControllerClass
//    - Flags: IsPremium, IsAdmin, SupportedFeatures
//    - Extension: CompatibleVersions, Dependencies, Permissions
// 4) Configures navigation properties for all relationships
// 5) Creates indexes on PortalID, TabID, ModuleDefID for performance
// 6) Uses file-scoped namespace and C# 12 features
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Module"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the Module entity to the existing 'Modules' table in the DNN database.
/// A Module represents an instance of a <see cref="ModuleDefinition"/> placed on a specific
/// <see cref="Tab"/> within a <see cref="Portal"/>. Modules are the primary content containers
/// in the DNN architecture.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where module data was
/// accessed via stored procedures (dbo.GetModule, dbo.AddModule, dbo.UpdateModule, dbo.DeleteModule)
/// through SqlHelper.ExecuteReader/ExecuteNonQuery calls in ModuleController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema column sizes and types:
/// - String lengths match existing nvarchar column definitions
/// - Boolean flags map to SQL Server bit columns
/// - DateTime fields are nullable for optional scheduling
/// - Visibility is stored as integer (enum value)
/// </para>
/// </remarks>
public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="Module"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for Module.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'Modules' with ModuleID primary key</item>
    /// <item>Foreign keys to Portal (PortalID), Tab (TabID), ModuleDefinition (ModuleDefID)</item>
    /// <item>Display properties (ModuleTitle, PaneName, ModuleOrder)</item>
    /// <item>Styling properties (Alignment, Color, Border, IconFile, ContainerSrc)</item>
    /// <item>Content blocks (Header, Footer)</item>
    /// <item>Behavior flags (AllTabs, IsDeleted, DisplayTitle, DisplayPrint, DisplaySyndicate)</item>
    /// <item>Permission flags (InheritViewPermissions, IsDefaultModule, AllModules)</item>
    /// <item>Scheduling (StartDate, EndDate, CacheTime, Visibility)</item>
    /// <item>Navigation collections (ModulePermissions)</item>
    /// <item>Indexes on PortalID, TabID, ModuleDefID for query optimization</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        // MIGRATION: Map to existing 'Modules' table in DNN database
        builder.ToTable("Modules");

        // MIGRATION: Primary key - maps to ModuleID column (identity column in SQL Server)
        builder.HasKey(m => m.ModuleId);
        builder.Property(m => m.ModuleId)
            .HasColumnName("ModuleID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: PortalID - int, foreign key to Portals table
        builder.Property(m => m.PortalId)
            .HasColumnName("PortalID");

        // MIGRATION: TabID - int, foreign key to Tabs table
        builder.Property(m => m.TabId)
            .HasColumnName("TabID");

        // MIGRATION: TabModuleID - int, unique identifier for tab-module placement
        // A module can be placed on multiple tabs, each with a different TabModuleID
        builder.Property(m => m.TabModuleId)
            .HasColumnName("TabModuleID");

        // MIGRATION: ModuleDefID - int, foreign key to ModuleDefinitions table
        builder.Property(m => m.ModuleDefId)
            .HasColumnName("ModuleDefID");

        // =====================================================================
        // Display Properties
        // =====================================================================

        // MIGRATION: ModuleTitle - nvarchar(256), display title for the module
        builder.Property(m => m.ModuleTitle)
            .HasColumnName("ModuleTitle")
            .HasMaxLength(256);

        // MIGRATION: PaneName - nvarchar(50), name of the pane (ContentPane, LeftPane, etc.)
        builder.Property(m => m.PaneName)
            .HasColumnName("PaneName")
            .HasMaxLength(50);

        // MIGRATION: ModuleOrder - int, display order within the pane
        builder.Property(m => m.ModuleOrder)
            .HasColumnName("ModuleOrder");

        // MIGRATION: CacheTime - int, output cache duration in seconds
        builder.Property(m => m.CacheTime)
            .HasColumnName("CacheTime");

        // =====================================================================
        // Styling Properties
        // =====================================================================

        // MIGRATION: Alignment - nvarchar(10), content alignment (left, center, right)
        builder.Property(m => m.Alignment)
            .HasColumnName("Alignment")
            .HasMaxLength(10);

        // MIGRATION: Color - nvarchar(10), background color value
        builder.Property(m => m.Color)
            .HasColumnName("Color")
            .HasMaxLength(10);

        // MIGRATION: Border - nvarchar(10), border style value
        builder.Property(m => m.Border)
            .HasColumnName("Border")
            .HasMaxLength(10);

        // MIGRATION: IconFile - nvarchar(100), relative path to module icon
        builder.Property(m => m.IconFile)
            .HasColumnName("IconFile")
            .HasMaxLength(100);

        // MIGRATION: ContainerSrc - nvarchar(200), path to container template
        builder.Property(m => m.ContainerSrc)
            .HasColumnName("ContainerSrc")
            .HasMaxLength(200);

        // =====================================================================
        // Content Block Properties
        // =====================================================================

        // MIGRATION: Header - ntext, custom HTML above module content
        builder.Property(m => m.Header)
            .HasColumnName("Header")
            .HasColumnType("ntext");

        // MIGRATION: Footer - ntext, custom HTML below module content
        builder.Property(m => m.Footer)
            .HasColumnName("Footer")
            .HasColumnType("ntext");

        // =====================================================================
        // Scheduling Properties
        // =====================================================================

        // MIGRATION: StartDate - datetime, nullable, when module becomes visible
        builder.Property(m => m.StartDate)
            .HasColumnName("StartDate");

        // MIGRATION: EndDate - datetime, nullable, when module becomes hidden
        builder.Property(m => m.EndDate)
            .HasColumnName("EndDate");

        // MIGRATION: Visibility - int, maps to VisibilityState enum (Maximized=0, Minimized=1, None=2)
        builder.Property(m => m.Visibility)
            .HasColumnName("Visibility");

        // =====================================================================
        // Behavior Flag Properties
        // =====================================================================

        // MIGRATION: AllTabs - bit, true if module appears on all tabs
        builder.Property(m => m.AllTabs)
            .HasColumnName("AllTabs");

        // MIGRATION: IsDeleted - bit, soft delete flag
        builder.Property(m => m.IsDeleted)
            .HasColumnName("IsDeleted");

        // MIGRATION: DisplayTitle - bit, true if module title should be displayed
        builder.Property(m => m.DisplayTitle)
            .HasColumnName("DisplayTitle");

        // MIGRATION: DisplayPrint - bit, true if print button should be displayed
        builder.Property(m => m.DisplayPrint)
            .HasColumnName("DisplayPrint");

        // MIGRATION: DisplaySyndicate - bit, true if RSS syndication should be enabled
        builder.Property(m => m.DisplaySyndicate)
            .HasColumnName("DisplaySyndicate");

        // =====================================================================
        // Permission Flag Properties
        // =====================================================================

        // MIGRATION: InheritViewPermissions - bit, true if permissions inherited from tab
        builder.Property(m => m.InheritViewPermissions)
            .HasColumnName("InheritViewPermissions");

        // MIGRATION: IsDefaultModule - bit, true if this is a default module
        builder.Property(m => m.IsDefaultModule)
            .HasColumnName("IsDefaultModule");

        // MIGRATION: AllModules - bit, module applies to all modules flag
        builder.Property(m => m.AllModules)
            .HasColumnName("AllModules");

        // =====================================================================
        // Legacy Authorization Properties
        // =====================================================================

        // MIGRATION: AuthorizedViewRoles - legacy semicolon-separated role IDs for view access
        // Consider using ModulePermissions navigation for modern permission checks
        builder.Property(m => m.AuthorizedViewRoles)
            .HasColumnName("AuthorizedViewRoles")
            .HasColumnType("nvarchar(max)");

        // MIGRATION: AuthorizedEditRoles - legacy semicolon-separated role IDs for edit access
        // Consider using ModulePermissions navigation for modern permission checks
        builder.Property(m => m.AuthorizedEditRoles)
            .HasColumnName("AuthorizedEditRoles")
            .HasColumnType("nvarchar(max)");

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: Module -> Portal relationship (many modules belong to one portal)
        builder.HasOne(m => m.Portal)
            .WithMany(p => p.Modules)
            .HasForeignKey(m => m.PortalId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: Module -> Tab relationship (many modules placed on one tab)
        builder.HasOne(m => m.Tab)
            .WithMany(t => t.Modules)
            .HasForeignKey(m => m.TabId)
            .OnDelete(DeleteBehavior.Restrict);

        // MIGRATION: Module -> ModuleDefinition relationship (many modules use one definition)
        builder.HasOne(m => m.ModuleDefinition)
            .WithMany(md => md.Modules)
            .HasForeignKey(m => m.ModuleDefId)
            .OnDelete(DeleteBehavior.Restrict);

        // MIGRATION: Module -> ModulePermissions relationship (one module has many permissions)
        builder.HasMany(m => m.ModulePermissions)
            .WithOne(mp => mp.Module)
            .HasForeignKey(mp => mp.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Index on PortalID for portal-scoped module queries
        builder.HasIndex(m => m.PortalId)
            .HasDatabaseName("IX_Modules_PortalID");

        // MIGRATION: Index on TabID for tab-specific module queries
        builder.HasIndex(m => m.TabId)
            .HasDatabaseName("IX_Modules_TabID");

        // MIGRATION: Index on ModuleDefID for definition-based queries
        builder.HasIndex(m => m.ModuleDefId)
            .HasDatabaseName("IX_Modules_ModuleDefID");

        // MIGRATION: Composite index for common query pattern (portal + tab)
        builder.HasIndex(m => new { m.PortalId, m.TabId })
            .HasDatabaseName("IX_Modules_PortalID_TabID");

        // MIGRATION: Index on IsDeleted for filtering active modules
        builder.HasIndex(m => m.IsDeleted)
            .HasDatabaseName("IX_Modules_IsDeleted");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="ModuleDefinition"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the ModuleDefinition entity to the existing 'ModuleDefinitions' table
/// in the DNN database. A ModuleDefinition describes a specific function within a 
/// <see cref="DesktopModule"/> package - for example, a Blog module package might contain
/// "Blog Entry" and "Blog Archive" module definitions.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where module definition
/// data was accessed via stored procedures (dbo.GetModuleDefinition, dbo.AddModuleDefinition,
/// dbo.UpdateModuleDefinition, dbo.DeleteModuleDefinition) through SqlHelper calls in
/// ModuleDefinitionController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema:
/// - FriendlyName has nvarchar(128) maximum length
/// - TempModuleID used during installation process
/// - DefaultCacheTime provides initial cache duration for new module instances
/// </para>
/// </remarks>
public class ModuleDefinitionConfiguration : IEntityTypeConfiguration<ModuleDefinition>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="ModuleDefinition"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for ModuleDefinition.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'ModuleDefinitions' with ModuleDefID primary key</item>
    /// <item>Foreign key to DesktopModule (DesktopModuleID)</item>
    /// <item>Display property (FriendlyName)</item>
    /// <item>Installation property (TempModuleID)</item>
    /// <item>Cache setting (DefaultCacheTime)</item>
    /// <item>Navigation collections (Modules)</item>
    /// <item>Index on DesktopModuleID for lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
    {
        // MIGRATION: Map to existing 'ModuleDefinitions' table in DNN database
        builder.ToTable("ModuleDefinitions");

        // MIGRATION: Primary key - maps to ModuleDefID column (identity column in SQL Server)
        builder.HasKey(md => md.ModuleDefId);
        builder.Property(md => md.ModuleDefId)
            .HasColumnName("ModuleDefID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Foreign Key Properties
        // =====================================================================

        // MIGRATION: DesktopModuleID - int, foreign key to DesktopModules table
        builder.Property(md => md.DesktopModuleId)
            .HasColumnName("DesktopModuleID");

        // =====================================================================
        // Display Properties
        // =====================================================================

        // MIGRATION: FriendlyName - nvarchar(128), user-friendly display name
        builder.Property(md => md.FriendlyName)
            .HasColumnName("FriendlyName")
            .HasMaxLength(128);

        // =====================================================================
        // Installation Properties
        // =====================================================================

        // MIGRATION: TempModuleID - int, temporary ID used during module installation
        // Used by module installation infrastructure to track mappings before final IDs assigned
        builder.Property(md => md.TempModuleId)
            .HasColumnName("TempModuleID");

        // =====================================================================
        // Cache Settings
        // =====================================================================

        // MIGRATION: DefaultCacheTime - int, default cache duration in seconds for new modules
        // Individual module instances can override this value
        builder.Property(md => md.DefaultCacheTime)
            .HasColumnName("DefaultCacheTime");

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: ModuleDefinition -> DesktopModule relationship (many definitions belong to one desktop module)
        builder.HasOne(md => md.DesktopModule)
            .WithMany(dm => dm.ModuleDefinitions)
            .HasForeignKey(md => md.DesktopModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION: ModuleDefinition -> Modules relationship (one definition has many module instances)
        // This is configured from the Module side (see ModuleConfiguration)

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Index on DesktopModuleID for desktop module lookup
        builder.HasIndex(md => md.DesktopModuleId)
            .HasDatabaseName("IX_ModuleDefinitions_DesktopModuleID");

        // MIGRATION: Index on FriendlyName for name-based searches
        builder.HasIndex(md => md.FriendlyName)
            .HasDatabaseName("IX_ModuleDefinitions_FriendlyName");
    }
}

/// <summary>
/// EF Core Fluent API configuration for the <see cref="DesktopModule"/> entity.
/// </summary>
/// <remarks>
/// <para>
/// This configuration maps the DesktopModule entity to the existing 'DesktopModules' table
/// in the DNN database. A DesktopModule represents an installable module package that can
/// contain one or more <see cref="ModuleDefinition"/> entries defining specific functionality.
/// </para>
/// <para>
/// MIGRATION: This configuration replaces the SqlDataProvider pattern where desktop module
/// data was accessed via stored procedures (dbo.GetDesktopModule, dbo.AddDesktopModule,
/// dbo.UpdateDesktopModule, dbo.DeleteDesktopModule) through SqlHelper calls in
/// DesktopModuleController.
/// </para>
/// <para>
/// Property configurations preserve the legacy database schema:
/// - ModuleName, FolderName, FriendlyName have nvarchar(128) maximum length
/// - Description uses ntext for long descriptions
/// - Version has nvarchar(8) for version strings (e.g., "01.00.00")
/// - BusinessControllerClass has nvarchar(200) for full type names
/// - SupportedFeatures uses integer bitwise flags (1=Portable, 2=Searchable, 4=Upgradeable)
/// </para>
/// </remarks>
public class DesktopModuleConfiguration : IEntityTypeConfiguration<DesktopModule>
{
    /// <summary>
    /// Configures the entity mapping for <see cref="DesktopModule"/>.
    /// </summary>
    /// <param name="builder">The entity type builder for DesktopModule.</param>
    /// <remarks>
    /// Configuration includes:
    /// <list type="bullet">
    /// <item>Table mapping to 'DesktopModules' with DesktopModuleID primary key</item>
    /// <item>Identification (ModuleName, FolderName, FriendlyName)</item>
    /// <item>Metadata (Description, Version, BusinessControllerClass)</item>
    /// <item>Flags (IsPremium, IsAdmin, SupportedFeatures)</item>
    /// <item>Extension info (CompatibleVersions, Dependencies, Permissions)</item>
    /// <item>Navigation collections (ModuleDefinitions)</item>
    /// <item>Indexes on ModuleName and FriendlyName for lookup</item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<DesktopModule> builder)
    {
        // MIGRATION: Map to existing 'DesktopModules' table in DNN database
        builder.ToTable("DesktopModules");

        // MIGRATION: Primary key - maps to DesktopModuleID column (identity column in SQL Server)
        builder.HasKey(dm => dm.DesktopModuleId);
        builder.Property(dm => dm.DesktopModuleId)
            .HasColumnName("DesktopModuleID")
            .ValueGeneratedOnAdd();

        // =====================================================================
        // Identification Properties
        // =====================================================================

        // MIGRATION: ModuleName - nvarchar(128), internal module name for identification
        // Typically matches the folder name but can differ
        builder.Property(dm => dm.ModuleName)
            .HasColumnName("ModuleName")
            .HasMaxLength(128);

        // MIGRATION: FolderName - nvarchar(128), folder path under DesktopModules directory
        builder.Property(dm => dm.FolderName)
            .HasColumnName("FolderName")
            .HasMaxLength(128);

        // MIGRATION: FriendlyName - nvarchar(128), user-friendly display name
        builder.Property(dm => dm.FriendlyName)
            .HasColumnName("FriendlyName")
            .HasMaxLength(128);

        // =====================================================================
        // Metadata Properties
        // =====================================================================

        // MIGRATION: Description - ntext, detailed description of module functionality
        builder.Property(dm => dm.Description)
            .HasColumnName("Description")
            .HasColumnType("ntext");

        // MIGRATION: Version - nvarchar(8), version string (e.g., "01.00.00")
        builder.Property(dm => dm.Version)
            .HasColumnName("Version")
            .HasMaxLength(8);

        // MIGRATION: BusinessControllerClass - nvarchar(200), fully qualified type name
        // Implements IPortable, ISearchable, or IUpgradeable interfaces
        builder.Property(dm => dm.BusinessControllerClass)
            .HasColumnName("BusinessControllerClass")
            .HasMaxLength(200);

        // =====================================================================
        // Flag Properties
        // =====================================================================

        // MIGRATION: IsPremium - bit, true if this is a premium (paid) module
        builder.Property(dm => dm.IsPremium)
            .HasColumnName("IsPremium");

        // MIGRATION: IsAdmin - bit, true if this is an admin-only module
        builder.Property(dm => dm.IsAdmin)
            .HasColumnName("IsAdmin");

        // MIGRATION: SupportedFeatures - int, bitwise flags for supported features
        // 1 = IsPortable (supports import/export)
        // 2 = IsSearchable (integrates with search)
        // 4 = IsUpgradeable (supports upgrade scripts)
        builder.Property(dm => dm.SupportedFeatures)
            .HasColumnName("SupportedFeatures");

        // =====================================================================
        // Extension Information Properties
        // =====================================================================

        // MIGRATION: CompatibleVersions - nvarchar(max), comma-separated list of compatible DNN versions
        builder.Property(dm => dm.CompatibleVersions)
            .HasColumnName("CompatibleVersions")
            .HasColumnType("nvarchar(max)");

        // MIGRATION: Dependencies - nvarchar(max), module dependencies listing
        builder.Property(dm => dm.Dependencies)
            .HasColumnName("Dependencies")
            .HasColumnType("nvarchar(max)");

        // MIGRATION: Permissions - nvarchar(max), default permission settings
        builder.Property(dm => dm.Permissions)
            .HasColumnName("Permissions")
            .HasColumnType("nvarchar(max)");

        // =====================================================================
        // Navigation Properties - Relationships
        // =====================================================================

        // MIGRATION: DesktopModule -> ModuleDefinitions relationship (one desktop module has many definitions)
        // This is configured from the ModuleDefinition side (see ModuleDefinitionConfiguration)

        // =====================================================================
        // Indexes for Query Optimization
        // =====================================================================

        // MIGRATION: Unique index on ModuleName for identification
        builder.HasIndex(dm => dm.ModuleName)
            .IsUnique()
            .HasDatabaseName("IX_DesktopModules_ModuleName");

        // MIGRATION: Index on FriendlyName for name-based searches
        builder.HasIndex(dm => dm.FriendlyName)
            .HasDatabaseName("IX_DesktopModules_FriendlyName");

        // MIGRATION: Index on FolderName for folder-based lookups
        builder.HasIndex(dm => dm.FolderName)
            .HasDatabaseName("IX_DesktopModules_FolderName");

        // MIGRATION: Index on IsPremium for filtering premium modules
        builder.HasIndex(dm => dm.IsPremium)
            .HasDatabaseName("IX_DesktopModules_IsPremium");

        // MIGRATION: Index on IsAdmin for filtering admin modules
        builder.HasIndex(dm => dm.IsAdmin)
            .HasDatabaseName("IX_DesktopModules_IsAdmin");
    }
}
