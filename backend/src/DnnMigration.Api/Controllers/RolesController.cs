// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Replaces legacy WebForms code-behinds:
//   - Website/admin/Security/Roles.ascx.vb (role listing)
//   - Website/admin/Security/EditRoles.ascx.vb (role create/update)
//   - Website/admin/Security/SecurityRoles.ascx.vb (user-role assignment)
// MIGRATION: Section 0.3.4 - Role API Endpoints
// MIGRATION: Section 0.4.1 - Controllers/RolesController.cs | CREATE
// MIGRATION: Section 0.7.1 - Domain Logic Preservation
// MIGRATION: Section 0.7.2 - C# 12 Coding Standards with async/await patterns
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// ASP.NET Core 8 REST API controller for security role management operations.
/// Provides endpoints for role CRUD operations and user-role assignments.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This controller replaces the following legacy WebForms code-behinds:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>Roles.ascx.vb</term>
/// <description>Role listing with filtering by role group - replaced by <see cref="GetRoles"/></description>
/// </item>
/// <item>
/// <term>EditRoles.ascx.vb</term>
/// <description>Role creation and editing - replaced by <see cref="CreateRole"/> and <see cref="UpdateRole"/></description>
/// </item>
/// <item>
/// <term>SecurityRoles.ascx.vb</term>
/// <description>User-role assignments - replaced by <see cref="AddUserToRole"/> and <see cref="RemoveUserFromRole"/></description>
/// </item>
/// </list>
/// <para>
/// All endpoints require authentication via JWT Bearer token. Administrative operations
/// (create, update, delete, user-role management) require the "Administrators" role.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RolesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesController"/> class.
    /// </summary>
    /// <param name="roleService">The role service for business logic operations.</param>
    /// <param name="logger">The logger for recording controller operations.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="roleService"/> or <paramref name="logger"/> is null.
    /// </exception>
    public RolesController(IRoleService roleService, ILogger<RolesController> logger)
    {
        _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a paginated list of security roles for a specific portal with optional role group filtering.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to retrieve roles for.</param>
    /// <param name="roleGroupId">
    /// Optional role group identifier for filtering. Pass -1 for global roles (no group), 
    /// -2 for all roles, or a specific group ID. If omitted, returns all roles.
    /// </param>
    /// <param name="pageIndex">The zero-based index of the page to retrieve. Default is 0.</param>
    /// <param name="pageSize">The maximum number of roles per page. Default is 10.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A paginated result containing the list of roles matching the filter criteria.
    /// </returns>
    /// <response code="200">Returns the paginated list of roles.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces Roles.ascx.vb BindData method (lines 66-93) which populated grdRoles DataGrid.
    /// </para>
    /// <para>
    /// Legacy filtering logic from Roles.ascx.vb:
    /// </para>
    /// <code>
    /// If RoleGroupId &lt; -1 Then
    ///     arrRoles = objRoles.GetPortalRoles(PortalId)
    /// Else
    ///     arrRoles = objRoles.GetRolesByGroup(PortalId, RoleGroupId)
    /// End If
    /// </code>
    /// <para>
    /// RoleGroupId values:
    /// </para>
    /// <list type="bullet">
    /// <item><term>-2</term><description>All roles (no group filter)</description></item>
    /// <item><term>-1</term><description>Global roles (roles not in any group)</description></item>
    /// <item><term>&gt;= 0</term><description>Roles in the specified group</description></item>
    /// </list>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<RoleDto>>> GetRoles(
        [FromQuery] int portalId,
        [FromQuery] int? roleGroupId = null,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetRoles called for PortalId: {PortalId}, RoleGroupId: {RoleGroupId}, PageIndex: {PageIndex}, PageSize: {PageSize}",
            portalId, roleGroupId, pageIndex, pageSize);

        // MIGRATION: Logic from Roles.ascx.vb BindData (lines 72-76)
        // If RoleGroupId < -1 Then GetPortalRoles (all roles)
        // Else GetRolesByGroup (filter by group, including -1 for global roles)
        PagedResult<RoleDto> result;

        if (!roleGroupId.HasValue || roleGroupId.Value < -1)
        {
            // MIGRATION: RoleGroupId < -1 (value -2) or null means "All Roles"
            // Original: arrRoles = objRoles.GetPortalRoles(PortalId)
            result = await _roleService.GetRolesAsync(portalId, pageIndex, pageSize, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // MIGRATION: RoleGroupId >= -1 means filter by group
            // -1 = Global Roles (no group), >= 0 = specific group
            // Original: arrRoles = objRoles.GetRolesByGroup(PortalId, RoleGroupId)
            var roles = await _roleService.GetRolesByGroupAsync(portalId, roleGroupId.Value, cancellationToken)
                .ConfigureAwait(false);

            var roleList = roles.ToList();
            var totalCount = roleList.Count;
            var pagedItems = roleList
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            result = PagedResult<RoleDto>.Create(pagedItems, pageIndex, pageSize, totalCount);
        }

        _logger.LogInformation(
            "GetRoles returned {Count} roles for PortalId: {PortalId}",
            result.Items.Count, portalId);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single security role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to retrieve.</param>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The role if found; otherwise, a 404 Not Found response.</returns>
    /// <response code="200">Returns the requested role.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Role with the specified ID was not found.</response>
    /// <remarks>
    /// MIGRATION: Replaces EditRoles.ascx.vb Page_Load role retrieval (line 136):
    /// <code>
    /// Dim objRoleInfo As RoleInfo = objUser.GetRole(RoleID, PortalSettings.PortalId)
    /// </code>
    /// </remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetRole(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetRole called for RoleId: {RoleId}, PortalId: {PortalId}",
            id, portalId);

        var role = await _roleService.GetRoleAsync(id, portalId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            _logger.LogWarning(
                "Role not found: RoleId: {RoleId}, PortalId: {PortalId}",
                id, portalId);

            return NotFound(new { Message = $"Role with ID {id} was not found in portal {portalId}." });
        }

        _logger.LogInformation(
            "GetRole returned role '{RoleName}' for RoleId: {RoleId}",
            role.RoleName, id);

        return Ok(role);
    }

    /// <summary>
    /// Creates a new security role with the specified properties.
    /// </summary>
    /// <param name="request">The role creation request containing all role properties.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The newly created role with HTTP 201 Created status.</returns>
    /// <response code="201">Role was created successfully.</response>
    /// <response code="400">The request is invalid (e.g., missing required fields, duplicate role name).</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have Administrator role.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces EditRoles.ascx.vb cmdUpdate_Click handler for new role creation (lines 208-274).
    /// </para>
    /// <para>
    /// Role properties mapped from legacy form controls:
    /// </para>
    /// <list type="bullet">
    /// <item><term>txtRoleName</term><description>Role name (required)</description></item>
    /// <item><term>txtDescription</term><description>Role description</description></item>
    /// <item><term>cboRoleGroups</term><description>Role group selection</description></item>
    /// <item><term>chkIsPublic</term><description>Public visibility flag</description></item>
    /// <item><term>chkAutoAssignment</term><description>Auto-assignment to new users</description></item>
    /// <item><term>txtServiceFee/txtBillingPeriod/cboBillingFrequency</term><description>Billing configuration</description></item>
    /// <item><term>txtTrialFee/txtTrialPeriod/cboTrialFrequency</term><description>Trial configuration</description></item>
    /// <item><term>txtRSVPCode</term><description>RSVP invitation code</description></item>
    /// <item><term>ctlIcon</term><description>Role icon file</description></item>
    /// </list>
    /// <para>
    /// Original duplicate check logic from EditRoles.ascx.vb (lines 252-258):
    /// </para>
    /// <code>
    /// If objRoleController.GetRoleByName(PortalId, objRoleInfo.RoleName) Is Nothing Then
    ///     objRoleController.AddRole(objRoleInfo)
    /// Else
    ///     ' Show duplicate role error
    /// End If
    /// </code>
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoleDto>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreateRole called for RoleName: {RoleName}",
            request.RoleName);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "CreateRole validation failed for RoleName: {RoleName}",
                request.RoleName);

            return BadRequest(ModelState);
        }

        try
        {
            // MIGRATION: Role creation from EditRoles.ascx.vb cmdUpdate_Click (lines 232-264)
            // The service layer handles duplicate role name validation
            var createdRole = await _roleService.CreateRoleAsync(request, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Role created successfully: RoleId: {RoleId}, RoleName: {RoleName}",
                createdRole.RoleId, createdRole.RoleName);

            return CreatedAtAction(
                nameof(GetRole),
                new { id = createdRole.RoleId, portalId = createdRole.PortalId },
                createdRole);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            // MIGRATION: Handle duplicate role name error from EditRoles.ascx.vb (lines 255-257)
            _logger.LogWarning(
                "Duplicate role name detected: {RoleName}",
                request.RoleName);

            return BadRequest(new { Message = $"A role with the name '{request.RoleName}' already exists." });
        }
    }

    /// <summary>
    /// Updates an existing security role with the specified properties.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="request">The role update request containing updated properties.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated role.</returns>
    /// <response code="200">Role was updated successfully.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have Administrator role.</response>
    /// <response code="404">Role with the specified ID was not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces EditRoles.ascx.vb cmdUpdate_Click handler for existing role update (lines 259-261):
    /// </para>
    /// <code>
    /// Else
    ///     objRoleController.UpdateRole(objRoleInfo)
    ///     objEventLog.AddLog(objRoleInfo, PortalSettings, UserId, "", EventLogType.ROLE_UPDATED)
    /// End If
    /// </code>
    /// <para>
    /// Note: The Administrator and Registered User roles cannot have their core properties modified
    /// (preserved from legacy EditRoles.ascx.vb lines 174-182).
    /// </para>
    /// </remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> UpdateRole(
        int id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateRole called for RoleId: {RoleId}",
            id);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "UpdateRole validation failed for RoleId: {RoleId}",
                id);

            return BadRequest(ModelState);
        }

        try
        {
            // MIGRATION: Role update from EditRoles.ascx.vb cmdUpdate_Click (lines 259-261)
            var updatedRole = await _roleService.UpdateRoleAsync(id, request, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Role updated successfully: RoleId: {RoleId}, RoleName: {RoleName}",
                updatedRole.RoleId, updatedRole.RoleName);

            return Ok(updatedRole);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "Role not found for update: RoleId: {RoleId}",
                id);

            return NotFound(new { Message = $"Role with ID {id} was not found." });
        }
    }

    /// <summary>
    /// Deletes a security role from the system.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>HTTP 204 No Content on successful deletion.</returns>
    /// <response code="204">Role was deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have Administrator role.</response>
    /// <response code="404">Role with the specified ID was not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces EditRoles.ascx.vb cmdDelete_Click handler (lines 287-301):
    /// </para>
    /// <code>
    /// Private Sub cmdDelete_Click(ByVal sender As Object, ByVal e As System.EventArgs)
    ///     Dim objUser As New RoleController
    ///     objUser.DeleteRole(RoleID, PortalSettings.PortalId)
    ///     DataCache.RemoveCache("GetRoles")
    ///     Response.Redirect(NavigateURL())
    /// End Sub
    /// </code>
    /// <para>
    /// Note: Administrator and Registered User roles cannot be deleted (preserved from legacy logic at lines 174-176).
    /// </para>
    /// </remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRole(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteRole called for RoleId: {RoleId}, PortalId: {PortalId}",
            id, portalId);

        try
        {
            // MIGRATION: Role deletion from EditRoles.ascx.vb cmdDelete_Click (lines 287-301)
            await _roleService.DeleteRoleAsync(id, portalId, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Role deleted successfully: RoleId: {RoleId}, PortalId: {PortalId}",
                id, portalId);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "Role not found for deletion: RoleId: {RoleId}, PortalId: {PortalId}",
                id, portalId);

            return NotFound(new { Message = $"Role with ID {id} was not found in portal {portalId}." });
        }
        catch (InvalidOperationException ex)
        {
            // Handle case where role cannot be deleted (e.g., system roles)
            _logger.LogWarning(
                "Role deletion not allowed: RoleId: {RoleId}, Reason: {Reason}",
                id, ex.Message);

            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Adds a user to a security role with optional effective and expiry dates.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="userId">The unique identifier of the user to add.</param>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="assignment">Optional assignment details including effective and expiry dates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>HTTP 204 No Content on successful assignment.</returns>
    /// <response code="204">User was added to the role successfully.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have Administrator role.</response>
    /// <response code="404">Role or user was not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces SecurityRoles.ascx.vb cmdAdd_Click handler (lines 518-551):
    /// </para>
    /// <code>
    /// Private Sub cmdAdd_Click(ByVal sender As System.Object, ByVal e As System.EventArgs)
    ///     If Page.IsValid Then
    ///         If (Not Role Is Nothing) AndAlso (Not User Is Nothing) Then
    ///             Dim datEffectiveDate As Date = If(txtEffectiveDate.Text &lt;&gt; "", Date.Parse(txtEffectiveDate.Text), Null.NullDate)
    ///             Dim datExpiryDate As Date = If(txtExpiryDate.Text &lt;&gt; "", Date.Parse(txtExpiryDate.Text), Null.NullDate)
    ///             RoleController.AddUserRole(User, Role, PortalSettings, datEffectiveDate, datExpiryDate, UserId, chkNotify.Checked)
    ///         End If
    ///     End If
    /// End Sub
    /// </code>
    /// <para>
    /// The effective and expiry dates control when the user's role membership is active.
    /// If both dates are null, the membership is immediately effective with no expiration.
    /// </para>
    /// </remarks>
    [HttpPost("{roleId:int}/users/{userId:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUserToRole(
        int roleId,
        int userId,
        [FromQuery] int portalId,
        [FromBody] UserRoleAssignment? assignment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AddUserToRole called: UserId: {UserId}, RoleId: {RoleId}, PortalId: {PortalId}",
            userId, roleId, portalId);

        try
        {
            // MIGRATION: User-role assignment from SecurityRoles.ascx.vb cmdAdd_Click (lines 518-551)
            // Extract effective and expiry dates from assignment request
            var effectiveDate = assignment?.EffectiveDate;
            var expiryDate = assignment?.ExpiryDate;

            await _roleService.AddUserToRoleAsync(
                portalId,
                userId,
                roleId,
                effectiveDate,
                expiryDate,
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "User added to role successfully: UserId: {UserId}, RoleId: {RoleId}",
                userId, roleId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                "Entity not found for user-role assignment: {Message}",
                ex.Message);

            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a user from a security role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="userId">The unique identifier of the user to remove.</param>
    /// <param name="portalId">The identifier of the portal context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>HTTP 204 No Content on successful removal.</returns>
    /// <response code="204">User was removed from the role successfully.</response>
    /// <response code="400">The removal is not allowed (e.g., cannot remove admin from admin role).</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have Administrator role.</response>
    /// <response code="404">Role or user was not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces SecurityRoles.ascx.vb grdUserRoles_Delete handler (lines 565-589):
    /// </para>
    /// <code>
    /// Public Sub grdUserRoles_Delete(ByVal sender As Object, ByVal e As DataGridCommandEventArgs)
    ///     If RoleId &lt;&gt; Null.NullInteger Then
    ///         If Not RoleController.DeleteUserRole(UserId, Role, PortalSettings, chkNotify.Checked) Then
    ///             strMessage = Localization.GetString("RoleRemoveError", Me.LocalResourceFile)
    ///         End If
    ///     End If
    /// End Sub
    /// </code>
    /// <para>
    /// The operation validates whether the user can be removed from the role. For example,
    /// the portal administrator cannot be removed from the Administrators role (preserved from
    /// RoleController.CanRemoveUserFromRole legacy logic).
    /// </para>
    /// </remarks>
    [HttpDelete("{roleId:int}/users/{userId:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserFromRole(
        int roleId,
        int userId,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RemoveUserFromRole called: UserId: {UserId}, RoleId: {RoleId}, PortalId: {PortalId}",
            userId, roleId, portalId);

        try
        {
            // MIGRATION: User-role removal from SecurityRoles.ascx.vb grdUserRoles_Delete (lines 565-589)
            var result = await _roleService.RemoveUserFromRoleAsync(
                portalId,
                userId,
                roleId,
                cancellationToken)
                .ConfigureAwait(false);

            if (!result)
            {
                // MIGRATION: Preserve CanRemoveUserFromRole validation from legacy RoleController
                // Returns false when removal is not allowed (e.g., admin from admin role)
                _logger.LogWarning(
                    "User removal from role not allowed: UserId: {UserId}, RoleId: {RoleId}",
                    userId, roleId);

                return BadRequest(new { Message = "Cannot remove this user from the specified role." });
            }

            _logger.LogInformation(
                "User removed from role successfully: UserId: {UserId}, RoleId: {RoleId}",
                userId, roleId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                "Entity not found for user-role removal: {Message}",
                ex.Message);

            return NotFound(new { Message = ex.Message });
        }
    }
}

/// <summary>
/// Request body for user-role assignment operations.
/// Contains optional effective and expiry dates for role membership.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Derived from SecurityRoles.ascx.vb date handling (lines 273-301):
/// </para>
/// <code>
/// Private Sub GetDates(ByVal UserId As Integer, ByVal RoleId As Integer)
///     If Null.IsNull(objUserRole.EffectiveDate) = False Then
///         strEffectiveDate = objUserRole.EffectiveDate.ToShortDateString
///     End If
///     If Null.IsNull(objUserRole.ExpiryDate) = False Then
///         strExpiryDate = objUserRole.ExpiryDate.ToShortDateString
///     End If
/// End Sub
/// </code>
/// <para>
/// This DTO captures the date inputs from txtEffectiveDate and txtExpiryDate controls in the legacy form.
/// </para>
/// </remarks>
public record UserRoleAssignment
{
    /// <summary>
    /// Gets the optional effective date when the role membership becomes active.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtEffectiveDate control in SecurityRoles.ascx.vb (lines 529-533).
    /// A null value indicates immediate effectiveness.
    /// </remarks>
    public DateTime? EffectiveDate { get; init; }

    /// <summary>
    /// Gets the optional expiry date when the role membership expires.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtExpiryDate control in SecurityRoles.ascx.vb (lines 534-539).
    /// A null value indicates no expiration (perpetual membership).
    /// </remarks>
    public DateTime? ExpiryDate { get; init; }
}
