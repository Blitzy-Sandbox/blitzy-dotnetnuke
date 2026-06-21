using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the JWT authentication surface
/// (<c>/api/auth/{login,logout,me}</c>), exercised in-process against the REAL
/// <c>DnnMigration.Api</c> pipeline (middleware + dependency injection) booted by
/// <see cref="CustomWebApplicationFactory"/> over the EF Core InMemory provider — so the suite
/// contacts NO real database and matches Validation Gate 5
/// (<c>dotnet test --filter Category=Integration</c>).
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: There is no legacy automated-test equivalent — DotNetNuke 4.9.0.85 shipped zero
/// automated tests, so this is a CREATE-from-scratch suite. It validates the SINGLE SANCTIONED
/// behavior change of the rewrite (AAP §0.6.2 / §0.7.1): the legacy ASP.NET Forms Authentication
/// + 56-bit DES credential model — <c>UserController.UserLogin</c>
/// (Library/Components/Users/UserController.vb:L991-L1008, which called <c>ValidateUser</c> then set
/// the Forms-auth cookie) and <c>PortalSecurity</c> (Library/Components/Security/PortalSecurity.vb:
/// <c>SignOut</c> L79, DES <c>Encrypt</c>/<c>Decrypt</c> L138-L211) — has been replaced by stateless
/// JWT Bearer issuance + BCrypt password verification orchestrated by <c>AuthService</c> and
/// <c>JwtService</c>.
/// </para>
/// <para>
/// ROUTE NOTE: the Auth controller is UNVERSIONED (<c>api/auth</c>, NOT <c>api/v1/...</c>) and carries
/// no class-level <c>[Authorize]</c>; <c>login</c> is <c>[AllowAnonymous]</c> while <c>me</c> and
/// <c>logout</c> require a Bearer token.
/// </para>
/// <para>
/// RATE-LIMIT DISCIPLINE (⚠️): the <c>"auth"</c> fixed-window policy (Program.cs Phase 9:
/// 5 permits / 1 minute) is applied at the ACTION level on <c>login</c>/<c>refresh</c> ONLY — it is
/// NOT class-level — so <c>me</c>/<c>logout</c> hits do not count against it. This class makes exactly FIVE
/// rate-limited calls — all within the single per-class limiter window: two <c>login</c> calls
/// (<c>Login_Valid_Returns200</c> and <c>Login_BadCredentials_Returns401</c>; BOTH now execute, because
/// membership-password sourcing (Finding CP5 MAJOR) means the valid-login test is no longer skipped); a
/// <c>login</c> + a <c>refresh</c> in <c>Refresh_Returns200_WithNewTokenPair</c> (the end-to-end
/// login→refresh rotation, QA Finding F3); and a single <c>refresh</c> in
/// <c>Refresh_WithAccessToken_Returns401</c>, which mints its access token directly via the factory's
/// <see cref="CustomWebApplicationFactory.GenerateTokenForSeededAdmin"/> and therefore spends NO
/// <c>login</c> permit. The <c>me</c> and <c>logout</c> tests use the factory's minted-token / no-token
/// clients and never touch the rate-limited endpoint. Because each test class owns its own
/// <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c> host instance (and therefore its own limiter
/// window), exactly 5 of the 5 permits are consumed — each request acquires a permit (the fifth takes the
/// last), and only a sixth would be rejected with 429 — so 429 cannot occur here.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>The in-process API host fixture (real pipeline over EF Core InMemory).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new <see cref="AuthApiTests"/> instance with the shared per-class API host fixture.
    /// </summary>
    /// <param name="factory">The in-process API host fixture supplied by xUnit's class-fixture lifecycle.</param>
    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// POST <c>/api/auth/login</c> with the seeded administrator's valid credentials returns
    /// <c>200 OK</c> and an <see cref="ApiResponse{T}"/> envelope whose <see cref="AuthResponseDto"/>
    /// carries a non-empty JWT access token.
    /// </summary>
    /// <remarks>
    /// MIGRATION: exercises the JWT replacement for the legacy <c>UserController.UserLogin</c> +
    /// <c>ValidateUser</c> flow — <c>AuthService.LoginAsync</c> resolves the seeded admin in the primary
    /// portal (PortalID=0), verifies the password with BCrypt, and issues a signed access token.
    /// MEMBERSHIP-PASSWORD SOURCING (Finding CP5 MAJOR — now implemented): per ADR-002 schema fidelity the
    /// legacy <c>[Users]</c> table has NO Password column — passwords live in the ASP.NET Membership
    /// <c>aspnet_Membership</c> table — so <c>UserConfiguration</c> RETAINS <c>builder.Ignore(u =&gt; u.Password)</c>.
    /// The credential hash is therefore SOURCED rather than stored on <c>[Users]</c>:
    /// <see cref="CustomWebApplicationFactory"/> seeds an <c>aspnet_Users</c> row (LoweredUserName = "admin") and
    /// an <c>aspnet_Membership</c> row carrying the BCrypt hash, both keyed by
    /// <see cref="CustomWebApplicationFactory.AdminMembershipUserId"/>, and <c>UserRepository.GetByUsernameAsync</c>
    /// JOINs <c>aspnet_Users -&gt; aspnet_Membership</c> on the lowered username to populate <c>user.Password</c>
    /// on the AsNoTracking instance (in-memory only — never persisted, ADR-002). <c>AuthService.LoginAsync</c>
    /// then passes its <c>string.IsNullOrEmpty(user.Password)</c> guard and BCrypt-verifies the credential, so a
    /// VALID login now returns 200 end to end (the complementary username-to-portal resolution — the
    /// <c>UserPortals</c> JOIN inside <c>GetByUsernameAsync</c> — is satisfied by the existing <c>UserPortals</c>
    /// seed row). This is login call 1 of 2 against the rate-limited endpoint.
    /// </remarks>
    [Fact]
    public async Task Login_Valid_Returns200()
    {
        // No-token client: login is [AllowAnonymous] and must NOT carry a Bearer header.
        var client = _factory.CreateClient();
        var request = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Success body is the uniform { data, meta } envelope wrapping the auth response.
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// POST <c>/api/auth/login</c> with the seeded username but a WRONG password returns
    /// <c>401 Unauthorized</c>.
    /// </summary>
    /// <remarks>
    /// MIGRATION: <c>AuthService.LoginAsync</c> throws <see cref="UnauthorizedAccessException"/> on a
    /// failed BCrypt verification (a deliberately generic message that avoids user enumeration), and the
    /// global <c>ExceptionHandlingMiddleware</c> maps that exception to an RFC 7807 401 response. The
    /// outcome is deterministic. This is login call 2 of 2 against the rate-limited endpoint.
    /// </remarks>
    [Fact]
    public async Task Login_BadCredentials_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            // A value that is intentionally NOT the seeded password, guaranteeing BCrypt verification fails.
            Password = "wrong-password-not-the-seeded-credential"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GET <c>/api/auth/me</c> requires authentication: a request with NO token is rejected with
    /// <c>401 Unauthorized</c>, while a request carrying the seeded admin's minted Bearer token returns
    /// <c>200 OK</c>.
    /// </summary>
    /// <remarks>
    /// MIGRATION: replaces <c>UserController.GetCurrentUserInfo</c>
    /// (Library/Components/Users/UserController.vb:L381-L403). The authenticated path uses the factory's
    /// minted token (<see cref="CustomWebApplicationFactory.CreateAuthenticatedClient"/>) — which does
    /// NOT drive the rate-limited <c>login</c> endpoint — whose <c>NameIdentifier</c> claim is the seeded
    /// <see cref="CustomWebApplicationFactory.AdminUserId"/>, so <c>AuthService.GetCurrentUserAsync</c>
    /// resolves the seeded admin and the endpoint returns the user projection.
    /// </remarks>
    [Fact]
    public async Task Me_Requires_Auth()
    {
        // 1) No Bearer token -> the JWT Bearer challenge rejects the request with 401.
        var anonymousClient = _factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/auth/me");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2) Minted admin Bearer token -> the user id (=AdminUserId) resolves the seeded admin -> 200.
        var authenticatedClient = _factory.CreateAuthenticatedClient();
        var authenticatedResponse = await authenticatedClient.GetAsync("/api/auth/me");
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// POST <c>/api/auth/logout</c> with a valid Bearer token returns <c>204 No Content</c>.
    /// </summary>
    /// <remarks>
    /// MIGRATION: replaces <c>FormsAuthentication.SignOut()</c>
    /// (Library/Components/Security/PortalSecurity.vb:L79). Phase 1 is STATELESS — the server keeps no
    /// refresh-token store — so <c>AuthService.LogoutAsync</c> is a no-op acknowledgement and the endpoint
    /// returns an empty 204. The minted-token client is used, so this test does NOT consume the
    /// <c>login</c> rate limit.
    /// </remarks>
    [Fact]
    public async Task Logout_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        // The logout endpoint reads identity from the JWT claims, not a request body; no content is sent.
        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// POST <c>/api/auth/refresh</c> using the httpOnly refresh-token COOKIE (obtained by first logging in)
    /// returns <c>200 OK</c> and a fresh access token; the rotated refresh token is delivered as a new cookie
    /// and never appears in the response body.
    /// </summary>
    /// <remarks>
    /// MIGRATION (C5/DEV-032 + Finding CP-FINAL-2): exercises the stateless refresh-token rotation through the
    /// REAL HTTP pipeline (QA Finding F3) under the hardened transport. The refresh token is NO LONGER carried
    /// in the request/response body (CWE-922): login sets it as an httpOnly <c>dnn_refresh_token</c> cookie and
    /// the default test client's cookie jar (<c>HandleCookies=true</c>) auto-resends it on the refresh POST —
    /// which carries an EMPTY body. Over the in-process plain-HTTP transport the cookie is not flagged
    /// <c>Secure</c> (Request.IsHttps is false), so it round-trips; in production behind nginx the forwarded
    /// HTTPS scheme flags it Secure. <c>AuthService.RefreshAsync</c> validates the signed JWT, enforces the
    /// <c>token_use=="refresh"</c> guard, resolves the subject, re-hydrates roles, and issues a rotated pair.
    /// That the refresh returns 200 is itself proof the cookie was set on login and round-tripped (a missing
    /// cookie yields a blank token → 401). This is rate-limited call 3 (<c>login</c>) and 4 (<c>refresh</c>) of
    /// the class's 5-permit budget. Tokens are asserted non-empty only — two tokens minted in the same second
    /// can be byte-identical, so an inequality assertion would be flaky.
    /// </remarks>
    [Fact]
    public async Task Refresh_Returns200_WithNewTokenPair()
    {
        // 1) Log in. The default client keeps a cookie jar (HandleCookies=true), capturing the httpOnly
        //    dnn_refresh_token cookie for automatic resend. Login is [AllowAnonymous]; no Bearer header.
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // SECURITY (Finding CP-FINAL-2): the refresh token is delivered ONLY as an httpOnly cookie, and is
        // stripped from the response body so no client-side JavaScript can ever read it.
        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        setCookies!.Should().Contain(
            c => c.StartsWith("dnn_refresh_token=", StringComparison.Ordinal)
                 && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        loginResult.Should().NotBeNull();
        loginResult!.Data.Should().NotBeNull();
        loginResult.Data!.AccessToken.Should().NotBeNullOrEmpty();
        loginResult.Data.RefreshToken.Should().BeNull("the refresh token must never appear in the response body");

        // 2) Exchange the cookie-borne refresh token for a new pair. The body is intentionally EMPTY — the
        //    refresh token is read from the auto-resent dnn_refresh_token cookie. A 200 proves the round-trip.
        var refreshResponse = await client.PostAsync("/api/auth/refresh", content: null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // The rotated refresh token is delivered via a fresh Set-Cookie, again absent from the body.
        refreshResponse.Headers.TryGetValues("Set-Cookie", out var rotatedCookies).Should().BeTrue();
        rotatedCookies!.Should().Contain(c => c.StartsWith("dnn_refresh_token=", StringComparison.Ordinal));

        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        refreshResult.Should().NotBeNull();
        refreshResult!.Data.Should().NotBeNull();
        var rotated = refreshResult.Data!;
        rotated.AccessToken.Should().NotBeNullOrEmpty();
        rotated.RefreshToken.Should().BeNull("rotation keeps the refresh token in the httpOnly cookie, not the body");
    }

    /// <summary>
    /// POST <c>/api/auth/refresh</c> with an ACCESS token (not a refresh token) returns <c>401 Unauthorized</c>,
    /// runtime-verifying the token-type-confusion guard.
    /// </summary>
    /// <remarks>
    /// MIGRATION (C5/DEV-032 + Finding CP-FINAL-2): <c>JwtService</c> stamps <c>token_use=access</c> on access
    /// tokens and <c>token_use=refresh</c> on refresh tokens; <c>AuthService.RefreshAsync</c> rejects anything
    /// whose <c>token_use</c> is not <c>"refresh"</c>, preventing an access token from being replayed at the
    /// refresh endpoint. Under the hardened transport (Finding CP-FINAL-2) the refresh endpoint reads its token
    /// from the httpOnly <c>dnn_refresh_token</c> COOKIE, not the body, so the access token is planted there.
    /// A client with <c>HandleCookies=false</c> is used so the manually-set Cookie header is sent verbatim and
    /// not overwritten by the (empty) cookie jar. The access token is minted directly via the factory
    /// (<see cref="CustomWebApplicationFactory.GenerateTokenForSeededAdmin"/>), so this test spends ONE
    /// <c>refresh</c> permit and NO <c>login</c> permit (rate-limited call 5 of the class's 5-permit budget).
    /// </remarks>
    [Fact]
    public async Task Refresh_WithAccessToken_Returns401()
    {
        // An access token is structurally valid and correctly signed, but carries token_use=access.
        var accessToken = _factory.GenerateTokenForSeededAdmin();

        // refresh is [AllowAnonymous] and reads the refresh token from the httpOnly cookie. Disable the cookie
        // jar so the manually-planted Cookie header reaches the server unchanged.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"dnn_refresh_token={accessToken}");

        var response = await client.SendAsync(request);

        // The token-type-confusion guard (token_use != "refresh") yields UnauthorizedAccessException -> 401.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
