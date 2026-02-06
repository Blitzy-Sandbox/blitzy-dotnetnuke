// -----------------------------------------------------------------------------
// <copyright file="UserServiceTests.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Test class verifying behavioral equivalence between the migrated C# 12 
// UserService and the original DNN 4.x VB.NET UserController.vb.
// Source reference: Library/Components/Users/UserController.vb
// 
// Test coverage target: 85% as per Section 0.7.5 Application layer requirements.
// Packages: xunit 2.9.2, Moq 4.20.72, FluentAssertions 6.12.2
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Services;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using DnnMigration.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DnnMigration.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="UserService"/> verifying behavioral equivalence with 
/// the original DNN 4.x VB.NET UserController.vb implementation.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: These tests verify that the migrated UserService produces behaviorally 
/// equivalent results to the original VB.NET UserController.vb user management operations.
/// </para>
/// <para>
/// Key behavioral requirements tested:
/// <list type="bullet">
///   <item>User CRUD operations (Get, Create, Update, Delete)</item>
///   <item>User lookup by username and email</item>
///   <item>Authentication validation with login status codes</item>
///   <item>Password management (change password)</item>
///   <item>Auto-role assignment during user creation</item>
///   <item>Administrator deletion safeguards</item>
/// </list>
/// </para>
/// </remarks>
public class UserServiceTests
{
    #region Private Fields - Mock Dependencies

    /// <summary>
    /// Mock for <see cref="IUserRepository"/> to isolate tests from data access layer.
    /// </summary>
    private readonly Mock<IUserRepository> _userRepositoryMock;

    /// <summary>
    /// Mock for <see cref="IRoleRepository"/> to test auto-role assignment logic.
    /// </summary>
    private readonly Mock<IRoleRepository> _roleRepositoryMock;

    /// <summary>
    /// Mock for <see cref="IPasswordHasher"/> to control password verification outcomes.
    /// </summary>
    private readonly Mock<IPasswordHasher> _passwordHasherMock;

    /// <summary>
    /// Mock for <see cref="IMapper"/> to control entity-to-DTO mappings.
    /// </summary>
    private readonly Mock<IMapper> _mapperMock;

    /// <summary>
    /// Mock for <see cref="ILogger{UserService}"/> to allow tests to run without logging.
    /// </summary>
    private readonly Mock<ILogger<UserService>> _loggerMock;

    /// <summary>
    /// System Under Test - the UserService instance being tested.
    /// </summary>
    private readonly UserService _sut;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="UserServiceTests"/> class.
    /// Sets up all mock dependencies and creates the System Under Test (SUT).
    /// </summary>
    public UserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _roleRepositoryMock = new Mock<IRoleRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<UserService>>();

