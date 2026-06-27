// MIGRATION: [CP-final review - profile workflow parity] Integration suite for the migrated profile read/update
// endpoints on UsersController (GET/PUT /api/users/{id}/profile), which replace the legacy
// Website/admin/Users/Profile.ascx.vb "view profile" / "save profile" postbacks. These endpoints project and upsert
// the EXISTING DNN profile EAV ([ProfilePropertyDefinition] per-portal definitions + [UserProfile] per-user values,
// AAP 0.7.1 - no schema alteration), so the suite seeds those legacy tables directly and proves:
//   - GET projects the stored values to the flat UserProfileDto, composing FullName = FirstName + " " + LastName;
//   - GET for a user that is not in the portal returns 404 (HandleGet);
//   - PUT inserts a new [UserProfile] value row when none exists and the change is persisted (re-GET confirms);
//   - PUT updates an existing value row in place;
//   - PUT enforces the data-driven Required rule carried by the definition, returning 400 (HandleResult).
//
// The suite runs against the real Api Program pipeline hosted by CustomWebApplicationFactory (EF Core InMemory + the
// always-authenticated super-user TestAuthHandler, which bypasses ApiControllerBase.EnforceTenant for any portalId).
// MIGRATION (test-design note): InMemory runs an identity generator for a default-valued int key, so every seed lets
// EF assign the id and reads it back (never an explicit 0), keeping the cases independent of key round-tripping.
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
/// Integration tests for the <c>UsersController</c> profile endpoints (GET/PUT <c>/api/users/{id}/profile</c>)
/// covering EAV projection, FullName composition, the portal-scoped not-found edge, insert/update upsert paths, and
/// the data-driven Required validation. The class-level <c>[Trait("Category", "Integration")]</c> selects the whole
/// suite under the Gate-5 <c>--filter "Category=Integration"</c> run.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UserProfileControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserProfileControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // --- seed helpers ------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a non-deleted, portal-scoped <see cref="ProfilePropertyDefinition"/> for the given well-known
    /// property name (EF assigns the PropertyDefinitionId).
    /// </summary>
    private static ProfilePropertyDefinition Def(int portalId, string name, bool required = false, int length = 0, string? validation = null) =>
        new()
        {
            PortalId = portalId,
            PropertyName = name,
            PropertyCategory = "Name",
            Deleted = false,
            DataType = 0,
            Required = required,
            Length = length,
            ValidationExpression = validation,
            ViewOrder = 0,
            Visible = true
        };

    /// <summary>Projects the success envelope's <c>data</c> element into a <see cref="UserProfileDto"/>.</summary>
    private static async Task<UserProfileDto> ReadProfileAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        return data.Deserialize<UserProfileDto>(JsonOptions)
               ?? throw new InvalidOperationException("The success envelope contained a null 'data' payload.");
    }

    // --- GET /api/users/{id}/profile ---------------------------------------------------------------------------

    /// <summary>
    /// GET projects the stored EAV values to the flat DTO and composes FullName VERBATIM (FirstName + " " +
    /// LastName), matching UserProfile.vb.
    /// </summary>
    [Fact]
    public async Task Get_Profile_Returns200_WithProjectedValuesAndComposedFullName()
    {
        var portalId = 0;
        var userId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Profile Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            var user = new User { Username = "profile_get_user", PortalId = portal.PortalId };
            db.Users.Add(user);

            var first = Def(portal.PortalId, "FirstName");
            var last = Def(portal.PortalId, "LastName");
            var city = Def(portal.PortalId, "City");
            db.ProfilePropertyDefinitions.AddRange(first, last, city);
            db.SaveChanges();

            db.UserProfileValues.AddRange(
                new UserProfileValue { UserId = user.UserId, PropertyDefinitionId = first.PropertyDefinitionId, PropertyValue = "John", LastUpdatedDate = DateTime.UtcNow },
                new UserProfileValue { UserId = user.UserId, PropertyDefinitionId = last.PropertyDefinitionId, PropertyValue = "Doe", LastUpdatedDate = DateTime.UtcNow },
                new UserProfileValue { UserId = user.UserId, PropertyDefinitionId = city.PropertyDefinitionId, PropertyValue = "Springfield", LastUpdatedDate = DateTime.UtcNow });
            db.SaveChanges();

            portalId = portal.PortalId;
            userId = user.UserId;
        });

        var response = await _client.GetAsync($"/api/users/{userId}/profile?portalId={portalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await ReadProfileAsync(response);
        profile.FirstName.Should().Be("John");
        profile.LastName.Should().Be("Doe");
        profile.City.Should().Be("Springfield");
        profile.FullName.Should().Be("John Doe");
        // A property the portal does not define projects to null (UserProfile.vb GetPropertyValue -> Null.NullString).
        profile.Website.Should().BeNull();
    }

    /// <summary>GET for a user that does not exist in the portal returns 404 (HandleGet).</summary>
    [Fact]
    public async Task Get_Profile_ForUnknownUser_Returns404()
    {
        var portalId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Profile Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();
            portalId = portal.PortalId;
        });

        var response = await _client.GetAsync($"/api/users/999999/profile?portalId={portalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PUT /api/users/{id}/profile ---------------------------------------------------------------------------

    /// <summary>
    /// PUT inserts a new [UserProfile] value row when the user has none for a defined property, and the change is
    /// persisted (a follow-up GET reads the new value back from the EAV).
    /// </summary>
    [Fact]
    public async Task Put_Profile_Returns200_InsertsNewValue_AndPersists()
    {
        var portalId = 0;
        var userId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Profile Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            var user = new User { Username = "profile_put_insert_user", PortalId = portal.PortalId };
            db.Users.Add(user);
            db.ProfilePropertyDefinitions.Add(Def(portal.PortalId, "FirstName"));
            db.SaveChanges();

            portalId = portal.PortalId;
            userId = user.UserId;
        });

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/users/{userId}/profile?portalId={portalId}", new UserProfileDto { FirstName = "Inserted" }, JsonOptions);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadProfileAsync(putResponse);
        updated.FirstName.Should().Be("Inserted");

        // Persistence proof: an independent GET reads the value back from the EAV.
        var getResponse = await _client.GetAsync($"/api/users/{userId}/profile?portalId={portalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadProfileAsync(getResponse)).FirstName.Should().Be("Inserted");
    }

    /// <summary>PUT updates an existing [UserProfile] value row in place.</summary>
    [Fact]
    public async Task Put_Profile_Returns200_UpdatesExistingValue()
    {
        var portalId = 0;
        var userId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Profile Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            var user = new User { Username = "profile_put_update_user", PortalId = portal.PortalId };
            db.Users.Add(user);
            var first = Def(portal.PortalId, "FirstName");
            db.ProfilePropertyDefinitions.Add(first);
            db.SaveChanges();

            db.UserProfileValues.Add(new UserProfileValue
            {
                UserId = user.UserId,
                PropertyDefinitionId = first.PropertyDefinitionId,
                PropertyValue = "OldName",
                LastUpdatedDate = DateTime.UtcNow
            });
            db.SaveChanges();

            portalId = portal.PortalId;
            userId = user.UserId;
        });

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/users/{userId}/profile?portalId={portalId}", new UserProfileDto { FirstName = "NewName" }, JsonOptions);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadProfileAsync(putResponse)).FirstName.Should().Be("NewName");
    }

    /// <summary>
    /// PUT enforces the data-driven Required rule carried by the [ProfilePropertyDefinition] (legacy DNN rendered a
    /// RequiredFieldValidator from it): an empty value for a Required property returns 400 (HandleResult).
    /// </summary>
    [Fact]
    public async Task Put_Profile_RequiredViolation_Returns400()
    {
        var portalId = 0;
        var userId = 0;
        _factory.ResetAndSeed(db =>
        {
            var portal = new Portal { PortalName = "Profile Portal" };
            db.Portals.Add(portal);
            db.SaveChanges();

            var user = new User { Username = "profile_put_required_user", PortalId = portal.PortalId };
            db.Users.Add(user);
            db.ProfilePropertyDefinitions.Add(Def(portal.PortalId, "FirstName", required: true));
            db.SaveChanges();

            portalId = portal.PortalId;
            userId = user.UserId;
        });

        var putResponse = await _client.PutAsJsonAsync(
            $"/api/users/{userId}/profile?portalId={portalId}", new UserProfileDto { FirstName = "" }, JsonOptions);

        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
