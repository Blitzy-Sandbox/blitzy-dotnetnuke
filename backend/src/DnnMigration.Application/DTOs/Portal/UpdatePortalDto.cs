// MIGRATION: Update request DTO for a Portal. Includes PortalID; omits server-derived metrics (Users, Pages).
// MIGRATION: Projected from Library/Components/Portal/PortalInfo.vb mutable fields + identity PortalID.
// MIGRATION: Single (HostFee) -> float; Date (ExpiryDate) -> DateTime?.
// MIGRATION (INTEGRITY): GUID is DELIBERATELY EXCLUDED from the update surface. The [Portals].GUID
//   uniqueidentifier is an identity-like value with a `newid()` default; it is retained server-side and is
//   never mutated by the client. PortalProfile.ForMember(GUID, Ignore()) enforces this on the mapping path.
//   Recorded in MIGRATION_NOTES.md.
// MIGRATION: Nullable physical FK/tab-id columns (AdministratorId, AdministratorRoleId, RegisteredRoleId,
//   SiteLogHistory, AdminTabId, SplashTabId, HomeTabId, LoginTabId, UserTabId) are int? so an optional/unset
//   value round-trips as null rather than being coerced to 0.
// MIGRATION (SECURITY): ProcessorPassword is INPUT-ONLY here (write-only); it is never returned by PortalDto.

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Update request DTO for a Portal. Includes PortalID; omits the server-managed GUID and server-derived
/// metrics (Users, Pages). <c>ProcessorPassword</c> is input-only (never echoed on read).
/// </summary>
public class UpdatePortalDto
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
    public string? PaymentProcessor { get; set; }
    public string? ProcessorPassword { get; set; }
    public string? ProcessorUserId { get; set; }
    public int? SiteLogHistory { get; set; }
    public string? Email { get; set; }
    public int? AdminTabId { get; set; }
    public int SuperTabId { get; set; }
    public int? SplashTabId { get; set; }
    public int? HomeTabId { get; set; }
    public int? LoginTabId { get; set; }
    public int? UserTabId { get; set; }
    public string? DefaultLanguage { get; set; }
    public int TimeZoneOffset { get; set; }
    public string? HomeDirectory { get; set; }
    public string? Version { get; set; }
}
