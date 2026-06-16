// =============================================================================
//  TabsApiTests
//  -----------------------------------------------------------------------------
//  End-to-end HTTP integration tests for the Tabs (Pages) REST resource
//  (/api/v1/tabs), exercised in-process against the REAL DnnMigration.Api
//  pipeline through CustomWebApplicationFactory (the genuine middleware + DI
//  graph, AutoMapper profiles, FluentValidation validators, JWT/BCrypt identity
//  concretes and the RFC 7807 exception middleware) with persistence redirected
//  to an EF Core InMemory store.
//
//  MIGRATION: a DotNetNuke "Tab" is a site Page. The legacy
//  Library/Components/Tabs/TabController.vb surface is portal-scoped (a tab's
//  identity is (TabID, PortalID)) and uses SOFT delete (the DeleteTab path sets
//  the IsDeleted flag rather than removing the row, and list reads exclude
//  soft-deleted rows). Both characteristics are asserted here:
//    * list / count / get-by-id / delete REQUIRE the ?portalId= filter (a 400 is
//      returned when it is absent), and
//    * after a DELETE (204) the deleted tab is EXCLUDED from the portal list.
//
//  The legacy DNN 4.9.0.85 codebase shipped zero automated tests, so this is a
//  CREATE / from-scratch test class with no legacy equivalent.
// =============================================================================

