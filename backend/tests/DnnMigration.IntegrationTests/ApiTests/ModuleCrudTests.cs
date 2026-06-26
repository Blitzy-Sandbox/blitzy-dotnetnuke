// MIGRATION: [QA-1 Issue #2 + INFO-2 / AAP Gate 5] Module CRUD integration test. This is the definitive runtime
// proof for QA-1 Issue #2: it exercises the REAL EF Core change tracker (InMemory) that the 313 mock-based unit tests
// never touched. Before the fix, Module.ModuleId was int? (nullable) and every create threw
// InvalidOperationException ("primary key property 'ModuleId' is null") -> 500, which would also fail here under
// InMemory. After the fix (non-nullable key + ValueGeneratedOnAdd), a valid create returns 201 and the full Gate-5
// status contract holds. The malformed-body case proves the CreateModuleValidator ModuleDefId rule turns the former
// 500 into a clean 400 (matching the other four resources). GET/PUT/DELETE require the BindRequired ?portalId= query.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

public sealed class ModuleCrudTests : IClassFixture<CustomWebApplicationFactory>
{
    private const int PortalId = 0;
    private const int TabId = 0;
    private readonly HttpClient _client;

    public ModuleCrudTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Module_Crud_FollowsGate5StatusContract()
    {
        // POST -> 201. A fully populated valid body. moduleOrder = 0 (not -1) avoids the bottom-placement branch.
        // moduleDefId = 1 satisfies the new CreateModuleValidator NotNull + GreaterThan(0) rule. Empty permissions.
        var createRequest = new
        {
            portalId = PortalId,
            tabId = TabId,
            moduleDefId = 1,
            desktopModuleId = 1,
            moduleTitle = "QA Integration Module",
            paneName = "ContentPane",
            moduleOrder = 0,
            permissions = Array.Empty<object>()
        };
        var createResponse = await _client.PostAsync(
            "/api/modules",
            JsonContent.Create(createRequest, options: EnvelopeReader.Web));

        // Pre-fix this returned 500 (change-tracker null primary key). Post-fix: 201 Created.
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();
        var moduleId = await EnvelopeReader.ReadDataIntAsync(createResponse, "moduleId");
        moduleId.Should().BeGreaterThan(0, "ValueGeneratedOnAdd assigns the InMemory store key on insert");

        // GET -> 200 (portalId query parameter is BindRequired)
        var getResponse = await _client.GetAsync($"/api/modules/{moduleId}?portalId={PortalId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // PUT -> 200. UpdateModuleValidator requires only TabId >= 0 (Border optional). Empty permissions; the
        // AllModules / IsDefaultModule operation flags default false (no propagation / no portal-setting writes).
        var updateRequest = new
        {
            tabId = TabId,
            moduleTitle = "QA Integration Module (Updated)",
            paneName = "ContentPane",
            moduleOrder = 0,
            permissions = Array.Empty<object>()
        };
        var updateResponse = await _client.PutAsync(
            $"/api/modules/{moduleId}?portalId={PortalId}",
            JsonContent.Create(updateRequest, options: EnvelopeReader.Web));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // DELETE -> 204
        var deleteResponse = await _client.DeleteAsync($"/api/modules/{moduleId}?portalId={PortalId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Module_Create_WithEmptyBody_Returns400_NotServerError()
    {
        // QA-1 Issue #2: a malformed {} body previously reached ModuleService.CreateAsync and returned 500. The new
        // CreateModuleValidator ModuleDefId rule (NotNull) now rejects it as a clean 400 before the service runs,
        // matching Portal/User/Role/Tab. Asserting on the field key keeps this resilient to the envelope unification
        // in QA-1 Issue #3 (the field name "ModuleDefId" is present in both the framework and the unified envelope).
        var response = await _client.PostAsync(
            "/api/modules",
            JsonContent.Create(new { }, options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ModuleDefId", "the required-field validation error names the offending field");
    }
}
