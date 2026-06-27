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
// MIGRATION (CP-final review - profile workflow parity): profile read/update (UserProfileDto) IS part of this
// contract via GetProfileAsync/UpdateProfileAsync below. The DNN user profile is the EXISTING EAV schema
// ([ProfilePropertyDefinition] definitions + [UserProfile] per-user values); these methods replace the legacy
// ProfileController + UserProfile.vb GetPropertyValue/SetProfileProperty, mapping to/from the flat UserProfileDto by
// the well-known property names (FirstName, LastName, Cell, Telephone, ...).
public interface IUserService
{
    // MIGRATION: Legacy GetUsers(portalId, pageIndex, pageSize, ByRef total) (UserController.vb L725) — scoped by
    // portalId (multi-tenant isolation, AAP 0.7.1) and paged (PagedResult<UserResponse>).
    Task<Result<PagedResult<UserResponse>>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetUser(PortalId, UserId) (UserController.vb L1245). CP1 review (IUserService #1) — PORTAL-SCOPED:
    // portalId is a CONTRACT parameter (not just an impl detail) so tenant ownership is enforceable at the Application
    // boundary; a user is only returned when it belongs to portalId (AAP 0.7.1).
    Task<Result<UserResponse>> GetByIdAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy CreateUser (UserController.vb L156). POST /api/users -> 201 (Gate 5). portalId is carried inside
    // the request (CreateUserRequest.PortalId). The implementation auto-assigns AutoAssignment roles after insert
    // (legacy CreateUser behavior) and persists the initial credential via the credential store.
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateUser(PortalId, objUser) (UserController.vb L963). CP1 review (IUserService #1) — PORTAL-SCOPED:
    // portalId + userId identify the target so the update is constrained to the owning portal. PUT -> 200 (Gate 5).
    Task<Result<UserResponse>> UpdateAsync(int portalId, int userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteUser(PortalId, UserId) (UserController.vb L1292). CP1 review (IUserService #1) — PORTAL-SCOPED.
    // DELETE -> 204. Non-generic Result.
    Task<Result> DeleteAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    // MIGRATION (CP-final review - profile workflow parity): reads a user's profile as the flat UserProfileDto.
    // Replaces legacy ProfileController.GetUserProfile + UserProfile.vb GetPropertyValue: loads the portal's profile
    // definitions and the user's stored values, projecting each well-known property by name (absent value ->
    // Null.NullString -> null; TimeZone parsed to int with Null.NullInteger fallback). Portal-scoped: a 404 (read
    // failure) is returned when the user does not belong to portalId (AAP 0.7.1). GET /api/users/{id}/profile.
    Task<Result<UserProfileDto>> GetProfileAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    // MIGRATION (CP-final review - profile workflow parity): updates a user's profile (UserProfileDto). Replaces
    // legacy ProfileController.UpdateUserProfile + UserProfile.vb SetProfileProperty: for each well-known property
    // that the portal DEFINES, the value row is upserted in [UserProfile] (legacy SetProfileProperty was a no-op for
    // a property the portal did not define). The legacy data-driven validation carried by each definition
    // (Required / Length / ValidationExpression) is enforced before persisting. PUT /api/users/{id}/profile -> 200.
    Task<Result<UserProfileDto>> UpdateProfileAsync(int portalId, int userId, UserProfileDto request, CancellationToken cancellationToken = default);
}
