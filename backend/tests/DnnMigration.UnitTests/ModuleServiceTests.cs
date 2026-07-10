// -----------------------------------------------------------------------------
//  ModuleServiceTests.cs
//
//  xUnit unit tests for DnnMigration.Application.Services.ModuleService — the
//  application service that carries the module-management business surface
//  migrated from the legacy DotNetNuke VB.NET ModuleController
//  (Library/Components/Modules/ModuleController.vb: AddModule L645, DeleteModule
//  L819, GetModule L885, GetModules L915, GetModuleByDefinition L955,
//  UpdateModule L1095) and ModuleInfo (Library/Components/Modules/ModuleInfo.vb).
//
//  Test strategy (matches the sibling *ServiceTests suites):
//    * The data-access collaborator IModuleRepository is REPLACED with a Moq
//      test double so the service is exercised in complete isolation from EF
//      Core / the database. Every repository member is stubbed and the
//      service-to-repository interaction (arguments + call counts) is asserted
//      with Moq's Verify.
//    * The IMapper collaborator is REAL: it is built from the production
//      AutoMapper profile (DnnMigration.Application.Mapping.MappingProfile) so
//      the tests exercise the genuine entity <-> DTO projection rather than a
//      hand-rolled fake. This proves that ModuleService + MappingProfile agree
//      on the Module/ModuleDto/CreateModuleDto/UpdateModuleDto shapes.
//
//  Coverage: all seven IModuleService methods — GetAllAsync, GetByPortalAsync,
//  GetByIdAsync, GetByDefinitionAsync, CreateAsync, UpdateAsync, DeleteAsync —
//  including the null / not-found branches of the read, update and delete paths
//  and the int passthrough of the legacy VisibilityState value.
//
//  MIGRATION: the legacy DNN 4.x solution shipped no automated tests; module
//  behaviour was verified manually against the Web Forms admin screens. This
//  net-new suite pins the migrated behaviour so Validation Gate 2
//  (`dotnet test -c Release`, 100% pass) stays green.
// -----------------------------------------------------------------------------

using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Exceptions;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="ModuleService"/>. The service is constructed with a mocked
/// <see cref="IModuleRepository"/> (so no database is touched) and a real
/// <see cref="IMapper"/> built from the production <see cref="MappingProfile"/>.
/// </summary>
public class ModuleServiceTests
{
    /// <summary>
    /// The mocked data-access port. xUnit creates a fresh test-class instance per test method,
    /// so each test gets its own clean mock with no cross-test state.
    /// </summary>
    private readonly Mock<IModuleRepository> _repository;

    /// <summary>
    /// The mocked tab-access port. Injected into <see cref="ModuleService"/> so the <c>AllTabs</c>
    /// placement fan-out (create a placement row for every portal tab) can be exercised without a database.
    /// </summary>
    private readonly Mock<ITabRepository> _tabRepository;

    /// <summary>The real AutoMapper instance, built once per test from <see cref="MappingProfile"/>.</summary>
    private readonly IMapper _mapper;

    /// <summary>System under test.</summary>
    private readonly ModuleService _sut;

    /// <summary>
    /// Builds the collaborators and the system under test. The mapper is intentionally the
    /// production profile (not a stub) so entity/DTO projection is exercised for real; only the
    /// repository is mocked. (Configuration is not asserted valid here because
    /// <see cref="MappingProfile"/> contains maps for other aggregates whose members are outside
    /// this suite's scope; the profile's own validity is covered by MappingProfileTests.)
    /// </summary>
    public ModuleServiceTests()
    {
        _repository = new Mock<IModuleRepository>();
        _tabRepository = new Mock<ITabRepository>();

        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();

        _sut = new ModuleService(_repository.Object, _tabRepository.Object, _mapper);
    }

    // =========================================================================
    //  Test-data factories
    //
    //  Small builders keep each test focused on the single behaviour under test.
    //  All non-supplied fields default to the C# defaults, which is sufficient
    //  because the tests only assert the fields they deliberately set.
    // =========================================================================

