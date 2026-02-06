// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Unit tests verifying behavioral equivalence between C# 12 RoleService
// and original DNN 4.x RoleController.vb role management operations.
// Source References:
// - Library/Components/Security/Roles/RoleController.vb (GetRole, AddRole, UpdateRole, DeleteRole, etc.)
// - Library/Components/Security/Roles/RoleInfo.vb (Role entity properties)
// - Library/Components/Users/UserRoleInfo.vb (User-Role junction entity)
// Test Coverage Target: 85% per Section 0.7.5 Application layer requirements
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="RoleService"/> verifying behavioral equivalence
/// between the migrated C# 12 service and the original DNN 4.x RoleController.vb.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: These tests verify that the refactored RoleService maintains
/// behavioral equivalence with the original VB.NET RoleController.vb operations:
/// </para>
/// <list type="bullet">
///   <item><description>GetRole(RoleID, PortalID) → GetRoleAsync</description></item>
///   <item><description>GetPortalRoles(PortalID) → GetRolesAsync</description></item>
///   <item><description>GetRolesByGroup(PortalID, RoleGroupID) → GetRolesByGroupAsync</description></item>
///   <item><description>GetRoleByName(PortalID, RoleName) → GetRoleByNameAsync</description></item>
///   <item><description>AddRole(RoleInfo) → CreateRoleAsync</description></item>
///   <item><description>UpdateRole(RoleInfo) → UpdateRoleAsync</description></item>
///   <item><description>DeleteRole(RoleID, PortalID) → DeleteRoleAsync</description></item>
///   <item><description>AddUserRole(PortalID, UserID, RoleID, ...) → AddUserToRoleAsync</description></item>
///   <item><description>DeleteUserRole(UserID, RoleID) → RemoveUserFromRoleAsync</description></item>
///   <item><description>GetRolesByUser(UserID, PortalID) → GetUserRolesAsync</description></item>
///   <item><description>AutoAssignUsers(RoleID) → AutoAssignUsersAsync (called during CreateRole/UpdateRole)</description></item>
/// </list>
/// </remarks>
public class RoleServiceTests
{
    #region Private Fields

    private readonly Mock<IRoleRepository> _mockRoleRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly RoleService _sut;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleServiceTests"/> class.
    /// Sets up mocks for all dependencies and creates the system under test (SUT).
    /// </summary>
    public RoleServiceTests()
    {
        _mockRoleRepository = new Mock<IRoleRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockMapper = new Mock<IMapper>();

        _sut = new RoleService(
            _mockRoleRepository.Object,
            _mockUserRepository.Object,
            _mockMapper.Object);
    }

    #endregion

    #region GetRoleAsync Tests

    /// <summary>
    /// Tests that GetRoleAsync returns a RoleDto when the role exists.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetRole(RoleID, PortalID).
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_WithRoleIdAndPortalId_ReturnsRoleDto()
    {
        // Arrange
        const int roleId = 1;
        const int portalId = 0;

        var role = new Role
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Administrators",
            Description = "Portal Administrators",
            IsPublic = false,
            AutoAssignment = false,
            ServiceFee = 0.0m,
            BillingPeriod = 0,
            BillingFrequency = "N",
            TrialFee = 0.0m,
            TrialPeriod = 0,
            TrialFrequency = "N"
        };

