// MIGRATION: [QA-1 INFO-2 / AAP Gate 5] User CRUD integration test. Verifies the Gate-5 status-code contract
// (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204). POST carries PortalId in the body; GET/PUT/DELETE require a
// BindRequired ?portalId= query parameter (UsersController). The seeded Test super-user (portalId=0, isSuperUser)
// bypasses ApiControllerBase.EnforceTenant, so portalId=0 is used throughout.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

public sealed class UserCrudTests : IClassFixture<CustomWebApplicationFactory>
{
    private const int PortalId = 0;
    private readonly HttpClient _client;

    public UserCrudTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_Crud_FollowsGate5StatusContract()
    {
        // POST -> 201. Username/DisplayName/Email(format)/FirstName/LastName satisfy CreateUserValidator. Password and
        // Confirm are omitted (both null => the equality rule passes), so the service generates a random password.
        var createRequest = new
        {
            portalId = PortalId,
            username = "qa_integration_user",
            email = "qa.integration.user@example.com",
            displayName = "QA Integration User",
            firstName = "QA",
            lastName = "User"
        };
        var createResponse = await _client.PostAsync(
            "/api/users",
            JsonContent.Create(createRequest, options: EnvelopeReader.Web));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();
        var userId = await EnvelopeReader.ReadDataIntAsync(createResponse, "userId");
        userId.Should().BeGreaterThan(0);

        // GET -> 200 (portalId query parameter is BindRequired)
        var getResponse = await _client.GetAsync($"/api/users/{userId}?portalId={PortalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // PUT -> 200. UpdateUserValidator requires Email(format)/DisplayName/FirstName/LastName.
        var updateRequest = new
        {
            email = "qa.integration.user@example.com",
            displayName = "QA Integration User (Updated)",
            firstName = "QA",
            lastName = "User",
            isApproved = true,
            lockedOut = false
        };
        var updateResponse = await _client.PutAsync(
            $"/api/users/{userId}?portalId={PortalId}",
            JsonContent.Create(updateRequest, options: EnvelopeReader.Web));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // DELETE -> 204 (the created user is not the portal administrator, so the admin guard does not apply)
        var deleteResponse = await _client.DeleteAsync($"/api/users/{userId}?portalId={PortalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
