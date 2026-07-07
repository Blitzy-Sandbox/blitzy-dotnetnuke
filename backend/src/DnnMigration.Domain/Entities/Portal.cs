using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Portal (site) settings entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Portals.PortalInfo
// (Library/Components/Portal/PortalInfo.vb, L28, XmlRoot "settings"). VB `Single` -> `float`,
// `Date` -> `DateTime`, `Guid` -> System.Guid. The integer UserRegistration/BannerAdvertising
// fields are strongly typed to Domain enums. Lazy-load logic in the legacy Users/Pages getters
// (UserController.GetUserCountByPortal / TabController.GetTabCount) is dropped — plain state now.
public class Portal
{
    public int PortalID { get; set; }
    public string PortalName { get; set; } = string.Empty;
    public string LogoFile { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }

    // MIGRATION: legacy Integer "userregistration"; strongly typed to UserRegistrationType enum.
    public UserRegistrationType UserRegistration { get; set; }

    // MIGRATION: legacy Integer "banneradvertising"; strongly typed to BannerType enum.
    public BannerType BannerAdvertising { get; set; }

    public int AdministratorId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public float HostFee { get; set; }            // MIGRATION: VB Single -> float
    public int HostSpace { get; set; }
    public int PageQuota { get; set; }
    public int UserQuota { get; set; }
    public int AdministratorRoleId { get; set; }
    public string AdministratorRoleName { get; set; } = string.Empty;
    public int RegisteredRoleId { get; set; }
    public string RegisteredRoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string KeyWords { get; set; } = string.Empty;
    public string BackgroundFile { get; set; } = string.Empty;
    public Guid GUID { get; set; }
    public string PaymentProcessor { get; set; } = string.Empty;
    public string ProcessorPassword { get; set; } = string.Empty;
    public string ProcessorUserId { get; set; } = string.Empty;
    public int SiteLogHistory { get; set; }
    public string Email { get; set; } = string.Empty;
    public int AdminTabId { get; set; }
    public int SuperTabId { get; set; }
    public int Users { get; set; }                // MIGRATION: legacy lazy-count getter dropped
    public int Pages { get; set; }                // MIGRATION: legacy lazy-count getter dropped
    public int SplashTabId { get; set; }
    public int HomeTabId { get; set; }
    public int LoginTabId { get; set; }
    public int UserTabId { get; set; }
    public string DefaultLanguage { get; set; } = string.Empty;
    public int TimeZoneOffset { get; set; }
    public string HomeDirectory { get; set; } = string.Empty;

    // MIGRATION: legacy ReadOnly computed via FolderController.GetMappedDirectory(ApplicationPath +
    // "/" + HomeDirectory + "/"); the DNN FileSystem/Globals infrastructure is out of scope, so this
    // is simplified to expose the relative home directory path.
    public string HomeDirectoryMapPath => HomeDirectory;

    public string Version { get; set; } = string.Empty;
}
