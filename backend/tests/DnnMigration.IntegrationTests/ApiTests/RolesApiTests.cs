// =============================================================================
//  RolesApiTests
//  -----------------------------------------------------------------------------
//  End-to-end HTTP integration tests for the Roles REST API (/api/v1/roles) and
//  its user-role membership sub-resources, exercised in-process against the REAL
//  DnnMigration.Api pipeline (genuine middleware, DI graph, AutoMapper profiles,
//  FluentValidation validators, JWT/BCrypt identity and the RFC 7807 exception
//  middleware) through CustomWebApplicationFactory, with persistence redirected
//  to an EF Core InMemory store. Validates Gate 5 (`dotnet test --filter
//  Category=Integration`): POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204.
//
//  MIGRATION: ports the public business surface of
//  Library/Components/Security/Roles/RoleController.vb. Role DELETE is a HARD
//  delete (legacy provider.DeleteRole — the Roles table carries no IsDeleted
//  column), so the suite asserts that a GET after a DELETE returns 404. The
//  legacy DNN 4.9.0.85 codebase shipped zero automated tests, so this is a
//  CREATE / from-scratch file with no legacy equivalent.
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.IntegrationTests;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using FluentAssertions;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// HTTP integration tests for <c>RolesController</c> (<c>/api/v1/roles</c>) and the user-role
/// membership sub-resources, run end to end against the real API pipeline hosted by
/// <see cref="CustomWebApplicationFactory"/> (EF Core InMemory persistence).
/// </summary>
/// <remarks>
/// The single shared <see cref="CustomWebApplicationFactory"/> (xUnit
/// <see cref="IClassFixture{TFixture}"/>) means every test in this class talks to the SAME InMemory
/// store, so each mutating test is self-contained: it POSTs the role (and, for membership, the user)
/// it needs and reads the server-assigned identifiers back from the success envelope. Protected
/// endpoints are reached through <see cref="CustomWebApplicationFactory.CreateAuthenticatedClient"/>
/// (a Bearer-token client for the seeded administrator, who satisfies every VIEW/EDIT/DELETE/
/// MANAGE_SETTINGS policy); the unauthenticated <c>CreateClient()</c> is used only to assert the 401
/// challenge.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class RolesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route for the Roles resource.</summary>
    private const string RolesRoute = "/api/v1/roles";

    /// <summary>Base route for the Users resource (used to mint a user for the membership test).</summary>
    private const string UsersRoute = "/api/v1/users";

    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared in-process API fixture.</summary>
    /// <param name="factory">The shared <see cref="CustomWebApplicationFactory"/> supplied by xUnit.</param>
    public RolesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // Builders / helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="CreateRoleDto"/> that satisfies <c>CreateRoleValidator</c>: <c>RoleName</c>
    /// is the only hard requirement (NotEmpty, &lt;= 50 chars) and is made globally unique so the
    /// shared InMemory store never collides across tests; the fees/periods default to 0 (&gt;= 0) and
    /// the optional billing/trial frequency codes are left null so the "valid frequency" rule is
    /// skipped (it only runs when a frequency is supplied).
    /// </summary>
    private static CreateRoleDto BuildValidCreateRoleDto() => new()
    {
        PortalID = CustomWebApplicationFactory.DefaultPortalId,
        RoleName = $"Role_{Guid.NewGuid():N}",
        Description = "Integration test role",
        ServiceFee = 0f,
        TrialFee = 0f,
        BillingPeriod = 0,
        TrialPeriod = 0,
        IsPublic = false,
        AutoAssignment = false
    };

    /// <summary>
    /// Builds a <see cref="CreateUserDto"/> that satisfies <c>CreateUserValidator</c>
    /// (Username/Password/DisplayName/Email/FirstName/LastName all required, Email well-formed).
    /// Username and Email are globally unique so the stateful duplicate-username guard in
    /// <c>UserService</c> never rejects the create on the shared store.
    /// </summary>
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

    /// <summary>Reads and unwraps the <c>{ data }</c> success envelope for a single role, guarding nulls.</summary>
    /// <param name="response">The HTTP response whose body carries an <see cref="ApiResponse{T}"/> of <see cref="RoleDto"/>.</param>
    /// <returns>The non-null <see cref="RoleDto"/> payload.</returns>
    private static async Task<RoleDto> ReadRoleAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    /// <summary>Reads and unwraps the <c>{ data }</c> success envelope for a single user, guarding nulls.</summary>
    /// <param name="response">The HTTP response whose body carries an <see cref="ApiResponse{T}"/> of <see cref="UserDto"/>.</param>
    /// <returns>The non-null <see cref="UserDto"/> payload.</returns>
    private static async Task<UserDto> ReadUserAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    /// <summary>
    /// POSTs a fresh valid role through the supplied client and returns the server-assigned
    /// <see cref="RoleDto"/>. Asserts the create round-trips to 201 with a positive identifier so the
    /// mutating tests can rely on a known-good role without repeating the create assertions.
    /// </summary>
    /// <param name="client">An authenticated client.</param>
    /// <returns>The created <see cref="RoleDto"/> (with <c>RoleID</c> &gt; 0).</returns>
    private static async Task<RoleDto> CreateRoleAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(RolesRoute, BuildValidCreateRoleDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadRoleAsync(response);
        created.RoleID.Should().BeGreaterThan(0);
        return created;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>POST creates a role (201 + Location), then GET by the returned id returns it (200).</summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(RolesRoute, BuildValidCreateRoleDto());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();
        var created = await ReadRoleAsync(createResponse);
        created.RoleID.Should().BeGreaterThan(0);

        var getResponse = await client.GetAsync($"{RolesRoute}/{created.RoleID}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadRoleAsync(getResponse);
        fetched.RoleID.Should().Be(created.RoleID);
    }

    /// <summary>The list endpoint requires <c>portalId</c>: present -&gt; 200, absent -&gt; 400.</summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        var withPortal = await client.GetAsync($"{RolesRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        var withoutPortal = await client.GetAsync(RolesRoute);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>PUT with a matching <c>RoleID</c> updates (200); a route/body id mismatch is rejected (400).</summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateRoleAsync(client);

        var update = new UpdateRoleDto
        {
            RoleID = created.RoleID,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            RoleName = $"Role_{Guid.NewGuid():N}",
            Description = "Updated integration test role",
            ServiceFee = 0f,
            TrialFee = 0f,
            BillingPeriod = 0,
            TrialPeriod = 0,
            IsPublic = true,
            AutoAssignment = false
        };

        var updateResponse = await client.PutAsJsonAsync($"{RolesRoute}/{created.RoleID}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadRoleAsync(updateResponse);
        updated.RoleID.Should().Be(created.RoleID);

        // The controller's id-guard rejects a route id that does not match the body RoleID before any
        // service work runs, surfacing an RFC 7807 400.
        var mismatch = new UpdateRoleDto
        {
            RoleID = created.RoleID + 1,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            RoleName = $"Role_{Guid.NewGuid():N}"
        };
        var mismatchResponse = await client.PutAsJsonAsync($"{RolesRoute}/{created.RoleID}", mismatch);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>DELETE removes a role (204); because the delete is HARD, a subsequent GET returns 404.</summary>
    [Fact]
    public async Task Delete_Returns204_HardDelete()
    {
        var client = _factory.CreateAuthenticatedClient();
        var created = await CreateRoleAsync(client);

        var deleteResponse = await client.DeleteAsync($"{RolesRoute}/{created.RoleID}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // MIGRATION: Roles are HARD-deleted (legacy provider.DeleteRole; the Roles table has no IsDeleted
        // column), so the row is physically gone and the follow-up GET must surface 404 rather than a
        // soft-deleted record. This is the lineage assertion called out in source_files (RoleController.vb).
        var getResponse = await client.GetAsync($"{RolesRoute}/{created.RoleID}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Full membership round-trip: assign a user to a role, list it from both directions, then remove it.
    /// </summary>
    [Fact]
    public async Task Membership_AssignListAndRemove()
    {
        var client = _factory.CreateAuthenticatedClient();

        var userResponse = await client.PostAsJsonAsync(UsersRoute, BuildValidCreateUserDto());
        userResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await ReadUserAsync(userResponse);
        user.UserID.Should().BeGreaterThan(0);

        var role = await CreateRoleAsync(client);

        var membershipRoute = $"{RolesRoute}/{role.RoleID}/users/{user.UserID}";

        // MIGRATION: the assign-user-to-role endpoint is an ADMIN UPSERT that returns 201 Created with the
        // persisted assignment (RolesController.AddUserToRole -> CreatedAtAction), NOT 204. The route ids are
        // authoritative, so the optional effective/expiry-window body is omitted entirely; the endpoint sets
        // EmptyBodyBehavior.Allow, so a no-body POST binds the body to a default assignment.
        var assignResponse = await client.PostAsync(membershipRoute, null);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        assignResponse.Headers.Location.Should().NotBeNull();

        // GET {roleId}/users -> the users assigned to the role (UserDto list).
        var usersInRoleResponse = await client.GetAsync($"{RolesRoute}/{role.RoleID}/users");
        usersInRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var usersInRole = await usersInRoleResponse.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        usersInRole.Should().NotBeNull();
        usersInRole!.Data.Should().NotBeNull();
        usersInRole.Data!.Should().Contain(u => u.UserID == user.UserID);

        // GET user/{userId} -> the roles the user belongs to (RoleDto list).
        var userRolesResponse = await client.GetAsync($"{RolesRoute}/user/{user.UserID}");
        userRolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var userRoles = await userRolesResponse.Content.ReadFromJsonAsync<ApiResponse<List<RoleDto>>>();
        userRoles.Should().NotBeNull();
        userRoles!.Data.Should().NotBeNull();
        userRoles.Data!.Should().Contain(r => r.RoleID == role.RoleID);

        var removeResponse = await client.DeleteAsync(membershipRoute);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>GET by an id that does not exist returns 404 (controller NotFound, not an exception).</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{RolesRoute}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>POST with an invalid body (empty RoleName) returns 400 with RFC 7807 validation errors.</summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Empty RoleName violates CreateRoleValidator.RoleName.NotEmpty -> FluentValidation throws ->
        // ExceptionHandlingMiddleware emits a 400 ValidationProblemDetails (application/problem+json).
        var invalid = BuildValidCreateRoleDto();
        invalid.RoleName = string.Empty;

        var response = await client.PostAsJsonAsync(RolesRoute, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>A request with no Bearer token is rejected by the [Authorize] pipeline with 401.</summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // Plain (unauthenticated) client: authentication must fail before any handler runs.
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{RolesRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
