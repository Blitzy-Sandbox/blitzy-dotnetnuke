using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the Users REST API (<c>/api/v1/users</c>), exercised in-process
/// against the REAL <c>DnnMigration.Api</c> middleware + dependency-injection pipeline via
/// <see cref="CustomWebApplicationFactory"/> (EF Core InMemory; no SQL Server contacted).
/// </summary>
/// <remarks>
/// <para>
/// This is one of the three Gate-5-critical CRUD round-trip classes (Portal/Module/User). It asserts the
/// exact HTTP contract the AAP mandates: <c>POST</c> 201 (+ <c>Location</c>), <c>GET</c> 200, <c>PUT</c> 200,
/// <c>DELETE</c> 204, plus the error envelope (RFC 7807) for 400/401/404.
/// </para>
/// <para>
/// MIGRATION (DEV-039): although the migration plan describes Users as a "soft delete", the [Users] table has
/// no <c>IsDeleted</c> column and ADR-002 forbids schema changes, so <c>UserService.DeleteAsync</c> performs a
/// HARD delete (faithful to the legacy <c>UserController.DeleteUser</c>, which removed the row via the
/// membership provider). The OBSERVABLE behavior asserted here is identical for either mechanism: after a
/// successful <c>DELETE</c> the user is EXCLUDED from the portal list — see
/// <see cref="Delete_IsSoftDelete_Returns204"/>.
/// </para>
/// <para>
/// State rule: the fixture exposes a single InMemory store shared by every test in this class (xUnit runs the
/// tests of one class sequentially), so each mutating test POSTs its OWN user with a unique
/// <c>Username</c>/<c>Email</c> on <c>PortalID = 0</c> and reads back the generated id — no test depends on
/// another's rows. MIGRATION: there is no legacy equivalent — DotNetNuke 4.9.0.85 shipped zero automated tests.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Relative path of the Users resource collection.</summary>
    private const string UsersRoute = "/api/v1/users";

    /// <summary>The shared in-process API host + InMemory database fixture for this test class.</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the in-process API fixture supplied by xUnit.</summary>
    /// <param name="factory">The shared <see cref="CustomWebApplicationFactory"/> instance.</param>
    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// POST a valid user, expect 201 + a <c>Location</c> header and a positive generated id; then GET the user
    /// by that id (the by-id route is portal-agnostic and needs no <c>portalId</c>) and expect 200 with the
    /// same id echoed back.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(UsersRoute, BuildValidCreateUserDto());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.UserID.Should().BeGreaterThan(0);

        var id = created.Data.UserID;

        var getResponse = await client.GetAsync($"{UsersRoute}/{id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched.Data!.UserID.Should().Be(id);
    }

    /// <summary>
    /// The list endpoint REQUIRES a <c>portalId</c> filter: <c>?portalId=0</c> succeeds (portal 0 is a valid
    /// scope), an explicit page request succeeds, and OMITTING <c>portalId</c> entirely yields 400 (the
    /// controller's nullable <c>HasValue</c> guard).
    /// </summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        var withPortal = await client.GetAsync($"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await withPortal.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        list.Should().NotBeNull();
        // The portal-scoped list JOINs the physical [UserPortals] membership table; the seeded admin is a
        // SuperUser with no membership row, so portal 0 may legitimately be empty. Asserting that the envelope's
        // data array deserialized (non-null) is the portable contract check — no specific membership is assumed.
        list!.Data.Should().NotBeNull();

        var paged = await client.GetAsync($"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}&pageIndex=0&pageSize=20");
        paged.StatusCode.Should().Be(HttpStatusCode.OK);

        var withoutPortal = await client.GetAsync(UsersRoute);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// PUT with a body whose <c>UserID</c> matches the route id updates the user and returns 200; a body whose
    /// <c>UserID</c> does NOT match the route id is rejected with 400 (the controller's identifier-mismatch guard).
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var createdId = await CreateUserAsync(client);

        var update = new UpdateUserDto
        {
            UserID = createdId,
            DisplayName = "Updated Display Name",
            Email = $"updated_{Guid.NewGuid():N}@dnnmigration.local",
            FirstName = "Updated",
            LastName = "Person",
            IsSuperUser = false,
            Approved = true
        };

        var updateResponse = await client.PutAsJsonAsync($"{UsersRoute}/{createdId}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.UserID.Should().Be(createdId);
        updated.Data.DisplayName.Should().Be("Updated Display Name");

        // Identifier mismatch: route id != body UserID -> 400 (guard runs before the service/validator).
        var mismatch = new UpdateUserDto
        {
            UserID = createdId + 1,
            DisplayName = "Mismatch",
            Email = $"mismatch_{Guid.NewGuid():N}@dnnmigration.local",
            FirstName = "Mis",
            LastName = "Match",
            IsSuperUser = false,
            Approved = true
        };

        var mismatchResponse = await client.PutAsJsonAsync($"{UsersRoute}/{createdId}", mismatch);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// DELETE returns 204 and the user is afterwards EXCLUDED from the portal list.
    /// </summary>
    /// <remarks>
    /// MIGRATION (DEV-039): the deletion is implemented as a HARD delete (the [Users] table has no
    /// <c>IsDeleted</c> column; ADR-002 forbids a schema change), but the exclusion-from-list invariant the
    /// test name implies holds identically. A FRESH, non-administrator user is created and deleted here so the
    /// administrator guard in <c>UserService.DeleteAsync</c> (which would 409 on the seeded admin, the
    /// <c>AdministratorId</c> of every seeded portal) is never tripped. The list is requested with a large page
    /// size so the assertion is meaningful — the deleted user would appear on the page if it still existed.
    /// </remarks>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();
        var deletedId = await CreateUserAsync(client);

        var deleteResponse = await client.DeleteAsync($"{UsersRoute}/{deletedId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The hard delete removes the row outright, so a subsequent by-id lookup now 404s. This is the
        // strongest, data-model-independent proof the user is gone (the by-id route is portal-agnostic).
        var getDeleted = await client.GetAsync($"{UsersRoute}/{deletedId}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // ...and the user is EXCLUDED from the portal list — the AAP-prescribed exclusion assertion, which
        // holds identically whether the delete is soft or hard.
        var listResponse = await client.GetAsync($"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}&pageIndex=0&pageSize=1000");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();
        list.Data!.All(u => u.UserID != deletedId).Should().BeTrue();
    }

    /// <summary>GET by an id that does not exist returns 404 (RFC 7807 Problem Details).</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{UsersRoute}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// POST with an invalid body (missing the required Username/Email/etc.) is rejected with 400 and an RFC 7807
    /// <see cref="ValidationProblemDetails"/> whose <c>errors</c> map is non-empty.
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // PortalID alone is valid (0); every required string field (Username/Password/DisplayName/Email/
        // FirstName/LastName) is left null, so CreateUserValidator raises multiple NotEmpty failures.
        var invalid = new CreateUserDto { PortalID = CustomWebApplicationFactory.DefaultPortalId };

        var response = await client.PostAsJsonAsync(UsersRoute, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>A request without a Bearer token is rejected with 401 before the action runs.</summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a valid <see cref="CreateUserDto"/> that satisfies <c>CreateUserValidator</c> in full, with a
    /// unique <c>Username</c> and <c>Email</c> (so concurrent/sequential tests never collide on the shared
    /// InMemory store) scoped to the default portal (<c>PortalID = 0</c>).
    /// </summary>
    /// <returns>A fully-populated, valid creation request.</returns>
    private static CreateUserDto BuildValidCreateUserDto()
    {
        var unique = Guid.NewGuid().ToString("N");

        return new CreateUserDto
        {
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            Username = $"user_{unique}",
            Password = "P@ssw0rd!2024",
            DisplayName = "Integration Test User",
            Email = $"user_{unique}@dnnmigration.local",
            FirstName = "Integration",
            LastName = "Tester",
            IsSuperUser = false,
            Approved = true
        };
    }

    /// <summary>
    /// POSTs a fresh valid user through the supplied client, asserts the 201 contract, and returns the
    /// generated <c>UserID</c> for follow-up GET/PUT/DELETE calls.
    /// </summary>
    /// <param name="client">An authenticated client able to satisfy the Edit policy on POST.</param>
    /// <returns>The generated identifier of the newly created user.</returns>
    private static async Task<int> CreateUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(UsersRoute, BuildValidCreateUserDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.UserID.Should().BeGreaterThan(0);

        return created.Data.UserID;
    }
}
