// MIGRATION: [AAP Gate 5 — API Integration Tests] Portal CRUD integration test proving the migrated
// PortalsController honors the Gate-5 status-code contract (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204)
// end-to-end through the REAL Program.cs request pipeline (WebApplicationFactory<Program>) against an EF Core
// InMemory store. This is one of the three mandatory Gate-5 resource controllers (Portal, Module, User).
//
// Source lineage: the legacy VB.NET DotNetNuke.Entities.Portals.PortalController
// (Library/Components/Portal/PortalController.vb — CreatePortal L980, GetPortals L1263, UpdatePortalInfo L1568,
// DeletePortalInfo L1191) and the Website/admin/Portal/Portals.ascx.vb host "Portals" grid (BindData L131 list,
// grdPortals_DeleteCommand L388 delete). Those ViewState/postback admin workflows are now thin JSON REST endpoints;
// this test asserts the externally observable HTTP contract that replaces them.
//
// MIGRATION (test-infra alignment): JSON (de)serialization reuses the shared EnvelopeReader.Web options (camelCase
// web defaults), and the standard success envelope { data, meta } is read through file-local typed records. Those
// records are kept PRIVATE + NESTED on purpose so they can never collide with the sibling *ControllerTests authored
// in parallel within this same namespace. Request bodies use the REAL Application DTOs
// (CreatePortalRequest / UpdatePortalRequest) and responses deserialize into the REAL read DTOs
// (PortalDto / PortalListItemDto), so the test fails to compile if those contracts drift.
//
// MIGRATION (test isolation): CustomWebApplicationFactory gives each test class its own uniquely-named InMemory
// database. Combined with a create-then-act-on-own-id strategy (every test that needs a portal creates one and acts
// on the server-generated id it receives back), the tests are independent of execution order and need no shared seed.
//
// Portals is the HOST-level resource: its endpoints are body/route only — there is NO ?portalId query parameter
// (unlike Modules/Users/Roles/Tabs). The seeded Test super-user (isSuperUser claim) satisfies the controller's
// HostAdministrator authorization policy, so the [Authorize] endpoints return 2xx rather than 401/403.

