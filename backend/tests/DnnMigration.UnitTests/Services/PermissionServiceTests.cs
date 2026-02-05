// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Unit tests verifying behavioral equivalence between PermissionService and
// original VB.NET PermissionController.vb (Library/Components/Security/Permissions/PermissionController.vb).
// Test methods map directly to original VB.NET operations:
// - GetPermission(permissionID) → GetPermissionAsync
// - GetPermissionsByModuleDefID(ModuleDefID) → GetPermissionsByModuleDefinitionIdAsync
// - GetPermissionsByModuleID(ModuleID) → GetPermissionsByModuleIdAsync
// - GetPermissionsByFolder(PortalID, Folder) → GetPermissionsByFolderAsync
// - GetPermissionByCodeAndKey(PermissionCode, PermissionKey) → GetPermissionsByCodeAndKeyAsync
// - GetPermissionsByTabID(TabID) → GetPermissionsByTabIdAsync
// - AddPermission(objPermission) → CreatePermissionAsync
// - UpdatePermission(objPermission) → UpdatePermissionAsync
// - DeletePermission(permissionID) → DeletePermissionAsync
// Uses xUnit for test framework, Moq for mocking, FluentAssertions for assertions.
// Target: 85% coverage per Section 0.7.5
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="PermissionService"/> verifying behavioral equivalence
/// with the original VB.NET <c>DotNetNuke.Security.Permissions.PermissionController</c>.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: These tests verify that the migrated C# 12 PermissionService maintains
/// behavioral equivalence with the original VB.NET PermissionController.vb implementation.
/// Each test method maps to original VB.NET operations from Library/Components/Security/Permissions/PermissionController.vb.
/// </para>
/// <para>
/// Test coverage targets per Section 0.7.5:
/// <list type="bullet">
/// <item><description>Application layer: 85% minimum coverage</description></item>
/// </list>
/// </para>
/// <para>
/// Dependencies:
/// <list type="bullet">
/// <item><description>xunit 2.9.2 - Test framework with [Fact] and [Theory] attributes</description></item>
/// <item><description>Moq 4.20.72 - Mock framework for IPermissionRepository and IMapper</description></item>
/// <item><description>FluentAssertions 6.12.2 - Fluent assertion syntax with .Should()</description></item>
/// </list>
/// </para>
/// </remarks>
public class PermissionServiceTests
{
    /// <summary>
    /// Mock instance of <see cref="IPermissionRepository"/> for isolating data access layer.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Replaces DataProvider.Instance() calls from original VB.NET implementation.
    /// Configured to return test data fixtures and verify method invocations.
    /// </remarks>
    private readonly Mock<IPermissionRepository> _mockPermissionRepository;

    /// <summary>
    /// Mock instance of <see cref="IMapper"/> for isolating AutoMapper transformations.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Replaces CBO.FillObject/CBO.FillCollection from original VB.NET implementation.
    /// Configured to simulate entity-to-DTO mapping behavior.
    /// </remarks>
    private readonly Mock<IMapper> _mockMapper;

    /// <summary>
    /// The System Under Test - the PermissionService instance being tested.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests verify behavioral equivalence with VB.NET PermissionController.
    /// </remarks>
    private readonly PermissionService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionServiceTests"/> class.
    /// Sets up mock dependencies and creates the service instance under test.
    /// </summary>
    public PermissionServiceTests()
    {
        _mockPermissionRepository = new Mock<IPermissionRepository>(MockBehavior.Strict);
        _mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        _sut = new PermissionService(_mockPermissionRepository.Object, _mockMapper.Object);
    }

    #region GetPermissionAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionAsync"/> returns a valid
    /// <see cref="PermissionDto"/> when the permission exists in the repository.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermission(permissionID As Integer):
    /// <code>
    /// Return CType(CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo)), PermissionInfo)
    /// </code>
    /// </remarks>
    [Fact]
    public async Task GetPermissionAsync_WithValidId_ReturnsPermissionDto()
    {
        // Arrange
        const int permissionId = 1;
        var permission = CreateTestPermission(permissionId, "SYSTEM_TAB", -1, "VIEW", "View Tab");
        var expectedDto = CreateTestPermissionDto(permissionId, "SYSTEM_TAB", -1, "VIEW", "View Tab");

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(permission))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetPermissionAsync(permissionId);

