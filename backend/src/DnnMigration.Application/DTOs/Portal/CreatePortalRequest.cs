namespace DnnMigration.Application.DTOs.Portal;

// MIGRATION: Inbound request DTO for POST /api/portals (Gate 5: POST -> 201). Carries the user-editable portal
// configuration captured by the legacy portal-creation flow (PortalController.CreatePortal, PortalController.vb L980)
// and the SiteSettings editor. EXCLUDES: the server-generated PortalId and Guid; and template/child-portal/filesystem
// provisioning internals (TemplatePath/TemplateFile/PortalAlias/ServerPath/ChildPath/IsChildPortal, out of scope).
// MIGRATION (CP1 review PortalService #3 — admin-user bootstrap): the legacy CreatePortal ALWAYS created the portal
// Administrator (FirstName/LastName/Username/Password/Email were REQUIRED parameters; L998-L1019) and set
// portal.AdministratorId to the new user's id. Those credentials are re-surfaced here as the OPTIONAL Admin* fields
// below so PortalService can perform the equivalent bootstrap (the credential ports IPasswordHasher/ICredentialStore
// now exist). They are optional at the DTO level (a portal MAY be created and have its admin provisioned later via the
// User API), but CreatePortalValidator enforces them as a REQUIRED GROUP whenever any one is supplied.
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

    // --- MIGRATION (CP1 review PortalService #3): OPTIONAL portal-administrator bootstrap fields ---
    // These mirror the legacy CreatePortal admin parameters (PortalController.vb L980/L1001-L1006). When AdminUsername
    // (the group trigger) is supplied, PortalService creates the portal Administrator with these values
    // (DisplayName = AdminFirstName + " " + AdminLastName, IsSuperUser=false, IsApproved=true), hashes AdminPassword
    // with BCrypt (IPasswordHasher) and persists it via ICredentialStore, then sets portal.AdministratorId to the new
    // user's id. CreatePortalValidator requires the whole group together when any is present.

    /// <summary>Optional portal-administrator login name. Acts as the bootstrap trigger; when set, the full admin group is required.</summary>
    public string? AdminUsername { get; set; }

    // MIGRATION (SECURITY, AAP 0.7.6): INBOUND-ONLY plaintext initial admin password. Hashed with BCrypt by
    // IPasswordHasher and persisted via ICredentialStore; NEVER stored in plaintext and NEVER echoed on any response.
    /// <summary>Optional portal-administrator initial password (inbound-only; BCrypt-hashed, never echoed).</summary>
    public string? AdminPassword { get; set; }

    /// <summary>Optional portal-administrator first name.</summary>
    public string? AdminFirstName { get; set; }

    /// <summary>Optional portal-administrator last name.</summary>
    public string? AdminLastName { get; set; }

    /// <summary>Optional portal-administrator email address.</summary>
    public string? AdminEmail { get; set; }
}
