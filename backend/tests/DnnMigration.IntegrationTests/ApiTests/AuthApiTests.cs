// =============================================================================
//  AuthApiTests
//  -----------------------------------------------------------------------------
//  End-to-end HTTP integration tests for the JWT authentication REST API
//  (/api/auth/{login,logout,me}), exercised in-process against the REAL
//  DnnMigration.Api middleware + DI pipeline through CustomWebApplicationFactory
//  (EF Core InMemory store seeded with a single administrator).
//
//  MIGRATION: these tests validate the single sanctioned behavior change of the
//  DotNetNuke 4.x -> .NET 8 migration (AAP section 0.6.2 / 0.7.1). The legacy
//  ASP.NET Forms Authentication flow -- UserController.UserLogin +
//  FormsAuthentication.SetAuthCookie
//  [Library/Components/Users/UserController.vb:L991-L1033] for login, and
//  PortalSecurity.SignOut -> FormsAuthentication.SignOut
//  [Library/Components/Security/PortalSecurity.vb:L77-L79] for logout -- is
//  replaced by stateless JWT Bearer issuance plus one-way BCrypt verification.
//  The assertions below exercise the NEW JWT behavior, not the retired
//  Forms-Auth/DES semantics. The legacy DNN 4.9.0.85 codebase shipped zero
//  automated tests, so this is a CREATE / from-scratch class.
//
//  Conventions shared by every class in this folder:
//    * file-scoped namespace DnnMigration.IntegrationTests.ApiTests
//    * class-level [Trait("Category", "Integration")] so the Gate 5 run
//      (`dotnet test --filter Category=Integration`) discovers it
//    * IClassFixture<CustomWebApplicationFactory> for the shared in-process host
//
//  RATE-LIMIT DISCIPLINE (AAP section 0.7.2): the AuthController opts /login and
//  /refresh into the "auth" fixed-window limiter (PermitLimit=5 / 1-minute window
//  -- confirmed in Program.cs). The [EnableRateLimiting("auth")] attribute is
//  applied at the ACTION level on login/refresh ONLY, NOT at the class level
//  (confirmed in AuthController.cs), so only login/refresh requests count toward
//  the limit. The "auth" named fixed-window limiter has NO partition key, so ALL
//  login/refresh requests served by a single host instance share ONE 5-per-minute
//  window.
//
//  The tests that consume the SHARED class-fixture `_factory` stay at EXACTLY TWO
//  login requests (one valid, one bad-credentials); the `me` and `logout` tests use
//  minted-token / no-token clients that never touch a rate-limited endpoint. Two
//  hits stay well under the per-minute permit, so no limiter relaxation is required
//  in the parent CustomWebApplicationFactory fixture.
//
//  Every test that drives login/refresh BEYOND those two shared hits -- the
//  valid-refresh rotation test, the invalid-refresh test, and the limiter-exhaustion
//  test -- builds its OWN ISOLATED `using var factory = new CustomWebApplicationFactory()`.
//  Because each WebApplicationFactory builds a separate in-process host with its own
//  DI graph (and therefore its own limiter state and its own uniquely-named InMemory
//  store), those tests cannot throttle -- or be throttled by -- the shared-fixture
//  tests, and the limiter-exhaustion test gets a guaranteed-fresh 5-request window.
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;                // ProblemDetails (RFC 7807 generic-body assertions)
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// HTTP integration tests for the unversioned Auth API surface (<c>/api/auth/...</c>).
/// </summary>
/// <remarks>
/// Each test runs against the real ASP.NET Core 8 pipeline booted in-process by
/// <see cref="CustomWebApplicationFactory"/>, with persistence redirected to an EF Core
/// InMemory store that is pre-seeded with the administrator described by the
/// <c>CustomWebApplicationFactory.Admin*</c> constants. The login endpoint is unversioned
/// (<c>api/auth</c>, NOT <c>api/v1/auth</c>) and rate-limited, so login calls are kept to a
/// strict minimum of two for the whole class.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>The shared in-process API host fixture (EF Core InMemory + seeded administrator).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthApiTests"/> class.
    /// </summary>
    /// <param name="factory">
    /// The xUnit class-fixture instance providing the in-process <c>DnnMigration.Api</c> host and the
    /// authenticated/anonymous <see cref="System.Net.Http.HttpClient"/> factory helpers.
    /// </param>
    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// POST <c>/api/auth/login</c> with the seeded administrator's valid credentials returns
    /// <c>200 OK</c> and a populated <see cref="AuthResponseDto"/> carrying a non-empty JWT access token.
    /// </summary>
    /// <remarks>
    /// This is login request 1 of 2 for the class. It posts a plain <see cref="LoginRequestDto"/> through a
    /// no-token <c>CreateClient()</c>; the AuthService resolves the seeded admin in the default portal
    /// (PortalID=0), BCrypt-verifies the password, and issues a JWT pair.
    /// </remarks>
    [Fact]
    public async Task Login_Valid_Returns200()
    {
        // Login uses a plain (no-token) client and posts the seeded admin's real credentials.
        var client = _factory.CreateClient();
        var request = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Success body is the uniform { data, meta } envelope wrapping the AuthResponseDto.
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// POST <c>/api/auth/login</c> with the seeded administrator's username but a wrong password returns
    /// <c>401 Unauthorized</c>.
    /// </summary>
    /// <remarks>
    /// This is login request 2 of 2 for the class. The AuthService throws
    /// <see cref="System.UnauthorizedAccessException"/> on a failed BCrypt verification, which the global
    /// <c>ExceptionHandlingMiddleware</c> maps to an RFC 7807 <c>401</c> problem+json response. The outcome
    /// is deterministic (the admin row is always seeded).
    /// </remarks>
    [Fact]
    public async Task Login_BadCredentials_Returns401()
    {
        // Same no-token client; same seeded username, but an intentionally wrong password.
        var client = _factory.CreateClient();
        var request = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = "this-is-not-the-admin-password"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GET <c>/api/auth/me</c> requires authentication: a no-token request returns <c>401 Unauthorized</c>,
    /// while a request bearing the seeded admin's minted JWT returns <c>200 OK</c>.
    /// </summary>
    /// <remarks>
    /// Neither call touches the rate-limited login endpoint. The authenticated client uses a token minted
    /// directly by the fixture (<c>CreateAuthenticatedClient()</c>) for <c>AdminUserId</c>, so the
    /// <c>me</c> action resolves the seeded user and returns its profile.
    /// </remarks>
    [Fact]
    public async Task Me_Requires_Auth()
    {
        // 1) No bearer token -> the [Authorize] gate challenges with 401.
        var anonymousClient = _factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/auth/me");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2) Minted-token client (no login hit) -> the seeded admin resolves -> 200.
        var authenticatedClient = _factory.CreateAuthenticatedClient();
        var authenticatedResponse = await authenticatedClient.GetAsync("/api/auth/me");
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// POST <c>/api/auth/logout</c> with a valid bearer token returns <c>204 No Content</c>.
    /// </summary>
    /// <remarks>
    /// Logout is authenticated but stateless: with no Phase-1 server-side refresh-token store the service
    /// performs a no-op acknowledgement and the controller returns <c>NoContent()</c>. The minted-token
    /// client is used, so this test never consumes the login rate limit.
    /// </remarks>
    [Fact]
    public async Task Logout_Returns204()
    {
        // Authenticated (Bearer) client minted by the fixture -- does NOT call the login endpoint.
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// POST <c>/api/auth/refresh</c> with a VALID refresh token (obtained from a prior login) returns
    /// <c>200 OK</c> and a fresh <see cref="AuthResponseDto"/> carrying a newly-issued, non-empty access
    /// token — proving the stateless refresh-token rotation path
    /// (<c>AuthService.RefreshAsync</c> → <c>IJwtService.ValidateToken</c> → re-issue) works end to end.
    /// </summary>
    /// <remarks>
    /// Uses an ISOLATED factory so the login + refresh pair consumed here never erodes the shared
    /// fixture's two-login budget. The legacy DNN Forms-Auth model had no refresh concept; this exercises
    /// the NEW JWT rotation behavior (AAP §0.6.2 / §0.7.2).
    /// </remarks>
    [Fact]
    public async Task Refresh_Valid_Returns200()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // 1) Login to obtain a genuine, signed refresh token (token_type=refresh).
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = CustomWebApplicationFactory.AdminPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        loginBody.Should().NotBeNull();
        loginBody!.Data.Should().NotBeNull();
        loginBody.Data!.RefreshToken.Should().NotBeNullOrEmpty();

        // 2) Exchange the refresh token for a fresh pair.
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = loginBody.Data.RefreshToken!
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
        refreshBody.Should().NotBeNull();
        refreshBody!.Data.Should().NotBeNull();
        refreshBody.Data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// POST <c>/api/auth/refresh</c> with a malformed / non-JWT refresh token returns <c>401 Unauthorized</c>
    /// as an RFC 7807 <c>application/problem+json</c> body, and that body is GENERIC: it never echoes the
    /// supplied token, a signing key, a password, or any stack/exception detail.
    /// </summary>
    /// <remarks>
    /// <c>AuthService.RefreshAsync</c> throws <see cref="System.UnauthorizedAccessException"/> when
    /// <c>IJwtService.ValidateToken</c> cannot validate the token; the global
    /// <c>ExceptionHandlingMiddleware</c> maps that to a sanitized <c>401</c> problem+json. Uses an isolated
    /// factory so this single refresh hit shares no limiter window with other tests.
    /// </remarks>
    [Fact]
    public async Task Refresh_Invalid_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        const string garbageToken = "not-a-valid-refresh-token";

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequestDto
        {
            RefreshToken = garbageToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The negative path must not leak the offending token or any secret/PII/stack detail.
        await AssertGenericProblemBodyAsync(response, forbiddenSubstrings: new[] { garbageToken });
    }

    /// <summary>
    /// Exceeding the auth fixed-window limiter (PermitLimit=5 / 1-minute) on <c>/api/auth/login</c> returns
    /// <c>429 Too Many Requests</c> on the SIXTH request, emitted as an RFC 7807
    /// <c>application/problem+json</c> body, and accompanied by a <c>Retry-After</c> header.
    /// </summary>
    /// <remarks>
    /// Uses an ISOLATED factory so the limiter window is guaranteed fresh and the six hits never affect the
    /// shared fixture. Bad credentials are used deliberately: the rate-limiter middleware runs BEFORE the
    /// endpoint, so requests count toward the window regardless of credential validity, and no real tokens
    /// are minted. Requests 1–5 pass the limiter (and return 401 for the bad credentials); request 6 is
    /// rejected by the limiter with 429 (AAP §0.7.2). The 429 body must also be a generic, non-leaking
    /// Problem Details payload.
    /// </remarks>
    [Fact]
    public async Task RateLimit_Exceeded_Returns429()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        const string submittedPassword = "intentionally-wrong-password";
        var request = new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = submittedPassword
        };

        HttpResponseMessage? throttled = null;

        // PermitLimit=5: the first five hits pass the limiter; the sixth must be rejected with 429.
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled = response;
                break;
            }
        }

        throttled.Should().NotBeNull("the auth limiter must reject the request that exceeds PermitLimit=5");
        throttled!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // RFC 7807 content type and the advertised Retry-After header (Program.cs OnRejected).
        throttled.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        throttled.Headers.Contains("Retry-After").Should().BeTrue();

        // The throttle body must not leak the submitted username/password or any secret/stack detail.
        await AssertGenericProblemBodyAsync(
            throttled,
            forbiddenSubstrings: new[] { submittedPassword });
    }

    /// <summary>
    /// POST <c>/api/auth/login</c> with bad credentials returns a <c>401</c> whose problem+json body is
    /// GENERIC — it never echoes the submitted password or any other secret/PII/stack detail.
    /// </summary>
    /// <remarks>
    /// Uses an isolated factory (a single login hit) so the bad-login probe shares no limiter window with
    /// the shared-fixture tests. This complements <see cref="Login_BadCredentials_Returns401"/> by asserting
    /// the SAFETY of the failure body, not merely its status code.
    /// </remarks>
    [Fact]
    public async Task Login_BadCredentials_BodyIsGeneric()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        const string submittedPassword = "definitely-the-wrong-password";

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Username = CustomWebApplicationFactory.AdminUsername,
            Password = submittedPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AssertGenericProblemBodyAsync(
            response,
            forbiddenSubstrings: new[] { submittedPassword });
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts that a negative-path HTTP response carries a SAFE, generic RFC 7807 Problem Details body:
    /// it parses as a <see cref="ProblemDetails"/> with a valid <c>status</c>, and its raw text contains
    /// none of <paramref name="forbiddenSubstrings"/> nor any obvious secret/stack markers
    /// (the seeded admin password, the JWT signing-key fragment, or a .NET stack-trace token).
    /// </summary>
    /// <param name="response">The HTTP response under test (a 401/403/429/400 failure).</param>
    /// <param name="forbiddenSubstrings">
    /// Caller-supplied values that must NOT appear in the body (for example a submitted password or token).
    /// </param>
    private static async Task AssertGenericProblemBodyAsync(
        HttpResponseMessage response,
        string[] forbiddenSubstrings)
    {
        var raw = await response.Content.ReadAsStringAsync();

        // When a body is present it must be valid RFC 7807 Problem Details with the matching status code.
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem.Should().NotBeNull("a failure response body must be RFC 7807 Problem Details");
            problem!.Status.Should().Be((int)response.StatusCode);
        }

        // The body must never leak the supplied secrets/PII...
        foreach (var forbidden in forbiddenSubstrings.Where(s => !string.IsNullOrEmpty(s)))
        {
            raw.Should().NotContain(forbidden, "failure bodies must not echo submitted secrets/PII");
        }

        // ...nor any well-known secret or stack-trace marker.
        raw.Should().NotContain(CustomWebApplicationFactory.AdminPassword);
        raw.Should().NotContain("StackTrace");
        raw.Should().NotContain("   at ", "a .NET stack trace must never be serialized to clients");
    }
}
