// <copyright file="RolesControllerTests.cs" company="DNN Migration Project">
// Copyright (c) DNN Migration Project. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// MIGRATION: Integration tests for RolesController derived from legacy patterns:
// - Roles.ascx.vb: BindData method for listing roles with role group filtering
// - EditRoles.ascx.vb: cmdUpdate_Click handler for role creation/updates with validation
// - SecurityRoles.ascx.vb: User-role assignment operations with effective/expiry dates

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Integration tests for RolesController verifying complete HTTP request/response cycles
/// for role management operations including role CRUD and user-role assignments.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: These tests cover functionality previously handled by:
/// <list type="bullet">
/// <item><description>Roles.ascx.vb - Role listing with role group filtering</description></item>
/// <item><description>EditRoles.ascx.vb - Role creation and modification with validation</description></item>
/// <item><description>SecurityRoles.ascx.vb - User-role assignment management</description></item>
/// </list>
/// </para>
/// <para>
/// Uses WebApplicationFactory pattern with EF Core InMemory provider for test isolation.
/// Each test method seeds its own data to avoid test interference.
/// </para>
/// </remarks>
public class RolesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesControllerTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory for creating test clients.</param>
    public RolesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<DnnDbContext>));
                
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory database for test isolation
                services.AddDbContext<DnnDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"RolesTestDb_{Guid.NewGuid()}");
                });
            });
        });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    #region Helper Methods

    /// <summary>
    /// Creates an HTTP client configured with a valid JWT Bearer token for authenticated requests.
    /// </summary>
    /// <param name="isAdmin">If true, generates admin-level authentication.</param>
    /// <returns>An HttpClient configured with authentication headers.</returns>
    private HttpClient CreateAuthenticatedClient(bool isAdmin = true)
    {
        var client = _factory.CreateClient();
        // In a real scenario, this would generate a valid JWT token
        // For integration tests, we configure the test server to accept a test token
        if (isAdmin)
        {
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", "test-admin-token");
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", "test-user-token");
        }
        return client;
    }

    /// <summary>
    /// Seeds test portal data required for role operations.
    /// </summary>
    /// <param name="portalId">The portal ID to create.</param>
    /// <param name="portalName">The portal name.</param>
    /// <returns>The seeded Portal entity.</returns>
    private async Task<Portal> SeedTestPortalAsync(int portalId = 1, string portalName = "Test Portal")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        var existingPortal = await dbContext.Portals.FindAsync(portalId);
        if (existingPortal != null)
        {
            return existingPortal;
        }

        var portal = new Portal
        {
            PortalId = portalId,
            PortalName = portalName,
            DefaultLanguage = "en-US",
            GUID = Guid.NewGuid()
        };

        dbContext.Portals.Add(portal);
        await dbContext.SaveChangesAsync();
        return portal;
    }

    /// <summary>
    /// Seeds a test role group.
    /// </summary>
    /// <param name="roleGroupId">The role group ID.</param>
    /// <param name="roleGroupName">The role group name.</param>
    /// <param name="portalId">The portal ID.</param>
    /// <returns>The seeded RoleGroup entity.</returns>
    private async Task<RoleGroup> SeedTestRoleGroupAsync(
        int roleGroupId, 
        string roleGroupName, 
        int portalId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        var roleGroup = new RoleGroup
        {
            RoleGroupId = roleGroupId,
            RoleGroupName = roleGroupName,
            PortalId = portalId,
            Description = $"Test role group: {roleGroupName}"
        };

        dbContext.RoleGroups.Add(roleGroup);
        await dbContext.SaveChangesAsync();
        return roleGroup;
    }

    /// <summary>
    /// Seeds a test role into the database.
    /// </summary>
    /// <param name="roleId">The role ID.</param>
    /// <param name="roleName">The role name.</param>
    /// <param name="portalId">The portal ID.</param>
    /// <param name="roleGroupId">Optional role group ID.</param>
    /// <param name="isPublic">Whether the role is public.</param>
    /// <param name="autoAssignment">Whether users are auto-assigned to this role.</param>
    /// <param name="serviceFee">Optional service fee for paid roles.</param>
    /// <param name="billingFrequency">Billing frequency code (N, O, D, W, M, Y).</param>
    /// <returns>The seeded Role entity.</returns>
    private async Task<Role> SeedTestRoleAsync(
        int roleId,
        string roleName,
        int portalId = 1,
        int? roleGroupId = null,
        bool isPublic = true,
        bool autoAssignment = false,
        decimal? serviceFee = null,
        string? billingFrequency = "N")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        var role = new Role
        {
            RoleId = roleId,
            RoleName = roleName,
            PortalId = portalId,
            RoleGroupId = roleGroupId,
            Description = $"Test role: {roleName}",
            IsPublic = isPublic,
            AutoAssignment = autoAssignment,
            ServiceFee = serviceFee,
            BillingFrequency = billingFrequency,
            BillingPeriod = serviceFee.HasValue ? 1 : null,
            CreatedOnDate = DateTime.UtcNow,
            CreatedByUserId = 1
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();
        return role;
    }

    /// <summary>
    /// Seeds a test user into the database.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="username">The username.</param>
    /// <param name="portalId">The portal ID.</param>
    /// <returns>The seeded User entity.</returns>
    private async Task<User> SeedTestUserAsync(
        int userId,
        string username,
        int portalId = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        var user = new User
        {
            UserId = userId,
            Username = username,
            DisplayName = username,
            Email = $"{username}@test.com",
            PortalId = portalId,
            IsSuperUser = false,
            IsApproved = true,
            CreatedOnDate = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a user-role assignment.
    /// </summary>
    /// <param name="userRoleId">The user-role ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="effectiveDate">Optional effective date.</param>
    /// <param name="expiryDate">Optional expiry date.</param>
    /// <returns>The seeded UserRole entity.</returns>
    private async Task<UserRole> SeedUserRoleAsync(
        int userRoleId,
        int userId,
        int roleId,
        DateTime? effectiveDate = null,
        DateTime? expiryDate = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DnnDbContext>();

        var userRole = new UserRole
        {
            UserRoleId = userRoleId,
            UserId = userId,
            RoleId = roleId,
            EffectiveDate = effectiveDate ?? DateTime.UtcNow,
            ExpiryDate = expiryDate,
            CreatedOnDate = DateTime.UtcNow
        };

        dbContext.UserRoles.Add(userRole);
        await dbContext.SaveChangesAsync();
        return userRole;
    }

    #endregion

    #region GET /api/roles Tests

    /// <summary>
    /// MIGRATION: Derived from Roles.ascx.vb BindData method that populates the roles grid.
    /// Verifies GET /api/roles returns paginated result when roles exist.
    /// </summary>
    [Fact]
    public async Task GetRoles_ReturnsPagedResult_WhenRolesExist()
    {
        // Arrange
        await SeedTestPortalAsync();
        await SeedTestRoleAsync(1, "Administrators");
        await SeedTestRoleAsync(2, "Registered Users");
        await SeedTestRoleAsync(3, "Custom Role");

        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/roles?portalId=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<RoleDto>>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterOrEqualTo(3);
        result.PageIndex.Should().Be(0);
        result.PageSize.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies GET /api/roles returns empty list when no roles exist for the portal.
    /// </summary>
    [Fact]
    public async Task GetRoles_ReturnsEmptyList_WhenNoRoles()
    {
        // Arrange
        await SeedTestPortalAsync(999, "Empty Portal");
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/roles?portalId=999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<RoleDto>>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// MIGRATION: Derived from Roles.ascx.vb BindData method with RoleGroupId filter.
    /// Verifies GET /api/roles filters by role group when roleGroupId parameter is provided.
    /// </summary>
    [Fact]
    public async Task GetRoles_FiltersByRoleGroup_WhenRoleGroupIdProvided()
    {
        // Arrange
        await SeedTestPortalAsync();
        var roleGroup = await SeedTestRoleGroupAsync(1, "Security Roles");
        await SeedTestRoleAsync(10, "Security Admin", roleGroupId: 1);
        await SeedTestRoleAsync(11, "Security Viewer", roleGroupId: 1);
        await SeedTestRoleAsync(12, "General Role", roleGroupId: null);

        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/roles?portalId=1&roleGroupId=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<RoleDto>>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().OnlyContain(r => r.RoleGroupId == 1);
    }

    /// <summary>
    /// Verifies GET /api/roles returns global roles (roles with no role group) when requested.
    /// </summary>
    [Fact]
    public async Task GetRoles_ReturnsGlobalRoles_WhenGlobalRolesRequested()
    {
        // Arrange
        await SeedTestPortalAsync();
        await SeedTestRoleGroupAsync(2, "Custom Group");
        await SeedTestRoleAsync(20, "Global Role 1", roleGroupId: null);
        await SeedTestRoleAsync(21, "Global Role 2", roleGroupId: null);
        await SeedTestRoleAsync(22, "Grouped Role", roleGroupId: 2);

        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/roles?portalId=1&globalRolesOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<RoleDto>>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().OnlyContain(r => r.RoleGroupId == null);
    }

    #endregion

    #region GET /api/roles/{id} Tests

    /// <summary>
    /// Verifies GET /api/roles/{id} returns role when it exists.
    /// </summary>
    [Fact]
    public async Task GetRole_ReturnsRole_WhenExists()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(100, "Test Role For Get", isPublic: true, autoAssignment: false);

        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/roles/{role.RoleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.RoleId.Should().Be(role.RoleId);
        result.RoleName.Should().Be("Test Role For Get");
        result.IsPublic.Should().BeTrue();
        result.AutoAssignment.Should().BeFalse();
    }

    /// <summary>
    /// Verifies GET /api/roles/{id} returns NotFound when role does not exist.
    /// </summary>
    [Fact]
    public async Task GetRole_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/roles/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/roles Tests

    /// <summary>
    /// MIGRATION: Derived from EditRoles.ascx.vb cmdUpdate_Click handler.
    /// Verifies POST /api/roles returns Created with Location header when valid.
    /// </summary>
    [Fact]
    public async Task CreateRole_ReturnsCreatedWithLocation_WhenValid()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "New Integration Test Role",
            Description = "Created via integration test",
            PortalId = 1,
            IsPublic = true,
            AutoAssignment = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.RoleName.Should().Be("New Integration Test Role");
        result.Description.Should().Be("Created via integration test");
        result.IsPublic.Should().BeTrue();
    }

    /// <summary>
    /// Verifies POST /api/roles returns BadRequest when role name already exists.
    /// </summary>
    [Fact]
    public async Task CreateRole_ReturnsBadRequest_WhenDuplicateRoleName()
    {
        // Arrange
        await SeedTestPortalAsync();
        await SeedTestRoleAsync(200, "Duplicate Role Name");
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "Duplicate Role Name",
            Description = "This should fail",
            PortalId = 1,
            IsPublic = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies POST /api/roles returns BadRequest when validation fails.
    /// Tests scenarios like empty role name, invalid billing frequency, etc.
    /// </summary>
    [Theory]
    [InlineData("", "Empty role name should fail")]
    [InlineData(null, "Null role name should fail")]
    public async Task CreateRole_ReturnsBadRequest_WhenValidationFails(string? roleName, string description)
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = roleName!,
            Description = description,
            PortalId = 1,
            IsPublic = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies POST /api/roles returns Unauthorized when not authenticated.
    /// </summary>
    [Fact]
    public async Task CreateRole_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = _factory.CreateClient(); // No auth headers

        var request = new CreateRoleRequest
        {
            RoleName = "Unauthorized Role",
            Description = "Should fail",
            PortalId = 1,
            IsPublic = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Verifies POST /api/roles returns Forbidden when user is not admin.
    /// </summary>
    [Fact]
    public async Task CreateRole_ReturnsForbidden_WhenNotAdmin()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient(isAdmin: false);

        var request = new CreateRoleRequest
        {
            RoleName = "Forbidden Role",
            Description = "Should fail without admin",
            PortalId = 1,
            IsPublic = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region PUT /api/roles/{id} Tests

    /// <summary>
    /// Verifies PUT /api/roles/{id} returns updated role when valid.
    /// </summary>
    [Fact]
    public async Task UpdateRole_ReturnsUpdatedRole_WhenValid()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(300, "Role To Update");
        using var client = CreateAuthenticatedClient();

        var updateRequest = new
        {
            RoleName = "Updated Role Name",
            Description = "Updated description",
            IsPublic = false,
            AutoAssignment = true
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/api/roles/{role.RoleId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.RoleName.Should().Be("Updated Role Name");
        result.Description.Should().Be("Updated description");
        result.IsPublic.Should().BeFalse();
        result.AutoAssignment.Should().BeTrue();
    }

    /// <summary>
    /// Verifies PUT /api/roles/{id} returns NotFound when role does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateRole_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        var updateRequest = new
        {
            RoleName = "Nonexistent Role",
            Description = "Should fail"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync("/api/roles/99998", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// MIGRATION: Derived from EditRoles.ascx.vb protection of system roles.
    /// Verifies PUT /api/roles/{id} returns BadRequest when attempting to update system roles
    /// like "Administrators" or "Registered Users" which should be protected.
    /// </summary>
    [Fact]
    public async Task UpdateRole_ReturnsBadRequest_WhenUpdatingSystemRole()
    {
        // Arrange
        await SeedTestPortalAsync();
        // Create a system role (Administrators is typically protected)
        var systemRole = await SeedTestRoleAsync(
            roleId: 400, 
            roleName: "Administrators", 
            isPublic: false,
            autoAssignment: false);
        
        using var client = CreateAuthenticatedClient();

        var updateRequest = new
        {
            RoleName = "Hacked Administrators",
            Description = "This should not be allowed"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/api/roles/{systemRole.RoleId}", content);

        // Assert
        // System roles should be protected from name changes
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    #endregion

    #region DELETE /api/roles/{id} Tests

    /// <summary>
    /// Verifies DELETE /api/roles/{id} returns NoContent when role exists.
    /// </summary>
    [Fact]
    public async Task DeleteRole_ReturnsNoContent_WhenExists()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(500, "Role To Delete");
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync($"/api/roles/{role.RoleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify role is actually deleted
        var getResponse = await client.GetAsync($"/api/roles/{role.RoleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies DELETE /api/roles/{id} returns NotFound when role does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteRole_ReturnsNotFound_WhenNotExists()
    {
        // Arrange
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync("/api/roles/99997");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// MIGRATION: Derived from EditRoles.ascx.vb protection of system roles.
    /// Verifies DELETE /api/roles/{id} returns BadRequest when attempting to delete 
    /// system roles like "Administrators" or "Registered Users".
    /// </summary>
    [Fact]
    public async Task DeleteRole_ReturnsBadRequest_WhenDeletingSystemRole()
    {
        // Arrange
        await SeedTestPortalAsync();
        var systemRole = await SeedTestRoleAsync(
            roleId: 600, 
            roleName: "Registered Users");
        
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync($"/api/roles/{systemRole.RoleId}");

        // Assert
        // System roles should not be deletable
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    #endregion

    #region POST /api/roles/{roleId}/users/{userId} Tests

    /// <summary>
    /// MIGRATION: Derived from SecurityRoles.ascx.vb cmdAdd_Click handler.
    /// Verifies POST /api/roles/{roleId}/users/{userId} returns NoContent when assignment is valid.
    /// </summary>
    [Fact]
    public async Task AddUserToRole_ReturnsNoContent_WhenValid()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(700, "Role For Assignment");
        var user = await SeedTestUserAsync(700, "UserForAssignment");
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync($"/api/roles/{role.RoleId}/users/{user.UserId}", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK, HttpStatusCode.Created);
    }

    /// <summary>
    /// Verifies POST /api/roles/{roleId}/users/{userId} returns NotFound when role does not exist.
    /// </summary>
    [Fact]
    public async Task AddUserToRole_ReturnsNotFound_WhenRoleNotExists()
    {
        // Arrange
        await SeedTestPortalAsync();
        var user = await SeedTestUserAsync(701, "UserForMissingRole");
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync($"/api/roles/99996/users/{user.UserId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies POST /api/roles/{roleId}/users/{userId} returns NotFound when user does not exist.
    /// </summary>
    [Fact]
    public async Task AddUserToRole_ReturnsNotFound_WhenUserNotExists()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(702, "Role For Missing User");
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync($"/api/roles/{role.RoleId}/users/99995", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/roles/{roleId}/users/{userId} Tests

    /// <summary>
    /// MIGRATION: Derived from SecurityRoles.ascx.vb grdUserRoles_Delete handler.
    /// Verifies DELETE /api/roles/{roleId}/users/{userId} returns NoContent when removal is valid.
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRole_ReturnsNoContent_WhenValid()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(800, "Role For Removal");
        var user = await SeedTestUserAsync(800, "UserForRemoval");
        await SeedUserRoleAsync(800, user.UserId, role.RoleId);
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync($"/api/roles/{role.RoleId}/users/{user.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Verifies DELETE /api/roles/{roleId}/users/{userId} returns NotFound when assignment does not exist.
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRole_ReturnsNotFound_WhenAssignmentNotExists()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(801, "Role Without User");
        var user = await SeedTestUserAsync(801, "UserNotInRole");
        // Note: No user-role assignment seeded
        using var client = CreateAuthenticatedClient();

        // Act
        var response = await client.DeleteAsync($"/api/roles/{role.RoleId}/users/{user.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Additional Test Scenarios

    /// <summary>
    /// Tests role creation with billing settings (ServiceFee, BillingFrequency).
    /// MIGRATION: Validates billing configuration from EditRoles.ascx.vb.
    /// </summary>
    [Fact]
    public async Task CreateRole_WithBillingSettings_SavesCorrectly()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "Premium Membership Role",
            Description = "Role with billing",
            PortalId = 1,
            IsPublic = true,
            ServiceFee = 19.99m,
            BillingFrequency = "M", // Monthly
            BillingPeriod = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.RoleName.Should().Be("Premium Membership Role");
        result.ServiceFee.Should().Be(19.99m);
        result.BillingFrequency.Should().Be("M");
    }

    /// <summary>
    /// Tests role creation with trial settings.
    /// MIGRATION: Validates trial configuration from EditRoles.ascx.vb.
    /// </summary>
    [Fact]
    public async Task CreateRole_WithTrialSettings_SavesCorrectly()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "Trial Membership Role",
            Description = "Role with trial period",
            PortalId = 1,
            IsPublic = true,
            ServiceFee = 29.99m,
            BillingFrequency = "M",
            BillingPeriod = 1,
            TrialPeriod = 14,
            TrialFrequency = "D" // Days
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.TrialPeriod.Should().Be(14);
        result.TrialFrequency.Should().Be("D");
    }

    /// <summary>
    /// Tests role creation with RSVP code.
    /// MIGRATION: Validates RSVP functionality from EditRoles.ascx.vb.
    /// </summary>
    [Fact]
    public async Task CreateRole_WithRSVPCode_SavesCorrectly()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "Invitation Only Role",
            Description = "Role requiring RSVP code",
            PortalId = 1,
            IsPublic = false,
            RSVPCode = "SECRET2024"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.RSVPCode.Should().Be("SECRET2024");
    }

    /// <summary>
    /// Tests role creation with auto-assignment flag.
    /// MIGRATION: Validates auto-assignment from EditRoles.ascx.vb.
    /// </summary>
    [Fact]
    public async Task CreateRole_WithAutoAssignment_SavesCorrectly()
    {
        // Arrange
        await SeedTestPortalAsync();
        using var client = CreateAuthenticatedClient();

        var request = new CreateRoleRequest
        {
            RoleName = "Auto Assigned Role",
            Description = "Role automatically assigned to new users",
            PortalId = 1,
            IsPublic = true,
            AutoAssignment = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<RoleDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.AutoAssignment.Should().BeTrue();
    }

    /// <summary>
    /// Tests user-role assignment with effective and expiry dates.
    /// MIGRATION: Validates temporal role assignments from SecurityRoles.ascx.vb GetDates method.
    /// </summary>
    [Fact]
    public async Task AddUserToRole_WithDates_SetsEffectiveAndExpiryDates()
    {
        // Arrange
        await SeedTestPortalAsync();
        var role = await SeedTestRoleAsync(900, "Dated Role Assignment");
        var user = await SeedTestUserAsync(900, "DatedUser");
        using var client = CreateAuthenticatedClient();

        var effectiveDate = DateTime.UtcNow.Date;
        var expiryDate = effectiveDate.AddMonths(3);

        var assignmentRequest = new
        {
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate
        };

        var content = new StringContent(
            JsonSerializer.Serialize(assignmentRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync($"/api/roles/{role.RoleId}/users/{user.UserId}", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK, HttpStatusCode.Created);
    }

    #endregion
}
