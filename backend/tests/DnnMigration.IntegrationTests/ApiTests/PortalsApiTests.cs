using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the Portals REST API (<c>/api/v1/portals</c>).
/// </summary>
/// <remarks>
/// <para>
/// These tests run in-process against the REAL <c>DnnMigration.Api</c> pipeline (full middleware +
/// dependency-injection graph) hosted by <see cref="CustomWebApplicationFactory"/>, backed by the EF Core
/// InMemory provider — so no SQL Server is contacted. This is one of the three Gate-5-critical CRUD classes:
/// it asserts the Portal round-trip exactly (<c>POST</c> → 201, <c>GET</c> → 200, <c>PUT</c> → 200,
/// <c>DELETE</c> → 204).
/// </para>
/// <para>
/// MIGRATION: these tests validate the ported CRUD/business logic of the legacy
/// <c>Library/Components/Portal/PortalController.vb</c> (DotNetNuke 4.9.0.85). Portal delete is a HARD delete
/// with transactional cascade (legacy <c>DeletePortalInfo</c>), so a deleted portal subsequently 404s.
/// DotNetNuke shipped zero automated tests, so this is a CREATE-from-scratch class.
/// </para>
/// <para>
/// Self-contained state: the <see cref="IClassFixture{TFixture}"/> shares ONE InMemory store across every
/// test in this class, so each mutating test creates its own portal (<c>POST</c>) and acts on the id read
/// back from the response. The fixture seeds three portals, so a throwaway delete always leaves at least one
/// remaining and never trips the legacy last-portal guard (409).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PortalsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route for the versioned Portals resource (all actions are <c>[Authorize]</c>).</summary>
    private const string BaseUrl = "/api/v1/portals";

    /// <summary>The shared in-process API host fixture (one isolated InMemory store per class instance).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the injected <see cref="CustomWebApplicationFactory"/>.</summary>
    /// <param name="factory">The shared in-process API host fixture provided by xUnit.</param>
    public PortalsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------------------------------
    // Payload builders. A valid-path Portal write must populate EVERY EF-required column
    // (PortalName, DefaultLanguage, HomeDirectory are .IsRequired() in PortalConfiguration); the InMemory
    // provider enforces required (non-null) properties at SaveChanges, so omitting any of them would make a
    // valid-path POST/PUT throw DbUpdateException (500) rather than the expected 201/200. Email is optional,
    // but is supplied as a well-formed address so the conditional email-format rule passes positively.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal-but-valid <see cref="CreatePortalDto"/> that satisfies both the FluentValidation
    /// rules (PortalName required) and the EF Core required-column constraints (PortalName, DefaultLanguage,
    /// HomeDirectory). The name and home directory are made unique so repeated creates never collide.
    /// </summary>
    /// <param name="name">Optional explicit portal name; a unique name is generated when omitted.</param>
    /// <returns>A fully populated, valid create request.</returns>
    private static CreatePortalDto BuildValidCreatePortalDto(string? name = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new CreatePortalDto
        {
            PortalName = name ?? $"Test Portal {unique}",
            DefaultLanguage = "en-US",
            HomeDirectory = $"Portals/{unique}",
            Email = "portal-admin@dnnmigration.local"
        };
    }

    /// <summary>
    /// Builds a valid <see cref="UpdatePortalDto"/> for the supplied portal id, populating the same EF-required
    /// columns so the update's SaveChanges does not null out a required property.
    /// </summary>
    /// <param name="portalId">The identity of the portal being updated (also echoed in <c>PortalID</c>).</param>
    /// <param name="name">Optional explicit portal name; a unique name is generated when omitted.</param>
    /// <returns>A fully populated, valid update request whose <c>PortalID</c> equals <paramref name="portalId"/>.</returns>
    private static UpdatePortalDto BuildValidUpdatePortalDto(int portalId, string? name = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new UpdatePortalDto
        {
            PortalID = portalId,
            PortalName = name ?? $"Updated Portal {unique}",
            DefaultLanguage = "en-US",
            HomeDirectory = $"Portals/{unique}",
            Email = "portal-admin@dnnmigration.local"
        };
    }

    // -------------------------------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------------------------------

    /// <summary>POST a valid portal returns 201 (with a Location header); reading it back by id returns 200.</summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Act: create a portal.
        var createResponse = await client.PostAsJsonAsync(BaseUrl, BuildValidCreatePortalDto());

        // Assert: 201 Created with a Location header pointing at the new resource.
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        createResult.Should().NotBeNull();
        createResult!.Data.Should().NotBeNull();
        var created = createResult!.Data!;
        created.PortalID.Should().BeGreaterThan(0);

        // Act: read the created portal back by id.
        var getResponse = await client.GetAsync($"{BaseUrl}/{created.PortalID}");

        // Assert: 200 OK and the same id round-trips.
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        getResult.Should().NotBeNull();
        getResult!.Data.Should().NotBeNull();
        getResult!.Data!.PortalID.Should().Be(created.PortalID);
    }

    /// <summary>GET the list (both the unfiltered GetAll branch and the ?query= paged branch) returns 200.</summary>
    [Fact]
    public async Task GetList_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Act + Assert: no query -> GetAll branch -> 200.
        var listResponse = await client.GetAsync(BaseUrl);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResult = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<PortalDto>>>();
        listResult.Should().NotBeNull();
        listResult!.Data.Should().NotBeNull();

        // Act + Assert: ?query=... -> paged GetByName branch -> 200.
        var pagedResponse = await client.GetAsync($"{BaseUrl}?query=Default&pageIndex=0&pageSize=10");
        pagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await pagedResponse.Content.ReadFromJsonAsync<ApiResponse<List<PortalDto>>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.Data.Should().NotBeNull();
    }

    /// <summary>PUT with a matching route id and body PortalID returns 200; a mismatch returns 400.</summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a portal to update (self-contained state).
        var createResponse = await client.PostAsJsonAsync(BaseUrl, BuildValidCreatePortalDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        createResult.Should().NotBeNull();
        createResult!.Data.Should().NotBeNull();
        var createdId = createResult!.Data!.PortalID;

        // Act + Assert: route id == body PortalID -> 200 OK.
        var updateResponse = await client.PutAsJsonAsync($"{BaseUrl}/{createdId}", BuildValidUpdatePortalDto(createdId));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act + Assert: route id != body PortalID -> controller id-guard -> 400 Bad Request.
        var mismatchResponse = await client.PutAsJsonAsync($"{BaseUrl}/{createdId}", BuildValidUpdatePortalDto(createdId + 1));
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>DELETE a throwaway portal returns 204 (hard delete); a subsequent GET returns 404.</summary>
    [Fact]
    public async Task Delete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a THROWAWAY portal so the seeded portals stay intact and the last-portal
        // guard (409) is never tripped.
        var createResponse = await client.PostAsJsonAsync(BaseUrl, BuildValidCreatePortalDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<PortalDto>>();
        createResult.Should().NotBeNull();
        createResult!.Data.Should().NotBeNull();
        var createdId = createResult!.Data!.PortalID;

        // Act + Assert: HARD delete -> 204 No Content.
        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{createdId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: the row is gone (hard delete + cascade) -> subsequent GET -> 404 Not Found.
        var getResponse = await client.GetAsync($"{BaseUrl}/{createdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>GET an unknown id returns 404 Not Found.</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{BaseUrl}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>POST an invalid body (empty PortalName) returns 400 with an RFC 7807 validation problem.</summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        // Authenticated: authorization runs before validation, so an anonymous request would 401 before it
        // ever reaches the (deliberately failing) FluentValidation rule.
        var client = _factory.CreateAuthenticatedClient();

        // PortalName is required (CreatePortalValidator.NotEmpty); an empty name fails validation.
        var response = await client.PostAsJsonAsync(BaseUrl, new CreatePortalDto { PortalName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // RFC 7807: the service's ValidateAndThrowAsync surfaces a ValidationProblemDetails
        // (application/problem+json) whose camelCased Errors map carries the failing field.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
        problem!.Errors.Keys.Should().Contain(key => key.Equals("portalName", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A request with no Bearer token returns 401 Unauthorized.</summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // No Authorization header: every /api/v1/portals/* action is [Authorize].
        var client = _factory.CreateClient();

        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
