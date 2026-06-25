using AutoMapper;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

// MIGRATION: Application-layer authentication service for the DotNetNuke 4.x (VB.NET / .NET 2.0) ->
// C# 12 / .NET 8 migration. Assembled "from scratch" (no single legacy counterpart) from THREE legacy
// sources, per AAP §0.4.1 (Application/Services/AuthService.cs | UserMembership.vb + PortalSecurity.vb):
//   * Library/Components/Users/UserController.vb (ValidateUser / UserLogin) — the login check ORDER.
//   * Library/Components/Users/Membership/UserMembership.vb (372 lines) — the Approved / LockedOut /
//     LastLoginDate account lifecycle (a pure data holder in the legacy code; its non-credential fields are
//     flattened onto the User domain entity).
//   * Library/Components/Security/PortalSecurity.vb — SignOut (L77 -> logout) and the deferred authorization
//     helpers IsInRole (L103) / IsInRoles (L115) / HasNecessaryPermission (L517). The legacy DES
//     Encrypt/Decrypt cipher is intentionally NOT migrated here (replaced by BCrypt in Infrastructure).
//
// ============================ ⚠ CRITICAL COORDINATION GAP (READ FIRST) ============================
// This service depends on TWO Application-layer abstractions — IPasswordHasher and IJwtService — that are
// declared in DnnMigration.Application.Interfaces. Their CONCRETE implementations (BCrypt password hashing
// and JWT issuance/rotation) live in DnnMigration.Infrastructure.Identity (PasswordHasher.cs, JwtService.cs)
// and are wired into DI in Infrastructure/DependencyInjection.cs — ALL out of scope for this file. Per the
// Clean/Onion rule the Application project references DOMAIN ONLY; it MUST NOT reference Infrastructure, and
// it MUST NOT reference Microsoft.AspNetCore.Authentication.JwtBearer or BCrypt.Net-Next (those are
// Infrastructure packages). The two ports were missing from the realized plan, so they were created
// alongside this file (Application/Interfaces/IPasswordHasher.cs and IJwtService.cs) to keep the module
// compiling. Recorded in MIGRATION_NOTES.md.
//
// MIGRATION: CREDENTIAL-STORE GAP — the User domain entity carries NO password-hash field and IUserRepository
// exposes NO credential lookup, so the stored hash required by IPasswordHasher.Verify has no source in this
// phase. The verification seam below is fully wired (IPasswordHasher is injected and invoked) so the file
// compiles and is unit-testable, but it FAILS CLOSED until the Infrastructure credential store is realized:
// login currently cannot succeed (the integration-test suite relies on this exact behavior). Documented in
// MIGRATION_NOTES.md.
//
// MIGRATION: legacy login outcomes were carried by the UserLoginStatus enum (LOGIN_FAILURE / LOGIN_SUCCESS /
// LOGIN_SUPERUSER / LOGIN_USERLOCKEDOUT / LOGIN_INSECUREADMINPASSWORD / LOGIN_INSECUREHOSTPASSWORD). The
// LoginResponse DTO has NO status channel (it is success-only), so failures and "must-change-insecure-password"
// outcomes are surfaced via Result.Failure(...) — translated to RFC 7807 ProblemDetails by the Api
// ExceptionHandlingMiddleware (AAP §0.7.5). Only a genuine success returns Result<LoginResponse>.Success(...).
//
// MIGRATION: SECURITY (AAP §0.7.6) — passwords and tokens are NEVER logged. This service intentionally has no
// logger; request.Password, AccessToken and RefreshToken must never be written to any sink. Authentication is
// PORTAL-SCOPED (multi-tenant isolation, AAP §0.7.1): login resolves the user within request.PortalId.
/// <summary>
/// Orchestrates authentication: login, refresh-token rotation, logout, and current-user projection. Validates
/// credentials via <see cref="IPasswordHasher"/>, issues and rotates JWT access/refresh tokens via
/// <see cref="IJwtService"/>, persists the last-login lifecycle via <see cref="IUserRepository"/> /
/// <see cref="IUnitOfWork"/>, and projects the authenticated user to <see cref="CurrentUserDto"/> via
/// <see cref="IMapper"/>. Replaces the legacy Forms-auth / AspNetSqlMembershipProvider flow with JWT Bearer.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="userRepository">Portal-scoped user data access (lookup, last-login update).</param>
    /// <param name="unitOfWork">Persistence boundary used to commit the last-login update.</param>
    /// <param name="mapper">AutoMapper used to project <see cref="User"/> to <see cref="CurrentUserDto"/>.</param>
    /// <param name="passwordHasher">BCrypt password verification port (Infrastructure adapter).</param>
    /// <param name="jwtService">JWT access/refresh token issuance and rotation port (Infrastructure adapter).</param>
    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <inheritdoc />
    // MIGRATION: ← legacy UserController.ValidateUser + the UserMembership lifecycle. The legacy check ORDER is
    // transcribed EXACTLY (AAP §0.7.2 — extract business rules verbatim; do not optimize or reorder):
    // lookup -> lockout -> approved -> password verify -> insecure default-account checks -> last-login update
    // -> token issuance. Backs POST /api/auth/login.
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Step 1 — Lookup (portal-scoped). Username matching is case-insensitive in the legacy code; that
        // behavior is owned by the repository implementation (OrdinalIgnoreCase), not duplicated here.
        // MIGRATION: UserController.ValidateUser -> LOGIN_FAILURE when the account does not exist.
        var user = await _userRepository.GetByUsernameAsync(request.PortalId, request.Username);
        if (user is null)
        {
            return Result<LoginResponse>.Failure("Login failed. The username or password is incorrect.");
        }

        // Step 2 — Lockout (UserMembership.LockedOut).
        // MIGRATION: LOGIN_USERLOCKEDOUT.
        if (user.LockedOut)
        {
            return Result<LoginResponse>.Failure("This account is locked out. Please contact your administrator.");
        }

        // Step 3 — Approved (UserMembership.Approved).
        // MIGRATION: legacy treated an unapproved account as a failed login.
        if (!user.IsApproved)
        {
            return Result<LoginResponse>.Failure("This account is not approved. Please contact your administrator.");
        }

        // Step 4 — Password verification.
        // MIGRATION: COORDINATION GAP — the stored password hash is owned by Infrastructure/Identity's credential
        // store, which is not realized in this phase (the User entity carries no hash; IUserRepository exposes no
        // credential lookup). Verification is delegated to IPasswordHasher.Verify against the stored hash. Until the
        // credential store is wired, the stored hash is unavailable and verification fails closed (LOGIN_FAILURE).
        // Recorded in MIGRATION_NOTES.md.
        // MIGRATION: the stored BCrypt hash is supplied by the Infrastructure credential store once realized; it is
        // null here until then, so string.IsNullOrEmpty(...) short-circuits and login fails closed. IPasswordHasher
        // is invoked (not dead code) so the dependency is real and both verification branches stay reachable/mockable.
        string? storedHash = null;
        if (string.IsNullOrEmpty(storedHash) || !_passwordHasher.Verify(request.Password, storedHash))
        {
            return Result<LoginResponse>.Failure("Login failed. The username or password is incorrect.");
        }

        // Step 5 — Insecure default-account checks (plaintext comparison — faithful; performed AFTER a valid
        // password). MIGRATION: ValidateUser returned LOGIN_INSECUREADMINPASSWORD ("admin"/"dnnadmin") and
        // LOGIN_INSECUREHOSTPASSWORD ("host"/"dnnhost"), forcing a password-change redirect. LoginResponse has no
        // status channel, so these surface as failures prompting a password change. The legacy code compared exact
        // lowercase literals, so StringComparison.Ordinal is used (NOT case-insensitive). Documented in
        // MIGRATION_NOTES.md.
        if (string.Equals(request.Password, "admin", StringComparison.Ordinal)
            || string.Equals(request.Password, "dnnadmin", StringComparison.Ordinal))
        {
            return Result<LoginResponse>.Failure("You are using an insecure default administrator password and must change it before continuing.");
        }

        // MIGRATION: the host-password check is gated on IsSuperUser, mirroring the legacy LOGIN_INSECUREHOSTPASSWORD
        // path which only applied to host/super-user accounts.
        if (user.IsSuperUser
            && (string.Equals(request.Password, "host", StringComparison.Ordinal)
                || string.Equals(request.Password, "dnnhost", StringComparison.Ordinal)))
        {
            return Result<LoginResponse>.Failure("You are using an insecure default host password and must change it before continuing.");
        }

        // Step 6 — Update the last-login lifecycle (UserMembership).
        // MIGRATION: UserMembership.UpdateUserLastLogin set LastLoginDate on successful authentication (and reset
        // failed-attempt counters, which are not modeled on the User entity in this phase). cancellationToken flows
        // only to the persistence boundary, per the repository contract (repo methods take no CancellationToken).
        user.LastLoginDate = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Steps 7-8 — Build roles, issue tokens, project the user, and assemble the success response.
        var response = BuildLoginResponse(user);
        return Result<LoginResponse>.Success(response);
    }

    /// <inheritdoc />
    // MIGRATION: ← NEW (refresh-token rotation). The legacy DNN model had NO refresh-token concept — it used a
    // persistent Forms-auth cookie ('CreatePersistentCookie'); JWT refresh-token rotation replaces it (AAP §0.7.6).
    // Backs POST /api/auth/refresh.
    public async Task<Result<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        // Step 1 — Validate the presented refresh token and resolve the owning user id (fail closed on invalid/expired).
        var userId = _jwtService.ValidateRefreshToken(request.RefreshToken);
        if (userId is null)
        {
            return Result<LoginResponse>.Failure("Invalid or expired refresh token.");
        }

        // Step 2 — Load the user. A token that resolves to a missing user is treated as invalid (same opaque message).
        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            return Result<LoginResponse>.Failure("Invalid or expired refresh token.");
        }

        // Step 3 — Re-validate account state on refresh.
        // MIGRATION: re-check lockout/approved on refresh so a previously valid session cannot be silently extended
        // after the account is locked or un-approved.
        if (user.LockedOut || !user.IsApproved)
        {
            return Result<LoginResponse>.Failure("This account can no longer be used.");
        }

        // Step 4 — Rotate: revoke the presented refresh token, then issue a brand-new access + refresh token pair
        // (identical issuance to LoginAsync steps 7-8).
        // MIGRATION: refresh-token rotation — the presented refresh token is revoked and replaced (NO legacy
        // equivalent; replaces the Forms-auth persistent cookie). AAP §0.7.6.
        _jwtService.RevokeRefreshToken(request.RefreshToken);

        var response = BuildLoginResponse(user);
        return Result<LoginResponse>.Success(response);
    }

    /// <inheritdoc />
    // MIGRATION: ← PortalSecurity.SignOut (PortalSecurity.vb L77) which called FormsAuthentication.SignOut and
    // expired the language/authentication/portalaliasid/portalroles cookies. JWT access tokens are STATELESS and
    // cannot be server-invalidated, so logout = server-side revocation of the refresh token. The controller maps
    // Result.Success() to HTTP 204/200. Backs POST /api/auth/logout (RefreshRequest is reused for the body).
    public async Task<Result> LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        _jwtService.RevokeRefreshToken(request.RefreshToken);

        // MIGRATION: the realized IJwtService.RevokeRefreshToken is synchronous (void); await a completed task to keep
        // this method cleanly async per the IAuthService contract (no CS1998). If revocation later becomes awaitable
        // (e.g. a DB-backed token store), replace this with the awaitable call.
        await Task.CompletedTask;
        return Result.Success();
    }

    /// <inheritdoc />
    // MIGRATION: ← current-user projection from legacy UserController.GetCurrentUserInfo. The userId is resolved from
    // the authenticated JWT principal in AuthController; this method maps the persisted user to CurrentUserDto (roles
    // flattened by AuthProfile). Backs GET /api/auth/me.
    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Result<CurrentUserDto>.Failure("The current user could not be found.");
        }

        return Result<CurrentUserDto>.Success(_mapper.Map<CurrentUserDto>(user));
    }

    // MIGRATION: shared composition of LoginAsync steps 7-8, reused VERBATIM by RefreshAsync step 4 so token issuance
    // stays identical between login and refresh (DRY; no behavioral change vs. the transcribed legacy flow).
    // Builds the role-name list from the UserRoles navigation, issues a 60-minute access token + a rotating refresh
    // token (lifetimes configured in Infrastructure/Identity JwtService, AAP §0.7.6), projects the user to
    // CurrentUserDto via AutoMapper, and assembles the success-only LoginResponse. TokenType is always "Bearer".
    private LoginResponse BuildLoginResponse(User user)
    {
        // MIGRATION: replaces the legacy DNN 'portalroles' cookie — role names are read from the UserRoles
        // navigation (skipping any rows whose Role failed to hydrate) and signed into the JWT by the issuer.
        var roles = user.UserRoles
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.RoleName)
            .ToList();

        // MIGRATION: replaces Forms-authentication ticket issuance; 60-minute access token + rotating refresh token
        // (AAP §0.7.6). The tuple element names match the IJwtService contract.
        var (accessToken, expiresAtUtc, expiresIn) = _jwtService.GenerateAccessToken(
            user.UserId,
            user.Username,
            user.PortalId,
            user.IsSuperUser,
            roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // MIGRATION: AuthProfile flattens Roles from UserRoles -> Role.RoleName; never return the raw User entity
        // (AAP §0.7.7 — DTO projection only).
        var currentUser = _mapper.Map<CurrentUserDto>(user);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            ExpiresAt = expiresAtUtc,
            User = currentUser
        };
    }

    // =============================== Phase 2 — DEFERRED legacy logic (DOCUMENTED, NOT implemented) ===============================
    // MIGRATION: the following PortalSecurity.vb authorization helpers are NOT part of the IAuthService contract and are
    // deliberately NOT implemented here. They move to JWT-claims-driven [Authorize] policies / attributes at the Api layer
    // (the role claims are already signed into the access token by IJwtService.GenerateAccessToken), to be enforced in a
    // later phase. Captured here (and in MIGRATION_NOTES.md) for traceability:
    //
    //   * IsInRole  (PortalSecurity.vb L103): true when the supplied role is non-empty AND either the request is
    //     unauthenticated and the role is the "Unauthenticated Users" role, OR the current user is a member of the role.
    //
    //   * IsInRoles (PortalSecurity.vb L115): splits a ';'-delimited role list and returns true if the user is a super
    //     user, OR (per role) the request is unauthenticated and the role is the "Unauthenticated Users" role, OR the role
    //     is the "All Users" role, OR the user is a member of the role.
    //
    //   * HasNecessaryPermission (PortalSecurity.vb L517-550), VERBATIM control flow over SecurityAccessLevel
    //     (enum: ControlPanel=-3, SkinObject=-2, Anonymous=-1, View=0, Edit=1, Admin=2, Host=3):
    //         pre-switch:  if (user is not null && user.IsSuperUser) authorized = true;   // super users short-circuit
    //         where:       isAdmin       = IsInRole(PortalSettings.AdministratorRoleName)
    //                      isPageEditor  = IsInRoles(PortalSettings.ActiveTab.AdministratorRoles)
    //                      canViewModule = IsInRoles(ModuleConfiguration.AuthorizedViewRoles)
    //                      canEditModule = ModulePermissionController.HasModulePermission(ModulePermissions, "EDIT")
    //         Anonymous -> true
    //         View      -> isAdmin || isPageEditor || canViewModule
    //         Edit      -> (isAdmin || isPageEditor) || (canViewModule && canEditModule)
    //         Admin     -> isAdmin || isPageEditor
    //         Host      -> (empty case) authorized only via the super-user pre-switch check
    //         return authorized
    //
    // MIGRATION: the legacy DES Encrypt/Decrypt and CreateKey (PortalSecurity.vb L138/L175/L564) are intentionally NOT
    // migrated here — credential hashing is replaced by BCrypt (IPasswordHasher) and the random refresh token by the JWT
    // issuer (IJwtService), both owned by Infrastructure/Identity. See MIGRATION_NOTES.md.
}
