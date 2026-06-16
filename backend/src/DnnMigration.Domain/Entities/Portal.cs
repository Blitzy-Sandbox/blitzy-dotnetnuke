namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PortalInfo.vb (DotNetNuke.Entities.Portals). XML serialization attributes
// (<XmlRoot>/<XmlElement>/<XmlIgnore>), the IPropertyAccess implementation, and the HomeDirectoryMapPath
// runtime helper (which depended on Services.FileSystem.FolderController + Common.Globals) are dropped —
// path resolution moves to the service/API layer. Lazy-computed Users/Pages counts become nullable plain
// properties (populated by the service/repository layer). VB Single HostFee -> C# float; VB Date -> DateTime.
// EF mapping is handled by a Fluent IEntityTypeConfiguration in the Infrastructure layer (PortalConfiguration);
// this Domain entity intentionally carries ZERO framework dependencies.

/// <summary>
/// Portal aggregate-root domain entity. A portal represents a single site/tenant within the
/// multi-tenant DotNetNuke installation. This is a pure POCO port of the legacy <c>PortalInfo</c>
/// value object; its public contract and domain semantics are preserved per the migration plan.
/// </summary>
public class Portal
{
    /// <summary>Unique identifier (primary key) of the portal.</summary>
    public int PortalID { get; set; }

    /// <summary>Display name of the portal.</summary>
    public string? PortalName { get; set; }

    /// <summary>Relative path/filename of the portal logo image.</summary>
    public string? LogoFile { get; set; }

    /// <summary>Copyright/footer text rendered at the bottom of portal pages.</summary>
    public string? FooterText { get; set; }

    // MIGRATION: schema column [ExpiryDate] datetime NULL -> nullable DateTime? (Null.NullDate sentinel
    // becomes null). Coercing this to a non-nullable DateTime would lose the legacy "no expiry" semantics.
    /// <summary>Date on which the portal subscription/account expires; <c>null</c> when the portal never expires.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>User-registration mode for the portal (legacy integer code: None/Private/Public/Verified).</summary>
    public int UserRegistration { get; set; }

    /// <summary>Banner advertising mode for the portal (legacy integer code).</summary>
    public int BannerAdvertising { get; set; }

    // MIGRATION: schema column [AdministratorId] int NULL -> nullable int? (legacy Null.NullInteger sentinel
    // becomes null; never coerce an unassigned administrator to 0).
    /// <summary>User identifier of the portal administrator; <c>null</c> when none is assigned.</summary>
    public int? AdministratorId { get; set; }

    /// <summary>ISO currency code used when charging hosting fees.</summary>
    public string? Currency { get; set; }

    // MIGRATION: legacy VB type was Single -> mapped to C# float.
    /// <summary>Recurring hosting fee charged to the portal.</summary>
    public float HostFee { get; set; }

    /// <summary>Disk-space quota (MB) allotted to the portal; 0 indicates unlimited.</summary>
    public int HostSpace { get; set; }

    /// <summary>Maximum number of pages allowed for the portal; 0 indicates unlimited.</summary>
    public int PageQuota { get; set; }

    /// <summary>Maximum number of users allowed for the portal; 0 indicates unlimited.</summary>
    public int UserQuota { get; set; }

    // MIGRATION: schema column [AdministratorRoleId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Role identifier of the portal Administrators security role; <c>null</c> when unassigned.</summary>
    public int? AdministratorRoleId { get; set; }

    /// <summary>Name of the portal Administrators security role.</summary>
    public string? AdministratorRoleName { get; set; }

    // MIGRATION: schema column [RegisteredRoleId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Role identifier of the Registered Users security role; <c>null</c> when unassigned.</summary>
    public int? RegisteredRoleId { get; set; }

    /// <summary>Name of the Registered Users security role.</summary>
    public string? RegisteredRoleName { get; set; }

    /// <summary>Portal description (used for the HTML meta description).</summary>
    public string? Description { get; set; }

    /// <summary>Portal meta keywords.</summary>
    public string? KeyWords { get; set; }

    /// <summary>Relative path/filename of the portal background image.</summary>
    public string? BackgroundFile { get; set; }

    // MIGRATION: name preserved verbatim as 'GUID' (legacy was <XmlIgnore> but is a persisted column).
    /// <summary>Globally unique identifier for the portal.</summary>
    public Guid GUID { get; set; }

    /// <summary>Name of the configured payment processor.</summary>
    public string? PaymentProcessor { get; set; }

    /// <summary>Credential/password for the configured payment processor.</summary>
    public string? ProcessorPassword { get; set; }

    /// <summary>User/account identifier for the configured payment processor.</summary>
    public string? ProcessorUserId { get; set; }

    // MIGRATION: schema column [SiteLogHistory] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Number of days of site-log history retained for the portal; <c>null</c> when unset.</summary>
    public int? SiteLogHistory { get; set; }

    /// <summary>Administrator/contact email address for the portal.</summary>
    public string? Email { get; set; }

    // MIGRATION: schema column [AdminTabId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Tab (page) identifier of the portal Admin page; <c>null</c> when unset.</summary>
    public int? AdminTabId { get; set; }

    /// <summary>Tab (page) identifier of the Host/SuperUser page.</summary>
    public int SuperTabId { get; set; }

    // MIGRATION: lazy-loaded count (legacy default Null.NullInteger; computed on demand via
    // UserController.GetUserCountByPortal). The lazy compute is dropped — now a plain nullable
    // property populated by the service/repository layer. null => not yet computed.
    /// <summary>Total number of users in the portal; null when the metric has not been computed.</summary>
    public int? Users { get; set; }

    // MIGRATION: lazy-loaded count (legacy default Null.NullInteger; computed on demand via
    // TabController.GetTabCount). The lazy compute is dropped — now a plain nullable property
    // populated by the service/repository layer. null => not yet computed.
    /// <summary>Total number of pages (tabs) in the portal; null when the metric has not been computed.</summary>
    public int? Pages { get; set; }

    // MIGRATION: schema column [SplashTabId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Tab (page) identifier of the portal splash page; <c>null</c> when unset.</summary>
    public int? SplashTabId { get; set; }

    // MIGRATION: schema column [HomeTabId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Tab (page) identifier of the portal home page; <c>null</c> when unset.</summary>
    public int? HomeTabId { get; set; }

    // MIGRATION: schema column [LoginTabId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Tab (page) identifier of the portal login page; <c>null</c> when unset.</summary>
    public int? LoginTabId { get; set; }

    // MIGRATION: schema column [UserTabId] int NULL -> nullable int? (Null.NullInteger -> null).
    /// <summary>Tab (page) identifier of the portal user-account page; <c>null</c> when unset.</summary>
    public int? UserTabId { get; set; }

    /// <summary>Default culture/language code for the portal.</summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>Time-zone offset (in minutes) applied to the portal.</summary>
    public int TimeZoneOffset { get; set; }

    /// <summary>Relative home directory path that holds the portal's files.</summary>
    public string? HomeDirectory { get; set; }

    /// <summary>DotNetNuke framework/portal version string.</summary>
    public string? Version { get; set; }
}
