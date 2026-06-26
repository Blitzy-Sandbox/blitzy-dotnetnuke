// MIGRATION: [QA-1 INFO-2 / AAP Gate 5] Portal CRUD integration test. Verifies the Gate-5 status-code contract
// (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204) end-to-end through the real Program.cs pipeline against an
// EF Core InMemory store. PortalsController POST/GET/PUT/DELETE are body/route only (no portalId query parameter).

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

public sealed class PortalCrudTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PortalCrudTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Portal_Crud_FollowsGate5StatusContract()
    {
        // POST -> 201. PortalName + Email satisfy CreatePortalValidator; no Admin* fields supplied, so the optional
        // administrator-bootstrap group is skipped and the portal is created without provisioning an admin user.
        var createRequest = new { portalName = "QA Integration Portal", email = "qa@portal.local" };
        var createResponse = await _client.PostAsync(
            "/api/portals",
            JsonContent.Create(createRequest, options: EnvelopeReader.Web));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull("HandleCreated emits a CreatedAtAction Location header");
        var portalId = await EnvelopeReader.ReadDataIntAsync(createResponse, "portalId");
        portalId.Should().BeGreaterThan(0);

        // GET -> 200
        var getResponse = await _client.GetAsync($"/api/portals/{portalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // PUT -> 200. UpdatePortalValidator applies only maximum-length checks, so a minimal valid body succeeds.
        var updateRequest = new { portalId, portalName = "QA Integration Portal (Updated)" };
        var updateResponse = await _client.PutAsync(
            $"/api/portals/{portalId}",
            JsonContent.Create(updateRequest, options: EnvelopeReader.Web));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // DELETE -> 204
        var deleteResponse = await _client.DeleteAsync($"/api/portals/{portalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
