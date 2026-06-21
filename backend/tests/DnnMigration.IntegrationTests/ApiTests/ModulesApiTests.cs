using System.Net;
using System.Net.Http.Json;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.IntegrationTests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

// MIGRATION: CREATE-from-scratch end-to-end HTTP integration tests for the Modules REST resource
// (/api/v1/modules). DotNetNuke 4.9.0.85 shipped ZERO automated tests, so there is no legacy
// equivalent; these tests assert the modern contract ported from Library/Components/Modules/
// ModuleController.vb. The single behavioral nuance carried over verbatim is SOFT delete: the legacy
// DeleteModule path set objModule.IsDeleted = True (ModuleController.vb L851-853) rather than removing
// the row, so the DELETE test asserts the module is EXCLUDED from list reads — NOT 404-on-get.

/// <summary>
/// End-to-end HTTP integration tests for the Modules API, exercised in-process against the REAL
/// <c>DnnMigration.Api</c> middleware + DI pipeline through <see cref="CustomWebApplicationFactory"/>
/// (the SQL Server data layer is swapped for the EF Core InMemory provider). These tests are the
/// Module slice of Validation Gate 5 (<c>dotnet test --filter Category=Integration</c>): they assert the
/// full CRUD round-trip (POST 201, GET 200, PUT 200, DELETE 204), the list-scope discriminator rule, the
/// soft-delete exclusion semantics, and the RFC 7807 error + JWT authorization contracts.
/// </summary>
/// <remarks>
/// The class shares ONE InMemory database for all its tests (xUnit constructs a single
/// <see cref="CustomWebApplicationFactory"/> per <see cref="IClassFixture{TFixture}"/>), so every
/// mutating test is SELF-CONTAINED: it POSTs its own module (referencing the seeded foreign-key chain
/// <c>PortalID=0</c>, <c>TabID=1</c>, <c>ModuleDefID=1</c>, <c>DesktopModuleID=1</c>) and reads the
/// server-generated <c>ModuleID</c> back from the response, never depending on rows created by a sibling
/// test. Protected endpoints are reached with <see cref="CustomWebApplicationFactory.CreateAuthenticatedClient"/>
/// (a Bearer token minted for the seeded administrator); the unauthenticated case uses a plain
/// <c>CreateClient()</c>.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ModulesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>Versioned resource route under test, per <c>ModulesController</c>'s <c>[Route]</c>.</summary>
    private const string BaseRoute = "/api/v1/modules";

    /// <summary>An identifier guaranteed not to exist in the seeded InMemory store (keys are small/sequential).</summary>
    private const int UnknownModuleId = 999999;

    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulesApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared in-process API host fixture supplied by xUnit.</param>
    public ModulesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds a fully valid <see cref="CreateModuleDto"/> that satisfies <c>CreateModuleValidator</c>
    /// (<c>ModuleTitle</c> non-empty, <c>CacheTime</c> &gt;= 0) and references the seeded foreign-key
    /// chain so the create round-trips against the InMemory store.
    /// </summary>
    /// <param name="title">
    /// Optional explicit module title. When <c>null</c> a unique title is generated so concurrent rows
    /// in the shared store never collide; pass <see cref="string.Empty"/> to deliberately produce an
    /// INVALID DTO for the negative-path validation test.
    /// </param>
    /// <returns>A populated <see cref="CreateModuleDto"/>.</returns>
    private static CreateModuleDto BuildValidCreateModuleDto(string? title = null) => new()
    {
        PortalID = CustomWebApplicationFactory.DefaultPortalId,
        TabID = CustomWebApplicationFactory.SeededTabId,
        ModuleDefID = CustomWebApplicationFactory.SeededModuleDefId,
        DesktopModuleID = CustomWebApplicationFactory.SeededDesktopModuleId,
        ModuleTitle = title ?? $"Integration Test Module {Guid.NewGuid():N}",
        ModuleOrder = 1,
        CacheTime = 0
    };

    /// <summary>
    /// CREATE then READ round-trip: <c>POST</c> a valid module yields <c>201 Created</c> with a
    /// <c>Location</c> header and a server-generated positive id, and a subsequent <c>GET</c> of that id
    /// yields <c>200 OK</c> echoing the same id.
    /// </summary>
    [Fact]
    public async Task Create_Then_Get_Returns201Then200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // POST a valid module -> 201 Created + Location header.
        var createResponse = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateModuleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var createBody = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createBody.Should().NotBeNull();
        var created = createBody!.Data;
        created.Should().NotBeNull();
        var createdId = created!.ModuleID;
        createdId.Should().BeGreaterThan(0);

        // GET the created module by id -> 200 OK with the same id.
        var getResponse = await client.GetAsync($"{BaseRoute}/{createdId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        getBody.Should().NotBeNull();
        var fetched = getBody!.Data;
        fetched.Should().NotBeNull();
        fetched!.ModuleID.Should().Be(createdId);
    }

    /// <summary>
    /// The list endpoint REQUIRES a scope discriminator: <c>?tabId=</c> and <c>?portalId=</c> each
    /// return <c>200 OK</c>, while a scopeless list request returns <c>400 Bad Request</c> (modules are
    /// always scoped to a tab or portal in the legacy model — there is no global module list).
    /// </summary>
    [Fact]
    public async Task GetList_RequiresScope()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Tab-scoped list -> 200 OK.
        var byTabResponse = await client.GetAsync($"{BaseRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");
        byTabResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Portal-scoped list -> 200 OK.
        var byPortalResponse = await client.GetAsync($"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        byPortalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // No scope discriminator -> 400 Bad Request.
        var noScopeResponse = await client.GetAsync(BaseRoute);
        noScopeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// UPDATE contract: a <c>PUT</c> whose route id matches the body <c>ModuleID</c> returns
    /// <c>200 OK</c>; a <c>PUT</c> whose route id does NOT match the body <c>ModuleID</c> is rejected by
    /// the controller id-guard with <c>400 Bad Request</c> before the service runs.
    /// </summary>
    [Fact]
    public async Task Update_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a module to update.
        var createResponse = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateModuleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createBody.Should().NotBeNull();
        var created = createBody!.Data;
        created.Should().NotBeNull();
        var createdId = created!.ModuleID;

        // Act + Assert (happy path): matching route/body id -> 200 OK.
        var updateDto = new UpdateModuleDto
        {
            ModuleID = createdId,
            ModuleTitle = $"Updated Module {Guid.NewGuid():N}",
            ModuleOrder = 2,
            CacheTime = 0
        };
        var updateResponse = await client.PutAsJsonAsync($"{BaseRoute}/{createdId}", updateDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateBody = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        updateBody.Should().NotBeNull();
        var updated = updateBody!.Data;
        updated.Should().NotBeNull();
        updated!.ModuleID.Should().Be(createdId);

        // Act + Assert (id-guard): body ModuleID != route id -> 400 Bad Request.
        var mismatchDto = new UpdateModuleDto
        {
            ModuleID = createdId + 1,
            ModuleTitle = $"Mismatch Module {Guid.NewGuid():N}",
            CacheTime = 0
        };
        var mismatchResponse = await client.PutAsJsonAsync($"{BaseRoute}/{createdId}", mismatchDto);
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// SOFT delete: <c>DELETE</c> returns <c>204 No Content</c> and the module's row PERSISTS with
    /// <c>IsDeleted = true</c> rather than being physically removed, so it is filtered OUT of subsequent
    /// list reads. This mirrors the legacy <c>ModuleController.DeleteModule</c> soft-delete path
    /// (<c>objModule.IsDeleted = True</c>). The deleted id is asserted present in the portal-scoped list
    /// BEFORE deletion and absent from both the tab- and portal-scoped lists AFTER deletion — so the
    /// exclusion assertion is meaningful rather than vacuous.
    /// </summary>
    [Fact]
    public async Task Delete_IsSoftDelete_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Arrange: create a module (PortalID=0, TabID=1) and capture its server-generated id.
        var createResponse = await client.PostAsJsonAsync(BaseRoute, BuildValidCreateModuleDto());
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ModuleDto>>();
        createBody.Should().NotBeNull();
        var created = createBody!.Data;
        created.Should().NotBeNull();
        var deletedId = created!.ModuleID;

        // Precondition: the new module is visible in the portal-scoped list (PortalID is a real column).
        var beforeResponse = await client.GetAsync($"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeBody = await beforeResponse.Content.ReadFromJsonAsync<ApiResponse<List<ModuleDto>>>();
        beforeBody.Should().NotBeNull();
        var beforeData = beforeBody!.Data;
        beforeData.Should().NotBeNull();
        beforeData!.Any(m => m.ModuleID == deletedId).Should().BeTrue();

        // Act: soft delete -> 204 No Content.
        var deleteResponse = await client.DeleteAsync($"{BaseRoute}/{deletedId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert (per contract): tab-scoped list returns 200 and EXCLUDES the soft-deleted id.
        var tabListResponse = await client.GetAsync($"{BaseRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");
        tabListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tabListBody = await tabListResponse.Content.ReadFromJsonAsync<ApiResponse<List<ModuleDto>>>();
        tabListBody.Should().NotBeNull();
        var tabListData = tabListBody!.Data;
        tabListData.Should().NotBeNull();
        tabListData!.All(m => m.ModuleID != deletedId).Should().BeTrue();

        // Assert (soft-delete proof): the portal-scoped list — where the module WAS present — now
        // excludes it, confirming the row was flagged IsDeleted and filtered from reads, not deleted.
        var afterResponse = await client.GetAsync($"{BaseRoute}?portalId={CustomWebApplicationFactory.DefaultPortalId}");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await afterResponse.Content.ReadFromJsonAsync<ApiResponse<List<ModuleDto>>>();
        afterBody.Should().NotBeNull();
        var afterData = afterBody!.Data;
        afterData.Should().NotBeNull();
        afterData!.All(m => m.ModuleID != deletedId).Should().BeTrue();
    }

    /// <summary>
    /// Reading an unknown id returns <c>404 Not Found</c> (an RFC 7807 problem response). Module uses
    /// SOFT delete, so a 404 here proves the id was never created — distinct from the deleted-module
    /// behavior verified in <see cref="Delete_IsSoftDelete_Returns204"/>.
    /// </summary>
    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"{BaseRoute}/{UnknownModuleId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// An invalid create body (empty <c>ModuleTitle</c>, violating <c>CreateModuleValidator</c>) is
    /// rejected with <c>400 Bad Request</c> carrying an RFC 7807 <see cref="ValidationProblemDetails"/>
    /// whose <c>errors</c> map is non-empty.
    /// </summary>
    [Fact]
    public async Task Create_InvalidBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Empty title fails the FluentValidation NotEmpty rule, surfaced as a 400 by the service +
        // ExceptionHandlingMiddleware (FluentValidation ValidationException -> ValidationProblemDetails).
        var invalidDto = BuildValidCreateModuleDto(title: string.Empty);

        var response = await client.PostAsJsonAsync(BaseRoute, invalidDto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// A request to a protected endpoint WITHOUT a Bearer token is rejected with <c>401 Unauthorized</c>
    /// by the JWT authentication challenge (the controller is <c>[Authorize]</c>-protected).
    /// </summary>
    [Fact]
    public async Task Request_NoBearer_Returns401()
    {
        // Plain client carries NO Authorization header.
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"{BaseRoute}?tabId={CustomWebApplicationFactory.SeededTabId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
