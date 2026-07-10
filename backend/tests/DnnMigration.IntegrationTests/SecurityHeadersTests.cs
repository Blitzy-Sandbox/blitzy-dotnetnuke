// -----------------------------------------------------------------------------
//  SecurityHeadersTests.cs
//
//  MIGRATION / QA REMEDIATION: Net-new xUnit integration tests that assert the
//  security response headers added to close QA Issue #2 (missing
//  "X-Content-Type-Options: nosniff"). The header-writing middleware registered in
//  Program.cs (5.0) sets the headers via Response.OnStarting so they are present on
//  EVERY response — success results, the RFC 7807 exception body, the 401 bearer
//  challenge, and the 403 authorization failure. These tests exercise that middleware
//  end-to-end against the real Program.cs pipeline (WebApplicationFactory + TestServer)
//  and act as a permanent regression guard.
//
//  Scope note: the "Server" header suppression (ConfigureKestrel AddServerHeader=false)
//  and the 429 "Retry-After" header are NOT asserted here because they are Kestrel /
//  rate-limiter behaviours that the in-memory TestServer does not surface identically;
//  they are verified against a live Kestrel host during runtime re-verification. The
//  X-Content-Type-Options header (the actual finding) is fully covered here.
// -----------------------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end tests asserting that the security response headers (Issue #2) are emitted on both
/// success and error/challenge responses by the <c>DnnMigration.Api</c> host booted in-memory by
/// <see cref="CustomWebApplicationFactory"/>.
/// </summary>
public class SecurityHeadersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>Initializes the test class with the shared in-memory API host.</summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public SecurityHeadersTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact] // Issue #2: nosniff (and the sibling hardening headers) present on a 200 success response.
    public async Task Health_Ok_CarriesSecurityHeaders()
    {
        var response = await _client.SendAsync(Anonymous(HttpMethod.Get, "/health"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertSecurityHeaders(response);
    }

    [Fact] // Issue #2: the headers must also be present on the 401 bearer-challenge response.
    public async Task Unauthorized_CarriesSecurityHeaders()
    {
        var response = await _client.SendAsync(Anonymous(HttpMethod.Get, "/api/portals"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertSecurityHeaders(response);
    }

    [Fact] // Issue #2: the headers must also be present on the 403 authorization-failure response.
    public async Task Forbidden_CarriesSecurityHeaders()
    {
        // A no-role, non-super authenticated caller fails the PortalAdministrator vertical gate -> 403.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/portals");
        request.Headers.Add("X-Test-Roles", "none");
        request.Headers.Add("X-Test-PortalId", "0");
        request.Headers.Add("X-Test-IsSuperUser", "false");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertSecurityHeaders(response);
    }

    /// <summary>
    /// Asserts that all three security response headers are present with the expected values, reading from
    /// either the response headers or the content headers (custom <c>X-</c> headers are surfaced on the
    /// response-header collection, but the lookup tolerates either location).
    /// </summary>
    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        HeaderValue(response, "X-Content-Type-Options").Should().Be("nosniff");
        HeaderValue(response, "X-Frame-Options").Should().Be("DENY");
        HeaderValue(response, "Referrer-Policy").Should().Be("no-referrer");
    }

    /// <summary>Returns the first value of the named header from the response or content headers, or null.</summary>
    private static string? HeaderValue(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            return values.FirstOrDefault();
        }

        if (response.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return contentValues.FirstOrDefault();
        }

        return null;
    }

    /// <summary>Unauthenticated request (no token) — challenges with 401 for protected resources.</summary>
    private static HttpRequestMessage Anonymous(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Test-Anonymous", "true");
        return request;
    }
}
