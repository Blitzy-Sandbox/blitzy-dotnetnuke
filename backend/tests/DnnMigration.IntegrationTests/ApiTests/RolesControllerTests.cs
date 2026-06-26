// MIGRATION: [AAP Gate 5 — API Integration Tests] Roles CRUD integration test. Verifies the Gate-5 status-code
// contract (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204) end-to-end through the real Program.cs pipeline
// against an EF Core InMemory store, PLUS the two RolesController contract specifics and the behavioral-parity
// duplicate-name guard:
//   * Role listing AND the by-id/update/delete actions are PORTAL-SCOPED — portalId is [FromQuery, BindRequired]
//     on GET /{id}, PUT /{id}, DELETE /{id} (and the list), so every such request carries ?portalId=. Omitting it
//     from the list request fails model binding -> 400 (proven by Get_Roles_List_MissingPortalId_Returns400).
//   * RoleService.CreateAsync rejects a duplicate role name (case-insensitive, same portal) with the EXACT legacy
//     DotNetNuke message preserved verbatim from Website/admin/Security/App_LocalResources/EditRoles.ascx.resx
//     ("A role with the same name already exists. The role was not added.") -> 400 (AAP §0.7.1 behavioral parity).
// Source lineage: legacy Library/Components/Security/Roles/RoleController.vb (AddRole / GetRoleByName / DeleteRole /
// GetRole) and Website/admin/Security/Roles.ascx.vb (the role grid). ViewState/postback is discarded; the workflow is
// re-expressed as REST calls validated here.
//
// MIGRATION: No Portal entity is seeded (matching the sibling ModuleCrudTests / UserCrudTests). The seeded Test
// super-user (TestAuthHandler: isSuperUser=true, portalId=0) bypasses ApiControllerBase.EnforceTenant, so portalId=0
// is used throughout; RoleService.CreateAsync needs no Portal row, and the system-role guard (GuardSystemRoleAsync)
// is a no-op when the portal is absent — so the freshly created roles (InMemory keys start at 1) are never mistaken
// for the Administrators/Registered system roles. MIGRATION: [CP4 review — Test Isolation] each test now RESETS the
// shared InMemory store first (CustomWebApplicationFactory.ResetDatabase: EnsureDeleted -> EnsureCreated), so it
// starts from a known-empty database and asserts EXACT state instead of relying on unique role names / shared state.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.IntegrationTests.ApiTests;

