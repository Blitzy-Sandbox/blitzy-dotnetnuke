// =============================================================================
// DnnMigration.IntegrationTests — ApiTests/TabsApiTests.cs
// =============================================================================
// End-to-end HTTP integration tests for the Tabs (pages) REST API, executed
// IN-PROCESS against the REAL DnnMigration.Api pipeline (full middleware + DI)
// through CustomWebApplicationFactory, backed by the EF Core InMemory provider
// (no SQL Server is ever contacted). The class is tagged
// [Trait("Category","Integration")] so Validation Gate 5
// (`dotnet test --filter Category=Integration`) selects it.
//
// MIGRATION: source_files = Library/Components/Tabs/TabController.vb
// (DotNetNuke 4.9.0.85). Two ported semantics drive these tests:
//   1. Tabs are PORTAL-SCOPED — the list, count, get-by-id, and delete routes
//      all REQUIRE a ?portalId= query parameter (legacy GetTabs/GetTabCount/
//      GetTab/DeleteTab were keyed by PortalId), and omitting it yields 400.
//   2. Tab delete is a SOFT delete (the [Tabs].IsDeleted flag; legacy DeleteTab
//      set IsDeleted = True after a child-tab guard), so the delete test asserts
//      the row is EXCLUDED from the subsequent list read rather than physically
//      removed.
// DotNetNuke 4.9.0.85 shipped zero automated tests, so this is a
// CREATE-from-scratch test class with no legacy equivalent.
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// HTTP integration tests covering the Tabs resource controller
/// (<c>/api/v1/tabs</c>): the create → read round-trip, portal-scoped list and
/// count, the update id-guard, soft-delete exclusion, the unknown-id 404, and
/// the anonymous 401 challenge.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TabsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Versioned base route for the Tabs resource.</summary>
    private const string BaseRoute = "/api/v1/tabs";

    /// <summary>
    /// Portal scope used by every test. Bound to the factory's seeded default portal
    /// (PortalID 0) so the portal-scoped routes resolve against real seeded data.
    /// </summary>
    private const int PortalId = CustomWebApplicationFactory.DefaultPortalId;

    private readonly CustomWebApplicationFactory _factory;

    public TabsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="CreateTabDto"/> that satisfies <c>CreateTabValidator</c>
    /// (whose only rule is <c>TabName</c> NotEmpty) on the seeded default portal.
    /// <c>TabName</c> is globally unique so the shared per-class InMemory store never
    /// collides across tests.
    /// </summary>
    private static CreateTabDto BuildValidCreateTabDto() => new()
    {
        TabName = $"Tab_{Guid.NewGuid():N}",
        PortalID = PortalId,
        IsVisible = true,
        TabOrder = 1
    };

    /// <summary>
    /// POSTs a fresh valid tab on the default portal, asserts 201 Created, and returns
    /// the created <see cref="TabDto"/>. Each mutating test calls this so it owns its
    /// own row in the shared InMemory database (self-contained-state rule).
    /// </summary>
    private static async Task<TabDto> CreateTabAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateTabDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        return body!.Data!;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// POST a valid tab returns 201 Created with a Location header and a generated id;
    /// the follow-up portal-scoped GET-by-id returns 200 with the same id.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // POST a valid tab -> 201 Created with a Location header.
        var createResponse = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateTabDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();

        var createdId = created!.Data!.TabID;
        createdId.Should().BeGreaterThan(0);

        // GET by id REQUIRES both the route id AND ?portalId= -> 200 with the same id.
        var getResponse = await client.GetAsync($"{BaseRoute}/{createdId}?portalId={PortalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched!.Data!.TabID.Should().Be(createdId);
    }

    /// <summary>
    /// The list endpoint returns 200 when scoped with <c>?portalId=</c> and 400 when the
    /// required portal scope is omitted.
    /// </summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        // WITH ?portalId= -> 200 and a (non-null) list payload.
        var withPortal = await client.GetAsync($"{BaseRoute}?portalId={PortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await withPortal.Content.ReadFromJsonAsync<ApiResponse<List<TabDto>>>();
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();

        // WITHOUT portalId -> 400 Bad Request (the controller requires the portal scope).
        var withoutPortal = await client.GetAsync(BaseRoute);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The count endpoint returns 200 with the scalar count carried in the envelope's
    /// <c>data</c> member.
    /// </summary>
    [Fact]
    public async Task GetCount_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{BaseRoute}/count?portalId={PortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body.Should().NotBeNull();

        // MIGRATION: the count faithfully reproduces the legacy GetTabCount stored
        // procedure (TabController.vb L512) — "SELECT COUNT(*) - 1 FROM {oq}Tabs WHERE
        // PortalID = @PortalID AND TabID <> @AdminTabId AND (ParentId <> @AdminTabId OR
        // ParentId IS NULL)". The seeded default portal has a NULL AdminTabId, so the
        // procedure's SQL three-valued logic makes "TabID <> @AdminTabId" UNKNOWN for
        // every row: COUNT(*) is 0 and it returns 0 - 1 = -1 (MIGRATION_NOTES.md
        // DEV-054). The assertion pins that exact ported scalar conveyed through the
        // { data } success envelope.
        body!.Data.Should().Be(-1);
    }

    /// <summary>
    /// PUT with a matching route id and body <c>TabID</c> returns 200; a mismatched
    /// route id returns 400 (the controller's id-guard).
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateTabAsync(client);

        var update = new UpdateTabDto
        {
            TabID = created.TabID,
            PortalID = PortalId,
            TabName = $"Tab_{Guid.NewGuid():N}",
            IsVisible = true,
            TabOrder = 2
        };

        // Matching route id and body TabID -> 200 OK.
        var okResponse = await client.PutAsJsonAsync($"{BaseRoute}/{created.TabID}", update);
        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await okResponse.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated!.Data!.TabID.Should().Be(created.TabID);

        // Route id that does NOT match the body TabID -> 400 Bad Request (id-guard).
        var mismatchResponse = await client.PutAsJsonAsync($"{BaseRoute}/{created.TabID + 1}", update);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// DELETE returns 204 and is a SOFT delete: the deleted tab is afterward EXCLUDED
    /// from the portal's list read.
    /// </summary>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateTabAsync(client);
        var deletedId = created.TabID;

        // DELETE REQUIRES both the route id AND ?portalId= -> 204 No Content (SOFT delete).
        var deleteResponse = await client.DeleteAsync($"{BaseRoute}/{deletedId}?portalId={PortalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Soft delete => the row still exists but is EXCLUDED from list reads.
        var listResponse = await client.GetAsync($"{BaseRoute}?portalId={PortalId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<TabDto>>>();
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();
        list!.Data!.Should().NotContain(t => t.TabID == deletedId);
    }

    /// <summary>
    /// GET-by-id with a valid portal scope but a non-existent id returns 404.
    /// </summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        // portalId is supplied (so this is NOT the 400 missing-scope path) but the id
        // does not exist within the portal -> 404 Not Found.
        var response = await client.GetAsync($"{BaseRoute}/999999?portalId={PortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A request without a Bearer token against a protected route returns 401.
    /// </summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // Plain client (no Authorization header) against an [Authorize] route -> 401.
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{BaseRoute}?portalId={PortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// POST an invalid body (empty <c>TabName</c>) returns 400 with an RFC 7807
    /// <see cref="ValidationProblemDetails"/> whose camelCased <c>errors</c> map carries the failing
    /// <c>tabName</c> field. This is the per-entity invalid-create negative case that brings Tabs to
    /// parity with Portal/Module/User/Role (QA Finding F1).
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        // Authenticated: authorization runs before validation, so an anonymous request would 401 before it
        // ever reaches the (deliberately failing) FluentValidation rule.
        var client = _factory.CreateAuthenticatedClient();

        // TabName is required (CreateTabValidator: NotEmpty -> "Page Name Is Required"). An empty TabName
        // fails validation inside TabService.CreateAsync (ValidateAndThrowAsync) BEFORE any EF persistence,
        // so the request never reaches the data layer. PortalID is a valid seeded scope but is irrelevant to
        // the single TabName rule.
        var response = await client.PostAsJsonAsync(BaseRoute, new CreateTabDto { TabName = "", PortalID = PortalId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // RFC 7807: the service's ValidateAndThrowAsync surfaces a ValidationProblemDetails
        // (application/problem+json) whose camelCased Errors map carries the failing field.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
        problem!.Errors.Keys.Should().Contain(key => key.Equals("tabName", StringComparison.OrdinalIgnoreCase));
    }
}