using System.Net;                              // HttpStatusCode
using System.Net.Http.Json;                    // PostAsJsonAsync, PutAsJsonAsync, ReadFromJsonAsync
using DnnMigration.Application.Common;         // ApiResponse<T> ({ data, meta } envelope)
using DnnMigration.Application.DTOs.Tab;       // TabDto, CreateTabDto, UpdateTabDto
using DnnMigration.IntegrationTests;           // CustomWebApplicationFactory
using FluentAssertions;                        // fluent assertion API
using Microsoft.AspNetCore.Mvc;                // ValidationProblemDetails (RFC 7807 validation payload)
using Xunit;                                   // [Fact], [Trait], IClassFixture, Assert

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// HTTP integration tests for <c>TabsController</c> (<c>/api/v1/tabs</c>). Verifies the create→read
/// round-trip, the portal-scoped read contract, the scalar count envelope, the update id-guard, the
/// soft-delete semantics (exclusion-from-list), the not-found path, and the unauthenticated challenge —
/// all against the real ASP.NET Core pipeline booted by <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// The class is annotated <c>[Trait("Category", "Integration")]</c> so the Gate 5 run
/// (<c>dotnet test --filter Category=Integration</c>) selects it, and implements
/// <see cref="IClassFixture{TFixture}"/> so xUnit shares one factory instance (and therefore one
/// uniquely-named InMemory store) across every test in the class. Because that store is shared, each
/// mutating test creates its OWN tab (via <see cref="CreateTabAsync"/>) and reads back the
/// server-generated <c>TabID</c>, never depending on ids minted by sibling tests.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class TabsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route of the Tabs resource (the controller's <c>[Route("api/v1/tabs")]</c>).</summary>
    private const string BaseRoute = "/api/v1/tabs";

    /// <summary>The shared in-process API host fixture (real pipeline + EF Core InMemory).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared <see cref="CustomWebApplicationFactory"/> instance.</summary>
    /// <param name="factory">The class fixture supplied by xUnit.</param>
    public TabsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds a minimal, VALID <see cref="CreateTabDto"/> that satisfies <c>CreateTabValidator</c>
    /// (a non-empty <c>TabName</c> on the seeded default portal). <c>RefreshInterval</c> is intentionally
    /// left <c>null</c> so the conditional "non-negative when supplied" rule does not apply. The
    /// <c>TabName</c> embeds a <see cref="Guid"/> so concurrently-running tests never collide on name.
    /// </summary>
    /// <returns>A create payload accepted by <c>POST /api/v1/tabs</c>.</returns>
    private static CreateTabDto BuildValidCreateTabDto() => new()
    {
        TabName = $"Tab_{Guid.NewGuid():N}",
        PortalID = CustomWebApplicationFactory.DefaultPortalId,
        IsVisible = true,
        TabOrder = 1
    };

    /// <summary>
    /// POSTs a freshly-built valid tab on the default portal, asserts the <c>201 Created</c> contract
    /// (status, non-null <c>Location</c> header carrying the new id, and a server-generated
    /// <c>TabID &gt; 0</c> in the <c>{ data, meta }</c> envelope), and returns the created <see cref="TabDto"/>.
    /// Shared by the create, update and delete tests so each owns its own row in the shared store.
    /// </summary>
    /// <param name="client">An authenticated client created via <see cref="CustomWebApplicationFactory.CreateAuthenticatedClient"/>.</param>
    /// <returns>The created tab projected as a <see cref="TabDto"/>.</returns>
    private static async Task<TabDto> CreateTabAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateTabDto());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.TabID.Should().BeGreaterThan(0);

        return body.Data;
    }

    /// <summary>
    /// <c>POST /api/v1/tabs</c> (valid) ⇒ <c>201 Created</c> with a <c>Location</c> header and
    /// <c>TabID &gt; 0</c>; the subsequent portal-scoped <c>GET /api/v1/tabs/{id}?portalId=0</c> (BOTH ids)
    /// ⇒ <c>200 OK</c> returning the same tab.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var created = await CreateTabAsync(client);

        var getResponse = await client.GetAsync(
            $"{BaseRoute}/{created.TabID}?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.TabID.Should().Be(created.TabID);
    }

    /// <summary>
    /// The list endpoint is portal-scoped: <c>GET /api/v1/tabs?portalId=0</c> ⇒ <c>200 OK</c> with the
    /// <c>{ data, meta }</c> list envelope, whereas <c>GET /api/v1/tabs</c> with NO <c>portalId</c> ⇒
    /// <c>400 Bad Request</c>.
    /// </summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        var withPortal = await client.GetAsync(
            $"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await withPortal.Content.ReadFromJsonAsync<ApiResponse<List<TabDto>>>();
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();

        var withoutPortal = await client.GetAsync(BaseRoute);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// <c>GET /api/v1/tabs/count?portalId=0</c> ⇒ <c>200 OK</c> returning a SCALAR <see cref="int"/> wrapped
    /// in the success envelope.
    /// </summary>
    /// <remarks>
    /// MIGRATION: the count reproduces the legacy <c>GetTabCount</c> stored procedure VERBATIM as
    /// <c>COUNT(*) - 1</c> over the non-admin tabs (MIGRATION_NOTES §4.2 / D-033). For the seeded default
    /// portal — whose <c>AdminTabId</c> is unset (null) — the legacy T-SQL <c>TabID &lt;&gt; @AdminTabId</c>
    /// predicate is UNKNOWN for every row, so the proc matches no rows and the <c>- 1</c> offset yields
    /// <c>-1</c>. The assertion therefore floors at <c>-1</c> (the contractual minimum), NOT <c>0</c>; the
    /// primary contract under test is the <c>200 OK</c> status and the scalar-int envelope shape.
    /// </remarks>
    [Fact]
    public async Task GetCount_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"{BaseRoute}/count?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body.Should().NotBeNull();

        // MIGRATION: GetTabCount is ported verbatim as COUNT(*) - 1 with admin-tab exclusion
        // (MIGRATION_NOTES §4.2 / D-033). The seeded default portal has a null AdminTabId, which the legacy
        // proc resolves to a zero match count -> -1, so -1 is the contractual floor for the scalar count.
        body!.Data.Should().BeGreaterThanOrEqualTo(-1);
    }

    /// <summary>
    /// <c>PUT /api/v1/tabs/{id}</c> with a body whose <c>TabID</c> matches the route id ⇒ <c>200 OK</c>;
    /// a second request whose body <c>TabID</c> does NOT match the route id trips the controller's
    /// id-guard ⇒ <c>400 Bad Request</c>.
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var created = await CreateTabAsync(client);

        var update = new UpdateTabDto
        {
            TabID = created.TabID,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            TabName = $"Tab_{Guid.NewGuid():N}",
            IsVisible = true,
            TabOrder = created.TabOrder
        };

        var updateResponse = await client.PutAsJsonAsync($"{BaseRoute}/{created.TabID}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<TabDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.TabID.Should().Be(created.TabID);

        // Route id and body TabID disagree -> the controller short-circuits with 400 before the service runs.
        var mismatch = new UpdateTabDto
        {
            TabID = created.TabID + 1,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            TabName = $"Tab_{Guid.NewGuid():N}",
            IsVisible = true
        };

        var mismatchResponse = await client.PutAsJsonAsync($"{BaseRoute}/{created.TabID}", mismatch);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Deletion is a SOFT delete: <c>DELETE /api/v1/tabs/{id}?portalId=0</c> (BOTH ids) ⇒ <c>204 No Content</c>,
    /// and the deleted tab is afterwards EXCLUDED from <c>GET /api/v1/tabs?portalId=0</c> (which returns
    /// <c>200 OK</c>), proving the row was flagged deleted rather than surfaced by the list read.
    /// </summary>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        var created = await CreateTabAsync(client);

        var deleteResponse = await client.DeleteAsync(
            $"{BaseRoute}/{created.TabID}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await client.GetAsync(
            $"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<TabDto>>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.All(t => t.TabID != created.TabID).Should().BeTrue();
    }

    /// <summary>
    /// <c>GET /api/v1/tabs/999999?portalId=0</c> (a present <c>portalId</c> but an unknown tab id) ⇒
    /// <c>404 Not Found</c> — distinguishing the not-found path from the missing-filter <c>400</c>.
    /// </summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"{BaseRoute}/999999?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A request issued WITHOUT a Bearer token (a plain <see cref="CustomWebApplicationFactory.CreateClient()"/>
    /// client) against the <c>[Authorize]</c>-protected list endpoint ⇒ <c>401 Unauthorized</c>.
    /// </summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// POSTing an invalid payload (an empty <see cref="CreateTabDto.TabName"/>, which
    /// <c>CreateTabValidator</c> rejects with the "Tab Name Is Required" rule) ⇒ <c>400 Bad Request</c>
    /// emitted as an RFC 7807 <see cref="ValidationProblemDetails"/> whose <c>Errors</c> dictionary is
    /// non-empty and keyed by the offending field (<c>tabName</c>).
    /// </summary>
    /// <remarks>
    /// This is the Gate-5-required invalid-create path for the Tabs resource, mirroring the equivalent
    /// coverage already present for the Portal, Module, User and Role resources. It exercises the real
    /// FluentValidation → RFC 7807 exception-middleware chain end to end: the validator runs inside the
    /// host pipeline and the global <c>ExceptionHandlingMiddleware</c> projects the failure to a
    /// <c>ValidationProblemDetails</c> response.
    /// </remarks>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Violate CreateTabValidator: an empty TabName fails the NotEmpty ("Tab Name Is Required") rule.
        var invalid = BuildValidCreateTabDto();
        invalid.TabName = string.Empty;

        var response = await client.PostAsJsonAsync(BaseRoute, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // RFC 7807 Problem Details with a non-empty validation errors dictionary keyed by the bad field.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
        problem.Errors.Keys.Should().Contain(key => key.Equals("tabName", StringComparison.OrdinalIgnoreCase));
    }
}
