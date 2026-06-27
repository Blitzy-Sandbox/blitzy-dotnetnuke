using DnnMigration.Api.Authorization;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms user administration code-behind
// Website/admin/Users/ManageUsers.ascx.vb + Users.ascx.vb (user list/grid) and User.ascx.vb
// (create/edit user). ViewState/postback is discarded; re-expressed as thin JSON REST endpoints
// delegating to IUserService.
// MIGRATION (CP2 review — API versioning): exposed BOTH at /api/v1/users (AAP §0.1.2/§0.3.4 URL-path
// versioning NFR) AND at /api/users (AAP §0.3.4 resource table + Gate 5 literal paths) via dual [Route]
// attributes (no external API-versioning package is available offline). Recorded in MIGRATION_NOTES.md.
// MIGRATION (CP2 review — authorization + tenant isolation): user administration requires the
// PortalAdministrator policy, and every action enforces that the client-supplied portalId matches the JWT
// "portalId" claim (EnforceTenant) so a portal admin can only manage users in its own portal; host SuperUsers
// bypass the tenant check.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
// MIGRATION (CP-final review - profile workflow parity): the profile workflow (Website/admin/Users/Profile.ascx.vb,
// ProfileDefinitions.ascx.vb) is exposed as GET/PUT /api/users/{id}/profile, delegating to
// IUserService.GetProfileAsync/UpdateProfileAsync. The DNN profile is the EXISTING EAV schema
// ([ProfilePropertyDefinition] + [UserProfile]); the service maps it to/from the flat UserProfileDto.
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AuthorizationPolicies.PortalAdministrator)]
[Produces("application/json")]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    // MIGRATION: User listing is always portal-scoped (multi-tenant isolation preserved from DNN's
    // PortalId discriminator) — portalId is a required query parameter; there is no host-wide user list.
    /// <summary>GET /api/users?portalId=&amp;pageIndex=&amp;pageSize= — paged users for a portal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPortal(
        [FromQuery, BindRequired] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await userService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    // MIGRATION: CP1 review (IUserService #1) — single-user read is portal-scoped; portalId is a required query
    // parameter so the service enforces tenant ownership (a user is only returned within its owning portal, AAP 0.7.1).
    /// <summary>GET /api/users/{id}?portalId= — a single user by id within a portal.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.GetByIdAsync(portalId, id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/users — create a user.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.CreateAsync(request, HttpContext.RequestAborted);
        // MIGRATION: the 201 Location route values must include portalId because GetById is now portal-scoped
        // (CP1 review IUserService #1); portalId is sourced from the create request's PortalId.
        return HandleCreated(result, nameof(GetById), created => new { id = created.UserId, portalId = request.PortalId });
    }

    // MIGRATION: CP1 review (IUserService #1) — update is portal-scoped; portalId is required so the service constrains
    // the update to the owning portal (multi-tenant isolation, AAP 0.7.1).
    /// <summary>PUT /api/users/{id}?portalId= — update a user within a portal.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromQuery, BindRequired] int portalId, int id, [FromBody] UpdateUserRequest request)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.UpdateAsync(portalId, id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    // MIGRATION: CP1 review (IUserService #1) — delete is portal-scoped; portalId is required so the service constrains
    // the delete to the owning portal (multi-tenant isolation, AAP 0.7.1).
    /// <summary>DELETE /api/users/{id}?portalId= — delete a user within a portal.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.DeleteAsync(portalId, id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }

    // MIGRATION (CP-final review - profile workflow parity): replaces the legacy Website/admin/Users/Profile.ascx.vb
    // "view profile" workflow. Portal-scoped (multi-tenant isolation) - portalId is required and tenant-enforced so a
    // portal admin reads only its own users' profiles; a user not in the portal yields 404 (HandleGet).
    /// <summary>GET /api/users/{id}/profile?portalId= - a user's profile within a portal.</summary>
    [HttpGet("{id:int}/profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.GetProfileAsync(portalId, id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    // MIGRATION (CP-final review - profile workflow parity): replaces the legacy Profile.ascx.vb "save profile"
    // postback. Upserts the EXISTING [UserProfile] EAV rows and enforces the data-driven definition validation
    // (Required / Length / ValidationExpression) in the service; a validation failure returns 400 (HandleResult).
    /// <summary>PUT /api/users/{id}/profile?portalId= - update a user's profile within a portal.</summary>
    [HttpPut("{id:int}/profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromQuery, BindRequired] int portalId, int id, [FromBody] UserProfileDto request)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await userService.UpdateProfileAsync(portalId, id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }
}
