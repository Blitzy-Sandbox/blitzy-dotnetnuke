// MIGRATION: Net-new end-to-end integration tests for the User REST resource. The legacy DotNetNuke 4.x
// solution shipped no automated tests — the user add/edit/delete workflows in
// Website/admin/Users/User.ascx.vb were verified manually against the ASP.NET Web Forms UI, and the
// data/business surface lived in Library/Components/Users/UserController.vb as Public Shared members
// (CreateUser L156, GetUser L497, UpdateUser L963, DeleteUser L200). Those postback/ViewState workflows
// are eliminated, not ported: this class drives the migrated stateless JSON API over real HTTP
// (POST/GET/PUT/DELETE against /api/users) through an in-process host, asserting the exact status codes
// and response envelope the Angular SPA consumes. It satisfies the UserApiTests requirement of
// Validation Gate 5 (full CRUD: POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204) plus a not-found
// (404) check, and contributes to Validation Gate 2 (the Release-configuration `dotnet test` run).
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end CRUD integration tests for the <c>/api/users</c> REST resource, executed against the full
/// <c>DnnMigration.Api</c> host booted in-memory by <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tests share a single factory instance (and therefore a single isolated EF Core InMemory database)
/// through <c>IClassFixture&lt;CustomWebApplicationFactory&gt;</c>. The factory registers a permissive
/// test authentication scheme as the default, so the plain <see cref="System.Net.Http.HttpClient"/> from
/// <c>CreateClient()</c> satisfies the class-level <c>[Authorize]</c> on <c>UsersController</c> without a
/// real JWT login/BCrypt round-trip.
/// </para>
/// <para>
/// MIGRATION: <c>UsersController</c> hashes the create payload's password with the real (DI-registered,
/// singleton) BCrypt <c>IPasswordHasher</c>, so the create request supplies a compliant password/confirm
/// pair; the plaintext never persists and never round-trips. The <c>UserID</c> primary key is
/// <c>ValueGeneratedOnAdd()</c>, so the InMemory provider assigns it on <c>POST</c> — the tests read the
/// server-assigned id back from the response envelope (<c>data.userID</c>) and reuse it to drive the rest
/// of the lifecycle rather than assuming any value.
/// </para>
/// </remarks>
public class UserApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The in-process HTTP client that speaks to the test host. Authenticated by the factory's default
    /// test scheme, so it transparently clears the resource's <c>[Authorize]</c> gate.
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// The shared factory, retained so tests can open a DI scope on the host's service provider and seed
    /// the InMemory database directly (used by the administrator-protection test to make a user the
    /// administrator of a portal — a state no REST route establishes for an arbitrary user).
    /// </summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// JSON options shared by every request/response body in this class.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> lets the local read-models bind to
    /// the API's camelCase payload (for example <c>userID</c> -&gt; <see cref="UserRead.UserID"/>), and the
    /// <see cref="JsonStringEnumConverter"/> mirrors the host's own converter (registered in
    /// <c>Program.cs</c>) so any string-valued enum in a response deserializes rather than throwing.
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="UserApiTests"/> class, capturing an authenticated HTTP
    /// client from the shared in-memory API host.
    /// </summary>
    /// <param name="factory">The shared factory that bootstraps the API host and InMemory database.</param>
    public UserApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Verifies the complete user lifecycle over HTTP: create (201) -&gt; read (200) -&gt; update (200)
    /// -&gt; delete (204) -&gt; read-again (404). This is the mandatory Validation Gate 5 CRUD path.
    /// </summary>
    /// <returns>A task that completes when the full lifecycle has been asserted.</returns>
    [Fact]
    public async Task User_Crud_Lifecycle_Succeeds()
    {
        // CREATE -> 201. MIGRATION: UserController.CreateUser (L156) / User.ascx.vb add-user (cmdAdd). The
        // body satisfies CreateUserDtoValidator: non-empty username; first/last name (<= 50); a valid email
        // (<= 256); and — because randomPassword is false — a password of length >= 7 with a matching
        // confirmPassword. Property names are camelCase so they bind directly onto CreateUserDto.
        var createBody = new
        {
            username = "itest_user",
            firstName = "Integration",
            lastName = "Tester",
            displayName = "Integration Tester",
            email = "integration.tester@dnnmigration.local",
            password = "P@ssw0rd123",           // length >= 7 (MinPasswordLength)
            confirmPassword = "P@ssw0rd123",     // MUST equal password (CompareValidator parity)
            passwordQuestion = "Favorite color?",
            passwordAnswer = "blue",
            portalID = 0,                        // no FK enforcement under InMemory, so 0 is fine
            authorize = true,
            notify = false,
            randomPassword = false               // => the password/confirmPassword rules apply
        };

        var createResponse = await _client.PostAsJsonAsync("/api/users", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        // MIGRATION: UserID is server-generated (ValueGeneratedOnAdd); read it back rather than assume it.
        created.Data!.UserID.Should().BeGreaterThan(0);
        created.Data.Username.Should().Be("itest_user");

        var id = created.Data.UserID;

        // READ -> 200. MIGRATION: UserController.GetUser (L497) / User.ascx.vb Page_Load read. Only the
        // Users-table scalars round-trip through GET (username/firstName/lastName/email); the owned
        // Membership/Profile are EF-ignored and default-initialized, so they are never asserted on.
        var getResponse = await _client.GetAsync($"/api/users/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched.Data!.UserID.Should().Be(id);
        fetched.Data.Username.Should().Be("itest_user");

        // UPDATE -> 200. MIGRATION: UserController.UpdateUser (L963) / User.ascx.vb cmdUpdate_Click. The body
        // maps onto UpdateUserDto (which carries NO credential fields); firstName/lastName/email are the
        // required, round-trippable identity scalars, so they are the ones asserted after the update.
        var updateBody = new
        {
            firstName = "Updated",
            lastName = "Tester",
            displayName = "Updated Tester",
            email = "updated.tester@dnnmigration.local",
            affiliateID = (int?)null,
            approved = true,
            timeZone = 0
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.FirstName.Should().Be("Updated");
        updated.Data.Email.Should().Be("updated.tester@dnnmigration.local");

        // DELETE -> 204. MIGRATION: UserController.DeleteUser (L200) / User.ascx.vb cmdDelete_Click. A 204
        // No Content carries no body, so no envelope is read here.
        var deleteResponse = await _client.DeleteAsync($"/api/users/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // VERIFY GONE -> 404. The now-removed user resolves to null in the service, which the controller
        // translates to an RFC 7807 404 (never the legacy silent null UserInfo return).
        var getAfterDelete = await _client.GetAsync($"/api/users/{id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that requesting a user that does not exist returns HTTP 404 (Not Found).
    /// </summary>
    /// <returns>A task that completes when the not-found response has been asserted.</returns>
    [Fact]
    public async Task GetUser_WithUnknownId_Returns404()
    {
        // MIGRATION: the legacy GetUser returned a null UserInfo for a missing id; the migrated GetById
        // maps that miss to an HTTP 404 produced centrally as RFC 7807 Problem Details.
        var response = await _client.GetAsync("/api/users/987654321");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies the administrator-protection delete restriction (finding #8): a user who is the
    /// administrator of a portal cannot be deleted, and the API surfaces the refusal as HTTP 409
    /// Conflict (RFC 7807) — not a 204 (deleted) or 404 (missing). The user must still exist afterward.
    /// </summary>
    /// <remarks>
    /// MIGRATION: reproduces the legacy <c>UserController.DeleteUser</c> deleteAdmin gate
    /// (Library/Components/Users/UserController.vb L200). The user is created over HTTP, then made a
    /// portal's administrator by seeding <c>Portals.AdministratorId</c> through a DI scope (no REST route
    /// assigns an arbitrary existing user as a portal administrator — <c>PortalService.CreateAsync</c>
    /// provisions its own admin). The seeded portal points at THIS user's server-assigned id only, so it
    /// never blocks the unrelated CRUD-lifecycle user in this class.
    /// </remarks>
    /// <returns>A task that completes when the 409 refusal and post-refusal existence are asserted.</returns>
    [Fact]
    public async Task DeleteUser_WhoAdministersAPortal_Returns409Conflict()
    {
        // CREATE the future portal administrator over HTTP.
        var createBody = new
        {
            username = "itest_admin_protected",
            firstName = "Protected",
            lastName = "Admin",
            displayName = "Protected Admin",
            email = "protected.admin@dnnmigration.local",
            password = "P@ssw0rd123",
            confirmPassword = "P@ssw0rd123",
            portalID = 0,
            authorize = true,
            notify = false,
            randomPassword = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/users", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        var adminId = created!.Data!.UserID;
        adminId.Should().BeGreaterThan(0);

        // Seed a Portal whose AdministratorId is exactly this user (directly, via a DI scope on the host).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
            db.Portals.Add(new Portal
            {
                PortalName = "Admin-Protected Portal",
                Email = "portal@dnnmigration.local",
                AdministratorId = adminId
            });
            await db.SaveChangesAsync();
        }

        // DELETE must be REFUSED with 409 Conflict (never 204/404) — the administrator-protection rule.
        var deleteResponse = await _client.DeleteAsync($"/api/users/{adminId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The user must STILL exist after the refused delete.
        var getAfter = await _client.GetAsync($"/api/users/{adminId}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// MIGRATION QA finding F1 (list data fidelity): verifies that the collection read
    /// (<c>GET /api/users</c>, which routes to <c>UserService.GetAllAsync</c> for the default host caller)
    /// hydrates each user's <c>membership</c> dates and <c>profile</c> exactly as the single-item read
    /// (<c>GET /api/users/{id}</c>) does — so a list row carries the SAME real values, not the
    /// <c>DateTime.MinValue</c> ("0001-01-01") membership dates and empty profile the buggy list projection
    /// returned.
    /// </summary>
    /// <remarks>
    /// The user is seeded directly through <see cref="IUserRepository"/> (the real persistence path that
    /// writes the <c>[Users]</c>, <c>[aspnet_Membership]</c>, <c>[UserPortals]</c> and <c>[aspnet_Profile]</c>
    /// rows) with EXPLICIT non-default membership dates and a populated profile, so the assertions can prove
    /// the list read hydrated genuine stored values. Date assertions compare the list value against the
    /// single-GET value (parity) and assert the year is greater than 1 (i.e. NOT <c>DateTime.MinValue</c>),
    /// which is robust regardless of the host time zone's effect on JSON date deserialization.
    /// </remarks>
    /// <returns>A task that completes when the list-vs-single hydration parity has been asserted.</returns>
    [Fact]
    public async Task UserList_HydratesMembershipDatesAndProfile_MatchingSingleGet()
    {
        // Seed a user with explicit membership dates + a populated profile via the real repository path.
        int seededId;
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var seed = new User
            {
                Username = "itest_f1_hydrate",
                FirstName = "Hydrate",
                LastName = "Target",
                DisplayName = "Hydrate Target",
                Email = "f1.hydrate@dnnmigration.local",
                PortalID = 0,
                Membership = new UserMembership
                {
                    Password = "not-a-real-hash",
                    Approved = true,
                    // Explicit, non-default dates: BuildMembershipRow preserves a non-default value, so these
                    // are the real stored values the list read must surface.
                    CreatedDate = new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc),
                    LastLoginDate = new DateTime(2021, 6, 20, 14, 45, 0, DateTimeKind.Utc),
                },
                Profile = new UserProfile
                {
                    Street = "123 Integration Way",
                    City = "Hydrationville",
                    Telephone = "555-0142",
                },
            };
            var created = await repo.AddAsync(seed);
            seededId = created.UserID;
        }
        seededId.Should().BeGreaterThan(0);

        // SINGLE GET (the already-correct HydrateAsync path) — the parity anchor.
        var singleResponse = await _client.GetAsync($"/api/users/{seededId}");
        singleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var single = await singleResponse.Content.ReadFromJsonAsync<Envelope<UserFullRead>>(JsonOptions);
        single.Should().NotBeNull();
        single!.Data.Should().NotBeNull();
        single.Data!.Membership.Should().NotBeNull();
        single.Data.Profile.Should().NotBeNull();
        // The single read already returns real values; year > 1 proves it is not DateTime.MinValue.
        single.Data.Membership!.CreatedDate.Year.Should().BeGreaterThan(1);
        single.Data.Profile!.City.Should().Be("Hydrationville");

        // LIST GET (the fixed GetAllAsync/HydrateManyAsync path) — must carry the SAME real values.
        var listResponse = await _client.GetAsync("/api/users");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<Envelope<List<UserFullRead>>>(JsonOptions);
        list.Should().NotBeNull();
        list!.Data.Should().NotBeNull();

        var listUser = list.Data!.SingleOrDefault(u => u.UserID == seededId);
        listUser.Should().NotBeNull("the seeded user must appear in the list read");
        listUser!.Membership.Should().NotBeNull();
        listUser.Profile.Should().NotBeNull();

        // F1 regression assertions: the list row's membership dates are the real stored values (NOT
        // DateTime.MinValue) and match the single GET exactly.
        listUser.Membership!.CreatedDate.Year.Should().BeGreaterThan(1);
        listUser.Membership.CreatedDate.Should().Be(single.Data.Membership.CreatedDate);
        listUser.Membership.LastLoginDate.Should().Be(single.Data.Membership.LastLoginDate);
        listUser.Membership.Approved.Should().Be(single.Data.Membership.Approved);

        // F1 regression assertions: the list row's profile is hydrated (NOT empty) and matches single GET.
        listUser.Profile!.City.Should().Be("Hydrationville");
        listUser.Profile.Street.Should().Be("123 Integration Way");
        listUser.Profile.Telephone.Should().Be("555-0142");
        listUser.Profile.City.Should().Be(single.Data.Profile.City);
    }

    /// <summary>
    /// Minimal read-model for the API's standard success envelope <c>{ "data": ..., "meta": ... }</c>.
    /// </summary>
    /// <typeparam name="T">The payload type carried under the envelope's <c>data</c> key.</typeparam>
    private sealed class Envelope<T>
    {
        /// <summary>Gets or sets the deserialized <c>data</c> payload.</summary>
        public T? Data { get; set; }

        /// <summary>
        /// Gets or sets the raw <c>meta</c> element. Captured as a <see cref="JsonElement"/> because these
        /// tests do not assert on metadata; a value type, so it never triggers CS8618.
        /// </summary>
        public JsonElement Meta { get; set; }
    }

    /// <summary>
    /// Minimal read-model projecting only the round-trippable <c>Users</c>-table scalars of the API's
    /// <c>UserDto</c> that these tests assert on. Reference members are nullable so they never require
    /// non-null construction (no CS8618); <see cref="UserID"/> is a value type.
    /// </summary>
    private sealed class UserRead
    {
        /// <summary>Gets or sets the server-generated user identifier (serialized as <c>userID</c>).</summary>
        public int UserID { get; set; }

        /// <summary>Gets or sets the login name (serialized as <c>username</c>).</summary>
        public string? Username { get; set; }

        /// <summary>Gets or sets the first (given) name (serialized as <c>firstName</c>).</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the e-mail address (serialized as <c>email</c>).</summary>
        public string? Email { get; set; }
    }

    /// <summary>
    /// Read-model projecting the members the F1 hydration test asserts on: identity scalars plus the nested
    /// <c>membership</c> and <c>profile</c> objects. Reference members are nullable so no CS8618 is raised.
    /// </summary>
    private sealed class UserFullRead
    {
        /// <summary>Gets or sets the server-generated user identifier (serialized as <c>userID</c>).</summary>
        public int UserID { get; set; }

        /// <summary>Gets or sets the login name (serialized as <c>username</c>).</summary>
        public string? Username { get; set; }

        /// <summary>Gets or sets the owning portal id (serialized as <c>portalID</c>).</summary>
        public int PortalID { get; set; }

        /// <summary>Gets or sets the nested membership projection (serialized as <c>membership</c>).</summary>
        public MembershipRead? Membership { get; set; }

        /// <summary>Gets or sets the nested profile projection (serialized as <c>profile</c>).</summary>
        public ProfileRead? Profile { get; set; }
    }

    /// <summary>Minimal read-model of the API's <c>MembershipDto</c> — only the fields the F1 test asserts on.</summary>
    private sealed class MembershipRead
    {
        /// <summary>Gets or sets whether the account is approved (serialized as <c>approved</c>).</summary>
        public bool Approved { get; set; }

        /// <summary>Gets or sets the account creation date (serialized as <c>createdDate</c>).</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>Gets or sets the last-login date (serialized as <c>lastLoginDate</c>).</summary>
        public DateTime LastLoginDate { get; set; }
    }

    /// <summary>Minimal read-model of the API's <c>ProfileDto</c> — only the fields the F1 test asserts on.</summary>
    private sealed class ProfileRead
    {
        /// <summary>Gets or sets the street line (serialized as <c>street</c>).</summary>
        public string? Street { get; set; }

        /// <summary>Gets or sets the city (serialized as <c>city</c>).</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets the telephone number (serialized as <c>telephone</c>).</summary>
        public string? Telephone { get; set; }
    }
}
