namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Inbound request DTO for updating an existing user (PUT /api/users/{id}). Derived from the legacy
// admin edit-user workflow (Website/admin/Users/User.ascx.vb -> UserController.UpdateUser). Mutable class.
// UserId is route-bound and intentionally NOT a body field. Username is the immutable login identity
// (legacy UserInfo.Username was <IsReadOnly(True)>) and is NOT editable here.
// MIGRATION (SECURITY, AAP 0.7.6): NO password field — password changes are a separate secured flow
// (Auth/Password endpoints), never part of the user-update payload.
public class UpdateUserRequest
{
    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    // MIGRATION: From the legacy admin "Authorize" checkbox (chkAuthorize -> UserMembership.Approved). Admin-editable, non-credential.
    public bool IsApproved { get; set; }

    // MIGRATION: From legacy UserMembership.LockedOut. Admin-editable (supports the legacy "unlock user" action). Non-credential.
    public bool LockedOut { get; set; }
}
