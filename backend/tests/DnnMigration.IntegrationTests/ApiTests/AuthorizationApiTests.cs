using System.Net;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the JWT Bearer AUTHORIZATION boundary, exercised in-process against
/// the REAL <c>DnnMigration.Api</c> pipeline (authentication + authorization middleware + dependency injection)
/// booted by <see cref="CustomWebApplicationFactory"/> over the EF Core InMemory provider — so the suite
/// contacts NO real database and matches Validation Gate 5 (<c>dotnet test --filter Category=Integration</c>).
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION (Finding CP5 MAJOR — security test coverage): the CP5 security checklist explicitly requires
/// coverage of (a) an INVALID Bearer token rejected with <c>401 Unauthorized</c> and (b) an
/// authenticated-but-UNAUTHORIZED principal rejected with <c>403 Forbidden</c>, complementing the existing
/// "missing Bearer -> 401" coverage in <see cref="PortalsApiTests"/>. There is no legacy automated-test
/// equivalent — DotNetNuke 4.9.0.85 shipped zero automated tests — so this is a CREATE-from-scratch suite.
/// </para>
/// <para>
/// PROBE ENDPOINT: every case targets <c>GET /api/v1/portals</c>, which is protected by the class-level
/// <c>[Authorize]</c> plus the action-level <c>[Authorize(Policy = Permissions.View)]</c>. That policy combines
/// <c>RequireAuthenticatedUser()</c> with a <c>PermissionRequirement</c> satisfied only by a SuperUser
/// (<c>IsSuperUser</c> claim true) or a member of the <c>Administrators</c> role. The three cases below
/// therefore isolate the three distinct outcomes of the boundary:
/// </para>
/// <list type="bullet">
///   <item><description>
///     INVALID token — a structurally well-formed but signature-corrupted Bearer fails
///     <c>RequireAuthenticatedUser()</c> at authentication time and short-circuits to <c>401</c> BEFORE the
///     permission requirement is ever evaluated.
///   </description></item>
///   <item><description>
///     NON-ADMIN token — a validly signed Bearer that authenticates successfully but carries
///     <c>IsSuperUser=False</c> and zero role claims fails the permission requirement and is rejected with
///     <c>403</c> (fail-closed in <c>PermissionAuthorizationHandler</c>). No database row is required: the
///     handler decides purely from JWT claims.
///   </description></item>
///   <item><description>
///     ADMIN token — the seeded administrator's Bearer satisfies the requirement (SuperUser short-circuit) and
///     returns <c>200</c>. This contrast case proves the <c>403</c> above is a genuine AUTHORIZATION denial
///     against a working endpoint, not an endpoint failure.
///   </description></item>
/// </list>
/// <para>
/// RATE-LIMIT NOTE: none of these cases touch the rate-limited <c>/api/auth/login</c>|<c>refresh</c> endpoints;
/// the <c>"auth"</c> fixed-window policy is applied only at those actions, so the portal-list probes here are
/// never throttled.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class AuthorizationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>The permission-protected probe endpoint (VIEW policy) used for every authorization case.</summary>
    private const string ProtectedUrl = "/api/v1/portals";

    /// <summary>The in-process API host fixture (real pipeline over EF Core InMemory).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new <see cref="AuthorizationApiTests"/> instance with the shared per-class API host fixture.
    /// </summary>
    /// <param name="factory">The in-process API host fixture supplied by xUnit's class-fixture lifecycle.</param>
    public AuthorizationApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A request carrying a structurally well-formed but cryptographically INVALID Bearer token returns
    /// <c>401 Unauthorized</c>.
    /// </summary>
    /// <remarks>
    /// MIGRATION (Finding CP5 MAJOR — security test coverage): the JWT Bearer middleware's signature validation
    /// rejects the tampered token, so <c>RequireAuthenticatedUser()</c> is unsatisfied and the request is
    /// short-circuited to <c>401</c> before the permission requirement is evaluated. This is the explicit
    /// "invalid Bearer -> 401" case distinct from the "missing Bearer -> 401" case in <see cref="PortalsApiTests"/>.
    /// </remarks>
    [Fact]
    public async Task ProtectedEndpoint_InvalidBearer_Returns401()
    {
        // A real admin token with its signature segment corrupted: parses as a JWT but fails HMAC validation.
        var client = _factory.CreateInvalidTokenClient();

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A request authenticated by a NON-administrator (valid signature, no privileges) returns
    /// <c>403 Forbidden</c> from a permission-protected endpoint.
    /// </summary>
    /// <remarks>
    /// MIGRATION (Finding CP5 MAJOR — security test coverage): the non-admin token authenticates successfully
    /// (valid signature) but carries <c>IsSuperUser=False</c> and no <c>Administrators</c> role claim, so
    /// <c>PermissionAuthorizationHandler</c> leaves the VIEW requirement unmet and the request is rejected with
    /// <c>403</c> — the authenticated-but-unauthorized path. The handler reads claims only, so no seeded row is
    /// needed for the non-admin principal.
    /// </remarks>
    [Fact]
    public async Task ProtectedEndpoint_NonAdmin_Returns403()
    {
        var client = _factory.CreateNonAdminClient();

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A request authenticated by the seeded administrator returns <c>200 OK</c> from the same
    /// permission-protected endpoint — the contrast case proving the <c>403</c> above is a genuine
    /// authorization denial, not an endpoint failure.
    /// </summary>
    /// <remarks>
    /// MIGRATION (Finding CP5 MAJOR — security test coverage): the admin token carries <c>IsSuperUser=True</c>,
    /// which satisfies the VIEW requirement via the SuperUser short-circuit in
    /// <c>PermissionAuthorizationHandler</c>, so the same endpoint that returned <c>401</c>/<c>403</c> above now
    /// returns <c>200</c>. This anchors the negative cases against a known-good positive.
    /// </remarks>
    [Fact]
    public async Task ProtectedEndpoint_Admin_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(ProtectedUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
