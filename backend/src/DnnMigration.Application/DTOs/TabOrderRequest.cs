// -----------------------------------------------------------------------
// <copyright file="TabOrderRequest.cs" company="DNN Migration">
//     Copyright (c) DNN Migration. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// <summary>
//     Data Transfer Object for tab reordering requests within navigation hierarchy.
// </summary>
// -----------------------------------------------------------------------
// MIGRATION: Fields derived from TabController.vb UpdatePortalTabOrder method signature:
// Public Sub UpdatePortalTabOrder(ByVal PortalId As Integer, ByVal TabId As Integer, 
//     ByVal NewParentId As Integer, ByVal Level As Integer, ByVal Order As Integer, 
//     ByVal IsVisible As Boolean, Optional ByVal NewTab As Boolean = False)
// Note: PortalId and TabId are provided via API route parameters.
// -----------------------------------------------------------------------

namespace DnnMigration.Application.DTOs;

/// <summary>
/// Data Transfer Object for tab reordering requests.
/// Used as request body for PUT /api/tabs/{id}/order endpoint to support
/// tab reorganization within the page navigation hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// This DTO allows reordering tabs within the navigation structure, including:
/// - Moving tabs to a new parent (hierarchy change)
/// - Changing the display order position
/// - Adjusting the hierarchy level
/// - Toggling visibility during reorder
/// </para>
/// <para>
/// MIGRATION: Derived from the legacy DotNetNuke TabController.vb UpdatePortalTabOrder method
/// which handles tab hierarchy reordering operations. The PortalId and TabId parameters
/// are supplied via the API route (e.g., /api/tabs/{id}/order) rather than the request body.
/// </para>
/// </remarks>
/// <example>
/// Example JSON request body:
/// <code>
/// {
///     "parentId": 5,
///     "order": 3,
///     "level": 1,
///     "isVisible": true
/// }
/// </code>
/// </example>
public record TabOrderRequest
{
    /// <summary>
    /// Gets or sets the new parent tab ID for moving the tab to a different parent in the hierarchy.
    /// </summary>
    /// <value>
    /// The parent tab ID, or null if no parent change is requested.
    /// A value of -1 indicates moving the tab to root level (no parent).
    /// </value>
    /// <remarks>
    /// MIGRATION: Maps to the NewParentId parameter in UpdatePortalTabOrder.
    /// In the legacy system, -1 represented a root-level tab with no parent,
    /// and -2 indicated a deleted tab (handled separately via delete endpoint).
    /// </remarks>
    public int? ParentId { get; init; }

    /// <summary>
    /// Gets or sets the new position order for the tab within its parent context.
    /// </summary>
    /// <value>
    /// The zero-based order position. Lower values appear first in navigation.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is a required field that determines the tab's position among its siblings.
    /// When reordering, all sibling tabs may have their order values adjusted accordingly.
    /// </para>
    /// <para>
    /// MIGRATION: Maps to the Order parameter in UpdatePortalTabOrder.
    /// </para>
    /// </remarks>
    public required int Order { get; init; }

    /// <summary>
    /// Gets or sets the new hierarchy level for the tab.
    /// </summary>
    /// <value>
    /// The zero-based hierarchy level, or null if no level change is explicitly requested.
    /// Level 0 represents root-level tabs, level 1 represents first-level children, etc.
    /// </value>
    /// <remarks>
    /// <para>
    /// The level is typically calculated automatically based on the ParentId.
    /// This property allows explicit level specification when needed for complex
    /// hierarchy operations or when preserving specific level semantics.
    /// </para>
    /// <para>
    /// MIGRATION: Maps to the Level parameter in UpdatePortalTabOrder.
    /// </para>
    /// </remarks>
    public int? Level { get; init; }

    /// <summary>
    /// Gets or sets whether the tab should be visible in navigation after reordering.
    /// </summary>
    /// <value>
    /// True if the tab should be visible in navigation menus, false to hide it,
    /// or null to leave the visibility unchanged.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property allows changing tab visibility as part of a reorder operation,
    /// which is useful when moving tabs and simultaneously updating their display status.
    /// </para>
    /// <para>
    /// MIGRATION: Maps to the IsVisible parameter in UpdatePortalTabOrder.
    /// In the legacy system, this controlled whether the tab appeared in navigation
    /// menus and breadcrumbs.
    /// </para>
    /// </remarks>
    public bool? IsVisible { get; init; }
}
