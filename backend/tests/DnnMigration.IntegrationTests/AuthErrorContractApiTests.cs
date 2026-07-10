// -----------------------------------------------------------------------------
//  AuthErrorContractApiTests.cs
//
//  MIGRATION / QA REMEDIATION (Checkpoint p2 finding P9-1 — uniform RFC 7807 error
//  contract for authentication-failure 401s): net-new xUnit integration tests that
//  assert the *body shape* of the 401 returned by the credential-exchanging auth
//  actions, not merely the status code.
//
//  Before the fix, AuthController.Login/Refresh/Me returned the bare framework
//  Unauthorized(). Under [ApiController], UnauthorizedResult is an
//  IClientErrorActionResult, so the client-error mapping rewrote it into the
//  DEFAULT ProblemDetails — a body carrying a "traceId" (not the API's uniform
//  "correlationId") with a generic RFC "type"/"title". Because that body sets a
//  Content-Type, it also slipped past StatusCodeProblemDetailsMiddleware (whose
//  empty-body guard skips any response already declaring a Content-Type). The
//  result: the expected bad-login 401 diverged from every OTHER 401 on the API
//  (notably the JWT-challenge 401 the middleware shapes).
//
//  The fix routes those returns through ApiControllerBase.UnauthorizedProblem(),
//  which hand-shapes the SAME envelope the middleware emits for a 401 and returns it
//  as a plain ObjectResult (NOT an IClientErrorActionResult, so [ApiController]
//  leaves it untouched). These tests run the full DnnMigration.Api host in-process
//  (WebApplicationFactory + EF Core InMemory + the permissive test auth scheme) so
//  the assertions exercise the REAL middleware pipeline + controller, and lock the
//  contract against regression.
// -----------------------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end tests asserting that the authentication-failure 401 emitted by the auth-flow endpoints
/// (<c>POST /api/auth/login</c>, <c>POST /api/auth/refresh</c>) carries the API's UNIFORM RFC 7807
/// Problem Details envelope — the same shape produced centrally by
/// <c>StatusCodeProblemDetailsMiddleware</c> for the framework challenge 401 — rather than the
/// <c>[ApiController]</c> framework-default problem body.
/// </summary>
/// <remarks>
/// The InMemory store is empty, so an unknown-user login and an invalid refresh token both drive the
/// service to its failure (<c>null</c>) path, which the controller surfaces via
/// <c>ApiControllerBase.UnauthorizedProblem()</c>. The endpoints are <c>[AllowAnonymous]</c>, so the
/// outcome is independent of the test authentication scheme.
/// </remarks>
public class AuthErrorContractApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ExpectedTitle = "Authentication is required or has failed.";
    private const string ExpectedType = "https://httpstatuses.io/401";
    private const string CorrelationHeaderName = "X-Correlation-ID";

    private readonly HttpClient _client;

    /// <summary>Initializes the test class with the shared in-memory API host.</summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public AuthErrorContractApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_with_unknown_user_returns_uniform_rfc7807_401_envelope()
    {
        // A well-shaped payload clears validation and reaches the service, which finds no user in the
        // empty InMemory store and returns 401 via UnauthorizedProblem().
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "nobody", password = "Passw0rd" });

        await AssertUniform401EnvelopeAsync(response, "/api/auth/login");
    }

    [Fact]
    public async Task Refresh_with_invalid_token_returns_uniform_rfc7807_401_envelope()
    {
        // Non-empty token clears RefreshRequestDtoValidator, so the request reaches the service, which
        // cannot match the (bogus) token and returns 401 via UnauthorizedProblem().
        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = "not-a-real-refresh-token" });

        await AssertUniform401EnvelopeAsync(response, "/api/auth/refresh");
    }

    /// <summary>
    /// Asserts the response is a 401 whose <c>application/problem+json</c> body matches the API's uniform
    /// RFC 7807 envelope: <c>type</c>/<c>title</c>/<c>status</c>/<c>detail</c>/<c>instance</c> plus a
    /// non-empty <c>correlationId</c> that equals the <c>X-Correlation-ID</c> response header, and that
    /// the framework-default <c>traceId</c> property is ABSENT (proving the result was not rewritten by
    /// the <c>[ApiController]</c> client-error mapping).
    /// </summary>
    private static async Task AssertUniform401EnvelopeAsync(HttpResponseMessage response, string expectedInstance)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be(ExpectedType);
        root.GetProperty("title").GetString().Should().Be(ExpectedTitle);
        root.GetProperty("status").GetInt32().Should().Be(401);
        // Detail mirrors the safe title (no framework internals / no sensitive data).
        root.GetProperty("detail").GetString().Should().Be(ExpectedTitle);
        root.GetProperty("instance").GetString().Should().Be(expectedInstance);

        // correlationId is present, non-empty, and matches the X-Correlation-ID header the
        // CorrelationIdMiddleware echoes on every response — the two are one and the same id.
        root.TryGetProperty("correlationId", out var correlationIdElement).Should().BeTrue(
            "the uniform envelope must carry the per-request correlationId, not the framework-default traceId");
        var correlationId = correlationIdElement.GetString();
        correlationId.Should().NotBeNullOrWhiteSpace();

        response.Headers.TryGetValues(CorrelationHeaderName, out var headerValues).Should().BeTrue(
            "CorrelationIdMiddleware echoes the correlation id on every response");
        headerValues!.Single().Should().Be(correlationId);

        // The framework-default ProblemDetails uses "traceId"; its absence proves the ObjectResult body
        // was NOT rewritten by the [ApiController] client-error transformation.
        root.TryGetProperty("traceId", out _).Should().BeFalse(
            "the auth 401 must use the uniform correlationId envelope, not the framework-default traceId body");
    }
}
