namespace DnnMigration.Domain.Enums;

/// <summary>
/// Controls who may view a user profile property value.
/// </summary>
// MIGRATION: Converted verbatim from VB.NET DotNetNuke.Entities.Users.UserVisibilityMode
// (Library/Components/Users/UserVisibilityMode.vb). The integer values are preserved exactly
// so the persistence/wire contract for profile-property visibility is unchanged.
public enum UserVisibilityMode
{
    /// <summary>Visible to all users.</summary>
    AllUsers = 0,

    /// <summary>Visible to authenticated members only.</summary>
    MembersOnly = 1,

    /// <summary>Visible to administrators only.</summary>
    AdminOnly = 2
}
