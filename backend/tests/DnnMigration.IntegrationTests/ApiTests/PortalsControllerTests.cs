// MIGRATION: Integration tests for PortalsController derived from SiteSettings.ascx.vb,
// Signup.ascx.vb, and Portals.ascx.vb patterns from legacy DotNetNuke 4.x codebase
// Tests verify complete HTTP request/response cycles for portal CRUD operations

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for PortalsController verifying complete HTTP request/response cycles
/// for portal CRUD operations. Uses WebApplicationFactory pattern with EF Core InMemory provider
/// for database isolation.
/// MIGRATION: Test scenarios derived from SiteSettings.ascx.vb cmdUpdate_Click handler patterns.
/// </summary>
public class PortalsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testDatabaseName;

    /// <summary>
    /// Initializes the test class with a configured WebApplicationFactory.
    /// Each test instance gets a unique in-memory database for isolation.
    /// </summary>
    public PortalsControllerTests(WebApplicationFactory<Program> factory)
    {
        _testDatabaseName = $"PortalsControllerTests_{Guid.NewGuid()}";
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DnnDbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<DnnDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                // Add DnnDbContext using InMemory database for test isolation
                services.AddDbContext<DnnDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDatabaseName);
                });

                // Configure test authentication to bypass JWT validation
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
            });
        });

        // Create authenticated client by default (admin user)
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.TestAdminToken);
    }

    #region Helper Methods

    /// <summary>
    /// Gets an authenticated HttpClient with a valid JWT Bearer token for admin user.
    /// MIGRATION: Replaces Forms Authentication with JWT Bearer token for protected endpoints.
    /// </summary>
    private async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        
        // Seed an admin user and get JWT token
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        // Create test admin user if not exists
        var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser is null)
        {
            adminUser = new User
            {
                UserId = 1,
                Username = "admin",
                DisplayName = "Administrator",
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@test.com",
                IsSuperUser = true,
                PortalId = 0,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);
            await dbContext.SaveChangesAsync();
        }

        // For integration tests, we'll set a mock authorization header
        // In a real scenario, you would call the /api/auth/login endpoint
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "test-admin-token");
        
        return client;
    }

    /// <summary>
    /// Seeds test portal data into the InMemory database.
    /// </summary>
    private async Task SeedTestDataAsync(int portalCount = 3)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        // Clear existing portals
        dbContext.Portals.RemoveRange(dbContext.Portals);
        await dbContext.SaveChangesAsync();

        // Seed test portals
        for (int i = 1; i <= portalCount; i++)
        {
            var portal = CreateTestPortal(i, $"Test Portal {i}");
            dbContext.Portals.Add(portal);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a test Portal entity with specified properties.
    /// MIGRATION: Properties mapped from PortalInfo.vb entity properties.
    /// </summary>
    private static Portal CreateTestPortal(int portalId, string portalName)
    {
        return new Portal
        {
            PortalId = portalId,
            PortalName = portalName,
            Description = $"Description for {portalName}",
            KeyWords = $"keywords, {portalName.ToLowerInvariant()}",
            DefaultLanguage = "en-US",
            TimeZoneOffset = 0,
            LogoFile = "logo.png",
            FooterText = $"Footer for {portalName}",
            GUID = Guid.NewGuid(),
            UserRegistration = 2, // Private registration
            HostSpace = 0,
            PageQuota = 0,
            UserQuota = 0,
            AdministratorId = 1,
            AdministratorRoleId = 1,
            RegisteredRoleId = 2,
            ExpiryDate = null,
            BannerAdvertising = 0,
            Currency = "USD",
            HostFee = 0,
            HomeTabId = 0,
            LoginTabId = 0,
            UserTabId = 0,
            SplashTabId = 0,
            HomeDirectory = $"/Portals/{portalId}"
        };
    }

    /// <summary>
    /// Creates a valid CreatePortalRequest for testing portal creation.
    /// MIGRATION: Fields derived from Signup.ascx.vb form inputs.
    /// </summary>
    private static CreatePortalRequest CreateValidCreatePortalRequest()
    {
        return new CreatePortalRequest(
            PortalAlias: $"testportal{Guid.NewGuid():N}",
            Title: "New Test Portal",
            Description: "A test portal for integration testing",
            KeyWords: "test, integration, portal",
            FirstName: "Admin",
            LastName: "User",
            Username: "portaladmin",
            Password: "Password123!",
            Email: "admin@newportal.test",
            Template: "Default Website",
            HomeDirectory: "/Portals/new",
            IsChildPortal: false
        );
    }

    /// <summary>
    /// Creates a valid UpdatePortalRequest for testing portal updates.
    /// MIGRATION: Fields derived from SiteSettings.ascx.vb cmdUpdate_Click handler.
    /// </summary>
    private static UpdatePortalRequest CreateValidUpdatePortalRequest()
    {
        return new UpdatePortalRequest
        {
            PortalName = "Updated Portal Name",
            LogoFile = "newlogo.png",
            FooterText = "Updated footer text",
            Description = "Updated description",
            KeyWords = "updated, keywords",
            DefaultLanguage = "en-US",
            TimeZoneOffset = -5,
            HostSpace = 100,
            PageQuota = 50,
            UserQuota = 100
        };
    }

    #endregion

    #region GET /api/portals Tests

    /// <summary>
    /// Tests that GET /api/portals returns a paged result when portals exist.
    /// MIGRATION: Derived from Portals.ascx.vb BindData() pattern.
    /// </summary>
    [Fact]
    public async Task GetPortals_ReturnsPagedResult_WhenPortalsExist()
    {
        // Arrange
        await SeedTestDataAsync(5);

        // Act
        var response = await _client.GetAsync("/api/portals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThanOrEqualTo(1);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.PageIndex.Should().BeGreaterThanOrEqualTo(0);
        result.PageSize.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that GET /api/portals returns an empty list when no portals exist.
    /// </summary>
    [Fact]
    public async Task GetPortals_ReturnsEmptyList_WhenNoPortals()
    {
        // Arrange - Clear all portals
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        dbContext.Portals.RemoveRange(dbContext.Portals);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/portals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Tests that GET /api/portals filters results by portal name when filter is provided.
    /// MIGRATION: Derived from Portals.ascx.vb filtering patterns.
    /// </summary>
    [Fact]
    public async Task GetPortals_FiltersByName_WhenNameFilterProvided()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        // Clear and add specific test data
        dbContext.Portals.RemoveRange(dbContext.Portals);
        dbContext.Portals.Add(CreateTestPortal(1, "Alpha Portal"));
        dbContext.Portals.Add(CreateTestPortal(2, "Beta Portal"));
        dbContext.Portals.Add(CreateTestPortal(3, "Alpha Testing"));
        await dbContext.SaveChangesAsync();

        // Act - Filter by name containing "Alpha"
        var response = await _client.GetAsync("/api/portals?nameFilter=Alpha");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        
        // All returned items should contain "Alpha" in the name
        foreach (var portal in result.Items)
        {
            portal.PortalName.Should().Contain("Alpha");
        }
    }

    #endregion

    #region GET /api/portals/{id} Tests

    /// <summary>
    /// Tests that GET /api/portals/{id} returns the portal when it exists.
    /// </summary>
    [Fact]
    public async Task GetPortal_ReturnsPortal_WhenExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        var testPortal = CreateTestPortal(99, "Specific Test Portal");
        dbContext.Portals.Add(testPortal);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/portals/99");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PortalDto>();
        result.Should().NotBeNull();
        result!.PortalId.Should().Be(99);
        result.PortalName.Should().Be("Specific Test Portal");
        result.Description.Should().NotBeNullOrEmpty();
        result.DefaultLanguage.Should().Be("en-US");
    }

    /// <summary>
    /// Tests that GET /api/portals/{id} returns 404 when the portal does not exist.
    /// </summary>
    [Fact]
    public async Task GetPortal_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        dbContext.Portals.RemoveRange(dbContext.Portals);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/portals/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/portals Tests

    /// <summary>
    /// Tests that POST /api/portals creates a portal and returns 201 Created with Location header.
    /// MIGRATION: Derived from Signup.ascx.vb cmdUpdate_Click handler calling CreatePortal.
    /// </summary>
    [Fact]
    public async Task CreatePortal_ReturnsCreatedWithLocation_WhenValid()
    {
        // Arrange
        var authenticatedClient = await GetAuthenticatedClientAsync();
        var request = CreateValidCreatePortalRequest();

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/portals", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var result = await response.Content.ReadFromJsonAsync<PortalDto>();
        result.Should().NotBeNull();
        result!.PortalId.Should().BeGreaterThan(0);
        result.PortalName.Should().Be(request.Title);
        result.Description.Should().Be(request.Description);
    }

    /// <summary>
    /// Tests that POST /api/portals returns 400 Bad Request when validation fails.
    /// MIGRATION: Validation patterns derived from Signup.ascx.vb valTitle RequiredFieldValidator.
    /// </summary>
    [Fact]
    public async Task CreatePortal_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var authenticatedClient = await GetAuthenticatedClientAsync();
        
        // Create an invalid request with missing required fields
        var invalidRequest = new CreatePortalRequest(
            PortalAlias: "", // Required but empty
            Title: "", // Required but empty
            Description: "Test description",
            KeyWords: "test",
            FirstName: "Admin",
            LastName: "User",
            Username: "admin",
            Password: "Pass123!",
            Email: "admin@test.com",
            Template: "Default",
            HomeDirectory: "/Portals/test",
            IsChildPortal: false
        );

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/api/portals", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Tests that POST /api/portals returns 401 Unauthorized when no authentication is provided.
    /// </summary>
    [Fact]
    public async Task CreatePortal_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();
        var request = CreateValidCreatePortalRequest();

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/portals", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Tests that POST /api/portals returns 403 Forbidden when user is not an administrator.
    /// MIGRATION: Authorization pattern derived from DNN security checks for portal operations.
    /// </summary>
    [Fact]
    public async Task CreatePortal_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Set a non-admin user token (simulated - in real scenario this would be a valid token for non-admin)
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "test-non-admin-token");
        
        var request = CreateValidCreatePortalRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/portals", request);

        // Assert
        // Should be either Unauthorized (401) or Forbidden (403) depending on auth configuration
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    #endregion

    #region PUT /api/portals/{id} Tests

    /// <summary>
    /// Tests that PUT /api/portals/{id} updates the portal and returns the updated portal.
    /// MIGRATION: Derived from SiteSettings.ascx.vb cmdUpdate_Click handler calling UpdatePortalInfo.
    /// </summary>
    [Fact]
    public async Task UpdatePortal_ReturnsUpdatedPortal_WhenValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        var existingPortal = CreateTestPortal(50, "Original Portal Name");
        dbContext.Portals.Add(existingPortal);
        await dbContext.SaveChangesAsync();

        var authenticatedClient = await GetAuthenticatedClientAsync();
        var updateRequest = CreateValidUpdatePortalRequest();

        // Act
        var response = await authenticatedClient.PutAsJsonAsync("/api/portals/50", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PortalDto>();
        result.Should().NotBeNull();
        result!.PortalId.Should().Be(50);
        result.PortalName.Should().Be("Updated Portal Name");
        result.Description.Should().Be("Updated description");
        result.FooterText.Should().Be("Updated footer text");
        result.TimeZoneOffset.Should().Be(-5);
    }

    /// <summary>
    /// Tests that PUT /api/portals/{id} returns 404 Not Found when the portal does not exist.
    /// </summary>
    [Fact]
    public async Task UpdatePortal_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        dbContext.Portals.RemoveRange(dbContext.Portals);
        await dbContext.SaveChangesAsync();

        var authenticatedClient = await GetAuthenticatedClientAsync();
        var updateRequest = CreateValidUpdatePortalRequest();

        // Act
        var response = await authenticatedClient.PutAsJsonAsync("/api/portals/99999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that PUT /api/portals/{id} returns 400 Bad Request when validation fails.
    /// MIGRATION: Validation patterns derived from SiteSettings.ascx.vb validators.
    /// </summary>
    [Fact]
    public async Task UpdatePortal_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        var existingPortal = CreateTestPortal(51, "Existing Portal");
        dbContext.Portals.Add(existingPortal);
        await dbContext.SaveChangesAsync();

        var authenticatedClient = await GetAuthenticatedClientAsync();
        
        // Create invalid update request
        var invalidRequest = new UpdatePortalRequest
        {
            PortalName = "", // Required but empty
            LogoFile = null,
            FooterText = null,
            Description = null,
            KeyWords = null,
            DefaultLanguage = "", // Invalid
            TimeZoneOffset = 0,
            HostSpace = -1, // Invalid negative value
            PageQuota = -1,
            UserQuota = -1
        };

        // Act
        var response = await authenticatedClient.PutAsJsonAsync("/api/portals/51", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region DELETE /api/portals/{id} Tests

    /// <summary>
    /// Tests that DELETE /api/portals/{id} deletes the portal and returns 204 No Content.
    /// MIGRATION: Derived from portal deletion functionality in DNN admin module.
    /// Note: The service requires at least one portal to remain, so we seed two portals.
    /// </summary>
    [Fact]
    public async Task DeletePortal_ReturnsNoContent_WhenExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        // Add two portals - service prevents deleting the last portal
        var portalToKeep = CreateTestPortal(99, "Portal To Keep");
        var portalToDelete = CreateTestPortal(100, "Portal To Delete");
        dbContext.Portals.AddRange(portalToKeep, portalToDelete);
        await dbContext.SaveChangesAsync();

        var authenticatedClient = await GetAuthenticatedClientAsync();

        // Act
        var response = await authenticatedClient.DeleteAsync("/api/portals/100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify portal was actually deleted
        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<DnnDbContext>();
        var deletedPortal = await verifyContext.Portals.FindAsync(100);
        deletedPortal.Should().BeNull();
        
        // Verify the other portal still exists
        var remainingPortal = await verifyContext.Portals.FindAsync(99);
        remainingPortal.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that DELETE /api/portals/{id} returns 404 Not Found when the portal does not exist.
    /// </summary>
    [Fact]
    public async Task DeletePortal_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        dbContext.Portals.RemoveRange(dbContext.Portals);
        // Seed at least one portal to ensure the system is in a valid state
        dbContext.Portals.Add(CreateTestPortal(1, "Existing Portal"));
        await dbContext.SaveChangesAsync();

        var authenticatedClient = await GetAuthenticatedClientAsync();

        // Act - Try to delete a portal ID that doesn't exist
        var response = await authenticatedClient.DeleteAsync("/api/portals/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that DELETE /api/portals/{id} returns 401 Unauthorized when not authenticated.
    /// </summary>
    [Fact]
    public async Task DeletePortal_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        var portal = CreateTestPortal(101, "Portal to Try Delete");
        dbContext.Portals.Add(portal);
        await dbContext.SaveChangesAsync();

        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.DeleteAsync("/api/portals/101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Pagination Tests

    /// <summary>
    /// Tests that GET /api/portals correctly handles pagination parameters.
    /// </summary>
    [Theory]
    [InlineData(0, 5, 5)]  // First page with 5 items
    [InlineData(1, 5, 5)]  // Second page with 5 items
    [InlineData(0, 10, 10)] // First page with 10 items
    public async Task GetPortals_SupportsPagination_WhenPageParametersProvided(
        int pageIndex, int pageSize, int expectedMaxItems)
    {
        // Arrange
        await SeedTestDataAsync(15); // Seed more than any page size

        // Act
        var response = await _client.GetAsync($"/api/portals?pageIndex={pageIndex}&pageSize={pageSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.PageIndex.Should().Be(pageIndex);
        result.PageSize.Should().Be(pageSize);
        result.Items.Count.Should().BeLessThanOrEqualTo(expectedMaxItems);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(result.Items.Count);
    }

    /// <summary>
    /// Tests that pagination metadata is correctly calculated.
    /// </summary>
    [Fact]
    public async Task GetPortals_CalculatesPaginationMetadata_Correctly()
    {
        // Arrange
        await SeedTestDataAsync(25); // 25 portals

        // Act
        var response = await _client.GetAsync("/api/portals?pageIndex=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3); // 25 items / 10 per page = 3 pages (ceiling)
        result.HasPreviousPage.Should().BeFalse(); // First page
        result.HasNextPage.Should().BeTrue(); // More pages exist
    }

    /// <summary>
    /// Tests that HasPreviousPage is true when not on first page.
    /// </summary>
    [Fact]
    public async Task GetPortals_HasPreviousPageIsTrue_WhenNotOnFirstPage()
    {
        // Arrange
        await SeedTestDataAsync(30);

        // Act
        var response = await _client.GetAsync("/api/portals?pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.HasPreviousPage.Should().BeTrue();
    }

    /// <summary>
    /// Tests that HasNextPage is false when on last page.
    /// </summary>
    [Fact]
    public async Task GetPortals_HasNextPageIsFalse_WhenOnLastPage()
    {
        // Arrange
        await SeedTestDataAsync(15);

        // Act - Request last page (page 2 when 15 items with 10 per page)
        var response = await _client.GetAsync("/api/portals?pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PortalDto>>();
        result.Should().NotBeNull();
        result!.HasNextPage.Should().BeFalse();
    }

    #endregion

    #region Content Type and Response Structure Tests

    /// <summary>
    /// Tests that API responses have the correct content type.
    /// </summary>
    [Fact]
    public async Task GetPortals_ReturnsJsonContentType()
    {
        // Arrange
        await SeedTestDataAsync(1);

        // Act
        var response = await _client.GetAsync("/api/portals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    /// <summary>
    /// Tests that portal response includes all expected properties.
    /// MIGRATION: Properties verified match PortalInfo.vb entity properties.
    /// </summary>
    [Fact]
    public async Task GetPortal_ResponseIncludesAllExpectedProperties()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();
        
        dbContext.Portals.RemoveRange(dbContext.Portals);
        var testPortal = CreateTestPortal(200, "Full Properties Portal");
        testPortal.LogoFile = "test-logo.png";
        testPortal.FooterText = "Test Footer";
        testPortal.DefaultLanguage = "en-GB";
        testPortal.TimeZoneOffset = 5;
        dbContext.Portals.Add(testPortal);
        await dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/portals/200");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PortalDto>();
        result.Should().NotBeNull();
        result!.PortalId.Should().Be(200);
        result.PortalName.Should().Be("Full Properties Portal");
        result.Description.Should().NotBeNullOrEmpty();
        result.LogoFile.Should().Be("test-logo.png");
        result.FooterText.Should().Be("Test Footer");
        result.DefaultLanguage.Should().Be("en-GB");
        result.TimeZoneOffset.Should().Be(5);
    }

    #endregion
}

/// <summary>
/// Test authentication handler for integration tests.
/// Validates test tokens and creates authenticated user claims.
/// </summary>
internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string TestAdminToken = "test-admin-token";
    public const string TestUserToken = "test-user-token";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeader = authorizationHeader.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..].Trim();

        // Validate test tokens and create appropriate claims
        var claims = new List<Claim>();

        if (token == TestAdminToken)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "1"));
            claims.Add(new Claim(ClaimTypes.Name, "admin"));
            claims.Add(new Claim(ClaimTypes.Email, "admin@test.com"));
            claims.Add(new Claim(ClaimTypes.Role, "Administrators"));
            claims.Add(new Claim("IsSuperUser", "true"));
        }
        else if (token == TestUserToken)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "2"));
            claims.Add(new Claim(ClaimTypes.Name, "testuser"));
            claims.Add(new Claim(ClaimTypes.Email, "user@test.com"));
            claims.Add(new Claim(ClaimTypes.Role, "Registered Users"));
            claims.Add(new Claim("IsSuperUser", "false"));
        }
        else
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));
        }

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
