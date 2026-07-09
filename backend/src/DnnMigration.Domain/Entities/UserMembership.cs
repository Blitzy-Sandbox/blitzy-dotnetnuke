namespace DnnMigration.Domain.Entities;

/// <summary>
/// User membership / credential state. Composed by the <c>User</c> entity (as <c>User.Membership</c>).
/// </summary>
// NOTE: The composition target is referenced as inline code (&lt;c&gt;User&lt;/c&gt;) rather than a
// &lt;see cref="User"/&gt; link. The dependency-free Domain layer is compiled with
// GenerateDocumentationFile under the Gate 1 warnings-as-errors build, so an unresolved cref
// (CS1574) would fail the build while the sibling User entity is not yet part of this
// compilation (it arrives in a later checkpoint). This mirrors the UserProfile.cs convention.
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserMembership
// (Library/Components/Users/Membership/UserMembership.vb, L41). VB `Date` -> C# `DateTime`.
// Downstream EF Core (DnnMigration.Infrastructure) maps this to the existing aspnet_Membership table.
//
// MIGRATION (SCHEMA FIDELITY — finding #1): this class plays two roles. (1) As the transient
// composition carrier on User.Membership (an in-memory value object; the EF User mapping Ignore()s the
// navigation because the int-keyed [Users] row cannot own the uniqueidentifier-keyed
// [aspnet_Membership] row). (2) As a STANDALONE EF entity mapped to the GUID-keyed [aspnet_Membership]
// table (UserMembershipConfiguration), reached by UserRepository via a deterministic UserId projection.
// The properties below now include the REQUIRED NOT NULL [aspnet_Membership] columns that the previous
// owned-type shape omitted (ApplicationId, PasswordFormat, PasswordSalt, the four failed-attempt
// fields) so a row can actually be inserted into the existing table.
public class UserMembership
{
    // MIGRATION (SCHEMA FIDELITY — finding #1): the [aspnet_Membership] primary key is a
    // uniqueidentifier [UserId] (FK -> aspnet_Users), NOT the DNN integer UserID. This Guid key is the
    // one physically on the table; UserRepository supplies it as a deterministic projection of the DNN
    // integer UserID so the credential row round-trips without an EF int-owned relationship and without
    // inventing any column. Mapped to column [UserId] by UserMembershipConfiguration.
    public Guid MembershipUserId { get; set; }

    // MIGRATION (SCHEMA FIDELITY — finding #1): required NOT NULL [aspnet_Membership] columns that the
    // legacy ASP.NET membership provider populated. Modelling aspnet_Membership as an int-owned type
    // previously omitted these, which would fail an INSERT against the real (NOT NULL) schema.
    public Guid ApplicationId { get; set; }
    public int PasswordFormat { get; set; }
    public string PasswordSalt { get; set; } = string.Empty;
    public int FailedPasswordAttemptCount { get; set; }
    public DateTime FailedPasswordAttemptWindowStart { get; set; }
    public int FailedPasswordAnswerAttemptCount { get; set; }
    public DateTime FailedPasswordAnswerAttemptWindowStart { get; set; }

    // MIGRATION: legacy UserMembership.vb initializes `_Approved As Boolean = True` (L45), so a
    // newly-constructed membership is Approved by default. Preserve that legacy default here
    // (an uninitialized C# bool would default to false and silently change user
    // creation/approval workflow behavior).
    public bool Approved { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public bool IsOnLine { get; set; }
    public DateTime LastActivityDate { get; set; }
    public DateTime LastLockoutDate { get; set; }
    public DateTime LastLoginDate { get; set; }
    public DateTime LastPasswordChangeDate { get; set; }
    public bool LockedOut { get; set; }
    // MIGRATION: legacy progressive-hydration flag; retained as plain state (no lazy-load logic).
    public bool ObjectHydrated { get; set; }
    public string Password { get; set; } = string.Empty;
    public string PasswordAnswer { get; set; } = string.Empty;
    public string PasswordQuestion { get; set; } = string.Empty;
    public bool UpdatePassword { get; set; }
    public string Email { get; set; } = string.Empty;

    // MIGRATION: optional [aspnet_Membership] columns (nullable in the existing schema).
    public string? LoweredEmail { get; set; }
    public string? MobilePIN { get; set; }
    public string? Comment { get; set; }

    // MIGRATION: [UserName] and [LastActivityDate] physically live on aspnet_Users, not
    // aspnet_Membership; Username is retained here only as a transient carrier for the composition.
    public string Username { get; set; } = string.Empty;
}
