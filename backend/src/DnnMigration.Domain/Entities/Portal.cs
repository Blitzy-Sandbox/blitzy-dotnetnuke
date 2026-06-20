namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PortalInfo.vb (DotNetNuke.Entities.Portals, assembly 4.9.0.85). The legacy
// XML serialization attributes (<XmlRoot("settings")>, <XmlElement>, <XmlIgnore>), the IPropertyAccess
// implementation, the legacy copyright header, and all VB Imports are dropped. The <XmlIgnore> ReadOnly
// HomeDirectoryMapPath runtime helper (which called Services.FileSystem.FolderController.GetMappedDirectory)
// is dropped because it depends on the filesystem/Globals subsystems that are out of scope for this entity;
// path resolution moves to the service/API layer. The lazy-computed Users/Pages counts (legacy default
// Null.NullInteger, computed on demand via UserController.GetUserCountByPortal / TabController.GetTabCount)
// become nullable plain properties populated by the service/repository layer. VB Single HostFee -> C# float;
// VB Date ExpiryDate -> C# DateTime. EF mapping (table/column names, keys, relationships) is handled by the
// Fluent IEntityTypeConfiguration in the Infrastructure layer (PortalConfiguration), keeping this a pure POCO.

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

    /// <summary>Date on which the portal subscription expires.</summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>User registration mode (None / Private / Public / Verified — legacy integer code).</summary>
    public int UserRegistration { get; set; }

    /// <summary>Banner advertising mode (legacy integer code).</summary>
    public int BannerAdvertising { get; set; }

    /// <summary>User id of the portal administrator account.</summary>
    public int AdministratorId { get; set; }

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

    /// <summary>Role id of the portal Administrators security role.</summary>
    public int AdministratorRoleId { get; set; }

    /// <summary>Name of the portal Administrators security role.</summary>
    public string? AdministratorRoleName { get; set; }

    /// <summary>Role id of the Registered Users security role.</summary>
    public int RegisteredRoleId { get; set; }

    /// <summary>Name of the Registered Users security role.</summary>
    public string? RegisteredRoleName { get; set; }

    /// <summary>Portal description used for metadata and search.</summary>
    public string? Description { get; set; }

    /// <summary>Portal keywords used for metadata and search.</summary>
    public string? KeyWords { get; set; }

    /// <summary>Relative path to the portal background image file.</summary>
    public string? BackgroundFile { get; set; }

    // MIGRATION: name preserved verbatim as `GUID` (legacy <XmlIgnore> but a persisted column).
    /// <summary>Globally unique identifier for the portal instance.</summary>
    public Guid GUID { get; set; }

    /// <summary>Name of the payment processor used for portal subscription billing.</summary>
    public string? PaymentProcessor { get; set; }

    /// <summary>Password/credential for the configured payment processor.</summary>
    public string? ProcessorPassword { get; set; }

    /// <summary>User id/account for the configured payment processor.</summary>
    public string? ProcessorUserId { get; set; }

    /// <summary>Number of days of site log history to retain.</summary>
    public int SiteLogHistory { get; set; }

    /// <summary>Administrative contact email address for the portal.</summary>
    public string? Email { get; set; }

    /// <summary>Tab (page) id of the portal Admin page.</summary>
    public int AdminTabId { get; set; }

    /// <summary>Tab (page) id of the host SuperUser page.</summary>
    public int SuperTabId { get; set; }

    // MIGRATION: was `Private _Users As Integer = Null.NullInteger` with an on-demand lazy getter
    // (UserController.GetUserCountByPortal). The lazy compute is dropped; this is now a nullable
    // metric populated by the service/repository layer (null = not yet computed).
    /// <summary>Cached count of users belonging to the portal; null when not yet populated.</summary>
    public int? Users { get; set; }

    // MIGRATION: was `Private _Pages As Integer = Null.NullInteger` with an on-demand lazy getter
    // (TabController.GetTabCount). The lazy compute is dropped; this is now a nullable metric
    // populated by the service/repository layer (null = not yet computed).
    /// <summary>Cached count of pages (tabs) belonging to the portal; null when not yet populated.</summary>
    public int? Pages { get; set; }

    /// <summary>Tab (page) id of the portal splash page.</summary>
    public int SplashTabId { get; set; }

    /// <summary>Tab (page) id of the portal home page.</summary>
    public int HomeTabId { get; set; }

    /// <summary>Tab (page) id of the portal login page.</summary>
    public int LoginTabId { get; set; }

    /// <summary>Tab (page) id of the portal user/profile page.</summary>
    public int UserTabId { get; set; }

    /// <summary>Default language/culture code for the portal (e.g. en-US).</summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>Default time zone offset for the portal, in minutes from server time.</summary>
    public int TimeZoneOffset { get; set; }

    /// <summary>Relative home directory path where the portal stores its files.</summary>
    public string? HomeDirectory { get; set; }

    /// <summary>Portal/site version stamp.</summary>
    public string? Version { get; set; }
}
