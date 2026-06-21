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
/// MIGRATION (DEV-066): User uses a NON-destructive (soft) delete. The [Users] table has no <c>IsDeleted</c>
/// column and ADR-002 forbids schema changes, so <c>UserService.DeleteAsync</c> realizes the soft delete
/// schema-faithfully by removing the user's <c>[UserPortals]</c> association row(s) rather than the durable
/// <c>[Users]</c> / aspnet_* rows. The user is thereby EXCLUDED from the portal list (the same observable
/// outcome the legacy delete produced) while a by-id lookup still resolves the preserved identity row — see
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
    /// DELETE returns 204 and the user is afterwards EXCLUDED from the portal list, while its preserved
    /// identity row is still resolvable by id (non-destructive soft delete).
    /// </summary>
    /// <remarks>
    /// MIGRATION (DEV-066): the deletion is NON-destructive — the [Users] table has no <c>IsDeleted</c> column
    /// (ADR-002 forbids a schema change), so the soft delete removes the user's <c>[UserPortals]</c> association
    /// row rather than the <c>[Users]</c> row. The AAP-prescribed exclusion-from-list invariant therefore holds
    /// (the portal-scoped list JOINs <c>[UserPortals]</c>, which is now gone), while a portal-agnostic by-id
    /// lookup STILL returns 200 because the identity row is preserved — the defining difference from the former
    /// hard delete. A FRESH, non-administrator user is created and deleted here so the administrator guard in
    /// <c>UserService.DeleteAsync</c> (which would 409 on the seeded admin) is never tripped. The list is
    /// requested with a large page size so the exclusion assertion is meaningful.
    /// </remarks>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();
        var deletedId = await CreateUserAsync(client);

        var deleteResponse = await client.DeleteAsync($"{UsersRoute}/{deletedId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // MIGRATION (DEV-066): the soft delete removes only the [UserPortals] association, so the durable
        // [Users] identity row is preserved and a subsequent portal-agnostic by-id lookup STILL returns 200.
        // (Under the former hard delete this returned 404; flipping this assertion is the test-level proof that
        // the delete is now non-destructive.)
        var getDeleted = await client.GetAsync($"{UsersRoute}/{deletedId}");
        getDeleted.StatusCode.Should().Be(HttpStatusCode.OK);

        // ...and the user is EXCLUDED from the portal list — the AAP-prescribed exclusion assertion. This holds
        // because the portal-scoped list JOINs [UserPortals], and the soft delete removed that association row.
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

    /// <summary>
    /// The portal-scoped paged list (<c>?portalId=&amp;pageIndex=&amp;pageSize=</c>) populates the envelope's
    /// scalar <c>meta</c> pagination fields. The exact <c>totalCount</c> is non-deterministic (other tests add
    /// users to portal 0 in the shared store), so this asserts the page coordinates echo the request and that
    /// <c>totalPages</c> equals the contract formula <c>ceil(totalCount / pageSize)</c> (QA Finding F2 — locks
    /// the pagination math end-to-end at the integration layer, not just HTTP 200).
    /// </summary>
    [Fact]
    public async Task GetList_Paged_PopulatesMeta()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: ensure at least one user exists on portal 0 so the page is non-empty.
        await CreateUserAsync(client);

        // Act: portal-scoped paged list branch with explicit page coordinates.
        var response = await client.GetAsync($"{UsersRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}&pageIndex=0&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();

        // Assert: the envelope meta carries the pagination scalars.
        var meta = result!.Meta;
        meta.Should().NotBeNull();
        meta!.PageIndex.Should().Be(0);                  // echoes the requested page index
        meta!.PageSize.Should().Be(20);                  // echoes the requested page size
        meta!.TotalCount.Should().NotBeNull();
        meta!.TotalPages.Should().NotBeNull();

        // Lock the pagination math without depending on the exact (order-dependent) count:
        // totalPages == ceil(totalCount / pageSize), matching PagedResult<T>.TotalPages.
        var totalCount = meta!.TotalCount!.Value;
        var pageSize = meta!.PageSize!.Value;
        var expectedTotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        meta!.TotalPages.Should().Be(expectedTotalPages);
    }

    /// <summary>
    /// Deleting the seeded portal administrator (<c>UserID = 1</c>, the <c>AdministratorId</c> of every seeded
    /// portal) trips the administrator-delete business-rule guard and returns 409 Conflict with an RFC 7807
    /// ProblemDetails carrying the guard message. The guard throws BEFORE any removal, so this is a
    /// non-destructive assertion on the shared fixture — the admin user is never deleted (QA Finding F4 —
    /// admin-delete 409 guard, runtime-verified at the HTTP layer in addition to Gate 2 unit coverage).
    /// </summary>
    [Fact]
    public async Task Delete_Administrator_Returns409()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Act: attempt to delete the seeded portal administrator. UserService.DeleteAsync loads the portal for
        // the user and throws when portal.AdministratorId == user.UserID, BEFORE the repository removal — so
        // the admin row is never touched and the shared fixture is unharmed.
        var response = await client.DeleteAsync($"{UsersRoute}/{CustomWebApplicationFactory.AdminUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // RFC 7807: InvalidOperationException is mapped by ExceptionHandlingMiddleware to a 409 ProblemDetails
        // whose detail is the exact guard message.
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem!.Detail.Should().Be("Cannot delete the portal administrator.");
    }

    /// <summary>
    /// MIGRATION (DEV-067 — Membership workflow parity): the full authorize / unauthorize / unlock round-trip
    /// against the real API + EF pipeline. A freshly created user is provisioned (DEV-065) with an approved,
    /// never-locked-out <c>[aspnet_Membership]</c> row, so <c>GET {id}/membership</c> reflects that baseline;
    /// the transitions then flip and restore the state, each returning the refreshed <c>MembershipDto</c>.
    /// </summary>
    [Fact]
    public async Task Membership_AuthorizeUnauthorizeUnlock_RoundTrip()
    {
        var client = _factory.CreateAuthenticatedClient();
        var id = await CreateUserAsync(client); // BuildValidCreateUserDto sets Approved = true

        // GET: provisioning (DEV-065) created an approved, never-locked-out membership row.
        var getResp = await client.GetAsync($"{UsersRoute}/{id}/membership");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var got = await getResp.Content.ReadFromJsonAsync<ApiResponse<MembershipDto>>();
        got.Should().NotBeNull();
        got!.Data.Should().NotBeNull();
        got.Data!.UserID.Should().Be(id);
        got.Data.Approved.Should().BeTrue();
        got.Data.LockedOut.Should().BeFalse();
        got.Data.LastLockoutDate.Should().BeNull(); // the "never locked out" sentinel maps to null

        // Unauthorize -> Approved flips to false, echoed in the refreshed projection.
        var unauth = await client.PostAsync($"{UsersRoute}/{id}/unauthorize", null);
        unauth.StatusCode.Should().Be(HttpStatusCode.OK);
        var unauthDto = await unauth.Content.ReadFromJsonAsync<ApiResponse<MembershipDto>>();
        unauthDto!.Data!.Approved.Should().BeFalse();

        // Authorize -> Approved restored to true.
        var auth = await client.PostAsync($"{UsersRoute}/{id}/authorize", null);
        auth.StatusCode.Should().Be(HttpStatusCode.OK);
        var authDto = await auth.Content.ReadFromJsonAsync<ApiResponse<MembershipDto>>();
        authDto!.Data!.Approved.Should().BeTrue();

        // Unlock -> idempotent for a not-locked user, returns 200 with a cleared lockout state.
        var unlock = await client.PostAsync($"{UsersRoute}/{id}/unlock", null);
        unlock.StatusCode.Should().Be(HttpStatusCode.OK);
        var unlockDto = await unlock.Content.ReadFromJsonAsync<ApiResponse<MembershipDto>>();
        unlockDto!.Data!.LockedOut.Should().BeFalse();
        unlockDto.Data.FailedPasswordAttemptCount.Should().Be(0);
    }

    /// <summary>GET membership for an id that does not exist returns 404 (RFC 7807 Problem Details).</summary>
    [Fact]
    public async Task GetMembership_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{UsersRoute}/999999/membership");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
