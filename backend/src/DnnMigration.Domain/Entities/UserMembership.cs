namespace DnnMigration.Domain.Entities;

/// <summary>
/// User membership / credential state. Composed by <see cref="User"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserMembership
// (Library/Components/Users/Membership/UserMembership.vb, L41). VB `Date` -> C# `DateTime`.
// Downstream EF Core (DnnMigration.Infrastructure) maps this to the existing aspnet_Membership table.
public class UserMembership
{
    public bool Approved { get; set; }
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
