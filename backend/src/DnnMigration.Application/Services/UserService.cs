using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Exceptions;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing user identity, membership and profile business rules.
/// </summary>
/// <remarks>
/// This is the "user" use-case ring of the Clean/Onion architecture. It orchestrates the domain
/// <see cref="User"/> aggregate through the <see cref="IUserRepository"/> port, projects to/from the
/// Application DTOs via <see cref="IMapper"/>, and owns credential material through the
/// <see cref="IPasswordHasher"/> port. It is deliberately free of any HTTP, EF Core/DbContext, or
/// framework concerns: the controller layer handles transport and the Infrastructure layer supplies
/// the concrete repository and BCrypt hasher.
/// </remarks>
// MIGRATION: business rules extracted from the legacy DotNetNuke UserController.vb
// (Library/Components/Users/UserController.vb). All legacy `Public Shared` (static) members become
// dependency-injected instance methods (AAP static->DI rule). Credential handling uses IPasswordHasher
// (BCrypt) instead of the DNN membership provider; data access is delegated to IUserRepository.
//
// SIDE-EFFECT DISPOSITION (finding #8 — Minimal Change Clause / behavioral equivalence):
//   * ADMIN PROTECTION (legacy DeleteUser deleteAdmin gate [UserController.vb L200]) — PRESERVED. A user
//     who is the AdministratorId of any portal cannot be deleted; enforced here against the real
//     Portals.AdministratorId column (populated by PortalService.CreateAsync) via the injected
//     IPortalRepository, surfaced as a 409 Conflict (ConflictException). This is why the service now
//     depends on IPortalRepository in addition to IUserRepository/IPasswordHasher.
//   * ROLE ASSIGNMENT / AUTO-ASSIGNMENT (legacy CreateUser auto-assigned the new user to every
//     AutoAssignment=True portal role via RoleController.GetPortalRoles/AddUserRole) — DROPPED as
//     genuinely outside the entire AAP target design: AAP §0.3.1 enumerates NO user-role junction
//     entity, NO user-role repository, and NO role-assignment endpoint (the Roles resource is pure
//     CRUD); User.Roles is an Ignore()'d transient string[] (UserConfiguration). There is therefore no
//     persistence port to assign through. Also consistent with §0.2.2 (DNN provider-based membership is
//     out of scope, replaced by JWT) and §0.6.4 (membership collapses to JWT/claims). See the boundary
//     test in UserServiceTests (CreateAsync performs no role assignment).
//   * CASCADED PERMISSION CLEANUP (legacy folder/module/tab permission deletes) — NOT APPLICABLE: no
//     per-user permission rows are written by the core Portal/Module/User CRUD surface (§0.2.1), so
//     there is nothing to cascade. The user's OWN dependent rows (the aspnet_Membership credential row
//     and the UserPortals association) ARE cascaded by UserRepository.DeleteAsync (Phase-2 bridge).
//   * EVENT-LOG / EMAIL NOTIFY / CACHE CLEAR — OUT OF SCOPE per §0.2.2 (logging is replaced by Serilog at
//     the host layer; email/notify is not in the core migration; there is no cache abstraction here).
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userRepository">The repository port used for all user persistence.</param>
    /// <param name="passwordHasher">The password hashing port (BCrypt) that owns credential material.</param>
    /// <param name="portalRepository">
    /// The portal persistence port, consulted on delete to enforce the legacy administrator-protection
    /// rule (a user who administers a portal cannot be removed). MIGRATION: replaces the legacy
    /// <c>deleteAdmin</c> gate of <c>UserController.DeleteUser</c> [UserController.vb L200] using the
    /// authoritative <c>Portals.AdministratorId</c> column.
    /// </param>
    /// <param name="mapper">The AutoMapper instance used for entity&lt;-&gt;DTO projection.</param>
    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPortalRepository portalRepository,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _portalRepository = portalRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.GetUsers(portalId) [L685] -> GetUsers(portalId, False, -1, -1, -1) (all portal users).
    public async Task<IEnumerable<UserDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.GetUser(portalId, userId, isHydrated) [L497] -> memberProvider.GetUser(...).
    // The legacy auto-hydration (Membership/Profile/Roles lazy-load) and provider-level caching are dropped;
    // a missing user maps to null (the controller translates null into an HTTP 404).
    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.GetUserByName(portalId, username) [L544] -> memberProvider.GetUserByUserName(portalId, username, False).
    // Usernames are unique per portal, so the lookup is scoped by portalId; a miss maps to null.
    public async Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(portalId, username, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <inheritdoc />
    // MIGRATION: Users.ascx.vb ddlSearchType + txtSearch (GetUsersByUserName / GetUsersByEmail / name
    // search). Delegated to IUserRepository.SearchAsync (field-specific Username/Email match, or free-text
    // across username/email/display-name/first-name/last-name, optionally portal-scoped) and projected to
    // DTOs. Serves the AAP §0.7.2 GET /api/users?query=... | ?filterProperty=&filter= contract server-side.
    public async Task<IEnumerable<UserDto>> SearchAsync(int? portalId, string? query, string? filterProperty, string? filter, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.SearchAsync(portalId, query, filterProperty, filter, cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.CreateUser(objUser) [L156]. Legacy delegated credential creation to the membership
    // provider (which hashed the password), then, on UserCreateStatus.Success, cleared the portal cache
    // (DataCache.ClearPortalCache) and, for non-superusers, auto-assigned the new user to every portal role flagged
    // AutoAssignment=True (via RoleController.GetPortalRoles/AddUserRole). Per the class-level side-effect
    // disposition: the role auto-assignment is DROPPED (no user-role junction/repository/endpoint exists in the AAP
    // §0.3.1 target design; §0.2.2/§0.6.4 collapse provider membership into JWT) and the cache clear is DROPPED (no
    // cache abstraction in this layer). Credentials are OWNED by this service (hashed via IPasswordHasher below);
    // this method performs NO role membership write — a boundary pinned by UserServiceTests.
    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = _mapper.Map<User>(dto);

        // MIGRATION: Legacy Users.DisplayName is NOT NULL DEFAULT ('') in the existing schema, and
        // CreateUserDto.DisplayName is optional ("when omitted a display name is derived downstream").
        // AutoMapper copies the DTO value - possibly null - over the entity's string.Empty default, so when
        // the client omits DisplayName we derive it here from the first/last name (mirroring the legacy
        // UserInfo.UpdateDisplayName [FIRSTNAME] [LASTNAME] format), falling back to the username when the
        // name parts are empty. This guarantees the NOT NULL column is never written null while preserving
        // the existing schema (no table-structure change).
        if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            var derivedDisplayName = $"{user.FirstName} {user.LastName}".Trim();
            user.DisplayName = string.IsNullOrWhiteSpace(derivedDisplayName)
                ? user.Username
                : derivedDisplayName;
        }

        // MIGRATION: AutoMapper never maps credentials (Password/PasswordQuestion/PasswordAnswer are source-only on
        // CreateUserDto); hash the password (BCrypt via IPasswordHasher) here - a plaintext password is NEVER stored.
        user.Membership.Password = _passwordHasher.Hash(dto.Password);
        user.Membership.PasswordQuestion = dto.PasswordQuestion ?? string.Empty;
        user.Membership.PasswordAnswer = dto.PasswordAnswer ?? string.Empty;

        // MIGRATION: CreateUser's Authorize flag maps to Membership.Approved (approval is a membership state the
        // service owns, not the mapper); CreatedDate is stamped at creation (UTC). Username/Email are mirrored onto
        // Membership so the credential record stays consistent with the identity record.
        user.Membership.Approved = dto.Authorize;
        user.Membership.CreatedDate = DateTime.UtcNow;
        user.Membership.Username = dto.Username;
        user.Membership.Email = dto.Email;

        var created = await _userRepository.AddAsync(user, cancellationToken);
        return _mapper.Map<UserDto>(created);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.UpdateUser(portalId, objUser) [L963]. Legacy called memberProvider.UpdateUser and then
    // DataCache.ClearUserCache(portalId, username); the cache clear is DROPPED (no cache abstraction in this layer).
    // AutoMapper copies the identity fields plus the flat profile fields (mapped onto User.Profile via ForPath) onto
    // the existing tracked entity; Approved is NOT part of the mapping profile (approval is a membership state
    // transition owned by the service). A missing user maps to null (the controller translates null into an HTTP 404).
    public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        _mapper.Map(dto, user);

        // MIGRATION: Approved is intentionally excluded from the mapping profile; apply the state transition here so
        // the admin approval toggle takes effect only when the client actually supplies a value.
        if (dto.Approved.HasValue)
        {
            user.Membership.Approved = dto.Approved.Value;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
        return _mapper.Map<UserDto>(user);
    }

    /// <inheritdoc />
    // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) [L200]. Legacy performed a portal-administrator
    // protection check (the deleteAdmin gate compared UserID against the portal's AdministratorId), cascaded folder /
    // module / tab permission deletes (FolderPermissionController/ModulePermissionController/TabPermissionController),
    // logged a USER_DELETED event, optionally emailed an unregister notice (notify) and cleared the portal/user caches.
    // Per the class-level side-effect disposition: the ADMIN-PROTECTION check is PRESERVED (below); the CASCADED
    // PERMISSION cleanup is NOT APPLICABLE (the core CRUD surface writes no per-user permission rows, and the user's own
    // membership + UserPortals rows are already cascaded by UserRepository.DeleteAsync); the event-log/email/cache
    // side-effects are OUT OF SCOPE (§0.2.2). A missing user maps to false (the controller translates false into 404).
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // MIGRATION: admin-protection — the faithful, in-scope equivalent of the legacy deleteAdmin gate. Deleting a
        // user who administers a portal would leave that portal without an administrator, so the operation is refused
        // with a 409 Conflict (ConflictException -> ExceptionHandlingMiddleware). The rule reads the authoritative
        // Portals.AdministratorId column (set by PortalService.CreateAsync) through IPortalRepository — no new schema
        // and no user-role junction is required. The modern REST contract exposes no caller-supplied "force" override,
        // so protection is unconditional (a behavioral clarification documented per the Minimal Change Clause).
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        if (portals.Any(portal => portal.AdministratorId == id))
        {
            throw new ConflictException(
                "The user is the administrator of one or more portals and cannot be deleted while those portals exist.");
        }

        await _userRepository.DeleteAsync(id, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    // MIGRATION: UserController.ChangePassword(user, oldPassword, newPassword) [L103]. Legacy validated the new password
    // (ValidatePassword; threw Exception("Invalid Password") when invalid), delegated the old->new change to the
    // membership provider (which verified the old password internally) and set Membership.UpdatePassword = False before
    // calling UpdateUser (which also cleared the user cache). Preserved here: reject an empty/whitespace new password,
    // verify the old password via IPasswordHasher.Verify, hash + store the new password, clear UpdatePassword and stamp
    // LastPasswordChangeDate. The legacy cache clear is DROPPED.
    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // MIGRATION: legacy ValidatePassword(newPassword) threw Exception("Invalid Password"); here we return false for
        // an empty/whitespace password. The full password policy (required, MinimumLength=7, non-alphanumeric rule, and
        // the new-must-differ-from-old comparison) is enforced at the API boundary by ChangePasswordDtoValidator
        // (DnnMigration.Application.Validators), which FluentValidation runs before this method is reached; a failing
        // payload is rejected there with a deterministic RFC 7807 400. This whitespace guard is retained as
        // defense-in-depth for any caller that reaches the service without passing through the HTTP validation pipeline,
        // and preserves the documented behavioral divergence (legacy throw becomes return-false) per the Minimal Change Clause.
        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return false;
        }

        // MIGRATION: memberProvider.ChangePassword verified the old password before applying the change; that check is
        // now performed explicitly via IPasswordHasher.Verify (BCrypt). A mismatch returns false (no change applied).
        if (!_passwordHasher.Verify(dto.OldPassword, user.Membership.Password))
        {
            return false;
        }

        user.Membership.Password = _passwordHasher.Hash(dto.NewPassword);
        user.Membership.UpdatePassword = false;
        user.Membership.LastPasswordChangeDate = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        return true;
    }
}
