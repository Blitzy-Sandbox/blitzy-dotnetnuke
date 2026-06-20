namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from UserInfo.vb (DotNetNuke.Entities.Users). Membership columns flattened from
// Membership/UserMembership.vb directly onto User (chosen for clean mapping to Users/Membership/Profile
// tables; documented per the Minimal Change Clause). IPropertyAccess (GetProperty/Cacheability) and the
// runtime ObjectHydrated flag are dropped (out of scope). FirstName/LastName delegate to Profile in legacy
// VB; here they are direct persisted properties. EF mapping via Fluent config in Infrastructure.
/// <summary>
/// Pure POCO User aggregate-root entity for the DotNetNuke 4.9.0.85 -> .NET 8 rewrite.
/// Represents an authenticated portal user together with the flattened membership columns
/// (credentials, lockout and activity tracking) originally split across the legacy
/// <c>UserInfo</c> and <c>UserMembership</c> business objects. This class carries data only;
/// authorization, hydration and token-replacement behavior live in the Application/Infrastructure layers.
/// </summary>
public class User
{
    // --- Core identity (UserInfo.vb) ---

    // MIGRATION: legacy default Null.NullInteger (-1); as the primary key it stays a non-nullable int.
    /// <summary>Primary key. Unique identifier of the user.</summary>
    public int UserID { get; set; }

    // MIGRATION: core FK — every user belongs to a portal; non-nullable int, consistent with all other entities.
    /// <summary>Identifier of the portal this user belongs to.</summary>
    public int PortalID { get; set; }

    // MIGRATION: genuinely optional; legacy default Null.NullInteger (-1) -> nullable int.
    /// <summary>Optional affiliate identifier associated with the user's registration.</summary>
    public int? AffiliateID { get; set; }

    /// <summary>Login name of the user.</summary>
    public string? Username { get; set; }

    /// <summary>Friendly display name shown in the UI.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Email address of the user.</summary>
    public string? Email { get; set; }

    // MIGRATION: legacy UserInfo.FirstName delegates to Profile.FirstName; modeled here as a direct property.
    /// <summary>Given name of the user.</summary>
    public string? FirstName { get; set; }

    // MIGRATION: legacy UserInfo.LastName delegates to Profile.LastName; modeled here as a direct property.
    /// <summary>Family name of the user.</summary>
    public string? LastName { get; set; }

    // MIGRATION: legacy default Null.NullBoolean (= False).
    /// <summary>Indicates whether the user is a host/super user with cross-portal privileges.</summary>
    public bool IsSuperUser { get; set; }

    // MIGRATION: legacy UserInfo.FullName computed = FirstName & " " & LastName.
    /// <summary>Read-only computed full name composed from <see cref="FirstName"/> and <see cref="LastName"/>.</summary>
    public string FullName => $"{FirstName} {LastName}";

    // MIGRATION: legacy UserInfo.Roles As String() preserved as denormalized role-name array (public-contract
    // preservation). NOT a persisted column; relational join is the UserRole entity. EF config must .Ignore() this.
    /// <summary>
    /// Denormalized array of role names for this user, preserved from the legacy public contract.
    /// This is not a database column — the relational association is modeled by the UserRole join entity —
    /// so the Infrastructure EF configuration ignores this member.
    /// </summary>
    public string[]? Roles { get; set; }

    // --- Membership (flattened from Membership/UserMembership.vb) ---

    // MIGRATION: legacy default True.
    /// <summary>Indicates whether the user account is approved/active.</summary>
    public bool Approved { get; set; } = true;

    /// <summary>UTC date the account was created.</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>Indicates whether the user is currently flagged as online.</summary>
    public bool IsOnLine { get; set; }

    /// <summary>Date of the user's last recorded activity.</summary>
    public DateTime? LastActivityDate { get; set; }

    /// <summary>Date the account was last locked out.</summary>
    public DateTime? LastLockoutDate { get; set; }

    /// <summary>Date of the user's last successful login.</summary>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>Date the user last changed their password.</summary>
    public DateTime? LastPasswordChangeDate { get; set; }

    // MIGRATION: legacy default False -> plain bool.
    /// <summary>Indicates whether the account is currently locked out.</summary>
    public bool LockedOut { get; set; }

    /// <summary>Stored password value (hashing handled by the Infrastructure PasswordHasher; BCrypt in the new stack).</summary>
    public string? Password { get; set; }

    /// <summary>Answer to the password-recovery security question.</summary>
    public string? PasswordAnswer { get; set; }

    /// <summary>Password-recovery security question.</summary>
    public string? PasswordQuestion { get; set; }

    /// <summary>Flag indicating the user must update their password on next login.</summary>
    public bool UpdatePassword { get; set; }
}
