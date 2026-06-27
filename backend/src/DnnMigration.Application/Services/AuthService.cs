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
//   * Library/Components/Security/PortalSecurity.vb — SignOut (L77 -> logout) and the (now implemented, see PermissionEvaluator below) authorization
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
// MIGRATION: CREDENTIAL-STORE — the User domain entity carries NO password-hash field and IUserRepository exposes
// NO credential lookup, so the stored hash required by IPasswordHasher.Verify is sourced from the ICredentialStore
// port (Application/Interfaces/ICredentialStore.cs) — the SAME port UserService.CreateAsync writes the initial hash
// through (CP1 review UserService #5), so the create->verify credential lifecycle is coherent end-to-end. The
// concrete adapter is owned by Infrastructure (Infrastructure/Identity/CredentialStore.cs) and maps onto the
// EXISTING legacy membership schema (aspnet_Users + aspnet_Membership; AAP 0.7.1 - no new table), storing a one-way
// BCrypt hash in aspnet_Membership.Password. When a user has NO persisted credential GetPasswordHashAsync returns
// null, so login still FAILS CLOSED (the IsNullOrEmpty(storedHash) guard short-circuits before IPasswordHasher.Verify)
// - the fail-closed behavior the CP1 review accepted (AAP matrix #10). Documented in MIGRATION_NOTES.md.
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
    private readonly ICredentialStore _credentialStore;
    private readonly IJwtService _jwtService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="userRepository">Portal-scoped user data access (lookup, last-login update).</param>
    /// <param name="unitOfWork">Persistence boundary used to commit the last-login update.</param>
    /// <param name="mapper">AutoMapper used to project <see cref="User"/> to <see cref="CurrentUserDto"/>.</param>
    /// <param name="passwordHasher">BCrypt password verification port (Infrastructure adapter).</param>
    /// <param name="credentialStore">Stored password-hash retrieval port; the source of the hash verified at login.</param>
    /// <param name="jwtService">JWT access/refresh token issuance and rotation port (Infrastructure adapter).</param>
    // MIGRATION: CP1 review (AuthService #1) — fail fast on DI misconfiguration. Every injected dependency is
    // null-guarded with ArgumentNullException.ThrowIfNull before assignment, consistent with PortalService,
    // UserService, RoleService, ModuleService and TabService, so a missing registration surfaces here at construction
    // rather than as a later NullReferenceException.
    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPasswordHasher passwordHasher,
        ICredentialStore credentialStore,
        IJwtService jwtService)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(jwtService);

        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _credentialStore = credentialStore;
        _jwtService = jwtService;
    }

    /// <inheritdoc />
    // MIGRATION: ← legacy UserController.ValidateUser + the UserMembership lifecycle. The legacy check ORDER is
    // transcribed EXACTLY (AAP §0.7.2 — extract business rules verbatim; do not optimize or reorder):
    // lookup -> lockout -> approved -> password verify -> insecure default-account checks -> last-login update
    // -> token issuance. Backs POST /api/auth/login.
    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (AuthService #2) — guard the request before any field access (request.PortalId /
        // request.Username) so a malformed/absent body fails fast rather than throwing a NullReferenceException deep
        // in the login flow.
        ArgumentNullException.ThrowIfNull(request);

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

        // Step 3 — Approved / verification-code branch (UserMembership.Approved).
        // MIGRATION: <- AspNetMembershipProvider.UserLogin L1465-1477, transcribed VERBATIM (AAP §0.7.2). The branch
        // applies ONLY to NON-superusers (legacy "Approved = False And IsSuperUser = False"); an unapproved SUPERUSER
        // is NOT blocked here and proceeds to credential verification. For an unapproved non-superuser, when the
        // supplied verification code matches the legacy "{portalId}-{userId}" pattern the account is approved and
        // PERSISTED (UpdateUser) before credential verification, then login continues; otherwise it fails
        // (LOGIN_USERNOTAPPROVED). Earlier this step failed ANY unapproved account (including superusers) and omitted
        // the verification-code path (CP1 review AuthService #3).
        if (!user.IsApproved && !user.IsSuperUser)
        {
            // MIGRATION: L1468 — exact legacy concatenation portalId.ToString & "-" & user.UserID. The INBOUND portalId
            // (request.PortalId) is used, matching the legacy parameter. Ordinal comparison (Option Compare Binary); a
            // null/missing VerificationCode never matches and falls through to the not-approved failure below.
            if (string.Equals(request.VerificationCode, $"{request.PortalId}-{user.UserId}", StringComparison.Ordinal))
            {
                // MIGRATION: L1470-1473 — approve and PERSIST before credential verification. This is the exact legacy
                // behavior (the approval is committed even if the password later fails); preserved verbatim, not "fixed".
                user.IsApproved = true;
                await _userRepository.UpdateAsync(user);
                // MIGRATION (CP-FINAL review - Critical #2 "auth approval state persist"): User.IsApproved is
                // Ignore()d on the [Users] mapping because it physically lives in [aspnet_Membership]; a plain
                // UserRepository.UpdateAsync(user) therefore does NOT persist it. Persist the approval onto the
                // existing membership row through the credential port, staged into the SAME unit of work as the User
                // update so both commit atomically on the SaveChanges below.
                await _credentialStore.SetApprovedAsync(user.UserId, true, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // MIGRATION: L1475 — LOGIN_USERNOTAPPROVED. Surfaced as a failure (LoginResponse has no status channel).
                return Result<LoginResponse>.Failure("This account is not approved. Please contact your administrator.");
            }
        }

        // Step 4 — Password verification.
        // MIGRATION: <- ValidateUser credential check. The stored BCrypt hash is retrieved through the ICredentialStore
        // port — the SAME port UserService.CreateAsync persists the initial hash through (CP1 review UserService #5) —
        // and the presented plaintext is verified against it with IPasswordHasher.Verify, replacing the legacy DES
        // decrypt-then-compare (PortalSecurity.vb) with a one-way BCrypt comparison (AAP §0.7.6). The concrete
        // credential-store adapter is owned by Infrastructure and maps onto the EXISTING legacy membership schema
// (aspnet_Users + aspnet_Membership; AAP 0.7.1 - no new table). When the user has no persisted credential
// GetPasswordHashAsync returns null, so the IsNullOrEmpty(storedHash) guard short-circuits and login FAILS CLOSED
// (LOGIN_FAILURE) - the fail-closed behavior the CP1 review accepted (AAP matrix #10).
// Recorded in MIGRATION_NOTES.md.
        string? storedHash = await _credentialStore.GetPasswordHashAsync(user.UserId, cancellationToken);
        if (string.IsNullOrEmpty(storedHash) || !_passwordHasher.Verify(request.Password, storedHash))
        {
            return Result<LoginResponse>.Failure("Login failed. The username or password is incorrect.");
        }

        // Step 5 — Insecure default-account checks (plaintext comparison — faithful; performed AFTER a valid
        // password). MIGRATION: <- UserController.ValidateUser L1144-1153, transcribed VERBATIM (AAP §0.7.2).
        // LOGIN_INSECUREADMINPASSWORD and LOGIN_INSECUREHOSTPASSWORD forced a password-change redirect; LoginResponse
        // has no status channel, so these surface as failures prompting a password change. All legacy comparisons are
        // Option Compare Binary (case-sensitive) => StringComparison.Ordinal, and the username compared is the INBOUND
        // request.Username, matching the legacy parameter. Documented in MIGRATION_NOTES.md.
        // MIGRATION: CP1 review (AuthService #4) — the admin check requires BOTH a NON-superuser success (legacy
        // "If loginStatus = LOGIN_SUCCESS") AND Username = "admin". Both predicates were missing (the check fired on the
        // password alone, regardless of username), so they are restored: a non-"admin" account using those passwords is
        // no longer wrongly blocked.
        if (!user.IsSuperUser
            && string.Equals(request.Username, "admin", StringComparison.Ordinal)
            && (string.Equals(request.Password, "admin", StringComparison.Ordinal)
                || string.Equals(request.Password, "dnnadmin", StringComparison.Ordinal)))
        {
            return Result<LoginResponse>.Failure("You are using an insecure default administrator password and must change it before continuing.");
        }

        // MIGRATION: CP1 review (AuthService #4) — the host check requires a SUPERUSER success (legacy
        // "If loginStatus = LOGIN_SUPERUSER") AND Username = "host". The IsSuperUser gate was already present but the
        // Username = "host" predicate was missing, so it is restored: a superuser whose username is not "host" using
        // those passwords is no longer wrongly blocked.
        if (user.IsSuperUser
            && string.Equals(request.Username, "host", StringComparison.Ordinal)
            && (string.Equals(request.Password, "host", StringComparison.Ordinal)
                || string.Equals(request.Password, "dnnhost", StringComparison.Ordinal)))
        {
            return Result<LoginResponse>.Failure("You are using an insecure default host password and must change it before continuing.");
        }

        // Step 6 — Update the last-login lifecycle (UserMembership).
        // MIGRATION: UserMembership.UpdateUserLastLogin set LastLoginDate on successful authentication (and reset
        // failed-attempt counters, which are not modeled on the User entity in this phase). cancellationToken flows
        // only to the persistence boundary, per the repository contract (repo methods take no CancellationToken).
        // MIGRATION (CP-FINAL review - Critical #2 "auth last-login state persist"): User.LastLoginDate is Ignore()d
        // on the [Users] mapping because it physically lives in [aspnet_Membership]; the plain User update below does
        // NOT persist it. Capture ONE timestamp, set it on the User (kept for the in-memory login projection) and
        // persist it onto the existing membership row via the credential port - staged into the SAME unit of work so
        // both commit atomically on the SaveChanges below (faithful to UserMembership.UpdateUserLastLogin).
        var loginTimeUtc = DateTime.UtcNow;
        user.LastLoginDate = loginTimeUtc;
        await _userRepository.UpdateAsync(user);
        await _credentialStore.RecordLoginAsync(user.UserId, loginTimeUtc, cancellationToken);
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
        // MIGRATION: CP1 review (AuthService #5) — guard the request before reading request.RefreshToken so a
        // malformed/absent body fails closed rather than throwing a NullReferenceException. Required-token shape is
        // additionally enforced by RefreshRequestValidator at the Api boundary.
        ArgumentNullException.ThrowIfNull(request);

        // Step 1 — Validate the presented refresh token and resolve its TENANT-BOUND identity (user + portal),
        // failing closed on invalid/expired/revoked tokens.
        var tokenInfo = _jwtService.ValidateRefreshToken(request.RefreshToken);
        if (tokenInfo is null)
        {
            return Result<LoginResponse>.Failure("Invalid or expired refresh token.");
        }

        // Step 2 — Load the user. A token that resolves to a missing user is treated as invalid (same opaque message).
        // MIGRATION: CP1 review (IJwtService #1 / AuthService #6) — the refresh token is TENANT-BOUND, and the lookup is
        // now PORTAL-SCOPED: GetByIdAsync(tokenInfo.PortalId, tokenInfo.UserId) so a token issued for one portal can never
        // resolve a user in another portal (multi-tenant isolation, AAP §0.7.1). Both the user id AND the portal id from
        // the validated token must match an existing user, or the refresh fails closed with the opaque message.
        var user = await _userRepository.GetByIdAsync(tokenInfo.PortalId, tokenInfo.UserId);
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
        // MIGRATION: CP1 review (AuthService #5) — guard the request before reading request.RefreshToken. Logout is
        // idempotent (RevokeRefreshToken is a documented no-op for an unknown/empty token), but a null body must not
        // throw a NullReferenceException.
        ArgumentNullException.ThrowIfNull(request);

        _jwtService.RevokeRefreshToken(request.RefreshToken);

        // MIGRATION: the realized IJwtService.RevokeRefreshToken is synchronous (void); await a completed task to keep
        // this method cleanly async per the IAuthService contract (no CS1998). If revocation later becomes awaitable
        // (e.g. a DB-backed token store), replace this with the awaitable call.
        await Task.CompletedTask;
        return Result.Success();
    }

    /// <inheritdoc />
    // MIGRATION: ← current-user projection from legacy UserController.GetCurrentUserInfo. The userId AND portalId are
    // resolved from the authenticated JWT principal in AuthController; this method maps the persisted user to
    // CurrentUserDto (roles flattened by AuthProfile). Backs GET /api/auth/me.
    // MIGRATION: CP1 review (AuthService #6 / IUserService #1) — PORTAL-SCOPED: the lookup carries portalId so the
    // projection is consistent with the token's tenant claim and a principal cannot read a user outside its portal
    // (multi-tenant isolation, AAP §0.7.1).
    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(int portalId, int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(portalId, userId);
        if (user is null)
        {
            return Result<CurrentUserDto>.Failure("The current user could not be found.");
        }

        return Result<CurrentUserDto>.Success(_mapper.Map<CurrentUserDto>(user));
    }

    /// <inheritdoc />
    // MIGRATION: legacy SendPassword.ascx.vb cmdSendPassword_Click. The legacy flow looked the user up
    // (GetUser: by email when RequiresUniqueEmail, else by username) and, if password retrieval was enabled, sent the
    // password via Mail.SendMail(MessageType.PasswordReminder). Two legacy facets are deliberately NOT reproduced:
    //   (1) "send the actual password" is IMPOSSIBLE by design under BCrypt (one-way hashing, AAP 0.7.6) - only a
    //       reset flow is meaningful; and
    //   (2) email dispatch is the Services.Mail / Messaging subsystem, OUT OF SCOPE per AAP 0.6.2.
    // The IN-SCOPE work performed here is: input validation (the registered FluentValidation validator), a
    // PORTAL-SCOPED user lookup (AAP 0.7.1), and a SECURE generic response. The lookup is performed UNCONDITIONALLY
    // and its outcome is intentionally NOT branched on, so the response - and the work/timing - are identical whether
    // or not a matching account exists (defends against account enumeration AND timing oracles). In a deployment with
    // the (excluded) mail subsystem enabled, a reset email would be enqueued for a found account at this point.
    // Documented in MIGRATION_NOTES.md. Backs POST /api/auth/forgot-password (rate-limited via the "auth" policy).
    public async Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: SendPassword.GetUser() - portal-scoped account resolution. Performed unconditionally; the result
        // is intentionally discarded so the caller-visible response never depends on whether the account exists.
        _ = await _userRepository.GetByUsernameAsync(request.PortalId, request.UsernameOrEmail);

        return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse
        {
            Message = "If an account matching the supplied details exists, instructions to reset the password have been sent to its registered email address.",
        });
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
        // MIGRATION: CP1 review (IJwtService #1) — issue a TENANT-BOUND refresh token (user + portal) so the refresh
        // flow can enforce portal isolation. user.PortalId is the authenticated tenant for this session (it is the
        // portal the login lookup was scoped to, and the portal a refreshed user was loaded from).
        var refreshToken = _jwtService.GenerateRefreshToken(user.UserId, user.PortalId);

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

    // =============================== Authorization helpers - IMPLEMENTED (PermissionEvaluator) ===============================
    // MIGRATION: the PortalSecurity.vb authorization helpers below are NOT part of the IAuthService contract (login /
    // refresh / logout / current-user). They are implemented as a reusable, injectable Application service -
    // IPermissionEvaluator / PermissionEvaluator (Application/Interfaces/IPermissionEvaluator.cs +
    // Application/Services/PermissionEvaluator.cs) - registered in DI (Program.cs, AddSingleton) and driven by an
    // explicit SecurityContext that the Api builds from the authenticated JWT principal via
    // ClaimsPrincipal.ToSecurityContext() (Api/Authorization/ClaimsPrincipalSecurityExtensions.cs). The role claims are
    // signed into the access token by IJwtService.GenerateAccessToken, so resource-level permission checks no longer
    // depend on ambient HttpContext / UserController. The coarse Host/PortalAdministrator [Authorize] route policies
    // remain for endpoint-level authorization. The transcribed logic (covered by PermissionEvaluatorTests) is:
    //
    //   * IsInRole  (PortalSecurity.vb L103): true when the supplied role is non-empty AND either the request is
    //     unauthenticated and the role is the "Unauthenticated Users" role, OR the current user is a member of the role.
    //
    //   * IsInRoles (PortalSecurity.vb L115): splits a ';'-delimited role list and returns true if the user is a super
    //     user, OR (per role) the request is unauthenticated and the role is the "Unauthenticated Users" role, OR the role
    //     is the "All Users" role, OR the user is a member of the role.
    //
    //   * HasNecessaryPermission (PortalSecurity.vb L517-549), VERBATIM control flow over SecurityAccessLevel
    //     (enum: ControlPanel=-3, SkinObject=-2, Anonymous=-1, View=0, Edit=1, Admin=2, Host=3):
    //         pre-switch:  if (context.IsSuperUser) authorized = true;   // super users short-circuit
    //         where:       isAdmin       = IsInRole(PortalSettings.AdministratorRoleName)
    //                      isPageEditor  = IsInRoles(PortalSettings.ActiveTab.AdministratorRoles)
    //                      canViewModule = IsInRoles(ModuleConfiguration.AuthorizedViewRoles)
    //                      canEditModule = HasModulePermission(ModulePermissions, "EDIT")
    //         Anonymous -> true
    //         View      -> isAdmin || isPageEditor || canViewModule
    //         Edit      -> (isAdmin || isPageEditor) || (canViewModule && canEditModule)
    //         Admin     -> isAdmin || isPageEditor
    //         Host      -> (empty case) authorized only via the super-user pre-switch check
    //         return authorized
    //
    // MIGRATION: the legacy DES Encrypt/Decrypt and CreateKey (PortalSecurity.vb L138/L175/L564) are intentionally NOT
    // migrated - credential hashing is replaced by BCrypt (IPasswordHasher) and the random refresh token by the JWT
    // issuer (IJwtService), both owned by Infrastructure/Identity. See MIGRATION_NOTES.md.
}
