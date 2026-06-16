// =============================================================================
//  UsersApiTests
//  -----------------------------------------------------------------------------
//  End-to-end HTTP integration tests for the Users REST API (/api/v1/users),
//  driven in-process against the REAL DnnMigration.Api pipeline (genuine
//  middleware + DI graph, AutoMapper profiles, FluentValidation validators,
//  JWT/BCrypt identity concretes and the RFC 7807 exception middleware) through
//  CustomWebApplicationFactory, with persistence redirected to an EF Core
//  InMemory store. This is one of the three Gate-5-critical resource suites
//  (Portal / Module / User): a successful run exercises User CRUD over HTTP
//  (POST 201, GET 200, PUT 200, DELETE 204).
//
//  MIGRATION: the legacy DotNetNuke 4.9.0.85 codebase shipped zero automated
//  tests, so this is a CREATE / from-scratch suite (no legacy equivalent). It
//  validates the migrated, ported business surface of
//  Library/Components/Users/UserController.vb (paged GetUsers + CreateUser /
//  UpdateUser / DeleteUser) re-expressed as a versioned REST resource.
//
//  Self-contained state rule: the InMemory store is shared across every test in
//  this class (one IClassFixture instance). Each mutating test therefore POSTs
//  its OWN user with a unique Username/Email on PortalID = 0 and reads the
//  generated UserID back, so the tests never depend on one another's data or on
//  execution order.
// =============================================================================