    /// <summary>
    /// Creates a <see cref="Module"/> domain entity with the fields the read tests assert.
    /// <paramref name="visibility"/> is the legacy <c>VisibilityState</c> value preserved as an
    /// <see cref="int"/> (0 = Maximized, 1 = Minimized, 2 = None).
    /// </summary>
    private static Module NewModule(
        int moduleId = 1,
        int portalId = 0,
        int tabId = 1,
        string moduleTitle = "Module",
        int visibility = 0) => new()
        {
            ModuleID = moduleId,
            PortalID = portalId,
            TabID = tabId,
            ModuleTitle = moduleTitle,
            Visibility = visibility,
            ModuleDefID = 1
        };

    /// <summary>
    /// Creates a valid <see cref="CreateModuleDto"/> (positive <c>ModuleDefID</c>, non-negative
    /// <c>CacheTime</c>, non-empty <c>ModuleTitle</c>) as an editor would submit from the legacy
    /// Add-Module workflow (Website/admin/Modules/**).
    /// </summary>
    private static CreateModuleDto NewCreateDto() => new()
    {
        PortalID = 7,
        TabID = 3,
        ModuleDefID = 1,
        ModuleTitle = "Announcements",
        PaneName = "ContentPane",
        ModuleOrder = 1,
        CacheTime = 0,
        Visibility = 0
    };

    /// <summary>
    /// Creates an <see cref="UpdateModuleDto"/> carrying the editable subset of a module's settings.
    /// </summary>
    private static UpdateModuleDto NewUpdateDto(string moduleTitle = "New") => new()
    {
        ModuleTitle = moduleTitle,
        PaneName = "ContentPane",
        ModuleOrder = 2,
        CacheTime = 60,
        Visibility = 1
    };

    // =========================================================================
    //  GetAllAsync
    //
    //  MIGRATION: legacy ModuleController.GetModules() enumeration. Delegates to
    //  IModuleRepository.GetAllAsync and projects the entity collection to DTOs.
    // =========================================================================

