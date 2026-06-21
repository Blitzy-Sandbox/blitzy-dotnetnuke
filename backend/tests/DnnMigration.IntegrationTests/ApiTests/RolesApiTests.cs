using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end HTTP integration tests for the Roles REST API (<c>/api/v1/roles</c>) and its user-role
/// membership sub-resources, exercised in-process against the REAL <c>DnnMigration.Api</c> pipeline
/// (middleware + dependency injection) via <see cref="CustomWebApplicationFactory"/>, with the SQL Server
/// data layer replaced by the EF Core InMemory provider so no real database is contacted (Validation Gate 5).
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: there is no legacy equivalent — DotNetNuke 4.9.0.85 shipped zero automated tests. The Role
/// surface under test ports <c>Library/Components/Security/Roles/RoleController.vb</c>; Role delete is a
/// HARD delete (legacy <c>DeleteRole</c> -&gt; <c>provider.DeleteRole</c>, no <c>IsDeleted</c> column), so the
/// delete test asserts a subsequent <c>GET /{id}</c> returns <c>404 Not Found</c>.
/// </para>
/// <para>
/// Every test that targets a <c>[Authorize]</c>-protected endpoint uses
/// <see cref="CustomWebApplicationFactory.CreateAuthenticatedClient"/> (a Bearer token minted for the seeded
/// super-user administrator, which satisfies the View/Edit/Delete authorization policies). The 401 test uses a
/// plain unauthenticated client. The shared InMemory store is reused across the class fixture, so each mutating
/// test creates its OWN role (and, for membership, its own user) and reads the generated identifiers back from
/// the <c>{ data, meta }</c> success envelope rather than depending on a fixed seeded row.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class RolesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base path of the versioned Roles resource controller.</summary>
    private const string RolesBaseUrl = "/api/v1/roles";

    /// <summary>Base path of the versioned Users resource controller (used to mint a membership subject).</summary>
    private const string UsersBaseUrl = "/api/v1/users";

    /// <summary>The shared in-process API host fixture (EF Core InMemory backed).</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared <see cref="CustomWebApplicationFactory"/> fixture.</summary>
    /// <param name="factory">The class-scoped in-process API host fixture supplied by xUnit.</param>
    public RolesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="CreateRoleDto"/> that satisfies <c>CreateRoleValidator</c>: a unique non-empty
    /// <c>RoleName</c>, the seeded default portal, and zero (>= 0) fees. The nullable billing/trial periods and
    /// frequencies are deliberately left unset — the FluentValidation comparison rules skip <c>null</c> values,
    /// so an unset period/frequency is valid (and the &gt; 0 period rule is never tripped).
    /// </summary>
    /// <returns>A valid role-creation request with a collision-free name.</returns>
    private static CreateRoleDto BuildValidCreateRoleDto() => new()
    {
        PortalID = CustomWebApplicationFactory.DefaultPortalId,
        RoleName = $"Role_{Guid.NewGuid():N}",
        Description = "Integration test role",
        ServiceFee = 0f,
        TrialFee = 0f,
        IsPublic = false,
        AutoAssignment = false
    };

    /// <summary>
    /// Builds a <see cref="CreateUserDto"/> that satisfies <c>CreateUserValidator</c>: a unique username and
    /// email, a non-empty password, and the required display/first/last names within their length limits. Used
    /// by the membership test to mint a real user as the assignment subject.
    /// </summary>
    /// <returns>A valid user-creation request with collision-free identity fields.</returns>
    private static CreateUserDto BuildValidCreateUserDto()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new CreateUserDto
        {
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            Username = $"user_{unique}",
            Password = "Passw0rd!$",
            DisplayName = "Integration Test User",
            Email = $"user_{unique}@dnnmigration.local",
            FirstName = "Integration",
            LastName = "Tester",
            IsSuperUser = false,
            Approved = true
        };
    }

    /// <summary>
    /// Reads and unwraps the <c>data</c> payload from a success-envelope response, asserting that both the
    /// envelope and its payload are present. Centralizing the null-guard here keeps the call sites free of
    /// repeated null-forgiving dereferences (which would otherwise be needed under the project's
    /// nullable + warnings-as-errors policy).
    /// </summary>
    /// <typeparam name="T">The envelope payload type (a reference type such as a DTO or a list of DTOs).</typeparam>
    /// <param name="response">The HTTP response whose JSON body is a <see cref="ApiResponse{T}"/>.</param>
    /// <returns>The non-null <c>data</c> payload.</returns>
    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
        envelope.Should().NotBeNull();

        var data = envelope!.Data;
        data.Should().NotBeNull();
        return data!;
    }

    /// <summary>
    /// Creates a valid role through the API and returns its server-assigned identifier, asserting the
    /// <c>201 Created</c> result and a positive <c>RoleID</c> along the way.
    /// </summary>
    /// <param name="client">An authenticated client able to call the Edit-protected create endpoint.</param>
    /// <returns>The identifier of the newly created role.</returns>
    private static async Task<int> CreateRoleAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(RolesBaseUrl, BuildValidCreateRoleDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var role = await ReadDataAsync<RoleDto>(response);
        role.RoleID.Should().BeGreaterThan(0);
        return role.RoleID;
    }

    /// <summary>
    /// Creates a valid user through the API and returns its server-assigned identifier, asserting the
    /// <c>201 Created</c> result and a positive <c>UserID</c> along the way.
    /// </summary>
    /// <param name="client">An authenticated client able to call the Edit-protected create endpoint.</param>
    /// <returns>The identifier of the newly created user.</returns>
    private static async Task<int> CreateUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(UsersBaseUrl, BuildValidCreateUserDto());
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await ReadDataAsync<UserDto>(response);
        user.UserID.Should().BeGreaterThan(0);
        return user.UserID;
    }

    // -------------------------------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------------------------------

    /// <summary>POST a valid role returns 201 + Location; the subsequent GET by id returns 200 with that id.</summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var createResponse = await client.PostAsJsonAsync(RolesBaseUrl, BuildValidCreateRoleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await ReadDataAsync<RoleDto>(createResponse);
        created.RoleID.Should().BeGreaterThan(0);
        var id = created.RoleID;

        var getResponse = await client.GetAsync($"{RolesBaseUrl}/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = await ReadDataAsync<RoleDto>(getResponse);
        fetched.RoleID.Should().Be(id);
    }

    /// <summary>The list endpoint returns 200 when <c>portalId</c> is supplied and 400 when it is omitted.</summary>
    [Fact]
    public async Task GetList_RequiresPortalId()
    {
        var client = _factory.CreateAuthenticatedClient();

        var withPortal = await client.GetAsync($"{RolesBaseUrl}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        withPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        // The seeded "Administrators" role lives in the default portal, so the success-envelope payload is
        // present and includes it — confirming the 200 carries a well-formed { data, meta } body.
        var roles = await ReadDataAsync<List<RoleDto>>(withPortal);
        roles.Should().Contain(r => r.RoleID == CustomWebApplicationFactory.SeededRoleId);

        var withoutPortal = await client.GetAsync(RolesBaseUrl);
        withoutPortal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>PUT with a matching id updates the role (200); a route/body id mismatch is rejected (400).</summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var id = await CreateRoleAsync(client);

        var update = new UpdateRoleDto
        {
            RoleID = id,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            RoleName = $"Updated_{Guid.NewGuid():N}",
            Description = "Updated integration test role",
            ServiceFee = 0f,
            TrialFee = 0f,
            IsPublic = true,
            AutoAssignment = false
        };

        var updateResponse = await client.PutAsJsonAsync($"{RolesBaseUrl}/{id}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await ReadDataAsync<RoleDto>(updateResponse);
        updated.RoleID.Should().Be(id);
        updated.RoleName.Should().Be(update.RoleName);

        // Identifier mismatch: the route id and the body RoleID disagree -> 400 (the controller id-guard fires
        // before the service is invoked, so the otherwise-valid body never reaches validation).
        var mismatch = new UpdateRoleDto
        {
            RoleID = id + 1,
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            RoleName = $"Mismatch_{Guid.NewGuid():N}",
            ServiceFee = 0f,
            TrialFee = 0f
        };

        var mismatchResponse = await client.PutAsJsonAsync($"{RolesBaseUrl}/{id}", mismatch);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>DELETE returns 204 and, because Role delete is a HARD delete, the role is then unreachable (404).</summary>
    [Fact]
    public async Task Delete_Returns204_HardDelete()
    {
        var client = _factory.CreateAuthenticatedClient();
        var id = await CreateRoleAsync(client);

        var deleteResponse = await client.DeleteAsync($"{RolesBaseUrl}/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // HARD delete (no soft-delete flag on Roles): the row is physically removed, so a re-read 404s.
        var getResponse = await client.GetAsync($"{RolesBaseUrl}/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Assigns a user to a role (204), confirms the user appears in the role's membership (200) and the role
    /// appears in the user's roles (200), then removes the membership (204).
    /// </summary>
    [Fact]
    public async Task Membership_AssignListAndRemove()
    {
        var client = _factory.CreateAuthenticatedClient();

        var userId = await CreateUserAsync(client);
        var roleId = await CreateRoleAsync(client);

        // Assign the user to the role. The HTTP route order is {roleId}/users/{userId}; the assignment carries
        // no request body.
        var assignResponse = await client.PostAsync($"{RolesBaseUrl}/{roleId}/users/{userId}", content: null);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The role's membership now includes the user.
        var usersResponse = await client.GetAsync($"{RolesBaseUrl}/{roleId}/users");
        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await ReadDataAsync<List<UserDto>>(usersResponse);
        users.Should().Contain(u => u.UserID == userId);

        // The user's roles now include the role.
        var rolesResponse = await client.GetAsync($"{RolesBaseUrl}/user/{userId}");
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var roles = await ReadDataAsync<List<RoleDto>>(rolesResponse);
        roles.Should().Contain(r => r.RoleID == roleId);

        // Remove the membership.
        var removeResponse = await client.DeleteAsync($"{RolesBaseUrl}/{roleId}/users/{userId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// MIGRATION (DEV-069 / Finding 5): the assignment endpoint accepts an OPTIONAL request body carrying the
    /// legacy SecurityRoles EffectiveDate/ExpiryDate/notify inputs. A populated body binds (the controller
    /// declares <c>EmptyBodyBehavior.Allow</c>, so both the bodyless form above and this populated form are
    /// valid), returns 204, and the membership is then visible in the role's user list.
    /// </summary>
    [Fact]
    public async Task Membership_Assign_WithEffectiveExpiryNotifyBody_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        var userId = await CreateUserAsync(client);
        var roleId = await CreateRoleAsync(client);

        var body = new AddUserRoleRequestDto
        {
            EffectiveDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2031, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            Notify = true,
        };

        var assignResponse = await client.PostAsJsonAsync($"{RolesBaseUrl}/{roleId}/users/{userId}", body);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The role's membership now includes the user (the operator-dated assignment was persisted).
        var usersResponse = await client.GetAsync($"{RolesBaseUrl}/{roleId}/users");
        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await ReadDataAsync<List<UserDto>>(usersResponse);
        users.Should().Contain(u => u.UserID == userId);
    }

    /// <summary>GET by an id that does not exist returns 404 (RFC 7807 not-found).</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{RolesBaseUrl}/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>POST with an invalid body (empty <c>RoleName</c>) returns 400 with a populated RFC 7807 errors map.</summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Empty RoleName violates the CreateRoleValidator "NotEmpty" rule; the service's ValidateAndThrowAsync
        // raises a FluentValidation ValidationException that the exception middleware renders as a
        // ValidationProblemDetails (RFC 7807) with a non-empty "errors" dictionary.
        var invalid = new CreateRoleDto
        {
            PortalID = CustomWebApplicationFactory.DefaultPortalId,
            RoleName = string.Empty,
            ServiceFee = 0f,
            TrialFee = 0f
        };

        var response = await client.PostAsJsonAsync(RolesBaseUrl, invalid);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>An unauthenticated request (no Bearer token) to a protected endpoint returns 401.</summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{RolesBaseUrl}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
