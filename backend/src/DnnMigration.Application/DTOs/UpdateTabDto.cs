namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to update an existing tab (page).
/// </summary>
/// <remarks>
/// MIGRATION: This DTO exposes the editable subset of the legacy
/// <c>TabInfo</c> class (<c>Library/Components/Tabs/TabInfo.vb</c>), matching the
/// attributes surfaced on the original Page Settings screen. It is bound from the
/// request body of <c>PUT /api/tabs/{id}</c>; the tab identifier is taken from the
/// route and the owning <c>PortalID</c> is immutable, so neither <c>TabID</c> nor
/// <c>PortalID</c> is carried in the body. Validation rules are enforced by the
/// sibling FluentValidation validator rather than by this type, which intentionally
/// contains no logic, data access, or attributes.
/// </remarks>
public record UpdateTabDto
{
    /// <summary>
    /// Display name of the tab (page). MIGRATION: legacy <c>TabInfo.TabName</c>
    /// (VB <c>String</c>); required and therefore defaulted to an empty string.
    /// </summary>
    public string TabName { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the parent tab establishing the page hierarchy.
    /// MIGRATION: legacy <c>TabInfo.ParentId</c> (VB <c>Integer</c>).
    /// </summary>
    public int ParentId { get; init; }

    /// <summary>
    /// Zero-based ordering position of the tab among its siblings.
    /// MIGRATION: legacy <c>TabInfo.TabOrder</c> (VB <c>Integer</c>).
    /// </summary>
    public int TabOrder { get; init; }

    /// <summary>
    /// Indicates whether the tab is shown in navigation menus.
    /// MIGRATION: legacy <c>TabInfo.IsVisible</c> (VB <c>Boolean</c>).
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Optional path to the icon rendered for the tab.
    /// MIGRATION: legacy <c>TabInfo.IconFile</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? IconFile { get; init; }

    /// <summary>
    /// Indicates whether the tab's navigation link is disabled (label only).
    /// MIGRATION: legacy <c>TabInfo.DisableLink</c> (VB <c>Boolean</c>).
    /// </summary>
    public bool DisableLink { get; init; }

    /// <summary>
    /// Optional browser/page title override for the tab.
    /// MIGRATION: legacy <c>TabInfo.Title</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional meta description for the tab used for SEO.
    /// MIGRATION: legacy <c>TabInfo.Description</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional comma-delimited meta keywords for the tab used for SEO.
    /// MIGRATION: legacy <c>TabInfo.KeyWords</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? KeyWords { get; init; }

    /// <summary>
    /// Optional target URL for tabs that redirect to another location.
    /// MIGRATION: legacy <c>TabInfo.Url</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Optional path to the skin applied to the tab.
    /// MIGRATION: legacy <c>TabInfo.SkinSrc</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? SkinSrc { get; init; }

    /// <summary>
    /// Optional path to the container applied to the tab.
    /// MIGRATION: legacy <c>TabInfo.ContainerSrc</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Auto-refresh interval, in seconds, for the tab (0 disables refresh).
    /// MIGRATION: legacy <c>TabInfo.RefreshInterval</c> (VB <c>Integer</c>).
    /// </summary>
    public int RefreshInterval { get; init; }

    /// <summary>
    /// Optional custom markup injected into the page &lt;head&gt; for the tab.
    /// MIGRATION: legacy <c>TabInfo.PageHeadText</c> (nullable VB <c>String</c>).
    /// </summary>
    public string? PageHeadText { get; init; }

    /// <summary>
    /// Indicates whether the tab must be served over a secure (HTTPS) connection.
    /// MIGRATION: legacy <c>TabInfo.IsSecure</c> (VB <c>Boolean</c>).
    /// </summary>
    public bool IsSecure { get; init; }
}
