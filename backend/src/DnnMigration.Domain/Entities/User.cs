namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserInfo (Library/Components/Users/UserInfo.vb),
// with non-credential account-status fields flattened from UserMembership.vb. Renamed UserInfo -> User;
// XML/UI attributes and IPropertyAccess removed; persistence-ignorant POCO. Portal-scoped (multi-tenant).
// MIGRATION (SECURITY, AAP 0.7.6): NO credential material is stored on this entity. Password, PasswordAnswer,
// PasswordQuestion, UpdatePassword, and password-change timestamps are handled exclusively by the Infrastructure
// Identity layer (BCrypt), not the Domain model.
// MIGRATION: Legacy Membership/Profile objects (lazy-hydrated) dropped; FirstName/LastName promoted from
// Profile delegates to plain properties; FullName's computed getter dropped (Application composes display names);
// the Email setter side-effect (Me.Membership.Email = Value) removed; the Roles String() lazy-load replaced by
// the UserRoles navigation; and the IsInRole/UpdateDisplayName methods moved to the Application layer.
public class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    // MIGRATION: Promoted from legacy Profile.FirstName delegate to a plain property.
    public string? FirstName { get; set; }

    // MIGRATION: Promoted from legacy Profile.LastName delegate to a plain property.
    public string? LastName { get; set; }

    // MIGRATION: Legacy FullName had a computed getter (FirstName & " " & LastName when empty). Now a plain property.
    public string? FullName { get; set; }

    public bool IsSuperUser { get; set; }

    // MIGRATION: Legacy AffiliateID initialized to Null.NullInteger (-1) -> nullable int.
    public int? AffiliateId { get; set; }

    // MIGRATION: Multi-tenant discriminator (a user belongs to a portal).
    public int PortalId { get; set; }

    // --- Account status flattened from UserMembership.vb (NON-credential only) ---

    // MIGRATION: Legacy UserMembership.Approved.
    public bool IsApproved { get; set; } = true;

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime? LastActivityDate { get; set; }

    public DateTime? LastLockoutDate { get; set; }

    // MIGRATION: Legacy UserMembership.LockedOut.
    public bool LockedOut { get; set; }

    // MIGRATION: Replaces the legacy Roles String() lazy-load. User <-> Role association via the UserRole join entity.
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
