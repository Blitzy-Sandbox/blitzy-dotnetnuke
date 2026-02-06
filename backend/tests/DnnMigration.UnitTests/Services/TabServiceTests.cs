// -----------------------------------------------------------------------------
// DnnMigration - TabService Unit Tests
// MIGRATION: Tests verify behavioral equivalence between the migrated C# 12
// TabService and the original VB.NET TabController.vb from DotNetNuke 4.x
// Source: Library/Components/Tabs/TabController.vb
// Source: Library/Components/Tabs/TabInfo.vb
// Source: Library/Components/Tabs/Navigation.vb
// -----------------------------------------------------------------------------
// Test Coverage Target: 85% per Section 0.7.5 Application Layer Requirements
// Testing Framework: xUnit 2.9.2
// Mocking Framework: Moq 4.20.72
// Assertion Framework: FluentAssertions 6.12.2
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="TabService"/> verifying tab/page management operations.
/// Tests ensure behavioral equivalence with the legacy VB.NET TabController.
/// </summary>
/// <remarks>
/// MIGRATION: These tests validate that the migrated TabService provides
/// identical behavior to the original DotNetNuke 4.x TabController.vb,
/// including tab CRUD operations, hierarchical navigation management,
/// and tab ordering within portal navigation structures.
/// </remarks>
public class TabServiceTests
{
    #region Private Fields

    private readonly Mock<ITabRepository> _mockTabRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly TabService _sut;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes test fixtures with mock dependencies and system under test.
    /// </summary>
    public TabServiceTests()
    {
        _mockTabRepository = new Mock<ITabRepository>();
        _mockMapper = new Mock<IMapper>();
        _sut = new TabService(_mockTabRepository.Object, _mockMapper.Object);
    }

    #endregion

    #region GetTabAsync Tests

    /// <summary>
    /// Verifies GetTabAsync returns the correct TabDto when tab exists and belongs to portal.
    /// MIGRATION: Corresponds to TabController.GetTab(TabId, PortalId, ignoreCache) in TabController.vb.
    /// Original method retrieved from cache first when ignoreCache was false, then database.
    /// </summary>
    [Fact]
    public async Task GetTabAsync_WithValidId_ReturnsTabDto()
    {
        // Arrange
        const int tabId = 42;
        const int portalId = 1;
        var tab = CreateTestTab(tabId, portalId, "Home Page");
        var expectedDto = CreateTestTabDto(tabId, portalId, "Home Page");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockMapper
            .Setup(m => m.Map<TabDto>(tab))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetTabAsync(tabId, portalId);

        // Assert
        result.Should().NotBeNull();
        result!.TabId.Should().Be(tabId);
        result.TabName.Should().Be("Home Page");
        result.PortalId.Should().Be(portalId);

        _mockTabRepository.Verify(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMapper.Verify(m => m.Map<TabDto>(tab), Times.Once);
    }

    /// <summary>
    /// Verifies GetTabAsync returns null when tab does not exist.
    /// MIGRATION: Original VB.NET method returned Nothing when FillTabInfo returned null.
    /// </summary>
    [Fact]
    public async Task GetTabAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int tabId = 999;
        const int portalId = 1;

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act
        var result = await _sut.GetTabAsync(tabId, portalId);

        // Assert
        result.Should().BeNull();
        _mockMapper.Verify(m => m.Map<TabDto>(It.IsAny<Tab>()), Times.Never);
    }

    /// <summary>
    /// Verifies GetTabAsync returns null when tab exists but belongs to different portal.
    /// MIGRATION: Preserves multi-tenant isolation - original VB.NET validated portal ownership.
    /// </summary>
    [Fact]
    public async Task GetTabAsync_WithWrongPortal_ReturnsNull()
    {
        // Arrange
        const int tabId = 42;
        const int requestedPortalId = 1;
        const int actualPortalId = 2;
        var tab = CreateTestTab(tabId, actualPortalId, "Home Page");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        // Act
        var result = await _sut.GetTabAsync(tabId, requestedPortalId);

        // Assert
        result.Should().BeNull();
        _mockMapper.Verify(m => m.Map<TabDto>(It.IsAny<Tab>()), Times.Never);
    }

