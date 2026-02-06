// -----------------------------------------------------------------------------
// UsersControllerTests.cs
// Integration tests for UsersController verifying complete HTTP request/response
// cycles for user administration operations. Tests all CRUD endpoints with proper
// authentication, validation, and error handling.
// MIGRATION: Test scenarios derived from ManageUsers.ascx.vb and User.ascx.vb patterns
// -----------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for UsersController verifying complete HTTP request/response cycles
/// for user administration operations. Tests GET /api/users (list with pagination and 
/// filtering by username/email), GET /api/users/{id} (single user retrieval), 
/// POST /api/users (user creation), PUT /api/users/{id} (user updates), 
/// and DELETE /api/users/{id} (user deletion).
/// 
/// MIGRATION: Test scenarios derived from legacy ManageUsers.ascx.vb and User.ascx.vb patterns.
/// </summary>
public class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _databaseName;

    // Test constants
    private const string TestAdminUsername = "admin";
    private const string TestAdminEmail = "admin@test.com";
    private const string TestAdminPassword = "Admin123!";
    private const string TestUserPassword = "User123!";
    private const int TestPortalId = 1;
    private const int TestAdminRoleId = 1;

    // JSON serializer options for consistent serialization
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UsersControllerTests(WebApplicationFactory<Program> factory)
    {
        // Generate unique database name per test class instance for isolation
        _databaseName = $"DnnMigration_IntegrationTests_{Guid.NewGuid()}";
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                services.RemoveAll<DbContextOptions<DnnDbContext>>();
                services.RemoveAll<DnnDbContext>();

                // Add InMemory database for test isolation
                services.AddDbContext<DnnDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });
            });
        });

        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Initialize test data before each test execution.
    /// </summary>
    public async Task InitializeAsync()
    {
        await SeedTestDataAsync();
    }

    /// <summary>
    /// Cleanup after test execution.
    /// </summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region Helper Methods

    /// <summary>
    /// Seeds the InMemory database with test portal, roles, and admin user data.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Ensure database is created and clean
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Create test portal
        var portal = new Portal
        {
            PortalId = TestPortalId,
            PortalName = "Test Portal",
            Description = "Integration test portal",
            DefaultLanguage = "en-US",
            TimeZoneOffset = 0,
            GUID = Guid.NewGuid()
        };
        dbContext.Portals.Add(portal);

        // Create Administrator role
        var adminRole = new Role
        {
            RoleId = TestAdminRoleId,
            RoleName = "Administrators",
            PortalId = TestPortalId,
            Description = "Portal administrators",
            IsPublic = false,
            AutoAssignment = false
        };
        dbContext.Roles.Add(adminRole);

        // Create Registered Users role
        var registeredRole = new Role
        {
            RoleId = 2,
            RoleName = "Registered Users",
            PortalId = TestPortalId,
            Description = "Registered users",
            IsPublic = true,
            AutoAssignment = true
        };
        dbContext.Roles.Add(registeredRole);

        await dbContext.SaveChangesAsync();

        // Create admin user with hashed password
        var adminUser = new User
        {
            UserId = 1,
            Username = TestAdminUsername,
            DisplayName = "Test Administrator",
            FirstName = "Admin",
            LastName = "User",
            Email = TestAdminEmail,
            PasswordHash = passwordHasher.HashPassword(TestAdminPassword),
            PortalId = TestPortalId,
            IsSuperUser = true,
            IsApproved = true,
            IsLockedOut = false,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };
        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();

        // Assign admin role to admin user
        var adminUserRole = new UserRole
        {
            UserRoleId = 1,
            UserId = adminUser.UserId,
            RoleId = TestAdminRoleId,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            ExpiryDate = null
        };
        dbContext.UserRoles.Add(adminUserRole);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates additional test users in the database.
    /// </summary>
    private async Task<List<User>> SeedAdditionalUsersAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var users = new List<User>();
        var existingMaxId = await dbContext.Users.MaxAsync(u => (int?)u.UserId) ?? 0;

        for (int i = 1; i <= count; i++)
        {
            var user = new User
            {
                UserId = existingMaxId + i,
                Username = $"testuser{i}",
                DisplayName = $"Test User {i}",
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Email = $"testuser{i}@test.com",
                PasswordHash = passwordHasher.HashPassword(TestUserPassword),
                PortalId = TestPortalId,
                IsSuperUser = false,
                IsApproved = true,
                IsLockedOut = false,
                CreatedOnDate = DateTime.UtcNow,
                LastModifiedOnDate = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            users.Add(user);
        }

        await dbContext.SaveChangesAsync();
        return users;
    }

    /// <summary>
    /// Gets an HttpClient with a valid JWT Bearer token for the admin user.
    /// </summary>
    private async Task<HttpClient> GetAuthenticatedClientAsync(
        string? username = null, 
        bool isAdmin = true,
        int? userId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        // Get user for authentication
        var userQuery = dbContext.Users.AsQueryable();
        
        if (userId.HasValue)
        {
            userQuery = userQuery.Where(u => u.UserId == userId.Value);
        }
        else if (!string.IsNullOrEmpty(username))
        {
            userQuery = userQuery.Where(u => u.Username == username);
        }
        else
        {
            // Default to admin user
            userQuery = userQuery.Where(u => u.Username == TestAdminUsername);
        }

        var user = await userQuery.FirstAsync();

        // Get user roles
        var roles = await dbContext.UserRoles
            .Where(ur => ur.UserId == user.UserId)
            .Join(dbContext.Roles,
                ur => ur.RoleId,
                r => r.RoleId,
                (ur, r) => r.RoleName)
            .ToListAsync();

        // Generate JWT token
        var tokenResult = await jwtService.GenerateTokenAsync(user, roles);

        // Create authenticated client
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        return client;
    }

    /// <summary>
    /// Creates a non-admin user and returns an authenticated client for that user.
    /// </summary>
    private async Task<(HttpClient client, User user)> CreateNonAdminAuthenticatedClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        // Create non-admin user
        var existingMaxId = await dbContext.Users.MaxAsync(u => (int?)u.UserId) ?? 0;
        var nonAdminUser = new User
        {
            UserId = existingMaxId + 100,
            Username = "nonadmin",
            DisplayName = "Non Admin User",
            FirstName = "NonAdmin",
            LastName = "User",
            Email = "nonadmin@test.com",
            PasswordHash = passwordHasher.HashPassword(TestUserPassword),
            PortalId = TestPortalId,
            IsSuperUser = false,
            IsApproved = true,
            IsLockedOut = false,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };
        dbContext.Users.Add(nonAdminUser);

        // Assign only Registered Users role (not Administrators)
        var registeredUserRole = new UserRole
        {
            UserRoleId = await dbContext.UserRoles.MaxAsync(ur => (int?)ur.UserRoleId) ?? 0 + 100,
            UserId = nonAdminUser.UserId,
            RoleId = 2, // Registered Users role
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            ExpiryDate = null
        };
        dbContext.UserRoles.Add(registeredUserRole);
        await dbContext.SaveChangesAsync();

        // Generate JWT token for non-admin user
        var roles = new List<string> { "Registered Users" };
        var tokenResult = await jwtService.GenerateTokenAsync(nonAdminUser, roles);

        // Create authenticated client
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        return (client, nonAdminUser);
    }

    #endregion

    #region GET /api/users Tests

    /// <summary>
    /// MIGRATION: Tests user listing functionality derived from ManageUsers.ascx.vb BindData method.
    /// Verifies that GET /api/users returns a paged result when users exist.
    /// </summary>
    [Fact]
    public async Task GetUsers_ReturnsPagedResult_WhenUsersExist()
    {
        // Arrange
        await SeedAdditionalUsersAsync(5);
        var client = await GetAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterOrEqualTo(6); // Admin + 5 seeded users
        result.PageIndex.Should().Be(0);
        result.PageSize.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies that GET /api/users returns an empty list when no users exist (except admin).
    /// </summary>
    [Fact]
    public async Task GetUsers_ReturnsEmptyList_WhenNoUsers()
    {
        // Arrange - Initialize fresh database with only admin
        var client = await GetAuthenticatedClientAsync();

        // Clear all users except admin to simulate empty state
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var usersToRemove = await dbContext.Users.Where(u => u.Username != TestAdminUsername).ToListAsync();
        dbContext.Users.RemoveRange(usersToRemove);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/api/users?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(JsonOptions);
        result.Should().NotBeNull();
        // Only admin user should exist
        result!.Items.Should().HaveCount(1);
        result.Items.First().Username.Should().Be(TestAdminUsername);
    }

    /// <summary>
    /// MIGRATION: Tests username filtering derived from Users.ascx.vb BindData filtering logic.
    /// Verifies that GET /api/users filters by username when filter parameter is provided.
    /// </summary>
    [Fact]
    public async Task GetUsers_FiltersByUsername_WhenUsernameFilterProvided()
    {
        // Arrange
        await SeedAdditionalUsersAsync(5);
        var client = await GetAuthenticatedClientAsync();
        const string usernameFilter = "testuser1";

        // Act
        var response = await client.GetAsync($"/api/users?username={usernameFilter}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(u => 
            u.Username.Contains(usernameFilter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that GET /api/users filters by email when email filter parameter is provided.
    /// </summary>
    [Fact]
    public async Task GetUsers_FiltersByEmail_WhenEmailFilterProvided()
    {
        // Arrange
        await SeedAdditionalUsersAsync(5);
        var client = await GetAuthenticatedClientAsync();
        const string emailFilter = "testuser2@test.com";

        // Act
        var response = await client.GetAsync($"/api/users?email={Uri.EscapeDataString(emailFilter)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().Contain(u => 
            u.Email.Equals(emailFilter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that GET /api/users supports pagination when page parameters are provided.
    /// </summary>
    [Fact]
    public async Task GetUsers_SupportsPagination_WhenPageParametersProvided()
    {
        // Arrange
        await SeedAdditionalUsersAsync(10);
        var client = await GetAuthenticatedClientAsync();
        const int pageSize = 3;
        const int pageIndex = 1; // Second page

        // Act
        var response = await client.GetAsync($"/api/users?pageIndex={pageIndex}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(JsonOptions);
        result.Should().NotBeNull();
        result!.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.Items.Count.Should().BeLessOrEqualTo(pageSize);
        result.TotalCount.Should().BeGreaterOrEqualTo(11); // Admin + 10 seeded users
        result.HasPreviousPage.Should().BeTrue();
    }

    #endregion

    #region GET /api/users/{id} Tests

    /// <summary>
    /// Verifies that GET /api/users/{id} returns the user when they exist.
    /// </summary>
    [Fact]
    public async Task GetUser_ReturnsUser_WhenExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        const int existingUserId = 1; // Admin user

        // Act
        var response = await client.GetAsync($"/api/users/{existingUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        user.Should().NotBeNull();
        user!.UserId.Should().Be(existingUserId);
        user.Username.Should().Be(TestAdminUsername);
        user.Email.Should().Be(TestAdminEmail);
        user.DisplayName.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that GET /api/users/{id} returns 404 Not Found when user doesn't exist.
    /// </summary>
    [Fact]
    public async Task GetUser_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        const int nonExistentUserId = 99999;

        // Act
        var response = await client.GetAsync($"/api/users/{nonExistentUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/users Tests

    /// <summary>
    /// MIGRATION: Tests user creation derived from User.ascx.vb cmdRegister_Click handler.
    /// Verifies that POST /api/users returns Created with Location header when valid.
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsCreatedWithLocation_WhenValid()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        var createRequest = new CreateUserRequest
        {
            Username = "newuser",
            Password = "NewUser123!",
            Email = "newuser@test.com",
            FirstName = "New",
            LastName = "User",
            DisplayName = "New Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var createdUser = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        createdUser.Should().NotBeNull();
        createdUser!.Username.Should().Be(createRequest.Username);
        createdUser.Email.Should().Be(createRequest.Email);
        createdUser.FirstName.Should().Be(createRequest.FirstName);
        createdUser.LastName.Should().Be(createRequest.LastName);
        createdUser.DisplayName.Should().Be(createRequest.DisplayName);
    }

    /// <summary>
    /// Verifies that POST /api/users returns BadRequest when username already exists.
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsBadRequest_WhenUsernameAlreadyExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        var createRequest = new CreateUserRequest
        {
            Username = TestAdminUsername, // Already exists
            Password = "NewUser123!",
            Email = "different@test.com",
            FirstName = "Duplicate",
            LastName = "User",
            DisplayName = "Duplicate User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that POST /api/users returns BadRequest when email already exists.
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsBadRequest_WhenEmailAlreadyExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        var createRequest = new CreateUserRequest
        {
            Username = "uniqueuser",
            Password = "NewUser123!",
            Email = TestAdminEmail, // Already exists
            FirstName = "Unique",
            LastName = "User",
            DisplayName = "Unique User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that POST /api/users returns BadRequest when password policy is violated.
    /// </summary>
    [Theory]
    [InlineData("short")] // Too short
    [InlineData("nouppercase123!")] // No uppercase
    [InlineData("NOLOWERCASE123!")] // No lowercase
    [InlineData("NoSpecialChar123")] // No special character
    [InlineData("NoNumbers!")] // No numbers
    public async Task CreateUser_ReturnsBadRequest_WhenPasswordPolicyViolated(string weakPassword)
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        var createRequest = new CreateUserRequest
        {
            Username = "passwordtestuser",
            Password = weakPassword,
            Email = "passwordtest@test.com",
            FirstName = "Password",
            LastName = "Test",
            DisplayName = "Password Test User"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that POST /api/users returns Unauthorized when not authenticated.
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange - Use unauthenticated client
        var createRequest = new CreateUserRequest
        {
            Username = "unauthuser",
            Password = "UnauthUser123!",
            Email = "unauth@test.com",
            FirstName = "Unauth",
            LastName = "User",
            DisplayName = "Unauthenticated User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that POST /api/users returns Forbidden when authenticated user is not an admin.
    /// </summary>
    [Fact]
    public async Task CreateUser_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        var (nonAdminClient, _) = await CreateNonAdminAuthenticatedClientAsync();
        var createRequest = new CreateUserRequest
        {
            Username = "forbiddenuser",
            Password = "ForbiddenUser123!",
            Email = "forbidden@test.com",
            FirstName = "Forbidden",
            LastName = "User",
            DisplayName = "Forbidden User"
        };

        // Act
        var response = await nonAdminClient.PostAsJsonAsync("/api/users", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        nonAdminClient.Dispose();
    }

    #endregion

    #region PUT /api/users/{id} Tests

    /// <summary>
    /// MIGRATION: Tests user update derived from User.ascx.vb cmdUpdate_Click handler.
    /// Verifies that PUT /api/users/{id} returns updated user when valid.
    /// </summary>
    [Fact]
    public async Task UpdateUser_ReturnsUpdatedUser_WhenValid()
    {
        // Arrange
        var users = await SeedAdditionalUsersAsync(1);
        var userToUpdate = users.First();
        var client = await GetAuthenticatedClientAsync();

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = "Updated Display Name",
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            Email = "updated@test.com"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{userToUpdate.UserId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        updatedUser.Should().NotBeNull();
        updatedUser!.DisplayName.Should().Be(updateRequest.DisplayName);
        updatedUser.FirstName.Should().Be(updateRequest.FirstName);
        updatedUser.LastName.Should().Be(updateRequest.LastName);
        updatedUser.Email.Should().Be(updateRequest.Email);
    }

    /// <summary>
    /// Verifies that PUT /api/users/{id} returns 404 Not Found when user doesn't exist.
    /// </summary>
    [Fact]
    public async Task UpdateUser_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        const int nonExistentUserId = 99999;

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = "Non Existent Update"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{nonExistentUserId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that PUT /api/users/{id} returns BadRequest when validation fails.
    /// </summary>
    [Theory]
    [InlineData("", "valid@email.com")] // Empty display name
    [InlineData("Valid Name", "invalid-email")] // Invalid email format
    public async Task UpdateUser_ReturnsBadRequest_WhenValidationFails(string displayName, string email)
    {
        // Arrange
        var users = await SeedAdditionalUsersAsync(1);
        var userToUpdate = users.First();
        var client = await GetAuthenticatedClientAsync();

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? null : displayName,
            Email = email
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{userToUpdate.UserId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region DELETE /api/users/{id} Tests

    /// <summary>
    /// Verifies that DELETE /api/users/{id} returns NoContent when user exists.
    /// </summary>
    [Fact]
    public async Task DeleteUser_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var users = await SeedAdditionalUsersAsync(1);
        var userToDelete = users.First();
        var client = await GetAuthenticatedClientAsync();

        // Act
        var response = await client.DeleteAsync($"/api/users/{userToDelete.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is deleted
        var getResponse = await client.GetAsync($"/api/users/{userToDelete.UserId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that DELETE /api/users/{id} returns 404 Not Found when user doesn't exist.
    /// </summary>
    [Fact]
    public async Task DeleteUser_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        const int nonExistentUserId = 99999;

        // Act
        var response = await client.DeleteAsync($"/api/users/{nonExistentUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that DELETE /api/users/{id} returns BadRequest when admin tries to delete themselves.
    /// MIGRATION: Prevents self-deletion which was protected in legacy ManageUsers.ascx.vb.
    /// </summary>
    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenDeletingSelf()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync();
        const int adminUserId = 1; // The authenticated admin user

        // Act
        var response = await client.DeleteAsync($"/api/users/{adminUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify admin user still exists
        var getResponse = await client.GetAsync($"/api/users/{adminUserId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
