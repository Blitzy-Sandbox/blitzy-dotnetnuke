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
// MIGRATION (QA finding - R10 Issue 2): the portal-delete cascade test asserts directly against the store
// (Users / UserPortals / aspnet_Membership) to prove NO orphaned rows remain - a fact the HTTP surface alone
// cannot establish for the junction/credential rows - so it resolves a scoped DnnDbContext from the factory.
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    /// The shared web-application factory, retained so the portal-delete cascade test can open a scoped
    /// <see cref="DnnDbContext"/> and assert directly that no orphaned user rows remain in the store.
    /// </summary>
    private readonly CustomWebApplicationFactory _factory;

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
    /// MIGRATION QA finding F2: the API serializes enums as their INTEGER codes (the default
    /// <c>System.Text.Json</c> behaviour; the previous global <see cref="JsonStringEnumConverter"/> was
    /// removed from <c>Program.cs</c> because its string names never matched the Angular numeric
    /// <c>&lt;select&gt;</c> options, blanking the Portal dropdowns). This suite therefore sends enum request
    /// values as integers and reads them back as integers (the read-models type them as <see cref="int"/>).
    /// The <see cref="JsonStringEnumConverter"/> is retained here only as a harmless tolerance: it affects
    /// only enum-typed CLR members (the read-models declare none), so it is a no-op for these payloads.
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
        _factory = factory;
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
        // MIGRATION QA finding (Portal.guid never generated): a freshly-created portal must carry a real,
        // non-empty GUID (CreateAsync assigns Guid.NewGuid()) rather than the all-zero CLR default.
        created.Data.GUID.Should().NotBe(Guid.Empty, "CreateAsync must generate the portal's GUID");

        var id = created.Data.PortalID;
        var createdGuid = created.Data.GUID;

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
        // The generated GUID persists and is returned unchanged on a subsequent read.
        fetched.Data.GUID.Should().Be(createdGuid, "the generated portal GUID persists across reads");

        // ---------- UPDATE → 200 ----------
        // MIGRATION: PortalController.UpdatePortalInfo (PortalController.vb L1568) / SiteSettings.ascx.vb
        // cmdUpdate_Click — the postback button handler becomes a PUT. UpdatePortalDto has several
        // non-nullable value-type members (two enums, a DateTime, several ints); explicit valid values are
        // sent — enums as their INTEGER codes (2 = UserRegistrationType.PublicRegistration, 1 = BannerType.Banner,
        // matching the numeric Angular <select> option values per QA finding F2) and all quota/fee numerics ≥ 0 —
        // so both model binding and UpdatePortalDtoValidator pass deterministically.
        var updateBody = new
        {
            portalName = "Updated Portal Name",
            logoFile = "logo.png",
            footerText = "© Integration Test",
            expiryDate = "2099-12-31T00:00:00",
            userRegistration = 2,   // integer enum code (UserRegistrationType.PublicRegistration = 2)
            bannerAdvertising = 1,  // integer enum code (BannerType.Banner = 1)
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
        // MIGRATION QA finding F2 (CRITICAL — enum contract): the enum-typed members round-trip on the wire
        // as their INTEGER codes (not string names), so the Angular numeric <select> options match the value
        // and the dropdowns are no longer blank. PortalRead types these as int and reads the raw wire number.
        updated.Data.UserRegistration.Should().Be(2); // UserRegistrationType.PublicRegistration
        updated.Data.BannerAdvertising.Should().Be(1); // BannerType.Banner
        // MIGRATION QA finding (Portal.guid): the GUID is a stable identity — an update must neither
        // regenerate nor clear it (UpdatePortalDto has no GUID field, so the persisted value is preserved).
        updated.Data.GUID.Should().Be(createdGuid, "the portal GUID is stable across updates");

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

    /// <summary>
    /// Verifies the portal-creation PARITY the code review flagged as missing (PortalService.CreateAsync
    /// previously dropped the administrator and alias): a successful create provisions the initial
    /// administrator user, wires <c>Portal.AdministratorId</c> to it, and registers the initial HTTP alias.
    /// All three are proven PERSISTED (not merely echoed) by re-reading the portal AND fetching the
    /// administrator user back through the real repository stack.
    /// MIGRATION: PortalController.CreatePortal (PortalController.vb L980-L1075) - admin [L1013],
    /// AdministratorId, and PortalAliasController.AddPortalAlias.
    /// </summary>
    /// <returns>A task that completes when the provisioning assertions have all passed.</returns>
    [Fact]
    public async Task CreatePortal_ProvisionsAdministrator_AndPersistsAlias()
    {
        // ---------- CREATE -> 201 ----------
        // A unique administrator username / host alias so this test is independent of the other creates in
        // this class (which share one InMemory database via the class fixture).
        const string adminUsername = "itest_admin_provision";
        const string hostAlias = "provision-portal.localtest.me";
        var createBody = new
        {
            portalName = "Provisioned Portal",
            firstName = "Grace",
            lastName = "Hopper",
            username = adminUsername,
            password = "P@ssw0rd123",
            email = "grace.hopper@dnnmigration.local",
            description = "admin + alias provisioning parity",
            keyWords = "provision",
            homeDirectory = "Portals/prov",
            portalAlias = hostAlias
        };

        var createResponse = await _client.PostAsJsonAsync("/api/portals", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();

        // The create response itself reflects the provisioned administrator and the registered alias.
        created.Data!.AdministratorId.Should().BeGreaterThan(0,
            "CreateAsync must provision an initial administrator and wire Portal.AdministratorId to it");
        created.Data.Aliases.Should().Contain(hostAlias,
            "CreateAsync must register the initial HTTP alias so the portal is resolvable by host alias");

        var portalId = created.Data.PortalID;
        var adminId = created.Data.AdministratorId;

        // ---------- RE-READ THE PORTAL -> proves real persistence (not just an echoed projection) ----------
        var getPortal = await _client.GetAsync($"/api/portals/{portalId}");
        getPortal.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedPortal = await getPortal.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        fetchedPortal.Should().NotBeNull();
        fetchedPortal!.Data.Should().NotBeNull();
        fetchedPortal.Data!.AdministratorId.Should().Be(adminId);
        fetchedPortal.Data.Aliases.Should().Contain(hostAlias);

        // ---------- FETCH THE ADMINISTRATOR USER -> proves the user row + portal association were written ----
        // MIGRATION: the default test identity is a host (super) user, so RequirePortalAccess permits reading a
        // user in any portal; the administrator is retrievable and associated with the newly created portal.
        var getAdmin = await _client.GetAsync($"/api/users/{adminId}");
        getAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
        var admin = await getAdmin.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        admin.Should().NotBeNull();
        admin!.Data.Should().NotBeNull();
        admin.Data!.Username.Should().Be(adminUsername);
        admin.Data.PortalID.Should().Be(portalId,
            "the administrator must be associated with the new portal via the UserPortals junction");
    }

    /// <summary>
    /// Deleting a portal must cascade-delete the administrator it provisioned, leaving NO orphaned rows in
    /// [Users], [UserPortals], or [aspnet_Membership]. This is the end-to-end proof for QA finding R10
    /// Issue 2: the create provisions an admin (Users + UserPortals junction + aspnet_Membership credential),
    /// and the delete - which no DB-level FK cascade covers in the existing schema - must remove all of them.
    /// The store is inspected directly (via a scoped DnnDbContext) because the HTTP surface alone cannot
    /// prove the junction and credential rows are gone (a 404 on GET /api/users/{id} only proves the [Users]
    /// row was removed).
    /// </summary>
    /// <returns>A task that completes when the cascade-cleanup assertions have all passed.</returns>
    [Fact]
    public async Task DeletePortal_CascadesAdministrator_LeavesNoOrphanedUserRows()
    {
        // ---------- CREATE -> 201 (provisions admin: Users + UserPortals junction + aspnet_Membership) ------
        const string adminUsername = "itest_admin_cascade";
        const string hostAlias = "cascade-portal.localtest.me";
        var createBody = new
        {
            portalName = "Cascade Portal",
            firstName = "Ada",
            lastName = "Lovelace",
            username = adminUsername,
            password = "P@ssw0rd123",
            email = "ada.lovelace@dnnmigration.local",
            description = "portal delete cascade parity",
            keyWords = "cascade",
            homeDirectory = "Portals/cascade",
            portalAlias = hostAlias
        };

        var createResponse = await _client.PostAsJsonAsync("/api/portals", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.AdministratorId.Should().BeGreaterThan(0);

        var portalId = created.Data.PortalID;
        var adminId = created.Data.AdministratorId;
        var adminMembershipKey = MembershipKeyFor(adminId);

        // ---------- PRECONDITION: the admin identity + junction + credential rows really exist ----------
        var getAdminBefore = await _client.GetAsync($"/api/users/{adminId}");
        getAdminBefore.StatusCode.Should().Be(HttpStatusCode.OK,
            "the provisioned administrator must be retrievable before the portal is deleted");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
            (await db.Users.AsNoTracking().AnyAsync(u => u.UserID == adminId))
                .Should().BeTrue("the administrator [Users] row must exist after create");
            (await db.UserPortals.AsNoTracking().AnyAsync(up => up.PortalId == portalId))
                .Should().BeTrue("the [UserPortals] junction associating the admin with the portal must exist");
            (await db.UserMemberships.AsNoTracking().AnyAsync(m => m.MembershipUserId == adminMembershipKey))
                .Should().BeTrue("the administrator [aspnet_Membership] credential row must exist");
        }

        // ---------- DELETE -> 204 ----------
        var deleteResponse = await _client.DeleteAsync($"/api/portals/{portalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ---------- the portal itself is gone ----------
        var getPortalAfter = await _client.GetAsync($"/api/portals/{portalId}");
        getPortalAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // ---------- CORE Issue 2 assertion via HTTP: the orphaned administrator is gone ----------
        var getAdminAfter = await _client.GetAsync($"/api/users/{adminId}");
        getAdminAfter.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "deleting a portal must cascade-delete the administrator it provisioned (no orphaned Users row)");

        // ---------- CORE Issue 2 assertion via the store: NO orphaned rows of ANY kind remain ----------
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
            (await db.Users.AsNoTracking().AnyAsync(u => u.UserID == adminId))
                .Should().BeFalse("the orphaned administrator's [Users] row must be removed");
            (await db.UserPortals.AsNoTracking().AnyAsync(up => up.PortalId == portalId))
                .Should().BeFalse("every [UserPortals] junction row for the deleted portal must be removed");
            (await db.UserPortals.AsNoTracking().AnyAsync(up => up.UserId == adminId))
                .Should().BeFalse("no [UserPortals] junction row may reference the cascade-deleted administrator");
            (await db.UserMemberships.AsNoTracking().AnyAsync(m => m.MembershipUserId == adminMembershipKey))
                .Should().BeFalse("the orphaned administrator's [aspnet_Membership] credential row must be removed");
        }
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

        // MIGRATION QA finding (Location header leaked internal host): the 201 Location header must be a
        // RELATIVE reference (e.g. "/api/portals/5") so no internal backend host:port (e.g. 127.0.0.1:8080)
        // is disclosed in any deployment topology. Assert it is present, relative (not absolute), and paths
        // at the created portal resource.
        response.Headers.Location.Should().NotBeNull("a 201 create must advertise the resource via Location");
        // KEY assertion for the finding: the URI is RELATIVE, so no internal host:port is exposed.
        response.Headers.Location!.IsAbsoluteUri.Should().BeFalse(
            "the Location header must be a relative URI so no internal host is exposed");
        // ...and it is a rooted path addressing the portals resource (route-token casing is "Portals").
        response.Headers.Location.OriginalString.Should().StartWith("/api/",
            "the relative Location must be a rooted path at the API resource");
        response.Headers.Location.OriginalString.ToLowerInvariant().Should().Contain("/portals/",
            "the Location must address the created portal resource");

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
    /// Reproduces the deterministic projection <c>UserRepository</c> uses to key a DNN integer
    /// <c>UserID</c> onto the <c>uniqueidentifier [aspnet_Membership].[UserId]</c> column
    /// (<c>new Guid(userId, 0, 0, new byte[8])</c>), so the cascade test can assert the credential row is gone
    /// by the same key the repository wrote it under.
    /// </summary>
    /// <param name="userId">The DNN integer user identifier.</param>
    /// <returns>The membership GUID key for <paramref name="userId"/>.</returns>
    private static Guid MembershipKeyFor(int userId) => new(userId, 0, 0, new byte[8]);

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

        /// <summary>
        /// The user-registration mode. MIGRATION QA finding F2: typed as <see cref="int"/> (not the
        /// <c>UserRegistrationType</c> enum) so the test asserts the RAW wire value is the integer code the
        /// Angular numeric dropdown expects (2 = PublicRegistration), proving enums are no longer serialized
        /// as string names.
        /// </summary>
        public int UserRegistration { get; set; }

        /// <summary>
        /// The banner-advertising mode. MIGRATION QA finding F2: typed as <see cref="int"/> so the test
        /// asserts the RAW wire value is the integer code (1 = Banner) the Angular numeric dropdown expects.
        /// </summary>
        public int BannerAdvertising { get; set; }

        /// <summary>The id of the portal's initial administrator user (wired by CreateAsync).</summary>
        public int AdministratorId { get; set; }

        /// <summary>
        /// The portal's globally-unique identifier. MIGRATION QA finding (Portal.guid never generated):
        /// CreateAsync now assigns a real <see cref="System.Guid"/> instead of leaving the all-zero default,
        /// so this must be non-empty after a create and stable across updates. The C# property name maps to
        /// the "guid" wire key under the System.Text.Json camelCase policy.
        /// </summary>
        public Guid GUID { get; set; }

        /// <summary>The portal's registered HTTP aliases (enriched onto the read model by the service).</summary>
        public List<string> Aliases { get; set; } = new();
    }

    /// <summary>
    /// Minimal test-only projection of the user payload; only the members this suite asserts on are modeled.
    /// </summary>
    /// <remarks>
    /// With <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> enabled, the wire keys
    /// <c>"userID"</c>/<c>"username"</c>/<c>"portalID"</c> bind to the PascalCase members below.
    /// <see cref="Username"/> is <c>string?</c> so no CS8618 is raised.
    /// </remarks>
    private sealed class UserRead
    {
        /// <summary>The server-assigned user identifier.</summary>
        public int UserID { get; set; }

        /// <summary>The administrator's login name.</summary>
        public string? Username { get; set; }

        /// <summary>The portal the user is associated with (hydrated from the UserPortals junction).</summary>
        public int PortalID { get; set; }
    }
}