        _sut = new UserService(
            _userRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _passwordHasherMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    #endregion

    #region GetUserAsync Tests

    /// <summary>
    /// Verifies that GetUserAsync returns a properly mapped UserDto when a valid user exists.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUser(portalId, userId, isHydrated) method.
    /// Source: UserController.vb GetUser function (lines 517-546).
    /// </remarks>
    [Fact]
    public async Task GetUserAsync_WithPortalAndUserId_ReturnsUserDto()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 100;
        
        var user = CreateTestUser(userId, portalId, "testuser");
        var expectedDto = CreateTestUserDto(userId, "testuser");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mapperMock
            .Setup(m => m.Map<UserDto>(user))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetUserAsync(portalId, userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Username.Should().Be("testuser");
        
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(portalId, userId, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetUserAsync returns null when user does not exist.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests null handling behavior equivalent to VB.NET Nothing check.
    /// </remarks>
    [Fact]
    public async Task GetUserAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        const int portalId = 1;
        const int invalidUserId = 99999;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(portalId, invalidUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.GetUserAsync(portalId, invalidUserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(portalId, invalidUserId, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region GetUsersAsync Tests

    /// <summary>
    /// Verifies that GetUsersAsync returns a properly paginated result.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUsers(ByRef totalRecords) method.
    /// Source: UserController.vb GetUsers function (lines 637-689).
    /// </remarks>
    [Fact]
    public async Task GetUsersAsync_WithPagination_ReturnsPagedResult()
    {
        // Arrange
        const int portalId = 1;
        const int pageIndex = 0;
        const int pageSize = 10;
        const int totalCount = 25;

        var users = new List<User>
        {
            CreateTestUser(1, portalId, "user1"),
            CreateTestUser(2, portalId, "user2"),
            CreateTestUser(3, portalId, "user3")
        };

        var userDtos = users.Select(u => CreateTestUserDto(u.UserId, u.Username)).ToList();

        _userRepositoryMock
            .Setup(r => r.GetPagedAsync(portalId, pageIndex, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((users, totalCount));

        _mapperMock
            .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
            .Returns(userDtos);

        // Act
        var result = await _sut.GetUsersAsync(portalId, pageIndex, pageSize, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.TotalCount.Should().Be(totalCount);
        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    #endregion

    #region GetUserByUsernameAsync Tests

    /// <summary>
    /// Verifies that GetUserByUsernameAsync returns user when username exists.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUserByName(portalId, username) method.
    /// Source: UserController.vb GetUserByName function (lines 556-564).
    /// </remarks>
    [Fact]
    public async Task GetUserByUsernameAsync_WithValidUsername_ReturnsUser()
    {
        // Arrange
        const int portalId = 1;
        const string username = "testuser";
        
        var user = CreateTestUser(100, portalId, username);
        var expectedDto = CreateTestUserDto(100, username);

        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mapperMock
            .Setup(m => m.Map<UserDto>(user))
            .Returns(expectedDto);

        // Act
        var result = await _sut.GetUserByUsernameAsync(portalId, username, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be(username);
        result.UserId.Should().Be(100);
    }

    #endregion

    #region GetUsersByEmailAsync Tests

    /// <summary>
    /// Verifies that GetUsersByEmailAsync returns paginated results for matching emails.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUsersByEmail method.
    /// Source: UserController.vb GetUsersByEmail function (lines 746-766).
    /// </remarks>
    [Fact]
    public async Task GetUsersByEmailAsync_WithMatchingEmail_ReturnsPagedResults()
    {
        // Arrange
        const int portalId = 1;
        const string emailFilter = "test@example.com";
        const int pageIndex = 0;
        const int pageSize = 10;

        var user = CreateTestUser(100, portalId, "testuser", emailFilter);
        var userDto = CreateTestUserDto(user.UserId, user.Username, user.Email);

        // MIGRATION: GetByEmailAsync returns a single User?, not a list
        // The service converts this to PagedResult internally
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(portalId, emailFilter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mapperMock
            .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(userDto);

        // Act
        var result = await _sut.GetUsersByEmailAsync(
            portalId, emailFilter, pageIndex, pageSize, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Email.Should().Be(emailFilter);
        result.TotalCount.Should().Be(1);
    }

    #endregion

    #region GetUsersByUsernameAsync Tests

    /// <summary>
    /// Verifies that GetUsersByUsernameAsync returns users matching the username pattern.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUsersByUserName method.
    /// Source: UserController.vb GetUsersByUserName function (lines 803-823).
    /// </remarks>
    [Fact]
    public async Task GetUsersByUsernameAsync_WithPattern_ReturnsMatchingUsers()
    {
        // Arrange
        const int portalId = 1;
        const string usernamePattern = "test";
        const int pageIndex = 0;
        const int pageSize = 10;

        // All portal users (including some that don't match the pattern)
        var allPortalUsers = new List<User>
        {
            CreateTestUser(1, portalId, "testuser1"),
            CreateTestUser(2, portalId, "testuser2"),
            CreateTestUser(3, portalId, "testadmin"),
            CreateTestUser(4, portalId, "adminonly")  // This one doesn't match the pattern
        };

        // Only users matching the pattern "test"
        var matchingUsers = allPortalUsers.Where(u => 
            u.Username.Contains(usernamePattern, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingUserDtos = matchingUsers.Select(u => 
            CreateTestUserDto(u.UserId, u.Username)).ToList();

        // MIGRATION: Service first checks for exact username match, then falls back to pattern matching
        // To test pattern matching, return null for exact match so service uses GetByPortalIdAsync
        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, usernamePattern, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // MIGRATION: Pattern matching path uses GetByPortalIdAsync to get all users then filters in-memory
        _userRepositoryMock
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPortalUsers);

        _mapperMock
            .Setup(m => m.Map<IEnumerable<UserDto>>(It.IsAny<IEnumerable<User>>()))
            .Returns(matchingUserDtos);

        // Act
        var result = await _sut.GetUsersByUsernameAsync(
            portalId, usernamePattern, pageIndex, pageSize, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(u => u.Username.Contains(usernamePattern));
        result.TotalCount.Should().Be(3);
    }

    #endregion

    #region CreateUserAsync Tests

    /// <summary>
    /// Verifies that CreateUserAsync creates a new user and returns the mapped DTO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET CreateUser method.
    /// Source: UserController.vb CreateUser function (lines 127-177).
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_WithValidRequest_CreatesUserAndReturnsDto()
    {
        // Arrange
        const int portalId = 1;
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Password = "SecurePassword123!",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User"
        };

        var createdUser = CreateTestUser(101, portalId, request.Username, request.Email);
        createdUser.FirstName = request.FirstName;
        createdUser.LastName = request.LastName;

        var expectedDto = CreateTestUserDto(101, request.Username, request.Email);

        // MIGRATION: The service's CreateUserAsync method takes only request and cancellation token.
        // PortalId is set via AutoMapper mapping. Set up mapper to return User with PortalId.
        var mappedUser = CreateTestUser(0, portalId, request.Username, request.Email);
        mappedUser.FirstName = request.FirstName;
        mappedUser.LastName = request.LastName;

        _mapperMock
            .Setup(m => m.Map<User>(It.IsAny<CreateUserRequest>()))
            .Returns(mappedUser);

        // MIGRATION: Service uses GetByUsernameAsync to check uniqueness, not ExistsAsync
        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(p => p.HashPassword(request.Password))
            .Returns("$2a$11$hashedpassword");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUser);

        _roleRepositoryMock
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Role>());

        _mapperMock
            .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(expectedDto);

        // Act
        // MIGRATION: CreateUserAsync signature is (CreateUserRequest request, CancellationToken cancellationToken)
        var result = await _sut.CreateUserAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be(request.Username);
        result.Email.Should().Be(request.Email);

        _userRepositoryMock.Verify(
            r => r.AddAsync(It.Is<User>(u => 
                u.Username == request.Username && 
                u.Email == request.Email &&
                u.PasswordHash == "$2a$11$hashedpassword"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that CreateUserAsync automatically assigns roles with AutoAssignment = true.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests auto-role assignment logic from VB.NET CreateUser method.
    /// Source: UserController.vb lines 155-176 - loop through arrRoles where AutoAssignment = True.
    /// Original VB.NET: For Each objRole As RoleInfo In arrRoles
    ///                     If objRole.AutoAssignment Then
    ///                         RoleController.AddUserRole(PortalId, objUser.UserID, objRole.RoleID, Null.NullDate, Null.NullDate)
    ///                     End If
    ///                  Next
    /// </remarks>
    [Fact]
    public async Task CreateUserAsync_WithAutoAssignmentRoles_AssignsRoles()
    {
        // Arrange
        const int portalId = 1;
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Password = "SecurePassword123!",
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User"
        };

        var createdUser = CreateTestUser(101, portalId, request.Username);
        var expectedDto = CreateTestUserDto(101, request.Username);

        // MIGRATION: The service's CreateUserAsync method takes only request and cancellation token.
        // PortalId is set via AutoMapper mapping. Set up mapper to return User with PortalId.
        var mappedUser = CreateTestUser(0, portalId, request.Username, request.Email);
        mappedUser.FirstName = request.FirstName;
        mappedUser.LastName = request.LastName;

        _mapperMock
            .Setup(m => m.Map<User>(It.IsAny<CreateUserRequest>()))
            .Returns(mappedUser);

        // Create roles with AutoAssignment = true (simulating "Registered Users" role)
        var autoAssignRole = new Role
        {
            RoleId = 10,
            RoleName = "Registered Users",
            PortalId = portalId,
            AutoAssignment = true
        };

        var regularRole = new Role
        {
            RoleId = 20,
            RoleName = "Administrators",
            PortalId = portalId,
            AutoAssignment = false
        };

        var portalRoles = new List<Role> { autoAssignRole, regularRole };

        // MIGRATION: Service uses GetByUsernameAsync to check uniqueness, not ExistsAsync
        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(p => p.HashPassword(request.Password))
            .Returns("$2a$11$hashedpassword");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUser);

        _roleRepositoryMock
            .Setup(r => r.GetByPortalIdAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalRoles);

        _roleRepositoryMock
            .Setup(r => r.AddUserToRoleAsync(
                portalId, 
                createdUser.UserId, 
                autoAssignRole.RoleId, 
                It.IsAny<DateTime?>(), 
                It.IsAny<DateTime?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRole { UserId = createdUser.UserId, RoleId = autoAssignRole.RoleId });

        _mapperMock
            .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(expectedDto);

        // Act
        // MIGRATION: CreateUserAsync signature is (CreateUserRequest request, CancellationToken cancellationToken)
        var result = await _sut.CreateUserAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify only the auto-assignment role was assigned
        _roleRepositoryMock.Verify(
            r => r.AddUserToRoleAsync(
                portalId, 
                createdUser.UserId, 
                autoAssignRole.RoleId, 
                It.IsAny<DateTime?>(), 
                It.IsAny<DateTime?>(), 
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify the non-auto-assignment role was NOT assigned
        _roleRepositoryMock.Verify(
            r => r.AddUserToRoleAsync(
                portalId, 
                createdUser.UserId, 
                regularRole.RoleId, 
                It.IsAny<DateTime?>(), 
                It.IsAny<DateTime?>(), 
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdateUserAsync Tests

    /// <summary>
    /// Verifies that UpdateUserAsync updates user properties and returns the mapped DTO.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET UpdateUser method.
    /// Source: UserController.vb UpdateUser function (lines 915-936).
    /// </remarks>
    [Fact]
    public async Task UpdateUserAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 100;
        
        var existingUser = CreateTestUser(userId, portalId, "existinguser", "old@example.com");
        existingUser.DisplayName = "Old Display Name";

        var request = new UpdateUserRequest
        {
            DisplayName = "New Display Name",
            Email = "new@example.com",
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast"
        };

        var updatedDto = new UserDto
        {
            UserId = userId,
            Username = "existinguser",
            Email = request.Email!,
            DisplayName = request.DisplayName,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PortalId = portalId,
            IsApproved = true,
            IsLockedOut = false
        };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mapperMock
            .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(updatedDto);

        // Act
        var result = await _sut.UpdateUserAsync(portalId, userId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be(request.DisplayName);
        result.Email.Should().Be(request.Email);

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<User>(u => 
                u.DisplayName == request.DisplayName && 
                u.Email == request.Email),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DeleteUserAsync Tests

    /// <summary>
    /// Verifies that DeleteUserAsync successfully deletes a non-administrator user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests normal user deletion behavior from VB.NET DeleteUser method.
    /// Source: UserController.vb DeleteUser function (lines 196-244).
    /// </remarks>
    [Fact]
    public async Task DeleteUserAsync_WithNonAdmin_DeletesSuccessfully()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 100;

        var regularUser = CreateTestUser(userId, portalId, "regularuser");
        regularUser.IsSuperUser = false;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regularUser);

        _roleRepositoryMock
            .Setup(r => r.GetUserRolesAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserRole>());

        // MIGRATION: The service performs soft delete (IsDeleted = true) via UpdateAsync
        // This aligns with modern data retention practices rather than hard delete
        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteUserAsync(portalId, userId, CancellationToken.None);

        // Assert
        // MIGRATION: Verify soft delete behavior - UpdateAsync called with IsDeleted = true
        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<User>(u => u.UserId == userId && u.IsDeleted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that DeleteUserAsync throws exception when attempting to delete a super user.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests administrator deletion safeguard from VB.NET DeleteUser method.
    /// Source: UserController.vb lines 208-213 - cannot delete administrator unless deleteAdmin = True.
    /// Original VB.NET: If objUser.IsSuperUser Then
    ///                     'cannot delete a superuser
    ///                     Return False
    ///                  End If
    /// </remarks>
    [Fact]
    public async Task DeleteUserAsync_WithPortalAdmin_ThrowsException()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 1; // Typically super user ID

        var superUser = CreateTestUser(userId, portalId, "admin");
        superUser.IsSuperUser = true;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(portalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superUser);

        // Act & Assert
        var act = async () => await _sut.DeleteUserAsync(portalId, userId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*super user*");

        _userRepositoryMock.Verify(
            r => r.DeleteAsync(portalId, userId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ValidateUserAsync Tests

    /// <summary>
    /// Verifies that ValidateUserAsync returns success status for valid credentials.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests successful authentication from VB.NET ValidateUser method.
    /// Source: UserController.vb ValidateUser function (lines 1100-1196).
    /// </remarks>
    [Fact]
    public async Task ValidateUserAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        const int portalId = 1;
        const string username = "validuser";
        const string password = "correctpassword";

        var user = CreateTestUser(100, portalId, username);
        user.PasswordHash = "$2a$11$validhash";
        user.IsApproved = true;
        user.IsLockedOut = false;
        user.IsDeleted = false;

        var userDto = CreateTestUserDto(user.UserId, user.Username, user.Email);

        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.VerifyPassword(password, user.PasswordHash))
            .Returns(true);

        _passwordHasherMock
            .Setup(p => p.VerifyAndUpgradeHash(password, user.PasswordHash))
            .Returns((true, false));

        // MIGRATION: Service updates last login date after successful validation
        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // MIGRATION: Service maps the User entity to UserDto before returning
        _mapperMock
            .Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(userDto);

        // Act
        var result = await _sut.ValidateUserAsync(portalId, username, password, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(UserLoginStatus.LoginSuccess);
        result.User.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that ValidateUserAsync returns failure status for invalid credentials.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests failed authentication from VB.NET ValidateUser method.
    /// Source: UserController.vb lines 1147-1150 - LOGIN_FAILURE on invalid password.
    /// </remarks>
    [Fact]
    public async Task ValidateUserAsync_WithInvalidCredentials_ReturnsFailure()
    {
        // Arrange
        const int portalId = 1;
        const string username = "validuser";
        const string wrongPassword = "wrongpassword";

        var user = CreateTestUser(100, portalId, username);
        user.PasswordHash = "$2a$11$validhash";
        user.IsApproved = true;
        user.IsLockedOut = false;
        user.IsDeleted = false;

        _userRepositoryMock
            .Setup(r => r.GetByUsernameAsync(portalId, username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(p => p.VerifyPassword(wrongPassword, user.PasswordHash))
            .Returns(false);

        _passwordHasherMock
            .Setup(p => p.VerifyAndUpgradeHash(wrongPassword, user.PasswordHash))
            .Returns((false, false));

        // Act
        var result = await _sut.ValidateUserAsync(portalId, username, wrongPassword, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(UserLoginStatus.LoginFailure);
    }

    #endregion

    #region ChangePasswordAsync Tests

    /// <summary>
    /// Verifies that ChangePasswordAsync succeeds when old password is valid.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests successful password change from VB.NET ChangePassword method.
    /// Source: UserController.vb ChangePassword function (lines 107-122).
    /// </remarks>
    [Fact]
    public async Task ChangePasswordAsync_WithValidOldPassword_ReturnsTrue()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 100;
        const string oldPassword = "oldpassword";
        const string newPassword = "newpassword123!";

        var user = CreateTestUser(userId, portalId, "testuser");
        user.PasswordHash = "$2a$11$oldhash";

        // MIGRATION: ChangePasswordAsync signature is (int userId, string oldPassword, string newPassword, CancellationToken)
        // The service uses FindUserByIdAsync which calls GetSuperUsersAsync and GetAllAsync, not GetByIdAsync
        _userRepositoryMock
            .Setup(r => r.GetSuperUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        _userRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        _passwordHasherMock
            .Setup(p => p.VerifyPassword(oldPassword, user.PasswordHash))
            .Returns(true);

        _passwordHasherMock
            .Setup(p => p.HashPassword(newPassword))
            .Returns("$2a$11$newhash");

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        // MIGRATION: ChangePasswordAsync takes (userId, oldPassword, newPassword, cancellationToken)
        var result = await _sut.ChangePasswordAsync(
            userId, oldPassword, newPassword, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<User>(u => u.PasswordHash == "$2a$11$newhash"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that ChangePasswordAsync fails when old password is invalid.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests failed password change from VB.NET ChangePassword method.
    /// Source: UserController.vb lines 115-118 - validate old password first.
    /// </remarks>
    [Fact]
    public async Task ChangePasswordAsync_WithInvalidOldPassword_ReturnsFalse()
    {
        // Arrange
        const int portalId = 1;
        const int userId = 100;
        const string wrongOldPassword = "wrongoldpassword";
        const string newPassword = "newpassword123!";

        var user = CreateTestUser(userId, portalId, "testuser");
        user.PasswordHash = "$2a$11$oldhash";

        // MIGRATION: ChangePasswordAsync signature is (int userId, string oldPassword, string newPassword, CancellationToken)
        // The service uses FindUserByIdAsync which calls GetSuperUsersAsync and GetAllAsync, not GetByIdAsync
        _userRepositoryMock
            .Setup(r => r.GetSuperUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        _userRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });

        _passwordHasherMock
            .Setup(p => p.VerifyPassword(wrongOldPassword, user.PasswordHash))
            .Returns(false);

        // Act
        // MIGRATION: ChangePasswordAsync takes (userId, oldPassword, newPassword, cancellationToken)
        var result = await _sut.ChangePasswordAsync(
            userId, wrongOldPassword, newPassword, CancellationToken.None);

        // Assert
        result.Should().BeFalse();

        _userRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetUserCountAsync Tests

    /// <summary>
    /// Verifies that GetUserCountAsync returns the correct count.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Tests behavioral equivalence with VB.NET GetUserCountByPortal method.
    /// Source: UserController.vb GetUserCountByPortal function (lines 573-578).
    /// </remarks>
    [Fact]
    public async Task GetUserCountAsync_ReturnsCount()
    {
        // Arrange
        const int portalId = 1;
        const int expectedCount = 150;

        _userRepositoryMock
            .Setup(r => r.GetUserCountAsync(portalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _sut.GetUserCountAsync(portalId, CancellationToken.None);

        // Assert
        result.Should().Be(expectedCount);
        
        _userRepositoryMock.Verify(
            r => r.GetUserCountAsync(portalId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test User entity with the specified properties.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="portalId">The portal ID.</param>
    /// <param name="username">The username.</param>
    /// <param name="email">The email address (defaults to {username}@example.com).</param>
    /// <returns>A configured User entity for testing.</returns>
    private static User CreateTestUser(
        int userId, 
        int portalId, 
        string username, 
        string? email = null)
    {
        return new User
        {
            UserId = userId,
            PortalId = portalId,
            Username = username,
            Email = email ?? $"{username}@example.com",
            DisplayName = username,
            FirstName = "Test",
            LastName = "User",
            IsApproved = true,
            IsLockedOut = false,
            IsDeleted = false,
            IsSuperUser = false,
            CreatedDate = DateTime.UtcNow.AddDays(-30)
        };
    }

    /// <summary>
    /// Creates a test UserDto with the specified properties.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="username">The username.</param>
    /// <param name="email">The email address (defaults to {username}@example.com).</param>
    /// <returns>A configured UserDto for testing.</returns>
    private static UserDto CreateTestUserDto(
        int userId, 
        string username, 
        string? email = null)
    {
        return new UserDto
        {
            UserId = userId,
            Username = username,
            Email = email ?? $"{username}@example.com",
            DisplayName = username,
            FirstName = "Test",
            LastName = "User",
            PortalId = 1,
            IsSuperUser = false,
            IsApproved = true,
            IsLockedOut = false,
            IsOnline = false,
            CreatedDate = DateTime.UtcNow.AddDays(-30)
        };
    }

    #endregion
}
