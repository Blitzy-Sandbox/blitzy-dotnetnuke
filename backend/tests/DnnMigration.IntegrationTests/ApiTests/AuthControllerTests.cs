// -----------------------------------------------------------------------------
// File: AuthControllerTests.cs
// Purpose: Integration tests for AuthController verifying complete HTTP 
//          request/response cycles for JWT authentication flows.
// MIGRATION: Test scenarios derived from PortalSecurity.vb authentication patterns
//            replacing Forms Authentication with JWT Bearer tokens.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for AuthController endpoints verifying JWT authentication flows.
/// Tests POST /api/auth/login, POST /api/auth/refresh, POST /api/auth/logout, and GET /api/auth/me.
/// Uses WebApplicationFactory pattern with EF Core InMemory provider for test isolation.
/// </summary>
public sealed class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDatabaseName;
    
    // Test user credentials
    private const string ValidUsername = "testuser";
    private const string ValidEmail = "testuser@example.com";
    private const string ValidPassword = "SecurePassword123!";
    private const string ValidDisplayName = "Test User";
    private const int TestPortalId = 1;
    private const int TestUserId = 100;

    // Locked user credentials
    private const string LockedUsername = "lockeduser";
    private const string LockedEmail = "locked@example.com";
    private const int LockedUserId = 101;

    // Not approved user credentials
    private const string NotApprovedUsername = "notapproved";
    private const string NotApprovedEmail = "notapproved@example.com";
    private const int NotApprovedUserId = 102;

    // Admin user credentials
    private const string AdminUsername = "admin";
    private const string AdminEmail = "admin@example.com";
    private const int AdminUserId = 103;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _testDatabaseName = $"DnnTestDb_{Guid.NewGuid()}";
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<DnnDbContext>));
                
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database for test isolation
                services.AddDbContext<DnnDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDatabaseName);
                });
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Seeds test data before each test.
    /// </summary>
    public async Task InitializeAsync()
    {
        await SeedTestDataAsync();
    }

    /// <summary>
    /// Cleanup after tests.
    /// </summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region Test Data Seeding

    /// <summary>
    /// Seeds test users with known passwords into the InMemory database.
    /// MIGRATION: Test data patterns derived from legacy UserController.vb CreateUser method.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Clear existing data
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();

        var hashedPassword = passwordHasher.HashPassword(ValidPassword);

        // Seed valid test user
        var validUser = new User
        {
            UserId = TestUserId,
            Username = ValidUsername,
            DisplayName = ValidDisplayName,
            FirstName = "Test",
            LastName = "User",
            Email = ValidEmail,
            PasswordHash = hashedPassword,
            IsSuperUser = false,
            PortalId = TestPortalId,
            IsApproved = true,
            IsLockedOut = false,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };

        // Seed locked user
        var lockedUser = new User
        {
            UserId = LockedUserId,
            Username = LockedUsername,
            DisplayName = "Locked User",
            FirstName = "Locked",
            LastName = "User",
            Email = LockedEmail,
            PasswordHash = hashedPassword,
            IsSuperUser = false,
            PortalId = TestPortalId,
            IsApproved = true,
            IsLockedOut = true,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };

        // Seed not approved user
        var notApprovedUser = new User
        {
            UserId = NotApprovedUserId,
            Username = NotApprovedUsername,
            DisplayName = "Not Approved User",
            FirstName = "NotApproved",
            LastName = "User",
            Email = NotApprovedEmail,
            PasswordHash = hashedPassword,
            IsSuperUser = false,
            PortalId = TestPortalId,
            IsApproved = false,
            IsLockedOut = false,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };

        // Seed admin/superuser
        var adminUser = new User
        {
            UserId = AdminUserId,
            Username = AdminUsername,
            DisplayName = "Administrator",
            FirstName = "Admin",
            LastName = "User",
            Email = AdminEmail,
            PasswordHash = hashedPassword,
            IsSuperUser = true,
            PortalId = TestPortalId,
            IsApproved = true,
            IsLockedOut = false,
            CreatedOnDate = DateTime.UtcNow,
            LastModifiedOnDate = DateTime.UtcNow
        };

        context.Users.AddRange(validUser, lockedUser, notApprovedUser, adminUser);
        await context.SaveChangesAsync();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a valid authentication token for the test user.
    /// </summary>
    private async Task<string> GetValidAccessTokenAsync()
    {
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse!.AccessToken;
    }

    /// <summary>
    /// Gets a valid AuthResponse for the test user.
    /// </summary>
    private async Task<AuthResponse> GetValidAuthResponseAsync()
    {
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
        
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>
    /// Creates an HTTP client with authentication header.
    /// </summary>
    private void SetAuthorizationHeader(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Clears the authorization header.
    /// </summary>
    private void ClearAuthorizationHeader()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region Login Tests

    /// <summary>
    /// MIGRATION: Tests successful login replacing Forms Authentication with JWT.
    /// Derived from PortalSecurity.vb authentication patterns.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsAuthResponse_WhenCredentialsValid()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that successful login returns proper JWT token structure.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsAuthResponseWithTokens_WhenValidUsernameAndPassword()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.RefreshToken.Should().NotBeNullOrEmpty();
        authResponse.TokenType.Should().Be("Bearer");
        authResponse.ExpiresIn.Should().BeGreaterThan(0);
        authResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    /// <summary>
    /// Verifies that AuthResponse contains correct user information.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsAuthResponseWithUserInfo_ContainingCorrectFields()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.User.Should().NotBeNull();
        authResponse.User!.UserId.Should().Be(TestUserId);
        authResponse.User.Username.Should().Be(ValidUsername);
        authResponse.User.DisplayName.Should().Be(ValidDisplayName);
        authResponse.User.Email.Should().Be(ValidEmail);
        authResponse.User.IsSuperUser.Should().BeFalse();
        authResponse.User.PortalId.Should().Be(TestPortalId);
    }

    /// <summary>
    /// MIGRATION: Tests invalid username scenario from UserController.vb ValidateUser pattern.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUsernameNotFound()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "nonexistentuser",
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// MIGRATION: Tests invalid password scenario from UserController.vb ValidateUser pattern.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIncorrect()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = "WrongPassword123!",
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// MIGRATION: Tests locked user scenario from UserController.vb user status checks.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserLocked()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = LockedUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// MIGRATION: Tests not approved user scenario from UserController.vb user status checks.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotApproved()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = NotApprovedUsername,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests validation failure when username is empty.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsBadRequest_WhenUsernameEmpty()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = string.Empty,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests validation failure when password is empty.
    /// </summary>
    [Fact]
    public async Task Login_ReturnsBadRequest_WhenPasswordEmpty()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidUsername,
            Password = string.Empty,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// MIGRATION: Tests login by email from UserController.vb GetUserByEmail pattern.
    /// </summary>
    [Fact]
    public async Task Login_SupportsLoginByEmail_WhenEmailProvided()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = ValidEmail,
            Password = ValidPassword,
            RememberMe = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.User.Should().NotBeNull();
        authResponse.User!.Username.Should().Be(ValidUsername);
    }

    #endregion

    #region Refresh Token Tests

    /// <summary>
    /// Verifies that valid refresh token returns new tokens.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsNewTokens_WhenRefreshTokenValid()
    {
        // Arrange
        var authResponse = await GetValidAuthResponseAsync();
        var originalAccessToken = authResponse.AccessToken;
        var refreshToken = authResponse.RefreshToken;

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        };

        // Small delay to ensure new token has different timestamp
        await Task.Delay(100);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var newAuthResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        newAuthResponse.Should().NotBeNull();
        newAuthResponse!.AccessToken.Should().NotBeNullOrEmpty();
        newAuthResponse.RefreshToken.Should().NotBeNullOrEmpty();
        newAuthResponse.TokenType.Should().Be("Bearer");
        newAuthResponse.ExpiresIn.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies that expired refresh token returns unauthorized.
    /// Note: This test simulates an expired token scenario.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenRefreshTokenExpired()
    {
        // Arrange - Use a fake expired token (in practice, the server validates expiration)
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "expired_refresh_token_12345678901234567890"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that invalid refresh token returns unauthorized.
    /// </summary>
    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenRefreshTokenInvalid()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid_token_that_does_not_exist"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout Tests

    /// <summary>
    /// MIGRATION: Tests logout replacing Forms Authentication SignOut from PortalSecurity.vb.
    /// </summary>
    [Fact]
    public async Task Logout_ReturnsNoContent_WhenAuthenticated()
    {
        // Arrange
        var accessToken = await GetValidAccessTokenAsync();
        SetAuthorizationHeader(accessToken);

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        // Cleanup
        ClearAuthorizationHeader();
    }

    /// <summary>
    /// Verifies that logout fails when not authenticated.
    /// </summary>
    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        ClearAuthorizationHeader();

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetCurrentUser Tests

    /// <summary>
    /// Verifies that authenticated user can retrieve their own information.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsUserDto_WhenAuthenticated()
    {
        // Arrange
        var accessToken = await GetValidAccessTokenAsync();
        SetAuthorizationHeader(accessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userDto = await response.Content.ReadFromJsonAsync<UserDto>();
        userDto.Should().NotBeNull();
        userDto!.UserId.Should().Be(TestUserId);
        userDto.Username.Should().Be(ValidUsername);
        userDto.DisplayName.Should().Be(ValidDisplayName);
        userDto.Email.Should().Be(ValidEmail);
        userDto.IsSuperUser.Should().BeFalse();
        userDto.PortalId.Should().Be(TestPortalId);
        userDto.IsApproved.Should().BeTrue();
        userDto.IsLockedOut.Should().BeFalse();
        
        // Cleanup
        ClearAuthorizationHeader();
    }

    /// <summary>
    /// Verifies that unauthenticated user cannot access current user endpoint.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        ClearAuthorizationHeader();

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that expired token returns unauthorized for current user endpoint.
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_ReturnsUnauthorized_WhenTokenExpired()
    {
        // Arrange - Use a malformed/expired-looking token
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZXhwIjoxfQ.invalid";
        SetAuthorizationHeader(expiredToken);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        // Cleanup
        ClearAuthorizationHeader();
    }

    #endregion

    #region Protected Endpoint Tests

    /// <summary>
    /// Verifies that protected endpoints reject requests without a token.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WhenNoToken()
    {
        // Arrange
        ClearAuthorizationHeader();

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies that protected endpoints reject requests with an invalid token.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WhenInvalidToken()
    {
        // Arrange
        var invalidToken = "this.is.not.a.valid.jwt.token";
        SetAuthorizationHeader(invalidToken);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        // Cleanup
        ClearAuthorizationHeader();
    }

    /// <summary>
    /// Verifies that protected endpoints accept requests with a valid Bearer token.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_ReturnsOk_WhenValidBearerToken()
    {
        // Arrange
        var accessToken = await GetValidAccessTokenAsync();
        SetAuthorizationHeader(accessToken);

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var userDto = await response.Content.ReadFromJsonAsync<UserDto>();
        userDto.Should().NotBeNull();
        userDto!.UserId.Should().BeGreaterThan(0);
        
        // Cleanup
        ClearAuthorizationHeader();
    }

    #endregion
}

/// <summary>
/// Request DTO for refresh token endpoint.
/// </summary>
public sealed record RefreshTokenRequest
{
    /// <summary>
    /// The refresh token obtained from login.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}
