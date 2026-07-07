using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for user management, exposing the CRUD and change-password
/// surface consumed by the <c>/api/users</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke UserController.vb. The legacy
// Public Shared (static) members (e.g. GetUser L497, GetUsers L685, DeleteUser L200) become
// DI-registered instance methods (AAP static->instance rule). DTO-only, async.
public interface IUserService
{
    /// <summary>Returns all users.</summary>
    Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all users belonging to the specified portal.</summary>
    // MIGRATION: legacy UserController.GetUsers(portalId).
    Task<IEnumerable<UserDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user with the given id, or <c>null</c> if not found.</summary>
    Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns a user within a portal by username, or <c>null</c> if not found.</summary>
    // MIGRATION: legacy UserController.GetUserByName(portalId, username).
    Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>Creates a new user and returns the created projection.</summary>
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the user with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Changes a user's password; returns <c>true</c> on success.</summary>
    // MIGRATION: legacy UserController.ChangePassword(user, oldPassword, newPassword) [L103]; the new
    // hash is produced by the IPasswordHasher port (BCrypt) in the implementation.
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
