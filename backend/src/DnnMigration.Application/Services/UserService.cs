using AutoMapper;
using FluentValidation;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the User aggregate (record management only; authentication lives in
/// <c>AuthService</c>/<c>IAuthService</c>).
/// </summary>
/// <remarks>
/// MIGRATION: ports the record-management business surface of
/// <c>Library/Components/Users/UserController.vb</c> into async, DTO-based operations following the
/// uniform service skeleton (validate → check rules → load → mutate/project via AutoMapper →
/// save → return DTO). The legacy provider-model <c>memberProvider</c> calls and the reflection-based
/// <c>CBO</c> hydration are replaced by <see cref="IUserRepository"/> (EF Core materialization);
/// plaintext passwords are one-way hashed via <see cref="IPasswordHasher"/> (BCrypt) — the single
/// sanctioned security change. The User aggregate is HARD-deleted at the persistence layer: the DNN 4.9
/// <c>dbo.Users</c> table has no <c>IsDeleted</c> column (9 physical columns only), so a soft delete is
/// impossible without a schema change (forbidden by ADR-002); delete removes the user row and its
/// <c>dbo.UserPortals</c> membership rows (MIGRATION_NOTES.md §6.3 / D-014).
/// Behavioral equivalence is preserved per the Minimal Change Clause; every deviation is annotated
/// with a <c>// MIGRATION:</c> comment and recorded in the root <c>MIGRATION_NOTES.md</c>.
/// </remarks>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPortalRepository _portalRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateUserDto> _createValidator;
    private readonly IValidator<UpdateUserDto> _updateValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userRepository">User aggregate data access (EF Core).</param>
    /// <param name="portalRepository">Portal data access; used solely for the administrator guard in <see cref="DeleteAsync"/>.</param>
    /// <param name="roleRepository">Role/membership data access; used to auto-assign a newly created non-superuser to every AutoAssignment portal role in <see cref="CreateAsync"/> (legacy CreateUser parity).</param>
    /// <param name="passwordHasher">One-way password hasher (BCrypt) used to hash new-user plaintext passwords.</param>
    /// <param name="mapper">AutoMapper instance backed by <c>UserProfile</c>.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreateUserDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdateUserDto"/>.</param>
    public UserService(
        IUserRepository userRepository,
        IPortalRepository portalRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        IValidator<CreateUserDto> createValidator,
        IValidator<UpdateUserDto> updateValidator)
    {
        _userRepository = userRepository;
        _portalRepository = portalRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Gets a single user by its identifier, or <c>null</c> when no such user exists.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The <see cref="UserDto"/> for the user, or <c>null</c> if not found.</returns>
    public async Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single user by username within a portal, or <c>null</c> when no match exists.
    /// </summary>
    /// <param name="portalId">The portal that scopes the username lookup.</param>
    /// <param name="username">The username to resolve.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The <see cref="UserDto"/> for the user, or <c>null</c> if not found.</returns>
    public async Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        // MIGRATION: mirrors UserController.GetUserByName [UserController.vb:L544-568], which delegated to
        // memberProvider.GetUserByUserName; the provider-model + reflection hydration is replaced by EF materialization.
        var user = await _userRepository.GetByUsernameAsync(portalId, username, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single user by exact email within a portal, or <c>null</c> when no match exists.
    /// </summary>
    /// <param name="portalId">The portal that scopes the email lookup.</param>
    /// <param name="email">The exact email address to resolve.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The <see cref="UserDto"/> for the user, or <c>null</c> if not found.</returns>
    public async Task<UserDto?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy UserController.GetUsersByEmail performed a PAGED pattern-match; this common exact-match
        // single lookup is served by the repository's GetByEmailAsync (EF materialization replaces reflection hydration).
        var user = await _userRepository.GetByEmailAsync(portalId, email, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single page of users for a portal together with the total user count.
    /// </summary>
    /// <param name="portalId">The portal whose users are listed.</param>
    /// <param name="pageIndex">The zero-based page index to return.</param>
    /// <param name="pageSize">The maximum number of users per page.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="PagedResult{T}"/> of <see cref="UserDto"/> for the requested page.</returns>
    public async Task<PagedResult<UserDto>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.GetByPortalAsync(portalId, pageIndex, pageSize, cancellationToken);
        // MIGRATION: memberProvider paged GetUsers + reflection hydration replaced by EF materialization; role hydration (Obsolete overload) dropped — User.Roles is EF-ignored.
        var mapped = _mapper.Map<List<UserDto>>(items);
        return new PagedResult<UserDto>(mapped, totalCount, pageIndex, pageSize);
    }

    /// <summary>
    /// Creates a new user. The plaintext password on <paramref name="request"/> is one-way hashed
    /// before persistence and is never stored or echoed back.
    /// </summary>
    /// <param name="request">The user-creation request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The created user projected as a <see cref="UserDto"/>.</returns>
    /// <exception cref="ValidationException">Thrown when <paramref name="request"/> fails validation.</exception>
    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = _mapper.Map<User>(request);

        // MIGRATION: UserProfile IGNORES Password on CreateUserDto→User. Hash the plaintext via BCrypt (IPasswordHasher) — replaces legacy DES/membership hashing [PortalSecurity.vb]; NEVER store plaintext (single sanctioned security change).
        user.Password = _passwordHasher.Hash(request.Password ?? string.Empty);

        var created = await _userRepository.AddAsync(user, cancellationToken);

        // MIGRATION: legacy CreateUser auto-assigned the new (non-superuser) account to every AutoAssignment
        // portal role [UserController.vb:L166-180]: it enumerated GetPortalRoles(PortalID) and, for each role
        // with AutoAssignment = True, called AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate)
        // (null effective/expiry window). This is the new-user→existing-roles direction and is PRESERVED here
        // (it is NOT covered by RoleService.AutoAssignUsers, which handles only the inverse role→existing-users
        // direction when an AutoAssignment role is created/updated). Clean Architecture is intact: the cross-
        // aggregate read+write goes through the Domain abstraction IRoleRepository (Application→Domain interface);
        // the EF Core implementation is authored in CP3. Unlike the bulk RoleService.AutoAssignUsers loop, legacy
        // CreateUser did NOT swallow assignment exceptions, so none are swallowed here. See MIGRATION_NOTES.md D-012.
        if (!created.IsSuperUser)
        {
            var portalRoles = await _roleRepository.GetByPortalAsync(created.PortalID, cancellationToken);
            foreach (var role in portalRoles)
            {
                if (role.AutoAssignment)
                {
                    await _roleRepository.AddUserRoleAsync(
                        new UserRole
                        {
                            UserID = created.UserID,
                            RoleID = role.RoleID,
                            EffectiveDate = null,
                            ExpiryDate = null,
                        },
                        cancellationToken);
                }
            }
        }

        return _mapper.Map<UserDto>(created);
    }

    /// <summary>
    /// Updates the mutable fields of an existing user. Username, password, and portal are immutable
    /// on update and are not altered by this operation.
    /// </summary>
    /// <param name="request">The user-update request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The updated user projected as a <see cref="UserDto"/>.</returns>
    /// <exception cref="ValidationException">Thrown when <paramref name="request"/> fails validation.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the target user does not exist.</exception>
    public async Task<UserDto> UpdateAsync(UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await _userRepository.GetByIdAsync(request.UserID, cancellationToken);
        if (existing is null)
            throw new KeyNotFoundException($"User {request.UserID} was not found.");

        // MIGRATION: UpdateUserDto omits Username/Password/PortalID (immutable). UserProfile maps the mutable subset onto the tracked entity; password changes are a separate flow (no plaintext round-trip) — parity with legacy UpdateUser which did not alter the password.
        _mapper.Map(request, existing);

        await _userRepository.UpdateAsync(existing, cancellationToken);
        return _mapper.Map<UserDto>(existing);
    }

    /// <summary>
    /// Sets or clears the "force password change on next login" requirement for a user, persisting the
    /// physical <c>dbo.Users.UpdatePassword</c> column, and returns the refreshed projection.
    /// </summary>
    /// <param name="userId">The id of the user whose flag is being set.</param>
    /// <param name="require"><see langword="true"/> to require a change at next login; <see langword="false"/> to clear it.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The updated <see cref="UserDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the target user does not exist.</exception>
    public async Task<UserDto> SetForcePasswordChangeAsync(int userId, bool require, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP-FINAL / Code-Review G5): the legacy DNN Membership admin surface toggled
        // UserMembership.UpdatePassword (Membership.ascx.vb). That flag is the REAL persisted
        // dbo.Users.UpdatePassword column (one of the 9 physical columns), so — unlike Unlock/Approved, which
        // live on the unmapped aspnet_Membership table (ADR-002 / AAP §0.2.2) — this transition is fully
        // implementable and durable. Load the tracked entity, flip ONLY this column, and persist; all other
        // columns (and the Ignore()'d aspnet_* scalars) are left untouched.
        var existing = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (existing is null)
            throw new KeyNotFoundException($"User {userId} was not found.");

        existing.UpdatePassword = require;

        await _userRepository.UpdateAsync(existing, cancellationToken);
        return _mapper.Map<UserDto>(existing);
    }

    /// <summary>
    /// Hard-deletes a user, refusing to delete the portal administrator. A missing user is treated
    /// as an idempotent no-op.
    /// </summary>
    /// <param name="userId">The identifier of the user to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the target user is the portal administrator.</exception>
    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return; // MIGRATION: legacy DeleteUser was wrapped in Try/Catch→CanDelete=False; tolerate a missing user as an idempotent no-op.

        // MIGRATION: administrator guard — legacy set CanDelete = deleteAdmin (False for the single-arg delete) when objUser.UserID == Portal.AdministratorId [L209-216], silently refusing. We load the portal and THROW so the REST layer returns RFC 7807 (consistent with Portal last-portal + Tab child guards).
        // MIGRATION (F5-01 hardening): BusinessRuleConflictException (subtype of InvalidOperationException) so the safe message maps to a 409 detail while raw IOE no longer leaks internals.
        var portal = await _portalRepository.GetByIdAsync(user.PortalID, cancellationToken);
        if (portal is not null && portal.AdministratorId == user.UserID)
            throw new BusinessRuleConflictException("Cannot delete the portal administrator.");

        // MIGRATION (CP3 correction): HARD-delete via repository (removes the dbo.Users row and its
        // dbo.UserPortals membership rows). The DNN 4.9 dbo.Users table has NO IsDeleted column, so a soft
        // delete is impossible without a schema change (ADR-002 forbids it), and the legacy DeleteUser also
        // removed the row. Legacy cascade of Folder/Module/Tab permission cleanup [L221-228], Mail
        // notification, and cache clear are OMITTED in Phase 1 (out of scope). Recorded in MIGRATION_NOTES.md
        // §6.3 (delete strategy) / D-014.
        await _userRepository.DeleteAsync(userId, cancellationToken);
    }
}
