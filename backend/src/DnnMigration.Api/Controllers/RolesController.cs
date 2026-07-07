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
[Authorize]
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
    /// Gets the list of roles, optionally filtered to a single portal.
    /// </summary>
    /// <param name="portalId">
    /// When supplied (<c>GET /api/roles?portalId={pid}</c>), restricts the result
    /// to roles owned by that portal; when omitted (<c>GET /api/roles</c>), every
    /// role is returned.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the request should be cancelled.</param>
    /// <returns>
    /// HTTP 200 with the standard envelope whose <c>data</c> is the role
    /// collection and whose <c>meta</c> carries the item <c>count</c>.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? portalId, CancellationToken cancellationToken)
    {
        var roles = portalId.HasValue
            ? await _roleService.GetByPortalAsync(portalId.Value, cancellationToken)   // MIGRATION: RoleController.GetPortalRoles(PortalId) (L146)
            : await _roleService.GetAllAsync(cancellationToken);                        // MIGRATION: RoleController.GetRoles() (L208)

        var list = roles.ToList();
        return OkEnvelope(list, new { count = list.Count });
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
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : OkEnvelope(role);
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
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleDto dto, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _roleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
