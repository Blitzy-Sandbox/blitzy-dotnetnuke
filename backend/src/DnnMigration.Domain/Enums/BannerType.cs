// -----------------------------------------------------------------------------
// <copyright file="BannerType.cs" company="DNN Migration Project">
//   Copyright (c) DNN Migration Project. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Extracted from Library/Components/Portal/PortalInfo.vb BannerAdvertising property
// MIGRATION: Original integer values from Website/admin/Portal/SiteSettings.ascx optBanners RadioButtonList
// MIGRATION: Value mapping: 0=None, 1=Site (Local "L"), 2=Vendor (Global "G")
// NOTE: This is different from DotNetNuke.Services.Vendors.BannerType which defines banner display formats (Banner, Button, etc.)

namespace DnnMigration.Domain.Enums;

/// <summary>
/// Defines the banner advertising mode for a portal.
/// Controls how banner advertisements are sourced and displayed within the portal.
/// </summary>
/// <remarks>
/// <para>
/// This enum maps directly to the Portal.BannerAdvertising database column.
/// Explicit integer values are preserved for backward compatibility with existing
/// DotNetNuke database schema and stored procedures.
/// </para>
/// <para>
/// The values correspond to the RadioButtonList options in the legacy
/// SiteSettings.ascx admin page (optBanners control).
/// </para>
/// </remarks>
[Serializable]
public enum BannerType
{
    /// <summary>
    /// Banner advertising is disabled for the portal.
    /// No banners will be displayed regardless of configuration.
    /// </summary>
    /// <remarks>
    /// Database value: 0
    /// Legacy UI: "None" option in Site Settings
    /// </remarks>
    None = 0,

    /// <summary>
    /// Only portal-owned (site-level) banners are allowed.
    /// Banners are managed locally within the portal scope.
    /// </summary>
    /// <remarks>
    /// Database value: 1
    /// Legacy UI: "Site" option in Site Settings
    /// Legacy type identifier: "L" (Local)
    /// Use case: Portal administrators can create and manage their own banners
    /// </remarks>
    Site = 1,

    /// <summary>
    /// External vendor banners are allowed in addition to site banners.
    /// Enables global/host-level banner advertising across the installation.
    /// </summary>
    /// <remarks>
    /// Database value: 2
    /// Legacy UI: "Host" option in Site Settings (renamed to "Vendor" for clarity)
    /// Legacy type identifier: "G" (Global)
    /// Use case: Allows centralized banner management and external advertising networks
    /// Note: When this mode is active, portal administrators cannot change the setting
    /// (controlled by host/super user only)
    /// </remarks>
    Vendor = 2
}
