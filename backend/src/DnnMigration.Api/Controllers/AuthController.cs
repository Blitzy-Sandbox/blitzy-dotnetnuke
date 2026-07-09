using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Authentication endpoints for the DnnMigration Backend-for-Frontend (BFF), exposed under the
/// <c>/api/auth</c> route: <c>POST login</c>, <c>POST refresh</c>, <c>POST logout</c>, and
/// <c>GET me</c>.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy DotNetNuke Forms-Authentication sign-in flow and the
/// <c>PortalSecurity</c> DES / <c>SecurityAccessLevel</c> model
/// (<c>Library/Components/Security/PortalSecurity.vb</c>) together with the Web Forms
/// password-reminder screen (<c>Website/admin/Security/SendPassword.ascx.vb</c>,
/// <c>cmdSendPassword_Click</c> L160). The stateful cookie/postback model
/// (<c>PortalSecurity.UserLogin</c> L632, DES <c>Encrypt</c>/<c>Decrypt</c> L138/L175, and
/// <c>FormsAuthentication.SignOut</c> L77-L79) is replaced by a stateless JWT bearer scheme: the API
/// issues an access/refresh token pair on login and the Angular SPA attaches the access token as an
/// <c>Authorization: Bearer</c> header on subsequent calls (AAP §0.6.4). The legacy
/// <c>SendPassword</c> password-reminder screen is subsumed by this token-based authentication flow
/// (a dedicated password-reset endpoint is intentionally out of scope for the core parity surface).
/// </para>
/// <para>
/// This controller is a THIN delegator (AAP §0.7.1 "no business logic in API controllers"). All
/// token issuance/validation, password verification (BCrypt), and claim shaping live in
/// <see cref="IAuthService"/> (Application), backed by the JWT/BCrypt identity components in
/// Infrastructure. The controller's only responsibilities are HTTP concerns: model binding, routing,
/// status-code selection, and shaping successful results through the inherited
/// <c>{ "data": ..., "meta": ... }</c> envelope helper exposed by <see cref="ApiControllerBase"/>. It
/// performs no token creation/validation, no password hashing, and touches no <c>DbContext</c>,
/// repository, or EF entity. Error bodies (including the 401 returned here) are produced centrally as
/// RFC 7807 Problem Details by the exception-handling middleware plus <c>AddProblemDetails()</c>; the
/// controller never hand-formats error JSON, and request correlation IDs are attached by
/// <c>CorrelationIdMiddleware</c> in the pipeline (not per action).
/// </para>
/// <para>
/// MIGRATION: the DNN <c>SecurityAccessLevel</c> ladder (Anonymous / View / Edit / Admin / Host)
/// collapses onto ASP.NET Core authorization attributes. The class is decorated with
/// <see cref="AuthorizeAttribute"/> so the resource is secure by default (the View/Edit/Admin/Host
/// levels); the credential-exchanging actions (<see cref="Login"/>, <see cref="Refresh"/>, and
/// <see cref="Logout"/>) opt back out with <see cref="AllowAnonymousAttribute"/> (the former
/// "Anonymous" level). Login and Refresh are anonymous because a caller cannot present a bearer token
/// before it has obtained one; Logout is anonymous because it is keyed off the refresh token in its
/// request body (the SPA interceptor never attaches a bearer to the auth-flow routes, and the access
/// token may be expired at logout), so it revokes by that token rather than by the bearer principal.
/// Only <see cref="Me"/> remains authorized (it reads the presented principal).
/// </para>
/// <para>
/// MIGRATION: the authentication actions are additionally rate-limited via
/// <see cref="EnableRateLimitingAttribute"/> bound to the fixed-window <c>"auth"</c> policy
/// registered in <c>Program.cs</c> (AAP §0.7.1 "rate limiting on authentication endpoints"),
/// mitigating credential-stuffing / brute-force attempts against the login and refresh surface that
/// the legacy stack left ungated.
/// </para>
/// </remarks>
[ApiController]
// MIGRATION: literal "api/auth" route (NOT "api/[controller]") so the auth surface is stated
// explicitly and cannot be affected by token-replacement ambiguity across the sibling controllers.
[Route("api/auth")]
// MIGRATION: secure-by-default. Replaces the PortalSecurity.SecurityAccessLevel (View/Edit/Admin/
// Host) gate; the anonymous actions opt out individually with [AllowAnonymous] below.
[Authorize]
// MIGRATION: the fixed-window "auth" limiter (registered in Program.cs) throttles the login/refresh
// surface — AAP §0.7.1 rate limiting on authentication endpoints.
[EnableRateLimiting("auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authService">
    /// The application authentication service that owns all token issuance/validation, credential
    /// verification (BCrypt), and claim shaping. Supplied by the built-in DI container.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="authService"/> is <see langword="null"/>.
    /// </exception>
    public AuthController(IAuthService authService)
    {
        // MIGRATION: the legacy PortalSecurity / UserController authentication entry points were
        // Public Shared (static) members that reached provider membership and data directly; the
        // collaborator is now an injected instance supplied by DI (no statics in application code —
        // AAP §0.6.1). Assigning the field here guarantees it is non-null (no CS8618).
        ArgumentNullException.ThrowIfNull(authService);
        _authService = authService;
    }

    /// <summary>
    /// Authenticates a set of credentials and, on success, issues a JWT access/refresh token pair.
    /// </summary>
    /// <param name="dto">The login payload (username, password, and optional portal id).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the token pair wrapped in the success envelope when the credentials are valid;
    /// HTTP 401 (Unauthorized) when authentication fails.
    /// </returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: PortalSecurity.UserLogin (L632) delegated to UserController.UserLogin, which
        // verified the membership password and returned a UserInfo plus a UserLoginStatus. The service
        // now performs BCrypt verification and returns a signed JWT pair instead of setting a Forms
        // authentication cookie. [ApiController] auto-validates the bound DTO (FluentValidation wired
        // in Program.cs) and short-circuits with an RFC 7807 400 before this body runs when invalid.
        var token = await _authService.LoginAsync(dto, cancellationToken);

        // MIGRATION: a null result models the legacy LOGIN_FAILURE path. Return 401 with no hand-built
        // body — the ProblemDetails payload is produced centrally by the middleware/[ApiController].
        if (token is null)
        {
            return Unauthorized();
        }

        return OkEnvelope(token);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access/refresh token pair (silent re-authentication).
    /// </summary>
    /// <param name="dto">The payload carrying the opaque refresh token previously issued at login.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with a fresh token pair wrapped in the success envelope when the refresh token is
    /// valid; HTTP 401 (Unauthorized) when it is missing, invalid, or expired.
    /// </returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequestDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: there is no legacy analogue — DNN relied on sliding FormsAuthentication cookies
        // rather than refresh tokens. [AllowAnonymous] because the expired/expiring access token cannot
        // be presented; identity is proven by the refresh token itself, which the service validates and
        // rotates. The Angular auth interceptor invokes this transparently on a 401.
        var token = await _authService.RefreshAsync(dto, cancellationToken);

        if (token is null)
        {
            return Unauthorized();
        }

        return OkEnvelope(token);
    }

    /// <summary>
    /// Logs a user out by revoking the refresh token(s) associated with the refresh token supplied in the
    /// request body.
    /// </summary>
    /// <param name="dto">The payload carrying the opaque refresh token whose session(s) to revoke.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 204 (No Content) once the logout has been processed.</returns>
    [HttpPost("logout")]
    // MIGRATION (Checkpoint-8 API-contract finding): logout is [AllowAnonymous] and keyed off the refresh
    // token in the request BODY — NOT the bearer principal. The SPA auth interceptor deliberately does not
    // attach a bearer to the auth-flow routes (login/refresh/logout), and the access token may already be
    // expired at logout, so an [Authorize] logout silently 401'd and the refresh tokens were never revoked
    // server-side. Accepting the refresh token in the body makes revocation deterministic and reachable
    // without a valid access token; possession of the refresh token is itself the revocation credential.
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequestDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: PortalSecurity.SignOut (L77-L79) called FormsAuthentication.SignOut() and expired
        // several cookies. With stateless JWTs there is no server session to drop, so the service revokes
        // the server-side refresh-token state identified by the presented refresh token. The controller
        // stays thin: it binds the DTO and delegates; the service owns the token lookup and revocation and
        // never has the controller parse claims/JWT itself.
        await _authService.LogoutAsync(dto, cancellationToken);

        // A 204 has no body, so it deliberately does NOT use the success-envelope helper.
        return NoContent();
    }

    /// <summary>
    /// Returns the profile of the current authenticated user, derived from the presented JWT.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the current-user projection wrapped in the success envelope; HTTP 401
    /// (Unauthorized) when the authenticated principal cannot be resolved to a user.
    /// </returns>
    [HttpGet("me")]
    // [Authorize] is inherited from the class-level attribute; restated here so the secured posture of
    // this action is explicit at the call site.
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        // MIGRATION: replaces reading the authenticated UserInfo from HttpContext/session in a Web
        // Forms request. Program.cs configures JWT bearer with MapInboundClaims=false,
        // RoleClaimType=ClaimTypes.Role, NameClaimType=ClaimTypes.Name plus custom portalId/isSuperUser
        // claims; the service owns all claim reading, so the controller forwards ControllerBase.User.
        var current = await _authService.GetCurrentUserAsync(User, cancellationToken);

        if (current is null)
        {
            return Unauthorized();
        }

        return OkEnvelope(current);
    }
}