    /// <summary>
    /// Verifies GetTabAsync handles various tab IDs correctly using theory-based test.
    /// </summary>
    [Theory]
    [InlineData(1, 1, "Tab One")]
    [InlineData(100, 2, "Tab Hundred")]
    [InlineData(int.MaxValue, 1, "Max Tab")]
    public async Task GetTabAsync_WithVariousIds_ReturnsCorrectTab(int tabId, int portalId, string tabName)
    {
        // Arrange
        var tab = CreateTestTab(tabId, portalId, tabName);
        var expectedDto = CreateTestTabDto(tabId, portalId, tabName);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockMapper
            .Setup(m => m.Map<TabDto>(tab))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetTabAsync(tabId, portalId);

        // Assert
        result.Should().NotBeNull();
        result!.TabId.Should().Be(tabId);
        result.TabName.Should().Be(tabName);
    }

    #endregion

    #region GetTabsAsync Tests

    /// <summary>
    /// Verifies GetTabsAsync returns all tabs for a portal ordered by TabOrder.
    /// MIGRATION: Corresponds to TabController.GetTabs(PortalId) in TabController.vb.
    /// Original method iterated GetTabsByPortal dictionary and returned ArrayList.
    /// </summary>
    [Fact]
    public async Task GetTabsAsync_WithPortalId_ReturnsTabList()
    {
        // Arrange
        const int portalId = 1;
        var tabs = new List<Tab>
        {
            CreateTestTab(3, portalId, "Services", tabOrder: 30),
            CreateTestTab(1, portalId, "Home", tabOrder: 10),
            CreateTestTab(2, portalId, "About", tabOrder: 20)
        };
        var tabDtos = tabs.OrderBy(t => t.TabOrder).Select(t => CreateTestTabDto(t.TabId, t.PortalId, t.TabName, t.TabOrder)).ToList();

        _mockTabRepository
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tabs);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns<Tab>(t => tabDtos.First(d => d.TabId == t.TabId));

        // Act
        var result = await _sut.GetTabsAsync(portalId);

        // Assert
        result.Should().NotBeNull();
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        // MIGRATION: Verify ordering matches original TabOrder sorting behavior
        resultList[0].TabOrder.Should().Be(10);
        resultList[1].TabOrder.Should().Be(20);
        resultList[2].TabOrder.Should().Be(30);

