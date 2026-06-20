using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the User aggregate (record management only; authentication
/// lives in IAuthService). MIGRATION: ported from the public business surface of UserController.vb,
/// re-expressed as async DTO-based operations that parallel IUserRepository. User is soft-deleted.
/// Implemented by Application/Services/UserService.cs.
/// </summary>
public interface IUserService
{
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    Task<UserDto?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default);

    Task<PagedResult<UserDto>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(UpdateUserDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int userId, CancellationToken cancellationToken = default);
}
