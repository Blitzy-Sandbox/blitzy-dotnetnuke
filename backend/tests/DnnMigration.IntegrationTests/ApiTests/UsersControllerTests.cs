// MIGRATION: [AAP Gate 5 — API Integration Tests] User CRUD + behavioral-parity integration suite for the
// migrated UsersController. This is the third of the three mandatory Gate-5 controllers (Portal, Module, User).
// It proves the CRUD status-code contract (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204), the portal-scoped
// list (the BindRequired ?portalId query parameter -> 400 when omitted), and the two behavioral rules ported
// VERBATIM from the legacy DotNetNuke user workflow (Library/Components/Users/UserController.vb +
// Website/admin/Users/ManageUsers.ascx.vb / Users.ascx.vb) into Application/Services/UserService.cs:
//   1. Auto-assigned roles  — on create, a non-super-user is granted every portal role flagged AutoAssignment
//      (legacy UserController.CreateUser -> RoleController.AddUserRole), surfaced through UserResponse.Roles.
//   2. Admin-delete guard    — the portal administrator (Portal.AdministratorId) cannot be deleted
//      (legacy UserController.DeleteUser CanDelete rule), surfaced as a 400 ProblemDetails.
//
// The suite runs against the real Api Program pipeline hosted by CustomWebApplicationFactory (EF Core InMemory +
// the always-authenticated super-user TestAuthHandler). Because the seeded Test principal is a host super-user, it
// bypasses ApiControllerBase.EnforceTenant for any portalId, so the portal-scoping query parameters exercise the
// status-code contract without a real JWT or SQL Server.
//
// MIGRATION (test-design note): the success body uses the project envelope { "data": {...}, "meta": {...} }
// (ApiControllerBase), so reading a created/updated user means projecting the "data" element into UserResponse.
// This file defines its own web JSON options and envelope readers. MIGRATION: [CP4 review — Test Isolation] the
// per-test reset/seed flow is now CENTRALIZED on CustomWebApplicationFactory (ResetAndSeed / ResetDatabase) and is
// shared by every CRUD suite; the thin instance forwarders below delegate to it (this file no longer owns a
// duplicate reset implementation). The file remains free of cross-test-file helper coupling and cannot collide
// with the other parallel CRUD test files in this namespace.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for <c>UsersController</c> covering the AAP Gate-5 CRUD status-code contract plus the two
/// <c>UserService</c> behavioral-parity rules (auto-assigned roles on create and the portal-administrator delete
/// guard). The class-level <c>[Trait("Category", "Integration")]</c> propagates to every test method so the whole
/// suite is selected by the Gate-5 <c>--filter "Category=Integration"</c> run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    // The TestAuthHandler issues a "portalId" claim of 0 and an "isSuperUser=true" claim. The super-user flag makes
    // ApiControllerBase.EnforceTenant pass for ANY requested portalId, so portal 0 (which is never seeded as a
    // Portal entity) is the simplest tenant for the pure CRUD tests; the parity tests seed their own portal and use
    // its database-generated id.
    private const int SuperUserPortalId = 0;

    // Web (camelCase) serializer options matching the API's JSON contract. Self-contained so this file does not
    // depend on any sibling helper type. PropertyNameCaseInsensitive (implied by Web defaults) makes envelope
    // deserialization tolerant of the API's camelCase property names.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes the fixture-shared host client. Calling <c>CreateClient()</c> builds the SUT host, after which
    /// the factory's <c>Services</c> provider is available for the database reset/seed helpers.
    /// </summary>
    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // --- request factories -------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a fully valid <see cref="CreateUserRequest"/> that satisfies every CreateUserValidator rule
    /// (PortalId >= 0; non-empty Username/DisplayName/FirstName/LastName; valid Email format; matching
    /// Password/Confirm) so the create endpoint returns 201. The username is parameterized so each test seeds a
    /// unique login identity (usernames are unique per portal).
    /// </summary>
    private static CreateUserRequest ValidUser(int portalId, string username) => new()
    {
        PortalId = portalId,
        Username = username,
        DisplayName = "Test User",
        Email = $"{username}@example.com",
        FirstName = "Test",
        LastName = "User",
        Password = "Password1",
        Confirm = "Password1"
    };

    /// <summary>
    /// Builds a fully valid <see cref="UpdateUserRequest"/> that satisfies every UpdateUserValidator rule
    /// (valid Email format; non-empty DisplayName/FirstName/LastName) so the update endpoint returns 200.
    /// </summary>
    private static UpdateUserRequest ValidUpdate(string displayName) => new()
    {
        Email = "testuser@example.com",
        DisplayName = displayName,
        FirstName = "Test",
        LastName = "User",
        IsApproved = true,
        LockedOut = false
    };

    // --- database isolation helpers ----------------------------------------------------------------------------

    // MIGRATION: [CP4 review — Test Isolation] The reset/seed flow is CENTRALIZED on CustomWebApplicationFactory so
    // every controller suite shares one implementation (EnsureDeleted → EnsureCreated → seed over a DI scope). These
    // thin instance forwarders keep the call sites below readable while delegating to that single source of truth.

    /// <summary>
    /// Clears the shared InMemory store so a test starts from a known-empty database. Delegates to
    /// <see cref="CustomWebApplicationFactory.ResetDatabase"/>. The fixture is shared per class
    /// (<see cref="IClassFixture{TFixture}"/>) and the assembly disables test parallelization, so resetting at the
    /// start of each test keeps the cases order-independent.
    /// </summary>
    private void ResetDatabase() => _factory.ResetDatabase();

    /// <summary>
    /// Resets the shared InMemory store and then runs the supplied seed action against a fresh
    /// <see cref="DnnDbContext"/>. Delegates to <see cref="CustomWebApplicationFactory.ResetAndSeed"/>.
    /// </summary>
    /// <param name="seed">An action that populates the context (the caller saves via the context as needed).</param>
    private void ResetAndSeed(Action<DnnDbContext> seed) => _factory.ResetAndSeed(seed);

    // --- envelope readers --------------------------------------------------------------------------------------

    /// <summary>
    /// Projects the success envelope's <c>data</c> element into a single <see cref="UserResponse"/>.
    /// </summary>
    private static async Task<UserResponse> ReadUserAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        return data.Deserialize<UserResponse>(JsonOptions)
               ?? throw new InvalidOperationException("The success envelope contained a null 'data' payload.");
    }

    /// <summary>
    /// MIGRATION: [CP4 review — Envelope Contract] Reads the FULL paged success envelope
    /// <c>{ "data": [...], "meta": {...} }</c> into a typed shape so the list test can assert BOTH data membership
    /// and every pagination metadata field (totalCount, pageIndex, pageSize, totalPages, hasPreviousPage,
    /// hasNextPage) required by the AAP §0.7.5 envelope contract — not just the data array.
    /// </summary>
    private static async Task<PagedEnvelope<UserResponse>> ReadUserPagedEnvelopeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PagedEnvelope<UserResponse>>(json, JsonOptions)
               ?? throw new InvalidOperationException("The paged success envelope could not be deserialized.");
    }

    // --- CRUD status-code contract (Gate 5) --------------------------------------------------------------------

    /// <summary>POST /api/users with a fully valid body returns 201 Created with a Location header.</summary>
    [Fact]
    public async Task Post_User_Returns201Created()
    {
        ResetDatabase();

        var response = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(SuperUserPortalId, "post_created_user"), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var user = await ReadUserAsync(response);
        user.UserId.Should().BeGreaterThan(0);
        user.Username.Should().Be("post_created_user");
        user.PortalId.Should().Be(SuperUserPortalId);
    }

    /// <summary>GET /api/users/{id}?portalId= returns 200 for an existing user.</summary>
    [Fact]
    public async Task Get_User_ById_Returns200()
    {
        ResetDatabase();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(SuperUserPortalId, "get_by_id_user"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadUserAsync(createResponse);

        // MIGRATION: GetById is portal-scoped — portalId is a BindRequired query parameter on UsersController.
        var getResponse = await _client.GetAsync($"/api/users/{created.UserId}?portalId={SuperUserPortalId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadUserAsync(getResponse);
        fetched.UserId.Should().Be(created.UserId);
    }

    /// <summary>PUT /api/users/{id}?portalId= with a valid body returns 200 and applies the update.</summary>
    [Fact]
    public async Task Put_User_Returns200()
    {
        ResetDatabase();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(SuperUserPortalId, "put_user"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadUserAsync(createResponse);

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/users/{created.UserId}?portalId={SuperUserPortalId}", ValidUpdate("Updated User"), JsonOptions);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadUserAsync(putResponse);
        updated.DisplayName.Should().Be("Updated User");
    }

    /// <summary>DELETE /api/users/{id}?portalId= returns 204 for a non-administrator user.</summary>
    [Fact]
    public async Task Delete_User_Returns204()
    {
        ResetDatabase();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(SuperUserPortalId, "delete_user"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadUserAsync(createResponse);

        // Portal 0 is never seeded as a Portal entity, so the admin-delete guard does not apply (the created user
        // is not a portal administrator) and the delete succeeds.
        var deleteResponse = await _client.DeleteAsync($"/api/users/{created.UserId}?portalId={SuperUserPortalId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- behavioral-parity rules (extracted verbatim from UserService) -----------------------------------------

    /// <summary>
    /// MIGRATION parity: on create, a non-super-user is auto-assigned every portal role flagged AutoAssignment, so
    /// the created <see cref="UserResponse.Roles"/> contains the seeded "Registered Users" role.
    /// </summary>
    [Fact]
    public async Task Post_User_AutoAssignsRegisteredUsersRole()
    {
        var portalId = 0;
        ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Seed Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            db.Roles.Add(new Role
            {
                PortalId = portal.PortalId,
                RoleName = "Registered Users",
                AutoAssignment = true
            });
            db.SaveChanges();

            portalId = portal.PortalId;
        });

        var response = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(portalId, "auto_role_user"), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await ReadUserAsync(response);
        user.Roles.Should().Contain("Registered Users");
    }

    /// <summary>
    /// MIGRATION parity: deleting the portal administrator (the user whose id equals Portal.AdministratorId) is
    /// refused with 400 (the legacy UserController.DeleteUser CanDelete guard).
    /// </summary>
    [Fact]
    public async Task Delete_PortalAdministrator_Returns400()
    {
        var portalId = 0;
        var adminUserId = 0;
        ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Seed Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            var user = new User { Username = "admin", PortalId = portal.PortalId };
            db.Users.Add(user);
            db.SaveChanges();

            portal.AdministratorId = user.UserId;
            db.SaveChanges();

            portalId = portal.PortalId;
            adminUserId = user.UserId;
        });

        var response = await _client.DeleteAsync($"/api/users/{adminUserId}?portalId={portalId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- portal-scoping / not-found edges ----------------------------------------------------------------------

    /// <summary>GET /api/users without the BindRequired ?portalId returns 400 Bad Request.</summary>
    [Fact]
    public async Task Get_Users_List_MissingPortalId_Returns400()
    {
        var response = await _client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// GET /api/users/{id}?portalId= for a non-existent id returns 404 Not Found AND the exact RFC 7807
    /// problem-detail message the migrated <c>UserService</c> emits (CP4 — Test Coverage). The migrated message is
    /// intentionally opaque (it does NOT echo the requested id), so the assertion targets that exact text verbatim
    /// rather than an id-bearing variant.
    /// </summary>
    [Fact]
    public async Task Get_User_ById_NotFound_Returns404()
    {
        ResetDatabase();

        var response = await _client.GetAsync($"/api/users/999999?portalId={SuperUserPortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await EnvelopeReader.ReadProblemDetailAsync(response);
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("The requested user was not found.");
    }

    /// <summary>
    /// GET /api/users?portalId= returns 200 with the full paged envelope <c>{ data, meta }</c>. After resetting to a
    /// known-empty state and creating EXACTLY ONE user, the test asserts BOTH that the data array contains that user
    /// AND every pagination metadata field from the known seed (CP4 — Envelope Contract): totalCount=1 at the
    /// default pageIndex=0 / pageSize=20, a single total page, and no previous/next page.
    /// </summary>
    [Fact]
    public async Task Get_Users_List_Returns200Paged()
    {
        var portalId = 0;
        ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Seed Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();
            portalId = portal.PortalId;
        });

        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", ValidUser(portalId, "list_user"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.GetAsync($"/api/users?portalId={portalId}");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await ReadUserPagedEnvelopeAsync(listResponse);
        envelope.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle().Which.Username.Should().Be("list_user");

        envelope.Meta.Should().NotBeNull();
        envelope.Meta!.TotalCount.Should().Be(1);
        envelope.Meta.PageIndex.Should().Be(0);
        envelope.Meta.PageSize.Should().Be(20);
        envelope.Meta.TotalPages.Should().Be(1);
        envelope.Meta.HasPreviousPage.Should().BeFalse();
        envelope.Meta.HasNextPage.Should().BeFalse();
    }

    // --- typed paged-envelope shape (CP4 — Envelope Contract) --------------------------------------------------

    /// <summary>
    /// MIGRATION: [CP4 review — Envelope Contract] Typed projection of the paged success envelope
    /// <c>{ "data": [...], "meta": {...} }</c>. Mirrors the API's <c>ApiControllerBase</c> paged shape so the list
    /// test asserts data membership and all pagination metadata in a single deserialization.
    /// </summary>
    private sealed record PagedEnvelope<T>(List<T>? Data, PageMeta? Meta);

    /// <summary>
    /// Typed projection of the paged envelope's <c>meta</c> object. Property names map case-insensitively (Web
    /// defaults) to the API's camelCase keys:
    /// <c>totalCount / pageIndex / pageSize / totalPages / hasPreviousPage / hasNextPage</c>.
    /// </summary>
    private sealed record PageMeta(
        int TotalCount,
        int PageIndex,
        int PageSize,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);
}
