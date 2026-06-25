using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for user management. Consumed by UsersController via constructor
/// injection. The implementation orchestrates the user/role repositories + unit of work,
/// projects Domain entities to DTOs, and auto-assigns default roles on creation.
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Users/UserController.vb
// (CreateUser L156, UpdateUser L963, DeleteUser L1292, GetUser L1245, GetUsers L725). Business logic moves to
// UserService; data access to IUserRepository. DTO-only contract — no raw Domain entities exposed (AAP 0.7.7).
// NOTE: profile editing (UserProfileDto) is a separate feature endpoint and is intentionally NOT part of this
// CRUD + by-portal contract.
public interface IUserService
{
    // MIGRATION: Legacy GetUsers(portalId, pageIndex, pageSize, ByRef total) (UserController.vb L725) — scoped by
    // portalId (multi-tenant isolation, AAP 0.7.1) and paged (PagedResult<UserResponse>).
    Task<Result<PagedResult<UserResponse>>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetUser(portalId, userId) (UserController.vb L1245). Single key (userId) in the contract;
    // per-entity tenant validation is enforced in the service implementation.
    Task<Result<UserResponse>> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy CreateUser (UserController.vb L156). POST /api/users -> 201 (Gate 5). The implementation
    // auto-assigns AutoAssignment roles after insert (legacy CreateUser behavior) — an impl detail, not a contract change.
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateUser (UserController.vb L963). userId route-bound; PUT -> 200 (Gate 5).
    Task<Result<UserResponse>> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteUser(PortalId, UserId) (UserController.vb L1292). DELETE -> 204. Non-generic Result.
    Task<Result> DeleteAsync(int userId, CancellationToken cancellationToken = default);
}
