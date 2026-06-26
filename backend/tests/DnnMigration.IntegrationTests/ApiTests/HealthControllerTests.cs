// MIGRATION: [AAP Gate 7 / Gate 5] Integration test for the migrated HealthController liveness endpoint.
// The legacy DotNetNuke 4.x stack had no liveness probe; this is the simplest integration test in the suite
// (no authentication, no database seeding) and verifies the new /health contract that Gate 7 exercises at
// container startup (curl -f http://localhost:8080/health). The class-level [Trait("Category","Integration")]
// makes every contained [Fact] selectable by `dotnet test --filter "Category=Integration"` (Gate 5), mirroring
// the convention used by the sibling CRUD integration tests in this folder.
//
// MIGRATION: The file specification referenced a `TestJson.Options` helper for case-insensitive deserialization,
// but no such type exists anywhere in the solution (the only sibling JSON helper, EnvelopeReader, targets the
// enveloped { data, meta } CRUD responses — not this raw body, and it is outside this file's declared
// dependencies). To keep the test self-contained and dependent only on its declared dependencies plus the test
// framework, a local JsonSerializerOptions built from JsonSerializerDefaults.Web is used instead. The Web
// defaults are case-insensitive, so the API's camelCase body ({ "status", "version" }) binds to the PascalCase
// HealthStatus record exactly as the documented contract intends.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for <c>HealthController</c>'s anonymous <c>GET /health</c> liveness endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The tests execute against the real API request pipeline hosted by
/// <see cref="CustomWebApplicationFactory"/>. The endpoint requires no authentication and touches no database,
/// so the default <see cref="System.Net.Http.HttpClient"/> is used and no data seeding is performed.
/// </para>
/// <para>
/// The endpoint returns the raw, <b>non-enveloped</b> shape
/// <c>{ "status": "Healthy", "version": "1.0.0.0" }</c>. There is deliberately no <c>{ data, meta }</c> success
/// wrapper here, because AAP Gate 7 expects this literal payload from the liveness probe.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class HealthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// JSON options used to read the liveness payload. The Web defaults are case-insensitive, so the API's
    /// camelCase property names bind to the PascalCase <see cref="HealthStatus"/> record members.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthControllerTests"/> class.
    /// </summary>
    /// <param name="factory">The shared web-application factory hosting the API under test.</param>
    public HealthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Strongly-typed view of the raw liveness body returned by <c>GET /health</c>.</summary>
    /// <param name="Status">The reported liveness status (expected to be <c>"Healthy"</c>).</param>
    /// <param name="Version">The reported application version (expected to be <c>"1.0.0.0"</c>).</param>
    private sealed record HealthStatus(string Status, string Version);

    /// <summary>
    /// <c>GET /health</c> responds with <c>200 OK</c>, satisfying the Gate 7 container-startup probe.
    /// </summary>
    [Fact]
    public async Task Get_Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <c>GET /health</c> returns the documented raw payload reporting a <c>"Healthy"</c> status and the
    /// <c>"1.0.0.0"</c> version, deserialized from the non-enveloped body.
    /// </summary>
    [Fact]
    public async Task Get_Health_ReturnsHealthyStatusPayload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The body is the RAW shape (NOT an ApiEnvelope<>); read it directly into the local record.
        var body = await response.Content.ReadFromJsonAsync<HealthStatus>(JsonOptions);

        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
        body.Version.Should().Be("1.0.0.0");
    }

    /// <summary>
    /// <c>GET /health</c> is anonymous: the mapped endpoint is annotated <c>[AllowAnonymous]</c> and carries no
    /// <c>[Authorize]</c> requirement, so the liveness probe is reachable without authentication.
    /// </summary>
    /// <remarks>
    /// MIGRATION: [CP4 review — Test Quality] This assertion is made STRUCTURALLY by inspecting the endpoint
    /// graph's metadata rather than by checking a status code. A status-code assertion here would be a tautology:
    /// the <see cref="TestAuthHandler"/> registered by <see cref="CustomWebApplicationFactory"/> authenticates
    /// EVERY request as a super-user, so <c>GET /health</c> would return <c>200 OK</c> even if
    /// <c>[AllowAnonymous]</c> were removed. Reading <see cref="AllowAnonymousAttribute"/> from the endpoint's
    /// metadata proves the anonymous contract independently of the test authentication scheme — the test would
    /// correctly FAIL if the attribute were removed from <c>HealthController</c>.
    /// </remarks>
    [Fact]
    public void Get_Health_IsAnnotatedAllowAnonymous()
    {
        // Accessing Services builds the SUT host, after which the routing endpoint graph (populated by
        // MapControllers) is resolvable from DI. EndpointDataSource is a singleton, so resolve it from the root.
        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        // HealthController is [Route("health")] with a parameterless [HttpGet], so the route pattern is "health".
        var healthEndpoint = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SingleOrDefault(endpoint =>
                string.Equals(endpoint.RoutePattern.RawText, "health", StringComparison.OrdinalIgnoreCase));

        healthEndpoint.Should().NotBeNull("HealthController maps the liveness probe at GET /health");

        // The meaningful, non-tautological assertions: the endpoint opts out of authorization via
        // [AllowAnonymous] and carries no [Authorize] requirement.
        healthEndpoint!.Metadata.GetMetadata<AllowAnonymousAttribute>().Should()
            .NotBeNull("GET /health is decorated [AllowAnonymous], so it is reachable without authentication");
        healthEndpoint.Metadata.GetMetadata<AuthorizeAttribute>().Should()
            .BeNull("the liveness probe must not impose an [Authorize] requirement");
    }
}
