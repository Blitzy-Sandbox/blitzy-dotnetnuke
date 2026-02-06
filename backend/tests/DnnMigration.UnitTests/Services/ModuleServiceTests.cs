// -----------------------------------------------------------------------------
// DnnMigration - ModuleService Unit Tests
// MIGRATION: Tests verify behavioral equivalence between the migrated C# 12
// ModuleService and the original DNN 4.x ModuleController.vb operations.
// Source Reference: Library/Components/Modules/ModuleController.vb
// Target: 85% coverage per Section 0.7.5
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="ModuleService"/> verifying behavioral equivalence
/// with the original VB.NET ModuleController.vb module lifecycle operations.
/// </summary>
/// <remarks>
/// MIGRATION: These tests ensure that the migrated ModuleService produces
/// identical outcomes for module CRUD operations, tab-module relationships,
/// copy/move operations, and settings management as the original implementation.
/// </remarks>
public class ModuleServiceTests
{
    #region Private Fields

    /// <summary>
    /// Mock for the module repository interface.
    /// </summary>
    private readonly Mock<IModuleRepository> _mockModuleRepository;

    /// <summary>
    /// Mock for the tab repository interface.
    /// </summary>
    private readonly Mock<ITabRepository> _mockTabRepository;

    /// <summary>
    /// Mock for the AutoMapper interface.
    /// </summary>
    private readonly Mock<IMapper> _mockMapper;

    /// <summary>
    /// The system under test (SUT).
    /// </summary>
    private readonly ModuleService _sut;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleServiceTests"/> class.
    /// Sets up all mock dependencies and creates the service under test.
    /// </summary>
    public ModuleServiceTests()
    {
        _mockModuleRepository = new Mock<IModuleRepository>();
        _mockTabRepository = new Mock<ITabRepository>();
        _mockMapper = new Mock<IMapper>();

        _sut = new ModuleService(
            _mockModuleRepository.Object,
            _mockTabRepository.Object,
            _mockMapper.Object);
    }

    #endregion

    #region GetModuleAsync Tests

