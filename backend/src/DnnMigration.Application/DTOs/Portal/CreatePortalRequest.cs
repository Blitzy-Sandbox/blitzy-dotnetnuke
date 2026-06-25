namespace DnnMigration.Application.DTOs.Portal;

// MIGRATION: Inbound request DTO for POST /api/portals (Gate 5: POST -> 201). Carries the user-editable portal
// configuration captured by the legacy portal-creation flow (PortalController.CreatePortal, PortalController.vb L980)
// and the SiteSettings editor. EXCLUDES: the server-generated PortalId and Guid; admin-user credentials
// (FirstName/LastName/Username/Password — these belong to the User/Auth flows); and template/child-portal/filesystem
// provisioning internals (TemplatePath/TemplateFile/PortalAlias/ServerPath/ChildPath/IsChildPortal, out of scope).
// Mutable class (forms-style); validation lives in the sibling Validators/CreatePortalValidator (FluentValidation), not here.
public class CreatePortalRequest
{
    public string PortalName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? KeyWords { get; set; }

    public string? LogoFile { get; set; }

    public string? FooterText { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public int UserRegistration { get; set; }

    public int BannerAdvertising { get; set; }

    public string? Currency { get; set; }

    public float HostFee { get; set; }

    public int HostSpace { get; set; }

    public int PageQuota { get; set; }

    public int UserQuota { get; set; }

    public string? Email { get; set; }

    public string? DefaultLanguage { get; set; }

    public int TimeZoneOffset { get; set; }

    public string? HomeDirectory { get; set; }
}
