// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Portals.PortalAliasInfo → C# 12 PortalAlias entity
// Source: Library/Components/Portal/PortalAliasInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields to C# auto-properties
// - Renamed HTTPAlias to HttpAlias (following C# naming conventions)
// - Applied nullable reference types
// - Added navigation property to Portal
// - Added IsPrimary flag for primary alias designation
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a domain alias for a portal, allowing a portal to be accessed via multiple URLs.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the PortalAlias table. Each portal can have multiple aliases,
/// enabling access through different domain names or URLs. One alias is typically
/// designated as the primary alias.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Portals.PortalAliasInfo.
/// HTTPAlias renamed to HttpAlias following C# naming conventions.
/// </para>
/// </remarks>
public class PortalAlias
{
    /// <summary>
    /// Gets or sets the unique identifier for the portal alias.
    /// </summary>
    /// <value>The primary key of the portal alias record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalAliasID field.
    /// </remarks>
    public int PortalAliasId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this alias belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalID field.
    /// </remarks>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the HTTP alias (domain/URL) for the portal.
    /// </summary>
    /// <value>The domain name or URL path alias (e.g., "www.example.com" or "example.com/portal1").</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _HTTPAlias field.
    /// Renamed from HTTPAlias to HttpAlias following C# naming conventions.
    /// </remarks>
    public string HttpAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the primary alias for the portal.
    /// </summary>
    /// <value><c>true</c> if this is the primary alias; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// The primary alias is used for generating canonical URLs and redirects.
    /// </remarks>
    public bool IsPrimary { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the portal that this alias belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    public virtual Portal? Portal { get; set; }
}
