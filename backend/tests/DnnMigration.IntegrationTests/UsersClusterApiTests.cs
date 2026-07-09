// MIGRATION: Net-new end-to-end integration tests for the QA-reported "Users cluster" defects (findings
// J, K, G, H, E). These drive the migrated stateless JSON API over real HTTP against the in-process
// DnnMigration.Api host (CustomWebApplicationFactory: EF Core InMemory store + permissive test auth), and
// re-execute the exact reproduction flows from the QA report to prove each fix at runtime:
//   * J - a duplicate (PortalID, Username) create is rejected with 409 Conflict (was: silently 201).
//   * K - RandomPassword=true provisions a USABLE account: the server-generated one-time password is
//         returned in the create response `meta` and successfully authenticates via /api/auth/login
//         (was: an approved-but-unusable account holding a BCrypt hash of the empty string).
//   * G - the user profile is persisted to [aspnet_Profile] and round-trips through GET (was: never
//         persisted or hydrated).
//   * H - updating a user's identity email keeps the denormalized [aspnet_Membership].[Email] in sync
//         (was: the credential email drifted, breaking email-based membership lookups).
//   * E - changing a password with the WRONG current password on an EXISTING user returns 409 Conflict,
//         while a genuinely missing user returns 404 (was: a wrong old password returned a misleading 404).
// The real (DI-registered, singleton) BCrypt IPasswordHasher is used throughout - only the database and
// authentication scheme are swapped by the factory - so the K login flow exercises a genuine BCrypt
// hash-on-create / verify-on-login round-trip.
using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for the QA "Users cluster" fixes (findings J, K, G, H, E), executed
/// against the full <c>DnnMigration.Api</c> host booted in-memory by
/// <see cref="CustomWebApplicationFactory"/>. Each test class receives its own factory instance and
/// therefore its own isolated InMemory database, so usernames only need to be unique within this class.
/// </summary>
public class UsersClusterApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Initializes the fixture, capturing an authenticated HTTP client from the shared host.</summary>
    /// <param name="factory">The shared factory that bootstraps the API host and InMemory database.</param>
    public UsersClusterApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------------------------------------------------------------------------------------------
    // Finding J - duplicate username in the same portal is rejected with 409 Conflict.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_DuplicateUsernameInSamePortal_Returns409Conflict()
    {
        // First create succeeds (201).
        var first = await _client.PostAsJsonAsync("/api/users", BuildCreate("dupe_j", "dupe.j@x.local"), JsonOptions);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second create with the SAME (portalID, username) must be refused with 409 Conflict (RFC 7807).
        var second = await _client.PostAsJsonAsync("/api/users", BuildCreate("dupe_j", "dupe.j.again@x.local"), JsonOptions);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Create_SameUsernameDifferentPortal_Succeeds()
    {
        // A username used in portal 0 does NOT block the same username in a different portal (per-portal
        // uniqueness), so both creates succeed.
        var p0 = await _client.PostAsJsonAsync("/api/users", BuildCreate("scoped_j", "scoped0@x.local", portalId: 0), JsonOptions);
        p0.StatusCode.Should().Be(HttpStatusCode.Created);

        var p1 = await _client.PostAsJsonAsync("/api/users", BuildCreate("scoped_j", "scoped1@x.local", portalId: 1), JsonOptions);
        p1.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---------------------------------------------------------------------------------------------
    // Finding K - RandomPassword=true returns a usable, immediately-authenticating account.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithRandomPassword_ReturnsGeneratedPasswordInMeta_ThatCanLogIn()
    {
        // CREATE with randomPassword=true and NO password supplied (the password rules are skipped by the
        // validator's When(!RandomPassword) guard). authorize=true so the membership is Approved.
        var createBody = new
        {
            username = "rand_k",
            firstName = "Random",
            lastName = "Password",
            email = "rand.k@x.local",
            portalID = 0,
            authorize = true,
            notify = false,
            randomPassword = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/users", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // The one-time generated plaintext is surfaced in the create response envelope's `meta`.
        var envelope = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Meta.ValueKind.Should().Be(JsonValueKind.Object);
        envelope.Meta.TryGetProperty("generatedPassword", out var generatedPasswordElement)
            .Should().BeTrue("the create response meta must carry the server-generated one-time password");
        var generatedPassword = generatedPasswordElement.GetString();
        generatedPassword.Should().NotBeNullOrWhiteSpace();

        // The returned USER projection must NOT carry any credential material (security contract).
        envelope.Data.Should().NotBeNull();
        envelope.Data!.Username.Should().Be("rand_k");

        // The generated password must actually authenticate the freshly-provisioned account.
        var loginBody = new { username = "rand_k", password = generatedPassword, portalId = 0 };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginBody, JsonOptions);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the server-generated password must be usable");

        var tokens = await loginResponse.Content.ReadFromJsonAsync<Envelope<TokenRead>>(JsonOptions);
        tokens.Should().NotBeNull();
        tokens!.Data.Should().NotBeNull();
        tokens.Data!.AccessToken.Should().NotBeNullOrWhiteSpace("a successful login issues a JWT access token");
    }

    [Fact]
    public async Task Create_WithoutRandomPassword_DoesNotEmitGeneratedPasswordMeta()
    {
        // The common caller-supplied-password path must NOT leak a generatedPassword into meta.
        var createResponse = await _client.PostAsJsonAsync("/api/users", BuildCreate("nonrand_k", "nonrand.k@x.local"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var envelope = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        envelope.Should().NotBeNull();
        // meta is present (default timestamp) but must not contain a generatedPassword key.
        if (envelope!.Meta.ValueKind == JsonValueKind.Object)
        {
            envelope.Meta.TryGetProperty("generatedPassword", out _)
                .Should().BeFalse("a caller-supplied password must never surface a generated password");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Finding G - user profile is persisted to [aspnet_Profile] and round-trips through GET.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Profile_UpdateThenGet_RoundTripsProfileFields()
    {
        // CREATE a plain user (CreateUserDto carries no profile fields), then capture the server id.
        var createResponse = await _client.PostAsJsonAsync("/api/users", BuildCreate("profile_g", "profile.g@x.local"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        var id = created!.Data!.UserID;
        id.Should().BeGreaterThan(0);

        // A GET immediately after create returns an (empty) profile object, never null.
        var initialGet = await _client.GetAsync($"/api/users/{id}");
        initialGet.StatusCode.Should().Be(HttpStatusCode.OK);
        var initial = await initialGet.Content.ReadFromJsonAsync<Envelope<UserReadWithProfile>>(JsonOptions);
        initial!.Data!.Profile.Should().NotBeNull();
        initial.Data.Profile!.City.Should().BeNullOrEmpty("a brand-new user has no profile data yet");

        // UPDATE with the flat profile fields (mapped onto User.Profile.* by AutoMapper's ForPath rules).
        var updateBody = new
        {
            firstName = "Profile",
            lastName = "Owner",
            email = "profile.g@x.local",
            approved = true,
            street = "123 Main St",
            unit = "Apt 4",
            city = "Springfield",
            region = "IL",
            country = "USA",
            postalCode = "62704",
            telephone = "555-1234",
            cell = "555-5678",
            fax = "555-9999",
            website = "https://example.com",
            im = "profile.im",
            preferredLocale = "en-US",
            timeZone = 5
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET must now hydrate the persisted profile blob back onto the response.
        var getResponse = await _client.GetAsync($"/api/users/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<UserReadWithProfile>>(JsonOptions);
        fetched!.Data!.Profile.Should().NotBeNull();

        var profile = fetched.Data.Profile!;
        profile.Street.Should().Be("123 Main St");
        profile.City.Should().Be("Springfield");
        profile.Region.Should().Be("IL");
        profile.Country.Should().Be("USA");
        profile.PostalCode.Should().Be("62704");
        profile.Telephone.Should().Be("555-1234");
        profile.Website.Should().Be("https://example.com");
        profile.PreferredLocale.Should().Be("en-US");
        profile.TimeZone.Should().Be(5);
    }

    // ---------------------------------------------------------------------------------------------
    // Finding H - updating the identity email keeps [aspnet_Membership].[Email] in sync.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_Email_SyncsDenormalizedMembershipEmail()
    {
        // CREATE with an initial email; the credential row is stamped with the same email.
        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", BuildCreate("email_h", "old.h@x.local"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        var id = created!.Data!.UserID;

        // UPDATE the identity email.
        var updateBody = new
        {
            firstName = "Email",
            lastName = "Mover",
            email = "New.H@x.local"
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Inspect the persisted [aspnet_Membership] credential row directly: its Email (and lowered form)
        // must track the new identity email, proving the two denormalized columns did not drift.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var membershipKey = new Guid(id, 0, 0, new byte[8]); // mirrors UserRepository.MembershipKey(id)
        var membership = await db.UserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MembershipUserId == membershipKey);

        membership.Should().NotBeNull("the credential row must exist for a created user");
        membership!.Email.Should().Be("New.H@x.local", "the credential email is synced from the updated identity email");
        membership.LoweredEmail.Should().Be("new.h@x.local", "the lowered credential email tracks the new email");
    }

    // ---------------------------------------------------------------------------------------------
    // Finding E - change-password error semantics (wrong old password => 409; missing user => 404).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_WrongOldPassword_OnExistingUser_Returns409Conflict()
    {
        // CREATE a user with a known current password.
        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", BuildCreate("chpw_e", "chpw.e@x.local", password: "P@ssw0rd123"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        var id = created!.Data!.UserID;

        // Change-password with a WRONG current password but an otherwise VALID new password (>= 7 chars,
        // differs from old). The user EXISTS, so a bad current credential is a 409 Conflict - not a 404.
        var body = new { oldPassword = "WrongOld1!", newPassword = "N3wP@ssw0rd456" };
        var response = await _client.PostAsJsonAsync($"/api/users/{id}/change-password", body, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ChangePassword_MissingUser_Returns404NotFound()
    {
        // A genuinely missing user is still a 404 (the controller pre-fetch), even with a valid DTO.
        var body = new { oldPassword = "P@ssw0rd123", newPassword = "N3wP@ssw0rd456" };
        var response = await _client.PostAsJsonAsync("/api/users/987654321/change-password", body, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangePassword_CorrectOldPassword_Returns204NoContent()
    {
        // Happy path (regression guard): the correct current password + a valid new password succeeds (204).
        var createResponse = await _client.PostAsJsonAsync(
            "/api/users", BuildCreate("chpw_ok_e", "chpw.ok.e@x.local", password: "P@ssw0rd123"), JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<UserRead>>(JsonOptions);
        var id = created!.Data!.UserID;

        var body = new { oldPassword = "P@ssw0rd123", newPassword = "N3wP@ssw0rd456" };
        var response = await _client.PostAsJsonAsync($"/api/users/{id}/change-password", body, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers and read-models
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a valid <c>POST /api/users</c> body (camelCase, binds onto CreateUserDto). With
    /// <c>randomPassword=false</c> (default) a compliant password/confirmPassword pair is supplied.
    /// </summary>
    private static object BuildCreate(
        string username,
        string email,
        string password = "P@ssw0rd123",
        bool authorize = true,
        int portalId = 0) => new
        {
            username,
            firstName = "Cluster",
            lastName = "Tester",
            email,
            password,
            confirmPassword = password,
            portalID = portalId,
            authorize,
            notify = false,
            randomPassword = false
        };

    /// <summary>Minimal read-model for the standard success envelope <c>{ "data": ..., "meta": ... }</c>.</summary>
    private sealed class Envelope<T>
    {
        public T? Data { get; set; }

        /// <summary>The raw <c>meta</c> element (captured as JsonElement so finding-K can read generatedPassword).</summary>
        public JsonElement Meta { get; set; }
    }

    /// <summary>Minimal user read-model (round-trippable Users-table scalars).</summary>
    private sealed class UserRead
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>User read-model including the nested profile projection (finding G).</summary>
    private sealed class UserReadWithProfile
    {
        public int UserID { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public ProfileRead? Profile { get; set; }
    }

    /// <summary>Nested profile projection matching the API's camelCase <c>profile</c> object.</summary>
    private sealed class ProfileRead
    {
        public string? Street { get; set; }
        public string? Unit { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Telephone { get; set; }
        public string? Cell { get; set; }
        public string? Fax { get; set; }
        public string? Website { get; set; }
        public string? IM { get; set; }
        public int TimeZone { get; set; }
        public string? PreferredLocale { get; set; }
    }

    /// <summary>Minimal token read-model for the login success envelope (finding K).</summary>
    private sealed class TokenRead
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}