        _mockTabRepository.Verify(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies GetTabsAsync returns empty collection when portal has no tabs.
    /// </summary>
    [Fact]
    public async Task GetTabsAsync_WithEmptyPortal_ReturnsEmptyList()
    {
        // Arrange
        const int portalId = 999;
        var tabs = new List<Tab>();

        _mockTabRepository
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tabs);

        // Act
        var result = await _sut.GetTabsAsync(portalId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetTabsByParentIdAsync Tests

    /// <summary>
    /// Verifies GetTabsByParentIdAsync returns child tabs for a given parent.
    /// MIGRATION: Corresponds to TabController.GetTabsByParentId(ParentId, PortalId) in TabController.vb.
    /// Original method called private GetTabsByParent which filtered GetTabsByPortal by ParentId.
    /// </summary>
    [Fact]
    public async Task GetTabsByParentIdAsync_WithParentTabId_ReturnsChildTabs()
    {
        // Arrange
        const int parentId = 1;
        const int portalId = 1;
        var childTabs = new List<Tab>
        {
            CreateTestTab(10, portalId, "Child 1", parentId: parentId, level: 1),
            CreateTestTab(11, portalId, "Child 2", parentId: parentId, level: 1),
            CreateTestTab(12, portalId, "Child 3", parentId: parentId, level: 1)
        };
        var childDtos = childTabs.Select(t => CreateTestTabDto(t.TabId, t.PortalId, t.TabName, parentId: t.ParentId, level: t.Level)).ToList();

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(parentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTabs);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns<Tab>(t => childDtos.First(d => d.TabId == t.TabId));

        // Act
        var result = await _sut.GetTabsByParentIdAsync(parentId, portalId);

        // Assert
        result.Should().NotBeNull();
        var resultList = result.ToList();
        resultList.Should().HaveCount(3);
        resultList.Should().OnlyContain(t => t.ParentId == parentId);
        resultList.Should().OnlyContain(t => t.Level == 1);

        _mockTabRepository.Verify(r => r.GetChildrenAsync(parentId, portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies GetTabsByParentIdAsync handles null parentId for root tabs.
    /// MIGRATION: VB.NET used -1 or Null.NullInteger for root level tabs.
    /// </summary>
    [Fact]
    public async Task GetTabsByParentIdAsync_WithNullParent_ReturnsRootTabs()
    {
        // Arrange
        int? parentId = null;
        const int portalId = 1;
        var rootTabs = new List<Tab>
        {
            CreateTestTab(1, portalId, "Home", parentId: null, level: 0),
            CreateTestTab(2, portalId, "About", parentId: null, level: 0)
        };
        var rootDtos = rootTabs.Select(t => CreateTestTabDto(t.TabId, t.PortalId, t.TabName, level: 0)).ToList();

        // MIGRATION: null is converted to -1 for repository compatibility
        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(-1, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootTabs);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns<Tab>(t => rootDtos.First(d => d.TabId == t.TabId));

        // Act
        var result = await _sut.GetTabsByParentIdAsync(parentId, portalId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.Level == 0);
    }

    /// <summary>
    /// Verifies GetTabsByParentIdAsync handles various hierarchy scenarios.
    /// </summary>
    [Theory]
    [InlineData(1, 1, 3)]   // Parent tab 1 has 3 children
    [InlineData(2, 1, 2)]   // Parent tab 2 has 2 children
    [InlineData(10, 1, 5)]  // Parent tab 10 has 5 children
    public async Task GetTabsByParentIdAsync_WithVariousParents_ReturnsCorrectChildren(int parentId, int portalId, int expectedChildCount)
    {
        // Arrange
        var childTabs = Enumerable.Range(1, expectedChildCount)
            .Select(i => CreateTestTab(parentId * 100 + i, portalId, $"Child {i}", parentId: parentId, level: 1))
            .ToList();
        var childDtos = childTabs.Select(t => CreateTestTabDto(t.TabId, t.PortalId, t.TabName, parentId: t.ParentId, level: t.Level)).ToList();

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(parentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTabs);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns<Tab>(t => childDtos.First(d => d.TabId == t.TabId));

        // Act
        var result = await _sut.GetTabsByParentIdAsync(parentId, portalId);

        // Assert
        result.Should().HaveCount(expectedChildCount);
    }

    #endregion

    #region GetTabByNameAsync Tests

    /// <summary>
    /// Verifies GetTabByNameAsync returns tab when found by name.
    /// MIGRATION: Corresponds to TabController.GetTabByName(TabName, PortalId) in TabController.vb.
    /// Original method called GetTabByNameAndParent with Integer.MinValue for ParentId.
    /// </summary>
    [Fact]
    public async Task GetTabByNameAsync_WithValidName_ReturnsTab()
    {
        // Arrange
        const string tabName = "About Us";
        const int portalId = 1;
        var tab = CreateTestTab(5, portalId, tabName);
        var expectedDto = CreateTestTabDto(5, portalId, tabName);

        _mockTabRepository
            .Setup(r => r.GetByNameAsync(tabName, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockMapper
            .Setup(m => m.Map<TabDto>(tab))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetTabByNameAsync(tabName, portalId);

        // Assert
        result.Should().NotBeNull();
        result!.TabName.Should().Be(tabName);
        result.PortalId.Should().Be(portalId);
    }

    /// <summary>
    /// Verifies GetTabByNameAsync returns null when tab name not found.
    /// </summary>
    [Fact]
    public async Task GetTabByNameAsync_WithInvalidName_ReturnsNull()
    {
        // Arrange
        const string tabName = "NonExistent";
        const int portalId = 1;

        _mockTabRepository
            .Setup(r => r.GetByNameAsync(tabName, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act
        var result = await _sut.GetTabByNameAsync(tabName, portalId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies GetTabByNameAsync returns null for whitespace or empty name.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetTabByNameAsync_WithEmptyOrWhitespaceName_ReturnsNull(string? tabName)
    {
        // Arrange
        const int portalId = 1;

        // Act
        var result = await _sut.GetTabByNameAsync(tabName!, portalId);

        // Assert
        result.Should().BeNull();
        _mockTabRepository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetTabCountAsync Tests

    /// <summary>
    /// Verifies GetTabCountAsync returns correct count for portal.
    /// MIGRATION: Corresponds to TabController.GetTabCount(portalId) in TabController.vb.
    /// Original method called DataProvider.Instance().GetTabCount(portalId).
    /// </summary>
    [Fact]
    public async Task GetTabCountAsync_WithPortalId_ReturnsCount()
    {
        // Arrange
        const int portalId = 1;
        const int expectedCount = 25;

        _mockTabRepository
            .Setup(r => r.GetTabCountAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _sut.GetTabCountAsync(portalId);

        // Assert
        result.Should().Be(expectedCount);
        _mockTabRepository.Verify(r => r.GetTabCountAsync(portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies GetTabCountAsync returns zero for portal with no tabs.
    /// </summary>
    [Fact]
    public async Task GetTabCountAsync_WithEmptyPortal_ReturnsZero()
    {
        // Arrange
        const int portalId = 999;

        _mockTabRepository
            .Setup(r => r.GetTabCountAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.GetTabCountAsync(portalId);

        // Assert
        result.Should().Be(0);
    }

    /// <summary>
    /// Verifies GetTabCountAsync handles various portal counts.
    /// </summary>
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 50)]
    [InlineData(3, 1)]
    public async Task GetTabCountAsync_WithVariousPortals_ReturnsCorrectCount(int portalId, int expectedCount)
    {
        // Arrange
        _mockTabRepository
            .Setup(r => r.GetTabCountAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _sut.GetTabCountAsync(portalId);

        // Assert
        result.Should().Be(expectedCount);
    }

    #endregion

    #region CreateTabAsync Tests

    /// <summary>
    /// Verifies CreateTabAsync creates a new tab and returns the created DTO.
    /// MIGRATION: Corresponds to TabController.AddTab(objTab, AddAllTabsModules) in TabController.vb.
    /// Original method generated TabPath, called DataProvider.Instance().AddTab, added permissions,
    /// updated portal tab order, optionally copied AllTabs modules, and cleared cache.
    /// </summary>
    [Fact]
    public async Task CreateTabAsync_WithValidRequest_CreatesTabAndReturnsDto()
    {
        // Arrange
        var request = new CreateTabRequest
        {
            PortalId = 1,
            TabName = "New Page",
            ParentId = null,
            Title = "New Page Title",
            Description = "A new test page",
            IsVisible = true
        };

        var createdTab = CreateTestTab(100, request.PortalId, request.TabName, tabPath: "//New Page");
        createdTab.Title = request.Title;
        createdTab.Description = request.Description;

        var expectedDto = CreateTestTabDto(100, request.PortalId, request.TabName);

        // Setup for GetChildrenAsync (used to calculate tab order)
        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(-1, request.PortalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>());

        // Setup for GetByIdAsync (used in GenerateTabPathAsync and CalculateTabLevelAsync)
        _mockTabRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        _mockTabRepository
            .Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTab);

        _mockMapper
            .Setup(m => m.Map<TabDto>(createdTab))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreateTabAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TabName.Should().Be(request.TabName);
        result.PortalId.Should().Be(request.PortalId);

        _mockTabRepository.Verify(r => r.AddAsync(It.Is<Tab>(t => 
            t.TabName == request.TabName && 
            t.PortalId == request.PortalId &&
            t.IsVisible == request.IsVisible), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies CreateTabAsync correctly sets hierarchy when parent is specified.
    /// MIGRATION: Original AddTab calculated Level based on parent and placed tab at end of siblings.
    /// </summary>
    [Fact]
    public async Task CreateTabAsync_WithParentId_SetsHierarchy()
    {
        // Arrange
        const int parentId = 5;
        const int portalId = 1;
        var parentTab = CreateTestTab(parentId, portalId, "Parent Tab", level: 1, tabPath: "//Parent Tab");
        parentTab.HasChildren = true;

        var request = new CreateTabRequest
        {
            PortalId = portalId,
            TabName = "Child Page",
            ParentId = parentId,
            IsVisible = true
        };

        var existingSiblings = new List<Tab>
        {
            CreateTestTab(10, portalId, "Sibling 1", parentId: parentId, tabOrder: 2),
            CreateTestTab(11, portalId, "Sibling 2", parentId: parentId, tabOrder: 4)
        };

        var createdTab = CreateTestTab(100, portalId, request.TabName, parentId: parentId, level: 2);
        var expectedDto = CreateTestTabDto(100, portalId, request.TabName, parentId: parentId, level: 2);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(parentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSiblings);

        _mockTabRepository
            .Setup(r => r.AddAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTab);

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<TabDto>(createdTab))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreateTabAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ParentId.Should().Be(parentId);
        result.Level.Should().Be(2);

        // MIGRATION: Verify tab is created with correct level and parent
        _mockTabRepository.Verify(r => r.AddAsync(It.Is<Tab>(t => 
            t.ParentId == parentId && 
            t.Level == 2 && // Parent level + 1
            t.TabOrder == 6), // Max sibling order (4) + 2
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies CreateTabAsync throws when request is null.
    /// </summary>
    [Fact]
    public async Task CreateTabAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _sut.CreateTabAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateTabAsync Tests

    /// <summary>
    /// Verifies UpdateTabAsync updates tab properties and returns updated DTO.
    /// MIGRATION: Corresponds to TabController.UpdateTab(objTab) in TabController.vb (lines 780-814).
    /// Original method checked if TabName or ParentId changed, updated order, called DataProvider,
    /// updated permissions, updated child paths, and cleared cache.
    /// </summary>
    [Fact]
    public async Task UpdateTabAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int tabId = 42;
        const int portalId = 1;
        var existingTab = CreateTestTab(tabId, portalId, "Old Name");
        
        var request = new UpdateTabRequest
        {
            TabName = "New Name",
            Title = "Updated Title",
            Description = "Updated description"
        };

        var updatedTab = CreateTestTab(tabId, portalId, "New Name");
        updatedTab.Title = "Updated Title";
        updatedTab.Description = "Updated description";

        var expectedDto = CreateTestTabDto(tabId, portalId, "New Name");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>());

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdateTabAsync(tabId, request);

        // Assert
        result.Should().NotBeNull();
        result.TabName.Should().Be("New Name");

        _mockTabRepository.Verify(r => r.UpdateAsync(It.Is<Tab>(t => 
            t.TabId == tabId && 
            t.TabName == "New Name" &&
            t.Title == "Updated Title"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies UpdateTabAsync throws when tab not found.
    /// </summary>
    [Fact]
    public async Task UpdateTabAsync_WithInvalidId_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 999;
        var request = new UpdateTabRequest { TabName = "New Name" };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.UpdateTabAsync(tabId, request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*not found*");
    }

    /// <summary>
    /// Verifies UpdateTabAsync triggers child path updates when TabName changes.
    /// MIGRATION: Original UpdateTab called UpdateChildTabPath when TabName or ParentId changed.
    /// </summary>
    [Fact]
    public async Task UpdateTabAsync_WithNameChange_UpdatesChildPaths()
    {
        // Arrange
        const int tabId = 1;
        const int portalId = 1;
        var existingTab = CreateTestTab(tabId, portalId, "Old Parent", tabPath: "//Old Parent");
        var childTab = CreateTestTab(10, portalId, "Child", parentId: tabId, level: 1, tabPath: "//Old Parent//Child");

        var request = new UpdateTabRequest { TabName = "New Parent" };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab> { childTab });

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<TabDto>(It.IsAny<Tab>()))
            .Returns(CreateTestTabDto(tabId, portalId, "New Parent"));

        // Act
        await _sut.UpdateTabAsync(tabId, request);

        // Assert - verify child tab path was updated
        _mockTabRepository.Verify(r => r.UpdateAsync(It.Is<Tab>(t => 
            t.TabId == 10 && 
            t.TabPath == "//New Parent//Child"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies UpdateTabAsync throws when request is null.
    /// </summary>
    [Fact]
    public async Task UpdateTabAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        const int tabId = 42;

        // Act & Assert
        await FluentActions.Invoking(() => _sut.UpdateTabAsync(tabId, null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteTabAsync Tests

    /// <summary>
    /// Verifies DeleteTabAsync deletes a tab when it has no children.
    /// MIGRATION: Corresponds to TabController.DeleteTab(TabId, PortalId) in TabController.vb (lines 446-457).
    /// Original method checked for children first - parent tabs cannot be deleted.
    /// </summary>
    [Fact]
    public async Task DeleteTabAsync_WithTabIdAndPortalId_DeletesTab()
    {
        // Arrange
        const int tabId = 42;
        const int portalId = 1;
        var tab = CreateTestTab(tabId, portalId, "Tab to Delete");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>()); // No children

        _mockTabRepository
            .Setup(r => r.DeleteAsync(tabId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTabAsync(tabId, portalId);

        // Assert
        _mockTabRepository.Verify(r => r.DeleteAsync(tabId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies DeleteTabAsync throws when tab has children.
    /// MIGRATION: Preserves original constraint from VB.NET: parent tabs cannot be deleted.
    /// Original code: If arrTabs.Count = 0 Then (checking GetTabsByParentId)
    /// </summary>
    [Fact]
    public async Task DeleteTabAsync_WithChildTabs_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 1;
        const int portalId = 1;
        var parentTab = CreateTestTab(tabId, portalId, "Parent");
        var childTabs = new List<Tab>
        {
            CreateTestTab(10, portalId, "Child 1", parentId: tabId),
            CreateTestTab(11, portalId, "Child 2", parentId: tabId)
        };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTabs);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.DeleteTabAsync(tabId, portalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*child tabs*");

        _mockTabRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies DeleteTabAsync throws when tab not found.
    /// </summary>
    [Fact]
    public async Task DeleteTabAsync_WithInvalidId_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 999;
        const int portalId = 1;

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.DeleteTabAsync(tabId, portalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*not found*");
    }

    /// <summary>
    /// Verifies DeleteTabAsync throws when tab belongs to different portal.
    /// MIGRATION: Multi-tenant security - prevent cross-portal tab deletion.
    /// </summary>
    [Fact]
    public async Task DeleteTabAsync_WithWrongPortal_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 42;
        const int requestedPortalId = 1;
        const int actualPortalId = 2;
        var tab = CreateTestTab(tabId, actualPortalId, "Tab");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.DeleteTabAsync(tabId, requestedPortalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*does not belong to portal*{requestedPortalId}*");
    }

    /// <summary>
    /// Verifies DeleteTabAsync updates parent HasChildren flag after deletion.
    /// MIGRATION: Maintains parent HasChildren denormalized flag consistency.
    /// </summary>
    [Fact]
    public async Task DeleteTabAsync_UpdatesParentHasChildrenFlag()
    {
        // Arrange
        const int tabId = 10;
        const int parentId = 1;
        const int portalId = 1;
        var parentTab = CreateTestTab(parentId, portalId, "Parent", hasChildren: true);
        var childTab = CreateTestTab(tabId, portalId, "Child", parentId: parentId);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTab);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>()); // Child has no children

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(parentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>()); // Parent will have no children after delete

        _mockTabRepository
            .Setup(r => r.DeleteAsync(tabId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteTabAsync(tabId, portalId);

        // Assert - verify parent's HasChildren was updated to false
        _mockTabRepository.Verify(r => r.UpdateAsync(It.Is<Tab>(t => 
            t.TabId == parentId && 
            t.HasChildren == false), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateTabOrderAsync Tests

    /// <summary>
    /// Verifies UpdateTabOrderAsync reorders tabs within the same parent level.
    /// MIGRATION: Corresponds to TabController.UpdateTabOrder(objTab) in TabController.vb (lines 816-819).
    /// Original method called DataProvider.Instance().UpdateTabOrder with Order parameter for movement.
    /// </summary>
    [Fact]
    public async Task UpdateTabOrderAsync_ReordersTabsWithinLevel()
    {
        // Arrange
        const int tabId = 2;
        const int portalId = 1;
        var tab = CreateTestTab(tabId, portalId, "Middle Tab", tabOrder: 20);
        var siblings = new List<Tab>
        {
            CreateTestTab(1, portalId, "First Tab", tabOrder: 10),
            CreateTestTab(tabId, portalId, "Middle Tab", tabOrder: 20),
            CreateTestTab(3, portalId, "Last Tab", tabOrder: 30)
        };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(-1, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(siblings);

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - move tab up (order = -1)
        await _sut.UpdateTabOrderAsync(tabId, null, -1);

        // Assert - tabs should swap positions
        _mockTabRepository.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Verifies UpdateTabOrderAsync throws when tab not found.
    /// </summary>
    [Fact]
    public async Task UpdateTabOrderAsync_WithInvalidTabId_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 999;

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.UpdateTabOrderAsync(tabId, null, -1))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*not found*");
    }

    /// <summary>
    /// Verifies UpdateTabOrderAsync does nothing when tab is at boundary.
    /// MIGRATION: Original VB.NET handled edge cases where tab is already first/last.
    /// </summary>
    [Fact]
    public async Task UpdateTabOrderAsync_AtBoundary_DoesNothing()
    {
        // Arrange
        const int tabId = 1;
        const int portalId = 1;
        var tab = CreateTestTab(tabId, portalId, "First Tab", tabOrder: 10);
        var siblings = new List<Tab>
        {
            CreateTestTab(tabId, portalId, "First Tab", tabOrder: 10),
            CreateTestTab(2, portalId, "Second Tab", tabOrder: 20)
        };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(-1, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(siblings);

        // Act - try to move first tab up
        await _sut.UpdateTabOrderAsync(tabId, null, -1);

        // Assert - no updates should occur since tab is already first
        _mockTabRepository.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies UpdateTabOrderAsync handles various order directions.
    /// </summary>
    [Theory]
    [InlineData(-1)] // Move up
    [InlineData(1)]  // Move down
    public async Task UpdateTabOrderAsync_WithVariousDirections_UpdatesCorrectly(int order)
    {
        // Arrange
        const int tabId = 2;
        const int portalId = 1;
        var tab = CreateTestTab(tabId, portalId, "Middle Tab", tabOrder: 20);
        var siblings = new List<Tab>
        {
            CreateTestTab(1, portalId, "First Tab", tabOrder: 10),
            CreateTestTab(tabId, portalId, "Middle Tab", tabOrder: 20),
            CreateTestTab(3, portalId, "Last Tab", tabOrder: 30)
        };

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(-1, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(siblings);

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateTabOrderAsync(tabId, null, order);

        // Assert - two tabs should be updated (swapped)
        _mockTabRepository.Verify(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion

    #region MoveTabAsync Tests

    /// <summary>
    /// Verifies MoveTabAsync moves a tab to a different parent with all descendants.
    /// MIGRATION: Corresponds to TabController.UpdatePortalTabOrder parent change logic (lines 550-778).
    /// Original method located last child position, updated ParentId, called MoveTab for repositioning,
    /// recalculated TabOrder, updated child paths, and cleared cache.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_BetweenParents_MovesTabWithDescendants()
    {
        // Arrange
        const int tabId = 10;
        const int oldParentId = 1;
        const int newParentId = 2;
        const int portalId = 1;

        var tab = CreateTestTab(tabId, portalId, "Moving Tab", parentId: oldParentId, level: 1);
        var newParent = CreateTestTab(newParentId, portalId, "New Parent", level: 0, tabPath: "//New Parent");
        var childTab = CreateTestTab(100, portalId, "Child of Moving", parentId: tabId, level: 2);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(newParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newParent);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(oldParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestTab(oldParentId, portalId, "Old Parent"));

        _mockTabRepository
            .Setup(r => r.MoveTabAsync(tabId, newParentId, portalId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab> { childTab });

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(oldParentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>()); // No remaining children

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveTabAsync(tabId, newParentId, portalId);

        // Assert
        _mockTabRepository.Verify(r => r.MoveTabAsync(tabId, newParentId, portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies MoveTabAsync can move tab to root level (null parent).
    /// MIGRATION: VB.NET used -1 for root level, null is converted to -1.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_ToRootLevel_UpdatesParentToNull()
    {
        // Arrange
        const int tabId = 10;
        const int oldParentId = 1;
        const int portalId = 1;

        var tab = CreateTestTab(tabId, portalId, "Moving Tab", parentId: oldParentId, level: 1);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(oldParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestTab(oldParentId, portalId, "Old Parent"));

        _mockTabRepository
            .Setup(r => r.MoveTabAsync(tabId, -1, portalId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>());

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(oldParentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>());

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveTabAsync(tabId, null, portalId);

        // Assert - verify move was called with -1 for root level
        _mockTabRepository.Verify(r => r.MoveTabAsync(tabId, -1, portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies MoveTabAsync prevents circular reference (moving tab under own descendant).
    /// MIGRATION: This validation prevents data integrity issues not present in original VB.NET.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_ToOwnDescendant_ThrowsInvalidOperationException()
    {
        // Arrange
        const int parentTabId = 1;
        const int childTabId = 10;
        const int portalId = 1;

        var parentTab = CreateTestTab(parentTabId, portalId, "Parent", level: 0);
        var childTab = CreateTestTab(childTabId, portalId, "Child", parentId: parentTabId, level: 1);

        // When checking if childTabId is a descendant of parentTabId
        _mockTabRepository
            .Setup(r => r.GetByIdAsync(parentTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentTab);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(childTabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTab);

        // Act & Assert - trying to move parent under its own child
        await FluentActions.Invoking(() => _sut.MoveTabAsync(parentTabId, childTabId, portalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*descendant*");
    }

    /// <summary>
    /// Verifies MoveTabAsync throws when tab not found.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_WithInvalidTabId_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 999;
        const int portalId = 1;

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tab?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.MoveTabAsync(tabId, 1, portalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*not found*");
    }

    /// <summary>
    /// Verifies MoveTabAsync throws when tab belongs to different portal.
    /// MIGRATION: Multi-tenant security - prevent cross-portal tab movement.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_WithWrongPortal_ThrowsInvalidOperationException()
    {
        // Arrange
        const int tabId = 10;
        const int requestedPortalId = 1;
        const int actualPortalId = 2;

        var tab = CreateTestTab(tabId, actualPortalId, "Tab");

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        // Act & Assert
        await FluentActions.Invoking(() => _sut.MoveTabAsync(tabId, 1, requestedPortalId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{tabId}*does not belong to portal*{requestedPortalId}*");
    }

    /// <summary>
    /// Verifies MoveTabAsync updates HasChildren flags for both old and new parents.
    /// </summary>
    [Fact]
    public async Task MoveTabAsync_UpdatesHasChildrenFlags()
    {
        // Arrange
        const int tabId = 10;
        const int oldParentId = 1;
        const int newParentId = 2;
        const int portalId = 1;

        var tab = CreateTestTab(tabId, portalId, "Moving Tab", parentId: oldParentId, level: 1);
        var oldParent = CreateTestTab(oldParentId, portalId, "Old Parent", hasChildren: true);
        var newParent = CreateTestTab(newParentId, portalId, "New Parent", hasChildren: false);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tab);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(oldParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldParent);

        _mockTabRepository
            .Setup(r => r.GetByIdAsync(newParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newParent);

        _mockTabRepository
            .Setup(r => r.MoveTabAsync(tabId, newParentId, portalId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(tabId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>());

        _mockTabRepository
            .Setup(r => r.GetChildrenAsync(oldParentId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tab>()); // No remaining children

        _mockTabRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Tab>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveTabAsync(tabId, newParentId, portalId);

        // Assert - verify HasChildren flags were updated
        _mockTabRepository.Verify(r => r.UpdateAsync(It.Is<Tab>(t => 
            t.TabId == oldParentId && t.HasChildren == false), It.IsAny<CancellationToken>()), Times.Once);
        _mockTabRepository.Verify(r => r.UpdateAsync(It.Is<Tab>(t => 
            t.TabId == newParentId && t.HasChildren == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test Tab entity with specified properties.
    /// </summary>
    private static Tab CreateTestTab(
        int tabId,
        int portalId,
        string tabName,
        int? parentId = null,
        int level = 0,
        int tabOrder = 0,
        bool isVisible = true,
        bool isDeleted = false,
        bool hasChildren = false,
        string? tabPath = null)
    {
        return new Tab
        {
            TabId = tabId,
            PortalId = portalId,
            TabName = tabName,
            ParentId = parentId,
            Level = level,
            TabOrder = tabOrder,
            IsVisible = isVisible,
            IsDeleted = isDeleted,
            HasChildren = hasChildren,
            TabPath = tabPath ?? $"//{tabName}",
            Title = tabName,
            Description = $"Description for {tabName}"
        };
    }

    /// <summary>
    /// Creates a test TabDto with specified properties.
    /// </summary>
    private static TabDto CreateTestTabDto(
        int tabId,
        int portalId,
        string tabName,
        int? parentId = null,
        int level = 0,
        int tabOrder = 0,
        bool isVisible = true,
        bool isDeleted = false,
        bool hasChildren = false)
    {
        return new TabDto
        {
            TabId = tabId,
            PortalId = portalId,
            TabName = tabName,
            ParentId = parentId,
            Level = level,
            TabOrder = tabOrder,
            IsVisible = isVisible,
            IsDeleted = isDeleted,
            HasChildren = hasChildren,
            TabPath = $"//{tabName}",
            Title = tabName,
            Description = $"Description for {tabName}"
        };
    }

    #endregion
}
