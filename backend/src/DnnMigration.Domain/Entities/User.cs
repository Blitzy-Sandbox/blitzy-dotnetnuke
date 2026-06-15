namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from UserInfo.vb (DotNetNuke.Entities.Users) and Membership/UserMembership.vb.
// The legacy "UserInfo + UserMembership + UserProfile" object graph is FLATTENED: the key persisted
// membership columns from UserMembership.vb live directly on this aggregate root, and FirstName/LastName
// (which delegate to UserProfile in legacy VB) are modeled as direct persisted properties here. This
// flatten is the deliberate design choice recorded under the Minimal Change Clause (the AAP enumerates
// only a single User.cs entity — no separate Membership/Profile entities). The legacy
// IPropertyAccess implementation (GetProperty/Cacheability/isAdminUser — a token-engine concern) and the
// runtime ObjectHydrated bookkeeping flag are intentionally dropped as out of scope. The behavioral
// IsInRole / UpdateDisplayName helpers are likewise omitted — this is a pure POCO data carrier with zero
// framework dependencies. Relational mapping (table/column names, the .Ignore() of the Roles array, and
// the user<->role join) is handled by Fluent IEntityTypeConfiguration in the Infrastructure layer.
public class User
{
    // --- Core identity (from UserInfo.vb) ---

    // MIGRATION: legacy UserInfo._UserID default Null.NullInteger (-1); the primary key stays a
    // non-nullable int (the sentinel becomes the EF/identity default of 0 for an unsaved entity).
    public int UserID { get; set; }

    // MIGRATION: legacy UserInfo._PortalID default Null.NullInteger (-1); every user belongs to a portal,
    // so the core FK stays a non-nullable int, consistent with all other entities in the domain.
    public int PortalID { get; set; }

    // MIGRATION: legacy UserInfo._AffiliateID default Null.NullInteger (-1) is genuinely optional ->
    // mapped to a nullable int? rather than a sentinel value.
    public int? AffiliateID { get; set; }

    // MIGRATION: legacy UserInfo.Username (Required, IsReadOnly in VB). Nullable string in the POCO;
    // required/length constraints are enforced via FluentValidation in the Application layer, not here.
    public string? Username { get; set; }

    // MIGRATION: legacy UserInfo.DisplayName (Required, MaxLength 128 in VB). Constraints live in
    // validators/EF configuration, keeping the Domain entity attribute-free.
    public string? DisplayName { get; set; }

    // MIGRATION: legacy UserInfo.Email (Required, MaxLength 256, email regex in VB). The legacy setter
    // also mirrored the value onto Membership.Email; that duplication is removed under the flatten.
    public string? Email { get; set; }

    // MIGRATION: legacy UserInfo.FirstName delegated to Profile.FirstName; modeled here as a direct
    // persisted property per the flatten decision.
    public string? FirstName { get; set; }

    // MIGRATION: legacy UserInfo.LastName delegated to Profile.LastName; modeled here as a direct
    // persisted property per the flatten decision.
    public string? LastName { get; set; }

    // MIGRATION: legacy UserInfo._IsSuperUser default Null.NullBoolean (False) -> plain non-nullable bool
    // defaulting to false.
    public bool IsSuperUser { get; set; }

    // --- Computed (read-only) ---

    // MIGRATION: legacy UserInfo.FullName (deprecated) returned FirstName & " " & LastName. Preserved as a
    // read-only, expression-bodied computed property (no setter) to retain the public-contract semantics.
    public string FullName => $"{FirstName} {LastName}";

    // --- Roles (legacy public contract preserved) ---

    // MIGRATION: legacy UserInfo.Roles As String() is preserved as a denormalized role-name array for
    // public-contract preservation. This is NOT a persisted column — the relational user<->role
    // association is the UserRole join entity — so the Infrastructure EF configuration must .Ignore(u => u.Roles).
    public string[]? Roles { get; set; }

    // --- Membership (flattened from Membership/UserMembership.vb) ---

    // MIGRATION: legacy UserMembership._Approved default True.
    public bool Approved { get; set; } = true;

    // MIGRATION: legacy UserMembership.CreatedDate (As Date, read-only in VB) -> nullable DateTime?.
    public DateTime? CreatedDate { get; set; }

    // MIGRATION: legacy UserMembership.IsOnLine -> plain bool defaulting to false.
    public bool IsOnLine { get; set; }

    // MIGRATION: legacy UserMembership.LastActivityDate (As Date) -> nullable DateTime?.
    public DateTime? LastActivityDate { get; set; }

    // MIGRATION: legacy UserMembership.LastLockoutDate (As Date) -> nullable DateTime?.
    public DateTime? LastLockoutDate { get; set; }

    // MIGRATION: legacy UserMembership.LastLoginDate (As Date) -> nullable DateTime?.
    public DateTime? LastLoginDate { get; set; }

    // MIGRATION: legacy UserMembership.LastPasswordChangeDate (As Date) -> nullable DateTime?.
    public DateTime? LastPasswordChangeDate { get; set; }

    // MIGRATION: legacy UserMembership._LockedOut default False -> plain bool.
    public bool LockedOut { get; set; }

    // MIGRATION: legacy UserMembership.Password. Stored hashed by the Infrastructure PasswordHasher
    // (BCrypt) — the legacy DES Encrypt/Decrypt path is retired.
    public string? Password { get; set; }

    // MIGRATION: legacy UserMembership.PasswordAnswer.
    public string? PasswordAnswer { get; set; }

    // MIGRATION: legacy UserMembership.PasswordQuestion.
    public string? PasswordQuestion { get; set; }

    // MIGRATION: legacy UserMembership.UpdatePassword (force-change-on-next-login flag).
    public bool UpdatePassword { get; set; }
}
