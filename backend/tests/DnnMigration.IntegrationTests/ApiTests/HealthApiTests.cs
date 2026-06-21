using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the unversioned <c>GET /health</c> liveness endpoint,
/// exercised in-process against the REAL <c>DnnMigration.Api</c> middleware and dependency-injection
/// pipeline through <see cref="CustomWebApplicationFactory"/> (EF Core InMemory; no SQL Server and no
/// real database is ever contacted).
/// </summary>
/// <remarks>
/// <para>
/// This is an infrastructure-only smoke test: it confirms the API host boots and the health probe
/// responds, underpinning validation Gate 7 (<c>curl -f http://localhost:8080/health</c> &#8594;
/// HTTP 200) and the Docker <c>HEALTHCHECK</c> wiring defined in <c>docker/</c>.
/// </para>
/// <para>
/// The <c>/health</c> route is deliberately UNVERSIONED (it is NOT mounted under <c>/api/v1</c>) and
/// <c>[AllowAnonymous]</c>, so the test drives a plain, unauthenticated client obtained from
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.CreateClient()"/>.
/// The endpoint returns a BARE JSON document (<c>{ "status": "Healthy", "timestamp": &lt;ISO&gt; }</c>)
/// rather than the platform-standard <c>{ data, meta }</c> success envelope, so the body is parsed
/// into the minimal local <see cref="HealthResult"/> record, which intentionally ignores the unmapped
/// <c>timestamp</c> property to stay decoupled from its serialized representation.
/// </para>
/// <para>
/// MIGRATION: there is no legacy equivalent — DotNetNuke 4.9.0.85 had no health endpoint and shipped
/// zero automated tests, so this is a CREATE-from-scratch class. The endpoint's existence rationale is
/// recorded in the root <c>MIGRATION_NOTES.md</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class HealthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The shared in-process API host fixture (EF Core InMemory-backed) that hosts the real
    /// <c>DnnMigration.Api</c> pipeline under test.
    /// </summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthApiTests"/> class.
    /// </summary>
    /// <param name="factory">
    /// The class-scoped <see cref="CustomWebApplicationFactory"/> supplied by xUnit through
    /// <see cref="IClassFixture{TFixture}"/>; it boots the real API host exactly once for this class.
    /// </param>
    public HealthApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies that an anonymous <c>GET /health</c> returns HTTP 200 OK with the bare
    /// <c>{ "status": "Healthy", ... }</c> payload, proving the host boots and the liveness probe is
    /// reachable without authentication (validation Gate 7).
    /// </summary>
    [Fact]
    public async Task Health_Returns200()
    {
        // Arrange: a plain, unauthenticated client — /health is [AllowAnonymous], so no Bearer token.
        var client = _factory.CreateClient();

        // Act: hit the unversioned liveness route mounted at the application root.
        var response = await client.GetAsync("/health");

        // Assert: 200 OK, and the bare body's camelCase "status" deserializes to "Healthy".
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResult>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
    }

    /// <summary>
    /// Minimal projection of the bare health response body. Only the <c>status</c> field is asserted:
    /// System.Text.Json web defaults are case-insensitive (so <c>status</c> binds to
    /// <see cref="Status"/>), and any additional JSON properties (such as <c>timestamp</c>) are ignored,
    /// keeping the test decoupled from the timestamp's serialized type.
    /// </summary>
    /// <param name="Status">The health status text emitted by the endpoint (expected: <c>"Healthy"</c>).</param>
    private sealed record HealthResult(string Status);
}
