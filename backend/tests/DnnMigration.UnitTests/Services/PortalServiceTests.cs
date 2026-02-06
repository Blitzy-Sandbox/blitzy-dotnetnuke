// -----------------------------------------------------------------------------
// DnnMigration - Unit Tests for PortalService
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Tests verify behavioral equivalence between PortalService (C# 12) and
// the original DNN 4.x PortalController.vb portal management operations.
// Source Reference: Library/Components/Portal/PortalController.vb
// Target Coverage: 85% per Section 0.7.5 Application layer requirements
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Comprehensive unit test class for <see cref="PortalService"/>.
/// Verifies behavioral equivalence to legacy VB.NET PortalController.vb operations.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This test class validates that the migrated PortalService in C# 12
/// produces identical outcomes for identical inputs compared to the original
/// DotNetNuke 4.x PortalController.vb implementation.
/// </para>
/// <para>
/// Key behaviors tested:
/// <list type="bullet">
///   <item><description>Portal CRUD operations (Create, Read, Update, Delete)</description></item>
///   <item><description>Portal creation workflow including admin user and role setup</description></item>
///   <item><description>Business rule: Cannot delete last portal in system</description></item>
///   <item><description>Space quota management and validation</description></item>
/// </list>
/// </para>
/// </remarks>
[Collection("PortalServiceTests")]
public class PortalServiceTests
{
    #region Private Fields

    /// <summary>
    /// Mock for IPortalRepository used to simulate portal data access operations.
    /// </summary>
    private readonly Mock<IPortalRepository> _mockPortalRepository;

    /// <summary>
    /// Mock for IUserService used to simulate user management operations during portal creation.
    /// MIGRATION: PortalController.vb called UserController.CreateUser for admin setup.
    /// </summary>
    private readonly Mock<IUserService> _mockUserService;

    /// <summary>
    /// Mock for IRoleService used to simulate role management operations during portal creation.
    /// MIGRATION: PortalController.vb created Administrators and Registered Users roles.
    /// </summary>
    private readonly Mock<IRoleService> _mockRoleService;

    /// <summary>
    /// Mock for IMapper used to simulate AutoMapper entity-to-DTO transformations.
    /// </summary>
    private readonly Mock<IMapper> _mockMapper;

    /// <summary>
    /// Mock for ILogger used to verify logging behavior.
    /// </summary>
    private readonly Mock<ILogger<PortalService>> _mockLogger;

    /// <summary>
    /// System Under Test - the PortalService instance being tested.
    /// </summary>
    private readonly PortalService _sut;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes the test class with mock dependencies and creates the SUT.
    /// </summary>
    public PortalServiceTests()
    {
        // Initialize mocks with Strict behavior to ensure all calls are explicitly setup
        _mockPortalRepository = new Mock<IPortalRepository>(MockBehavior.Strict);
        _mockUserService = new Mock<IUserService>(MockBehavior.Strict);
        _mockRoleService = new Mock<IRoleService>(MockBehavior.Strict);
        _mockMapper = new Mock<IMapper>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<PortalService>>();

        // Create the System Under Test with injected mock dependencies
        _sut = new PortalService(
            _mockPortalRepository.Object,
            _mockUserService.Object,
            _mockRoleService.Object,
            _mockMapper.Object,
            _mockLogger.Object);
    }

    #endregion

    #region GetPortalAsync Tests

    /// <summary>
    /// Verifies that GetPortalAsync returns a PortalDto when a valid portal ID is provided.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior equivalent to VB.NET PortalController.GetPortal(portalId).
    /// Original returned PortalInfo; migrated version returns PortalDto via AutoMapper.
    /// </remarks>
    [Fact]
    public async Task GetPortalAsync_WithValidId_ReturnsPortalDto()
    {
        // Arrange
        const int portalId = 1;
        var portal = CreateTestPortal(portalId, "Test Portal");
        var expectedDto = CreateTestPortalDto(portalId, "Test Portal");

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portal);

        _mockMapper
            .Setup(m => m.Map<PortalDto>(portal))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetPortalAsync(portalId);

        // Assert
        result.Should().NotBeNull();
        result!.PortalId.Should().Be(portalId);
        result.PortalName.Should().Be("Test Portal");

        _mockPortalRepository.VerifyAll();
        _mockMapper.VerifyAll();
    }

    /// <summary>
    /// Verifies that GetPortalAsync returns null when an invalid/non-existent portal ID is provided.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior equivalent to VB.NET PortalController.GetPortal returning Nothing
    /// when portal is not found in database.
    /// </remarks>
    [Fact]
    public async Task GetPortalAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int invalidPortalId = 999;

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(invalidPortalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portal?)null);

        // Act
        var result = await _sut.GetPortalAsync(invalidPortalId);

        // Assert
        result.Should().BeNull();

        _mockPortalRepository.VerifyAll();
    }

    #endregion

    #region GetPortalsAsync Tests

    /// <summary>
    /// Verifies that GetPortalsAsync returns a properly paginated result.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior derived from VB.NET PortalController.GetPortals methods.
    /// Original used ByRef totalRecords pattern; migrated uses PagedResult wrapper.
    /// </remarks>
    [Fact]
    public async Task GetPortalsAsync_WithPagination_ReturnsPagedResult()
    {
        // Arrange
        const int pageIndex = 0;
        const int pageSize = 10;
        const int totalCount = 25;

        var portals = new List<Portal>
        {
            CreateTestPortal(1, "Portal 1"),
            CreateTestPortal(2, "Portal 2"),
            CreateTestPortal(3, "Portal 3")
        };

        var portalDtos = new List<PortalDto>
        {
            CreateTestPortalDto(1, "Portal 1"),
            CreateTestPortalDto(2, "Portal 2"),
            CreateTestPortalDto(3, "Portal 3")
        };

        _mockPortalRepository
            .Setup(r => r.GetPagedAsync(pageIndex, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((portals.AsEnumerable(), totalCount));

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PortalDto>>(portals))
            .Returns(portalDtos);

        // Act
        var result = await _sut.GetPortalsAsync(pageIndex, pageSize);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.TotalCount.Should().Be(totalCount);
        result.TotalPages.Should().Be(3); // ceil(25/10) = 3
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();

        _mockPortalRepository.VerifyAll();
        _mockMapper.VerifyAll();
    }

    #endregion

    #region GetPortalsByNameAsync Tests

    /// <summary>
    /// Verifies that GetPortalsByNameAsync returns filtered results matching the provided name.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates portal name filtering functionality. Original VB.NET PortalController
    /// had GetPortalsByName that filtered by portal name for multi-tenant scenarios.
    /// </remarks>
    [Fact]
    public async Task GetPortalsByNameAsync_WithMatchingName_ReturnsFilteredResults()
    {
        // Arrange
        const string searchName = "Test";
        var portals = new List<Portal>
        {
            CreateTestPortal(1, "Test Portal 1"),
            CreateTestPortal(2, "Test Portal 2")
        };

        var portalDtos = new List<PortalDto>
        {
            CreateTestPortalDto(1, "Test Portal 1"),
            CreateTestPortalDto(2, "Test Portal 2")
        };

        _mockPortalRepository
            .Setup(r => r.GetByNameAsync(searchName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portals);

        _mockMapper
            .Setup(m => m.Map<IEnumerable<PortalDto>>(portals))
            .Returns(portalDtos);

        // Act
        var result = await _sut.GetPortalsByNameAsync(searchName);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(p => p.PortalName.Contains("Test")).Should().BeTrue();

        _mockPortalRepository.VerifyAll();
        _mockMapper.VerifyAll();
    }

    #endregion

    #region CreatePortalAsync Tests

    /// <summary>
    /// Verifies that CreatePortalAsync creates a portal and returns the DTO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior equivalent to VB.NET PortalController.CreatePortal.
    /// Original method (lines 235-430) created portal record, roles, and admin user.
    /// </remarks>
    [Fact]
    public async Task CreatePortalAsync_WithValidRequest_CreatesPortalAndReturnsDto()
    {
        // Arrange
        var request = CreateTestCreatePortalRequest();
        var createdPortal = CreateTestPortal(1, request.Title);
        var adminRole = CreateTestRoleDto(1, "Administrators", 1);
        var registeredRole = CreateTestRoleDto(2, "Registered Users", 1);
        var adminUser = CreateTestUserDto(1, request.Username);
        var expectedDto = CreateTestPortalDto(1, request.Title);

        // Setup portal creation
        _mockPortalRepository
            .Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPortal);

        // Setup role creation (Administrators and Registered Users)
        _mockRoleService
            .Setup(s => s.CreateRoleAsync(It.Is<CreateRoleRequest>(r => r.RoleName == "Administrators"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminRole);

        _mockRoleService
            .Setup(s => s.CreateRoleAsync(It.Is<CreateRoleRequest>(r => r.RoleName == "Registered Users"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registeredRole);

        // Setup admin user creation
        _mockUserService
            .Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Setup adding admin user to admin role
        _mockRoleService
            .Setup(s => s.AddUserToRoleAsync(
                createdPortal.PortalId,
                adminUser.UserId,
                adminRole.RoleId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup portal update with admin references
        _mockPortalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup final mapping
        _mockMapper
            .Setup(m => m.Map<PortalDto>(It.IsAny<Portal>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreatePortalAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PortalId.Should().Be(1);
        result.PortalName.Should().Be(request.Title);

        _mockPortalRepository.Verify(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRoleService.Verify(s => s.CreateRoleAsync(It.IsAny<CreateRoleRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Verifies that CreatePortalAsync creates an administrator user as part of portal creation workflow.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates admin user creation from VB.NET PortalController.CreatePortal (line 350).
    /// Original code: objUser = UserController.CreateUser(PortalId, objAdminUser)
    /// </remarks>
    [Fact]
    public async Task CreatePortalAsync_WithAdministratorSetup_CreatesAdminUser()
    {
        // Arrange
        var request = CreateTestCreatePortalRequest();
        var createdPortal = CreateTestPortal(1, request.Title);
        var adminRole = CreateTestRoleDto(1, "Administrators", 1);
        var registeredRole = CreateTestRoleDto(2, "Registered Users", 1);
        var adminUser = CreateTestUserDto(1, request.Username);
        var expectedDto = CreateTestPortalDto(1, request.Title);

        // Setup portal creation
        _mockPortalRepository
            .Setup(r => r.AddAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPortal);

        // Setup role creation
        _mockRoleService
            .Setup(s => s.CreateRoleAsync(It.Is<CreateRoleRequest>(r => r.RoleName == "Administrators"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminRole);

        _mockRoleService
            .Setup(s => s.CreateRoleAsync(It.Is<CreateRoleRequest>(r => r.RoleName == "Registered Users"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registeredRole);

        // Setup admin user creation with verification capture
        CreateUserRequest? capturedUserRequest = null;
        _mockUserService
            .Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateUserRequest, CancellationToken>((req, _) => capturedUserRequest = req)
            .ReturnsAsync(adminUser);

        // Setup adding admin to role
        _mockRoleService
            .Setup(s => s.AddUserToRoleAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup portal update
        _mockPortalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup mapping
        _mockMapper
            .Setup(m => m.Map<PortalDto>(It.IsAny<Portal>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreatePortalAsync(request);

        // Assert
        result.Should().NotBeNull();
        
        // Verify admin user was created with correct details from request
        _mockUserService.Verify(
            s => s.CreateUserAsync(It.Is<CreateUserRequest>(r => 
                r.Username == request.Username &&
                r.Email == request.Email &&
                r.FirstName == request.FirstName &&
                r.LastName == request.LastName),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify admin user was added to admin role
        _mockRoleService.Verify(
            s => s.AddUserToRoleAsync(
                createdPortal.PortalId,
                adminUser.UserId,
                adminRole.RoleId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpdatePortalAsync Tests

    /// <summary>
    /// Verifies that UpdatePortalAsync updates the portal and returns the updated DTO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior equivalent to VB.NET PortalController.UpdatePortalInfo.
    /// Original method updated portal record and cleared cache.
    /// </remarks>
    [Fact]
    public async Task UpdatePortalAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int portalId = 1;
        var existingPortal = CreateTestPortal(portalId, "Original Name");
        var updateRequest = CreateTestUpdatePortalRequest("Updated Name");
        var expectedDto = CreateTestPortalDto(portalId, "Updated Name");

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPortal);

        _mockPortalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<PortalDto>(It.IsAny<Portal>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdatePortalAsync(portalId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.PortalId.Should().Be(portalId);
        result.PortalName.Should().Be("Updated Name");

        _mockPortalRepository.Verify(r => r.UpdateAsync(It.IsAny<Portal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeletePortalAsync Tests

    /// <summary>
    /// Verifies that DeletePortalAsync throws an exception when attempting to delete the last portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates business rule from VB.NET PortalController.DeletePortal (line 212):
    /// "If objPortals.GetPortals.Count > 1 Then" - prevents deletion of last portal.
    /// </remarks>
    [Fact]
    public async Task DeletePortalAsync_WithSinglePortal_ThrowsException()
    {
        // Arrange
        const int portalId = 1;
        var existingPortal = CreateTestPortal(portalId, "Last Portal");

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPortal);

        _mockPortalRepository
            .Setup(r => r.GetPortalCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Only one portal exists - cannot delete

        // Act
        Func<Task> act = async () => await _sut.DeletePortalAsync(portalId);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot delete*last*portal*");

        _mockPortalRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that DeletePortalAsync successfully deletes a portal when multiple portals exist.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates successful deletion path from VB.NET PortalController.DeletePortal.
    /// Original method deleted portal when PortalCount > 1.
    /// </remarks>
    [Fact]
    public async Task DeletePortalAsync_WithMultiplePortals_DeletesSuccessfully()
    {
        // Arrange
        const int portalId = 2;
        var existingPortal = CreateTestPortal(portalId, "Second Portal");

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPortal);

        _mockPortalRepository
            .Setup(r => r.GetPortalCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // Multiple portals exist - can delete

        _mockPortalRepository
            .Setup(r => r.DeleteAsync(portalId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = async () => await _sut.DeletePortalAsync(portalId);

        // Assert
        await act.Should().NotThrowAsync();

        _mockPortalRepository.Verify(r => r.DeleteAsync(portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetPortalSpaceUsedAsync Tests

    /// <summary>
    /// Verifies that GetPortalSpaceUsedAsync returns the storage bytes used by the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior equivalent to VB.NET PortalController.GetPortalSpaceUsedBytes.
    /// Original method calculated total file storage used by portal.
    /// </remarks>
    [Fact]
    public async Task GetPortalSpaceUsedAsync_ReturnsStorageBytes()
    {
        // Arrange
        const int portalId = 1;
        const long expectedSpaceUsed = 1048576L; // 1 MB in bytes

        _mockPortalRepository
            .Setup(r => r.GetSpaceUsedAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSpaceUsed);

        // Act
        var result = await _sut.GetPortalSpaceUsedAsync(portalId);

        // Assert
        result.Should().Be(expectedSpaceUsed);

        _mockPortalRepository.VerifyAll();
    }

    #endregion

    #region HasSpaceAvailableAsync Tests

    /// <summary>
    /// Verifies that HasSpaceAvailableAsync returns true when the file size is within quota.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates behavior from VB.NET PortalController.HasSpaceAvailable (line 488):
    /// "If (CurrentSpace + FileSize) < HostSpace Then HasSpaceAvailable = True"
    /// </remarks>
    [Fact]
    public async Task HasSpaceAvailableAsync_WithinQuota_ReturnsTrue()
    {
        // Arrange
        const int portalId = 1;
        const long fileSize = 524288L; // 0.5 MB
        const int hostSpace = 100; // 100 MB quota
        const long currentSpaceUsed = 10485760L; // 10 MB used

        var portal = CreateTestPortal(portalId, "Test Portal", hostSpace);

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portal);

        _mockPortalRepository
            .Setup(r => r.GetSpaceUsedAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentSpaceUsed);

        // Act
        var result = await _sut.HasSpaceAvailableAsync(portalId, fileSize);

        // Assert
        // (10 MB used + 0.5 MB new) < 100 MB quota = true
        result.Should().BeTrue();

        _mockPortalRepository.VerifyAll();
    }

    /// <summary>
    /// Verifies that HasSpaceAvailableAsync returns false when the file size exceeds quota.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates quota exceeded scenario from VB.NET PortalController.HasSpaceAvailable.
    /// Original returned False when (CurrentSpace + FileSize) >= HostSpace.
    /// </remarks>
    [Fact]
    public async Task HasSpaceAvailableAsync_ExceedsQuota_ReturnsFalse()
    {
        // Arrange
        const int portalId = 1;
        const long fileSize = 52428800L; // 50 MB
        const int hostSpace = 50; // 50 MB quota
        const long currentSpaceUsed = 10485760L; // 10 MB already used

        var portal = CreateTestPortal(portalId, "Test Portal", hostSpace);

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portal);

        _mockPortalRepository
            .Setup(r => r.GetSpaceUsedAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentSpaceUsed);

        // Act
        var result = await _sut.HasSpaceAvailableAsync(portalId, fileSize);

        // Assert
        // (10 MB used + 50 MB new) >= 50 MB quota = false
        result.Should().BeFalse();

        _mockPortalRepository.VerifyAll();
    }

    /// <summary>
    /// Verifies that HasSpaceAvailableAsync returns true when HostSpace is 0 (unlimited).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Validates unlimited space scenario from VB.NET PortalController.HasSpaceAvailable.
    /// Original code: "If HostSpace = 0 Then HasSpaceAvailable = True" (HostSpace = 0 means unlimited).
    /// </remarks>
    [Fact]
    public async Task HasSpaceAvailableAsync_WithUnlimitedSpace_ReturnsTrue()
    {
        // Arrange
        const int portalId = 1;
        const long fileSize = 1073741824L; // 1 GB
        const int hostSpace = 0; // 0 = unlimited

        var portal = CreateTestPortal(portalId, "Test Portal", hostSpace);

        _mockPortalRepository
            .Setup(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portal);

        // Note: GetSpaceUsedAsync should not be called when HostSpace is 0

        // Act
        var result = await _sut.HasSpaceAvailableAsync(portalId, fileSize);

        // Assert
        result.Should().BeTrue();

        _mockPortalRepository.Verify(r => r.GetByIdAsync(portalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test Portal entity for use in test arrangements.
    /// </summary>
    private static Portal CreateTestPortal(int portalId, string portalName, int hostSpace = 100)
    {
        return new Portal
        {
            PortalId = portalId,
            PortalName = portalName,
            Description = $"Description for {portalName}",
            HostSpace = hostSpace,
            PageQuota = 0,
            UserQuota = 0,
            AdministratorId = null,
            AdministratorRoleId = null,
            RegisteredRoleId = null,
            GUID = Guid.NewGuid(),
            HomeDirectory = $"Portals/{portalId}",
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test PortalDto for use in test assertions.
    /// </summary>
    private static PortalDto CreateTestPortalDto(int portalId, string portalName)
    {
        return new PortalDto
        {
            PortalId = portalId,
            PortalName = portalName,
            Description = $"Description for {portalName}",
            HostSpace = 100,
            PageQuota = 0,
            UserQuota = 0,
            HomeDirectory = $"Portals/{portalId}",
            GUID = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Creates a test CreatePortalRequest for portal creation tests.
    /// </summary>
    private static CreatePortalRequest CreateTestCreatePortalRequest()
    {
        return new CreatePortalRequest
        {
            PortalAlias = "testportal.com",
            Title = "Test Portal",
            Description = "A test portal for unit testing",
            FirstName = "Admin",
            LastName = "User",
            Username = "admin",
            Password = "TestPassword123!",
            Email = "admin@testportal.com",
            IsChildPortal = false
        };
    }

    /// <summary>
    /// Creates a test UpdatePortalRequest for portal update tests.
    /// </summary>
    private static UpdatePortalRequest CreateTestUpdatePortalRequest(string portalName)
    {
        return new UpdatePortalRequest
        {
            PortalName = portalName,
            Description = $"Updated description for {portalName}",
            HostSpace = 200,
            PageQuota = 100,
            UserQuota = 50,
            AdministratorId = 1,
            UserRegistration = 2, // Public
            BannerAdvertising = 0,
            TimeZoneOffset = 0
        };
    }

    /// <summary>
    /// Creates a test RoleDto for role-related tests.
    /// </summary>
    private static RoleDto CreateTestRoleDto(int roleId, string roleName, int portalId)
    {
        return new RoleDto
        {
            RoleId = roleId,
            RoleName = roleName,
            PortalId = portalId,
            Description = $"{roleName} role",
            IsPublic = true,
            AutoAssignment = roleName == "Registered Users"
        };
    }

    /// <summary>
    /// Creates a test UserDto for user-related tests.
    /// </summary>
    private static UserDto CreateTestUserDto(int userId, string username)
    {
        return new UserDto
        {
            UserId = userId,
            Username = username,
            DisplayName = $"Display {username}",
            Email = $"{username}@test.com",
            FirstName = "Test",
            LastName = "User",
            IsSuperUser = false
        };
    }

    #endregion
}