using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.DTOs.Portal;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Gate-5 CRUD integration tests for <c>PortalsController</c>. Verifies POST -> 201 (with a <c>Location</c> header),
/// GET -> 200, PUT -> 200, DELETE -> 204, GET(nonexistent) -> 404, and the paged list -> 200, exercised against the
/// real API pipeline with an EF Core InMemory store via <see cref="CustomWebApplicationFactory"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PortalsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    // CreatePortalValidator requires BOTH PortalName (NotEmpty, Max128) AND Email (NotEmpty, Max100). Supplying both
    // guarantees the happy-path 201 (a body missing either is a 400). No Admin* bootstrap fields are sent, so the
    // optional portal-administrator group is skipped and the portal is created without provisioning an admin user.
    private const string DefaultPortalName = "Test Portal";
    private const string DefaultAdminEmail = "admin@test.com";

    private readonly CustomWebApplicationFactory _factory;

    public PortalsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>POST /api/portals with a valid body returns 201 Created, a Location header, and the created portal.</summary>
    [Fact]
    public async Task Post_Portal_Returns201Created()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/portals",
            new CreatePortalRequest { PortalName = DefaultPortalName, Email = DefaultAdminEmail },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull(
            "HandleCreated emits a CreatedAtAction Location header pointing at GetById");

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PortalDto>>(EnvelopeReader.Web);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.PortalId.Should().BeGreaterThan(0, "the InMemory store assigns the generated key on insert");
        envelope.Data.PortalName.Should().Be(DefaultPortalName);
    }

    /// <summary>GET /api/portals/{id} for an existing portal returns 200 with the matching portal.</summary>
    [Fact]
    public async Task Get_Portal_ById_Returns200()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        var created = await CreatePortalAsync(client);

        var response = await client.GetAsync($"/api/portals/{created.PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PortalDto>>(EnvelopeReader.Web);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.PortalId.Should().Be(created.PortalId);
        envelope.Data.PortalName.Should().Be(DefaultPortalName);
    }

    /// <summary>PUT /api/portals/{id} with a valid body returns 200 and the updated portal.</summary>
    [Fact]
    public async Task Put_Portal_Returns200()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        var created = await CreatePortalAsync(client);

        // UpdatePortalValidator enforces only PortalName (Max128). The route id is authoritative, but PortalId is also
        // set on the body to mirror the legacy UpdatePortalInfo(PortalId, ...) contract (PortalController.vb L1568).
        const string updatedName = "Updated Portal";
        var response = await client.PutAsJsonAsync(
            $"/api/portals/{created.PortalId}",
            new UpdatePortalRequest { PortalId = created.PortalId, PortalName = updatedName },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PortalDto>>(EnvelopeReader.Web);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.PortalName.Should().Be(updatedName);
    }

    /// <summary>DELETE /api/portals/{id} returns 204 No Content, after which the portal can no longer be read (404).</summary>
    [Fact]
    public async Task Delete_Portal_Returns204()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        var created = await CreatePortalAsync(client);

        var deleteResponse = await client.DeleteAsync($"/api/portals/{created.PortalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm the delete actually removed the row: a subsequent read is a single-read failure -> 404.
        var getResponse = await client.GetAsync($"/api/portals/{created.PortalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// GET /api/portals/{id} for an id that does not exist returns 404 Not Found AND the exact id-bearing RFC 7807
    /// problem-detail message the migrated <c>PortalService</c> emits (CP4 — Test Coverage). Unlike the opaque
    /// not-found messages of the other resources, PortalService echoes the requested id, so the assertion targets
    /// that exact text including the id "999999".
    /// </summary>
    [Fact]
    public async Task Get_Portal_ById_NotFound_Returns404()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();

        // After the reset the store is empty, so id 999999 is guaranteed absent.
        var response = await client.GetAsync("/api/portals/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await EnvelopeReader.ReadProblemDetailAsync(response);
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("Portal 999999 was not found.");
    }

    /// <summary>
    /// GET /api/portals returns 200 with the full paged success envelope <c>{ data, meta }</c>. MIGRATION:
    /// [CP4 review — Test Isolation + Envelope Contract] The test resets to a known-empty store and then creates
    /// EXACTLY TWO portals, so it can assert the EXACT pagination metadata from that known state — totalCount=2 at
    /// pageIndex=0 / pageSize=10, a single total page, and no previous/next page — instead of the previous loose
    /// "greater-than-or-equal" count that tolerated sibling-test rows.
    /// </summary>
    [Fact]
    public async Task Get_Portals_List_Returns200Paged()
    {
        _factory.ResetDatabase();
        var client = _factory.CreateClient();
        var first = await CreatePortalAsync(client, "List Portal A");
        var second = await CreatePortalAsync(client, "List Portal B");

        const int pageSize = 10;
        var response = await client.GetAsync($"/api/portals?pageIndex=0&pageSize={pageSize}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope =
            await response.Content.ReadFromJsonAsync<PagedEnvelope<List<PortalListItemDto>>>(EnvelopeReader.Web);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Meta.Should().NotBeNull();

        // Exact pagination metadata from the known seed (two portals created from an empty store).
        envelope.Meta!.TotalCount.Should().Be(2);
        envelope.Meta.PageIndex.Should().Be(0);
        envelope.Meta.PageSize.Should().Be(pageSize);
        envelope.Meta.TotalPages.Should().Be(1);
        envelope.Meta.HasPreviousPage.Should().BeFalse();
        envelope.Meta.HasNextPage.Should().BeFalse();

        // Both created portals are present on the single page, identified by their server-generated ids.
        envelope.Data!.Should().HaveCount(2);
        envelope.Data.Should().Contain(p => p.PortalId == first.PortalId);
        envelope.Data.Should().Contain(p => p.PortalId == second.PortalId);
    }

    /// <summary>
    /// POSTs a fully-valid portal (PortalName + Email) and returns the created <see cref="PortalDto"/>. Asserts the
    /// 201 status and a non-null envelope so callers can rely on the returned, server-generated portal.
    /// </summary>
    private static async Task<PortalDto> CreatePortalAsync(HttpClient client, string name = DefaultPortalName)
    {
        var response = await client.PostAsJsonAsync(
            "/api/portals",
            new CreatePortalRequest { PortalName = name, Email = DefaultAdminEmail },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PortalDto>>(EnvelopeReader.Web);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    // --- File-local typed envelope readers --------------------------------------------------------------------------
    // The API wraps every success body in the standard envelope { "data": ..., "meta": ... } (ApiControllerBase:
    // Envelope for single resources, HandlePaged for collections). These minimal records deserialize that envelope into
    // the real Portal read DTOs so the tests can assert on strongly-typed values instead of raw JsonElement access.
    // They are intentionally PRIVATE NESTED types: the planned namespace-level helpers (ApiEnvelope/PagedEnvelope) do
    // not exist in this project, and nesting them makes collisions with the parallel sibling *ControllerTests impossible.

    /// <summary>Typed view of the single-resource success envelope <c>{ data: T, meta: {} }</c>.</summary>
    private sealed class ApiEnvelope<T>
    {
        public T? Data { get; init; }
    }

    /// <summary>Typed view of the paged-collection success envelope <c>{ data: T, meta: { ...pagination } }</c>.</summary>
    private sealed class PagedEnvelope<T>
    {
        public T? Data { get; init; }

        public PageMeta? Meta { get; init; }
    }

    /// <summary>The pagination metadata block emitted by <c>ApiControllerBase.HandlePaged</c>.</summary>
    private sealed class PageMeta
    {
        public int TotalCount { get; init; }

        public int PageIndex { get; init; }

        public int PageSize { get; init; }

        public int TotalPages { get; init; }

        public bool HasPreviousPage { get; init; }

        public bool HasNextPage { get; init; }
    }
}
