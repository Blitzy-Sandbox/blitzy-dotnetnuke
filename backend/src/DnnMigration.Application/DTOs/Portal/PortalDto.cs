// MIGRATION: Read projection of the Portal entity (legacy Library/Components/Portal/PortalInfo.vb). Pure POCO; no attributes/packages.
// MIGRATION: Legacy Single (HostFee) -> float; legacy Date (ExpiryDate) -> DateTime?.
// MIGRATION: Lazy Null.NullInteger metrics (Users, Pages) -> int? (server-derived, read-only).
// MIGRATION: UserRegistration & BannerAdvertising retained for full PortalInfo public-contract fidelity.
// MIGRATION: HomeDirectoryMapPath (XmlIgnore runtime helper) intentionally excluded (not persisted).
// MIGRATION: Property names/casing preserved verbatim so AutoMapper maps by-name with no config.

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Read projection of the Portal entity (legacy Library/Components/Portal/PortalInfo.vb). Pure POCO; no attributes/packages.
/// </summary>
public class PortalDto
{
    public int PortalID { get; set; }
    public string? PortalName { get; set; }
    public string? LogoFile { get; set; }
    public string? FooterText { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int UserRegistration { get; set; }
    public int BannerAdvertising { get; set; }
    public int? AdministratorId { get; set; }
    public string? Currency { get; set; }
    public float HostFee { get; set; }
    public int HostSpace { get; set; }
    public int PageQuota { get; set; }
    public int UserQuota { get; set; }
    public int? AdministratorRoleId { get; set; }
    public string? AdministratorRoleName { get; set; }
    public int? RegisteredRoleId { get; set; }
    public string? RegisteredRoleName { get; set; }
    public string? Description { get; set; }
    public string? KeyWords { get; set; }
    public string? BackgroundFile { get; set; }
    public Guid GUID { get; set; }
    public string? PaymentProcessor { get; set; }
    // MIGRATION/SECURITY: ProcessorPassword is INTENTIONALLY NOT exposed on this read projection.
    // It is a payment-processor credential; echoing it in GET responses/logs is sensitive-data
    // exposure. It remains a persisted [Portals] column and is accepted (write-only) via
    // CreatePortalDto/UpdatePortalDto over HTTPS, but is never returned by the API.
    public string? ProcessorUserId { get; set; }
    public int? SiteLogHistory { get; set; }
    public string? Email { get; set; }
    public int? AdminTabId { get; set; }
    public int SuperTabId { get; set; }
    public int? Users { get; set; }
    public int? Pages { get; set; }
    public int? SplashTabId { get; set; }
    public int? HomeTabId { get; set; }
    public int? LoginTabId { get; set; }
    public int? UserTabId { get; set; }
    public string? DefaultLanguage { get; set; }
    public int TimeZoneOffset { get; set; }
    public string? HomeDirectory { get; set; }
    public string? Version { get; set; }
}
