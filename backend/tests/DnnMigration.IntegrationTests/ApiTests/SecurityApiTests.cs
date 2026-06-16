// =============================================================================
//  SecurityApiTests
//  -----------------------------------------------------------------------------
//  Cross-cutting SECURITY / API-STANDARDS integration coverage for the
//  DotNetNuke 4.x -> .NET 8 migration backend, exercised in-process against the
//  REAL DnnMigration.Api pipeline through CustomWebApplicationFactory (the genuine
//  middleware + DI graph, JWT/BCrypt identity concretes, the claims-based
//  PermissionAuthorizationHandler, the RFC 7807 exception middleware and the CORS
//  policy) with persistence redirected to an EF Core InMemory store.
//
//  These tests close the Gate-5 security-coverage gaps that the per-resource CRUD
//  classes do not assert (each resource class proves only its own happy path plus
//  a single no-bearer 401). They are deliberately resource-agnostic and use the
//  Portals resource as a representative [Authorize]-protected, policy-gated surface:
//
//    * InvalidBearer_Returns401            - a malformed/garbage Bearer token is
//                                            rejected by JWT authentication with 401
//                                            (a non-leaking problem body when present).
//    * InsufficientPermission_Returns403   - a VALID token for an authenticated but
//                                            UNPRIVILEGED principal is rejected by the
//                                            permission policy with 403 + generic
//                                            problem+json (no secret/PII/stack leak).
//    * Cors_AllowedOrigin_EchoesHeader     - a CORS preflight from the Angular origin
//                                            receives Access-Control-Allow-Origin.
//    * Cors_DisallowedOrigin_OmitsHeader   - a CORS preflight from a foreign origin
//                                            does NOT receive Access-Control-Allow-Origin.
//
//  MIGRATION: the legacy DNN 4.9.0.85 codebase shipped zero automated tests and
//  used Forms-Auth/DES + a portalroles cookie; this CREATE / from-scratch class
//  asserts the NEW stateless JWT-Bearer + claims-policy + BFF-CORS contract
//  (AAP sections 0.6.2 / 0.7.2). Documented in root MIGRATION_NOTES.md.
//
//  Conventions shared with sibling ApiTests classes: file-scoped namespace
//  DnnMigration.IntegrationTests.ApiTests, class-level [Trait("Category",
//  "Integration")] so the Gate 5 run (`dotnet test --filter Category=Integration`)
//  discovers it, and IClassFixture<CustomWebApplicationFactory> for the shared
//  in-process host (one uniquely-named InMemory store per class).
// =============================================================================