        // Assert
        result.Should().NotBeNull();
        result!.PermissionId.Should().Be(permissionId);
        result.PermissionCode.Should().Be("SYSTEM_TAB");
        result.ModuleDefId.Should().Be(-1);
        result.PermissionKey.Should().Be("VIEW");
        result.PermissionName.Should().Be("View Tab");

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionAsync"/> returns null
    /// when the permission does not exist in the repository.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies null handling equivalent to VB.NET CType returning Nothing:
    /// <code>
    /// Return CType(CBO.FillObject(DataProvider.Instance().GetPermission(permissionID), GetType(PermissionInfo)), PermissionInfo)
    /// </code>
    /// When GetPermission returns no data, CBO.FillObject returns Nothing, and CType of Nothing is Nothing.
    /// </remarks>
    [Fact]
    public async Task GetPermissionAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int invalidPermissionId = 99999;

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(invalidPermissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);

        // Act
        var result = await _sut.GetPermissionAsync(invalidPermissionId);

        // Assert
        result.Should().BeNull();

        // Verify repository was called
        _mockPermissionRepository.Verify(
            r => r.GetByIdAsync(invalidPermissionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPermissionsByModuleDefinitionIdAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByModuleDefinitionIdAsync"/> returns
    /// a collection of <see cref="PermissionDto"/> for the specified module definition.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermissionsByModuleDefID(ModuleDefID):
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleDefID(ModuleDefID), GetType(PermissionInfo))
    /// </code>
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByModuleDefinitionIdAsync_ReturnsPermissions()
    {
        // Arrange
        const int moduleDefId = 100;
        var permissions = new List<Permission>
        {
            CreateTestPermission(1, "MODULE_DEF", moduleDefId, "VIEW", "View"),
            CreateTestPermission(2, "MODULE_DEF", moduleDefId, "EDIT", "Edit"),
            CreateTestPermission(3, "MODULE_DEF", moduleDefId, "DELETE", "Delete")
        };

        var expectedDtos = new List<PermissionDto>
        {
            CreateTestPermissionDto(1, "MODULE_DEF", moduleDefId, "VIEW", "View"),
            CreateTestPermissionDto(2, "MODULE_DEF", moduleDefId, "EDIT", "Edit"),
            CreateTestPermissionDto(3, "MODULE_DEF", moduleDefId, "DELETE", "Delete")
        };

        _mockPermissionRepository
            .Setup(r => r.GetByModuleDefIdAsync(moduleDefId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(permissions))
            .Returns(expectedDtos);

        // Act
        var result = await _sut.GetPermissionsByModuleDefinitionIdAsync(moduleDefId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedDtos);

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByModuleDefIdAsync(moduleDefId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByModuleDefinitionIdAsync"/> returns
    /// an empty collection when no permissions exist for the specified module definition.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies empty collection handling equivalent to VB.NET CBO.FillCollection
    /// returning an empty ArrayList when no records are found.
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByModuleDefinitionIdAsync_WithNoPermissions_ReturnsEmptyList()
    {
        // Arrange
        const int moduleDefIdWithNoPermissions = 99999;
        var emptyPermissions = new List<Permission>();
        var emptyDtos = new List<PermissionDto>();

        _mockPermissionRepository
            .Setup(r => r.GetByModuleDefIdAsync(moduleDefIdWithNoPermissions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyPermissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(emptyPermissions))
            .Returns(emptyDtos);

        // Act
        var result = await _sut.GetPermissionsByModuleDefinitionIdAsync(moduleDefIdWithNoPermissions);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify repository was called
        _mockPermissionRepository.Verify(
            r => r.GetByModuleDefIdAsync(moduleDefIdWithNoPermissions, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPermissionsByModuleIdAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByModuleIdAsync"/> returns
    /// module-scoped permissions for the specified module instance.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermissionsByModuleID(ModuleID):
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByModuleID(ModuleID), GetType(PermissionInfo))
    /// </code>
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByModuleIdAsync_ReturnsModulePermissions()
    {
        // Arrange
        const int moduleId = 50;
        var permissions = new List<Permission>
        {
            CreateTestPermission(10, "CONTENT_MODULE", 5, "VIEW", "View Content"),
            CreateTestPermission(11, "CONTENT_MODULE", 5, "EDIT", "Edit Content")
        };

        var expectedDtos = new List<PermissionDto>
        {
            CreateTestPermissionDto(10, "CONTENT_MODULE", 5, "VIEW", "View Content"),
            CreateTestPermissionDto(11, "CONTENT_MODULE", 5, "EDIT", "Edit Content")
        };

        _mockPermissionRepository
            .Setup(r => r.GetByModuleIdAsync(moduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(permissions))
            .Returns(expectedDtos);

        // Act
        var result = await _sut.GetPermissionsByModuleIdAsync(moduleId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().PermissionCode.Should().Be("CONTENT_MODULE");

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByModuleIdAsync(moduleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPermissionsByFolderAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByFolderAsync"/> returns
    /// folder-scoped permissions for the specified portal and folder path.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermissionsByFolder(PortalID, Folder):
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByFolderPath(PortalID, Folder), GetType(PermissionInfo))
    /// </code>
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByFolderAsync_WithPortalAndFolder_ReturnsPermissions()
    {
        // Arrange
        const int portalId = 1;
        const string folder = "Images/";
        var permissions = new List<Permission>
        {
            CreateTestPermission(20, "SYSTEM_FOLDER", -1, "READ", "Read Folder"),
            CreateTestPermission(21, "SYSTEM_FOLDER", -1, "WRITE", "Write Folder"),
            CreateTestPermission(22, "SYSTEM_FOLDER", -1, "BROWSE", "Browse Folder")
        };

        var expectedDtos = new List<PermissionDto>
        {
            CreateTestPermissionDto(20, "SYSTEM_FOLDER", -1, "READ", "Read Folder"),
            CreateTestPermissionDto(21, "SYSTEM_FOLDER", -1, "WRITE", "Write Folder"),
            CreateTestPermissionDto(22, "SYSTEM_FOLDER", -1, "BROWSE", "Browse Folder")
        };

        _mockPermissionRepository
            .Setup(r => r.GetByFolderAsync(portalId, folder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(permissions))
            .Returns(expectedDtos);

        // Act
        var result = await _sut.GetPermissionsByFolderAsync(portalId, folder);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.All(p => p.PermissionCode == "SYSTEM_FOLDER").Should().BeTrue();

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByFolderAsync(portalId, folder, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByFolderAsync"/> throws
    /// <see cref="ArgumentNullException"/> when folder parameter is null.
    /// </summary>
    [Fact]
    public async Task GetPermissionsByFolderAsync_WithNullFolder_ThrowsArgumentNullException()
    {
        // Arrange
        const int portalId = 1;
        string? nullFolder = null;

        // Act
        Func<Task> act = async () => await _sut.GetPermissionsByFolderAsync(portalId, nullFolder!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("folder");
    }

    #endregion

    #region GetPermissionsByCodeAndKeyAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByCodeAndKeyAsync"/> returns
    /// permissions matching the specified permission code and key.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermissionByCodeAndKey(PermissionCode, PermissionKey):
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionByCodeAndKey(PermissionCode, PermissionKey), GetType(PermissionInfo))
    /// </code>
    /// </remarks>
    [Theory]
    [InlineData("SYSTEM_TAB", "VIEW")]
    [InlineData("CONTENT_MODULE", "EDIT")]
    [InlineData("SYSTEM_FOLDER", "BROWSE")]
    public async Task GetPermissionsByCodeAndKeyAsync_ReturnsMatchingPermissions(
        string permissionCode, 
        string permissionKey)
    {
        // Arrange
        var permissions = new List<Permission>
        {
            CreateTestPermission(30, permissionCode, -1, permissionKey, $"{permissionKey} Permission")
        };

        var expectedDtos = new List<PermissionDto>
        {
            CreateTestPermissionDto(30, permissionCode, -1, permissionKey, $"{permissionKey} Permission")
        };

        _mockPermissionRepository
            .Setup(r => r.GetByCodeAndKeyAsync(permissionCode, permissionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(permissions))
            .Returns(expectedDtos);

        // Act
        var result = await _sut.GetPermissionsByCodeAndKeyAsync(permissionCode, permissionKey);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.First().PermissionCode.Should().Be(permissionCode);
        result.First().PermissionKey.Should().Be(permissionKey);

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByCodeAndKeyAsync(permissionCode, permissionKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByCodeAndKeyAsync"/> returns
    /// an empty collection when no permissions match the specified code and key.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies empty collection handling for non-existent permission codes,
    /// equivalent to VB.NET CBO.FillCollection returning empty ArrayList.
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByCodeAndKeyAsync_WithNonExistentCode_ReturnsEmptyList()
    {
        // Arrange
        const string nonExistentCode = "NON_EXISTENT_CODE";
        const string nonExistentKey = "NON_EXISTENT_KEY";
        var emptyPermissions = new List<Permission>();
        var emptyDtos = new List<PermissionDto>();

        _mockPermissionRepository
            .Setup(r => r.GetByCodeAndKeyAsync(nonExistentCode, nonExistentKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyPermissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(emptyPermissions))
            .Returns(emptyDtos);

        // Act
        var result = await _sut.GetPermissionsByCodeAndKeyAsync(nonExistentCode, nonExistentKey);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        // Verify repository was called
        _mockPermissionRepository.Verify(
            r => r.GetByCodeAndKeyAsync(nonExistentCode, nonExistentKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByCodeAndKeyAsync"/> throws
    /// <see cref="ArgumentNullException"/> when permissionCode is null.
    /// </summary>
    [Fact]
    public async Task GetPermissionsByCodeAndKeyAsync_WithNullCode_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullCode = null;
        const string validKey = "VIEW";

        // Act
        Func<Task> act = async () => await _sut.GetPermissionsByCodeAndKeyAsync(nullCode!, validKey);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("permissionCode");
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByCodeAndKeyAsync"/> throws
    /// <see cref="ArgumentNullException"/> when permissionKey is null.
    /// </summary>
    [Fact]
    public async Task GetPermissionsByCodeAndKeyAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        const string validCode = "SYSTEM_TAB";
        string? nullKey = null;

        // Act
        Func<Task> act = async () => await _sut.GetPermissionsByCodeAndKeyAsync(validCode, nullKey!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("permissionKey");
    }

    #endregion

    #region GetPermissionsByTabIdAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionsByTabIdAsync"/> returns
    /// tab-scoped permissions for the specified tab.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPermissionsByTabID(TabID):
    /// <code>
    /// Return CBO.FillCollection(DataProvider.Instance().GetPermissionsByTabID(TabID), GetType(PermissionInfo))
    /// </code>
    /// </remarks>
    [Fact]
    public async Task GetPermissionsByTabIdAsync_ReturnsTabPermissions()
    {
        // Arrange
        const int tabId = 75;
        var permissions = new List<Permission>
        {
            CreateTestPermission(40, "SYSTEM_TAB", -1, "VIEW", "View Tab"),
            CreateTestPermission(41, "SYSTEM_TAB", -1, "EDIT", "Edit Tab"),
            CreateTestPermission(42, "SYSTEM_TAB", -1, "ADD", "Add Tab"),
            CreateTestPermission(43, "SYSTEM_TAB", -1, "DELETE", "Delete Tab")
        };

        var expectedDtos = new List<PermissionDto>
        {
            CreateTestPermissionDto(40, "SYSTEM_TAB", -1, "VIEW", "View Tab"),
            CreateTestPermissionDto(41, "SYSTEM_TAB", -1, "EDIT", "Edit Tab"),
            CreateTestPermissionDto(42, "SYSTEM_TAB", -1, "ADD", "Add Tab"),
            CreateTestPermissionDto(43, "SYSTEM_TAB", -1, "DELETE", "Delete Tab")
        };

        _mockPermissionRepository
            .Setup(r => r.GetByTabIdAsync(tabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PermissionDto>>(permissions))
            .Returns(expectedDtos);

        // Act
        var result = await _sut.GetPermissionsByTabIdAsync(tabId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.All(p => p.PermissionCode == "SYSTEM_TAB").Should().BeTrue();

        // Verify repository was called with correct parameters
        _mockPermissionRepository.Verify(
            r => r.GetByTabIdAsync(tabId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreatePermissionAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.CreatePermissionAsync"/> creates a new permission
    /// and returns the created <see cref="PermissionDto"/>.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET AddPermission(objPermission):
    /// <code>
    /// Return CType(DataProvider.Instance().AddPermission(objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName), Integer)
    /// </code>
    /// The original returned only the new permission ID as Integer. The modern implementation
    /// returns the complete PermissionDto for richer API responses.
    /// </remarks>
    [Fact]
    public async Task CreatePermissionAsync_WithValidRequest_CreatesAndReturnsDto()
    {
        // Arrange
        var request = new CreatePermissionRequest(
            PermissionCode: "NEW_MODULE",
            ModuleDefId: 200,
            PermissionKey: "MANAGE",
            PermissionName: "Manage Module"
        );

        var createdPermission = new Permission
        {
            PermissionId = 100,
            PermissionCode = request.PermissionCode,
            ModuleDefId = request.ModuleDefId,
            PermissionKey = request.PermissionKey,
            PermissionName = request.PermissionName
        };

        var expectedDto = CreateTestPermissionDto(
            100,
            request.PermissionCode,
            request.ModuleDefId,
            request.PermissionKey,
            request.PermissionName
        );

        _mockPermissionRepository
            .Setup(r => r.AddAsync(It.Is<Permission>(p =>
                p.PermissionCode == request.PermissionCode &&
                p.ModuleDefId == request.ModuleDefId &&
                p.PermissionKey == request.PermissionKey &&
                p.PermissionName == request.PermissionName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPermission);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(createdPermission))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreatePermissionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PermissionId.Should().Be(100);
        result.PermissionCode.Should().Be(request.PermissionCode);
        result.ModuleDefId.Should().Be(request.ModuleDefId);
        result.PermissionKey.Should().Be(request.PermissionKey);
        result.PermissionName.Should().Be(request.PermissionName);

        // Verify repository AddAsync was called with correct permission data
        _mockPermissionRepository.Verify(
            r => r.AddAsync(It.Is<Permission>(p =>
                p.PermissionCode == request.PermissionCode &&
                p.ModuleDefId == request.ModuleDefId &&
                p.PermissionKey == request.PermissionKey &&
                p.PermissionName == request.PermissionName),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.CreatePermissionAsync"/> throws
    /// <see cref="ArgumentNullException"/> when request is null.
    /// </summary>
    [Fact]
    public async Task CreatePermissionAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        CreatePermissionRequest? nullRequest = null;

        // Act
        Func<Task> act = async () => await _sut.CreatePermissionAsync(nullRequest!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    #endregion

    #region UpdatePermissionAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.UpdatePermissionAsync"/> updates an existing
    /// permission and returns the updated <see cref="PermissionDto"/>.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET UpdatePermission(objPermission):
    /// <code>
    /// DataProvider.Instance().UpdatePermission(objPermission.PermissionID, objPermission.PermissionCode, objPermission.ModuleDefID, objPermission.PermissionKey, objPermission.PermissionName)
    /// </code>
    /// The original was a Sub (void). The modern implementation returns PermissionDto for API response.
    /// </remarks>
    [Fact]
    public async Task UpdatePermissionAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int permissionId = 50;
        var existingPermission = CreateTestPermission(permissionId, "OLD_CODE", 10, "OLD_KEY", "Old Name");

        var updateRequest = new UpdatePermissionRequest(
            PermissionCode: "NEW_CODE",
            ModuleDefId: 20,
            PermissionKey: "NEW_KEY",
            PermissionName: "New Name"
        );

        var updatedDto = CreateTestPermissionDto(
            permissionId,
            updateRequest.PermissionCode!,
            updateRequest.ModuleDefId!.Value,
            updateRequest.PermissionKey!,
            updateRequest.PermissionName!
        );

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPermission);

        _mockPermissionRepository
            .Setup(r => r.UpdateAsync(It.Is<Permission>(p =>
                p.PermissionId == permissionId &&
                p.PermissionCode == updateRequest.PermissionCode &&
                p.ModuleDefId == updateRequest.ModuleDefId &&
                p.PermissionKey == updateRequest.PermissionKey &&
                p.PermissionName == updateRequest.PermissionName),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(It.Is<Permission>(p =>
                p.PermissionId == permissionId &&
                p.PermissionCode == updateRequest.PermissionCode)))
            .Returns(updatedDto);

        // Act
        var result = await _sut.UpdatePermissionAsync(permissionId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.PermissionId.Should().Be(permissionId);
        result.PermissionCode.Should().Be(updateRequest.PermissionCode);
        result.ModuleDefId.Should().Be(updateRequest.ModuleDefId);
        result.PermissionKey.Should().Be(updateRequest.PermissionKey);
        result.PermissionName.Should().Be(updateRequest.PermissionName);

        // Verify repository methods were called
        _mockPermissionRepository.Verify(
            r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockPermissionRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.UpdatePermissionAsync"/> supports partial updates
    /// by only updating non-null properties from the request.
    /// </summary>
    [Fact]
    public async Task UpdatePermissionAsync_WithPartialRequest_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        const int permissionId = 60;
        var existingPermission = CreateTestPermission(permissionId, "EXISTING_CODE", 15, "EXISTING_KEY", "Existing Name");

        // Only update PermissionName, leave others as null
        var partialRequest = new UpdatePermissionRequest(
            PermissionCode: null,
            ModuleDefId: null,
            PermissionKey: null,
            PermissionName: "Updated Name Only"
        );

        var expectedDto = CreateTestPermissionDto(
            permissionId,
            existingPermission.PermissionCode!,
            existingPermission.ModuleDefId,
            existingPermission.PermissionKey!,
            partialRequest.PermissionName!
        );

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPermission);

        _mockPermissionRepository
            .Setup(r => r.UpdateAsync(It.Is<Permission>(p =>
                p.PermissionId == permissionId &&
                p.PermissionCode == existingPermission.PermissionCode &&
                p.ModuleDefId == existingPermission.ModuleDefId &&
                p.PermissionKey == existingPermission.PermissionKey &&
                p.PermissionName == partialRequest.PermissionName),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(It.Is<Permission>(p =>
                p.PermissionName == partialRequest.PermissionName)))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdatePermissionAsync(permissionId, partialRequest);

        // Assert
        result.Should().NotBeNull();
        result.PermissionCode.Should().Be(existingPermission.PermissionCode);
        result.ModuleDefId.Should().Be(existingPermission.ModuleDefId);
        result.PermissionKey.Should().Be(existingPermission.PermissionKey);
        result.PermissionName.Should().Be(partialRequest.PermissionName);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.UpdatePermissionAsync"/> throws
    /// <see cref="InvalidOperationException"/> when the permission does not exist.
    /// </summary>
    [Fact]
    public async Task UpdatePermissionAsync_WithNonExistentPermission_ThrowsInvalidOperationException()
    {
        // Arrange
        const int nonExistentId = 99999;
        var updateRequest = new UpdatePermissionRequest(PermissionCode: "UPDATED_CODE");

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Permission?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdatePermissionAsync(nonExistentId, updateRequest);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Permission with ID {nonExistentId} not found.");

        // Verify repository GetByIdAsync was called
        _mockPermissionRepository.Verify(
            r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify UpdateAsync was NOT called
        _mockPermissionRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.UpdatePermissionAsync"/> throws
    /// <see cref="ArgumentNullException"/> when request is null.
    /// </summary>
    [Fact]
    public async Task UpdatePermissionAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        const int permissionId = 1;
        UpdatePermissionRequest? nullRequest = null;

        // Act
        Func<Task> act = async () => await _sut.UpdatePermissionAsync(permissionId, nullRequest!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    #endregion

    #region DeletePermissionAsync Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.DeletePermissionAsync"/> deletes the permission
    /// with the specified ID by calling the repository.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Verifies behavioral equivalence with VB.NET DeletePermission(permissionID):
    /// <code>
    /// DataProvider.Instance().DeletePermission(permissionID)
    /// </code>
    /// </remarks>
    [Fact]
    public async Task DeletePermissionAsync_WithValidId_DeletesPermission()
    {
        // Arrange
        const int permissionId = 25;

        _mockPermissionRepository
            .Setup(r => r.DeleteAsync(permissionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeletePermissionAsync(permissionId);

        // Assert - Verify repository DeleteAsync was called with correct ID
        _mockPermissionRepository.Verify(
            r => r.DeleteAsync(permissionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CancellationToken Propagation Tests

    /// <summary>
    /// Tests that <see cref="PermissionService.GetPermissionAsync"/> propagates the
    /// <see cref="CancellationToken"/> to the repository layer.
    /// </summary>
    [Fact]
    public async Task GetPermissionAsync_PropagatesCancellationToken()
    {
        // Arrange
        const int permissionId = 1;
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var permission = CreateTestPermission(permissionId, "TEST", -1, "TEST", "Test");
        var expectedDto = CreateTestPermissionDto(permissionId, "TEST", -1, "TEST", "Test");

        _mockPermissionRepository
            .Setup(r => r.GetByIdAsync(permissionId, cancellationToken))
            .ReturnsAsync(permission);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(permission))
            .Returns(expectedDto);

        // Act
        await _sut.GetPermissionAsync(permissionId, cancellationToken);

        // Assert - Verify the exact CancellationToken was passed
        _mockPermissionRepository.Verify(
            r => r.GetByIdAsync(permissionId, cancellationToken),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.CreatePermissionAsync"/> propagates the
    /// <see cref="CancellationToken"/> to the repository layer.
    /// </summary>
    [Fact]
    public async Task CreatePermissionAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var request = new CreatePermissionRequest("CODE", 1, "KEY", "Name");
        var createdPermission = CreateTestPermission(1, "CODE", 1, "KEY", "Name");
        var expectedDto = CreateTestPermissionDto(1, "CODE", 1, "KEY", "Name");

        _mockPermissionRepository
            .Setup(r => r.AddAsync(It.IsAny<Permission>(), cancellationToken))
            .ReturnsAsync(createdPermission);

        _mockMapper
            .Setup(m => m.Map<PermissionDto>(createdPermission))
            .Returns(expectedDto);

        // Act
        await _sut.CreatePermissionAsync(request, cancellationToken);

        // Assert - Verify the exact CancellationToken was passed
        _mockPermissionRepository.Verify(
            r => r.AddAsync(It.IsAny<Permission>(), cancellationToken),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="PermissionService.DeletePermissionAsync"/> propagates the
    /// <see cref="CancellationToken"/> to the repository layer.
    /// </summary>
    [Fact]
    public async Task DeletePermissionAsync_PropagatesCancellationToken()
    {
        // Arrange
        const int permissionId = 1;
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _mockPermissionRepository
            .Setup(r => r.DeleteAsync(permissionId, cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeletePermissionAsync(permissionId, cancellationToken);

        // Assert - Verify the exact CancellationToken was passed
        _mockPermissionRepository.Verify(
            r => r.DeleteAsync(permissionId, cancellationToken),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test <see cref="Permission"/> entity with the specified values.
    /// </summary>
    /// <param name="permissionId">The permission ID.</param>
    /// <param name="permissionCode">The permission code.</param>
    /// <param name="moduleDefId">The module definition ID.</param>
    /// <param name="permissionKey">The permission key.</param>
    /// <param name="permissionName">The permission name.</param>
    /// <returns>A configured <see cref="Permission"/> instance.</returns>
    private static Permission CreateTestPermission(
        int permissionId,
        string permissionCode,
        int moduleDefId,
        string permissionKey,
        string permissionName)
    {
        return new Permission
        {
            PermissionId = permissionId,
            PermissionCode = permissionCode,
            ModuleDefId = moduleDefId,
            PermissionKey = permissionKey,
            PermissionName = permissionName
        };
    }

    /// <summary>
    /// Creates a test <see cref="PermissionDto"/> with the specified values.
    /// </summary>
    /// <param name="permissionId">The permission ID.</param>
    /// <param name="permissionCode">The permission code.</param>
    /// <param name="moduleDefId">The module definition ID.</param>
    /// <param name="permissionKey">The permission key.</param>
    /// <param name="permissionName">The permission name.</param>
    /// <returns>A configured <see cref="PermissionDto"/> instance.</returns>
    private static PermissionDto CreateTestPermissionDto(
        int permissionId,
        string permissionCode,
        int moduleDefId,
        string permissionKey,
        string permissionName)
    {
        return new PermissionDto(
            PermissionId: permissionId,
            PermissionCode: permissionCode,
            ModuleDefId: moduleDefId,
            PermissionKey: permissionKey,
            PermissionName: permissionName
        );
    }

    #endregion
}
