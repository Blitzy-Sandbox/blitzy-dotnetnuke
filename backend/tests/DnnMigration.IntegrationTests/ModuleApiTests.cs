// -----------------------------------------------------------------------------
//  ModuleApiTests.cs
//
//  MIGRATION: Net-new xUnit integration tests for the Module REST resource
//  (/api/modules), verifying the migrated ASP.NET Core 8 Backend-for-Frontend
//  end-to-end against the in-memory API host booted by CustomWebApplicationFactory.
//  This satisfies the ModuleApiTests requirement of Validation Gate 5 (AAP
//  section 0.7.2): full CRUD (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204)
//  plus a not-found (404) check.
//
//  The legacy DotNetNuke 4.x solution shipped no automated tests; module settings
//  were verified manually through the ASP.NET Web Forms admin screens. REFERENCE
//  lineage (domain behavior / workflow only, both unmodified):
//    * Library/Components/Modules/ModuleController.vb   (module CRUD semantics)
//    * Website/admin/Modules/ModuleSettings.ascx.vb     (module settings save flow)
//  Those legacy behaviors are now realized by DnnMigration.Application ModuleService
//  + DnnMigration.Infrastructure ModuleRepository behind DnnMigration.Api's
//  ModulesController; these tests exercise that migrated surface over real HTTP.
//
//  All requests reach the class-level [Authorize] ModulesController without a real
//  login because CustomWebApplicationFactory registers a permissive test
//  authentication handler as the default scheme, and the SQL Server DnnDbContext is
//  swapped for EF Core InMemory, so the tests are deterministic and require no
//  external database.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end CRUD integration tests for the module REST resource
/// (<c>/api/modules</c>) exposed by <c>DnnMigration.Api.Controllers.ModulesController</c>.
/// </summary>
/// <remarks>
/// <para>
/// The class shares a single <see cref="CustomWebApplicationFactory"/> instance (and
/// therefore a single, uniquely named EF Core InMemory database) across its tests via
/// <see cref="IClassFixture{TFixture}"/>. Each request is issued through the factory's
/// <see cref="System.Net.Http.HttpClient"/>, whose default test authentication scheme
/// satisfies the controller's class-level <c>[Authorize]</c> requirement.
/// </para>
/// <para>
/// The primary-key <c>ModuleID</c> is server-generated
/// (<c>ValueGeneratedOnAdd()</c> honoured by the InMemory provider), so the create
/// test reads the assigned identifier back from the response envelope's
/// <c>data.moduleID</c> and reuses it to drive the read/update/delete steps rather
/// than assuming any particular value.
/// </para>
/// </remarks>
public class ModuleApiTests : IClassFixture<CustomWebApplicationFactory>
{
    /// <summary>
    /// The HTTP client bound to the in-memory API host. Created once per test-class
    /// fixture; the factory's test authentication handler authenticates every request
    /// as a synthetic administrator so <c>[Authorize]</c> endpoints are reachable.
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// The shared web-application factory. Retained (in addition to <see cref="_client"/>) so tests can
    /// open a DI scope against the SAME InMemory database the API host uses and assert directly on the
    /// persisted <c>[TabModules]</c> placement rows — there is no by-TabModule HTTP route, so the module
    /// placement side-effects (finding #5) are verified at the data layer through this factory.
    /// </summary>
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    /// JSON options used for request serialization and response deserialization.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> is enabled so the
    /// API's camelCase payload keys (for example <c>data</c>, <c>moduleID</c>,
    /// <c>moduleTitle</c>) bind onto the PascalCase read-model properties below. The
    /// <see cref="JsonStringEnumConverter"/> mirrors the API host's own JSON
    /// configuration so any string-encoded enum values round-trip identically.
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleApiTests"/> class.
    /// </summary>
    /// <param name="factory">
    /// The shared web-application factory that boots the API host in-process with an
    /// InMemory database and a permissive test authentication scheme.
    /// </param>
    public ModuleApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Verifies the full module CRUD lifecycle over HTTP: create (201) then read (200),
    /// update (200), delete (204), and finally confirm the resource is gone (404).
    /// This is the Module slice of Validation Gate 5.
    /// </summary>
    [Fact]
    public async Task Module_Crud_Lifecycle_Succeeds()
    {
        // MIGRATION (QA finding C): the create path now pre-validates the target tab, so seed tab 1 first.
        await EnsureTabAsync(tabId: 1, portalId: 0);

        // CREATE -> 201
        // MIGRATION: mirrors the "add module to page" save on the legacy
        // Website/admin/Modules/ModuleSettings.ascx.vb screen (ModuleController.AddModule).
        var createBody = new
        {
            portalID = 0,                    // PortalID >= 0 (DNN portals are zero-based)
            // MIGRATION: TabID MUST be > 0. The API host enables
            // AddFluentValidationAutoValidation(), and CreateModuleDtoValidator declares
            // RuleFor(x => x.TabID).GreaterThan(0) (mirroring the legacy Module Settings
            // screen, which always operated within a valid page context). A TabID of 0
            // would be rejected with HTTP 400 before persistence. The EF Core InMemory
            // provider does not enforce the TabID foreign key, so any positive value is
            // accepted for the module row this endpoint persists.
            tabID = 1,
            moduleDefID = 1,                 // MUST be > 0 (CreateModuleDtoValidator)
            moduleTitle = "Integration Test Module",
            paneName = "ContentPane",        // non-nullable => required
            moduleOrder = 1,
            cacheTime = 0,                   // >= 0
            alignment = "left",
            color = "",
            border = "",
            iconFile = "",
            allTabs = false,
            visibility = 0,                  // int on both DTO and entity; must be 0..2
            header = "",
            footer = "",
            containerSrc = "",
            displayTitle = true,
            displayPrint = false,
            displaySyndicate = false,
            inheritViewPermissions = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/modules", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<ModuleRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        created.Data!.ModuleID.Should().BeGreaterThan(0);
        created.Data.ModuleTitle.Should().Be("Integration Test Module");

        // MIGRATION: the created ModuleID is server-assigned by the InMemory provider
        // (ValueGeneratedOnAdd), so it is read from the POST envelope, never assumed.
        var id = created.Data.ModuleID;

        // READ -> 200
        var getResponse = await _client.GetAsync($"/api/modules/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<ModuleRead>>(JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Data.Should().NotBeNull();
        fetched.Data!.ModuleID.Should().Be(id);

        // UPDATE -> 200
        // MIGRATION: mirrors the ModuleSettings.ascx.vb "update settings" save
        // (ModuleController.UpdateModule). ModuleTitle is included because
        // UpdateModuleDto.ModuleTitle is a non-nullable string and UpdateModuleDtoValidator
        // requires it to be non-empty.
        var updateBody = new
        {
            moduleTitle = "Updated Module Title",
            paneName = "ContentPane",
            moduleOrder = 2,
            cacheTime = 60,                  // >= 0
            alignment = "center",
            color = "",
            border = "",
            iconFile = "",
            allTabs = false,
            visibility = 0,                  // must be 0..2
            header = "",
            footer = "",
            containerSrc = "",
            displayTitle = true,
            displayPrint = true,
            displaySyndicate = false,
            inheritViewPermissions = true
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/modules/{id}", updateBody, JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<Envelope<ModuleRead>>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Data.Should().NotBeNull();
        updated.Data!.ModuleTitle.Should().Be("Updated Module Title");

        // DELETE -> 204
        var deleteResponse = await _client.DeleteAsync($"/api/modules/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // VERIFY GONE -> 404
        var getAfterDelete = await _client.GetAsync($"/api/modules/{id}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that requesting a module whose identifier does not exist returns
    /// HTTP 404 (Not Found).
    /// </summary>
    [Fact]
    public async Task GetModule_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/modules/987654321");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// MIGRATION PARITY (finding #5): a DNN "create module" persists BOTH the <c>[Modules]</c> record and a
    /// <c>[TabModules]</c> placement row (legacy <c>DataProvider.AddTabModule</c>), and deleting the module
    /// cascades its placement rows (legacy <c>ON DELETE CASCADE</c>). There is no by-TabModule HTTP route,
    /// so this test drives the public <c>/api/modules</c> endpoints and then inspects the persisted
    /// <c>[TabModules]</c> rows directly through a DI scope on the shared InMemory database. It fails if the
    /// placement create or the delete cascade side-effect is dropped.
    /// </summary>
    [Fact]
    public async Task CreateModule_PersistsTabModulePlacement_AndDeleteCascadesIt()
    {
        // MIGRATION (QA finding C): the create path now pre-validates the target tab, so seed tab 77 first.
        await EnsureTabAsync(tabId: 77, portalId: 0);

        // CREATE a module placed on a single tab (AllTabs=false). Body mirrors the proven-valid CRUD shape
        // (so CreateModuleDtoValidator passes) but targets a distinct tab/pane/order to assert on.
        var createBody = new
        {
            portalID = 0,
            tabID = 77,
            moduleDefID = 1,
            moduleTitle = "Placement Parity Module",
            paneName = "RightPane",
            moduleOrder = 5,
            cacheTime = 0,
            alignment = "left",
            color = "",
            border = "",
            iconFile = "",
            allTabs = false,
            visibility = 0,
            header = "",
            footer = "",
            containerSrc = "",
            displayTitle = true,
            displayPrint = false,
            displaySyndicate = false,
            inheritViewPermissions = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/modules", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<ModuleRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        var moduleId = created.Data!.ModuleID;
        moduleId.Should().BeGreaterThan(0);

        // The placement row must exist in [TabModules] (module is placed, not created unplaced/invisible).
        var placements = await ReadTabModulesAsync(moduleId);
        placements.Should().ContainSingle("AllTabs=false places the module on exactly one tab");
        placements[0].TabID.Should().Be(77, "the placement must target the requested tab");
        placements[0].PaneName.Should().Be("RightPane");
        placements[0].ModuleOrder.Should().Be(5);

        // DELETE the module -> its placement rows must be cascaded away (InMemory has no referential cascade,
        // so this proves the service performs the cascade explicitly).
        var deleteResponse = await _client.DeleteAsync($"/api/modules/{moduleId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await ReadTabModulesAsync(moduleId);
        afterDelete.Should().BeEmpty("deleting the module must cascade-delete its TabModules placement rows");
    }

    /// <summary>
    /// MIGRATION (QA finding C) — ATOMIC CREATE: a create request naming a NON-EXISTENT target tab must be
    /// rejected with HTTP 409 (RFC 7807 Problem Details) and leave NO <c>[Modules]</c> row behind. This is the
    /// end-to-end reproduction of the reviewer's defect ("tabID=999999 -> 500 + orphaned Modules row"). The
    /// tab (999999) is deliberately NOT seeded, so the service's up-front tab pre-validation trips.
    /// </summary>
    [Fact]
    public async Task CreateModule_WithNonExistentTab_Returns409_AndCreatesNoModuleRow()
    {
        var modulesBefore = await CountModulesAsync();

        // TabID 999999 is > 0 (so CreateModuleDtoValidator passes) but no such tab exists.
        var createBody = new
        {
            portalID = 0,
            tabID = 999999,
            moduleDefID = 1,
            moduleTitle = "Orphan Attempt Module",
            paneName = "ContentPane",
            moduleOrder = 1,
            cacheTime = 0,
            alignment = "left",
            color = "",
            border = "",
            iconFile = "",
            allTabs = false,
            visibility = 0,
            header = "",
            footer = "",
            containerSrc = "",
            displayTitle = true,
            displayPrint = false,
            displaySyndicate = false,
            inheritViewPermissions = true
        };

        var response = await _client.PostAsJsonAsync("/api/modules", createBody, JsonOptions);

        // 409 Conflict, NOT 500, and NOT 201.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "creating a module on a non-existent tab must surface as a clean 409, never a raw 500");

        // RFC 7807 Problem Details body.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        // ATOMICITY: no orphaned [Modules] row was left behind.
        var modulesAfter = await CountModulesAsync();
        modulesAfter.Should().Be(modulesBefore,
            "a failed create must not persist an orphaned Modules row (the create is atomic)");
    }

    /// <summary>
    /// MIGRATION (QA finding I) — READ FIDELITY: a module GET must round-trip its full denormalized field
    /// set, not just <c>moduleTitle</c>. The Domain <c>Module</c> is the legacy <c>[vw_Modules]</c> shape
    /// (M.* + TM.* placement + DM.* definition lookup); only the 11 M.* columns are physically on
    /// <c>[Modules]</c>, so the placement (TabID/PaneName/ModuleOrder/CacheTime/Visibility/Display*) and the
    /// desktop-module lookup (DesktopModuleID/FriendlyName/FolderName/Description/Version/ModuleName) must be
    /// hydrated on the read path. This test seeds the definition + desktop-module registration, creates a
    /// module placed on a seeded tab, then GETs it and asserts every hydrated carrier survives the round-trip.
    /// </summary>
    [Fact]
    public async Task GetModule_AfterCreate_RoundTripsPlacementAndDefinitionLookup()
    {
        const int tabId = 4242;
        const int moduleDefId = 4243;
        const int desktopModuleId = 4244;

        // Seed the target tab, the definition, and the desktop-module registration the module resolves through.
        await SeedDefinitionGraphAsync(tabId, moduleDefId, desktopModuleId);

        var createBody = new
        {
            portalID = 0,
            tabID = tabId,
            moduleDefID = moduleDefId,
            moduleTitle = "Round-Trip Module",
            paneName = "RightPane",
            moduleOrder = 9,
            cacheTime = 120,
            alignment = "center",
            color = "",
            border = "",
            iconFile = "",
            allTabs = false,
            visibility = 2,
            header = "",
            footer = "",
            containerSrc = "",
            displayTitle = true,
            displayPrint = true,
            displaySyndicate = true,
            inheritViewPermissions = true
        };

        var createResponse = await _client.PostAsJsonAsync("/api/modules", createBody, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<ModuleFullRead>>(JsonOptions);
        var moduleId = created!.Data!.ModuleID;
        moduleId.Should().BeGreaterThan(0);

        // READ -> the hydrated read path must surface placement + definition lookup, not CLR defaults.
        var getResponse = await _client.GetAsync($"/api/modules/{moduleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<Envelope<ModuleFullRead>>(JsonOptions);
        var m = fetched!.Data!;

        // Own [Modules] columns.
        m.ModuleID.Should().Be(moduleId);
        m.ModuleTitle.Should().Be("Round-Trip Module");
        m.ModuleDefID.Should().Be(moduleDefId);

        // Placement (TM.*) carriers — previously defaulted to zero/empty (the reviewer's defect).
        m.TabID.Should().Be(tabId, "the placement tab must be hydrated from [TabModules]");
        m.TabModuleID.Should().BeGreaterThan(0, "the placement surrogate id must be hydrated");
        m.PaneName.Should().Be("RightPane");
        m.ModuleOrder.Should().Be(9);
        m.CacheTime.Should().Be(120);
        m.Visibility.Should().Be(2);
        m.DisplayTitle.Should().BeTrue();
        m.DisplayPrint.Should().BeTrue();
        m.DisplaySyndicate.Should().BeTrue();

        // Definition/desktop-module (DM.*) lookup carriers — previously defaulted to empty.
        m.DesktopModuleID.Should().Be(desktopModuleId, "the desktop-module id must be resolved via the definition");
        m.FriendlyName.Should().Be("Round-Trip Friendly");
        m.FolderName.Should().Be("RoundTripFolder");
        m.Description.Should().Be("Round-trip description");
        m.Version.Should().Be("9.9.9");
        m.ModuleName.Should().Be("RoundTripModuleName");

        // MIGRATION (QA finding I): ControlSrc has NO backing entity in scope (ModuleControls was not
        // modelled), so it intentionally remains empty. Documented in MIGRATION_NOTES.md.
        m.ControlSrc.Should().BeEmpty("ControlSrc has no backing ModuleControls entity in scope (documented limitation)");
    }

    /// <summary>
    /// Seeds the <c>[Tabs]</c>, <c>[ModuleDefinitions]</c> and <c>[DesktopModules]</c> rows a module resolves
    /// through, so the read-path hydration (QA finding I) has definition data to project.
    /// </summary>
    private async Task SeedDefinitionGraphAsync(int tabId, int moduleDefId, int desktopModuleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        if (!await db.Tabs.AsNoTracking().AnyAsync(t => t.TabID == tabId))
        {
            db.Tabs.Add(new Tab { TabID = tabId, PortalID = 0, TabName = $"Tab {tabId}" });
        }

        if (!await db.DesktopModules.AsNoTracking().AnyAsync(d => d.DesktopModuleID == desktopModuleId))
        {
            db.DesktopModules.Add(new DesktopModule
            {
                DesktopModuleID = desktopModuleId,
                FriendlyName = "Round-Trip Friendly",
                FolderName = "RoundTripFolder",
                Description = "Round-trip description",
                Version = "9.9.9",
                ModuleName = "RoundTripModuleName",
                BusinessControllerClass = string.Empty,
                CompatibleVersions = string.Empty,
                Dependencies = string.Empty,
                Permissions = string.Empty
            });
        }

        if (!await db.ModuleDefinitions.AsNoTracking().AnyAsync(md => md.ModuleDefID == moduleDefId))
        {
            db.ModuleDefinitions.Add(new ModuleDefinition
            {
                ModuleDefID = moduleDefId,
                DesktopModuleID = desktopModuleId,
                FriendlyName = "Round-Trip Friendly",
                DefaultCacheTime = 0
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Counts the persisted <c>[Modules]</c> rows directly from the shared InMemory store.</summary>
    private async Task<int> CountModulesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        return await db.Modules.AsNoTracking().CountAsync();
    }

    /// <summary>
    /// Reads the persisted <c>[TabModules]</c> rows for a module directly from the InMemory database the API
    /// host uses, by opening a DI scope on the shared factory. Used to assert placement side-effects that
    /// have no dedicated HTTP route.
    /// </summary>
    private async Task<List<TabModule>> ReadTabModulesAsync(int moduleId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        return await db.TabModules.AsNoTracking()
            .Where(tm => tm.ModuleID == moduleId)
            .ToListAsync();
    }

    /// <summary>
    /// Idempotently seeds a <c>[Tabs]</c> row directly into the shared InMemory database so a module can be
    /// placed on it.
    /// </summary>
    /// <remarks>
    /// MIGRATION (QA finding C): creating a module now PRE-VALIDATES its target tab — the service reads the
    /// tab via <c>ITabRepository.GetByIdAsync</c> and throws a <c>ConflictException</c> (HTTP 409) if it does
    /// not exist, so a module can no longer be placed on a phantom tab (the reviewer's "tabID=999999 -> 500 +
    /// orphaned Modules row" defect). The pre-existing CRUD/placement tests therefore seed their target tab
    /// first, mirroring the legacy Module Settings screen, which always operated within a valid page context.
    /// The check-then-add keeps the helper safe to call more than once against the class's shared store.
    /// </remarks>
    private async Task EnsureTabAsync(int tabId, int portalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var exists = await db.Tabs.AsNoTracking().AnyAsync(t => t.TabID == tabId);
        if (!exists)
        {
            db.Tabs.Add(new Tab { TabID = tabId, PortalID = portalId, TabName = $"Tab {tabId}" });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Minimal read model for the API success envelope
    /// (<c>{ "data": ..., "meta": ... }</c>) shaped by
    /// <c>DnnMigration.Api.Controllers.ApiControllerBase</c>. Only the <c>data</c>
    /// payload is strongly typed; <c>meta</c> is captured as a raw
    /// <see cref="JsonElement"/> because these tests do not assert on it.
    /// </summary>
    /// <typeparam name="T">The payload type carried under the <c>data</c> key.</typeparam>
    private sealed class Envelope<T>
    {
        /// <summary>The response payload projected from the <c>data</c> key.</summary>
        public T? Data { get; set; }

        /// <summary>The raw response metadata from the <c>meta</c> key.</summary>
        public JsonElement Meta { get; set; }
    }

    /// <summary>
    /// Minimal projection of the module payload returned by the API. Only the fields
    /// these tests assert on are declared; the reference-type property is nullable so
    /// the read model never triggers CS8618 under the Gate 1 warnings-as-errors build.
    /// </summary>
    private sealed class ModuleRead
    {
        /// <summary>The server-assigned module identifier (serialized as <c>moduleID</c>).</summary>
        public int ModuleID { get; set; }

        /// <summary>The module display title (serialized as <c>moduleTitle</c>).</summary>
        public string? ModuleTitle { get; set; }
    }

    /// <summary>
    /// Richer read model used by the QA-finding-I round-trip test. Declares the placement (TM.*) and
    /// definition/desktop-module (DM.*) carriers whose hydration is under test, in addition to the module's
    /// own columns. Reference-type members are nullable so the model never triggers CS8618 under the Gate 1
    /// warnings-as-errors build.
    /// </summary>
    private sealed class ModuleFullRead
    {
        public int ModuleID { get; set; }
        public int ModuleDefID { get; set; }
        public string? ModuleTitle { get; set; }

        // Placement (TM.*) carriers.
        public int TabID { get; set; }
        public int TabModuleID { get; set; }
        public string? PaneName { get; set; }
        public int ModuleOrder { get; set; }
        public int CacheTime { get; set; }
        public int Visibility { get; set; }
        public bool DisplayTitle { get; set; }
        public bool DisplayPrint { get; set; }
        public bool DisplaySyndicate { get; set; }

        // Definition / desktop-module (DM.*) lookup carriers.
        public int DesktopModuleID { get; set; }
        public string? FriendlyName { get; set; }
        public string? FolderName { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? ModuleName { get; set; }
        public string? ControlSrc { get; set; }
    }
}
