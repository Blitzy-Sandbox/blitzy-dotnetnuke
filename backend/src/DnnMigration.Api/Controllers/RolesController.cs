// -----------------------------------------------------------------------------
//  RolesController.cs
//
//  MIGRATION: Thin ASP.NET Core 8 REST controller for security-role management,
//  serving the "/api/roles" resource. It replaces the legacy DotNetNuke 4.x
//  Web Forms role-administration workflows together with the co-mingled
//  business/data-access logic that backed them:
//    - Library/Components/Security/Roles/RoleController.vb
//        GetRoles (L208)        -> GET  /api/roles
//        GetPortalRoles (L146)  -> GET  /api/roles?portalId={pid}
//        GetRole (L163)         -> GET  /api/roles/{id}
//        AddRole (L100)         -> POST /api/roles
//        UpdateRole (L254)      -> PUT  /api/roles/{id}
//        DeleteRole (L125)      -> DELETE /api/roles/{id}
//    - Website/admin/Security/EditRoles.ascx.vb
//        Page_Load (L98)        -> GET (load for edit)
//        cmdUpdate_Click (L208) -> POST when RoleID == -1 (AddRole, L253),
//                                  otherwise PUT (UpdateRole, L260)
//        cmdDelete_Click (L287) -> DELETE
//    - Website/admin/Security/Roles.ascx.vb
//        BindData() grid population via GetPortalRoles -> GET list
//
//  Architectural rules honoured (AAP sec. 0.7.1 - "Minimal Change Clause &
//  Migration Discipline" / "Code organization"):
//    * THIN controller - performs NO business logic and NO data access. It has
//      no EF/DbContext, no repository, and no SQL; every operation is delegated
//      to the injected DnnMigration.Application.Interfaces.IRoleService (the
//      Application/Service layer owns the business rules extracted from
//      RoleController.vb).
//    * DTO in / DTO out - only Application-layer DTOs cross this boundary;
//      EF Core entities are never exposed here.
//    * Async-only - every action is awaited and forwards the request
//      CancellationToken so client aborts and shutdown propagate downstream.
//    * Errors are never hand-formatted in this controller. Not-found paths
//      return NotFound() (a bare 404, never wrapped in the success envelope);
//      unhandled exceptions are converted to RFC 7807 Problem Details by the
//      central exception-handling middleware. Successful responses use the
//      "{ data, meta }" envelope produced by ApiControllerBase.
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Provides the RESTful CRUD surface for security roles under <c>/api/roles</c>
/// (list, read-by-id, create, update, and delete).
/// </summary>
/// <remarks>
/// <para>
/// This controller is a deliberately thin HTTP adapter over
/// <see cref="IRoleService"/>. It maps HTTP verbs and routes onto service calls,
/// projects the results into the standard success envelope, and translates the
/// service's "not found" signals into HTTP 404 responses. It contains no
/// business logic, performs no persistence, and never touches EF Core.
/// </para>
/// <para>
/// MIGRATION: the legacy screens gated role administration behind DotNetNuke's
/// <c>SecurityAccessLevel</c> (Admin/Host) checks. That provider-based
/// authorization is replaced here by the class-level <see cref="AuthorizeAttribute"/>:
/// every action requires an authenticated caller carrying a valid JWT bearer
/// token issued by the API. Finer-grained role/claims policies can be layered on
/// individual actions later without changing this controller's shape.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
// MIGRATION (authorization — vertical gate): the caller must be an Administrator or a host
// (isSuperUser) to reach any role action (AAP §0.6.4). Horizontal (per-portal) scoping is applied
// per action below via the ApiControllerBase guards.
[Authorize(Policy = "PortalAdministrator")]
// MIGRATION QA finding F: declare the response contract for OpenAPI/Swagger. Success bodies use the
// { data, meta } envelope (ApiResponse<T>); every error body is an RFC 7807 Problem Details payload
// produced centrally by the exception-handling middleware. 401 is declared once here because every
// action on this authorized resource returns it when the bearer token is missing or invalid; the
// per-action attributes below add the success shape plus the action-specific 400/403/404 responses.
// MIGRATION QA finding F: intentionally NO [Produces("application/json")] here. That attribute is an
// MVC result filter that would override the Content-Type of the [ApiController]-produced 400
// ValidationProblemDetails from "application/problem+json" to "application/json", breaking RFC 7807.
// The [ProducesResponseType] attributes alone supply the response schemas to Swagger.
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class RolesController : ApiControllerBase
{
    // MIGRATION: the legacy RoleController.vb was instantiated ad-hoc
    // ("Dim objRoles As New RoleController") inside each Web Forms code-behind
    // and reached data through a static RoleProvider.Instance() singleton. In
    // the target architecture that business surface becomes the DI-registered
    // IRoleService, constructor-injected here so the controller carries no
    // provider/singleton statics (AAP sec. 0.6.1 - "Static-to-DI conversion").
    private readonly IRoleService _roleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesController"/> class.
    /// </summary>
    /// <param name="roleService">
    /// The application service that owns all role business logic and data access.
    /// Supplied by the built-in dependency-injection container.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="roleService"/> is <see langword="null"/>.
    /// </exception>
    public RolesController(IRoleService roleService)
    {
        ArgumentNullException.ThrowIfNull(roleService);
        _roleService = roleService;
    }

    /// <summary>
    /// Gets a bounded, server-paginated list of roles, optionally filtered to a single portal.
    /// </summary>
    /// <param name="portalId">
    /// When supplied (<c>GET /api/roles?portalId={pid}</c>), restricts the result
    /// to roles owned by that portal; when omitted (<c>GET /api/roles</c>), every
    /// role is returned.
    /// </param>
    /// <param name="page">
    /// Optional 1-based page number (default 1). Values below 1 are normalized to 1.
    /// </param>
    /// <param name="pageSize">
    /// Optional page size (default 50, hard maximum 200). Values above the maximum are clamped so a
    /// single request can never materialize every role (R6 Issue 1 — unbounded lists).
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 200 with the standard envelope whose <c>data</c> is the current page of roles and whose
    /// <c>meta</c> carries <c>count</c>, <c>page</c>, <c>pageSize</c>, <c>totalCount</c>, and <c>totalPages</c>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<RoleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? portalId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // MIGRATION (R6 Issue 1 — unbounded lists): the previous implementation hydrated the ENTIRE role
        // set into memory (all portals, or every role in a portal) before returning it. The endpoint is
        // now bounded — page and pageSize are normalized (default 1/50, hard cap 200) and the data layer
        // applies Skip/Take + a COUNT so a single request can never stream an unbounded set.
        var paging = PaginationParameters.Normalize(page, pageSize);

        // MIGRATION (authorization — horizontal scoping): a host (super) user may list any portal (or
        // all portals); a non-host caller is confined to the portal named by its own portalId claim.
        // An explicit cross-portal query is rejected with 403; an unfiltered request is narrowed to
        // the caller's own portal (AAP §0.6.4).
        if (!CallerIsSuperUser())
        {
            var callerPortalId = CallerPortalId();
            if (callerPortalId is null) return ForbiddenProblem("The caller has no portal scope.");
            if (portalId.HasValue && portalId.Value != callerPortalId.Value)
                return ForbiddenProblem($"The caller is not authorized to access resources owned by portal {portalId.Value}.");
            portalId = callerPortalId;
        }

        var result = portalId.HasValue
            ? await _roleService.GetByPortalPagedAsync(portalId.Value, paging.Skip, paging.PageSize, cancellationToken)   // MIGRATION: RoleController.GetPortalRoles(PortalId) (L146)
            : await _roleService.GetPagedAsync(paging.Skip, paging.PageSize, cancellationToken);                          // MIGRATION: RoleController.GetRoles() (L208)

        return PagedEnvelope(result, paging);
    }

    /// <summary>
    /// Gets a single role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role (<c>RoleID</c>).</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 200 with the role wrapped in the standard envelope, or HTTP 404 when
    /// no role with the given identifier exists.
    /// </returns>
    // MIGRATION: RoleController.GetRole(RoleID, PortalID) (L163). The legacy
    // signature also required a PortalID; the service resolves a role by its
    // globally unique RoleID, so the portal argument is no longer needed.
    // The action name "GetById" is referenced by Create via nameof(GetById)
    // to build the 201 Location header - do not rename without updating Create.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetByIdAsync(id, cancellationToken);
        if (role is null) return NotFound();

        // MIGRATION (authorization — horizontal scoping): a non-host caller may read a role only when
        // it belongs to the caller's own portal (AAP §0.6.4).
        var denied = RequirePortalAccess(role.PortalID);
        if (denied is not null) return denied;

        return OkEnvelope(role);
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="dto">The role attributes to persist, bound from the request body.</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 201 (Created) with a <c>Location</c> header pointing at
    /// <see cref="GetById"/> and the created role wrapped in the standard envelope.
    /// </returns>
    // MIGRATION: RoleController.AddRole(objRoleInfo) (L100) and the create branch
    // of EditRoles.cmdUpdate_Click, taken when RoleID == -1 (L253). Duplicate-name
    // rejection and any other business validation live in IRoleService, not here.
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto, CancellationToken cancellationToken)
    {
        // MIGRATION (authorization — horizontal scoping): a non-host caller may create a role only
        // within its own portal (the target portal travels in the DTO) (AAP §0.6.4).
        var denied = RequirePortalAccess(dto.PortalID);
        if (denied is not null) return denied;

        var created = await _roleService.CreateAsync(dto, cancellationToken);
        return CreatedEnvelope(nameof(GetById), new { id = created.RoleID }, created);
    }

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update (<c>RoleID</c>).</param>
    /// <param name="dto">The updated role attributes, bound from the request body.</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 200 with the updated role wrapped in the standard envelope, or HTTP 404
    /// when no role with the given identifier exists.
    /// </returns>
    // MIGRATION: RoleController.UpdateRole(objRoleInfo) (L254) and the update
    // branch of EditRoles.cmdUpdate_Click (L260).
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleDto dto, CancellationToken cancellationToken)
    {
        // MIGRATION (authorization — horizontal scoping): confirm the target role belongs to the
        // caller's portal before mutating it. The existing row is fetched first so a cross-portal
        // caller is rejected with 403 (not a silent no-op) and a missing row yields 404 (AAP §0.6.4).
        var existing = await _roleService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        var updated = await _roleService.UpdateAsync(id, dto, cancellationToken);
        return updated is null ? NotFound() : OkEnvelope(updated);
    }

    /// <summary>
    /// Deletes a role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete (<c>RoleID</c>).</param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 204 (No Content) when the role was deleted, or HTTP 404 when no role
    /// with the given identifier exists.
    /// </returns>
    // MIGRATION: RoleController.DeleteRole(RoleId, PortalId) (L125) and
    // EditRoles.cmdDelete_Click (L287). A 204 has no body, so the success
    // envelope is intentionally not used here.
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // MIGRATION (authorization — horizontal scoping): confirm the target role belongs to the
        // caller's portal before deleting it (AAP §0.6.4). A missing row yields 404; a cross-portal
        // delete is rejected with 403.
        var existing = await _roleService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        var deleted = await _roleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
