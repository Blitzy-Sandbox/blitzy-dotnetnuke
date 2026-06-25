namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Inbound request DTO for creating a user (POST /api/users). Derived from the legacy admin
// create-user workflow (Website/admin/Users/User.ascx.vb -> UserController.CreateUser) and the UserInfo
// editable scalar fields. Mutable class. Requiredness (PortalId, Username) and Password==Confirm matching are
// enforced by CreateUserValidator (FluentValidation, Application/Validators) — this remains a plain shape.
public class CreateUserRequest
{
    // MIGRATION: Multi-tenant discriminator (AAP 0.7.1) — the portal the new user is created under. Required (validator-enforced).
    public int PortalId { get; set; }

    // MIGRATION: Required login identity (validator-enforced).
    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    // MIGRATION (SECURITY, AAP 0.7.6): INBOUND-ONLY plaintext initial password. The Application service hands this
    // to Infrastructure/Identity for BCrypt hashing; it is NEVER persisted in plaintext, NEVER echoed on any
    // response, and NEVER present on UpdateUserRequest. Mirrors the Auth login plaintext-inbound pattern.
    public string? Password { get; set; }

    // MIGRATION (SECURITY): INBOUND-ONLY password confirmation (legacy txtConfirm). Used only by CreateUserValidator
    // to verify Password == Confirm; never persisted, never echoed.
    public string? Confirm { get; set; }
}