    [Fact]
    public async Task GetAllAsync_ReturnsMappedDtosForEveryModule()
    {
        // Two distinct modules; distinct Visibility values prove the int passthrough survives the
        // entity -> DTO projection (VisibilityState was an enum in DNN, kept as int here).
        var modules = new List<Module>
        {
            NewModule(moduleId: 10, portalId: 1, moduleTitle: "First",  visibility: 0),
            NewModule(moduleId: 20, portalId: 1, moduleTitle: "Second", visibility: 2)
        };

        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var result = (await _sut.GetAllAsync()).ToList();

        result.Should().HaveCount(2, "every entity returned by the repository must be projected");

        result[0].ModuleID.Should().Be(10);
        result[0].ModuleTitle.Should().Be("First");
        result[0].Visibility.Should().Be(0, "the legacy VisibilityState int must pass through unchanged");

        result[1].ModuleID.Should().Be(20);
        result[1].ModuleTitle.Should().Be("Second");
        result[1].Visibility.Should().Be(2, "the legacy VisibilityState int must pass through unchanged");

        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAllAsync_WithNoModules_ReturnsEmptyCollection()
    {
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Module>());

        var result = await _sut.GetAllAsync();

        result.Should().NotBeNull().And.BeEmpty("an empty repository yields an empty projection, never null");
        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    // =========================================================================
    //  GetByPortalAsync
    //
    //  MIGRATION: ModuleController.GetModules(PortalID) [ModuleController.vb L915].
    //  Delegates to IModuleRepository.GetByPortalAsync(portalId) and projects to DTOs. The exact
    //  portal id supplied by the caller must be forwarded to the repository unchanged.
    // =========================================================================

    [Fact]
    public async Task GetByPortalAsync_ReturnsMappedDtosForThePortal()
    {
        const int portalId = 5;
        var modules = new List<Module>
        {
            NewModule(moduleId: 100, portalId: portalId, moduleTitle: "Alpha", visibility: 1),
            NewModule(moduleId: 101, portalId: portalId, moduleTitle: "Beta",  visibility: 0)
        };

        _repository
            .Setup(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        var result = (await _sut.GetByPortalAsync(portalId)).ToList();

        result.Should().HaveCount(2);
        result.Select(m => m.ModuleID).Should().Equal(100, 101);
        result[0].ModuleTitle.Should().Be("Alpha");
        result[0].Visibility.Should().Be(1);
        result[1].PortalID.Should().Be(portalId);

        // The portal id must be forwarded verbatim to the repository (not the generic GetAllAsync).
        _repository.Verify(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task GetByPortalAsync_WithNoModules_ReturnsEmptyCollection()
    {
        _repository
            .Setup(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Module>());

        var result = await _sut.GetByPortalAsync(999);

        result.Should().NotBeNull().And.BeEmpty();
        _repository.Verify(r => r.GetByPortalAsync(999, It.IsAny<CancellationToken>()), Times.Once());
    }

    // =========================================================================
    //  GetByIdAsync
    //
    //  MIGRATION: ModuleController.GetModule(ModuleId, ...) [ModuleController.vb L885]. A found
    //  module is projected to a DTO; a missing module maps to null (legacy returned Nothing).
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedDto()
    {
        const int moduleId = 42;
        _repository
            .Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewModule(moduleId: moduleId, portalId: 3, moduleTitle: "Found", visibility: 2));

        var result = await _sut.GetByIdAsync(moduleId);

        result.Should().NotBeNull();
        ModuleDto dto = result!;
        dto.ModuleID.Should().Be(moduleId);
        dto.ModuleTitle.Should().Be("Found");
        dto.PortalID.Should().Be(3);
        dto.Visibility.Should().Be(2, "the legacy VisibilityState int must pass through unchanged");

        _repository.Verify(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        var result = await _sut.GetByIdAsync(123);

        result.Should().BeNull("a missing module maps to null, mirroring the legacy Nothing return");
        _repository.Verify(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>()), Times.Once());
    }

    // =========================================================================
    //  GetByDefinitionAsync
    //
    //  MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName)
    //  [ModuleController.vb L955]. Delegates to IModuleRepository.GetByDefinitionAsync; a found
    //  module is projected to a DTO, a missing one maps to null. Both arguments must be forwarded.
    // =========================================================================

    [Fact]
    public async Task GetByDefinitionAsync_WhenFound_ReturnsMappedDtoAndForwardsArguments()
    {
        const int portalId = 1;
        const string friendlyName = "HtmlModule";

        _repository
            .Setup(r => r.GetByDefinitionAsync(portalId, friendlyName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewModule(moduleId: 7, portalId: portalId, moduleTitle: "Html", visibility: 0));

        var result = await _sut.GetByDefinitionAsync(portalId, friendlyName);

        result.Should().NotBeNull();
        ModuleDto dto = result!;
        dto.ModuleID.Should().Be(7);
        dto.ModuleTitle.Should().Be("Html");
        dto.PortalID.Should().Be(portalId);

        // Both the portal id AND the friendly name must reach the repository verbatim.
        _repository.Verify(
            r => r.GetByDefinitionAsync(portalId, friendlyName, It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetByDefinitionAsync_WhenNotFound_ReturnsNull()
    {
        _repository
            .Setup(r => r.GetByDefinitionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        var result = await _sut.GetByDefinitionAsync(2, "Missing");

        result.Should().BeNull();
        _repository.Verify(
            r => r.GetByDefinitionAsync(2, "Missing", It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // =========================================================================
    //  CreateAsync
    //
    //  MIGRATION: ModuleController.AddModule(objModule) [ModuleController.vb L645] returned the new
    //  integer id assigned by the DataProvider insert. Here the CreateModuleDto is mapped to a
    //  Module and, together with its computed [TabModules] placement row(s), persisted ATOMICALLY via
    //  IModuleRepository.AddWithPlacementsAsync (which assigns the module id and stamps it onto every
    //  placement); the stored entity is then projected to a DTO.
    //
    //  MIGRATION (QA finding C): the single-tab path now PRE-VALIDATES the target tab via
    //  ITabRepository.GetByIdAsync BEFORE any write — a request naming a non-existent tab throws a
    //  ConflictException (surfaced as HTTP 409) and NOTHING is persisted, replacing the previous
    //  behaviour where the bad-tab placement violated FK_TabModules_Tabs, returned a raw 500, and left
    //  an orphaned [Modules] row. Persistence is a single atomic unit (AddWithPlacementsAsync) rather
    //  than an unguarded module insert followed by independent placement inserts.
    // =========================================================================

    /// <summary>
    /// Helper: stubs <see cref="IModuleRepository.AddWithPlacementsAsync"/> to mimic the real repository —
    /// assign the module its store-generated id, stamp that id onto every placement, capture both, and
    /// echo the module back. Mirrors <c>PersistModuleAndPlacementsAsync</c> so the service's atomic-create
    /// contract is exercised without a database.
    /// </summary>
    private void SetupAddWithPlacements(int assignedId, Action<Module, IReadOnlyList<TabModule>> capture)
    {
        _repository
            .Setup(r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module m, IReadOnlyList<TabModule> placements, CancellationToken _) =>
            {
                m.ModuleID = assignedId;
                foreach (var placement in placements)
                {
                    placement.ModuleID = m.ModuleID;
                }

                capture(m, placements);
                return m;
            });
    }

    [Fact]
    public async Task CreateAsync_MapsPersistsAndReturnsDtoWithAssignedId()
    {
        const int assignedId = 555;
        var dto = NewCreateDto(); // TabID=3 (> 0), AllTabs=false -> single-tab path (tab must be pre-validated)

        // MIGRATION (QA finding C): the target tab is pre-validated; return a real tab so create proceeds.
        _tabRepository
            .Setup(r => r.GetByIdAsync(dto.TabID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tab { TabID = dto.TabID, PortalID = dto.PortalID });

        // The atomic insert assigns the new identity (as the legacy DataProvider.AddModule did) and
        // returns the persisted entity. Capture the mapped entity so its fields can be asserted.
        Module? persisted = null;
        SetupAddWithPlacements(assignedId, (m, _) => persisted = m);

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.ModuleID.Should().Be(assignedId, "the id assigned by the repository insert must flow to the DTO");
        result.ModuleTitle.Should().Be(dto.ModuleTitle);
        result.PortalID.Should().Be(dto.PortalID);
        result.TabID.Should().Be(dto.TabID);
        result.ModuleDefID.Should().Be(dto.ModuleDefID);

        // The dto was mapped onto a real Module before being handed to the repository.
        persisted.Should().NotBeNull();
        Module saved = persisted!;
        saved.ModuleTitle.Should().Be(dto.ModuleTitle);
        saved.PortalID.Should().Be(dto.PortalID);

        _repository.Verify(
            r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // MIGRATION PARITY GUARD (finding #5): a DNN "create module" persists BOTH the [Modules] record and a
    // [TabModules] placement row (legacy DataProvider.AddTabModule). This test fails if the placement
    // side-effect is dropped (as it was before the fix): it captures the placement handed to the atomic
    // AddWithPlacementsAsync insert and asserts it carries the store-assigned ModuleID, the single target
    // TabID (AllTabs=false), and the placement/presentation fields from the create request.
    [Fact]
    public async Task CreateAsync_WritesTabModulePlacement_ForTheTargetTab()
    {
        const int assignedId = 555;
        // NewCreateDto: PortalID=7, TabID=3, PaneName="ContentPane", ModuleOrder=1, Visibility=0, AllTabs=false.
        var dto = NewCreateDto();

        // MIGRATION (QA finding C): pre-validation reads the target tab; return a real tab so create proceeds.
        _tabRepository
            .Setup(r => r.GetByIdAsync(dto.TabID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tab { TabID = dto.TabID, PortalID = dto.PortalID });

        IReadOnlyList<TabModule> placements = Array.Empty<TabModule>();
        SetupAddWithPlacements(assignedId, (_, p) => placements = p);

        await _sut.CreateAsync(dto);

        // The module + placement are persisted as ONE atomic unit, never a portal-wide fan-out.
        _repository.Verify(
            r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()),
            Times.Once());
        _tabRepository.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        // Legacy per-row placement insert must NOT be used on the create path anymore (superseded by the atomic insert).
        _repository.Verify(r => r.AddTabModuleAsync(It.IsAny<TabModule>(), It.IsAny<CancellationToken>()), Times.Never());

        placements.Should().ContainSingle("the module must be placed on its tab, not created unplaced/invisible");
        var row = placements[0];
        row.ModuleID.Should().Be(assignedId, "the placement must reference the store-assigned module id (FK)");
        row.TabID.Should().Be(dto.TabID, "AllTabs=false places the module on the single target tab");
        row.PaneName.Should().Be(dto.PaneName);
        row.ModuleOrder.Should().Be(dto.ModuleOrder);
        row.CacheTime.Should().Be(dto.CacheTime);
        row.Visibility.Should().Be(dto.Visibility);
    }

    // MIGRATION PARITY GUARD (finding #5): AllTabs=true fans the placement out to EVERY tab in the portal
    // (legacy "add to all pages"), enumerated via ITabRepository.GetByPortalAsync. This test seeds three
    // portal tabs and asserts three placement rows are handed to the atomic insert — one per tab — each
    // carrying the module id.
    [Fact]
    public async Task CreateAsync_WhenAllTabs_PlacesModuleOnEveryPortalTab()
    {
        const int assignedId = 900;
        const int portalId = 7;
        var dto = NewCreateDto() with { AllTabs = true, TabID = 0 };

        var portalTabs = new List<Tab>
        {
            new() { TabID = 11, PortalID = portalId },
            new() { TabID = 22, PortalID = portalId },
            new() { TabID = 33, PortalID = portalId },
        };
        _tabRepository
            .Setup(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalTabs);

        IReadOnlyList<TabModule> placements = Array.Empty<TabModule>();
        SetupAddWithPlacements(assignedId, (_, p) => placements = p);

        await _sut.CreateAsync(dto);

        _tabRepository.Verify(r => r.GetByPortalAsync(portalId, It.IsAny<CancellationToken>()), Times.Once());
        // AllTabs path must NOT pre-validate a single tab (there is no single target tab).
        _tabRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        _repository.Verify(
            r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()),
            Times.Once());
        placements.Select(w => w.TabID).Should().Equal(new[] { 11, 22, 33 }, "one placement row per portal tab, in order");
        placements.Should().OnlyContain(w => w.ModuleID == assignedId, "every fan-out row references the new module id");
    }

    // MIGRATION PARITY GUARD (finding #5): the add path always targeted a real tab. When AllTabs=false and
    // no target tab is supplied (TabID <= 0), no placement row is written (guard against orphaned rows) and
    // no tab pre-validation occurs (there is no tab to validate).
    [Fact]
    public async Task CreateAsync_WhenNotAllTabsAndNoTargetTab_WritesNoPlacement()
    {
        var dto = NewCreateDto() with { AllTabs = false, TabID = 0 };

        IReadOnlyList<TabModule> placements = new List<TabModule> { new() }; // seed non-empty to prove it is replaced
        SetupAddWithPlacements(1, (_, p) => placements = p);

        await _sut.CreateAsync(dto);

        // The atomic insert still runs (persisting the module), but with ZERO placements.
        _repository.Verify(
            r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()),
            Times.Once());
        placements.Should().BeEmpty("TabID <= 0 with AllTabs=false places the module nowhere");
        _tabRepository.Verify(r => r.GetByPortalAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        _tabRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // MIGRATION (QA finding C) — ATOMICITY GUARD: a create request naming a NON-EXISTENT target tab
    // (e.g. TabID 999999) must be rejected with a ConflictException (HTTP 409) BEFORE any write, and the
    // atomic insert must NEVER run — so no orphaned [Modules] row can be left behind. This is the unit-level
    // reproduction of the reviewer's "tabID=999999 -> 500 + orphan row" defect.
    [Fact]
    public async Task CreateAsync_WhenTargetTabDoesNotExist_ThrowsConflict_AndPersistsNothing()
    {
        var dto = NewCreateDto() with { AllTabs = false, TabID = 999999 };

        // The target tab does not exist -> pre-validation returns null.
        _tabRepository
            .Setup(r => r.GetByIdAsync(dto.TabID, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<ConflictException>(
            "a create targeting a non-existent tab must surface as HTTP 409, not a raw 500");

        // Nothing is persisted: the atomic insert must never be reached (no orphaned module row).
        _tabRepository.Verify(r => r.GetByIdAsync(dto.TabID, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(
            r => r.AddWithPlacementsAsync(
                It.IsAny<Module>(), It.IsAny<IReadOnlyList<TabModule>>(), It.IsAny<CancellationToken>()),
            Times.Never());
        _repository.Verify(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Never());
        _repository.Verify(r => r.AddTabModuleAsync(It.IsAny<TabModule>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // =========================================================================
    //  UpdateAsync
    //
    //  MIGRATION: ModuleController.UpdateModule(objModule) [ModuleController.vb L1095]. Only the
    //  field copy is preserved (in-place AutoMapper Map(dto, module)); every legacy
    //  permission/tab-module/order/cache side-effect is dropped. A missing module maps to null and
    //  MUST NOT trigger a repository update.
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_WhenFound_AppliesChangesPersistsAndReturnsUpdatedDto()
    {
        const int moduleId = 8;
        var existing = NewModule(moduleId: moduleId, portalId: 4, moduleTitle: "Old", visibility: 0);

        _repository
            .Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = NewUpdateDto("New");

        var result = await _sut.UpdateAsync(moduleId, dto);

        result.Should().NotBeNull();
        ModuleDto updated = result!;
        updated.ModuleTitle.Should().Be("New", "the update DTO's title must be copied onto the entity and returned");
        updated.CacheTime.Should().Be(dto.CacheTime);
        updated.Visibility.Should().Be(dto.Visibility);
        // Identity fields the update contract never touches are preserved from the fetched entity.
        updated.ModuleID.Should().Be(moduleId);
        updated.PortalID.Should().Be(4);

        // The mutation was applied in-place to the fetched entity, then persisted.
        existing.ModuleTitle.Should().Be("New");
        _repository.Verify(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsNullAndDoesNotPersist()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        var result = await _sut.UpdateAsync(404, NewUpdateDto("Irrelevant"));

        result.Should().BeNull("a missing module cannot be updated");
        _repository.Verify(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // MIGRATION PARITY GUARD (finding #5): a DNN "update module" persisted edits to the module's pane
    // placement / presentation settings (legacy DataProvider.UpdateTabModule). This test seeds an existing
    // [TabModules] row and asserts the update propagates the new placement fields onto it — it fails if the
    // TabModule update side-effect is dropped.
    [Fact]
    public async Task UpdateAsync_PropagatesPlacementEdits_ToExistingTabModuleRows()
    {
        const int moduleId = 8;
        var existing = NewModule(moduleId: moduleId, portalId: 4, moduleTitle: "Old", visibility: 0);
        _repository.Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var placementRow = new TabModule
        {
            TabModuleID = 50, ModuleID = moduleId, TabID = 3,
            PaneName = "OldPane", ModuleOrder = 1, CacheTime = 0, Visibility = 0
        };
        _repository
            .Setup(r => r.GetTabModulesByModuleAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { placementRow });

        TabModule? updatedRow = null;
        _repository
            .Setup(r => r.UpdateTabModuleAsync(It.IsAny<TabModule>(), It.IsAny<CancellationToken>()))
            .Returns((TabModule tm, CancellationToken _) => { updatedRow = tm; return Task.CompletedTask; });

        // NewUpdateDto("New"): PaneName="ContentPane", ModuleOrder=2, CacheTime=60, Visibility=1.
        var dto = NewUpdateDto("New");

        await _sut.UpdateAsync(moduleId, dto);

        _repository.Verify(r => r.GetTabModulesByModuleAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.UpdateTabModuleAsync(It.IsAny<TabModule>(), It.IsAny<CancellationToken>()), Times.Once());

        updatedRow.Should().NotBeNull("the module's existing placement row must receive the presentation edits");
        var row = updatedRow!;
        row.TabModuleID.Should().Be(50, "the SAME placement row is updated, keyed by its surrogate id");
        row.PaneName.Should().Be(dto.PaneName, "a non-null PaneName on the update DTO overwrites the pane");
        row.ModuleOrder.Should().Be(dto.ModuleOrder);
        row.CacheTime.Should().Be(dto.CacheTime);
        row.Visibility.Should().Be(dto.Visibility);
    }

    // MIGRATION BOUNDARY GUARD (finding #5): UpdateModuleDto.PaneName is nullable; a null PaneName preserves
    // the placement row's current pane rather than clearing it to empty. Documents and pins that boundary.
    [Fact]
    public async Task UpdateAsync_WithNullPaneName_PreservesExistingPane()
    {
        const int moduleId = 9;
        var existing = NewModule(moduleId: moduleId, portalId: 4, moduleTitle: "Old", visibility: 0);
        _repository.Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var placementRow = new TabModule
        {
            TabModuleID = 60, ModuleID = moduleId, TabID = 3, PaneName = "LeftPane", ModuleOrder = 1
        };
        _repository
            .Setup(r => r.GetTabModulesByModuleAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { placementRow });

        TabModule? updatedRow = null;
        _repository
            .Setup(r => r.UpdateTabModuleAsync(It.IsAny<TabModule>(), It.IsAny<CancellationToken>()))
            .Returns((TabModule tm, CancellationToken _) => { updatedRow = tm; return Task.CompletedTask; });

        // PaneName omitted (null) => the existing "LeftPane" must be preserved.
        var dto = new UpdateModuleDto { ModuleTitle = "New", PaneName = null, ModuleOrder = 5 };

        await _sut.UpdateAsync(moduleId, dto);

        updatedRow.Should().NotBeNull();
        updatedRow!.PaneName.Should().Be("LeftPane", "a null PaneName preserves the current pane");
        updatedRow.ModuleOrder.Should().Be(5, "other supplied placement fields still apply");
    }

    // =========================================================================
    //  DeleteAsync
    //
    //  MIGRATION: ModuleController.DeleteModule(ModuleId) [ModuleController.vb L819] performed an
    //  unconditional delete. The migrated service first checks existence so the API layer can
    //  surface a 404: a found module is deleted (by id) and true is returned; a missing module
    //  returns false WITHOUT calling the repository delete.
    // =========================================================================

    [Fact]
    public async Task DeleteAsync_WhenFound_DeletesByIdAndReturnsTrue()
    {
        const int moduleId = 12;
        _repository
            .Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewModule(moduleId: moduleId));
        _repository
            .Setup(r => r.DeleteAsync(moduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(moduleId);

        result.Should().BeTrue();
        // The delete targets the id (not the entity), matching the IRepository.DeleteAsync(int) port.
        _repository.Verify(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.DeleteAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ReturnsFalseAndDoesNotDelete()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        var result = await _sut.DeleteAsync(77);

        result.Should().BeFalse("a missing module cannot be deleted");
        _repository.Verify(r => r.GetByIdAsync(77, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // MIGRATION PARITY GUARD (finding #5): the legacy FK_{oq}TabModules_{oq}Modules was ON DELETE CASCADE, so
    // deleting a module removed its [TabModules] placement rows. The EF Core InMemory provider does not
    // enforce cascade, so the service performs it explicitly. This test asserts the placement cascade runs
    // (and runs BEFORE the module row is removed) — it fails if the cascade side-effect is dropped.
    [Fact]
    public async Task DeleteAsync_WhenFound_CascadesTabModulePlacements_BeforeDeletingModule()
    {
        const int moduleId = 12;
        var callOrder = new List<string>();

        _repository
            .Setup(r => r.GetByIdAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewModule(moduleId: moduleId));
        _repository
            .Setup(r => r.DeleteTabModulesByModuleAsync(moduleId, It.IsAny<CancellationToken>()))
            .Returns((int _, CancellationToken _) => { callOrder.Add("tabmodules"); return Task.CompletedTask; });
        _repository
            .Setup(r => r.DeleteAsync(moduleId, It.IsAny<CancellationToken>()))
            .Returns((int _, CancellationToken _) => { callOrder.Add("module"); return Task.CompletedTask; });

        var result = await _sut.DeleteAsync(moduleId);

        result.Should().BeTrue();
        _repository.Verify(r => r.DeleteTabModulesByModuleAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
        _repository.Verify(r => r.DeleteAsync(moduleId, It.IsAny<CancellationToken>()), Times.Once());
        callOrder.Should().Equal(
            new[] { "tabmodules", "module" },
            "the placement rows must be cascaded BEFORE the module row (provider-agnostic parity)");
    }

    // MIGRATION PARITY GUARD (finding #5): a missing module performs NO writes — neither the placement
    // cascade nor the module delete runs (so the API can surface a 404).
    [Fact]
    public async Task DeleteAsync_WhenNotFound_DoesNotCascadeOrDelete()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        var result = await _sut.DeleteAsync(88);

        result.Should().BeFalse();
        _repository.Verify(r => r.DeleteTabModulesByModuleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
        _repository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // =========================================================================
    //  Contract & cancellation-token propagation
    // =========================================================================

    [Fact]
    public void ModuleService_ImplementsIModuleService()
    {
        // The API/DI layer depends on the IModuleService port; ModuleService must be substitutable
        // for it so it can be registered and injected through the container.
        _sut.Should().BeAssignableTo<IModuleService>(
            "ModuleService is the IModuleService implementation registered in the DI container");
    }

    [Fact]
    public async Task GetByIdAsync_ForwardsTheProvidedCancellationToken()
    {
        // Beyond It.IsAny matching, this proves the caller's token is threaded through to the
        // repository rather than being swallowed or replaced with CancellationToken.None.
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewModule(moduleId: 1));

        await _sut.GetByIdAsync(1, token);

        _repository.Verify(r => r.GetByIdAsync(1, token), Times.Once(),
            "the CancellationToken supplied by the caller must be forwarded to the repository unchanged");
    }

    // =========================================================================
    //  SearchAsync
    //  MIGRATION: ModuleController list filtering (Website/admin/Modules search)
    //  -> AAP §0.7.2 server-side GET /api/modules?query=... contract.
    // =========================================================================

    [Fact]
    public async Task SearchAsync_DelegatesToRepository_ForwardsPortalScopeAndQuery_AndProjectsToDtos()
    {
        // Arrange: repository returns the portal-scoped, title-matched modules; the service must
        // forward BOTH the (nullable) portal scope and the free-text term unchanged, then project
        // the Domain entities to DTOs via the real mapper (never returning the entity type).
        var matches = new[]
        {
            NewModule(moduleId: 11, portalId: 7, moduleTitle: "Announcements"),
            NewModule(moduleId: 12, portalId: 7, moduleTitle: "Announcement Archive"),
        };
        _repository
            .Setup(r => r.SearchAsync(7, "announce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        // Act
        var result = (await _sut.SearchAsync(7, "announce")).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Select(m => m.ModuleID).Should().BeEquivalentTo(new[] { 11, 12 });
        result.Should().OnlyContain(m => m.PortalID == 7);
        _repository.Verify(r => r.SearchAsync(7, "announce", It.IsAny<CancellationToken>()), Times.Once(),
            "the service must delegate the search to the repository with the portal scope and query intact");
    }

    [Fact]
    public async Task SearchAsync_WithNullPortalScope_ForwardsNullToRepository()
    {
        // A HOST superuser lists across all portals: the controller passes portalId = null, which the
        // service must forward verbatim so the repository performs an unscoped (all-portal) search.
        _repository
            .Setup(r => r.SearchAsync(null, "news", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { NewModule(moduleId: 21, portalId: 3, moduleTitle: "News") });

        var result = (await _sut.SearchAsync(null, "news")).ToList();

        result.Should().ContainSingle().Which.ModuleID.Should().Be(21);
        _repository.Verify(r => r.SearchAsync(null, "news", It.IsAny<CancellationToken>()), Times.Once());
    }
}
