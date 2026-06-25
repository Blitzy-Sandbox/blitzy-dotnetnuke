namespace DnnMigration.Application.DTOs.Portal;

// MIGRATION: Inbound request DTO for PUT /api/portals/{id} (Gate 5: PUT -> 200). Field set mirrors the legacy
// editable-settings contract PortalController.UpdatePortalInfo (PortalController.vb L1568) as driven by the
// SiteSettings.ascx.vb settings editor (27 fields). Mutable class; validation lives in the sibling
// Validators/UpdatePortalValidator, not here.
// MIGRATION: PortalId is present (multi-tenant tenant-root identity, AAP 0.7.1) but is route-authoritative
// (/api/portals/{id}); the service treats the route id as canonical.
// MIGRATION (SECURITY): ProcessorPassword is accepted as a WRITE-ONLY inbound field (the legacy settings form
// allowed setting it) but is NEVER returned by the read DTO (PortalDto omits it).
// MIGRATION: Email is intentionally absent — the legacy UpdatePortalInfo signature does not update Email.
public class UpdatePortalRequest
{
    public int PortalId { get; set; }

    public string PortalName { get; set; } = string.Empty;

    public string? LogoFile { get; set; }

    public string? FooterText { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public int UserRegistration { get; set; }

    public int BannerAdvertising { get; set; }

    public string? Currency { get; set; }

    public int AdministratorId { get; set; }

    public float HostFee { get; set; }

    public int HostSpace { get; set; }

    public int PageQuota { get; set; }

    public int UserQuota { get; set; }

    public string? PaymentProcessor { get; set; }

    public string? ProcessorUserId { get; set; }

    // MIGRATION (SECURITY): write-only inbound payment-processor secret; never echoed back in responses.
    public string? ProcessorPassword { get; set; }

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    public string? BackgroundFile { get; set; }

    public int SiteLogHistory { get; set; }

    public int SplashTabId { get; set; }

    public int HomeTabId { get; set; }

    public int LoginTabId { get; set; }

    public int UserTabId { get; set; }

    public string? DefaultLanguage { get; set; }

    public int TimeZoneOffset { get; set; }

    public string? HomeDirectory { get; set; }
}
