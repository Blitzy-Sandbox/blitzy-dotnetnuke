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
public class UserMembership
{
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
    public string Username { get; set; } = string.Empty;
}
