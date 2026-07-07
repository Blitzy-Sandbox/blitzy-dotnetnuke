namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to create a tab (page) within a portal's page hierarchy.
/// Bound from the body of <c>POST /api/tabs</c>, validated by the sibling
/// FluentValidation validator, and projected onto the <c>Tab</c> domain entity by
/// the AutoMapper mapping profile.
/// MIGRATION: writable subset of the legacy VB.NET <c>TabInfo</c> entity
/// (Library/Components/Tabs/TabInfo.vb). Read-only / server-computed members of the
/// legacy class (for example TabID, Level, TabPath, IsDeleted, HasChildren,
/// StartDate, EndDate, TabPermissions and the PortalSettings-loaded fields) are
/// intentionally excluded from this create-request contract.
/// </summary>
public record CreateTabDto
{
    /// <summary>Identifier of the portal that owns the tab. MIGRATION: TabInfo.PortalID (Integer).</summary>
    public int PortalID { get; init; }

    /// <summary>Display name of the tab (page). MIGRATION: TabInfo.TabName (String); required, hence non-nullable with an empty-string default.</summary>
    public string TabName { get; init; } = string.Empty;

    /// <summary>Identifier of the parent tab, establishing the page hierarchy. MIGRATION: TabInfo.ParentId (Integer).</summary>
    public int ParentId { get; init; }

    /// <summary>Sort order of the tab within its parent level. MIGRATION: TabInfo.TabOrder (Integer).</summary>
    public int TabOrder { get; init; }

    /// <summary>Whether the tab is shown in navigation. MIGRATION: TabInfo.IsVisible (Boolean).</summary>
    public bool IsVisible { get; init; }

    /// <summary>Optional icon file associated with the tab. MIGRATION: TabInfo.IconFile (String, nullable in DB).</summary>
    public string? IconFile { get; init; }

    /// <summary>Whether the tab's navigation link is disabled. MIGRATION: TabInfo.DisableLink (Boolean).</summary>
    public bool DisableLink { get; init; }

    /// <summary>Optional page title. MIGRATION: TabInfo.Title (String, nullable in DB).</summary>
    public string? Title { get; init; }

    /// <summary>Optional page description. MIGRATION: TabInfo.Description (String, nullable in DB).</summary>
    public string? Description { get; init; }

    /// <summary>Optional page keywords (SEO meta). MIGRATION: TabInfo.KeyWords (String, nullable in DB).</summary>
    public string? KeyWords { get; init; }

    /// <summary>Optional external/redirect URL for the tab. MIGRATION: TabInfo.Url (String, nullable in DB).</summary>
    public string? Url { get; init; }

    /// <summary>Optional skin source override for the tab. MIGRATION: TabInfo.SkinSrc (String, nullable in DB).</summary>
    public string? SkinSrc { get; init; }

    /// <summary>Optional container source override for the tab. MIGRATION: TabInfo.ContainerSrc (String, nullable in DB).</summary>
    public string? ContainerSrc { get; init; }

    /// <summary>Auto-refresh interval, in seconds, for the tab. MIGRATION: TabInfo.RefreshInterval (Integer).</summary>
    public int RefreshInterval { get; init; }

    /// <summary>Optional additional markup injected into the page head. MIGRATION: TabInfo.PageHeadText (String, nullable in DB).</summary>
    public string? PageHeadText { get; init; }

    /// <summary>Whether the tab requires a secure (SSL) connection. MIGRATION: TabInfo.IsSecure (Boolean).</summary>
    public bool IsSecure { get; init; }
}
