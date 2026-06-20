namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PortalInfo.vb (DotNetNuke.Entities.Portals, assembly 4.9.0.85). The legacy
// XML serialization attributes (<XmlRoot("settings")>, <XmlElement>, <XmlIgnore>), the IPropertyAccess
// implementation, the legacy copyright header, and all VB Imports are dropped. The <XmlIgnore> ReadOnly
// HomeDirectoryMapPath runtime helper (which called Services.FileSystem.FolderController.GetMappedDirectory)
// is dropped because it depends on the filesystem/Globals subsystems that are out of scope for this entity;
// path resolution moves to the service/API layer. The lazy-computed Users/Pages counts (legacy default
// Null.NullInteger, computed on demand via UserController.GetUserCountByPortal / TabController.GetTabCount)
// become nullable plain properties populated by the service/repository layer. VB Single HostFee -> C# float.
//
// MIGRATION (CP1 schema-fidelity correction, ADR-002): the physical [Portals] table (authoritative
// DotNetNuke.Schema.SqlDataProvider) declares ExpiryDate, AdministratorId, AdministratorRoleId,
// RegisteredRoleId, SiteLogHistory, HomeTabId, LoginTabId, UserTabId, AdminTabId and SplashTabId as
// NULL-able columns. They are therefore modeled as nullable CLR types (DateTime? / int?) so EF Core
// materialization preserves the legacy Null.NullInteger/no-value semantics rather than coercing a missing
// DB value to 0 (a 0 AdministratorId would falsely denote the host superuser). Nonphysical/aggregate fields
// (Email, SuperTabId, AdministratorRoleName, RegisteredRoleName, Users, Pages, Version) are NOT columns of
// [Portals]; they are runtime/projection fields and are explicitly .Ignore()d by the Fluent
// IEntityTypeConfiguration in the Infrastructure layer (PortalConfiguration), keeping this a pure POCO.

/// <summary>
/// Portal aggregate-root domain entity. Pure POCO representation of a DotNetNuke portal (site),
/// ported verbatim — in field set and semantics — from the legacy <c>PortalInfo</c> value object.
/// Carries zero framework dependencies (no EF, DataAnnotations, or serialization attributes), in
/// keeping with the Clean Architecture Domain (inner) ring.
/// </summary>
public class Portal
{
    /// <summary>Primary key / identity of the portal. Maps to the legacy <c>PortalID</c> column.</summary>
    public int PortalID { get; set; }

    /// <summary>Display name of the portal.</summary>
    public string? PortalName { get; set; }

    /// <summary>Relative path to the portal logo image file.</summary>
    public string? LogoFile { get; set; }

    /// <summary>Custom footer text / copyright line rendered on portal pages.</summary>
    public string? FooterText { get; set; }

    // MIGRATION: physical [Portals].ExpiryDate is NULL-able -> nullable DateTime (legacy "no expiry").
    /// <summary>Date on which the portal subscription expires; null when the portal does not expire.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>User registration mode (None / Private / Public / Verified — legacy integer code).</summary>
    public int UserRegistration { get; set; }

    /// <summary>Banner advertising mode (legacy integer code).</summary>
    public int BannerAdvertising { get; set; }

    // MIGRATION: physical [Portals].AdministratorId is NULL-able -> nullable int (preserves
    // Null.NullInteger semantics; null != 0, since user id 0 would falsely denote the host superuser).
    /// <summary>User id of the portal administrator account; null when unset.</summary>
    public int? AdministratorId { get; set; }

    /// <summary>ISO currency code used for portal billing (e.g. USD).</summary>
    public string? Currency { get; set; }

    // MIGRATION: legacy VB Single -> C# float (preserves single-precision storage semantics).
    /// <summary>Monthly hosting fee charged for the portal.</summary>
    public float HostFee { get; set; }

    /// <summary>Allotted disk space quota for the portal, in megabytes (0 = unlimited).</summary>
    public int HostSpace { get; set; }

    /// <summary>Maximum number of pages permitted for the portal (0 = unlimited).</summary>
    public int PageQuota { get; set; }

    /// <summary>Maximum number of users permitted for the portal (0 = unlimited).</summary>
    public int UserQuota { get; set; }

    // MIGRATION: physical [Portals].AdministratorRoleId is NULL-able -> nullable int.
    /// <summary>Role id of the portal Administrators security role; null when unset.</summary>
    public int? AdministratorRoleId { get; set; }

    // MIGRATION: nonphysical — not a [Portals] column; resolved from the Roles table at the service layer
    // and .Ignore()d by PortalConfiguration.
    /// <summary>Name of the portal Administrators security role (projection only).</summary>
    public string? AdministratorRoleName { get; set; }

