// MIGRATION: Application-layer service for the User domain. Converted from the public business
// operations of the legacy VB.NET Library/Components/Users/UserController.vb (DotNetNuke 4.x). The
// business rules are extracted VERBATIM (AAP 0.7.2): CreateUser auto-assigns every portal role flagged
// AutoAssignment, and DeleteUser guards the portal administrator against deletion. All data access is
// delegated to Domain repository interfaces (no DbContext/EF/SQL here, AAP 0.7.3); Domain entities are
// projected to DTOs through AutoMapper and never returned raw (AAP 0.7.7). Credential/password handling
// is intentionally ABSENT from this service — it lives in AuthService and Infrastructure/Identity (BCrypt)
// per AAP 0.7.6, and the User entity carries no password/hash member.
using AutoMapper;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Services;

/// <summary>
/// Provides user-management business logic for the migrated DotNetNuke User domain. Implements
/// <see cref="IUserService"/> and orchestrates the user, role, and portal repositories together with the
/// unit-of-work persistence boundary, projecting <see cref="User"/> entities to <see cref="UserResponse"/>
/// DTOs. Every operation is portal-scoped to preserve the legacy multi-tenant isolation (AAP 0.7.1).
/// </summary>
/// <remarks>
/// MIGRATION: Replaces the static, provider-driven Library/Components/Users/UserController.vb. The legacy
/// <c>memberProvider</c>/<c>DataProvider.Instance()</c> + <c>IDataReader</c> pipeline is replaced by
/// constructor-injected repositories and EF Core unit-of-work semantics. Expected (non-exceptional) business
/// failures are surfaced as <see cref="Result"/>/<see cref="Result{T}"/> failures; unexpected exceptions are
/// allowed to bubble up to the API exception-handling middleware (RFC 7807) rather than being swallowed here.
/// </remarks>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPortalRepository _portalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userRepository">User data-access abstraction (portal-scoped queries, add/update/delete).</param>
    /// <param name="roleRepository">Role data-access abstraction; used to resolve AutoAssignment roles on create.</param>
    /// <param name="portalRepository">Portal data-access abstraction; used to read the portal administrator on delete.</param>
    /// <param name="unitOfWork">Unit-of-work persistence boundary wrapping <c>SaveChangesAsync</c>.</param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    // MIGRATION: DI replaces the legacy reflection-instantiated singletons (RoleController/DataProvider.Instance()).
    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPortalRepository portalRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _portalRepository = portalRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <summary>
    /// Returns a single page of users belonging to the specified portal.
    /// </summary>
    /// <param name="portalId">The portal (tenant) whose users are listed.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary (unused for this read path).</param>
    /// <returns>A successful <see cref="Result{T}"/> wrapping the requested <see cref="PagedResult{T}"/> of users.</returns>
    public async Task<Result<PagedResult<UserResponse>>> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.GetUsers(PortalId) / GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords)
        // paged at the ASP.NET 2.0 MembershipProvider with a ZERO-BASED page index; here the portal-scoped set is
        // fetched once and paged in-memory (PageIndex stays zero-based for behavioral parity). UserResponse.Roles is
        // flattened by UserProfile from the UserRoles -> Role.RoleName navigation.
        var users = (await _userRepository.GetByPortalIdAsync(portalId)).ToList();
        var total = users.Count;

        var pageItems = users
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(u => _mapper.Map<UserResponse>(u))
            .ToList();

        return Result<PagedResult<UserResponse>>.Success(new PagedResult<UserResponse>
        {
            Items = pageItems,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Retrieves a single user by its identity.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary (unused for this read path).</param>
    /// <returns>The user as a <see cref="UserResponse"/>, or a failure when no user matches <paramref name="userId"/>.</returns>
    public async Task<Result<UserResponse>> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.GetUser(PortalId, UserId) delegated to memberProvider.GetUser; collapsed here to a
        // UserId-only lookup (UserId is a globally-unique identity column). A missing user becomes an expected failure
        // rather than a null UserInfo, so the Api can map it to a 404 ProblemDetails.
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Result<UserResponse>.Failure($"User {userId} was not found.");
        }

        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(user));
    }

    /// <summary>
    /// Creates a new user under a portal, auto-assigning the portal's AutoAssignment roles.
    /// </summary>
    /// <param name="request">The create-user request payload.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>The created user as a <see cref="UserResponse"/>, or a failure for a duplicate username/email.</returns>
    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.CreateUser -> UserCreateStatus.DuplicateUserName / UserAlreadyRegistered /
        // UsernameAlreadyExists -> Localization key "UserNameExists" (GetUserCreateStatus L598). This is a
        // multi-entity rule the FluentValidation validator cannot enforce, so it is checked here, scoped by
        // request.PortalId (usernames are unique only within a portal). The .resx string is out of scope; an
        // equivalent message is surfaced here.
        var existing = await _userRepository.GetByUsernameAsync(request.PortalId, request.Username);
        if (existing is not null)
        {
            return Result<UserResponse>.Failure("A user with this username already exists.");
        }

        // MIGRATION: UserCreateStatus.DuplicateEmail -> key "UserEmailExists". Portal-scoped duplicate-email guard,
        // case-insensitive to mirror the legacy provider comparison.
        var portalUsers = await _userRepository.GetByPortalIdAsync(request.PortalId);
        if (portalUsers.Any(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<UserResponse>.Failure("A user with this email address already exists.");
        }

        // MIGRATION: CreateUserRequest.Password/Confirm are intentionally IGNORED — credential persistence belongs to
        // Infrastructure/Identity (BCrypt, AAP 0.7.6); the User entity has no hash field, and UserProfile leaves the
        // request's credential inputs unmapped. Recorded in MIGRATION_NOTES.md.
        var user = _mapper.Map<User>(request);

        // MIGRATION: UserController.CreateUser L156 — after the user was created and NOT a SuperUser, every portal role
        // with AutoAssignment=True was assigned via RoleController.AddUserRole(PortalID, UserID, RoleID, Null.NullDate,
        // Null.NullDate). Preserved here through the User.UserRoles navigation (null effective/expiry dates); EF persists
        // the join rows on SaveChanges. (Null.NullDate -> null per the nullable-DateTime conversion.)
        if (!user.IsSuperUser)
        {
            var roles = await _roleRepository.GetByPortalIdAsync(request.PortalId);
            foreach (var role in roles.Where(r => r.AutoAssignment))
            {
                user.UserRoles.Add(new UserRole
                {
                    RoleId = role.RoleId,
                    EffectiveDate = null,
                    ExpiryDate = null
                });
            }
        }

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: reload by the database-generated UserId so the response's UserRoles -> Role navigation is populated
        // for the UserResponse.Roles projection. Falls back to the in-memory entity if the reload returns null.
        var created = await _userRepository.GetByIdAsync(user.UserId) ?? user;
        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(created));
    }

    /// <summary>
    /// Updates the editable profile/account fields of an existing user.
    /// </summary>
    /// <param name="userId">The identity of the user to update (route-bound).</param>
    /// <param name="request">The update-user request payload.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>The updated user as a <see cref="UserResponse"/>, or a failure when no user matches <paramref name="userId"/>.</returns>
    public async Task<Result<UserResponse>> UpdateAsync(int userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.UpdateUser(PortalId, objUser) L963 delegated to memberProvider.UpdateUser. A missing
        // user becomes an expected failure (Api maps to 404) instead of throwing.
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Result<UserResponse>.Failure($"User {userId} was not found.");
        }

        // MIGRATION: UpdateUserRequest -> User applies the editable profile fields (FirstName/LastName/DisplayName/Email)
        // plus the admin-editable IsApproved/LockedOut flags; UserProfile Ignores the immutable identity (UserId/PortalId/
        // Username), derived members, and the UserRoles navigation. The legacy UpdateUser also called UpdateDisplayName
        // (re-deriving DisplayName from a profile format mask) and persisted profile-definition properties; profile-
        // definition handling is DEFERRED — there is no profile DTO in scope for this CRUD contract.
        _mapper.Map(request, user);

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(user));
    }

    /// <summary>
    /// Deletes an existing user, refusing to delete the portal administrator.
    /// </summary>
    /// <param name="userId">The identity of the user to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>
    /// A successful <see cref="Result"/> (mapped to HTTP 204 by the Api) when the user is deleted; a failure when the
    /// user does not exist or is the portal administrator.
    /// </returns>
    public async Task<Result> DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) L200. A missing user becomes an expected
        // failure rather than the legacy swallowed exception (Catch Exc -> CanDelete = False).
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure($"User {userId} was not found.");
        }

        // MIGRATION: UserController.DeleteUser L200 — CanDelete = True; reading the portal, If UserID =
        // PortalSettings.AdministratorId Then CanDelete = deleteAdmin. This DELETE endpoint exposes no deleteAdmin flag,
        // so it defaults to False -> the portal administrator cannot be deleted. (Legacy continued the delete only If
        // CanDelete.) The admin-guard is read from the user's own portal (user.PortalId) to keep tenant scoping intact.
        var portal = await _portalRepository.GetByIdAsync(user.PortalId);
        if (portal is not null && user.UserId == portal.AdministratorId)
        {
            return Result.Failure("The portal administrator cannot be deleted.");
        }

        // MIGRATION: DeleteUser L200 also called DeleteFolderPermissionsByUserID and the module + tab permission cleanup
        // (ModulePermissionController/TabPermissionController) before deleting the user. The permission repositories are
        // not in scope for this phase, so explicit cleanup is DEFERRED here (EF cascade may apply once the relationships
        // are configured). Recorded in MIGRATION_NOTES.md.
        await _userRepository.DeleteAsync(userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
