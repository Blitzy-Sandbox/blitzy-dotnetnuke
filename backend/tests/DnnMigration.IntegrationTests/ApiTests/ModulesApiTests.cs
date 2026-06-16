// =============================================================================
//  ModulesApiTests
//  -----------------------------------------------------------------------------
//  End-to-end HTTP integration tests for the Modules REST resource
//  (/api/v1/modules), driven in-process against the REAL DnnMigration.Api
//  middleware + DI pipeline through CustomWebApplicationFactory (EF Core
//  InMemory). This is one of the THREE Gate-5-critical CRUD classes
//  (dotnet test --filter Category=Integration -> POST 201, GET 200, PUT 200,
//  DELETE 204).
//
//  MIGRATION: the legacy DNN 4.9.0.85 codebase shipped ZERO automated tests, so
//  this is a CREATE / from-scratch class with no legacy equivalent. The Module
//  surface under test is ported from Library/Components/Modules/ModuleController.vb
//  (AddModule / GetModule / GetTabModules / GetModules / UpdateModule /
//  DeleteModule). Module DELETE is a SOFT delete: the legacy DeleteModule path
//  (ModuleController.vb:L819-L821 -> DataProvider.DeleteModule stored proc) and
//  the cascade path (ModuleController.vb:L852 "objModule.IsDeleted = True") flag
//  the row rather than removing it. The delete test therefore asserts
//  EXCLUSION-FROM-LIST, not 404-on-get, because the single-entity read
//  (GET /{id}) is intentionally unfiltered by IsDeleted.
// =============================================================================