    // MIGRATION: physical [Portals].RegisteredRoleId is NULL-able -> nullable int.
    /// <summary>Role id of the Registered Users security role; null when unset.</summary>
    public int? RegisteredRoleId { get; set; }

    // MIGRATION: nonphysical — not a [Portals] column; resolved from the Roles table at the service layer
    // and .Ignore()d by PortalConfiguration.
    /// <summary>Name of the Registered Users security role (projection only).</summary>
    public string? RegisteredRoleName { get; set; }

    /// <summary>Portal description used for metadata and search.</summary>
    public string? Description { get; set; }

    /// <summary>Portal keywords used for metadata and search.</summary>
    public string? KeyWords { get; set; }

    /// <summary>Relative path to the portal background image file.</summary>
    public string? BackgroundFile { get; set; }

    // MIGRATION: name preserved verbatim as `GUID` (legacy <XmlIgnore> but a persisted column). The physical
    // column carries a `newid()` default, so it is server-generated/retained and is NOT client-writable
    // (excluded from the create/update DTO surface).
    /// <summary>Globally unique identifier for the portal instance.</summary>
    public Guid GUID { get; set; }

    /// <summary>Name of the payment processor used for portal subscription billing.</summary>
    public string? PaymentProcessor { get; set; }

    /// <summary>Password/credential for the configured payment processor.</summary>
    public string? ProcessorPassword { get; set; }

    /// <summary>User id/account for the configured payment processor.</summary>
    public string? ProcessorUserId { get; set; }

    // MIGRATION: physical [Portals].SiteLogHistory is NULL-able -> nullable int.
    /// <summary>Number of days of site log history to retain; null when unset.</summary>
    public int? SiteLogHistory { get; set; }

    // MIGRATION: nonphysical — not a [Portals] column; the administrative contact email is derived from the
    // administrator user account at the service layer and .Ignore()d by PortalConfiguration.
    /// <summary>Administrative contact email address for the portal (projection only).</summary>
    public string? Email { get; set; }

    // MIGRATION: physical [Portals].AdminTabId is NULL-able -> nullable int.
    /// <summary>Tab (page) id of the portal Admin page; null when unset.</summary>
    public int? AdminTabId { get; set; }

    // MIGRATION: nonphysical — SuperTabId is a host-level setting, not a [Portals] column; .Ignore()d by
    // PortalConfiguration.
    /// <summary>Tab (page) id of the host SuperUser page (projection only).</summary>
    public int SuperTabId { get; set; }

    // MIGRATION: was `Private _Users As Integer = Null.NullInteger` with an on-demand lazy getter
    // (UserController.GetUserCountByPortal). The lazy compute is dropped; this is now a nullable
    // metric populated by the service/repository layer (null = not yet computed). Nonphysical — .Ignore()d.
    /// <summary>Cached count of users belonging to the portal; null when not yet populated.</summary>
    public int? Users { get; set; }

    // MIGRATION: was `Private _Pages As Integer = Null.NullInteger` with an on-demand lazy getter
    // (TabController.GetTabCount). The lazy compute is dropped; this is now a nullable metric
    // populated by the service/repository layer (null = not yet computed). Nonphysical — .Ignore()d.
    /// <summary>Cached count of pages (tabs) belonging to the portal; null when not yet populated.</summary>
    public int? Pages { get; set; }

    // MIGRATION: physical [Portals].SplashTabId is NULL-able -> nullable int.
    /// <summary>Tab (page) id of the portal splash page; null when unset.</summary>
    public int? SplashTabId { get; set; }

    // MIGRATION: physical [Portals].HomeTabId is NULL-able -> nullable int.
    /// <summary>Tab (page) id of the portal home page; null when unset.</summary>
    public int? HomeTabId { get; set; }

    // MIGRATION: physical [Portals].LoginTabId is NULL-able -> nullable int.
    /// <summary>Tab (page) id of the portal login page; null when unset.</summary>
    public int? LoginTabId { get; set; }

    // MIGRATION: physical [Portals].UserTabId is NULL-able -> nullable int.
    /// <summary>Tab (page) id of the portal user/profile page; null when unset.</summary>
    public int? UserTabId { get; set; }

    /// <summary>Default language/culture code for the portal (e.g. en-US).</summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>Default time zone offset for the portal, in minutes from server time.</summary>
    public int TimeZoneOffset { get; set; }

    /// <summary>Relative home directory path where the portal stores its files.</summary>
    public string? HomeDirectory { get; set; }

    // MIGRATION: nonphysical — not a [Portals] column; the portal/site version stamp is a runtime value and
    // .Ignore()d by PortalConfiguration.
    /// <summary>Portal/site version stamp (projection only).</summary>
    public string? Version { get; set; }
}
