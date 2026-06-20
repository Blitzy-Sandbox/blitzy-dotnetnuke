// MIGRATION: Create request DTO for a Portal. Omits server-assigned PortalID and server-derived metrics (Users, Pages).
// MIGRATION: Projected from Library/Components/Portal/PortalInfo.vb mutable fields.
// MIGRATION: Single (HostFee) -> float; Date (ExpiryDate) -> DateTime?.

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Create request DTO for a Portal. Omits server-assigned PortalID and server-derived metrics (Users, Pages).
/// </summary>
public class CreatePortalDto
{
    public string? PortalName { get; set; }
    public string? LogoFile { get; set; }
    public string? FooterText { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int UserRegistration { get; set; }
    public int BannerAdvertising { get; set; }
    public int AdministratorId { get; set; }
    public string? Currency { get; set; }
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
    public Guid GUID { get; set; }
    public string? PaymentProcessor { get; set; }
    public string? ProcessorPassword { get; set; }
    public string? ProcessorUserId { get; set; }
    public int SiteLogHistory { get; set; }
    public string? Email { get; set; }
    public int AdminTabId { get; set; }
    public int SuperTabId { get; set; }
    public int SplashTabId { get; set; }
    public int HomeTabId { get; set; }
    public int LoginTabId { get; set; }
    public int UserTabId { get; set; }
    public string? DefaultLanguage { get; set; }
    public int TimeZoneOffset { get; set; }
    public string? HomeDirectory { get; set; }
    public string? Version { get; set; }
}