/// <summary>
/// Gate-5 CRUD integration coverage for <c>RolesController</c> (<c>/api/roles</c>), including the portal-scoped
/// query-parameter contract and the behavioral-parity duplicate-role-name guard. Runs against the real API host
/// supplied by <see cref="CustomWebApplicationFactory"/> (EF Core InMemory + always-authenticated test super-user).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RolesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    // MIGRATION: the seeded Test principal carries portalId=0 and isSuperUser=true, so EnforceTenant always permits
    // portalId=0 (host SuperUsers administer every portal). All requests use this tenant.
    private const int PortalId = 0;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RolesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_Role_Returns201Created()
    {
        _factory.ResetDatabase();

        // POST -> 201. The minimal valid body satisfies CreateRoleValidator: RoleName NotEmpty/Max50; ServiceFee and
        // TrialFee default 0 (>= 0 OK); BillingPeriod/TrialPeriod default 0 so their GreaterThan(0) rule is skipped.
        var response = await _client.PostAsync(
            "/api/roles",
            JsonContent.Create(
                new CreateRoleRequest { PortalId = PortalId, RoleName = "Test Role Create" },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("HandleCreated emits a CreatedAtAction Location header");

        var role = await ReadRoleAsync(response);
        role.RoleId.Should().BeGreaterThan(0, "the InMemory store assigns the generated key on insert");
        role.RoleName.Should().Be("Test Role Create");
        role.PortalId.Should().Be(PortalId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Role_ById_Returns200()
    {
        _factory.ResetDatabase();
        var created = await CreateRoleAsync(_client, PortalId, "Test Role Get");

        // GET /{id} is portal-scoped: portalId is a BindRequired query parameter (omitting it would be a 400).
        var response = await _client.GetAsync($"/api/roles/{created.RoleId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadRoleAsync(response);
        fetched.RoleId.Should().Be(created.RoleId);
        fetched.RoleName.Should().Be("Test Role Get");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_Role_Returns200()
    {
        _factory.ResetDatabase();
        var created = await CreateRoleAsync(_client, PortalId, "Test Role Update");

        // PUT /{id}?portalId= -> 200. UpdateRoleValidator mirrors create minus PortalId; the route id is authoritative.
        var response = await _client.PutAsync(
            $"/api/roles/{created.RoleId}?portalId={PortalId}",
            JsonContent.Create(
                new UpdateRoleRequest { RoleName = "Updated Role" },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadRoleAsync(response);
        updated.RoleId.Should().Be(created.RoleId);
        updated.RoleName.Should().Be("Updated Role");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_Role_Returns204()
    {
        _factory.ResetDatabase();
        var created = await CreateRoleAsync(_client, PortalId, "Test Role Delete");

        // DELETE /{id}?portalId= -> 204. The created role is not a system role (no portal seeded), so the
        // RoleService system-role guard does not apply.
        var response = await _client.DeleteAsync($"/api/roles/{created.RoleId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_DuplicateRoleName_Returns400()
    {
        _factory.ResetDatabase();

        const string duplicateName = "Duplicate Role Guard";

        // First create succeeds (201) and registers the name within portal 0.
        await CreateRoleAsync(_client, PortalId, duplicateName);

        // Second create with the SAME name (case-insensitive, same portal) is rejected by RoleService.CreateAsync.
        var response = await _client.PostAsync(
            "/api/roles",
            JsonContent.Create(
                new CreateRoleRequest { PortalId = PortalId, RoleName = duplicateName },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // MIGRATION: behavioral parity — the exact legacy DNN message (verified in EditRoles.ascx.resx) is surfaced
        // in the RFC 7807 ProblemDetails "detail". Asserting on the raw body keeps this resilient to envelope shape.
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(
            "A role with the same name already exists. The role was not added.",
            "RoleService preserves the exact legacy duplicate-role-name message");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Roles_List_MissingPortalId_Returns400()
    {
        _factory.ResetDatabase();

        // portalId is [FromQuery, BindRequired]; omitting it fails model binding before the action executes -> 400.
        var response = await _client.GetAsync("/api/roles");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Role_ById_NotFound_Returns404()
    {
        _factory.ResetDatabase();

        // A role id that cannot exist. portalId is required so the request reaches the action (otherwise it is a 400);
        // RoleService.GetByIdAsync returns a failure for the missing role, which HandleGet maps to 404.
        var response = await _client.GetAsync($"/api/roles/999999?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // MIGRATION: [CP4 review — Test Coverage] Assert the EXACT RFC 7807 problem-detail message the migrated
        // RoleService emits. The message is intentionally opaque (it does NOT echo the requested id).
        var problem = await EnvelopeReader.ReadProblemDetailAsync(response);
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("The requested role was not found.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Roles_List_Returns200Paged()
    {
        _factory.ResetDatabase();

        const string roleName = "Test Role List";
        await CreateRoleAsync(_client, PortalId, roleName);

        // MIGRATION: [CP4 review — Envelope Contract + Test Isolation] After reset + exactly one created role, assert
        // BOTH data membership AND every pagination metadata field from the known state: totalCount=1 at pageIndex=0 /
        // pageSize=100 (the NormalizePaging maximum), a single total page, and no previous/next page.
        const int pageSize = 100;
        var response = await _client.GetAsync($"/api/roles?portalId={PortalId}&pageSize={pageSize}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await ReadRolePagedEnvelopeAsync(response);
        envelope.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle().Which.RoleName.Should().Be(roleName);

        envelope.Meta.Should().NotBeNull();
        envelope.Meta!.TotalCount.Should().Be(1);
        envelope.Meta.PageIndex.Should().Be(0);
        envelope.Meta.PageSize.Should().Be(pageSize);
        envelope.Meta.TotalPages.Should().Be(1);
        envelope.Meta.HasPreviousPage.Should().BeFalse();
        envelope.Meta.HasNextPage.Should().BeFalse();
    }

    // -------------------------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates a role through the API (asserting the 201 status) and returns the deserialized <see cref="RoleResponse"/>
    /// read from the success envelope's <c>data</c> object. Used by the read/update/delete tests to obtain a
    /// server-generated role id without coupling them to the POST assertions.
    /// </summary>
    private static async Task<RoleResponse> CreateRoleAsync(HttpClient client, int portalId, string name)
    {
        var response = await client.PostAsync(
            "/api/roles",
            JsonContent.Create(
                new CreateRoleRequest { PortalId = portalId, RoleName = name },
                options: EnvelopeReader.Web));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadRoleAsync(response);
    }

    /// <summary>
    /// Projects the standard success envelope (<c>{ "data": {...}, "meta": {...} }</c>) into the real
    /// <see cref="RoleResponse"/> read model, deserializing the <c>data</c> element with the API's web (camelCase,
    /// case-insensitive) JSON options.
    /// </summary>
    private static async Task<RoleResponse> ReadRoleAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        var role = data.Deserialize<RoleResponse>(EnvelopeReader.Web);
        role.Should().NotBeNull("the success envelope's data object must deserialize to a RoleResponse");
        return role!;
    }

    /// <summary>
    /// MIGRATION: [CP4 review — Envelope Contract] Reads the FULL paged success envelope
    /// <c>{ "data": [...], "meta": {...} }</c> into a typed shape so the list test asserts BOTH data membership and
    /// every pagination metadata field (totalCount, pageIndex, pageSize, totalPages, hasPreviousPage, hasNextPage)
    /// required by the AAP §0.7.5 envelope contract — not just raw body text.
    /// </summary>
    private static async Task<PagedEnvelope<RoleResponse>> ReadRolePagedEnvelopeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PagedEnvelope<RoleResponse>>(json, EnvelopeReader.Web)
               ?? throw new InvalidOperationException("The paged success envelope could not be deserialized.");
    }

    // --- typed paged-envelope shape (CP4 — Envelope Contract) --------------------------------------------------
    // PRIVATE NESTED records so they can never collide with the equivalently-shaped helpers in the sibling
    // *ControllerTests files. Positional parameters bind case-insensitively to the API's camelCase response keys.

    /// <summary>Typed view of the paged-collection success envelope <c>{ data: [...], meta: { ...pagination } }</c>.</summary>
    private sealed record PagedEnvelope<T>(List<T>? Data, PageMeta? Meta);

    /// <summary>The pagination metadata block emitted by <c>ApiControllerBase.HandlePaged</c>.</summary>
    private sealed record PageMeta(
        int TotalCount,
        int PageIndex,
        int PageSize,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);
}