        var expectedDto = new RoleDto
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Administrators",
            Description = "Portal Administrators",
            IsPublic = false,
            AutoAssignment = false
        };

        _mockRoleRepository
            .Setup(r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(role))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetRoleAsync(roleId, portalId);

        // Assert
        result.Should().NotBeNull();
        result!.RoleId.Should().Be(roleId);
        result.PortalId.Should().Be(portalId);
        result.RoleName.Should().Be("Administrators");
        result.Description.Should().Be("Portal Administrators");

        _mockRoleRepository.Verify(
            r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that GetRoleAsync returns null when the role does not exist.
    /// MIGRATION: Verifies null handling equivalent to VB.NET GetRole returning Nothing.
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int invalidRoleId = 9999;
        const int portalId = 0;

        _mockRoleRepository
            .Setup(r => r.GetByIdAsync(invalidRoleId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        // Act
        var result = await _sut.GetRoleAsync(invalidRoleId, portalId);

        // Assert
        result.Should().BeNull();

        _mockRoleRepository.Verify(
            r => r.GetByIdAsync(invalidRoleId, portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetRolesAsync Tests

    /// <summary>
    /// Tests that GetRolesAsync returns a paginated result of roles.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetPortalRoles(PortalID).
    /// </summary>
    [Fact]
    public async Task GetRolesAsync_WithPortalIdAndPagination_ReturnsPagedResult()
    {
        // Arrange
        const int portalId = 0;
        const int pageIndex = 0;
        const int pageSize = 10;

        var roles = new List<Role>
        {
            new Role { RoleId = 1, PortalId = portalId, RoleName = "Administrators" },
            new Role { RoleId = 2, PortalId = portalId, RoleName = "Registered Users" },
            new Role { RoleId = 3, PortalId = portalId, RoleName = "Subscribers" }
        };

        var roleDtos = roles.Select(r => new RoleDto
        {
            RoleId = r.RoleId,
            PortalId = r.PortalId,
            RoleName = r.RoleName
        }).ToList();

        _mockRoleRepository
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(It.IsAny<Role>()))
            .Returns<Role>(r => roleDtos.First(d => d.RoleId == r.RoleId));

        // Act
        var result = await _sut.GetRolesAsync(portalId, pageIndex, pageSize);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(1);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();

        _mockRoleRepository.Verify(
            r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetRolesByGroupAsync Tests

    /// <summary>
    /// Tests that GetRolesByGroupAsync returns roles filtered by role group.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetRolesByGroup(PortalID, RoleGroupID).
    /// </summary>
    [Fact]
    public async Task GetRolesByGroupAsync_WithGroupId_ReturnsRolesInGroup()
    {
        // Arrange
        const int portalId = 0;
        const int roleGroupId = 1;

        var roles = new List<Role>
        {
            new Role { RoleId = 1, PortalId = portalId, RoleGroupId = roleGroupId, RoleName = "Manager" },
            new Role { RoleId = 2, PortalId = portalId, RoleGroupId = roleGroupId, RoleName = "Editor" }
        };

        var roleDtos = roles.Select(r => new RoleDto
        {
            RoleId = r.RoleId,
            PortalId = r.PortalId,
            RoleGroupId = r.RoleGroupId,
            RoleName = r.RoleName
        }).ToList();

        _mockRoleRepository
            .Setup(r => r.GetByGroupIdAsync(portalId, roleGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(It.IsAny<Role>()))
            .Returns<Role>(r => roleDtos.First(d => d.RoleId == r.RoleId));

        // Act
        var result = await _sut.GetRolesByGroupAsync(portalId, roleGroupId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.RoleName == "Manager");
        result.Should().Contain(r => r.RoleName == "Editor");

        _mockRoleRepository.Verify(
            r => r.GetByGroupIdAsync(portalId, roleGroupId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetRoleByNameAsync Tests

    /// <summary>
    /// Tests that GetRoleByNameAsync returns a role by its name.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetRoleByName(PortalID, RoleName).
    /// </summary>
    [Fact]
    public async Task GetRoleByNameAsync_WithValidName_ReturnsRole()
    {
        // Arrange
        const int portalId = 0;
        const string roleName = "Administrators";

        var role = new Role
        {
            RoleId = 1,
            PortalId = portalId,
            RoleName = roleName,
            Description = "Portal Administrators"
        };

        var expectedDto = new RoleDto
        {
            RoleId = 1,
            PortalId = portalId,
            RoleName = roleName,
            Description = "Portal Administrators"
        };

        _mockRoleRepository
            .Setup(r => r.GetByNameAsync(portalId, roleName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(role))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetRoleByNameAsync(portalId, roleName);

        // Assert
        result.Should().NotBeNull();
        result!.RoleName.Should().Be(roleName);
        result.PortalId.Should().Be(portalId);

        _mockRoleRepository.Verify(
            r => r.GetByNameAsync(portalId, roleName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetRoleNamesAsync Tests

    /// <summary>
    /// Tests that GetRoleNamesAsync returns an array of role names.
    /// MIGRATION: Verifies role name enumeration for user role assignments.
    /// </summary>
    [Fact]
    public async Task GetRoleNamesAsync_ReturnsStringArray()
    {
        // Arrange
        const int portalId = 0;
        var roleNames = new[] { "Administrators", "Registered Users", "Subscribers" };

        _mockRoleRepository
            .Setup(r => r.GetRoleNamesAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleNames);

        // Act
        var result = await _sut.GetRoleNamesAsync(portalId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain("Administrators");
        result.Should().Contain("Registered Users");
        result.Should().Contain("Subscribers");

        _mockRoleRepository.Verify(
            r => r.GetRoleNamesAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreateRoleAsync Tests

    /// <summary>
    /// Tests that CreateRoleAsync creates a role and returns the DTO.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET AddRole(RoleInfo).
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithValidRequest_CreatesRoleAndReturnsDto()
    {
        // Arrange
        const int portalId = 0;
        var request = new CreateRoleRequest
        {
            PortalId = portalId,
            RoleName = "Premium Members",
            Description = "Premium subscription members",
            IsPublic = true,
            AutoAssignment = false,
            ServiceFee = 9.99m,
            BillingPeriod = 1,
            BillingFrequency = "M"
        };

        // Role created by mapper from request
        var mappedRole = new Role
        {
            PortalId = portalId,
            RoleName = request.RoleName,
            Description = request.Description,
            IsPublic = request.IsPublic,
            AutoAssignment = request.AutoAssignment,
            ServiceFee = request.ServiceFee ?? 0m,
            BillingPeriod = request.BillingPeriod ?? 0,
            BillingFrequency = request.BillingFrequency ?? "N"
        };

        // Role returned from repository (with assigned ID)
        var createdRole = new Role
        {
            RoleId = 10,
            PortalId = portalId,
            RoleName = request.RoleName,
            Description = request.Description,
            IsPublic = request.IsPublic,
            AutoAssignment = request.AutoAssignment,
            ServiceFee = request.ServiceFee ?? 0m,
            BillingPeriod = request.BillingPeriod ?? 0,
            BillingFrequency = request.BillingFrequency ?? "N"
        };

        var expectedDto = new RoleDto
        {
            RoleId = 10,
            PortalId = portalId,
            RoleName = request.RoleName,
            Description = request.Description,
            IsPublic = request.IsPublic,
            AutoAssignment = request.AutoAssignment,
            ServiceFee = request.ServiceFee ?? 0m
        };

        // Setup mapper to convert request to Role entity
        _mockMapper
            .Setup(m => m.Map<Role>(request))
            .Returns(mappedRole);

        _mockRoleRepository
            .Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdRole);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(createdRole))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreateRoleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RoleId.Should().Be(10);
        result.RoleName.Should().Be("Premium Members");
        result.Description.Should().Be("Premium subscription members");
        result.IsPublic.Should().BeTrue();
        result.AutoAssignment.Should().BeFalse();

        _mockRoleRepository.Verify(
            r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that CreateRoleAsync with AutoAssignment=true assigns existing portal users to the role.
    /// MIGRATION: Verifies AutoAssignUsers private method logic from RoleController.vb
    /// where when role has AutoAssignment=true, it iterates portal users and adds them to the role.
    /// </summary>
    [Fact]
    public async Task CreateRoleAsync_WithAutoAssignment_AssignsExistingUsers()
    {
        // Arrange
        const int portalId = 0;
        var request = new CreateRoleRequest
        {
            PortalId = portalId,
            RoleName = "All Members",
            Description = "Auto-assigned to all users",
            IsPublic = true,
            AutoAssignment = true
        };

        var createdRole = new Role
        {
            RoleId = 15,
            PortalId = portalId,
            RoleName = request.RoleName,
            Description = request.Description,
            IsPublic = true,
            AutoAssignment = true
        };

        var portalUsers = new List<User>
        {
            new User { UserId = 1, PortalId = portalId, Username = "admin", DisplayName = "Administrator" },
            new User { UserId = 2, PortalId = portalId, Username = "user1", DisplayName = "User One" },
            new User { UserId = 3, PortalId = portalId, Username = "user2", DisplayName = "User Two" }
        };

        var expectedDto = new RoleDto
        {
            RoleId = 15,
            PortalId = portalId,
            RoleName = request.RoleName,
            AutoAssignment = true
        };

        _mockRoleRepository
            .Setup(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdRole);

        _mockUserRepository
            .Setup(u => u.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalUsers);

        _mockRoleRepository
            .Setup(r => r.AddUserToRoleAsync(portalId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRole());

        _mockMapper
            .Setup(m => m.Map<RoleDto>(createdRole))
            .Returns(expectedDto);

        // Act
        var result = await _sut.CreateRoleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AutoAssignment.Should().BeTrue();

        // Verify that user repository was called to get portal users
        _mockUserRepository.Verify(
            u => u.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify that each user was added to the role
        _mockRoleRepository.Verify(
            r => r.AddUserToRoleAsync(
                portalId,
                It.IsAny<int>(),
                15,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    #endregion

    #region UpdateRoleAsync Tests

    /// <summary>
    /// Tests that UpdateRoleAsync updates a role and returns the updated DTO.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET UpdateRole(RoleInfo).
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int roleId = 1;
        const int portalId = 0;
        var request = new UpdateRoleRequest
        {
            RoleName = "Updated Role Name",
            Description = "Updated description",
            IsPublic = true
        };

        var existingRole = new Role
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Original Name",
            Description = "Original description",
            IsPublic = false,
            AutoAssignment = false
        };

        var updatedRole = new Role
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = request.RoleName!,
            Description = request.Description,
            IsPublic = request.IsPublic ?? false,
            AutoAssignment = false
        };

        var expectedDto = new RoleDto
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = request.RoleName!,
            Description = request.Description,
            IsPublic = request.IsPublic ?? false
        };

        _mockRoleRepository
            .Setup(r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        _mockRoleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMapper
            .Setup(m => m.Map<RoleDto>(It.IsAny<Role>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdateRoleAsync(roleId, portalId, request);

        // Assert
        result.Should().NotBeNull();
        result!.RoleId.Should().Be(roleId);
        result.RoleName.Should().Be("Updated Role Name");
        result.Description.Should().Be("Updated description");
        result.IsPublic.Should().BeTrue();

        _mockRoleRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that UpdateRoleAsync triggers auto-assignment when AutoAssignment changes to true.
    /// MIGRATION: Verifies that AutoAssignUsers is called when AutoAssignment flag changes from false to true.
    /// </summary>
    [Fact]
    public async Task UpdateRoleAsync_WithAutoAssignmentChange_TriggersAutoAssign()
    {
        // Arrange
        const int roleId = 5;
        const int portalId = 0;
        var request = new UpdateRoleRequest
        {
            AutoAssignment = true // Changing from false to true
        };

        var existingRole = new Role
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Existing Role",
            AutoAssignment = false // Originally false
        };

        var portalUsers = new List<User>
        {
            new User { UserId = 1, PortalId = portalId, Username = "user1" },
            new User { UserId = 2, PortalId = portalId, Username = "user2" }
        };

        var expectedDto = new RoleDto
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Existing Role",
            AutoAssignment = true
        };

        _mockRoleRepository
            .Setup(r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        _mockRoleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserRepository
            .Setup(u => u.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalUsers);

        _mockRoleRepository
            .Setup(r => r.AddUserToRoleAsync(portalId, It.IsAny<int>(), roleId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRole());

        _mockMapper
            .Setup(m => m.Map<RoleDto>(It.IsAny<Role>()))
            .Returns(expectedDto);

        // Act
        var result = await _sut.UpdateRoleAsync(roleId, portalId, request);

        // Assert
        result.Should().NotBeNull();
        result!.AutoAssignment.Should().BeTrue();

        // Verify auto-assignment logic was triggered
        _mockUserRepository.Verify(
            u => u.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRoleRepository.Verify(
            r => r.AddUserToRoleAsync(
                portalId,
                It.IsAny<int>(),
                roleId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region DeleteRoleAsync Tests

    /// <summary>
    /// Tests that DeleteRoleAsync deletes a role successfully.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET DeleteRole(RoleID, PortalID).
    /// </summary>
    [Fact]
    public async Task DeleteRoleAsync_WithRoleIdAndPortalId_DeletesRole()
    {
        // Arrange
        const int roleId = 10;
        const int portalId = 0;

        var existingRole = new Role
        {
            RoleId = roleId,
            PortalId = portalId,
            RoleName = "Test Role"
        };

        // Service first checks if role exists before deleting
        _mockRoleRepository
            .Setup(r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRole);

        _mockRoleRepository
            .Setup(r => r.DeleteAsync(roleId, portalId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteRoleAsync(roleId, portalId);

        // Assert
        _mockRoleRepository.Verify(
            r => r.GetByIdAsync(roleId, portalId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRoleRepository.Verify(
            r => r.DeleteAsync(roleId, portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region AddUserToRoleAsync Tests

    /// <summary>
    /// Tests that AddUserToRoleAsync adds a user to a role with effective and expiry dates.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET AddUserRole(PortalID, UserID, RoleID, EffectiveDate, ExpiryDate).
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WithDates_AddsUserRole()
    {
        // Arrange
        const int portalId = 0;
        const int userId = 100;
        const int roleId = 5;
        var effectiveDate = new DateTime(2024, 1, 1);
        var expiryDate = new DateTime(2024, 12, 31);

        var userRole = new UserRole
        {
            UserRoleId = 1,
            UserId = userId,
            RoleId = roleId,
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate
        };

        _mockRoleRepository
            .Setup(r => r.AddUserToRoleAsync(portalId, userId, roleId, effectiveDate, expiryDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userRole);

        // Act
        await _sut.AddUserToRoleAsync(portalId, userId, roleId, effectiveDate, expiryDate);

        // Assert
        _mockRoleRepository.Verify(
            r => r.AddUserToRoleAsync(
                portalId,
                userId,
                roleId,
                effectiveDate,
                expiryDate,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that AddUserToRoleAsync handles null dates correctly (uses defaults).
    /// MIGRATION: Verifies null date handling equivalent to VB.NET Null.NullDate.
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WithNullDates_UsesDefaults()
    {
        // Arrange
        const int portalId = 0;
        const int userId = 100;
        const int roleId = 5;

        var userRole = new UserRole
        {
            UserRoleId = 1,
            UserId = userId,
            RoleId = roleId,
            EffectiveDate = null,
            ExpiryDate = null
        };

        _mockRoleRepository
            .Setup(r => r.AddUserToRoleAsync(portalId, userId, roleId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userRole);

        // Act
        await _sut.AddUserToRoleAsync(portalId, userId, roleId, null, null);

        // Assert
        _mockRoleRepository.Verify(
            r => r.AddUserToRoleAsync(
                portalId,
                userId,
                roleId,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region RemoveUserFromRoleAsync Tests

    /// <summary>
    /// Tests that RemoveUserFromRoleAsync successfully removes a user from a non-protected role.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET DeleteUserRole(PortalID, UserID, RoleID).
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRoleAsync_WithNonProtectedRole_ReturnsTrue()
    {
        // Arrange
        const int portalId = 0;
        const int userId = 100;
        const int roleId = 10; // Non-protected custom role

        var existingUserRole = new UserRole
        {
            UserRoleId = 1,
            UserId = userId,
            RoleId = roleId
        };

        _mockRoleRepository
            .Setup(r => r.GetUserRoleAsync(portalId, userId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUserRole);

        _mockRoleRepository
            .Setup(r => r.RemoveUserFromRoleAsync(portalId, userId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemoveUserFromRoleAsync(portalId, userId, roleId);

        // Assert
        result.Should().BeTrue();

        _mockRoleRepository.Verify(
            r => r.RemoveUserFromRoleAsync(portalId, userId, roleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that RemoveUserFromRoleAsync returns false when user-role assignment doesn't exist.
    /// MIGRATION: Verifies null check behavior from RoleController.vb DeleteUserRole method.
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRoleAsync_WithNonExistentAssignment_ThrowsKeyNotFoundException()
    {
        // Arrange
        const int portalId = 0;
        const int userId = 100;
        const int roleId = 10;

        _mockRoleRepository
            .Setup(r => r.GetUserRoleAsync(portalId, userId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRole?)null);

        // Act
        Func<Task> act = async () => await _sut.RemoveUserFromRoleAsync(portalId, userId, roleId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{userId}*{roleId}*");

        // Verify that RemoveUserFromRoleAsync was NOT called since assignment doesn't exist
        _mockRoleRepository.Verify(
            r => r.RemoveUserFromRoleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetUserRolesAsync Tests

    /// <summary>
    /// Tests that GetUserRolesAsync returns an array of role names for a user.
    /// MIGRATION: Verifies behavioral equivalence with VB.NET GetRolesByUser(UserID, PortalID).
    /// </summary>
    [Fact]
    public async Task GetUserRolesAsync_ReturnsRoleNames()
    {
        // Arrange
        const int portalId = 0;
        const int userId = 100;

        // Create UserRole entities with populated Role navigation property
        // The service extracts role names from ur.Role.RoleName
        var userRoles = new List<UserRole>
        {
            new UserRole 
            { 
                UserRoleId = 1, 
                UserId = userId, 
                RoleId = 1,
                Role = new Role { RoleId = 1, RoleName = "Administrators" }
            },
            new UserRole 
            { 
                UserRoleId = 2, 
                UserId = userId, 
                RoleId = 2,
                Role = new Role { RoleId = 2, RoleName = "Registered Users" }
            },
            new UserRole 
            { 
                UserRoleId = 3, 
                UserId = userId, 
                RoleId = 3,
                Role = new Role { RoleId = 3, RoleName = "Subscribers" }
            }
        };

        _mockRoleRepository
            .Setup(r => r.GetUserRolesAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userRoles);

        // Act
        var result = await _sut.GetUserRolesAsync(portalId, userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain("Administrators");
        result.Should().Contain("Registered Users");
        result.Should().Contain("Subscribers");

        _mockRoleRepository.Verify(
            r => r.GetUserRolesAsync(portalId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
