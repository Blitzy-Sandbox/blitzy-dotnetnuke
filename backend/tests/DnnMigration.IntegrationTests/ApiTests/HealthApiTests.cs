using System.Net;
using System.Net.Http.Json;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the unversioned <c>GET /health</c> liveness endpoint, exercised
/// in-process against the REAL <c>DnnMigration.Api</c> pipeline through
/// <see cref="CustomWebApplicationFactory"/> (which redirects persistence to an EF Core InMemory store so
/// the suite never contacts SQL Server).
/// </summary>
/// <remarks>
/// <para>
/// This is the host-boot smoke test: a successful response proves the entire ASP.NET Core 8 host
/// constructs and serves requests (composition root, middleware pipeline, controller routing), which is the
/// in-process equivalent of validation <b>Gate 7</b> (<c>curl -f http://localhost:8080/health</c> &#8594; HTTP 200).
/// </para>
/// <para>
/// The class is decorated with <c>[Trait("Category", "Integration")]</c> so it is selected by the
/// <c>Category=Integration</c> filter used for the integration-test gate, and it consumes the shared
/// <see cref="CustomWebApplicationFactory"/> via <see cref="IClassFixture{TFixture}"/> so the in-process host
/// is built once for the class.
/// </para>
/// <para>
/// The <c>/health</c> route is intentionally <b>unversioned</b> (it is NOT under <c>/api/v1/...</c>) and is
/// <c>[AllowAnonymous]</c>, so the probe is reachable with a plain unauthenticated client. Its body is a
/// <b>bare</b> operational payload (<c>{ "status": "Healthy", "timestamp": &lt;ISO-8601&gt; }</c>) and is
/// deliberately NOT wrapped in the standard <c>{ data, meta }</c> resource envelope used by the versioned
/// resource controllers; the assertions below therefore parse a bare shape rather than <c>ApiResponse&lt;T&gt;</c>.
/// </para>
/// </remarks>
// MIGRATION: Infrastructure-only test with no legacy equivalent. DNN 4.9.0.85 shipped no health probe and
// no automated tests; this class is CREATE / from-scratch and backs the containerized (Docker/nginx) Gate 7
// host-boot check. Documented in root MIGRATION_NOTES.md.
[Trait("Category", "Integration")]
public sealed class HealthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The shared in-process test host (REAL Api pipeline over an EF Core InMemory database), injected by
    /// xUnit through the <see cref="IClassFixture{TFixture}"/> contract.
    /// </summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared application factory supplied by xUnit's class-fixture mechanism.</param>
    public HealthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// <c>GET /health</c> on a plain (anonymous) client returns <c>200 OK</c> with the bare
    /// <c>status == "Healthy"</c> payload, confirming the host booted and the liveness endpoint is wired.
    /// </summary>
    [Fact]
    public async Task Health_Returns200()
    {
        // A plain client is sufficient: /health is [AllowAnonymous], so no Bearer token is attached.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        // Gate 7 contract: the liveness probe must answer HTTP 200.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The body is bare (NOT the { data, meta } envelope). Deserialize into a minimal record that maps
        // the camelCase "status" field (System.Text.Json web defaults are case-insensitive) and ignores the
        // unmapped "timestamp" property, avoiding any coupling to the timestamp's serialized type.
        var body = await response.Content.ReadFromJsonAsync<HealthResult>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
    }

    /// <summary>
    /// Minimal projection of the bare <c>/health</c> response body. Only the <c>status</c> field is captured;
    /// any additional JSON properties (such as <c>timestamp</c>) are ignored during deserialization.
    /// </summary>
    /// <param name="Status">The operational status string emitted by the endpoint (expected: "Healthy").</param>
    private sealed record HealthResult(string Status);
}
