namespace DnnMigration.Application.DTOs;

/// <summary>
/// Inbound credentials for the login endpoint (<c>POST /api/auth/login</c>).
/// </summary>
/// <remarks>
/// MIGRATION: replaces the legacy DotNetNuke Forms Authentication sign-in flow
/// (<c>Library/Components/Security/PortalSecurity.vb</c>), where the browser
/// posted credentials through a Web Forms postback and the server issued a
/// <c>FormsAuthentication</c> cookie. In the modern Backend-for-Frontend (BFF)
/// design the Angular SPA sends these credentials as JSON and receives a
/// <see cref="TokenResponseDto"/> JWT pair in return.
/// <para>
/// This is an inbound Data Transfer Object at the Application boundary: it holds
/// no business logic, performs no data access, and carries no serialization or
/// validation attributes. Request validation (non-empty username/password) is
/// applied by a FluentValidation validator at the Application/Api layer, and the
/// <c>{ data, meta }</c> success envelope / RFC 7807 error envelope are applied
/// by the Api layer &#8212; never here.
/// </para>
/// </remarks>
public record LoginRequestDto
{
    /// <summary>
    /// Login name supplied by the user. MIGRATION: corresponds to the legacy
    /// <c>UserInfo.Username</c> (VB <c>String</c>) used by the DNN membership
    /// authentication routine. Defaults to <see cref="string.Empty"/> so the
    /// property is never null under nullable reference types.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Plain-text password supplied by the user, verified server-side against the
    /// stored hash. MIGRATION: the legacy stack verified credentials through
    /// <c>PortalSecurity</c>/provider membership; the modern stack verifies via
    /// BCrypt and issues a JWT. This value is consumed only during verification
    /// and is never persisted, logged, or echoed back to the client.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Optional identifier of the portal the user is authenticating against.
    /// MIGRATION: from the legacy <c>UserInfo.PortalID</c> (VB <c>Integer</c>)
    /// portal-scoping concept. Nullable because the portal is optional at login
    /// and may instead be derived from request context (host/alias); when
    /// <see langword="null"/> the login is resolved against the ambient portal.
    /// </summary>
    public int? PortalId { get; init; }
}

/// <summary>
/// Inbound payload for the token refresh endpoint (<c>POST /api/auth/refresh</c>).
/// </summary>
/// <remarks>
/// MIGRATION: there is no legacy analogue &#8212; DotNetNuke relied on sliding
/// <c>FormsAuthentication</c> cookies rather than refresh tokens. This DTO
/// carries the opaque refresh token the client obtained from a prior
/// <see cref="TokenResponseDto"/> so the Api can rotate it and issue a fresh
/// access token during silent re-authentication (the Angular auth interceptor
/// calls this endpoint transparently on a 401). It holds no logic and no
/// attributes.
/// </remarks>
public record RefreshRequestDto
{
    /// <summary>
    /// The opaque refresh token previously issued in a <see cref="TokenResponseDto"/>.
    /// Defaults to <see cref="string.Empty"/> so the property is never null under
    /// nullable reference types.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Inbound payload for the logout endpoint (<c>POST /api/auth/logout</c>).
/// </summary>
/// <remarks>
/// MIGRATION: replaces <c>PortalSecurity.SignOut</c>
/// (<c>Library/Components/Security/PortalSecurity.vb</c> L77), which cleared the
/// <c>FormsAuthentication</c> cookie server-side. With stateless JWTs there is no
/// server session to drop; the server-side state that CAN be revoked is the
/// opaque refresh token. Logout therefore carries the refresh token in the
/// request body and revokes it (and the owning user's sessions) by lookup — the
/// same token-in-body contract used by <see cref="RefreshRequestDto"/>.
/// <para>
/// Sending the token in the body (rather than relying on the <c>Authorization</c>
/// bearer header) is deliberate: the Angular auth interceptor treats the auth-flow
/// routes (login / refresh / logout) as credential-exchanging endpoints and does
/// NOT attach a bearer to them, and the access token may already be expired at
/// logout time. The refresh token is the revocation credential, so the endpoint is
/// <c>[AllowAnonymous]</c> and keyed entirely off this value. It holds no logic and
/// no attributes; revoking an unknown/blank token is an idempotent no-op.
/// </para>
/// </remarks>
public record LogoutRequestDto
{
    /// <summary>
    /// The opaque refresh token whose session(s) should be revoked. Defaults to
    /// <see cref="string.Empty"/> so the property is never null under nullable
    /// reference types; a blank/unknown value results in an idempotent no-op.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Outbound JWT token pair returned by the login and refresh endpoints
/// (<c>POST /api/auth/login</c> and <c>POST /api/auth/refresh</c>).
/// </summary>
/// <remarks>
/// MIGRATION: replaces the legacy DotNetNuke <c>FormsAuthentication</c> /
/// <c>PortalSecurity</c> DES-based authentication flow
/// (<c>Library/Components/Security/PortalSecurity.vb</c>) with stateless JWT
/// bearer tokens. Instead of setting an authentication cookie server-side, the
/// Api returns this pair so the SPA can attach the access token as a
/// <c>Bearer</c> Authorization header on subsequent requests and use the
/// refresh token for silent renewal. This is an outbound Data Transfer Object:
/// it holds no business logic, performs no data access, and carries no
/// serialization attributes.
/// </remarks>
public record TokenResponseDto
{
    /// <summary>
    /// The short-lived JWT access token presented on each API call via the
    /// <c>Authorization: Bearer</c> header. Defaults to <see cref="string.Empty"/>
    /// so the property is never null under nullable reference types.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// The longer-lived refresh token used to obtain a new access token without
    /// re-entering credentials (see <see cref="RefreshRequestDto"/>). Subject to
    /// rotation on each successful refresh. Defaults to <see cref="string.Empty"/>.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Absolute expiry instant of <see cref="AccessToken"/>, expressed in UTC, so
    /// the client can proactively refresh before the token lapses. MIGRATION:
    /// the legacy stack expressed session lifetime through cookie timeouts; here
    /// it is an explicit, serializable expiry timestamp.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// The authentication scheme for <see cref="AccessToken"/>. Defaults to
    /// <c>"Bearer"</c>, matching the JWT bearer scheme the Api registers and the
    /// value the client places before the token in the <c>Authorization</c> header.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";
}

/// <summary>
/// Outbound payload for the current-user endpoint (<c>GET /api/auth/me</c>),
/// describing the authenticated principal derived from the presented JWT.
/// </summary>
/// <remarks>
/// MIGRATION: replaces the legacy pattern of reading the authenticated
/// <c>UserInfo</c> (<c>Library/Components/Users/UserInfo.vb</c>) from
/// <c>HttpContext</c>/session in a Web Forms request. It composes the full
/// <see cref="UserDto"/> projection (which already includes the user's role
/// names), reusing the same read contract surfaced by the Users API so the SPA
/// has a single, consistent user shape. This is an outbound Data Transfer
/// Object: no business logic, no data access, no attributes.
/// </remarks>
public record CurrentUserDto
{
    /// <summary>
    /// The full projection of the authenticated user, including role names.
    /// MIGRATION: mirrors the legacy <c>UserInfo</c> model via the shared
    /// <see cref="UserDto"/> (same Application DTO namespace). Defaults to a new
    /// <see cref="UserDto"/> instance so the property is never null under
    /// nullable reference types.
    /// </summary>
    public UserDto User { get; init; } = new();
}
