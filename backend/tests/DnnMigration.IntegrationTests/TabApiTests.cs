// -----------------------------------------------------------------------------
//  TabApiTests
//
//  End-to-end CRUD integration tests for the tab (portal page) REST resource
//  (/api/tabs) exposed by DnnMigration.Api.Controllers.TabsController, executed
//  against the in-memory API host booted by CustomWebApplicationFactory.
//
//  Beyond the Validation-Gate-5 CRUD lifecycle (POST -> 201, GET -> 200,
//  PUT -> 200, DELETE -> 204), these tests specifically pin the hierarchy
//  behaviour restored for review finding #6:
//    * page-path (TabPath) + depth (Level) generation on create, and
//    * child-path CASCADE on parent rename (a descendant's TabPath tracks the
//      renamed ancestor).
//  TabDto exposes TabPath + Level, so both are asserted directly from the JSON
//  response envelope without any database inspection.
//
//  MIGRATION: the legacy DNN 4.x solution shipped no automated tests; these are
//  net-new and keep Validation Gate 5 (full CRUD) green for the tab resource.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for <c>/api/tabs</c>. The class shares a single
/// <see cref="CustomWebApplicationFactory"/> (and therefore one uniquely named EF Core InMemory
/// database) via <see cref="IClassFixture{TFixture}"/>; each request is issued through the factory's
/// <see cref="System.Net.Http.HttpClient"/>, whose default test authentication scheme authenticates
/// as a synthetic super-user so the controller's <c>[Authorize]</c> + portal-scoping checks pass.
/// </summary>
public class TabApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    /// <summary>
    /// JSON options mirroring the API host: case-insensitive property binding (camelCase payload keys
    /// bind onto the PascalCase read models) and string-encoded enum round-tripping.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TabApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Verifies the full tab CRUD lifecycle over HTTP: create a root tab (201) — asserting the generated
    /// path/level — then read (200), update/rename (200) — asserting the recomputed path — delete (204),
    /// and finally confirm the resource is gone (404). This is the Tab slice of Validation Gate 5.
    /// </summary>
    [Fact]
    public async Task Tab_Crud_Lifecycle_Succeeds()
    {
        // CREATE a root tab -> 201. A root tab's server-derived path is "//" + CleanName(name), depth 0.
        var createBody = new
        {
            portalID = 0,
            tabName = "Lifecycle Home",
            parentId = 0,
            tabOrder = 1,
            isVisible = true,
            title = "Lifecycle Home",
            description = "",
            keyWords = "",
            url = "",
            skinSrc = "",
            containerSrc = "",
            refreshInterval = 0,
            pageHeadText = "",
            isSecure = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tabs", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        created!.Data.Should().NotBeNull();
        var id = created.Data!.TabID;
        id.Should().BeGreaterThan(0);
        created.Data.TabPath.Should().Be("//LifecycleHome", "a root tab path is '//' + CleanName(TabName)");
        created.Data.Level.Should().Be(0);

        // READ -> 200
        var getResponse = await _client.GetAsync($"/api/tabs/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        fetched!.Data!.TabID.Should().Be(id);

        // UPDATE (rename) -> 200; the path must be recomputed from the new name.
        var updateBody = new
        {
            tabName = "Lifecycle Renamed",
            parentId = 0,
            tabOrder = 2,
            isVisible = true,
            title = "Lifecycle Renamed",
            description = "",
            keyWords = "",
            url = "",
            skinSrc = "",
            containerSrc = "",
            refreshInterval = 0,
            pageHeadText = "",
            isSecure = false
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/tabs/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        updated!.Data!.TabName.Should().Be("Lifecycle Renamed");
        updated.Data.TabPath.Should().Be("//LifecycleRenamed", "a renamed root tab's path is recomputed");

        // DELETE -> 204 (leaf tab, no children -> deletable), then GET -> 404.
        var deleteResponse = await _client.DeleteAsync($"/api/tabs/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getAfterDelete = await _client.GetAsync($"/api/tabs/{id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// MIGRATION PARITY (finding #6): a child tab inherits its parent's page path + depth, and renaming the
    /// parent CASCADES the recomputed path down to the child. Drives only the public /api/tabs endpoints and
    /// asserts the child's TabPath/Level from the response envelope. Fails if page-path generation or the
    /// child-path cascade is dropped.
    /// </summary>
    [Fact]
    public async Task CreateChildTab_InheritsParentPath_AndParentRenameCascadesToChild()
    {
        // CREATE parent (root) "Docs" -> path "//Docs", level 0.
        var parent = await CreateTabAsync(portalId: 0, tabName: "Docs", parentId: 0);
        parent.TabPath.Should().Be("//Docs");
        parent.Level.Should().Be(0);

        // CREATE child "Guide" under the parent -> path "//Docs//Guide", level 1.
        var child = await CreateTabAsync(portalId: 0, tabName: "Guide", parentId: parent.TabID);
        child.TabPath.Should().Be("//Docs//Guide", "a child inherits parent.TabPath + '//' + CleanName(name)");
        child.Level.Should().Be(1, "a child sits one level below its parent");

        // RENAME the parent -> its own path is recomputed AND the child path must cascade.
        var renameBody = new
        {
            tabName = "Documentation",
            parentId = 0,
            tabOrder = 1,
            isVisible = true,
            title = "Documentation",
            description = "",
            keyWords = "",
            url = "",
            skinSrc = "",
            containerSrc = "",
            refreshInterval = 0,
            pageHeadText = "",
            isSecure = false
        };
        var renameResponse = await _client.PutAsJsonAsync($"/api/tabs/{parent.TabID}", renameBody, JsonOptions);
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await renameResponse.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        renamed!.Data!.TabPath.Should().Be("//Documentation");

        // GET the child back -> its path must now track the renamed parent (cascade proof).
        var childAfter = await _client.GetAsync($"/api/tabs/{child.TabID}");
        childAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var childRead = await childAfter.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        childRead!.Data!.TabPath.Should().Be(
            "//Documentation//Guide",
            "renaming the parent must cascade the recomputed path to the child");
        childRead.Data.Level.Should().Be(1);
    }

    /// <summary>
    /// POSTs a create request and returns the created tab projection from the 201 envelope.
    /// </summary>
    private async Task<TabRead> CreateTabAsync(int portalId, string tabName, int parentId)
    {
        var body = new
        {
            portalID = portalId,
            tabName,
            parentId,
            tabOrder = 1,
            isVisible = true,
            title = tabName,
            description = "",
            keyWords = "",
            url = "",
            skinSrc = "",
            containerSrc = "",
            refreshInterval = 0,
            pageHeadText = "",
            isSecure = false
        };

        var response = await _client.PostAsJsonAsync("/api/tabs", body, JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TabRead>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        return envelope.Data!;
    }

    /// <summary>
    /// Minimal read model for the API success envelope (<c>{ "data": ..., "meta": ... }</c>). Only the
    /// <c>data</c> payload is strongly typed; <c>meta</c> is captured as a raw <see cref="JsonElement"/>.
    /// </summary>
    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    /// <summary>
    /// Minimal projection of the tab payload returned by the API — only the fields these tests assert on.
    /// Reference-type members are non-null with defaults so the read model never triggers CS8618 under the
    /// Gate 1 warnings-as-errors build.
    /// </summary>
    private sealed class TabRead
    {
        public int TabID { get; set; }
        public string TabName { get; set; } = string.Empty;
        public string TabPath { get; set; } = string.Empty;
        public int Level { get; set; }
        public int ParentId { get; set; }
        public int TabOrder { get; set; }
        public bool IsVisible { get; set; }
    }
}
