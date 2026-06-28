// MIGRATION: [CP-final review - RolesController / IRoleService / RoleService user-role assignment WRITE workflow].
// HTTP-level integration coverage for the user-role ASSIGNMENT endpoints that the CP-final review flagged as
// deferred/omitted from the service contract. They are now IMPLEMENTED and exercised end-to-end here:
//   * POST   /api/roles/assignments            -> 201 (assign user to role)            [AssignUserRoleAsync]
//   * PUT    /api/roles/assignments            -> 200 (recompute expiry / cancel)      [UpdateUserRoleAsync]
//   * DELETE /api/roles/{roleId}/users/{userId} -> 204 (remove assignment)             [RemoveUserRoleAsync]
// Running through the real Program.cs pipeline (CustomWebApplicationFactory + EF Core InMemory) proves:
//   (1) the new literal "assignments" route and the "{roleId}/users/{userId}" route do NOT collide with the
//       existing "{id:int}" role routes (the :int constraint disambiguates "assignments" from an id);
//   (2) FluentValidation is wired (AssignUserRoleValidator: RoleId > 0 -> 400 before the action runs);
//   (3) the CanRemoveUserFromRole guard (RoleController.vb L741/L764) is enforced through the HTTP surface
//       (removal from the portal's Registered Users role -> 400, assignment retained).
//
// Source lineage: legacy Library/Components/Security/Roles/RoleController.vb (AddUserRole L277/L295,
// DeleteUserRole L330, CanRemoveUserFromRole L741/L764, UpdateUserRole L472/L489) + the
// Website/admin/Security/SecurityRoles.ascx.vb assignment grid (ViewState/postback discarded).
//
// MIGRATION: a Portal + Role + User are seeded at a NON-ZERO PortalId (7). The EF Core InMemory identity generator
// reassigns a default-valued (0) int key, so a non-zero PortalId is required to round-trip the seeded Portal; the
// Portal's presence makes DELETE actually exercise removal + the guard (rather than the legacy missing-portal no-op).
// The TestAuthHandler super-user (isSuperUser=true) bypasses ApiControllerBase.EnforceTenant, so it administers portal 7.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DnnMigration.Application.DTOs.Role;
using FluentAssertions;
using Xunit;
using PortalEntity = DnnMigration.Domain.Entities.Portal;
using RoleEntity = DnnMigration.Domain.Entities.Role;
using UserEntity = DnnMigration.Domain.Entities.User;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// End-to-end integration coverage for the <c>RolesController</c> user-role assignment sub-resource
/// (<c>/api/roles/assignments</c> and <c>/api/roles/{roleId}/users/{userId}</c>). Runs against the real API host
/// supplied by <see cref="CustomWebApplicationFactory"/> (EF Core InMemory + always-authenticated test super-user).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RoleAssignmentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const int PortalId = 7;
    private const int RoleId = 50;
    private const int UserId = 5;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RoleAssignmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_Assignment_Returns201_AndPersists()
    {
        SeedPortalRoleUser();

        // POST -> 201. AssignUserRoleAsync validates the role + user exist (both seeded) and upserts the [UserRoles] row.
        var response = await _client.PostAsync(
            "/api/roles/assignments",
            JsonContent.Create(
                new AssignUserRoleRequest { PortalId = PortalId, UserId = UserId, RoleId = RoleId },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("HandleCreated emits a CreatedAtAction Location header");

        var dto = await ReadUserRoleAsync(response);
        dto.UserId.Should().Be(UserId);
        dto.RoleId.Should().Be(RoleId);

        // The assignment is queryable through GET /api/roles/user/{userId}.
        var list = await GetUserRolesAsync();
        list.Should().ContainSingle(ur => ur.RoleId == RoleId && ur.UserId == UserId);
    }

    // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #18 - role-assignment PortalId contract alignment].
    // PortalId 0 is the valid first/default DNN portal: the JWT "portalId" claim and the Angular admin UI
    // both default to 0, and the canonical FluentValidation contract across the stack
    // (CreateModuleValidator, CreateUserValidator, ForgotPasswordValidator, LoginRequestValidator) is
    // GreaterThanOrEqualTo(0). AssignUserRoleValidator previously used GreaterThan(0), so a portal-0
    // assignment - the exact context the admin UI uses - was rejected at the API boundary with
    // 400 "A valid Portal must be specified." (QA reproduction: POST {portalId:0,userId:1,roleId:1}).
    // This end-to-end test seeds a Role + User at PortalId 0 (the non-zero RoleId/UserId primary keys
    // round-trip the EF Core InMemory identity generator; the PortalId COLUMN value 0 is a plain scalar
    // and is preserved) and proves the corrected validator now lets the request through to the service,
    // which completes the assignment and returns 201 - NOT a 400 portal-validation rejection.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_Assignment_WithPortalIdZero_Returns201_NotPortalValidationError()
    {
        const int portalZero = 0;
        const int roleIdZeroPortal = 1;
        const int userIdZeroPortal = 1;

        _factory.ResetAndSeed(db =>
        {
            db.Set<RoleEntity>().Add(new RoleEntity
            {
                RoleId = roleIdZeroPortal,
                PortalId = portalZero,
                RoleName = "Registered Users",
                BillingFrequency = "N",
                TrialFrequency = "N"
            });
            db.Set<UserEntity>().Add(new UserEntity
            {
                UserId = userIdZeroPortal,
                PortalId = portalZero,
                Username = "member0",
                IsApproved = true
            });
            db.SaveChanges();
        });

        var response = await _client.PostAsync(
            "/api/roles/assignments",
            JsonContent.Create(
                new AssignUserRoleRequest { PortalId = portalZero, UserId = userIdZeroPortal, RoleId = roleIdZeroPortal },
                options: EnvelopeReader.Web));

        // The corrected validator (GreaterThanOrEqualTo(0)) lets PortalId 0 through; the service completes the
        // assignment -> 201 Created. The previous contract returned 400 with "A valid Portal must be specified.".
        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "PortalId 0 is the valid default DNN portal and must not be rejected by the role-assignment validator");

        var dto = await ReadUserRoleAsync(response);
        dto.UserId.Should().Be(userIdZeroPortal);
        dto.RoleId.Should().Be(roleIdZeroPortal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_Assignment_Returns200()
    {
        SeedPortalRoleUser();
        await AssignAsync();

        // PUT (Cancel = false) -> 200. The role's billing/trial frequency "N" recomputes the expiry to null.
        var response = await _client.PutAsync(
            "/api/roles/assignments",
            JsonContent.Create(
                new UpdateUserRoleRequest { PortalId = PortalId, UserId = UserId, RoleId = RoleId, Cancel = false },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadUserRoleAsync(response);
        dto.UserId.Should().Be(UserId);
        dto.RoleId.Should().Be(RoleId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_Assignment_Returns204_AndRemoves()
    {
        SeedPortalRoleUser();
        await AssignAsync();

        // DELETE -> 204. The seeded Portal's Administrators/Registered roles differ from RoleId, so the guard permits it.
        var response = await _client.DeleteAsync($"/api/roles/{RoleId}/users/{UserId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The assignment is gone.
        var list = await GetUserRolesAsync();
        list.Should().NotContain(ur => ur.RoleId == RoleId && ur.UserId == UserId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_Assignment_InvalidRoleId_Returns400()
    {
        SeedPortalRoleUser();

        // AssignUserRoleValidator requires RoleId > 0; FluentValidation auto-validation rejects before the action runs.
        var response = await _client.PostAsync(
            "/api/roles/assignments",
            JsonContent.Create(
                new AssignUserRoleRequest { PortalId = PortalId, UserId = UserId, RoleId = 0 },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_RegisteredRoleAssignment_IsBlocked_Returns400()
    {
        // MIGRATION: CanRemoveUserFromRole (RoleController.vb L741/L764) - NO user may be removed from the portal's
        // Registered Users role. Seed so the target role IS the Registered role; the guard must block the removal.
        _factory.ResetAndSeed(db =>
        {
            db.Set<PortalEntity>().Add(new PortalEntity
            {
                PortalId = PortalId,
                PortalName = "Assignment Test Portal",
                AdministratorId = 1,
                AdministratorRoleId = 900,
                RegisteredRoleId = RoleId
            });
            db.Set<RoleEntity>().Add(new RoleEntity
            {
                RoleId = RoleId,
                PortalId = PortalId,
                RoleName = "Registered Users",
                BillingFrequency = "N",
                TrialFrequency = "N"
            });
            db.Set<UserEntity>().Add(new UserEntity { UserId = UserId, PortalId = PortalId, Username = "member1", IsApproved = true });
            db.SaveChanges();
        });
        await AssignAsync();

        var response = await _client.DeleteAsync($"/api/roles/{RoleId}/users/{UserId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The guard prevented the removal: the assignment is still present.
        var list = await GetUserRolesAsync();
        list.Should().ContainSingle(ur => ur.RoleId == RoleId && ur.UserId == UserId);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------------

    // Seeds a Portal (Administrators/Registered roles deliberately != RoleId so the guard permits removal), the target
    // Role, and the target User into the shared InMemory store at the non-zero PortalId.
    private void SeedPortalRoleUser()
    {
        _factory.ResetAndSeed(db =>
        {
            db.Set<PortalEntity>().Add(new PortalEntity
            {
                PortalId = PortalId,
                PortalName = "Assignment Test Portal",
                AdministratorId = 1,
                AdministratorRoleId = 900,
                RegisteredRoleId = 901
            });
            db.Set<RoleEntity>().Add(new RoleEntity
            {
                RoleId = RoleId,
                PortalId = PortalId,
                RoleName = "Members",
                BillingFrequency = "N",
                TrialFrequency = "N"
            });
            db.Set<UserEntity>().Add(new UserEntity
            {
                UserId = UserId,
                PortalId = PortalId,
                Username = "member1",
                IsApproved = true
            });
            db.SaveChanges();
        });
    }

    private async Task AssignAsync()
    {
        var response = await _client.PostAsync(
            "/api/roles/assignments",
            JsonContent.Create(
                new AssignUserRoleRequest { PortalId = PortalId, UserId = UserId, RoleId = RoleId },
                options: EnvelopeReader.Web));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<List<UserRoleDto>> GetUserRolesAsync()
    {
        var response = await _client.GetAsync($"/api/roles/user/{UserId}?portalId={PortalId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        var list = data.Deserialize<List<UserRoleDto>>(EnvelopeReader.Web);
        return list ?? new List<UserRoleDto>();
    }

    private static async Task<UserRoleDto> ReadUserRoleAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        var dto = data.Deserialize<UserRoleDto>(EnvelopeReader.Web);
        dto.Should().NotBeNull("the success envelope's data object must deserialize to a UserRoleDto");
        return dto!;
    }
}
