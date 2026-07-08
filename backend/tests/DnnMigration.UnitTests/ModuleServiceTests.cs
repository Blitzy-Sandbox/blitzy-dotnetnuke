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
using DnnMigration.Application.DTOs;
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

        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = configuration.CreateMapper();

        _sut = new ModuleService(_repository.Object, _mapper);
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
    //  Module, persisted via IModuleRepository.AddAsync (which echoes back the stored entity with
    //  its assigned id), and the stored entity is projected to a DTO.
    // =========================================================================

    [Fact]
    public async Task CreateAsync_MapsPersistsAndReturnsDtoWithAssignedId()
    {
        const int assignedId = 555;
        var dto = NewCreateDto();

        // The repository insert assigns the new identity (as the legacy DataProvider.AddModule did)
        // and returns the persisted entity. Capture the mapped entity so its fields can be asserted.
        Module? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module m, CancellationToken _) =>
            {
                m.ModuleID = assignedId;
                persisted = m;
                return m;
            });

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

        _repository.Verify(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()), Times.Once());
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
}
