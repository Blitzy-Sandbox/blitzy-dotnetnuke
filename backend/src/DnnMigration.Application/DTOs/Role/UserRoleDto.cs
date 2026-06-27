namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Read/return shape for a user-to-role assignment, backing the role-assignment
/// workflow. Projected from the <c>DnnMigration.Domain.Entities.UserRole</c> join
/// entity with its User and Role navigations flattened to identifiers and names.
/// </summary>
// MIGRATION: Source = Library/Components/Users/UserRoleInfo.vb (join, Inherits RoleInfo) +
//            Website/admin/Security/SecurityRoles.ascx.vb (grdUserRoles assignment grid).
// MIGRATION: Navigations flattened — UserRole.Role -> RoleName, UserRole.User -> Username/DisplayName.
//            Raw Domain.Entities navigations (User?/Role?) are never exposed (no raw entities).
// MIGRATION: Legacy UserRoleInfo.FullName/.Email are not surfaced (already dropped from the UserRole entity);
//            DisplayName carries the user label shown by SecurityRoles.FormatUser(UserID, DisplayName).
// MIGRATION (CP-final review): assignment WRITE is NOW IMPLEMENTED - IRoleRepository exposes
//            GetUserRoleAsync/AddUserRoleAsync/UpdateUserRoleAsync/RemoveUserRoleAsync, surfaced via
//            IRoleService.AssignUserRoleAsync/RemoveUserRoleAsync/UpdateUserRoleAsync; this DTO is the read/return shape.
public record UserRoleDto
{
    public int UserRoleId { get; init; }

    public int UserId { get; init; }

    public int RoleId { get; init; }

    // MIGRATION: Flattened from UserRole.Role navigation (was RoleInfo.RoleName on the inherited base).
    public string? RoleName { get; init; }

    // MIGRATION: Flattened from UserRole.User navigation.
    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public DateTime? EffectiveDate { get; init; }

    public DateTime? ExpiryDate { get; init; }

    public bool IsTrialUsed { get; init; }

    public bool Subscribed { get; init; }
}