using System.Net;                          // HttpStatusCode
using System.Net.Http.Json;                // PostAsJsonAsync, PutAsJsonAsync, ReadFromJsonAsync
using DnnMigration.Application.Common;     // ApiResponse<T> ({ data, meta } success envelope)
using DnnMigration.Application.DTOs.User;  // UserDto, CreateUserDto, UpdateUserDto
using FluentAssertions;                    // fluent assertion API
using Microsoft.AspNetCore.Mvc;            // ValidationProblemDetails (RFC 7807 validation errors)
using Xunit;                               // Fact, Trait, IClassFixture

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// HTTP integration tests for the Users resource controller, asserting the exact REST contract:
/// route <c>/api/v1/users</c>, <c>[Authorize]</c> on every endpoint, a required <c>portalId</c> on the
/// list endpoint, a portal-agnostic GET-by-id, and the documented status codes (POST 201 + Location,
/// GET 200, PUT 200, DELETE 204).
/// </summary>
[Trait("Category", "Integration")]
public sealed class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route of the Users REST resource (URL-path versioned per the API standard).</summary>
    private const string UsersRoute = "/api/v1/users";

    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared in-process API fixture.</summary>
    /// <param name="factory">The xUnit class fixture that boots the real API on an InMemory database.</param>
    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// POST a valid user -> 201 Created with a Location header and a positive generated UserID, then
    /// GET that user by id (portal-agnostic, no portalId required) -> 200 OK with the same UserID.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var request = BuildValidCreateUserDto();

        var createResponse = await client.PostAsJsonAsync(UsersRoute, request);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await ReadUserAsync(createResponse);
        created.UserID.Should().BeGreaterThan(0);

        // GET /{id} is portal-agnostic: no portalId query parameter is needed.
        var getResponse = await client.GetAsync($"{UsersRoute}/{created.UserID}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await ReadUserAsync(getResponse);
        fetched.UserID.Should().Be(created.UserID);
    }

    /// <summary>
    /// The list endpoint requires a <c>portalId</c>: portal 0 is a valid scope (200), paging parameters are
    /// honoured (200), but omitting <c>portalId</c> trips the controller's nullable <c>HasValue</c> guard (400).
    /// </summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Portal 0 is the seeded default portal and a VALID list scope.
        var withPortal = await client.GetAsync(
            $"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        // Explicit paging parameters are accepted.
        var withPaging = await client.GetAsync(
            $"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}&pageIndex=0&pageSize=20");
        withPaging.StatusCode.Should().Be(HttpStatusCode.OK);

        // portalId is REQUIRED — omitting it yields a 400 (portal 0 is valid, so this is a true
        // "missing parameter" failure rather than a "portal 0 rejected" one).
        var withoutPortal = await client.GetAsync(UsersRoute);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// PUT an update whose body UserID matches the route id -> 200 OK; a route id that does NOT match the
    /// body UserID is rejected by the controller's identifier guard -> 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateUserAsync(client);

        // UpdateUserDto carries the UserID plus the mutable profile fields only
        // (Username/Password/PortalID are immutable on update by design).
        var update = new UpdateUserDto
        {
            UserID = created.UserID,
            DisplayName = "Updated Display Name",
            Email = $"updated_{Guid.NewGuid():N}@dnnmigration.local",
            FirstName = "Updated",
            LastName = "User",
            IsSuperUser = created.IsSuperUser,
            Approved = true
        };

        var updateResponse = await client.PutAsJsonAsync($"{UsersRoute}/{created.UserID}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await ReadUserAsync(updateResponse);
        updated.UserID.Should().Be(created.UserID);
        updated.DisplayName.Should().Be("Updated Display Name");

        // Route id must match the body UserID; a mismatch is rejected before the service runs.
        var mismatchResponse = await client.PutAsJsonAsync($"{UsersRoute}/{created.UserID + 1}", update);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// DELETE a user -> 204 No Content, after which the user is excluded from the portal listing.
    /// </summary>
    /// <remarks>
    /// MIGRATION: the file-implementation prompt framed User delete as a SOFT delete, but the migrated
    /// <c>UsersController</c>/<c>UserService</c>/<c>UserRepository</c> perform a documented HARD delete —
    /// the DNN 4.9 <c>dbo.Users</c> table has no <c>IsDeleted</c> column and ADR-002 forbids a schema change
    /// (MIGRATION_NOTES.md §6.3 / D-014). From this black-box HTTP test the two strategies are
    /// observationally identical: DELETE returns 204 and the user is thereafter EXCLUDED from the portal
    /// listing. This test asserts exactly that observable contract, so it holds under either strategy.
    /// </remarks>
    [Fact]
    public async Task Delete_RemovesUserFromList_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateUserAsync(client);

        var deleteResponse = await client.DeleteAsync($"{UsersRoute}/{created.UserID}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A generous page size guarantees the user WOULD appear on this page if it had not been removed,
        // making the exclusion assertion meaningful regardless of how many users sibling tests created.
        var listResponse = await client.GetAsync(
            $"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}&pageIndex=0&pageSize=200");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await ReadUserListAsync(listResponse);
        users.Should().NotContain(u => u.UserID == created.UserID);
    }

    /// <summary>GET an id that does not exist -> 404 Not Found.</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{UsersRoute}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// POST a body that violates the create validation rules -> 400 Bad Request with an RFC 7807
    /// Problem Details payload carrying a non-empty <c>errors</c> dictionary.
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Empty Username/Email (and missing Password/names) violate CreateUserValidator. The service calls
        // ValidateAndThrowAsync, which throws FluentValidation.ValidationException; ExceptionHandlingMiddleware
        // surfaces it as an application/problem+json 400 carrying an `errors` dictionary.
        var invalid = new CreateUserDto
        {
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            Username = string.Empty,
            Email = string.Empty
        };

        var response = await client.PostAsJsonAsync(UsersRoute, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>A request with no Bearer token against an <c>[Authorize]</c> endpoint -> 401 Unauthorized.</summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // A plain client carries NO Authorization header. portalId=0 is supplied so the ONLY reason the
        // request can fail is the missing authentication (a 401 challenge before the action ever runs),
        // never the list endpoint's portalId guard.
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// POSTs a freshly-built valid user through the supplied (authenticated) client, asserts the 201, and
    /// returns the created <see cref="UserDto"/> (with its server-generated <c>UserID</c>).
    /// </summary>
    /// <param name="client">An authenticated client able to satisfy the create policy.</param>
    /// <returns>The created user projected as a <see cref="UserDto"/>.</returns>
    private static async Task<UserDto> CreateUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(UsersRoute, BuildValidCreateUserDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadUserAsync(response);
    }

    /// <summary>
    /// Builds a <see cref="CreateUserDto"/> that satisfies every <c>CreateUserValidator</c> rule, with a
    /// globally-unique Username and Email on PortalID 0 so concurrent/sequential tests never collide.
    /// </summary>
    /// <returns>A valid, unique user-creation request.</returns>
    private static CreateUserDto BuildValidCreateUserDto()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new CreateUserDto
        {
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            Username = $"user_{unique}",
            Password = "Str0ngP@ssw0rd!",
            DisplayName = $"Test User {unique[..8]}",
            Email = $"user_{unique}@dnnmigration.local",
            FirstName = "Test",
            LastName = "User",
            IsSuperUser = false,
            Approved = true
        };
    }

    /// <summary>
    /// Deserializes a success response into <see cref="ApiResponse{T}"/> of <see cref="UserDto"/> and
    /// returns its non-null <c>Data</c> payload, asserting the envelope and payload are present.
    /// </summary>
    /// <param name="response">The HTTP response carrying the success envelope.</param>
    /// <returns>The non-null <see cref="UserDto"/> payload.</returns>
    private static async Task<UserDto> ReadUserAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    /// <summary>
    /// Deserializes a list/paged success response into <see cref="ApiResponse{T}"/> of
    /// <c>List&lt;UserDto&gt;</c> and returns its non-null <c>Data</c> collection.
    /// </summary>
    /// <param name="response">The HTTP response carrying the paged success envelope.</param>
    /// <returns>The non-null list of <see cref="UserDto"/> items.</returns>
    private static async Task<List<UserDto>> ReadUserListAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }
}