using System.Net;                              // HttpStatusCode
using System.Net.Http;                         // HttpRequestMessage, HttpMethod
using System.Net.Http.Headers;                 // AuthenticationHeaderValue
using System.Net.Http.Json;                    // ReadFromJsonAsync
using DnnMigration.IntegrationTests;           // CustomWebApplicationFactory
using FluentAssertions;                        // fluent assertion API
using Microsoft.AspNetCore.Mvc;                // ProblemDetails (RFC 7807 generic-body assertions)
using Xunit;                                   // [Fact], [Trait], IClassFixture

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Cross-cutting security and API-standards HTTP integration tests (invalid-bearer 401,
/// insufficient-permission 403, and CORS allow-list preflight behavior), driven against the real
/// ASP.NET Core 8 pipeline booted by <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// The Portals resource (<c>/api/v1/portals</c>) is used as a representative <c>[Authorize]</c>-protected,
/// permission-policy-gated endpoint: <c>GET</c> requires the <c>VIEW</c> policy, which
/// <c>PermissionAuthorizationHandler</c> grants ONLY to a host super-user or an <c>Administrators</c> member
/// (claims-based, no database lookup). The class is <c>[Trait("Category", "Integration")]</c> so the Gate 5
/// run discovers it and shares one factory instance via <see cref="IClassFixture{TFixture}"/>.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class SecurityApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>A representative <c>[Authorize]</c>-protected, policy-gated resource route.</summary>
    private const string ProtectedRoute = "/api/v1/portals";

    /// <summary>The exact Angular SPA origin allow-listed by the <c>AngularSpa</c> CORS policy (Program.cs / appsettings.json).</summary>
    private const string AllowedOrigin = "http://localhost:4200";

    /// <summary>A foreign origin that is NOT on the CORS allow-list.</summary>
    private const string DisallowedOrigin = "http://evil.example.com";

    /// <summary>The shared in-process API host fixture (real pipeline + EF Core InMemory).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared <see cref="CustomWebApplicationFactory"/> instance.</summary>
    /// <param name="factory">The class fixture supplied by xUnit.</param>
    public SecurityApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A request bearing a MALFORMED / non-JWT Bearer token against a <c>[Authorize]</c>-protected endpoint is
    /// rejected by the JWT Bearer authentication handler with <c>401 Unauthorized</c> (an authentication
    /// challenge), and any response body that is present is a generic, non-leaking payload.
    /// </summary>
    /// <remarks>
    /// This distinguishes the AUTHENTICATION failure (a bad/garbage token ⇒ 401) from the AUTHORIZATION
    /// failure asserted by <see cref="InsufficientPermission_Returns403"/> (a valid token lacking the
    /// permission ⇒ 403). A 401 challenge typically carries no body (only a <c>WWW-Authenticate</c> header);
    /// the assertion helper tolerates an empty body and only validates a present one.
    /// </remarks>
    [Fact]
    public async Task InvalidBearer_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not-a-valid-jwt");

        var response = await client.GetAsync(ProtectedRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A challenge body, when present, must remain a sanitized problem payload (no token/secret/stack leak).
        await AssertGenericProblemBodyAsync(response);
    }

    /// <summary>
    /// A VALID, correctly-signed token for an authenticated but UNPRIVILEGED principal (no super-user flag and
    /// no <c>Administrators</c> role) is rejected by the <c>VIEW</c> permission policy with <c>403 Forbidden</c>,
    /// and the response is a GENERIC RFC 7807 <c>application/problem+json</c> body that leaks no secret/PII/stack.
    /// </summary>
    /// <remarks>
    /// The token is minted by the REAL <see cref="DnnMigration.Application.Interfaces.IJwtService"/> via
    /// <see cref="CustomWebApplicationFactory.CreateNonPrivilegedClient"/>, so it passes authentication (hence
    /// 403, not 401). <c>PermissionAuthorizationHandler</c> grants permissions only to a super-user or an
    /// <c>Administrators</c> member, so the unprivileged principal is forbidden — the fail-closed authorization
    /// contract (AAP §0.6.2).
    /// </remarks>
    [Fact]
    public async Task InsufficientPermission_Returns403()
    {
        var client = _factory.CreateNonPrivilegedClient();

        var response = await client.GetAsync(ProtectedRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AssertGenericProblemBodyAsync(response);
    }

    /// <summary>
    /// A CORS preflight (<c>OPTIONS</c> + <c>Origin</c> + <c>Access-Control-Request-Method</c>) from the
    /// allow-listed Angular origin receives an <c>Access-Control-Allow-Origin</c> response header echoing that
    /// origin — proving the <c>AngularSpa</c> policy admits the SPA origin.
    /// </summary>
    [Fact]
    public async Task Cors_AllowedOrigin_EchoesHeader()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, ProtectedRoute);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue(
            "the AngularSpa CORS policy must admit the allow-listed Angular origin");
        values.Should().NotBeNull().And.Contain(AllowedOrigin);
    }

    /// <summary>
    /// A CORS preflight from a foreign (non-allow-listed) origin does NOT receive an
    /// <c>Access-Control-Allow-Origin</c> response header — proving the BFF CORS contract restricts access to
    /// the configured Angular origin only (<c>AllowAnyOrigin</c> is never used).
    /// </summary>
    [Fact]
    public async Task Cors_DisallowedOrigin_OmitsHeader()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, ProtectedRoute);
        request.Headers.Add("Origin", DisallowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // The disallowed origin must NOT be echoed; no Access-Control-Allow-Origin header may be present.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse(
            "a foreign origin must never be granted CORS access by the AngularSpa allow-list");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts that a negative-path HTTP response carries a SAFE, generic body: when a body is present it
    /// parses as an RFC 7807 <see cref="ProblemDetails"/> whose <c>status</c> matches the HTTP status code,
    /// and its raw text contains no obvious secret/PII/stack markers (the seeded admin password, a
    /// stack-trace token, or a serialized exception type).
    /// </summary>
    /// <param name="response">The HTTP response under test (a 401/403 failure).</param>
    private static async Task AssertGenericProblemBodyAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();

        if (!string.IsNullOrWhiteSpace(raw))
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem.Should().NotBeNull("a failure response body must be RFC 7807 Problem Details");
            problem!.Status.Should().Be((int)response.StatusCode);
        }

        raw.Should().NotContain(CustomWebApplicationFactory.AdminPassword, "failure bodies must not leak secrets");
        raw.Should().NotContain("StackTrace");
        raw.Should().NotContain("   at ", "a .NET stack trace must never be serialized to clients");
    }
}
