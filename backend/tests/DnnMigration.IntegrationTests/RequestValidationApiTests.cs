// -----------------------------------------------------------------------------
//  RequestValidationApiTests.cs
//
//  MIGRATION / QA REMEDIATION: Net-new xUnit integration tests that assert the
//  request-validators added to close the Checkpoint-8 "Secure input validation at
//  boundaries" findings (validators for LoginRequestDto, RefreshRequestDto and
//  ChangePasswordDto). Before the fix, the Validators folder held only Module,
//  Portal and User validators, so blank credentials / refresh tokens / password
//  payloads were NOT rejected with a deterministic 400 at the boundary.
//
//  These tests run the full DnnMigration.Api host in-process (WebApplicationFactory
//  + EF Core InMemory + the permissive test auth scheme) and prove the validators
//  are actually WIRED: an invalid payload is short-circuited by [ApiController] +
//  FluentValidation auto-validation into an RFC 7807 ProblemDetails 400 BEFORE the
//  controller action / service runs. A positive-contrast test confirms a
//  well-shaped-but-unauthenticated login passes validation and reaches the service
//  (401, not 400), so the validators gate shape only — never credentials.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end request-validation tests for the auth-flow endpoints
/// (<c>POST /api/auth/login</c>, <c>POST /api/auth/refresh</c>) and the
/// change-password endpoint (<c>POST /api/users/{id}/change-password</c>),
/// exercised against the full <c>DnnMigration.Api</c> host booted in-memory by
/// <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// The 400 outcomes asserted here are produced by the <c>[ApiController]</c>
/// automatic model-state short-circuit fed by FluentValidation auto-validation, so
/// they are decided BEFORE the controller action executes and are independent of
/// the (empty) InMemory database. The login / refresh endpoints are
/// <c>[AllowAnonymous]</c>; change-password relies on the factory's default
/// host/super identity to clear the controller authorization gate so the request
/// reaches model validation.
/// </remarks>
public class RequestValidationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes the test class with the shared in-memory API host.</summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public RequestValidationApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =========================================================================================
    //  LOGIN — POST /api/auth/login (LoginRequestDtoValidator)
    // =========================================================================================

    [Fact]
    public async Task Login_with_empty_credentials_returns_400_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { username = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("Username");
        errors.Should().ContainKey("Password");
    }

    [Fact]
    public async Task Login_with_negative_portalId_returns_400_problem_details()
    {
        // Username/Password are present and valid, so ONLY the PortalId non-negative rule fires.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "jdoe", password = "Passw0rd", portalId = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("PortalId");
    }

    [Fact]
    public async Task Login_with_valid_shape_but_unknown_user_passes_validation_and_returns_401()
    {
        // Positive contrast: a well-shaped payload clears validation (NOT a 400) and reaches the
        // service, which cannot find the user in the empty InMemory store and returns 401. This
        // proves the validator gates SHAPE only, never credentials.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "nobody", password = "Passw0rd" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // =========================================================================================
    //  REFRESH — POST /api/auth/refresh (RefreshRequestDtoValidator)
    // =========================================================================================

    [Fact]
    public async Task Refresh_with_empty_token_returns_400_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("RefreshToken");
    }

    // =========================================================================================
    //  CHANGE PASSWORD — POST /api/users/{id}/change-password (ChangePasswordDtoValidator)
    // =========================================================================================

    [Fact]
    public async Task ChangePassword_with_empty_payload_returns_400_problem_details()
    {
        // The factory's default identity is a host/super administrator, so the controller-level
        // authorization gate is cleared and the request reaches model validation, which rejects the
        // empty old/new passwords before UserService.ChangePasswordAsync runs.
        var response = await _client.PostAsJsonAsync(
            "/api/users/1/change-password",
            new { oldPassword = "", newPassword = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("OldPassword");
        errors.Should().ContainKey("NewPassword");
    }

    [Fact]
    public async Task ChangePassword_with_new_equal_to_old_returns_400_problem_details()
    {
        // Old and new are both a valid password shape, so only the "must differ" (NotEqual) rule fires.
        var response = await _client.PostAsJsonAsync(
            "/api/users/1/change-password",
            new { oldPassword = "SamePassw0rd", newPassword = "SamePassw0rd" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ReadValidationErrorsAsync(response);
        errors.Should().ContainKey("NewPassword");
    }

    // =========================================================================================
    //  Helper — read the RFC 7807 ValidationProblemDetails "errors" dictionary case-insensitively.
    // =========================================================================================

    /// <summary>
    /// Parses the RFC 7807 problem-details body and returns its <c>errors</c> member as a
    /// case-insensitive dictionary of field name -&gt; messages, so assertions are robust to the
    /// JSON property-naming policy applied to the dictionary keys.
    /// </summary>
    private static async Task<IDictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Errors.Should().NotBeNull().And.NotBeEmpty();

        return new Dictionary<string, string[]>(payload.Errors!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Minimal projection of the RFC 7807 validation problem-details envelope.</summary>
    private sealed record ValidationProblem
    {
        public int Status { get; init; }
        public Dictionary<string, string[]>? Errors { get; init; }
    }
}
