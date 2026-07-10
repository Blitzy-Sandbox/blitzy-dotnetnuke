// -----------------------------------------------------------------------------
//  UserSearchApiTests.cs
//
//  MIGRATION: Net-new xUnit integration-test class covering finding C3 (server-side
//  user search) end-to-end against the in-memory host.
//
//  AAP §0.7.2 maps the legacy Website/admin/Users list search to
//  "GET /api/users?query=... | ?filterProperty=&filter=". The migrated
//  UsersController.GetAll now forwards both search shapes to
//  UserService.SearchAsync (a field-specific Username/Email match, or a free-text
//  match across username/email/display-name/first-name/last-name, honouring the
//  effective portal scope) instead of silently ignoring them. These tests prove the
//  search terms are honoured SERVER-SIDE for BOTH the field-specific filter and the
//  free-text query, and that an absent search returns the full list (opt-in).
//
//  The class owns an isolated EF Core InMemory store (IClassFixture) and seeds rows
//  through ResetAndSeedAsync. The User.Membership owned type defaults to new(), so a
//  directly-seeded user persists cleanly; the default HOST super-user identity means
//  the unscoped (all-portals) search path is exercised.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for server-side user search (finding C3), exercised against the in-memory
/// <c>DnnMigration.Api</c> host.
/// </summary>
public class UserSearchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSearchApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public UserSearchApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Seeds two users (Alice / Bob) into the isolated store for the search assertions.
    /// </summary>
    private Task SeedTwoUsersAsync() => _factory.ResetAndSeedAsync(async db =>
    {
        db.Users.Add(new User
        {
            UserID = 3001,
            PortalID = 0,
            Username = "alice",
            Email = "alice@contoso.example",
            FirstName = "Alice",
            LastName = "Anderson",
            DisplayName = "Alice Anderson"
        });
        db.Users.Add(new User
        {
            UserID = 3002,
            PortalID = 0,
            Username = "bob",
            Email = "bob@northwind.example",
            FirstName = "Bob",
            LastName = "Baker",
            DisplayName = "Bob Baker"
        });
        await Task.CompletedTask;
    });

    // -------------------------------------------------------------------------
    //  C3 - field-specific ?filterProperty=&filter=
    // -------------------------------------------------------------------------

    /// <summary>
    /// A field-specific search on Username is filtered SERVER-SIDE: only the user whose username matches is
    /// returned. Core assertion for the finding-C3 field-search shape.
    /// </summary>
    [Fact]
    public async Task GetAll_WithUsernameFilter_FiltersServerSide()
    {
        await SeedTwoUsersAsync();

        var response = await _client.GetAsync("/api/users?filterProperty=Username&filter=alice");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(u => u.UserID == 3001).Which.Username.Should().Be("alice");
        envelope.Data.Should().NotContain(u => u.UserID == 3002,
            "the field-specific username filter must exclude the non-matching user server-side");
    }

    /// <summary>
    /// A field-specific search on Email is filtered SERVER-SIDE over the email address only.
    /// </summary>
    [Fact]
    public async Task GetAll_WithEmailFilter_FiltersServerSide()
    {
        await SeedTwoUsersAsync();

        var response = await _client.GetAsync("/api/users?filterProperty=Email&filter=northwind");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(u => u.UserID == 3002).Which.Username.Should().Be("bob");
        envelope.Data.Should().NotContain(u => u.UserID == 3001);
    }

    // -------------------------------------------------------------------------
    //  C3 - free-text ?query= (all fields)
    // -------------------------------------------------------------------------

    /// <summary>
    /// A free-text <c>?query=</c> matches across the identity fields (here the last name) and is filtered
    /// SERVER-SIDE. Core assertion for the finding-C3 free-text shape.
    /// </summary>
    [Fact]
    public async Task GetAll_WithFreeTextQuery_FiltersServerSide()
    {
        await SeedTwoUsersAsync();

        var response = await _client.GetAsync("/api/users?query=anderson");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(u => u.UserID == 3001).Which.Username.Should().Be("alice");
        envelope.Data.Should().NotContain(u => u.UserID == 3002,
            "the free-text query must match on the identity fields and exclude the non-matching user");
    }

    /// <summary>
    /// A search term matching no user returns an empty data array, proving the filter is genuinely applied.
    /// </summary>
    [Fact]
    public async Task GetAll_WithNonMatchingQuery_ReturnsEmpty()
    {
        await SeedTwoUsersAsync();

        var response = await _client.GetAsync("/api/users?query=zzz-no-such-user");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().BeEmpty();
    }

    /// <summary>
    /// With no search parameters the full list is returned (search is strictly opt-in).
    /// </summary>
    [Fact]
    public async Task GetAll_WithoutSearch_ReturnsAll()
    {
        await SeedTwoUsersAsync();

        var response = await _client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<UserRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Select(u => u.UserID).Should().Contain(new[] { 3001, 3002 });
    }

    // -------------------------------------------------------------------------
    //  Local read-models (test-only shapes)
    // -------------------------------------------------------------------------

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    private sealed class UserRead
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}
