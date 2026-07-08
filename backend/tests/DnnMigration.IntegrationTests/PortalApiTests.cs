// -----------------------------------------------------------------------------
//  PortalApiTests.cs
//
//  MIGRATION: Net-new xUnit integration-test class. The legacy DotNetNuke 4.x
//  solution (VB.NET / ASP.NET Web Forms) shipped no automated test suite — the
//  Portal administration workflow was verified manually against the Web Forms UI
//  (Website/admin/Portal/SiteSettings.ascx.vb, the single-portal editor, and the
//  data/business surface of Library/Components/Portal/PortalController.vb). Those
//  legacy files are REFERENCE lineage only and are NOT modified; this class
//  re-expresses their behaviour as REST and asserts it end-to-end.
//
//  This class satisfies the PortalApiTests requirement of Validation Gate 5
//  (AAP §0.7.2): full CRUD over the in-memory API host — POST → 201, GET → 200,
//  PUT → 200, DELETE → 204 — and additionally covers the cross-cutting API
//  standards the migration mandates: the { data, meta } success envelope
//  (§0.7.2), RFC 7807 ProblemDetails on validation failure, the X-Correlation-ID
//  response header (§0.7.1), and the anonymous GET /health probe (§0.3.1).
//
//  Design notes:
//   * Assertions use FluentAssertions (.Should()...) so no Assert.* xUnit
//     assertion analyzers fire under the Gate 1 warnings-as-errors build.
//   * Every test is `public async Task` and awaits its work — no async void and
//     no blocking .Result/.Wait() (avoids xUnit1031).
//   * The tests assert on the DTO boundary ({ data, meta }); EF entities never
//     surface. Portal.PortalID is ValueGeneratedOnAdd, so the server-assigned id
//     is READ BACK from the POST response body and reused for GET/PUT/DELETE — it
//     is never hard-coded, keeping the lifecycle immune to shared-store state.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for the Portal REST resource (<c>/api/portals</c>) exercised against the
/// full <c>DnnMigration.Api</c> host booted in-memory by <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="IClassFixture{TFixture}"/> so all tests in the class share one factory instance — and
/// therefore one uniquely named EF Core InMemory database — keeping this class isolated from the sibling
/// <c>ModuleApiTests</c> / <c>UserApiTests</c> classes while sharing state across the requests within it.
/// </para>
/// <para>
/// The factory's test authentication scheme authenticates every request as a synthetic administrator, so a
/// plain <see cref="System.Net.Http.HttpClient"/> from <c>factory.CreateClient()</c> satisfies the
/// class-level <c>[Authorize]</c> on <c>PortalsController</c> with no token handling required.
/// </para>
/// </remarks>
public class PortalApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The in-memory HTTP client bound to the test host. Requests through it are authenticated by the
    /// factory's test scheme, so the secured CRUD endpoints are reachable without a login round-trip.
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// JSON options used for request serialization and response deserialization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> is <see langword="true"/> so the
    /// local read-models (whose members are PascalCase, e.g. <c>PortalID</c>/<c>PortalName</c>) bind to the
    /// wire's camelCase keys (<c>"portalID"</c>/<c>"portalName"</c>) emitted by the API's
    /// <c>JsonSerializerDefaults.Web</c> naming policy.
    /// </para>
    /// <para>
    /// The <see cref="JsonStringEnumConverter"/> mirrors the API configuration (Program.cs) which serializes
    /// enums (e.g. <c>UserRegistrationType</c>, <c>BannerType</c>) as their string names, so request bodies
    /// can send the readable enum names and any enum-typed response member round-trips correctly.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public PortalApiTests(CustomWebApplicationFactory factory)
    {
        // The factory authenticates every request via the "Test" scheme, so no bearer token is attached.
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    //  Phase B — Validation Gate 5: the required full-CRUD lifecycle.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Walks the complete Portal CRUD lifecycle in a single self-contained test so it does not depend on any
    /// pre-seeded state: CREATE → 201, READ → 200, UPDATE → 200, DELETE → 204, and READ-after-DELETE → 404.
    /// </summary>
    /// <returns>A task that completes when the lifecycle assertions have all passed.</returns>
    [Fact]
    public async Task Portal_Crud_Lifecycle_Succeeds()
    {
        // ---------- CREATE → 201 ----------
        // MIGRATION: PortalController.CreatePortal (PortalController.vb L980) — the legacy method returned
        // the new PortalId; the REST POST returns 201 Created with the created projection in the envelope.
        // The body carries every field CreatePortalDtoValidator requires (portalName ≤128, first/last name,
        // username, password, a valid email, and portalAlias).
        var createBody = new
        {
            portalName = "Integration Test Portal",
            firstName = "Test",
            lastName = "Administrator",
            username = "itest_portal_admin",
            password = "P@ssw0rd123",
            email = "portal.admin@dnnmigration.local",
            description = "Created by PortalApiTests",
            keyWords = "integration,test",
            homeDirectory = "Portals/itest",
            portalAlias = "itest-portal.localtest.me"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/portals", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        // MIGRATION: Portal.PortalID is ValueGeneratedOnAdd — the InMemory provider assigns it on insert, so
        // the id is read from the response rather than assumed to be 1.
        created.Data!.PortalID.Should().BeGreaterThan(0);
        created.Data.PortalName.Should().Be("Integration Test Portal");

        var id = created.Data.PortalID;

        // ---------- READ → 200 ----------
        // MIGRATION: PortalController.GetPortal (PortalController.vb L1224) / SiteSettings.ascx.vb Page_Load
        // read — a Web Forms postback fetch becomes an HTTP GET returning the { data, meta } envelope.
        var getResponse = await _client.GetAsync($"/api/portals/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched.Data!.PortalID.Should().Be(id);
        fetched.Data.PortalName.Should().Be("Integration Test Portal");

        // ---------- UPDATE → 200 ----------
        // MIGRATION: PortalController.UpdatePortalInfo (PortalController.vb L1568) / SiteSettings.ascx.vb
        // cmdUpdate_Click — the postback button handler becomes a PUT. UpdatePortalDto has several
        // non-nullable value-type members (two enums, a DateTime, several ints); explicit valid values are
        // sent — enums as their string names ("PublicRegistration"/"Banner") and all quota/fee numerics ≥ 0 —
        // so both model binding and UpdatePortalDtoValidator pass deterministically.
        var updateBody = new
        {
            portalName = "Updated Portal Name",
            logoFile = "logo.png",
            footerText = "© Integration Test",
            expiryDate = "2099-12-31T00:00:00",
            userRegistration = "PublicRegistration",   // enum-as-string (UserRegistrationType)
            bannerAdvertising = "Banner",               // enum-as-string (BannerType)
            currency = "USD",
            administratorId = 1,
            hostFee = 0.0,
            hostSpace = 0,
            pageQuota = 0,
            userQuota = 0,
            siteLogHistory = 0,
            splashTabId = 0,
            homeTabId = 0,
            loginTabId = 0,
            userTabId = 0,
            defaultLanguage = "en-US",
            timeZoneOffset = 0
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/portals/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.PortalName.Should().Be("Updated Portal Name");

        // ---------- DELETE → 204 ----------
        // MIGRATION: PortalController.DeletePortalInfo (PortalController.vb L1191) / SiteSettings.ascx.vb
        // cmdDelete_Click — a successful delete returns 204 No Content (empty body).
        var deleteResponse = await _client.DeleteAsync($"/api/portals/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ---------- VERIFY GONE → 404 ----------
        // A subsequent GET of the deleted id yields 404 (the migrated controller returns NotFound rather than
        // the legacy null PortalInfo), confirming the row was actually removed.
        var getAfterDelete = await _client.GetAsync($"/api/portals/{id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    //  Phase C — Cross-cutting API standards (recommended coverage).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a successful create emits the standard <c>{ data, meta }</c> success envelope and that
    /// the response carries the <c>X-Correlation-ID</c> header applied by <c>CorrelationIdMiddleware</c>.
    /// </summary>
    /// <returns>A task that completes when the envelope-shape and header assertions have passed.</returns>
    [Fact]
    public async Task CreatePortal_ReturnsDataMetaEnvelope_AndCorrelationIdHeader()
    {
        var body = ValidCreateBody("itest_env_admin", "env-portal.localtest.me", "env.admin@dnnmigration.local");

        var response = await _client.PostAsJsonAsync("/api/portals", body, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // MIGRATION (§0.7.1): every response echoes the correlation id on X-Correlation-ID.
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();

        // Parse the raw JSON with JsonDocument so the assertion depends only on the wire keys "data"/"meta"
        // (lowercase under the camelCase policy) and not on the concrete DTO shape.
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("meta", out _).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that an invalid create payload is rejected with HTTP 400 and an RFC 7807 ProblemDetails body
    /// (<c>title</c>/<c>status</c>/<c>errors</c>) produced automatically by <c>[ApiController]</c> +
    /// FluentValidation.
    /// </summary>
    /// <returns>A task that completes when the ProblemDetails assertions have passed.</returns>
    [Fact]
    public async Task CreatePortal_WithInvalidBody_Returns400ProblemDetails()
    {
        // An empty portalName violates CreatePortalDtoValidator, and the remaining required fields default to
        // empty on the bound DTO — so multiple NotEmpty rules fail and the request is rejected before the
        // controller body runs.
        var invalid = new { portalName = "" };

        var response = await _client.PostAsJsonAsync("/api/portals", invalid, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The content type is application/problem+json, so read the raw string and parse with JsonDocument
        // rather than ReadFromJsonAsync.
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("title", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();   // validation errors map
    }

    /// <summary>
    /// Verifies that requesting a portal id that does not exist returns HTTP 404.
    /// </summary>
    /// <returns>A task that completes when the 404 assertion has passed.</returns>
    [Fact]
    public async Task GetPortal_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/portals/987654321");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that the anonymous liveness probe <c>GET /health</c> returns HTTP 200 with a body reporting
    /// the healthy status, over plain HTTP and without authentication.
    /// </summary>
    /// <returns>A task that completes when the health-probe assertions have passed.</returns>
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Healthy");
    }

    // -------------------------------------------------------------------------
    //  Phase D — Private helpers + local read-models (test-only shapes).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal-but-valid <c>CreatePortalDto</c> request body (as an anonymous object with camelCase
    /// property names) that satisfies <c>CreatePortalDtoValidator</c>.
    /// </summary>
    /// <param name="username">The initial administrator username (unique per call to avoid collisions).</param>
    /// <param name="portalAlias">The initial HTTP alias (host name) for the portal.</param>
    /// <param name="email">A valid email address for the initial administrator user.</param>
    /// <returns>An anonymous object suitable for <c>PostAsJsonAsync</c>.</returns>
    /// <remarks>
    /// MIGRATION: the <c>Portal</c> domain entity maps <c>Description</c>, <c>KeyWords</c> and
    /// <c>HomeDirectory</c> as REQUIRED (non-nullable) columns, and the AutoMapper profile
    /// (<c>CreateMap&lt;CreatePortalDto, Portal&gt;()</c>) copies these optional DTO members verbatim.
    /// Omitting them therefore leaves the corresponding entity properties <c>null</c>, and EF Core's
    /// InMemory provider rejects the insert with a required-property <c>DbUpdateException</c> (surfaced as
    /// HTTP 500) — even though <c>CreatePortalDtoValidator</c> does not require them. Supplying non-empty
    /// values here (mirroring the full-lifecycle create body) keeps this a genuinely valid create request
    /// whose sole purpose is to assert the success envelope and the correlation-id header on a 201 result.
    /// </remarks>
    private static object ValidCreateBody(string username, string portalAlias, string email) => new
    {
        portalName = "Envelope Test Portal",
        firstName = "Env",
        lastName = "Admin",
        username,
        password = "P@ssw0rd123",
        email,
        description = "Created by PortalApiTests envelope check",
        keyWords = "integration,test,envelope",
        homeDirectory = "Portals/itest-env",
        portalAlias
    };

    /// <summary>
    /// Test-only read-model for the API success envelope <c>{ "data": ..., "meta": ... }</c>.
    /// </summary>
    /// <typeparam name="T">The payload type carried under <c>data</c>.</typeparam>
    /// <remarks>
    /// <see cref="Data"/> is nullable (<c>T?</c>) so no CS8618 is raised; it is dereferenced only after a
    /// <c>.Should().NotBeNull()</c> guard. <see cref="Meta"/> is a <see cref="JsonElement"/> so the model is
    /// decoupled from the exact meta shape (which varies between <c>{ count }</c> and <c>{ timestamp }</c>).
    /// </remarks>
    private sealed class Envelope<T>
    {
        /// <summary>The payload under the envelope's <c>data</c> key.</summary>
        public T? Data { get; set; }

        /// <summary>The raw metadata under the envelope's <c>meta</c> key.</summary>
        public JsonElement Meta { get; set; }
    }

    /// <summary>
    /// Minimal test-only projection of the portal payload; only the members the tests assert on are modeled.
    /// </summary>
    /// <remarks>
    /// With <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> enabled, the wire keys
    /// <c>"portalID"</c>/<c>"portalName"</c> bind to <see cref="PortalID"/>/<see cref="PortalName"/>.
    /// <see cref="PortalName"/> is <c>string?</c> so no CS8618 is raised.
    /// </remarks>
    private sealed class PortalRead
    {
        /// <summary>The server-assigned portal identifier.</summary>
        public int PortalID { get; set; }

        /// <summary>The portal display name.</summary>
        public string? PortalName { get; set; }
    }
}
