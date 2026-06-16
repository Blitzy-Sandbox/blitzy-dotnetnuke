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
//  the limit. This class therefore issues EXACTLY TWO login requests (one valid,
//  one with bad credentials); the `me` and `logout` tests use the minted-token
//  and no-token clients, which never touch the rate-limited endpoints. Two login
//  hits stay well under the per-minute permit, so no limiter relaxation is
//  required in the parent CustomWebApplicationFactory fixture.
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.IntegrationTests;
using FluentAssertions;
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
}
