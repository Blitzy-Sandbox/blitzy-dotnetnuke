using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

// MIGRATION: End-to-end HTTP integration coverage for the Portals REST resource
// (DnnMigration.Api.Controllers.PortalsController -> /api/v1/portals). These tests validate the ported
// business surface of the legacy Library/Components/Portal/PortalController.vb (GetPortals / GetPortal /
// GetPortalsByName / CreatePortal / UpdatePortalInfo / DeletePortalInfo) end to end through the REAL
// ASP.NET Core 8 pipeline: routing, JWT Bearer authentication, permission authorization, model binding,
// FluentValidation, AutoMapper, EF Core (InMemory provider) and the RFC 7807 exception middleware.
//
// This is one of the THREE Gate-5-critical CRUD classes (Portal / Module / User): the suite asserts the
// exact HTTP contract POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204, plus the negative paths
// (unknown id -> 404, invalid body -> 400, id mismatch -> 400, missing bearer -> 401). Portal delete is a
// HARD delete with transactional cascade (legacy DeletePortalInfo, PortalController.vb), so a DELETE
// followed by a GET must yield 404.

/// <summary>
/// xUnit end-to-end HTTP integration tests for the Portals REST API (<c>/api/v1/portals</c>).
/// </summary>
/// <remarks>
/// <para>
/// The class boots the real <c>DnnMigration.Api</c> host in-process via
/// <see cref="CustomWebApplicationFactory"/> (which substitutes an EF Core InMemory store for SQL Server
/// and seeds deterministic fixture data) and drives the live middleware/DI pipeline over HTTP. Because the
/// factory is consumed as an <see cref="IClassFixture{TFixture}"/>, ONE InMemory database instance is shared
/// by every test in this class; consequently each mutating test creates its OWN portal (via <c>POST</c>) and
/// then operates on the returned identifier, so the tests never depend on one another's ordering or state.
/// </para>
/// <para>
/// The <c>[Trait("Category", "Integration")]</c> attribute is MANDATORY: the Gate 5 validation command
/// <c>dotnet test --filter Category=Integration</c> selects tests by this trait, so omitting it would exclude
/// this class from the gate.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class PortalsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route for the Portals resource (URL-path versioned under <c>/api/v1</c>).</summary>
    private const string PortalsRoute = "/api/v1/portals";

    /// <summary>The in-process test host factory shared across this class's tests (one InMemory DB instance).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the test class with the shared <see cref="CustomWebApplicationFactory"/> supplied by xUnit's
    /// <see cref="IClassFixture{TFixture}"/> mechanism.
    /// </summary>
    /// <param name="factory">The in-process API host factory (InMemory-backed, seeded, JWT-capable).</param>
    public PortalsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Happy-path create-then-read: a valid <c>POST</c> returns <c>201 Created</c> with a <c>Location</c> header
    /// and a server-assigned identifier, and a subsequent <c>GET</c> of that identifier returns <c>200 OK</c>
    /// with the same identifier. Validates the ported <c>CreatePortal</c> / <c>GetPortal</c> surface.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // POST a valid payload -> 201 Created with a Location header pointing at the new resource.
        var createResponse = await client.PostAsJsonAsync(PortalsRoute, BuildValidCreatePortalDto());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.PortalID.Should().BeGreaterThan(0);

        var createdId = created.Data.PortalID;

        // GET the freshly created portal by its server-assigned id -> 200 OK with the same id echoed back.
        var getResponse = await client.GetAsync($"{PortalsRoute}/{createdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched.Data!.PortalID.Should().Be(createdId);
    }

    /// <summary>
    /// The list endpoint returns <c>200 OK</c> in both of its branches: the unfiltered <c>GetAll</c> branch
    /// (no query string) and the paged name-prefix <c>GetByName</c> branch (<c>?query=&amp;pageIndex=&amp;pageSize=</c>).
    /// </summary>
    [Fact]
    public async Task GetList_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // No query string -> the controller takes the unfiltered GetAll branch.
        var listResponse = await client.GetAsync(PortalsRoute);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<PortalDto>>>();
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();

        // With a query string -> the controller takes the paged name-prefix GetByName branch.
        var pagedResponse = await client.GetAsync($"{PortalsRoute}?query=Default&pageIndex=0&pageSize=10");
        pagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var paged = await pagedResponse.Content.ReadFromJsonAsync<ApiResponse<List<PortalDto>>>();
        paged.Should().NotBeNull();
        paged!.Data.Should().NotBeNull();
    }

    /// <summary>
    /// A valid <c>PUT</c> whose route id matches the body <see cref="UpdatePortalDto.PortalID"/> returns
    /// <c>200 OK</c>; a <c>PUT</c> whose body id disagrees with the route id is rejected by the controller's
    /// id-guard with <c>400 Bad Request</c>.
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a portal this test owns, then read back its server-assigned id.
        var createResponse = await client.PostAsJsonAsync(PortalsRoute, BuildValidCreatePortalDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        var createdId = created.Data!.PortalID;

        // Act + Assert (happy path): matching ids -> 200 OK.
        var updateDto = BuildValidUpdatePortalDto(createdId);
        updateDto.PortalName = $"Renamed Portal {Guid.NewGuid():N}";

        var updateResponse = await client.PutAsJsonAsync($"{PortalsRoute}/{createdId}", updateDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.PortalID.Should().Be(createdId);

        // Act + Assert (id-mismatch guard): route id != body PortalID -> 400 Bad Request.
        var mismatchDto = BuildValidUpdatePortalDto(createdId + 1000);
        var mismatchResponse = await client.PutAsJsonAsync($"{PortalsRoute}/{createdId}", mismatchDto);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Deleting a portal returns <c>204 No Content</c>, and because the legacy delete is a HARD delete with
    /// transactional cascade (<c>DeletePortalInfo</c>), a subsequent <c>GET</c> of the deleted id returns
    /// <c>404 Not Found</c>. A THROWAWAY portal is created and deleted so the seeded portals (≥ 3) keep the
    /// store above the API's last-portal guard (which would otherwise yield <c>409</c>).
    /// </summary>
    [Fact]
    public async Task Delete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a throwaway portal dedicated to this delete test.
        var createResponse = await client.PostAsJsonAsync(PortalsRoute, BuildValidCreatePortalDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        var createdId = created.Data!.PortalID;

        // Act: hard delete -> 204 No Content.
        var deleteResponse = await client.DeleteAsync($"{PortalsRoute}/{createdId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: the row is gone (HARD delete) -> a follow-up GET is 404.
        var getResponse = await client.GetAsync($"{PortalsRoute}/{createdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Requesting a portal that does not exist returns <c>404 Not Found</c> (the controller's
    /// <c>NotFound()</c> short-circuit when the service yields <see langword="null"/>).
    /// </summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{PortalsRoute}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Posting an invalid payload (empty <see cref="CreatePortalDto.PortalName"/>, which the
    /// <c>CreatePortalValidator</c> rejects) returns <c>400 Bad Request</c> as an RFC 7807
    /// <see cref="ValidationProblemDetails"/> whose <c>Errors</c> collection is non-empty and keyed by the
    /// offending field (<c>portalName</c>).
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // PortalName is the single NotEmpty rule in CreatePortalValidator; an empty value must fail validation.
        var response = await client.PostAsJsonAsync(PortalsRoute, new CreatePortalDto { PortalName = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The exception middleware emits RFC 7807 problem+json with a ValidationProblemDetails payload.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
        problem.Errors.Keys.Should().Contain(key => key.Equals("portalName", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A request carrying no bearer token is rejected by the authentication/authorization pipeline with
    /// <c>401 Unauthorized</c> (all <c>/api/v1/portals/*</c> actions are <c>[Authorize]</c>-protected).
    /// </summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // A plain client carries no Authorization header, so the protected endpoint challenges.
        var client = _factory.CreateClient();

        var response = await client.GetAsync(PortalsRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal-but-valid <see cref="CreatePortalDto"/> that passes <c>CreatePortalValidator</c>.
    /// </summary>
    /// <param name="name">
    /// Optional portal name. When <see langword="null"/>, a unique name is generated so repeated creates within
    /// the shared InMemory store never collide.
    /// </param>
    /// <returns>A populated <see cref="CreatePortalDto"/> with a non-empty name and sensible defaults.</returns>
    /// <remarks>
    /// Only <c>PortalName</c> is required by the validator; <c>Email</c> is intentionally left unset so its
    /// "valid format when supplied" rule does not fire. The remaining fields are populated with realistic,
    /// schema-faithful values so the create path mirrors a genuine request.
    /// </remarks>
    private static CreatePortalDto BuildValidCreatePortalDto(string? name = null) => new()
    {
        PortalName = name ?? $"Test Portal {Guid.NewGuid():N}",
        Description = "Integration-test portal.",
        HomeDirectory = "Portals/test",
        Currency = "USD",
        DefaultLanguage = "en-US",
        Version = "4.9.0.85",
        AdministratorId = CustomWebApplicationFactory.AdminUserId,
        HostFee = 0f,
        GUID = Guid.NewGuid()
    };

    /// <summary>
    /// Builds a minimal-but-valid <see cref="UpdatePortalDto"/> that passes <c>UpdatePortalValidator</c> for the
    /// supplied portal identifier.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to update; assigned to <see cref="UpdatePortalDto.PortalID"/>.</param>
    /// <returns>A populated <see cref="UpdatePortalDto"/> carrying the supplied id, a non-empty name and defaults.</returns>
    /// <remarks>
    /// The validator requires a positive <c>PortalID</c> and a non-empty <c>PortalName</c>; <c>Email</c> is left
    /// unset for the same reason as in <see cref="BuildValidCreatePortalDto(string?)"/>.
    /// </remarks>
    private static UpdatePortalDto BuildValidUpdatePortalDto(int portalId) => new()
    {
        PortalID = portalId,
        PortalName = $"Updated Portal {Guid.NewGuid():N}",
        Description = "Integration-test portal (updated).",
        HomeDirectory = "Portals/test",
        Currency = "USD",
        DefaultLanguage = "en-US",
        Version = "4.9.0.85",
        AdministratorId = CustomWebApplicationFactory.AdminUserId,
        HostFee = 0f,
        GUID = Guid.NewGuid()
    };
}