using System.Net;                            // HttpStatusCode
using System.Net.Http.Json;                  // PostAsJsonAsync, PutAsJsonAsync, ReadFromJsonAsync
using DnnMigration.Application.Common;       // ApiResponse<T> ({ data, meta } success envelope)
using DnnMigration.Application.DTOs.Module;  // ModuleDto, CreateModuleDto, UpdateModuleDto
using DnnMigration.IntegrationTests;         // CustomWebApplicationFactory
using FluentAssertions;                      // fluent assertion API
using Microsoft.AspNetCore.Mvc;              // ValidationProblemDetails (RFC 7807 error body)
using Xunit;                                 // Fact, Trait, IClassFixture

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for <c>ModulesController</c> (<c>/api/v1/modules</c>) exercised over real HTTP via
/// <see cref="CustomWebApplicationFactory"/>. Verifies the full Module CRUD contract — create/read/update,
/// the list-scope discriminator rule, the SOFT-delete semantics, validation failures and authentication —
/// against the genuine ASP.NET Core 8 pipeline backed by an EF Core InMemory store.
/// </summary>
/// <remarks>
/// The class is decorated with <c>[Trait("Category", "Integration")]</c> so it is selected by the Gate 5
/// command <c>dotnet test --filter Category=Integration</c>. It implements
/// <see cref="IClassFixture{TFixture}"/> over <see cref="CustomWebApplicationFactory"/>, so every test in the
/// class shares one in-process host and one uniquely-named InMemory database. Tests are self-contained: each
/// mutating test POSTs its own module (referencing the factory's seeded foreign keys) and reads the new
/// identifier back, so no test depends on another test's state or on a particular execution order.
/// </remarks>
[Trait("Category", "Integration")]
public class ModulesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Base route of the Modules REST resource.</summary>
    private const string ModulesRoute = "/api/v1/modules";

    /// <summary>The shared in-process API factory supplied by xUnit's class-fixture mechanism.</summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>Initializes the test class with the shared in-process API factory.</summary>
    /// <param name="factory">The xUnit class fixture that boots the real API host on an InMemory database.</param>
    public ModulesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// POST a valid module then GET it back: the create returns <c>201 Created</c> with a <c>Location</c>
    /// header and a positive generated identifier, and the subsequent read returns <c>200 OK</c> for the
    /// same identifier.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // POST a valid module referencing the seeded FK chain (portal 0, tab 1, module-def 1, desktop-module 1).
        var createResponse = await client.PostAsJsonAsync(ModulesRoute, BuildValidCreateModuleDto());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var createdEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createdEnvelope.Should().NotBeNull();
        createdEnvelope!.Data.Should().NotBeNull();

        var createdId = createdEnvelope.Data!.ModuleID;
        createdId.Should().BeGreaterThan(0);

        // GET the freshly-created module by its server-assigned identifier.
        var getResponse = await client.GetAsync($"{ModulesRoute}/{createdId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        getEnvelope.Should().NotBeNull();
        getEnvelope!.Data.Should().NotBeNull();
        getEnvelope.Data!.ModuleID.Should().Be(createdId);
    }

    /// <summary>
    /// The list endpoint requires exactly one scope discriminator: <c>?tabId=</c> and <c>?portalId=</c> each
    /// return <c>200 OK</c>, while supplying neither returns <c>400 Bad Request</c> (modules have no global list).
    /// </summary>
    [Fact]
    public async Task GetList_RequiresScope()
    {
        var client = _factory.CreateAuthenticatedClient();

        // ?tabId= -> GetByTab -> 200.
        var byTab = await client.GetAsync($"{ModulesRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");
        byTab.StatusCode.Should().Be(HttpStatusCode.OK);

        // ?portalId= -> GetByPortal -> 200 (portal 0 is a valid scope).
        var byPortal = await client.GetAsync($"{ModulesRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        byPortal.StatusCode.Should().Be(HttpStatusCode.OK);

        // Neither discriminator -> the controller cannot scope the list -> 400.
        var unscoped = await client.GetAsync(ModulesRoute);
        unscoped.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// PUT a module: the happy path (route id equal to <see cref="UpdateModuleDto.ModuleID"/>) returns
    /// <c>200 OK</c>, and the id-guard path (route id different from the body id) returns <c>400 Bad Request</c>.
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a module to update.
        var createResponse = await client.PostAsJsonAsync(ModulesRoute, BuildValidCreateModuleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createdEnvelope.Should().NotBeNull();
        createdEnvelope!.Data.Should().NotBeNull();
        var createdId = createdEnvelope.Data!.ModuleID;

        // Act + Assert (happy path): body ModuleID == route id -> 200.
        var update = new UpdateModuleDto
        {
            ModuleID = createdId,
            ModuleTitle = "Updated Module " + Guid.NewGuid().ToString("N"),
            ModuleOrder = 0,
            CacheTime = 0,
            PaneName = "ContentPane"
        };

        var updateResponse = await client.PutAsJsonAsync($"{ModulesRoute}/{createdId}", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedEnvelope = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        updatedEnvelope.Should().NotBeNull();
        updatedEnvelope!.Data.Should().NotBeNull();
        updatedEnvelope.Data!.ModuleID.Should().Be(createdId);

        // Act + Assert (id guard): route id != body ModuleID -> 400.
        var mismatch = new UpdateModuleDto
        {
            ModuleID = createdId + 1,
            ModuleTitle = "Mismatch Module " + Guid.NewGuid().ToString("N"),
            ModuleOrder = 0,
            CacheTime = 0,
            PaneName = "ContentPane"
        };

        var mismatchResponse = await client.PutAsJsonAsync($"{ModulesRoute}/{createdId}", mismatch);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// DELETE a module: the delete returns <c>204 No Content</c> and the module is then EXCLUDED from the
    /// tab list. This proves the delete is a SOFT delete — the row persists with <c>IsDeleted = true</c> and
    /// is filtered out of reads — rather than a physical removal (the single-entity read is unfiltered, so a
    /// 404-on-get would NOT prove soft-delete semantics).
    /// </summary>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a module placed on the seeded tab so it appears in the ?tabId= list.
        var createResponse = await client.PostAsJsonAsync(ModulesRoute, BuildValidCreateModuleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEnvelope = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createdEnvelope.Should().NotBeNull();
        createdEnvelope!.Data.Should().NotBeNull();
        var deletedId = createdEnvelope.Data!.ModuleID;
        deletedId.Should().BeGreaterThan(0);

        // Act: delete -> 204 (SOFT delete: the row is flagged IsDeleted = true, not physically removed).
        var deleteResponse = await client.DeleteAsync($"{ModulesRoute}/{deletedId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert: the soft-deleted module is EXCLUDED from the tab list (list reads filter out IsDeleted rows).
        var listResponse = await client.GetAsync($"{ModulesRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ModuleDto>>>();
        listEnvelope.Should().NotBeNull();
        listEnvelope!.Data.Should().NotBeNull();
        listEnvelope.Data!.Any(m => m.ModuleID == deletedId).Should().BeFalse();
    }

    /// <summary>Requesting a module that does not exist returns <c>404 Not Found</c>.</summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        // 999999 is far above any IDENTITY value the seed or these tests generate.
        var response = await client.GetAsync($"{ModulesRoute}/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// POSTing an invalid body (an empty <c>ModuleTitle</c>, violating <c>CreateModuleValidator</c>) returns
    /// <c>400 Bad Request</c> with an RFC 7807 Problem Details payload carrying a non-empty validation
    /// <c>errors</c> map.
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Violate CreateModuleValidator: an empty ModuleTitle fails the NotEmpty rule.
        var invalid = BuildValidCreateModuleDto();
        invalid.ModuleTitle = string.Empty;

        var response = await client.PostAsJsonAsync(ModulesRoute, invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // RFC 7807 Problem Details with a non-empty validation errors dictionary.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// A request without a Bearer token is rejected by the <c>[Authorize]</c> pipeline with
    /// <c>401 Unauthorized</c> (an authentication challenge), not <c>403 Forbidden</c>.
    /// </summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // A plain client carries no Authorization header, so the JWT Bearer pipeline issues a challenge.
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{ModulesRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Builds a fully-populated, valid <see cref="CreateModuleDto"/> that references the factory's seeded
    /// foreign keys (portal 0, tab 1, module-definition 1, desktop-module 1) and satisfies every
    /// <c>CreateModuleValidator</c> rule (non-empty <c>ModuleTitle</c>, non-negative <c>CacheTime</c>). The
    /// title is unique per call so modules seeded across tests never collide. <c>Visibility</c> is left at its
    /// default (<c>Maximized</c>); it is not validated and is therefore not set here.
    /// </summary>
    /// <returns>A valid create payload ready to POST to <c>/api/v1/modules</c>.</returns>
    private static CreateModuleDto BuildValidCreateModuleDto() => new()
    {
        PortalID = CustomWebApplicationFactory.DefaultPortalId,
        TabID = CustomWebApplicationFactory.SeededTabId,
        ModuleDefID = CustomWebApplicationFactory.SeededModuleDefId,
        DesktopModuleID = CustomWebApplicationFactory.SeededDesktopModuleId,
        ModuleTitle = "Integration Test Module " + Guid.NewGuid().ToString("N"),
        ModuleOrder = 0,
        CacheTime = 0,
        PaneName = "ContentPane"
    };
}
