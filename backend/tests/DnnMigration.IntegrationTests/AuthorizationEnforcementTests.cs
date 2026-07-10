// -----------------------------------------------------------------------------
//  AuthorizationEnforcementTests.cs
//
//  MIGRATION / QA REMEDIATION: Net-new xUnit integration tests that assert the
//  resource-level authorization gate added to close QA Issue #1 (Broken Access
//  Control, OWASP A01:2021). Before the fix, EVERY authenticated principal —
//  regardless of role, portalId, or isSuperUser — passed straight through to the
//  service layer on every resource endpoint (the QA probe matrix observed 500,
//  never 403). These tests reproduce the exact QA reproduction steps and assert
//  the corrected behaviour, running fully in-process against the real Program.cs
//  authorization pipeline (WebApplicationFactory + EF Core InMemory).
//
//  The two enforcement layers exercised here (AAP §0.6.4 — legacy
//  SecurityAccessLevel Anonymous/View/Edit/Admin/Host -> [Authorize]/[AllowAnonymous]
//  role/claims policies) are:
//    * VERTICAL gate  — the "PortalAdministrator" policy on every resource
//                       controller: an authenticated caller lacking BOTH the
//                       Administrators role AND the isSuperUser=true claim is
//                       rejected with 403 (before the controller/service runs).
//    * HORIZONTAL gate — per-action portalId scoping (isSuperUser exempt): a
//                       non-host Administrator acting outside its own portalId
//                       claim is rejected with 403.
//
//  Identities are injected via the test-only X-Test-* headers honoured by
//  CustomWebApplicationFactory.TestAuthHandler (see that file); the claim shape
//  (portalId / isSuperUser / role / name) matches JwtTokenService exactly, so the
//  policy and guards resolve identically to production.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end authorization-enforcement tests for the secured resource endpoints
/// (<c>/api/portals</c>, <c>/api/modules</c>, <c>/api/users</c>, <c>/api/roles</c>, <c>/api/tabs</c>),
/// exercised against the full <c>DnnMigration.Api</c> host booted in-memory by
/// <see cref="CustomWebApplicationFactory"/>.
/// </summary>
/// <remarks>
/// The 403/401 outcomes asserted here are decided by the authentication + authorization pipeline BEFORE
/// (or, for horizontal scoping, at the very start of) the controller action, so they are deterministic and
/// independent of the (empty) InMemory database — reaching the service is exactly what the QA finding
/// proved should NOT happen for an unauthorized caller.
/// </remarks>
public class AuthorizationEnforcementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Initializes the test class with the shared in-memory API host.</summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public AuthorizationEnforcementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // =========================================================================================
    //  VERTICAL GATE — an authenticated caller with NO role and isSuperUser=false must be rejected
    //  with 403 on every resource endpoint (QA repro steps 1-3 + full-controller coverage).
    // =========================================================================================

    [Fact] // QA repro step 3: no-role token -> GET /api/portals -> expected 403 (was 500).
    public async Task NoRole_GetPortals_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Get, "/api/portals"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 2: no-role token -> POST /api/roles -> expected 403 (was 500).
    public async Task NoRole_PostRoles_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Post, "/api/roles", RoleBody()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 1: no-role token -> DELETE /api/users/5 -> expected 403 (was 500).
    public async Task NoRole_DeleteUser_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Delete, "/api/users/5"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Coverage: the vertical gate is uniform across every resource controller (modules).
    public async Task NoRole_PostModules_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Post, "/api/modules", ModuleBody()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Coverage: the vertical gate is uniform across every resource controller (tabs).
    public async Task NoRole_PostTabs_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Post, "/api/tabs", TabBody()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Coverage: the vertical gate is uniform across every resource controller (users, create).
    public async Task NoRole_PostUsers_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Post, "/api/users", UserBody("norole_user")));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Coverage: the vertical gate applies to READS too, not just writes (modules list).
    public async Task NoRole_GetModules_Returns403()
    {
        var response = await _client.SendAsync(NoRole(HttpMethod.Get, "/api/modules"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // =========================================================================================
    //  AUTHENTICATION CONTRAST — no/invalid token must remain 401 (secure-by-default), and the
    //  anonymous /health probe must remain reachable even with the FallbackPolicy in place.
    // =========================================================================================

    [Fact] // QA contrast: no token -> 401 (authentication), NOT 403 (authorization).
    public async Task Anonymous_GetPortals_Returns401()
    {
        var response = await _client.SendAsync(Anonymous(HttpMethod.Get, "/api/portals"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // QA contrast: no token -> 401 on a write endpoint as well.
    public async Task Anonymous_PostRoles_Returns401()
    {
        var response = await _client.SendAsync(Anonymous(HttpMethod.Post, "/api/roles", RoleBody()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Regression: [AllowAnonymous] overrides the FallbackPolicy — /health stays open (Gate 7).
    public async Task Anonymous_Health_Returns200()
    {
        var response = await _client.SendAsync(Anonymous(HttpMethod.Get, "/health"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================================
    //  HORIZONTAL GATE — a non-host Administrator whose portalId=99 must be rejected with 403 when
    //  it targets portal-0 resources (QA repro step 4: cross-portal / cross-tenant escalation).
    // =========================================================================================

    [Fact] // QA repro step 4: portalId=99 admin -> POST /api/roles for portal 0 -> 403.
    public async Task CrossPortal_PostRoles_Portal0_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Post, "/api/roles", RoleBody(portalId: 0)));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> POST /api/modules for portal 0 -> 403.
    public async Task CrossPortal_PostModules_Portal0_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Post, "/api/modules", ModuleBody(portalId: 0)));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> POST /api/tabs for portal 0 -> 403.
    public async Task CrossPortal_PostTabs_Portal0_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Post, "/api/tabs", TabBody(portalId: 0)));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> POST /api/users for portal 0 -> 403.
    public async Task CrossPortal_PostUsers_Portal0_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Post, "/api/users", UserBody("xportal_user", portalId: 0)));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> GET /api/portals/0 -> 403 (portal is its own scope).
    public async Task CrossPortal_GetPortalZero_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Get, "/api/portals/0"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> GET /api/modules?portalId=0 -> 403 (explicit cross-portal query).
    public async Task CrossPortal_GetModulesQueryPortalZero_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Get, "/api/modules?portalId=0"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // QA repro step 4: portalId=99 admin -> GET /api/users?portalId=0 -> 403 (explicit cross-portal query).
    public async Task CrossPortal_GetUsersQueryPortalZero_Returns403()
    {
        var response = await _client.SendAsync(CrossPortal(HttpMethod.Get, "/api/users?portalId=0"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    // QA repro step 4 (existing-resource variant): seed a portal-0 role as host, then a portalId=99
    // non-host Administrator must be denied read AND delete of that existing cross-portal resource, while
    // a same-portal (portalId=0) non-host Administrator is allowed to read it.
    public async Task CrossPortal_ExistingRole_ReadAndDelete_Denied_But_SamePortal_Allowed()
    {
        // Arrange: create a role in portal 0 as the default host (super) identity.
        var createResponse = await _client.SendAsync(Super(HttpMethod.Post, "/api/roles", RoleBody(portalId: 0)));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<RoleRead>>(JsonOptions);
        created.Should().NotBeNull();
        created!.Data.Should().NotBeNull();
        var roleId = created.Data!.RoleID;
        roleId.Should().BeGreaterThan(0);

        // Act + Assert: cross-portal (portalId=99) non-host caller is denied on the EXISTING resource.
        var crossRead = await _client.SendAsync(CrossPortal(HttpMethod.Get, $"/api/roles/{roleId}"));
        crossRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var crossDelete = await _client.SendAsync(CrossPortal(HttpMethod.Delete, $"/api/roles/{roleId}"));
        crossDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Same-portal (portalId=0) non-host Administrator IS allowed to read its own portal's resource.
        var sameRead = await _client.SendAsync(SamePortalAdmin(HttpMethod.Get, $"/api/roles/{roleId}"));
        sameRead.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================================
    //  POSITIVE — a host (super) user and a same-portal Administrator are correctly ALLOWED
    //  (QA contrast: super / admin-same-portal should pass, not be blocked).
    // =========================================================================================

    [Fact] // Host (super) user passes both gates -> 200 on the portal list.
    public async Task Super_GetPortals_Returns200()
    {
        var response = await _client.SendAsync(Super(HttpMethod.Get, "/api/portals"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // Same-portal (portalId=0) non-host Administrator may create within its own portal -> 201.
    public async Task SamePortalAdmin_PostRoles_Portal0_Returns201()
    {
        var response = await _client.SendAsync(SamePortalAdmin(HttpMethod.Post, "/api/roles", RoleBody(portalId: 0)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    // Same-portal (portalId=0) non-host Administrator is NOT blocked by the horizontal gate on GET-by-id:
    // portal 0 is never created (ids are server-assigned from 1), so the access check passes and the
    // request reaches the not-found path (404) — the key assertion is that it is neither 401 nor 403.
    public async Task SamePortalAdmin_GetPortalZero_IsNotForbidden()
    {
        var response = await _client.SendAsync(SamePortalAdmin(HttpMethod.Get, "/api/portals/0"));
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    // Portal creation is a HOST-only operation (CreatePortalDto carries no PortalID). A same-portal
    // non-host Administrator sending a fully valid create body must be rejected with 403 by
    // RequireSuperUser (proving host-level restriction, not a validation 400).
    public async Task SamePortalAdmin_PostPortal_Returns403()
    {
        var body = ValidPortalCreateBody("hostonly_admin", "hostonly.localtest.me", "hostonly@dnnmigration.local");
        var response = await _client.SendAsync(SamePortalAdmin(HttpMethod.Post, "/api/portals", body));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // =========================================================================================
    //  Identity/request builders — inject the synthetic identity via the test-only X-Test-* headers.
    // =========================================================================================

    /// <summary>Authenticated caller with NO role and isSuperUser=false (fails the vertical gate).</summary>
    private static HttpRequestMessage NoRole(HttpMethod method, string url, object? body = null)
        => Build(method, url, roles: "none", portalId: "0", isSuperUser: "false", body: body);

    /// <summary>Administrator whose portalId=99 and isSuperUser=false (fails the horizontal gate for portal 0).</summary>
    private static HttpRequestMessage CrossPortal(HttpMethod method, string url, object? body = null)
        => Build(method, url, roles: "Administrators", portalId: "99", isSuperUser: "false", body: body);

    /// <summary>Administrator whose portalId=0 and isSuperUser=false (passes both gates for portal 0).</summary>
    private static HttpRequestMessage SamePortalAdmin(HttpMethod method, string url, object? body = null)
        => Build(method, url, roles: "Administrators", portalId: "0", isSuperUser: "false", body: body);

    /// <summary>Host (super) user: Administrators role, portalId=0, isSuperUser=true (exempt from portal scoping).</summary>
    private static HttpRequestMessage Super(HttpMethod method, string url, object? body = null)
        => Build(method, url, roles: "Administrators", portalId: "0", isSuperUser: "true", body: body);

    /// <summary>Unauthenticated request (no token) — expected to challenge with 401.</summary>
    private static HttpRequestMessage Anonymous(HttpMethod method, string url, object? body = null)
        => Build(method, url, anonymous: true, body: body);

    private static HttpRequestMessage Build(
        HttpMethod method,
        string url,
        string? roles = null,
        string? portalId = null,
        string? isSuperUser = null,
        bool anonymous = false,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (anonymous)
        {
            request.Headers.Add("X-Test-Anonymous", "true");
        }
        else
        {
            if (roles is not null) request.Headers.Add("X-Test-Roles", roles);
            if (portalId is not null) request.Headers.Add("X-Test-PortalId", portalId);
            if (isSuperUser is not null) request.Headers.Add("X-Test-IsSuperUser", isSuperUser);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    // =========================================================================================
    //  Valid request bodies (minimal but validator-satisfying, so the horizontal-gate 403 is reached
    //  rather than a 400 for the cross-portal POST tests).
    // =========================================================================================

    private static object RoleBody(int portalId = 0) => new
    {
        portalID = portalId,
        roleName = "AuthZ Test Role " + Guid.NewGuid().ToString("N")
    };

    private static object ModuleBody(int portalId = 0) => new
    {
        portalID = portalId,
        tabID = 1,
        moduleDefID = 1,
        moduleTitle = "AuthZ Test Module",
        moduleOrder = 1,
        cacheTime = 0,
        visibility = 0
    };

    private static object TabBody(int portalId = 0) => new
    {
        portalID = portalId,
        tabName = "AuthZ Test Tab",
        parentId = 0,
        tabOrder = 1,
        isVisible = true
    };

    private static object UserBody(string username, int portalId = 0) => new
    {
        username,
        firstName = "AuthZ",
        lastName = "Tester",
        email = username + "@dnnmigration.local",
        password = "P@ssw0rd123",
        confirmPassword = "P@ssw0rd123",
        portalID = portalId,
        authorize = true,
        notify = false,
        randomPassword = false
    };

    private static object ValidPortalCreateBody(string username, string portalAlias, string email) => new
    {
        portalName = "AuthZ Host-Only Portal",
        firstName = "Host",
        lastName = "Admin",
        username,
        password = "P@ssw0rd123",
        email,
        description = "Created by AuthorizationEnforcementTests host-only check",
        keyWords = "authz,hostonly",
        homeDirectory = "Portals/authz-hostonly",
        portalAlias
    };

    // =========================================================================================
    //  Test-only read-models for deserializing the { data, meta } success envelope.
    // =========================================================================================

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    private sealed class RoleRead
    {
        public int RoleID { get; set; }
        public int PortalID { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }
}
