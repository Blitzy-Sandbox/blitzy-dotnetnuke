namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Portals.PortalInfo (Library/Components/Portal/PortalInfo.vb).
// Renamed PortalInfo -> Portal; <XmlRoot>/<XmlElement>/<XmlIgnore> attributes removed; persistence-ignorant POCO.
// Portal is the multi-tenant root entity (PortalId is the tenant key referenced by all portal-scoped entities).
public class Portal
{
    public int PortalId { get; set; }

    public string PortalName { get; set; } = string.Empty;

    public string? LogoFile { get; set; }

    public string? FooterText { get; set; }

    public DateTime? ExpiryDate { get; set; }

    // MIGRATION: Legacy UserRegistration registration-type code retained as int (no enum introduced in this phase).
    public int UserRegistration { get; set; }

    // MIGRATION: Legacy BannerAdvertising banner-type code retained as int (no enum introduced in this phase).
    public int BannerAdvertising { get; set; }

    public int AdministratorId { get; set; }

    public string? Currency { get; set; }

    // MIGRATION: VB Single -> C# float.
    public float HostFee { get; set; }

    public int HostSpace { get; set; }

    public int PageQuota { get; set; }

    public int UserQuota { get; set; }

    public int AdministratorRoleId { get; set; }

    public string? AdministratorRoleName { get; set; }

    public int RegisteredRoleId { get; set; }

    public string? RegisteredRoleName { get; set; }

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    public string? BackgroundFile { get; set; }

    // MIGRATION: Legacy GUID property (System.Guid). DB column GUID mapped via Fluent API.
    public Guid Guid { get; set; }

    public string? PaymentProcessor { get; set; }

    public string? ProcessorPassword { get; set; }

    public string? ProcessorUserId { get; set; }

    public int SiteLogHistory { get; set; }

    public string? Email { get; set; }

    public int AdminTabId { get; set; }

    public int SuperTabId { get; set; }

    // MIGRATION: Legacy Users property had a lazy side-effect getter (UserController.GetUserCountByPortal).
    // Side effect DROPPED — now a plain nullable count populated by the Application/Infrastructure layer.
    public int? Users { get; set; }

    // MIGRATION: Legacy Pages property had a lazy side-effect getter (TabController.GetTabCount).
    // Side effect DROPPED — now a plain nullable count populated by the Application/Infrastructure layer.
    public int? Pages { get; set; }

    public int SplashTabId { get; set; }

    public int HomeTabId { get; set; }

    public int LoginTabId { get; set; }

    public int UserTabId { get; set; }

    public string? DefaultLanguage { get; set; }

    public int TimeZoneOffset { get; set; }

    public string? HomeDirectory { get; set; }

    public string? Version { get; set; }

    // MIGRATION: Legacy read-only HomeDirectoryMapPath (computed via Services.FileSystem.FolderController)
    // DROPPED entirely — filesystem/presentation concern, not domain data.
}
