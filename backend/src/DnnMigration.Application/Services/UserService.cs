// MIGRATION: Application-layer service for the User domain. Converted from the public business
// operations of the legacy VB.NET Library/Components/Users/UserController.vb (DotNetNuke 4.x). The
// business rules are extracted VERBATIM (AAP 0.7.2): CreateUser auto-assigns every portal role flagged
// AutoAssignment, and DeleteUser guards the portal administrator against deletion. All data access is
// delegated to Domain repository interfaces (no DbContext/EF/SQL here, AAP 0.7.3); Domain entities are
// projected to DTOs through AutoMapper and never returned raw (AAP 0.7.7). Credential/password handling uses
// the IPasswordHasher (BCrypt) + ICredentialStore ports: CreateUser hashes the initial password and persists it
// through the credential store so accounts are NEVER credentialless (CP1 review UserService #5 / Security #1).
// The User entity itself still carries no password/hash member (AAP 0.7.6) — the hash lives behind the port.
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICredentialStore _credentialStore;
    private readonly IPortalSettingsService _portalSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class.
    /// </summary>
    /// <param name="userRepository">User data-access abstraction (portal-scoped queries, add/update/delete).</param>
    /// <param name="roleRepository">Role data-access abstraction; used to resolve AutoAssignment roles on create.</param>
    /// <param name="portalRepository">Portal data-access abstraction; used to read the portal administrator on delete.</param>
    /// <param name="unitOfWork">Unit-of-work persistence boundary wrapping <c>SaveChangesAsync</c>.</param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    /// <param name="passwordHasher">BCrypt one-way password hashing port (initial-credential creation).</param>
    /// <param name="credentialStore">Credential-store port persisting the user's password hash (AAP 0.7.6).</param>
    /// <param name="portalSettingsService">Per-portal settings port; supplies the Security_DisplayNameFormat rule.</param>
    // MIGRATION: DI replaces the legacy reflection-instantiated singletons (RoleController/DataProvider.Instance()).
    // CP1 review (UserService #1): fail-fast null guards on every dependency so DI misconfiguration surfaces at
    // construction rather than later as a NullReferenceException — consistent with PortalService/ModuleService/TabService.
    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPortalRepository portalRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPasswordHasher passwordHasher,
        ICredentialStore credentialStore,
        IPortalSettingsService portalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(roleRepository);
        ArgumentNullException.ThrowIfNull(portalRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(portalSettingsService);

        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _portalRepository = portalRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _credentialStore = credentialStore;
        _portalSettingsService = portalSettingsService;
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
        // MIGRATION: CP1 review (UserService #2) — validate paging inputs BEFORE any repository access. A negative page
        // index or a non-positive page size is a controlled validation failure (Api -> 400 ProblemDetails), never a
        // repository call with invalid bounds.
        if (pageIndex < 0)
        {
            return Result<PagedResult<UserResponse>>.Failure("Page index must be zero or greater.");
        }

        if (pageSize <= 0)
        {
            return Result<PagedResult<UserResponse>>.Failure("Page size must be greater than zero.");
        }

        // MIGRATION: UserController.GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords) — the legacy ASP.NET 2.0
        // MembershipProvider paged at the data source with a ZERO-BASED page index. CP1 review (performance #22): use the
        // paged repository so ONLY the requested page plus the total count is materialized (no fetch-all-then-page-in-
        // memory). PageIndex stays zero-based for behavioral parity; UserResponse.Roles is flattened by UserProfile from
        // the UserRoles -> Role.RoleName navigation.
        var (users, total) = await _userRepository.GetByPortalPagedAsync(portalId, pageIndex, pageSize);

        var pageItems = users
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
    /// Retrieves a single user within a portal by its identity.
    /// </summary>
    /// <param name="portalId">The portal (tenant) that must own the user.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary (unused for this read path).</param>
    /// <returns>The user as a <see cref="UserResponse"/>, or a failure when no matching user exists in the portal.</returns>
    public async Task<Result<UserResponse>> GetByIdAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.GetUser(PortalId, UserId) delegated to memberProvider.GetUser. CP1 review
        // (IUserService #1 / IUserRepository #1) — PORTAL-SCOPED: the lookup is constrained to portalId so a user from
        // another portal is never returned (multi-tenant isolation, AAP 0.7.1). A missing/unowned user is an expected
        // failure (Api -> 404). CP1 review #7 (enumeration): the not-found message is OPAQUE — no raw id is echoed.
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result<UserResponse>.Failure("The requested user was not found.");
        }

        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(user));
    }

    /// <summary>
    /// Creates a new user under a portal, auto-assigning the portal's AutoAssignment roles and persisting an initial
    /// hashed credential so the account is never credentialless.
    /// </summary>
    /// <param name="request">The create-user request payload.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>The created user as a <see cref="UserResponse"/>, or a failure for a duplicate username.</returns>
    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP1 review UserService #3): fail-fast null guard. A null request is a programming/binding error,
        // not an expected business failure, so it throws before any request field is dereferenced (the Api maps it to a
        // ProblemDetails) — consistent with the constructor null guards.
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: UserController.CreateUser -> UserCreateStatus.DuplicateUserName / UserAlreadyRegistered /
        // UsernameAlreadyExists -> Localization key "UserNameExists" (GetUserCreateStatus L598). This is a multi-entity
        // rule the FluentValidation validator cannot enforce, so it is checked here, scoped by request.PortalId
        // (usernames are unique only within a portal). CP1 review (UserService #4 / AAP 0.7.2 exact error-message
        // parity): the failure text is the VERBATIM legacy "UserNameExists" resource string.
        var existing = await _userRepository.GetByUsernameAsync(request.PortalId, request.Username);
        if (existing is not null)
        {
            return Result<UserResponse>.Failure(
                "A User Already Exists For the Username Specified. Please Register Again Using A Different Username.");
        }

        // MIGRATION (CP1 review UserService #4): the unconditional duplicate-EMAIL guard has been REMOVED. Legacy
        // Website/release.config configures the membership provider with requiresUniqueEmail="false", so DotNetNuke did
        // NOT reject a duplicate email on create — the "UserEmailExists" status only fired when requiresUniqueEmail was
        // true. Rejecting duplicate emails unconditionally tightened behavior and violated exact migration parity
        // (AAP 0.7.2). When a migrated membership configuration sets unique-email = true, the guard can be reintroduced
        // behind that setting. Recorded in MIGRATION_NOTES.md.

        var user = _mapper.Map<User>(request);

        // MIGRATION: UserInfo.UpdateDisplayName(format) (Library/Components/Users/UserInfo.vb). DotNetNuke derived
        // DisplayName from the portal "Security_DisplayNameFormat" token mask before persisting (CP1 review
        // UserService #6). Ported via the IPortalSettingsService port (implemented in Infrastructure/Settings/PortalSettingsService.cs,
        // DI-registered Scoped), which resolves the portal "Site Settings" module and reads its [ModuleSettings] row.
        // When no display-name format is configured (no Site Settings module, or the setting is absent) DisplayName is
        // left exactly as supplied, matching the legacy "no format" path.
        await ApplyDisplayNameFormatAsync(user, request.PortalId, cancellationToken);

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

        // MIGRATION (CP1 review UserService #5 / Security #1) — CRITICAL credential lifecycle. Legacy CreateUser persisted
        // the user's password into aspnet_Membership (UserMembership.Password) so the account could authenticate; the
        // target MUST NOT create credentialless users. The inbound Password is used when supplied (CreateUserValidator
        // enforces its rules only When the password is non-empty); otherwise a cryptographically-random password is
        // generated so an admin-created account is still never credentialless. The plaintext is one-way hashed with
        // BCrypt (IPasswordHasher, AAP 0.7.6 — replaces the legacy DES) and persisted through the ICredentialStore port,
        // then committed by the same unit of work. This runs after the first SaveChanges so the database-generated
        // UserId is available to key the credential. (Out of scope for CP1: emailing a generated credential to the user.)
        var initialPassword = string.IsNullOrEmpty(request.Password)
            ? GenerateRandomPassword()
            : request.Password;
        var passwordHash = _passwordHasher.Hash(initialPassword);
        await _credentialStore.SetPasswordAsync(user.UserId, passwordHash, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: reload by the database-generated UserId (PORTAL-SCOPED, CP1 review #1) so the response's
        // UserRoles -> Role navigation is populated for the UserResponse.Roles projection. Falls back to the in-memory
        // entity if the reload returns null.
        var created = await _userRepository.GetByIdAsync(request.PortalId, user.UserId) ?? user;
        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(created));
    }

    /// <summary>
    /// Updates the editable profile/account fields of an existing user within a portal.
    /// </summary>
    /// <param name="portalId">The portal (tenant) that must own the user.</param>
    /// <param name="userId">The identity of the user to update (route-bound).</param>
    /// <param name="request">The update-user request payload.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>The updated user as a <see cref="UserResponse"/>, or a failure when no matching user exists in the portal.</returns>
    public async Task<Result<UserResponse>> UpdateAsync(int portalId, int userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP1 review UserService #3): fail-fast null guard before any request field is read.
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: UserController.UpdateUser(PortalId, objUser) L963 delegated to memberProvider.UpdateUser. CP1 review
        // (IUserService #1) — PORTAL-SCOPED ownership: the user is loaded constrained to portalId, so a user from another
        // portal can never be updated through this tenant's request (multi-tenant isolation, AAP 0.7.1). A missing/unowned
        // user is an expected failure (Api -> 404); CP1 review #7 — the message is OPAQUE (no raw id echoed).
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result<UserResponse>.Failure("The requested user was not found.");
        }

        // MIGRATION: UpdateUserRequest -> User applies the editable profile fields (FirstName/LastName/DisplayName/Email)
        // plus the admin-editable IsApproved/LockedOut flags; UserProfile Ignores the immutable identity (UserId/PortalId/
        // Username), derived members, and the UserRoles navigation. Profile-definition properties are DEFERRED — there is
        // no profile DTO in scope for this CRUD contract.
        _mapper.Map(request, user);

        // MIGRATION: UserController.UpdateUser also called UserInfo.UpdateDisplayName(format) using the portal
        // "Security_DisplayNameFormat" setting before persistence (CP1 review UserService #6). Re-derive DisplayName from
        // the mask here (no-op when the setting is unconfigured, which is the CP1 default).
        await ApplyDisplayNameFormatAsync(user, portalId, cancellationToken);

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(_mapper.Map<UserResponse>(user));
    }

    /// <summary>
    /// Deletes an existing user within a portal, refusing to delete the portal administrator.
    /// </summary>
    /// <param name="portalId">The portal (tenant) that must own the user.</param>
    /// <param name="userId">The identity of the user to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the persistence boundary.</param>
    /// <returns>
    /// A successful <see cref="Result"/> (mapped to HTTP 204 by the Api) when the user is deleted; a failure when the
    /// user does not exist in the portal or is the portal administrator.
    /// </returns>
    public async Task<Result> DeleteAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: UserController.DeleteUser(objUser, notify, deleteAdmin) L200. CP1 review (IUserService #1 /
        // IUserRepository #1) — PORTAL-SCOPED ownership: the user is loaded constrained to portalId so this tenant cannot
        // delete a user owned by another portal (multi-tenant isolation, AAP 0.7.1). A missing/unowned user is an expected
        // failure (Api -> 404); CP1 review #7 — the message is OPAQUE (no raw id echoed).
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result.Failure("The requested user was not found.");
        }

        // MIGRATION: UserController.DeleteUser L200 — CanDelete = True; reading the portal, If UserID =
        // PortalSettings.AdministratorId Then CanDelete = deleteAdmin. This DELETE endpoint exposes no deleteAdmin flag,
        // so it defaults to False -> the portal administrator cannot be deleted. (Legacy continued the delete only If
        // CanDelete.) The admin-guard is read from the same portal (portalId == user.PortalId after the scoped lookup).
        var portal = await _portalRepository.GetByIdAsync(portalId);
        if (portal is not null && user.UserId == portal.AdministratorId)
        {
            return Result.Failure("The portal administrator cannot be deleted.");
        }

        // MIGRATION: DeleteUser L200 also called DeleteFolderPermissionsByUserID and the module + tab permission cleanup
        // (ModulePermissionController/TabPermissionController) before deleting the user. The permission repositories are
        // not in scope for this phase, so explicit cleanup is DEFERRED here (EF cascade may apply once the relationships
        // are configured). Recorded in MIGRATION_NOTES.md.
        await _userRepository.DeleteAsync(portalId, userId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // MIGRATION (CP-final review - profile workflow parity): the canonical well-known DNN profile property names
    // (UserProfile.vb private constants cFirstName/cLastName/cCell/...). The flat UserProfileDto is projected to/from
    // these names; a portal that does not DEFINE a given name simply does not expose it (legacy GetProperty returned
    // Nothing and GetPropertyValue/SetProfileProperty were no-ops). Names are matched case-insensitively to mirror the
    // SQL Server default (case-insensitive) collation used by GetProfilePropertyDefinitionID.
    private const string ProfileFirstName = "FirstName";
    private const string ProfileLastName = "LastName";
    private const string ProfileCell = "Cell";
    private const string ProfileTelephone = "Telephone";
    private const string ProfileFax = "Fax";
    private const string ProfileIM = "IM";
    private const string ProfileStreet = "Street";
    private const string ProfileUnit = "Unit";
    private const string ProfileCity = "City";
    private const string ProfileRegion = "Region";
    private const string ProfileCountry = "Country";
    private const string ProfilePostalCode = "PostalCode";
    private const string ProfilePreferredLocale = "PreferredLocale";
    private const string ProfileTimeZone = "TimeZone";
    private const string ProfileWebsite = "Website";

    /// <summary>
    /// Reads a user's profile, projecting the EXISTING DNN profile EAV ([ProfilePropertyDefinition] +
    /// [UserProfile]) to the flat <see cref="UserProfileDto"/>. Replaces ProfileController.GetUserProfile +
    /// UserProfile.vb GetPropertyValue.
    /// </summary>
    public async Task<Result<UserProfileDto>> GetProfileAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: portal-scoped (AAP 0.7.1) - resolve the user within its owning portal first; a missing user is a
        // read failure (the controller maps it to 404 via HandleGet), mirroring GetUser returning Nothing.
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result<UserProfileDto>.Failure($"User '{userId}' was not found in portal '{portalId}'.");
        }

        var definitions = await _userRepository.GetProfileDefinitionsAsync(portalId);
        var defsByName = BuildDefinitionLookup(definitions);

        var values = await _userRepository.GetProfileValuesAsync(userId);
        var valuesByDefinition = new Dictionary<int, UserProfileValue>();
        foreach (var value in values)
        {
            valuesByDefinition[value.PropertyDefinitionId] = value;
        }

        // MIGRATION: UserProfile.vb GetPropertyValue(name): resolve the definition by name, return its stored value;
        // Null.NullString (-> null) when the portal does not define the property OR the user has no value row for it.
        string? Read(string propertyName)
            => defsByName.TryGetValue(propertyName, out var definition)
               && valuesByDefinition.TryGetValue(definition.PropertyDefinitionId, out var value)
                ? value.PropertyValue
                : null;

        var firstName = Read(ProfileFirstName);
        var lastName = Read(ProfileLastName);

        // MIGRATION: UserProfile.vb TimeZone getter parsed the stored string to an Integer, defaulting to
        // Null.NullInteger (-1) when unset. DIVERGENCE (documented, non-blocking): the legacy Integer.Parse THREW on a
        // non-numeric stored value; we use TryParse with the same -1 fallback so legacy dirty data cannot turn a
        // profile read into a 500. Recorded in MIGRATION_NOTES.md.
        var timeZoneRaw = Read(ProfileTimeZone);
        int timeZone = int.TryParse(timeZoneRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeZone)
            ? parsedTimeZone
            : -1;

        var dto = new UserProfileDto
        {
            FirstName = firstName,
            LastName = lastName,
            // MIGRATION: UserProfile.vb FullName getter = FirstName & " " & LastName (VERBATIM, no trim; Null.NullString
            // coalesces to empty), composed here in the Application layer per the DTO contract (no computation in the DTO).
            FullName = (firstName ?? string.Empty) + " " + (lastName ?? string.Empty),
            Cell = Read(ProfileCell),
            Telephone = Read(ProfileTelephone),
            Fax = Read(ProfileFax),
            IM = Read(ProfileIM),
            Street = Read(ProfileStreet),
            Unit = Read(ProfileUnit),
            City = Read(ProfileCity),
            Region = Read(ProfileRegion),
            Country = Read(ProfileCountry),
            PostalCode = Read(ProfilePostalCode),
            PreferredLocale = Read(ProfilePreferredLocale),
            TimeZone = timeZone,
            Website = Read(ProfileWebsite),
        };

        return Result<UserProfileDto>.Success(dto);
    }

    /// <summary>
    /// Updates a user's profile (upserts the EXISTING [UserProfile] value rows for each well-known property the
    /// portal defines), enforcing the legacy data-driven validation carried by each
    /// <see cref="ProfilePropertyDefinition"/> (Required / Length / ValidationExpression). Replaces
    /// ProfileController.UpdateUserProfile + UserProfile.vb SetProfileProperty.
    /// </summary>
    public async Task<Result<UserProfileDto>> UpdateProfileAsync(int portalId, int userId, UserProfileDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: portal-scoped (AAP 0.7.1) - the update is constrained to a user owned by portalId.
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result<UserProfileDto>.Failure($"User '{userId}' was not found in portal '{portalId}'.");
        }

        var definitions = await _userRepository.GetProfileDefinitionsAsync(portalId);
        var defsByName = BuildDefinitionLookup(definitions);

        var values = await _userRepository.GetProfileValuesAsync(userId);
        var valuesByDefinition = new Dictionary<int, UserProfileValue>();
        foreach (var value in values)
        {
            valuesByDefinition[value.PropertyDefinitionId] = value;
        }

        // MIGRATION: the flat DTO mapped back to the well-known property names. TimeZone is stored as its Integer's
        // string form (UserProfile.vb SetProfileProperty(cTimeZone, Value.ToString)).
        var incoming = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [ProfileFirstName] = request.FirstName,
            [ProfileLastName] = request.LastName,
            [ProfileCell] = request.Cell,
            [ProfileTelephone] = request.Telephone,
            [ProfileFax] = request.Fax,
            [ProfileIM] = request.IM,
            [ProfileStreet] = request.Street,
            [ProfileUnit] = request.Unit,
            [ProfileCity] = request.City,
            [ProfileRegion] = request.Region,
            [ProfileCountry] = request.Country,
            [ProfilePostalCode] = request.PostalCode,
            [ProfilePreferredLocale] = request.PreferredLocale,
            [ProfileTimeZone] = request.TimeZone.ToString(CultureInfo.InvariantCulture),
            [ProfileWebsite] = request.Website,
        };

        // MIGRATION: legacy profile validation was data-driven by the property definition (Required / Length /
        // ValidationExpression - DNN rendered Required/RegularExpression validators from these). Validate ALL provided
        // properties the portal defines BEFORE any write so a single failure does not leave a partial update.
        var validationErrors = new List<string>();
        foreach (var entry in incoming)
        {
            if (!defsByName.TryGetValue(entry.Key, out var definition))
            {
                // The portal does not define this property -> legacy no-op (SetProfileProperty did nothing).
                continue;
            }

            var candidate = entry.Value;

            if (definition.Required && string.IsNullOrEmpty(candidate))
            {
                validationErrors.Add($"{definition.PropertyName} is required.");
                continue;
            }

            if (definition.Length > 0 && candidate is not null && candidate.Length > definition.Length)
            {
                validationErrors.Add($"{definition.PropertyName} exceeds the maximum length of {definition.Length} characters.");
                continue;
            }

            if (!string.IsNullOrEmpty(definition.ValidationExpression) && !string.IsNullOrEmpty(candidate)
                && !MatchesValidationExpression(candidate, definition.ValidationExpression))
            {
                validationErrors.Add($"{definition.PropertyName} is not in a valid format.");
            }
        }

        if (validationErrors.Count > 0)
        {
            return Result<UserProfileDto>.Failure(validationErrors);
        }

        // MIGRATION: UpdateUserProfile upsert - for each well-known property the portal defines, update the existing
        // value row or insert a new one. Properties the portal does NOT define, and any custom (non-well-known)
        // definitions, are left untouched (the flat DTO only carries the well-known set). A user has at most one row
        // per definition (the unique (UserID, PropertyDefinitionID) index), so the lookup is exact.
        // MIGRATION: legacy stamped LastUpdatedDate via getdate() (server-local). Standardized on UtcNow here, consistent
        // with the [UserPortals] membership write (UserRepository.AddAsync); the column is not exposed on the DTO.
        var timestamp = DateTime.UtcNow;
        foreach (var entry in incoming)
        {
            if (!defsByName.TryGetValue(entry.Key, out var definition))
            {
                continue;
            }

            if (valuesByDefinition.TryGetValue(definition.PropertyDefinitionId, out var existing))
            {
                existing.PropertyValue = entry.Value;
                existing.LastUpdatedDate = timestamp;
            }
            else
            {
                await _userRepository.AddProfileValueAsync(new UserProfileValue
                {
                    UserId = userId,
                    PropertyDefinitionId = definition.PropertyDefinitionId,
                    PropertyValue = entry.Value,
                    Visibility = 0,
                    LastUpdatedDate = timestamp,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return the canonical persisted representation (re-projected from the EAV after commit).
        return await GetProfileAsync(portalId, userId, cancellationToken);
    }

    // MIGRATION: builds a case-insensitive PropertyName -> definition lookup over a portal's profile definitions
    // (mirrors the SQL Server default case-insensitive collation of GetProfilePropertyDefinitionID). Names are unique
    // per portal (the [ProfilePropertyDefinition] UNIQUE(PortalID, ModuleDefID, PropertyName) index), so the indexer
    // assignment cannot lose a distinct property.
    private static Dictionary<string, ProfilePropertyDefinition> BuildDefinitionLookup(IReadOnlyList<ProfilePropertyDefinition> definitions)
    {
        var lookup = new Dictionary<string, ProfilePropertyDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            lookup[definition.PropertyName] = definition;
        }

        return lookup;
    }

    // MIGRATION: evaluates a ProfilePropertyDefinition.ValidationExpression against a value (legacy DNN rendered an
    // ASP.NET RegularExpressionValidator from it). A 1-second match timeout guards against catastrophic backtracking
    // (ReDoS) on untrusted stored patterns; a malformed stored expression cannot be compiled, so - like the legacy
    // client-side validator that simply would not fire - it is treated as "no constraint" rather than blocking the save.
    private static bool MatchesValidationExpression(string value, string validationExpression)
    {
        try
        {
            return Regex.IsMatch(value, validationExpression, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // MIGRATION: UserInfo.UpdateDisplayName(format) (Library/Components/Users/UserInfo.vb). DotNetNuke applied the portal
    // "Security_DisplayNameFormat" token mask to derive DisplayName before persisting the user (CP1 review UserService #6).
    // The four legacy tokens are replaced VERBATIM ([USERID], [FIRSTNAME], [LASTNAME], [USERNAME]). The format is read
    // through the IPortalSettingsService port (implemented in Infrastructure as PortalSettingsService against the
    // legacy [ModuleSettings] table for the portal "Site Settings" module, DI-registered Scoped); when it is null/empty — the portal has no
    // "Site Settings" module, or the format setting is absent — DisplayName is left exactly as supplied, matching the legacy "no format"
    // path so runtime parity is preserved. (Null FirstName/LastName coalesce to empty, matching the legacy token removal.)
    private async Task ApplyDisplayNameFormatAsync(User user, int portalId, CancellationToken cancellationToken)
    {
        var format = await _portalSettingsService.GetSettingAsync(portalId, "Security_DisplayNameFormat", cancellationToken);
        if (string.IsNullOrEmpty(format))
        {
            return;
        }

        format = format.Replace("[USERID]", user.UserId.ToString());
        format = format.Replace("[FIRSTNAME]", user.FirstName ?? string.Empty);
        format = format.Replace("[LASTNAME]", user.LastName ?? string.Empty);
        format = format.Replace("[USERNAME]", user.Username);
        user.DisplayName = format;
    }

    // MIGRATION (CP1 review UserService #5 / Security #1): generates a cryptographically-strong random password for an
    // admin-created account that supplied no password, so the user is hashed-and-persisted and NEVER credentialless. The
    // legacy provider likewise never created a user without an aspnet_Membership password row.
    private static string GenerateRandomPassword()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
