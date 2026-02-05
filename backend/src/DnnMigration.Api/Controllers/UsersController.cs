// -----------------------------------------------------------------------------
// <copyright file="UsersController.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Converted from VB.NET WebForms code-behind files to ASP.NET Core 8 REST API controller.
// Source files:
//   - Website/admin/Users/ManageUsers.ascx.vb (user management page flow)
//   - Website/admin/Users/User.ascx.vb (user create/update form handlers)
//   - Website/admin/Users/Users.ascx.vb (user listing with filtering)
//   - Website/admin/Users/Membership.ascx.vb (membership status management)
// Original namespace: DotNetNuke.Modules.Admin.Users
// Original classes: ManageUsers, User (UserUserControlBase), UserAccounts, Membership
// Pattern: WebForms postback events → REST API endpoints with JWT authentication
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// ASP.NET Core 8 REST API controller for user administration.
/// Provides endpoints for user CRUD operations with role-based authorization.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This controller replaces the legacy VB.NET WebForms user management pages:
/// <list type="bullet">
///   <item><description>ManageUsers.ascx.vb - User management workflow with tabbed panels</description></item>
///   <item><description>User.ascx.vb - User creation (cmdRegister_Click) and update (cmdUpdate_Click) handlers</description></item>
///   <item><description>Users.ascx.vb - User listing with BindData filtering (Username, Email, ProfileProperty)</description></item>
///   <item><description>Membership.ascx.vb - Account status operations (authorize, unlock, password reset)</description></item>
/// </list>
/// </para>
/// <para>
/// Authorization requirements:
/// <list type="bullet">
///   <item><description>All endpoints require authentication via [Authorize] attribute</description></item>
///   <item><description>Create, Update, Delete operations require Administrators role</description></item>
///   <item><description>Read operations (GetUsers, GetUser) available to any authenticated user</description></item>
/// </list>
/// </para>
/// <para>
/// Endpoint mapping from legacy operations:
/// <list type="table">
///   <listheader>
///     <term>Legacy Operation</term>
///     <description>New Endpoint</description>
///   </listheader>
///   <item>
///     <term>Users.ascx.vb BindData()</term>
///     <description>GET /api/users?portalId=&amp;pageIndex=&amp;pageSize=&amp;usernameFilter=&amp;emailFilter=</description>
///   </item>
///   <item>
///     <term>ManageUsers.ascx.vb BindData()</term>
///     <description>GET /api/users/{id}?portalId=</description>
///   </item>
///   <item>
///     <term>User.ascx.vb CreateUser()/cmdRegister_Click</term>
///     <description>POST /api/users</description>
///   </item>
///   <item>
///     <term>User.ascx.vb cmdUpdate_Click</term>
///     <description>PUT /api/users/{id}</description>
///   </item>
///   <item>
///     <term>User.ascx.vb cmdDelete_Click</term>
///     <description>DELETE /api/users/{id}?portalId=</description>
///   </item>
/// </list>
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userService">The user service for business logic operations.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <remarks>
    /// MIGRATION: Constructor injection replaces legacy UserModuleBase dependencies
    /// which accessed services via static methods (e.g., UserController.GetUser).
    /// </remarks>
    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a paginated list of users with optional filtering.
    /// </summary>
    /// <param name="portalId">The portal identifier to filter users by.</param>
    /// <param name="pageIndex">The zero-based page index (default: 0).</param>
    /// <param name="pageSize">The number of items per page (default: 10).</param>
    /// <param name="usernameFilter">Optional username filter (supports wildcards via service layer).</param>
    /// <param name="emailFilter">Optional email filter (supports wildcards via service layer).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A paginated list of users matching the specified criteria.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces Users.ascx.vb BindData method (lines 231-291) which supported multiple filter modes:
    /// <list type="bullet">
    ///   <item><description>"All" - GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords)</description></item>
    ///   <item><description>"Username" - GetUsersByUserName(portalId, usernameToMatch + "%", pageIndex, pageSize, ByRef totalRecords)</description></item>
    ///   <item><description>"Email" - GetUsersByEmail(portalId, emailToMatch + "%", pageIndex, pageSize, ByRef totalRecords)</description></item>
    ///   <item><description>"ProfileProperty" - GetUsersByProfileProperty(portalId, propertyName, propertyValue + "%", pageIndex, pageSize, ByRef totalRecords)</description></item>
    ///   <item><description>"Unauthorized" - GetUnAuthorizedUsers(portalId, includeDeleted)</description></item>
    ///   <item><description>"OnLine" - GetOnlineUsers(portalId)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The legacy first-letter-search pattern (clicking A-Z links) is replaced by prefix matching
    /// in the usernameFilter/emailFilter parameters with wildcard support.
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the paginated list of users.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? usernameFilter = null,
        [FromQuery] string? emailFilter = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetUsers called for PortalId={PortalId}, PageIndex={PageIndex}, PageSize={PageSize}, " +
            "UsernameFilter={UsernameFilter}, EmailFilter={EmailFilter}",
            portalId, pageIndex, pageSize, usernameFilter ?? "(none)", emailFilter ?? "(none)");

        // Validate pagination parameters
        if (pageIndex < 0)
        {
            pageIndex = 0;
        }

        if (pageSize <= 0)
        {
            pageSize = 10;
        }

        // Apply maximum page size to prevent excessive data retrieval
        // MIGRATION: Original system used Records_PerPage portal setting, typically 10-25
        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
        {
            pageSize = maxPageSize;
        }

        PagedResult<UserDto> result;

        // MIGRATION: Filter logic from Users.ascx.vb BindData method (lines 248-291)
        // Select Case SearchField handling converted to conditional filter application
        if (!string.IsNullOrWhiteSpace(usernameFilter))
        {
            // MIGRATION: Case "Username" from BindData - uses GetUsersByUserName with wildcard
            // Original: Users = UserController.GetUsersByUserName(UsersPortalId, False, SearchText + "%", CurrentPage - 1, PageSize, TotalRecords)
            _logger.LogInformation(
                "Filtering users by username pattern: {UsernameFilter}",
                usernameFilter);
            
            result = await _userService.GetUsersByUsernameAsync(
                portalId,
                usernameFilter,
                pageIndex,
                pageSize,
                cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(emailFilter))
        {
            // MIGRATION: Case "Email" from BindData - uses GetUsersByEmail with wildcard
            // Original: Users = UserController.GetUsersByEmail(UsersPortalId, False, SearchText + "%", CurrentPage - 1, PageSize, TotalRecords)
            _logger.LogInformation(
                "Filtering users by email pattern: {EmailFilter}",
                emailFilter);
            
            result = await _userService.GetUsersByEmailAsync(
                portalId,
                emailFilter,
                pageIndex,
                pageSize,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // MIGRATION: Case "All" from BindData - returns all users paginated
            // Original: Users = UserController.GetUsers(UsersPortalId, False, CurrentPage - 1, PageSize, TotalRecords)
            _logger.LogInformation("Retrieving all users (no filter applied)");
            
            result = await _userService.GetUsersAsync(
                portalId,
                pageIndex,
                pageSize,
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "GetUsers returning {ItemCount} users (TotalCount={TotalCount}, TotalPages={TotalPages})",
            result.Items.Count, result.TotalCount, result.TotalPages);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique user identifier.</param>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The user details if found.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces ManageUsers.ascx.vb BindData method user retrieval logic (lines 202-276).
    /// Original pattern:
    /// <code>
    /// ' Check if User is a member of the Current Portal
    /// If User.PortalID &lt;&gt; Null.NullInteger And User.PortalID &lt;&gt; PortalId Then
    ///     AddModuleMessage("InvalidUser", ModuleMessageType.YellowWarning, True)
    ///     DisableForm()
    ///     Exit Sub
    /// End If
    /// </code>
    /// </para>
    /// <para>
    /// The portal validation is handled by the service layer to ensure users can only
    /// access users within their authorized portal context.
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the user details.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If the user is not found or doesn't belong to the specified portal.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetUser called for UserId={UserId}, PortalId={PortalId}",
            id, portalId);

        var user = await _userService.GetUserAsync(
            portalId, 
            id, 
            cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            _logger.LogWarning(
                "User not found: UserId={UserId}, PortalId={PortalId}",
                id, portalId);
            return NotFound(new { message = $"User with ID {id} not found in portal {portalId}." });
        }

        _logger.LogInformation(
            "GetUser returning user: Username={Username}, Email={Email}",
            user.Username, user.Email);

        return Ok(user);
    }

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <param name="request">The user creation request containing all required user data.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The created user details with HTTP 201 Created status.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces User.ascx.vb CreateUser method (lines 209-238) and cmdRegister_Click handler
    /// (ManageUsers.ascx.vb lines 601-607). The original workflow included:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Captcha validation (UseCaptcha property check) - now handled by separate anti-forgery mechanisms
    ///   </description></item>
    ///   <item><description>
    ///     Display name formatting based on Security_DisplayNameFormat portal setting via UpdateDisplayName()
    ///   </description></item>
    ///   <item><description>
    ///     Password validation and confirmation checking (txtPassword.Text != txtConfirm.Text)
    ///   </description></item>
    ///   <item><description>
    ///     Random password generation when chkRandom.Checked = True
    ///   </description></item>
    ///   <item><description>
    ///     Security question/answer validation when MembershipProviderConfig.RequiresQuestionAndAnswer = True
    ///   </description></item>
    ///   <item><description>
    ///     User approval based on registration type (Public/Private/Verified) or admin authorization
    ///   </description></item>
    ///   <item><description>
    ///     Email notification when chkNotify.Checked = True
    ///   </description></item>
    ///   <item><description>
    ///     Auto-assignment to registered users role
    ///   </description></item>
    /// </list>
    /// <para>
    /// Original VB.NET pattern:
    /// <code>
    /// If IsRegister Then
    ///     'Set the Approved status based on the Portal Settings
    ///     If PortalSettings.UserRegistration = PortalRegistrationType.PublicRegistration Then
    ///         User.Membership.Approved = True
    ///     Else
    ///         User.Membership.Approved = False
    ///     End If
    /// Else
    ///     'Set the Approved status from the value in the Authorized checkbox
    ///     User.Membership.Approved = chkAuthorize.Checked
    /// End If
    /// Dim createStatus As UserCreateStatus = UserController.CreateUser(User)
    /// </code>
    /// </para>
    /// </remarks>
    /// <response code="201">Returns the created user with location header.</response>
    /// <response code="400">If the request data is invalid or validation fails.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have Administrators role.</response>
    /// <response code="409">If a user with the same username or email already exists.</response>
    [HttpPost]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreateUser called for Username={Username}, Email={Email}",
            request.Username, request.Email);

        // MIGRATION: Model validation replaces legacy ctlUser.IsValid check
        // and the Validate() method validation in User.ascx.vb
        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "CreateUser validation failed for Username={Username}: {Errors}",
                request.Username,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
            return BadRequest(ModelState);
        }

        // MIGRATION: Password confirmation validation from User.ascx.vb Validate() method (lines 151-154)
        // Original: If txtPassword.Text <> txtConfirm.Text Then createStatus = UserCreateStatus.PasswordMismatch
        if (!request.GenerateRandomPassword && 
            !string.IsNullOrEmpty(request.PasswordConfirm) && 
            request.Password != request.PasswordConfirm)
        {
            _logger.LogWarning(
                "CreateUser password mismatch for Username={Username}",
                request.Username);
            ModelState.AddModelError("PasswordConfirm", "Password and confirmation do not match.");
            return BadRequest(ModelState);
        }

        try
        {
            var createdUser = await _userService.CreateUserAsync(
                request, 
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "User created successfully: UserId={UserId}, Username={Username}",
                createdUser.UserId, createdUser.Username);

            // Return 201 Created with location header pointing to the new resource
            return CreatedAtAction(
                nameof(GetUser),
                new { id = createdUser.UserId, portalId = createdUser.PortalId },
                createdUser);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                                                    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // MIGRATION: UserCreateStatus.DuplicateUserName or DuplicateEmail from UserController.CreateUser
            _logger.LogWarning(
                ex,
                "CreateUser conflict: Username={Username}, Email={Email}",
                request.Username, request.Email);
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // MIGRATION: Various UserCreateStatus error conditions from UserController.CreateUser
            // InvalidPassword, InvalidQuestion, InvalidAnswer, PasswordMismatch, etc.
            _logger.LogWarning(
                ex,
                "CreateUser validation error: Username={Username}",
                request.Username);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing user's information.
    /// </summary>
    /// <param name="id">The unique user identifier to update.</param>
    /// <param name="request">The update request containing fields to modify.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The updated user details.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces User.ascx.vb cmdUpdate_Click handler (lines 361-385) and related
    /// membership operations from Membership.ascx.vb:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Basic info update: UserController.UpdateUser(UserPortalID, User) from cmdUpdate_Click
    ///   </description></item>
    ///   <item><description>
    ///     Display name formatting: UpdateDisplayName() method (lines 117-123)
    ///   </description></item>
    ///   <item><description>
    ///     Authorization: cmdAuthorize_Click sets User.Membership.Approved = True
    ///   </description></item>
    ///   <item><description>
    ///     Unauthorization: cmdUnAuthorize_Click sets User.Membership.Approved = False
    ///   </description></item>
    ///   <item><description>
    ///     Unlock: cmdUnLock_Click calls UserController.UnLockUser(User)
    ///   </description></item>
    ///   <item><description>
    ///     Force password update: sets User.Membership.UpdatePassword = True
    ///   </description></item>
    /// </list>
    /// <para>
    /// Original VB.NET cmdUpdate_Click pattern:
    /// <code>
    /// If UserEditor.IsValid AndAlso UserEditor.IsDirty AndAlso (Not User Is Nothing) Then
    ///     If User.UserID = PortalSettings.AdministratorId Then
    ///         'Clear the Portal Cache
    ///         DotNetNuke.Common.Utilities.DataCache.ClearPortalCache(UserPortalID, False)
    ///     End If
    ///     Try
    ///         'Update DisplayName to conform to Format
    ///         UpdateDisplayName()
    ///         UserController.UpdateUser(UserPortalID, User)
    ///         OnUserUpdated(EventArgs.Empty)
    ///         OnUserUpdateCompleted(EventArgs.Empty)
    ///     Catch ex As Exception
    ///         Dim args As New UserUpdateErrorArgs(User.UserID, User.Username, "EmailError")
    ///         OnUserUpdateError(args)
    ///     End Try
    /// End If
    /// </code>
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the updated user details.</response>
    /// <response code="400">If the request data is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have Administrators role.</response>
    /// <response code="404">If the user is not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "UpdateUser called for UserId={UserId}",
            id);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "UpdateUser validation failed for UserId={UserId}: {Errors}",
                id,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
            return BadRequest(ModelState);
        }

        try
        {
            // MIGRATION: Extract portalId from route/query or user context
            // In the original system, this came from PortalSettings.PortalId
            // For the API, we rely on the service layer to validate portal access
            // based on the authenticated user's context or use a default value
            // The portalId is typically available in the user's claims or from the request
            
            // Get portalId from query string if provided, otherwise service handles it
            var portalId = 0;
            if (Request.Query.TryGetValue("portalId", out var portalIdValue) && 
                int.TryParse(portalIdValue, out var parsedPortalId))
            {
                portalId = parsedPortalId;
            }

            var updatedUser = await _userService.UpdateUserAsync(
                portalId, 
                id, 
                request, 
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "User updated successfully: UserId={UserId}, Username={Username}",
                updatedUser.UserId, updatedUser.Username);

            return Ok(updatedUser);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "UpdateUser user not found: UserId={UserId}",
                id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }
        catch (InvalidOperationException ex)
        {
            // MIGRATION: Handles UserUpdateErrorArgs scenarios from User.ascx.vb
            _logger.LogWarning(
                ex,
                "UpdateUser failed for UserId={UserId}",
                id);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a user from the system.
    /// </summary>
    /// <param name="id">The unique user identifier to delete.</param>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces User.ascx.vb cmdDelete_Click handler (lines 342-351).
    /// Original VB.NET pattern:
    /// <code>
    /// Private Sub cmdDelete_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles cmdDelete.Click
    ///     Dim name As String = User.Username
    ///     Dim id As Integer = UserId
    ///     If UserController.DeleteUser(User, True, False) Then
    ///         OnUserDeleted(New UserDeletedEventArgs(id, name))
    ///     Else
    ///         OnUserDeleteError(New UserUpdateErrorArgs(id, name, "UserDeleteError"))
    ///     End If
    /// End Sub
    /// </code>
    /// </para>
    /// <para>
    /// Deletion constraints from ManageUsers.ascx.vb (lines 264-268):
    /// <list type="bullet">
    ///   <item><description>
    ///     Cannot delete the portal administrator: User.UserID = PortalSettings.AdministratorId check
    ///   </description></item>
    ///   <item><description>
    ///     Regular users cannot delete super users: User.IsSuperUser And Not Me.UserInfo.IsSuperUser
    ///   </description></item>
    ///   <item><description>
    ///     Users cannot delete themselves if they are super users: IsUser And User.IsSuperUser
    ///   </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The service layer handles deletion of associated data including:
    /// folder permissions, module permissions, tab permissions, and cache clearing.
    /// </para>
    /// </remarks>
    /// <response code="204">User deleted successfully.</response>
    /// <response code="400">If deletion is not allowed (e.g., portal administrator).</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have Administrators role.</response>
    /// <response code="404">If the user is not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DeleteUser called for UserId={UserId}, PortalId={PortalId}",
            id, portalId);

        try
        {
            await _userService.DeleteUserAsync(
                portalId, 
                id, 
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "User deleted successfully: UserId={UserId}, PortalId={PortalId}",
                id, portalId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "DeleteUser user not found: UserId={UserId}, PortalId={PortalId}",
                id, portalId);
            return NotFound(new { message = $"User with ID {id} not found in portal {portalId}." });
        }
        catch (InvalidOperationException ex)
        {
            // MIGRATION: Handles cases where deletion is blocked:
            // - Deleting portal administrator without deleteAdmin flag
            // - Other system constraints from UserController.DeleteUser
            _logger.LogWarning(
                ex,
                "DeleteUser not allowed for UserId={UserId}: {Message}",
                id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
