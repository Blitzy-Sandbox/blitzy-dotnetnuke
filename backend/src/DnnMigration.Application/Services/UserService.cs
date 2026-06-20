using AutoMapper;
using FluentValidation;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the User aggregate (record management only; authentication / credential
/// validation lives in <c>AuthService</c>). MIGRATION: ports the business surface of the legacy
/// <c>UserController.vb</c> (DotNetNuke 4.9.0.85, <c>DotNetNuke.Entities.Users</c>) into async,
/// DTO-based, repository-backed operations following the uniform service skeleton
/// (validate → check rules → load → mutate/project via AutoMapper → save → return DTO).
/// </summary>
/// <remarks>
/// Behavioral equivalence is preserved with the legacy controller except where a deviation is annotated
/// with a <c>// MIGRATION:</c> comment (and recorded in the root <c>MIGRATION_NOTES.md</c>). The single
/// sanctioned behavior change is credential handling: new-user passwords are hashed with BCrypt via
/// <see cref="IPasswordHasher"/> instead of the legacy DES/membership routines, and plaintext is never
/// persisted. The legacy reflection-based hydration (<c>CBO</c>) and <c>memberProvider</c> data calls are
/// replaced wholesale by EF Core entity materialization behind <see cref="IUserRepository"/>.
/// </remarks>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPortalRepository _portalRepository;
    // MIGRATION (M4/DEV-033): cross-aggregate dependency required to reproduce the legacy CreateUser
    // auto-assignment of new non-superusers to AutoAssignment portal roles. Injected as the Domain
    // repository interface (not IRoleService) so Clean-Architecture dependency direction and DI acyclicity
    // are preserved (RoleService depends on IUserRepository, never IUserService — no cycle).
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateUserDto> _createValidator;
    private readonly IValidator<UpdateUserDto> _updateValidator;

    /// <summary>
    /// Initializes a new <see cref="UserService"/> with its injected collaborators.
    /// </summary>
    /// <param name="userRepository">Persistence gateway for the <see cref="User"/> aggregate.</param>
    /// <param name="portalRepository">
    /// Portal lookup, used solely by the administrator guard in <see cref="DeleteAsync"/> to compare a
    /// candidate user against <see cref="Portal.AdministratorId"/>.
    /// </param>
    /// <param name="roleRepository">
    /// Role/membership gateway, used solely by <see cref="CreateAsync"/> to reproduce the legacy
    /// auto-assignment of new non-superusers to <see cref="Role.AutoAssignment"/> portal roles.
    /// </param>
    /// <param name="passwordHasher">One-way BCrypt hasher used to protect new-user passwords.</param>
    /// <param name="mapper">AutoMapper instance providing the User entity ↔ DTO projections.</param>
    /// <param name="createValidator">FluentValidation rules for <see cref="CreateUserDto"/>.</param>
    /// <param name="updateValidator">FluentValidation rules for <see cref="UpdateUserDto"/>.</param>
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
    /// Gets a single user by its identifier, or <c>null</c> when no user exists.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The mapped <see cref="UserDto"/>, or <c>null</c> if the user was not found.</returns>
    public async Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy UserController.GetUser hydrated a UserInfo via reflection (CBO/memberProvider);
        // replaced by EF Core materialization behind IUserRepository, then projected to a read DTO.
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single user by username within a portal, or <c>null</c> when no match exists.
    /// </summary>
    /// <param name="portalId">The portal that scopes the username lookup.</param>
    /// <param name="username">The login name to resolve.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The mapped <see cref="UserDto"/>, or <c>null</c> if the user was not found.</returns>
    public async Task<UserDto?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        // MIGRATION: mirrors UserController.GetUserByName [L544-568], which delegated to
        // memberProvider.GetUserByUserName; EF materialization replaces reflection hydration.
        var user = await _userRepository.GetByUsernameAsync(portalId, username, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single user by exact email within a portal, or <c>null</c> when no match exists.
    /// </summary>
    /// <param name="portalId">The portal that scopes the email lookup.</param>
    /// <param name="email">The email address to resolve.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The mapped <see cref="UserDto"/>, or <c>null</c> if the user was not found.</returns>
    public async Task<UserDto?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy UserController.GetUsersByEmail performed a PAGED pattern match; the data contract
        // models the common exact-match single lookup (uniqueness/registration/reset checks) via EF.
        var user = await _userRepository.GetByEmailAsync(portalId, email, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Gets a single page of users for a portal, together with the total user count.
    /// </summary>
    /// <param name="portalId">The portal whose users are listed.</param>
    /// <param name="pageIndex">The zero-based index of the page to return.</param>
    /// <param name="pageSize">The maximum number of users per page.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>A <see cref="PagedResult{UserDto}"/> wrapping the page of users and its metadata.</returns>
    public async Task<PagedResult<UserDto>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.GetByPortalAsync(portalId, pageIndex, pageSize, cancellationToken);
        // MIGRATION: memberProvider paged GetUsers + reflection hydration replaced by EF materialization; role hydration (Obsolete overload) dropped — User.Roles is EF-ignored.
        var mapped = _mapper.Map<List<UserDto>>(items);
        return new PagedResult<UserDto>(mapped, totalCount, pageIndex, pageSize);
    }

    /// <summary>
    /// Creates a new user. Validates the request, hashes the supplied plaintext password, persists the
    /// entity, and returns the created user projected to a <see cref="UserDto"/>.
    /// </summary>
    /// <param name="request">The user creation request.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The created user as a <see cref="UserDto"/>.</returns>
    /// <exception cref="ValidationException">Thrown when <paramref name="request"/> fails validation.</exception>
    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = _mapper.Map<User>(request);

        // MIGRATION: UserProfile IGNORES Password on CreateUserDto→User. Hash the plaintext via BCrypt (IPasswordHasher) — replaces legacy DES/membership hashing [PortalSecurity.vb]; NEVER store plaintext (single sanctioned security change).
        user.Password = _passwordHasher.Hash(request.Password ?? string.Empty);

        var created = await _userRepository.AddAsync(user, cancellationToken);

        // MIGRATION (M4/DEV-033): reproduce the legacy CreateUser auto-assignment [UserController.vb:L166-180].
        // On successful creation (the C# AddAsync return is the parity equivalent of the legacy
        // UserCreateStatus.Success branch), a NEW NON-superuser is enrolled into every portal role flagged
        // AutoAssignment. Ported verbatim: load GetPortalRoles, and for each AutoAssignment role INSERT a
        // user→role join with NULL effective/expiry dates via the insert-only AddUserRoleAsync — a DIRECT add
        // (legacy passed Null.NullDate/Null.NullDate), NOT the expiry-computing UpdateUserRole/RoleService path.
        // No try/catch wraps the loop, matching the legacy control flow. Superusers are skipped exactly as in DNN.
        if (!created.IsSuperUser)
        {
            var portalRoles = await _roleRepository.GetByPortalAsync(created.PortalID, cancellationToken);
            foreach (var role in portalRoles)
            {
                if (role.AutoAssignment)
                {
                    await _roleRepository.AddUserRoleAsync(
                        new UserRole { UserID = created.UserID, RoleID = role.RoleID, EffectiveDate = null, ExpiryDate = null },
                        cancellationToken);
                }
            }
        }

        return _mapper.Map<UserDto>(created);
    }

    /// <summary>
    /// Updates the mutable fields of an existing user. Validates the request, loads the tracked entity,
    /// applies the mutable subset, persists, and returns the refreshed <see cref="UserDto"/>.
    /// </summary>
    /// <param name="request">The user update request.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The updated user as a <see cref="UserDto"/>.</returns>
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
    /// Hard-deletes a user by identifier, refusing to delete the portal administrator. MIGRATION (DEV-039):
    /// the delete is a HARD delete — the DNN 4.9 [Users] table has no IsDeleted column, so a soft delete is
    /// impossible without a schema change (ADR-002 forbids it), and the legacy UserController.DeleteUser
    /// likewise removed the row via the membership provider. A missing user is treated as an idempotent no-op.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Thrown when the user is the portal administrator.</exception>
    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return; // MIGRATION: legacy DeleteUser was wrapped in Try/Catch→CanDelete=False; tolerate a missing user as an idempotent no-op.

        // MIGRATION: administrator guard — legacy set CanDelete = deleteAdmin (False for the single-arg delete) when objUser.UserID == Portal.AdministratorId [L209-216], silently refusing. We load the portal and THROW so the REST layer returns RFC 7807 (consistent with Portal last-portal + Tab child guards).
        var portal = await _portalRepository.GetByIdAsync(user.PortalID, cancellationToken);
        if (portal is not null && portal.AdministratorId == user.UserID)
            throw new InvalidOperationException("Cannot delete the portal administrator.");

        // MIGRATION (DEV-039): HARD-delete via repository (UserRepository.DeleteAsync issues _context.Users.Remove). The [Users] table has no IsDeleted column, so a soft delete is impossible without a schema change (ADR-002 forbids it); the legacy UserController.DeleteUser likewise hard-deleted via the membership provider. Legacy cascade of Folder/Module/Tab permission cleanup [L221-228], Mail notification, and cache clear are OMITTED in Phase 1 (out of scope) — documented in MIGRATION_NOTES.md.
        await _userRepository.DeleteAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Flags a user to change their password on next login by setting the mapped <c>[Users].UpdatePassword</c>
    /// column. Returns the updated user; throws <see cref="KeyNotFoundException"/> when the user does not exist.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to flag.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists for <paramref name="userId"/>.</exception>
    public async Task<UserDto> ForcePasswordChangeAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: reproduces the legacy admin "force password change" affordance (cmdPassword_Click in
        // Website/admin/Users/Membership.ascx.vb), which set exactly the [Users].UpdatePassword bit so the
        // user is prompted to change their password at next login. [Users].UpdatePassword is a real mapped
        // column (UserConfiguration: builder.Property(u => u.UpdatePassword).HasColumnName("UpdatePassword")),
        // so this transition has a durable Phase-1 home. The related aspnet_Membership transitions
        // (approve/unauthorize/unlock) target EF-Ignore()d fields with no [Users] column and are deferred per
        // ADR-002 / §0.6.2 — see MIGRATION_NOTES.md.
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} was not found.");

        user.UpdatePassword = true;
        await _userRepository.UpdateAsync(user, cancellationToken);
        return _mapper.Map<UserDto>(user);
    }
}
