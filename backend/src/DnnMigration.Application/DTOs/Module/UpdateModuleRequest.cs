// -----------------------------------------------------------------------------
// MIGRATION: This DTO is derived from Website/admin/Modules/ModuleSettings.ascx.vb
// cmdUpdate_Click handler (lines 326-385) for module update operations.
// Original VB.NET ModuleInfo property assignments converted to C# 12 record properties.
// All fields are nullable to support partial updates (PATCH-style semantics).
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Module;

/// <summary>
/// Data Transfer Object for module update requests.
/// Contains all updatable properties for modifying existing module settings.
/// Most fields are nullable to support partial updates via PUT /api/modules/{id} endpoint.
/// </summary>
/// <remarks>
/// MIGRATION: Properties mapped from ModuleSettings.ascx.vb cmdUpdate_Click handler.
/// The Visibility property maps to the legacy VisibilityState enum:
/// 0 = Maximized (fully visible), 1 = Minimized (collapsed), 2 = None (hidden).
/// </remarks>
public record UpdateModuleRequest
{
    /// <summary>
    /// Gets or sets the tab ID where the module instance exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: Required to identify the specific TabModule association to update.
    /// A module can exist on multiple tabs, so TabId is needed along with ModuleId
    /// (from URL path) to uniquely identify the TabModule record.
    /// </para>
    /// <para>
    /// This property is required because DNN modules use a TabModule junction table
    /// that stores tab-specific settings for each module placement.
    /// </para>
    /// </remarks>
    [Required(ErrorMessage = "TabId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "TabId must be a positive integer.")]
    public int TabId { get; init; }

    /// <summary>
    /// Gets or sets the module display title.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtTitle.Text (line 344 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(256, ErrorMessage = "Module title cannot exceed 256 characters.")]
    public string? ModuleTitle { get; init; }

    /// <summary>
    /// Gets or sets the module alignment (e.g., "left", "right", "center").
    /// </summary>
    /// <remarks>
    /// MIGRATION: From cboAlign.SelectedItem.Value (line 345 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(10, ErrorMessage = "Alignment cannot exceed 10 characters.")]
    public string? Alignment { get; init; }

    /// <summary>
    /// Gets or sets the module background color.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtColor.Text (line 346 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(50, ErrorMessage = "Color cannot exceed 50 characters.")]
    public string? Color { get; init; }

    /// <summary>
    /// Gets or sets the module border style.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtBorder.Text (line 347 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(50, ErrorMessage = "Border cannot exceed 50 characters.")]
    public string? Border { get; init; }

    /// <summary>
    /// Gets or sets the module icon file path.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ctlIcon.Url (line 348 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(256, ErrorMessage = "Icon file path cannot exceed 256 characters.")]
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets or sets the module output cache time in seconds.
    /// Value must be between 0 (no caching) and 86400 (24 hours).
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtCacheTime.Text (lines 349-353 of ModuleSettings.ascx.vb)
    /// </remarks>
    [Range(0, 86400, ErrorMessage = "Cache time must be between 0 and 86400 seconds (24 hours).")]
    public int? CacheTime { get; init; }

    /// <summary>
    /// Gets or sets the module visibility state.
    /// 0 = Maximized (fully visible), 1 = Minimized (collapsed), 2 = None (hidden).
    /// </summary>
    /// <remarks>
    /// MIGRATION: From cboVisibility.SelectedItem.Value (lines 359-363 of ModuleSettings.ascx.vb)
    /// Maps to legacy VisibilityState enum: Maximized=0, Minimized=1, None=2
    /// </remarks>
    [Range(0, 2, ErrorMessage = "Visibility must be 0 (Maximized), 1 (Minimized), or 2 (None).")]
    public int? Visibility { get; init; }

    /// <summary>
    /// Gets or sets the header HTML content displayed above the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtHeader.Text (line 365 of ModuleSettings.ascx.vb)
    /// </remarks>
    public string? Header { get; init; }

    /// <summary>
    /// Gets or sets the footer HTML content displayed below the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtFooter.Text (line 366 of ModuleSettings.ascx.vb)
    /// </remarks>
    public string? Footer { get; init; }

    /// <summary>
    /// Gets or sets the date when the module becomes visible.
    /// Module is hidden before this date.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtStartDate.Text (lines 367-371 of ModuleSettings.ascx.vb)
    /// Null value indicates no start date restriction.
    /// </remarks>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets or sets the date when the module becomes hidden.
    /// Module is hidden after this date.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtEndDate.Text (lines 372-376 of ModuleSettings.ascx.vb)
    /// Null value indicates no end date restriction.
    /// </remarks>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or sets the container skin source path.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From ctlModuleContainer.SkinSrc (line 377 of ModuleSettings.ascx.vb)
    /// </remarks>
    [StringLength(256, ErrorMessage = "Container source path cannot exceed 256 characters.")]
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets or sets whether the module inherits view permissions from the parent tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkInheritPermissions.Checked (line 379 of ModuleSettings.ascx.vb)
    /// </remarks>
    public bool? InheritViewPermissions { get; init; }

    /// <summary>
    /// Gets or sets whether the module title is displayed.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkDisplayTitle.Checked (line 380 of ModuleSettings.ascx.vb)
    /// </remarks>
    public bool? DisplayTitle { get; init; }

    /// <summary>
    /// Gets or sets whether the print button is displayed for the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkDisplayPrint.Checked (line 381 of ModuleSettings.ascx.vb)
    /// </remarks>
    public bool? DisplayPrint { get; init; }

    /// <summary>
    /// Gets or sets whether the RSS/syndication button is displayed for the module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkDisplaySyndicate.Checked (line 382 of ModuleSettings.ascx.vb)
    /// </remarks>
    public bool? DisplaySyndicate { get; init; }

    /// <summary>
    /// Gets or sets whether this module is the default module for new pages.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkDefault.Checked (line 383 of ModuleSettings.ascx.vb)
    /// Only administrators can modify this setting.
    /// </remarks>
    public bool? IsDefaultModule { get; init; }

    /// <summary>
    /// Gets or sets whether settings changes apply to all instances of this module.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkAllModules.Checked (line 384 of ModuleSettings.ascx.vb)
    /// Only administrators can modify this setting.
    /// </remarks>
    public bool? AllModules { get; init; }

    /// <summary>
    /// Gets or sets whether the module appears on all tabs/pages.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From chkAllTabs.Checked (line 358 of ModuleSettings.ascx.vb)
    /// When changed from false to true, the module is copied to all tabs.
    /// When changed from true to false, the module is removed from other tabs.
    /// Only administrators can modify this setting.
    /// </remarks>
    public bool? AllTabs { get; init; }

    /// <summary>
    /// Gets or sets whether the module is soft-deleted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: From ModuleInfo.IsDeleted property. Used for soft-delete functionality.
    /// </para>
    /// <para>
    /// When true, the module is marked as deleted but remains in the database for
    /// potential recovery (recycle bin functionality). When false, the module is
    /// active and visible (subject to other visibility settings like StartDate/EndDate).
    /// </para>
    /// <para>
    /// Setting this to true via update is an alternative to calling the delete endpoint,
    /// allowing for soft-delete operations with additional update fields.
    /// </para>
    /// </remarks>
    public bool? IsDeleted { get; init; }
}