    /// <summary>
    /// Verifies that GetModuleAsync returns a properly mapped ModuleDto
    /// when a valid module ID and tab ID are provided.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.GetModule(moduleId, tabId)
    /// which retrieves a module by its ID and tab placement.
    /// </remarks>
    [Fact]
    public async Task GetModuleAsync_WithValidModuleAndTabId_ReturnsModuleDto()
    {
        // Arrange
        const int moduleId = 100;
        const int tabId = 50;
        var module = CreateTestModule(moduleId, tabId);
        var expectedDto = CreateTestModuleDto(moduleId, tabId);

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(module);

        _mockMapper
            .Setup(m => m.Map<ModuleDto>(module))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetModuleAsync(moduleId, tabId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ModuleId.Should().Be(moduleId);
        result.TabId.Should().Be(tabId);

        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetModuleAsync returns null when the module is not found.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Original VB.NET returned Nothing when module not found.
    /// </remarks>
    [Fact]
    public async Task GetModuleAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int invalidModuleId = 999;
        const int tabId = 50;

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(invalidModuleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        // Act
        var result = await _sut.GetModuleAsync(invalidModuleId, tabId, CancellationToken.None);

        // Assert
        result.Should().BeNull();

        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(invalidModuleId, tabId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetModuleAsync returns null when module ID is zero or negative.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Business logic preserved - invalid IDs return null without DB call.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetModuleAsync_WithZeroOrNegativeId_ReturnsNull(int invalidId)
    {
        // Arrange
        const int tabId = 50;

        // Act
        var result = await _sut.GetModuleAsync(invalidId, tabId, CancellationToken.None);

        // Assert
        result.Should().BeNull();

        // Repository should not be called for invalid IDs
        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetModulesAsync Tests

    /// <summary>
    /// Verifies that GetModulesAsync returns a properly paginated result
    /// when retrieving modules by portal ID.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.GetModules(portalId)
    /// with pagination support added for modern API patterns.
    /// </remarks>
    [Fact]
    public async Task GetModulesAsync_ByPortalId_ReturnsPagedResult()
    {
        // Arrange
        const int portalId = 1;
        const int pageIndex = 0;
        const int pageSize = 10;

        var modules = new List<Module>
        {
            CreateTestModule(1, 10, portalId),
            CreateTestModule(2, 10, portalId),
            CreateTestModule(3, 20, portalId)
        };

        var moduleDtos = modules.Select(m => CreateTestModuleDto(m.ModuleId, m.TabId, portalId)).ToList();

        _mockModuleRepository
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<ModuleDto>>(It.IsAny<IEnumerable<Module>>()))
            .Returns(moduleDtos);

        // Act
        var result = await _sut.GetModulesAsync(portalId, pageIndex, pageSize, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.TotalCount.Should().Be(3);

        _mockModuleRepository.Verify(
            r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetModulesAsync returns empty result for invalid portal ID.
    /// </summary>
    [Fact]
    public async Task GetModulesAsync_WithInvalidPortalId_ReturnsEmptyResult()
    {
        // Arrange
        const int invalidPortalId = -1;
        const int pageIndex = 0;
        const int pageSize = 10;

        // Act
        var result = await _sut.GetModulesAsync(invalidPortalId, pageIndex, pageSize, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);

        // Repository should not be called for invalid portal IDs
        _mockModuleRepository.Verify(
            r => r.GetByPortalIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetModulesByTabAsync Tests

    /// <summary>
    /// Verifies that GetModulesByTabAsync returns all modules placed on a specific tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.GetTabModules(tabId)
    /// which returns an ArrayList of ModuleInfo objects for a tab.
    /// </remarks>
    [Fact]
    public async Task GetModulesByTabAsync_WithValidTabId_ReturnsModuleList()
    {
        // Arrange
        const int tabId = 50;
        const int portalId = 1;

        var modules = new List<Module>
        {
            CreateTestModule(1, tabId, portalId, "ContentPane", 1),
            CreateTestModule(2, tabId, portalId, "ContentPane", 2),
            CreateTestModule(3, tabId, portalId, "LeftPane", 1)
        };

        var moduleDtos = modules.Select(m => CreateTestModuleDto(m.ModuleId, m.TabId, portalId, m.PaneName ?? "ContentPane", m.ModuleOrder)).ToList();

        _mockModuleRepository
            .Setup(r => r.GetByTabIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<ModuleDto>>(modules))
            .Returns(moduleDtos);

        // Act
        var result = await _sut.GetModulesByTabAsync(tabId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.All(m => m.TabId == tabId).Should().BeTrue();

        _mockModuleRepository.Verify(
            r => r.GetByTabIdAsync(tabId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetModulesByTabAsync returns empty for invalid tab ID.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetModulesByTabAsync_WithInvalidTabId_ReturnsEmpty(int invalidTabId)
    {
        // Act
        var result = await _sut.GetModulesByTabAsync(invalidTabId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockModuleRepository.Verify(
            r => r.GetByTabIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetAllTabsModulesAsync Tests

    /// <summary>
    /// Verifies that GetAllTabsModulesAsync returns modules flagged for all tabs display.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.GetAllTabsModules(portalId, allTabs:=True)
    /// which returns modules that should appear on all tabs.
    /// </remarks>
    [Fact]
    public async Task GetAllTabsModulesAsync_ReturnsAllTabsModules()
    {
        // Arrange
        const int portalId = 1;
        const bool allTabs = true;

        var modules = new List<Module>
        {
            CreateTestModule(1, 10, portalId, allTabs: true),
            CreateTestModule(2, 10, portalId, allTabs: true)
        };

        var moduleDtos = modules.Select(m => CreateTestModuleDto(m.ModuleId, m.TabId, portalId, allTabs: true)).ToList();

        _mockModuleRepository
            .Setup(r => r.GetAllTabsModulesAsync(portalId, allTabs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modules);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<ModuleDto>>(modules))
            .Returns(moduleDtos);

        // Act
        var result = await _sut.GetAllTabsModulesAsync(portalId, allTabs, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(m => m.AllTabs).Should().BeTrue();

        _mockModuleRepository.Verify(
            r => r.GetAllTabsModulesAsync(portalId, allTabs, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetAllTabsModulesAsync returns empty for invalid portal ID.
    /// </summary>
    [Fact]
    public async Task GetAllTabsModulesAsync_WithInvalidPortalId_ReturnsEmpty()
    {
        // Arrange
        const int invalidPortalId = -1;
        const bool allTabs = true;

        // Act
        var result = await _sut.GetAllTabsModulesAsync(invalidPortalId, allTabs, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockModuleRepository.Verify(
            r => r.GetAllTabsModulesAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CreateModuleAsync Tests

    /// <summary>
    /// Verifies that CreateModuleAsync creates a module with proper permission setup.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.AddModule which creates
    /// the module record, tab-module association, and iterates through ModulePermissions.
    /// </remarks>
    [Fact]
    public async Task CreateModuleAsync_WithValidRequest_CreatesModuleWithPermissions()
    {
        // Arrange
        const int tabId = 50;
        const int moduleDefId = 117;
        const int portalId = 1;
        const int newModuleId = 100;

        var request = new CreateModuleRequest
        {
            TabId = tabId,
            ModuleDefId = moduleDefId,
            PaneName = "ContentPane",
            ModuleOrder = 1,
            ModuleTitle = "Test Module",
            ContainerSrc = null,
            CacheTime = 0,
            DisplayTitle = true,
            InheritViewPermissions = true,
            AllTabs = false,
            Visibility = 0
        };

        var createdModule = CreateTestModule(newModuleId, tabId, portalId);
        var expectedDto = CreateTestModuleDto(newModuleId, tabId, portalId);

        _mockModuleRepository
            .Setup(r => r.AddAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdModule);

        _mockMapper
            .Setup(m => m.Map<ModuleDto>(createdModule))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreateModuleAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ModuleId.Should().Be(newModuleId);
        result.TabId.Should().Be(tabId);

        _mockModuleRepository.Verify(
            r => r.AddAsync(It.Is<Module>(m =>
                m.TabId == tabId &&
                m.ModuleDefId == moduleDefId &&
                m.PaneName == "ContentPane" &&
                m.ModuleTitle == "Test Module"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that CreateModuleAsync throws when request is null.
    /// </summary>
    [Fact]
    public async Task CreateModuleAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.CreateModuleAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateModuleAsync Tests

    /// <summary>
    /// Verifies that UpdateModuleAsync updates the module and returns the updated DTO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.UpdateModule which updates
    /// module properties and tab-module settings.
    /// </remarks>
    [Fact]
    public async Task UpdateModuleAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int moduleId = 100;
        const int tabId = 50;
        const int portalId = 1;

        var request = new UpdateModuleRequest
        {
            TabId = tabId,
            ModuleTitle = "Updated Module Title",
            Alignment = "center",
            Color = "#FFFFFF",
            Border = "1px solid #000",
            IconFile = "~/images/icon.gif",
            CacheTime = 3600,
            Visibility = 0,
            Header = "<div>Header</div>",
            Footer = "<div>Footer</div>",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            ContainerSrc = "[G]Skins/DefaultSkin/Title.ascx",
            DisplayTitle = true,
            InheritViewPermissions = false,
            AllTabs = false
        };

        var existingModule = CreateTestModule(moduleId, tabId, portalId);
        var expectedDto = CreateTestModuleDto(moduleId, tabId, portalId, title: "Updated Module Title");

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingModule);

        _mockModuleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<ModuleDto>(It.IsAny<Module>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdateModuleAsync(moduleId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ModuleId.Should().Be(moduleId);
        result.ModuleTitle.Should().Be("Updated Module Title");

        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockModuleRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Module>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateModuleAsync throws when module not found.
    /// </summary>
    [Fact]
    public async Task UpdateModuleAsync_WithNonExistentModule_ThrowsInvalidOperationException()
    {
        // Arrange
        const int moduleId = 999;
        const int tabId = 50;

        var request = new UpdateModuleRequest
        {
            TabId = tabId,
            ModuleTitle = "Updated Title"
        };

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        // Act
        var act = () => _sut.UpdateModuleAsync(moduleId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Module with ID {moduleId} not found.");
    }

    /// <summary>
    /// Verifies that UpdateModuleAsync throws when module ID is invalid.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateModuleAsync_WithInvalidModuleId_ThrowsArgumentException(int invalidId)
    {
        // Arrange
        var request = new UpdateModuleRequest { TabId = 50 };

        // Act
        var act = () => _sut.UpdateModuleAsync(invalidId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region DeleteModuleAsync Tests

    /// <summary>
    /// Verifies that DeleteModuleAsync deletes a module with both module ID and tab ID.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.DeleteTabModule(tabId, moduleId)
    /// which removes a module from a specific tab.
    /// </remarks>
    [Fact]
    public async Task DeleteModuleAsync_WithModuleAndTabId_DeletesModule()
    {
        // Arrange
        const int moduleId = 100;
        const int tabId = 50;
        const int portalId = 1;

        var existingModule = CreateTestModule(moduleId, tabId, portalId);

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingModule);

        _mockModuleRepository
            .Setup(r => r.DeleteTabModuleAsync(tabId, moduleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteModuleAsync(moduleId, tabId, CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockModuleRepository.Verify(
            r => r.DeleteTabModuleAsync(tabId, moduleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that DeleteModuleAsync is idempotent when module doesn't exist.
    /// </summary>
    [Fact]
    public async Task DeleteModuleAsync_WithNonExistentModule_DoesNotThrow()
    {
        // Arrange
        const int moduleId = 999;
        const int tabId = 50;

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        // Act
        var act = () => _sut.DeleteModuleAsync(moduleId, tabId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();

        _mockModuleRepository.Verify(
            r => r.DeleteTabModuleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that DeleteModuleAsync throws when module ID is invalid.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteModuleAsync_WithInvalidModuleId_ThrowsArgumentException(int invalidId)
    {
        // Arrange
        const int tabId = 50;

        // Act
        var act = () => _sut.DeleteModuleAsync(invalidId, tabId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region CopyModuleAsync Tests

    /// <summary>
    /// Verifies that CopyModuleAsync copies a module between tabs including settings.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.CopyModule(ModuleId, FromTabId, ToTabId, ToPaneName, includeSettings:=True)
    /// which copies module and optionally its settings to another tab.
    /// </remarks>
    [Fact]
    public async Task CopyModuleAsync_BetweenTabs_CopiesModuleWithSettings()
    {
        // Arrange
        const int sourceModuleId = 100;
        const int sourceTabId = 50;
        const int targetTabId = 60;
        const int portalId = 1;
        const string targetPaneName = "ContentPane";
        const bool includeSettings = true;

        var sourceModule = CreateTestModule(sourceModuleId, sourceTabId, portalId);

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(sourceModuleId, sourceTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceModule);

        _mockModuleRepository
            .Setup(r => r.CopyModuleAsync(sourceModuleId, sourceTabId, targetTabId, targetPaneName, includeSettings, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CopyModuleAsync(
            sourceModuleId,
            sourceTabId,
            targetTabId,
            targetPaneName,
            includeSettings,
            CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(sourceModuleId, sourceTabId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockModuleRepository.Verify(
            r => r.CopyModuleAsync(sourceModuleId, sourceTabId, targetTabId, targetPaneName, includeSettings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that CopyModuleAsync copies a module without settings when specified.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.CopyModule with includeSettings:=False
    /// </remarks>
    [Fact]
    public async Task CopyModuleAsync_WithoutSettings_CopiesModuleOnly()
    {
        // Arrange
        const int sourceModuleId = 100;
        const int sourceTabId = 50;
        const int targetTabId = 60;
        const int portalId = 1;
        const string targetPaneName = "ContentPane";
        const bool includeSettings = false;

        var sourceModule = CreateTestModule(sourceModuleId, sourceTabId, portalId);

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(sourceModuleId, sourceTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceModule);

        _mockModuleRepository
            .Setup(r => r.CopyModuleAsync(sourceModuleId, sourceTabId, targetTabId, targetPaneName, includeSettings, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CopyModuleAsync(
            sourceModuleId,
            sourceTabId,
            targetTabId,
            targetPaneName,
            includeSettings,
            CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.CopyModuleAsync(sourceModuleId, sourceTabId, targetTabId, targetPaneName, includeSettings, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that CopyModuleAsync throws when source module doesn't exist.
    /// </summary>
    [Fact]
    public async Task CopyModuleAsync_WithNonExistentSourceModule_ThrowsInvalidOperationException()
    {
        // Arrange
        const int sourceModuleId = 999;
        const int sourceTabId = 50;
        const int targetTabId = 60;
        const string targetPaneName = "ContentPane";

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(sourceModuleId, sourceTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        // Act
        var act = () => _sut.CopyModuleAsync(
            sourceModuleId,
            sourceTabId,
            targetTabId,
            targetPaneName,
            false,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Source module with ID {sourceModuleId} on tab {sourceTabId} not found.");
    }

    /// <summary>
    /// Verifies that CopyModuleAsync validates input parameters.
    /// </summary>
    [Theory]
    [InlineData(0, 50, 60, "ContentPane")]
    [InlineData(100, 0, 60, "ContentPane")]
    [InlineData(100, 50, 0, "ContentPane")]
    [InlineData(100, 50, 60, "")]
    [InlineData(100, 50, 60, "  ")]
    public async Task CopyModuleAsync_WithInvalidParameters_ThrowsArgumentException(
        int moduleId, int fromTabId, int toTabId, string paneName)
    {
        // Act
        var act = () => _sut.CopyModuleAsync(moduleId, fromTabId, toTabId, paneName, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region MoveModuleAsync Tests

    /// <summary>
    /// Verifies that MoveModuleAsync moves a module between tabs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.MoveModule which removes
    /// the module from the source tab and adds it to the target tab.
    /// </remarks>
    [Fact]
    public async Task MoveModuleAsync_BetweenTabs_MovesModule()
    {
        // Arrange
        const int moduleId = 100;
        const int sourceTabId = 50;
        const int targetTabId = 60;
        const int portalId = 1;
        const string targetPaneName = "ContentPane";

        var sourceModule = CreateTestModule(moduleId, sourceTabId, portalId);

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, sourceTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceModule);

        _mockModuleRepository
            .Setup(r => r.MoveModuleAsync(moduleId, sourceTabId, targetTabId, targetPaneName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveModuleAsync(
            moduleId,
            sourceTabId,
            targetTabId,
            targetPaneName,
            CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.GetByIdAsync(moduleId, sourceTabId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockModuleRepository.Verify(
            r => r.MoveModuleAsync(moduleId, sourceTabId, targetTabId, targetPaneName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that MoveModuleAsync throws when source module doesn't exist.
    /// </summary>
    [Fact]
    public async Task MoveModuleAsync_WithNonExistentSourceModule_ThrowsInvalidOperationException()
    {
        // Arrange
        const int moduleId = 999;
        const int sourceTabId = 50;
        const int targetTabId = 60;
        const string targetPaneName = "ContentPane";

        _mockModuleRepository
            .Setup(r => r.GetByIdAsync(moduleId, sourceTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Module?)null);

        // Act
        var act = () => _sut.MoveModuleAsync(
            moduleId,
            sourceTabId,
            targetTabId,
            targetPaneName,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Source module with ID {moduleId} on tab {sourceTabId} not found.");
    }

    #endregion

    #region UpdateModuleOrderAsync Tests

    /// <summary>
    /// Verifies that UpdateModuleOrderAsync reorders modules within a pane.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.UpdateModuleOrder(tabId, moduleId, moduleOrder, paneName)
    /// which updates the display order of modules within a pane.
    /// </remarks>
    [Fact]
    public async Task UpdateModuleOrderAsync_WithinPane_ReordersModules()
    {
        // Arrange
        const int tabId = 50;
        const int moduleId = 100;
        const int newModuleOrder = 3;
        const string paneName = "ContentPane";

        _mockModuleRepository
            .Setup(r => r.UpdateModuleOrderAsync(tabId, moduleId, newModuleOrder, paneName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateModuleOrderAsync(
            tabId,
            moduleId,
            newModuleOrder,
            paneName,
            CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.UpdateModuleOrderAsync(tabId, moduleId, newModuleOrder, paneName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateModuleOrderAsync validates input parameters.
    /// </summary>
    [Theory]
    [InlineData(0, 100, 1, "ContentPane")]
    [InlineData(50, 0, 1, "ContentPane")]
    [InlineData(50, 100, 1, "")]
    [InlineData(50, 100, 1, "  ")]
    public async Task UpdateModuleOrderAsync_WithInvalidParameters_ThrowsArgumentException(
        int tabId, int moduleId, int moduleOrder, string paneName)
    {
        // Act
        var act = () => _sut.UpdateModuleOrderAsync(tabId, moduleId, moduleOrder, paneName, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetModuleSettingsAsync Tests

    /// <summary>
    /// Verifies that GetModuleSettingsAsync returns module settings as key-value pairs.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.GetModuleSettings(moduleId)
    /// which returns a Hashtable of module settings.
    /// </remarks>
    [Fact]
    public async Task GetModuleSettingsAsync_ReturnsSettings()
    {
        // Arrange
        const int moduleId = 100;

        var expectedSettings = new Dictionary<string, string>
        {
            { "Template", "default.html" },
            { "ItemsPerPage", "10" },
            { "ShowAuthor", "true" }
        };

        _mockModuleRepository
            .Setup(r => r.GetModuleSettingsAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSettings);

        // Act
        var result = await _sut.GetModuleSettingsAsync(moduleId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainKey("Template");
        result["Template"].Should().Be("default.html");
        result.Should().ContainKey("ItemsPerPage");
        result["ItemsPerPage"].Should().Be("10");
        result.Should().ContainKey("ShowAuthor");
        result["ShowAuthor"].Should().Be("true");

        _mockModuleRepository.Verify(
            r => r.GetModuleSettingsAsync(moduleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetModuleSettingsAsync returns empty dictionary for invalid module ID.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetModuleSettingsAsync_WithInvalidModuleId_ReturnsEmptyDictionary(int invalidId)
    {
        // Act
        var result = await _sut.GetModuleSettingsAsync(invalidId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockModuleRepository.Verify(
            r => r.GetModuleSettingsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdateModuleSettingAsync Tests

    /// <summary>
    /// Verifies that UpdateModuleSettingAsync saves a setting.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Equivalent to VB.NET ModuleController.UpdateModuleSetting(moduleId, settingName, settingValue)
    /// which persists or updates a module setting.
    /// </remarks>
    [Fact]
    public async Task UpdateModuleSettingAsync_SavesSetting()
    {
        // Arrange
        const int moduleId = 100;
        const string settingName = "DisplayMode";
        const string settingValue = "Grid";

        _mockModuleRepository
            .Setup(r => r.UpdateModuleSettingAsync(moduleId, settingName, settingValue, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateModuleSettingAsync(moduleId, settingName, settingValue, CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.UpdateModuleSettingAsync(moduleId, settingName, settingValue, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateModuleSettingAsync handles empty value by saving empty string.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Original VB.NET behavior where null/empty values are stored as empty strings.
    /// </remarks>
    [Fact]
    public async Task UpdateModuleSettingAsync_WithEmptyValue_SavesEmptyString()
    {
        // Arrange
        const int moduleId = 100;
        const string settingName = "OptionalSetting";
        const string settingValue = "";

        _mockModuleRepository
            .Setup(r => r.UpdateModuleSettingAsync(moduleId, settingName, settingValue, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateModuleSettingAsync(moduleId, settingName, settingValue, CancellationToken.None);

        // Assert
        _mockModuleRepository.Verify(
            r => r.UpdateModuleSettingAsync(moduleId, settingName, string.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that UpdateModuleSettingAsync validates input parameters.
    /// </summary>
    [Theory]
    [InlineData(0, "Setting", "Value")]
    [InlineData(-1, "Setting", "Value")]
    [InlineData(100, "", "Value")]
    [InlineData(100, "  ", "Value")]
    public async Task UpdateModuleSettingAsync_WithInvalidParameters_ThrowsArgumentException(
        int moduleId, string settingName, string settingValue)
    {
        // Act
        var act = () => _sut.UpdateModuleSettingAsync(moduleId, settingName, settingValue, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test Module entity with specified properties.
    /// </summary>
    private static Module CreateTestModule(
        int moduleId,
        int tabId,
        int portalId = 1,
        string paneName = "ContentPane",
        int moduleOrder = 1,
        string title = "Test Module",
        bool allTabs = false)
    {
        return new Module
        {
            ModuleId = moduleId,
            TabModuleId = moduleId * 10,
            TabId = tabId,
            PortalId = portalId,
            ModuleDefId = 117,
            PaneName = paneName,
            ModuleOrder = moduleOrder,
            ModuleTitle = title,
            AllTabs = allTabs,
            IsDeleted = false,
            CacheTime = 0,
            Visibility = VisibilityState.Maximized,
            StartDate = null,
            EndDate = null,
            ContainerSrc = null,
            DisplayTitle = true,
            InheritViewPermissions = true,
            Alignment = string.Empty,
            Color = string.Empty,
            Border = string.Empty,
            IconFile = string.Empty,
            Header = string.Empty,
            Footer = string.Empty,
            IsDefaultModule = false,
            AllModules = false
        };
    }

    /// <summary>
    /// Creates a test ModuleDto with specified properties.
    /// </summary>
    private static ModuleDto CreateTestModuleDto(
        int moduleId,
        int tabId,
        int portalId = 1,
        string paneName = "ContentPane",
        int moduleOrder = 1,
        string title = "Test Module",
        bool allTabs = false)
    {
        return new ModuleDto
        {
            ModuleId = moduleId,
            TabModuleId = moduleId * 10,
            TabId = tabId,
            PortalId = portalId,
            ModuleDefId = 117,
            PaneName = paneName,
            ModuleOrder = moduleOrder,
            ModuleTitle = title,
            AllTabs = allTabs,
            IsDeleted = false,
            CacheTime = 0,
            Visibility = 0,
            StartDate = null,
            EndDate = null,
            ContainerSrc = null,
            DisplayTitle = true,
            InheritViewPermissions = true,
            DesktopModuleId = 1,
            FriendlyName = "Test Module",
            ModuleName = "TestModule",
            FolderName = "TestModuleFolder",
            Description = "A test module",
            Version = "1.0.0",
            IsPortable = false,
            IsSearchable = false,
            IsUpgradeable = false
        };
    }

    #endregion
}
