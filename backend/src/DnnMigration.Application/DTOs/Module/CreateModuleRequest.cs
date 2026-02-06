// -----------------------------------------------------------------------------
// <copyright file="CreateModuleRequest.cs" company="DNN Migration">
//   Copyright (c) DNN Migration. All rights reserved.
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// <summary>
//   Data Transfer Object for module creation requests.
//   MIGRATION: Derived from ModuleInfo.vb constructor and ModuleController.AddModule pattern.
// </summary>
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Module;

/// <summary>
/// Represents a request to create a new module instance on a tab/page.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This DTO captures all fields needed to add a new module to a tab/page
/// via POST /api/modules endpoint. Fields are derived from:
/// - ModuleInfo.vb constructor (lines 102-125) for default values
/// - ModuleController.AddModule method (lines 645-682) for creation pattern
/// - DataProvider.Instance().AddModule for database persistence
/// - DataProvider.Instance().AddTabModule for tab-module association
/// </para>
/// <para>
/// The module creation process involves:
/// 1. Creating a Module record (PortalID, ModuleDefID, ModuleTitle, AllTabs, Header, Footer, StartDate, EndDate, InheritViewPermissions, IsDeleted)
/// 2. Creating a TabModule record (TabID, ModuleID, ModuleOrder, PaneName, CacheTime, Alignment, Color, Border, IconFile, Visibility, ContainerSrc, DisplayTitle, DisplayPrint, DisplaySyndicate)
/// </para>
/// </remarks>
public record CreateModuleRequest
{
    /// <summary>
    /// Gets or sets the target tab/page ID where the module will be placed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._TabID (line 44). Required for module placement.
    /// References the Tabs table for the target page.
    /// </remarks>
    /// <example>42</example>
    [Required(ErrorMessage = "TabId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "TabId must be a positive integer.")]
    public int TabId { get; init; }

    /// <summary>
    /// Gets or sets the module definition ID that defines the type of module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._ModuleDefID (line 47). Required for module type.
    /// Links to ModuleDefinitionInfo to determine module behavior and control source.
    /// </remarks>
    /// <example>117</example>
    [Required(ErrorMessage = "ModuleDefId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "ModuleDefId must be a positive integer.")]
    public int ModuleDefId { get; init; }

    /// <summary>
    /// Gets or sets the name of the pane where the module will be placed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._PaneName (line 49). Required for layout positioning.
    /// Common values: "ContentPane", "LeftPane", "RightPane", "TopPane", "BottomPane".
    /// The pane must exist in the skin/theme being used.
    /// </remarks>
    /// <example>ContentPane</example>
    [Required(ErrorMessage = "PaneName is required.")]
    [StringLength(50, ErrorMessage = "PaneName cannot exceed 50 characters.")]
    public string PaneName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the position of the module within the pane.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._ModuleOrder (line 48). Optional, defaults to -1.
    /// When -1 or null, the module is positioned at the bottom of the pane.
    /// See ModuleController.AddModule lines 667-672 for ordering logic.
    /// </remarks>
    /// <example>1</example>
    public int? ModuleOrder { get; init; }

    /// <summary>
    /// Gets or sets the display title of the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._ModuleTitle (line 50). Optional.
    /// Shown in module header when DisplayTitle is true.
    /// </remarks>
    /// <example>Welcome Module</example>
    [StringLength(256, ErrorMessage = "ModuleTitle cannot exceed 256 characters.")]
    public string? ModuleTitle { get; init; }

    /// <summary>
    /// Gets or sets the container skin source path for the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._ContainerSrc (line 66). Optional.
    /// Specifies custom container skin. Format: "[G]Skins/SkinName/ContainerName.ascx"
    /// If null, uses the default container from the skin.
    /// </remarks>
    /// <example>[G]Skins/DefaultSkin/Title.ascx</example>
    [StringLength(256, ErrorMessage = "ContainerSrc cannot exceed 256 characters.")]
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets or sets the alignment of the module content.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Alignment (line 54). Optional.
    /// Valid values: "left", "center", "right", or null for default.
    /// </remarks>
    /// <example>left</example>
    [StringLength(10, ErrorMessage = "Alignment cannot exceed 10 characters.")]
    public string? Alignment { get; init; }

    /// <summary>
    /// Gets or sets the cache duration in seconds.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._CacheTime (line 52). Optional.
    /// Range: 0 (no caching) to 86400 (24 hours).
    /// Used for output caching of module content.
    /// </remarks>
    /// <example>3600</example>
    [Range(0, 86400, ErrorMessage = "CacheTime must be between 0 and 86400 seconds (24 hours).")]
    public int? CacheTime { get; init; }

    /// <summary>
    /// Gets or sets the background color of the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Color (line 55). Optional.
    /// CSS color value for module background styling.
    /// </remarks>
    /// <example>#FFFFFF</example>
    [StringLength(50, ErrorMessage = "Color cannot exceed 50 characters.")]
    public string? Color { get; init; }

    /// <summary>
    /// Gets or sets the border style of the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Border (line 56). Optional.
    /// CSS border value for module border styling.
    /// </remarks>
    /// <example>1px solid #000000</example>
    [StringLength(50, ErrorMessage = "Border cannot exceed 50 characters.")]
    public string? Border { get; init; }

    /// <summary>
    /// Gets or sets the icon file path for the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._IconFile (line 57). Optional.
    /// Path to module icon displayed in the header.
    /// </remarks>
    /// <example>~/images/icon.gif</example>
    [StringLength(256, ErrorMessage = "IconFile cannot exceed 256 characters.")]
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets or sets the HTML content for the module header.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Header (line 62). Optional.
    /// Custom HTML injected before module content.
    /// </remarks>
    public string? Header { get; init; }

    /// <summary>
    /// Gets or sets the HTML content for the module footer.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Footer (line 63). Optional.
    /// Custom HTML injected after module content.
    /// </remarks>
    public string? Footer { get; init; }

    /// <summary>
    /// Gets or sets the date when the module becomes visible.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._StartDate (line 64). Optional.
    /// Module is hidden before this date. Null means immediately visible.
    /// Used for scheduled content publication.
    /// </remarks>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets or sets the date when the module is hidden.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._EndDate (line 65). Optional.
    /// Module is hidden after this date. Null means never expires.
    /// Used for scheduled content expiration.
    /// </remarks>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or sets whether the module title is displayed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._DisplayTitle (line 67). Default: true.
    /// Constructor default in ModuleInfo.vb line 122: _DisplayTitle = True
    /// </remarks>
    public bool DisplayTitle { get; init; } = true;

    /// <summary>
    /// Gets or sets whether the print button is displayed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._DisplayPrint (line 68). Default: true.
    /// Constructor default in ModuleInfo.vb line 123: _DisplayPrint = True
    /// Shows a print icon in the module header when enabled.
    /// </remarks>
    public bool DisplayPrint { get; init; } = true;

    /// <summary>
    /// Gets or sets whether RSS syndication is enabled.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._DisplaySyndicate (line 69). Default: false.
    /// Constructor default in ModuleInfo.vb line 124: _DisplaySyndicate = False
    /// Shows an RSS icon in the module header when enabled.
    /// </remarks>
    public bool DisplaySyndicate { get; init; } = false;

    /// <summary>
    /// Gets or sets whether the module inherits view permissions from the tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._InheritViewPermissions (line 70). Default: false.
    /// When true, module uses tab's view permissions instead of its own.
    /// Simplifies permission management for modules that follow page security.
    /// </remarks>
    public bool InheritViewPermissions { get; init; } = false;

    /// <summary>
    /// Gets or sets whether the module appears on all tabs/pages.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._AllTabs (line 58). Default: false.
    /// When true, module is automatically added to all tabs in the portal.
    /// Used for site-wide modules like navigation or headers.
    /// </remarks>
    public bool AllTabs { get; init; } = false;

    /// <summary>
    /// Gets or sets the visibility state of the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModuleInfo._Visibility (line 59). Default: 0 (Maximized).
    /// Values map to VisibilityState enum from ModuleInfo.vb (lines 30-34):
    /// 0 = Maximized (fully visible, default)
    /// 1 = Minimized (collapsed, only header shown)
    /// 2 = None (completely hidden)
    /// </remarks>
    /// <example>0</example>
    [Range(0, 2, ErrorMessage = "Visibility must be 0 (Maximized), 1 (Minimized), or 2 (None).")]
    public int Visibility { get; init; } = 0;

    /// <summary>
    /// Gets or sets the collection of permissions to assign to the module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From ModuleController.AddModule permission loop pattern.
    /// Original code iterated over ModulePermissions collection to add permissions.
    /// </para>
    /// <para>
    /// Permissions define which roles or users can view, edit, or perform other
    /// actions on the module. When null or empty, the module inherits default
    /// permissions based on portal settings.
    /// </para>
    /// </remarks>
    public IEnumerable<ModulePermissionRequest>? Permissions { get; init; }
}

/// <summary>
/// Represents a permission assignment request for a module.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Converted from ModulePermissionInfo pattern in ModuleController.AddModule.
/// Business Rule: Either RoleId or UserId should be set, but not both simultaneously.
/// </para>
/// <para>
/// This nested record captures the permission data needed to create ModulePermission
/// entities during module creation. The actual ModulePermission entities are created
/// by the service layer using these values.
/// </para>
/// </remarks>
public record ModulePermissionRequest
{
    /// <summary>
    /// Gets or sets the permission definition ID.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ModulePermissionInfo.PermissionID.
    /// References the Permissions table to identify which permission type is being assigned
    /// (e.g., VIEW, EDIT, DELETE).
    /// </remarks>
    [Required(ErrorMessage = "PermissionId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "PermissionId must be a positive integer.")]
    public int PermissionId { get; init; }

    /// <summary>
    /// Gets or sets the role ID for role-based permissions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From ModulePermissionInfo.RoleID with nullable type.
    /// </para>
    /// <para>
    /// When set, this permission applies to all users in the specified role.
    /// Must be null when UserId is set (user-specific permission).
    /// </para>
    /// </remarks>
    public int? RoleId { get; init; }

    /// <summary>
    /// Gets or sets the user ID for user-specific permissions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From ModulePermissionInfo.UserID with nullable type.
    /// </para>
    /// <para>
    /// When set, this permission applies only to the specified user.
    /// Must be null when RoleId is set (role-based permission).
    /// </para>
    /// </remarks>
    public int? UserId { get; init; }

    /// <summary>
    /// Gets or sets whether access is allowed or denied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From ModulePermissionInfo.AllowAccess.
    /// </para>
    /// <para>
    /// When true, the permission grants access. When false, the permission explicitly
    /// denies access, which typically takes precedence over allow permissions.
    /// </para>
    /// </remarks>
    public bool AllowAccess { get; init; } = true;
}
