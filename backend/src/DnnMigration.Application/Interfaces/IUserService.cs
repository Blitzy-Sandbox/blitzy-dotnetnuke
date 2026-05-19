// -----------------------------------------------------------------------------
// <copyright file="IUserService.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Service interface extracted from UserController.vb business logic.
// Source: Library/Components/Users/UserController.vb
// Original VB.NET shared/static methods converted to async instance methods.
// Methods pattern: synchronous VB.NET → Task-based async C# with CancellationToken
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Defines the contract for user management operations in the application layer.
/// Provides async methods for user CRUD operations, authentication, and password management.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This interface extracts business logic from the legacy VB.NET UserController class
/// (Library/Components/Users/UserController.vb) and converts all synchronous operations to
/// Task-based async methods with CancellationToken support per Section 0.7.2 requirements.
/// </para>
/// <para>
/// Key transformations from original UserController.vb:
/// <list type="bullet">
///   <item><description>Public Shared Function GetUser → GetUserAsync with Task return</description></item>
///   <item><description>Public Shared Function GetUsers → GetUsersAsync with PagedResult return</description></item>
///   <item><description>Public Shared Function CreateUser → CreateUserAsync with UserDto return</description></item>
///   <item><description>Public Shared Sub UpdateUser → UpdateUserAsync with Task and UserDto return</description></item>
///   <item><description>Public Shared Function DeleteUser → DeleteUserAsync with Task return</description></item>
///   <item><description>Public Shared Function ValidateUser → ValidateUserAsync with UserValidationResult return</description></item>
///   <item><description>Public Shared Function ChangePassword → ChangePasswordAsync with Task of bool return</description></item>
///   <item><description>ByRef totalRecords pattern → PagedResult wrapper object</description></item>
/// </list>
/// </para>
/// <para>
/// All methods include CancellationToken parameters to support cooperative cancellation
/// of I/O operations and long-running database queries.
/// </para>
/// </remarks>
/// <example>
/// Service registration in DI container:
/// <code>
/// builder.Services.AddScoped&lt;IUserService, UserService&gt;();
/// </code>
/// </example>
public interface IUserService
{
    /// <summary>
    /// Retrieves a single user by portal and user ID.
    /// </summary>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the <see cref="UserDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUser(portalId, userId, isHydrated) from line 497.
    /// Original signature: Public Shared Function GetUser(ByVal portalId As Integer, 
    ///                     ByVal userId As Integer, ByVal isHydrated As Boolean) As UserInfo
    /// The isHydrated parameter is removed as EF Core handles entity loading automatically.
    /// </remarks>
    Task<UserDto?> GetUserAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of users for a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to retrieve users from.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing a <see cref="PagedResult{T}"/> of <see cref="UserDto"/> objects.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUsers(portalId, pageIndex, pageSize, ByRef totalRecords)
    /// from line 725.
    /// Original signature: Public Shared Function GetUsers(ByVal portalId As Integer, 
    ///                     ByVal pageIndex As Integer, ByVal pageSize As Integer, 
    ///                     ByRef totalRecords As Integer) As ArrayList
    /// The ByRef totalRecords pattern is replaced with PagedResult.TotalCount property.
    /// </remarks>
    Task<PagedResult<UserDto>> GetUsersAsync(
        int portalId, 
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by username within a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="username">The exact username to search for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the <see cref="UserDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUserByName(portalId, username) from line 544.
    /// Original signature: Public Shared Function GetUserByName(ByVal portalId As Integer, 
    ///                     ByVal username As String) As UserInfo
    /// This performs an exact username match, not a partial search.
    /// </remarks>
    Task<UserDto?> GetUserByUsernameAsync(
        int portalId, 
        string username, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for users by email address with pagination support.
    /// </summary>
    /// <param name="portalId">The portal identifier to search within.</param>
    /// <param name="email">The email address filter expression (supports wildcards).</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing a <see cref="PagedResult{T}"/> of <see cref="UserDto"/> objects
    /// matching the email filter.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUsersByEmail(portalId, emailToMatch, pageIndex, 
    ///            pageSize, ByRef totalRecords) from line 769.
    /// Original signature: Public Shared Function GetUsersByEmail(ByVal portalId As Integer, 
    ///                     ByVal emailToMatch As String, ByVal pageIndex As Integer, 
    ///                     ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList
    /// The emailToMatch parameter supports SQL LIKE wildcards (% for any characters).
    /// </remarks>
    Task<PagedResult<UserDto>> GetUsersByEmailAsync(
        int portalId, 
        string email, 
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for users by username with pagination support.
    /// </summary>
    /// <param name="portalId">The portal identifier to search within.</param>
    /// <param name="usernameToMatch">The username filter expression (supports wildcards).</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing a <see cref="PagedResult{T}"/> of <see cref="UserDto"/> objects
    /// matching the username filter.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUsersByUserName(portalId, userNameToMatch, pageIndex,
    ///            pageSize, ByRef totalRecords) from line 816.
    /// Original signature: Public Shared Function GetUsersByUserName(ByVal portalId As Integer, 
    ///                     ByVal userNameToMatch As String, ByVal pageIndex As Integer, 
    ///                     ByVal pageSize As Integer, ByRef totalRecords As Integer) As ArrayList
    /// The userNameToMatch parameter supports SQL LIKE wildcards (% for any characters).
    /// This differs from GetUserByUsernameAsync which performs exact match.
    /// </remarks>
    Task<PagedResult<UserDto>> GetUsersByUsernameAsync(
        int portalId, 
        string usernameToMatch, 
        int pageIndex, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <param name="portalId">The portal identifier the user will belong to.</param>
    /// <param name="request">The user creation request containing all required user data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the created <see cref="UserDto"/> with assigned identifiers.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when user creation fails due to validation errors, duplicate username/email,
    /// or invalid password according to membership provider configuration.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Replaces UserController.CreateUser(ByRef objUser) from line 156.
    /// Original signature: Public Shared Function CreateUser(ByRef objUser As UserInfo) As UserCreateStatus
    /// The ByRef parameter pattern is replaced with a response DTO.
    /// UserCreateStatus return is converted to exceptions for error conditions.
    /// 
    /// The implementation should:
    /// 1. Validate the user data
    /// 2. Generate password if GenerateRandomPassword is true
    /// 3. Assign user to auto-assignment roles
    /// 4. Clear portal cache after creation
    /// 5. Send notification email if Notify is true
    /// </remarks>
    Task<UserDto> CreateUserAsync(int portalId, CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user's information.
    /// </summary>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="userId">The unique identifier of the user to update.</param>
    /// <param name="request">The update request containing fields to modify.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the updated <see cref="UserDto"/>.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no user exists with the specified portal ID and user ID.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Replaces UserController.UpdateUser(portalId, objUser) from line 963.
    /// Original signature: Public Shared Sub UpdateUser(ByVal portalId As Integer, 
    ///                     ByVal objUser As UserInfo)
    /// 
    /// The implementation should:
    /// 1. Load the existing user
    /// 2. Apply partial updates from request (null values indicate no change)
    /// 3. Call membership provider UpdateUser
    /// 4. Clear user cache after update
    /// </remarks>
    Task<UserDto> UpdateUserAsync(
        int portalId, 
        int userId, 
        UpdateUserRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user from the system.
    /// </summary>
    /// <param name="portalId">The portal identifier the user belongs to.</param>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to delete the portal administrator without explicit permission,
    /// or when the user cannot be deleted due to system constraints.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no user exists with the specified portal ID and user ID.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Replaces UserController.DeleteUser(ByRef objUser, notify, deleteAdmin) from line 200.
    /// Original signature: Public Shared Function DeleteUser(ByRef objUser As UserInfo, 
    ///                     ByVal notify As Boolean, ByVal deleteAdmin As Boolean) As Boolean
    /// 
    /// The implementation should:
    /// 1. Check if user is portal administrator (prevent deletion by default)
    /// 2. Delete folder, module, and tab permissions
    /// 3. Delete user via membership provider
    /// 4. Log deletion event
    /// 5. Clear portal and user caches
    /// </remarks>
    Task DeleteUserAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates user credentials against the data store.
    /// </summary>
    /// <param name="portalId">The portal identifier to validate against.</param>
    /// <param name="username">The username to validate.</param>
    /// <param name="password">The password to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing a <see cref="UserValidationResult"/> with the validation outcome
    /// and user information if successful.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.ValidateUser(portalId, Username, Password, 
    ///            VerificationCode, PortalName, IP, ByRef loginStatus) from line 1110.
    /// Original signature: Public Shared Function ValidateUser(ByVal portalId As Integer, 
    ///                     ByVal Username As String, ByVal Password As String, 
    ///                     ByVal VerificationCode As String, ByVal PortalName As String, 
    ///                     ByVal IP As String, ByRef loginStatus As UserLoginStatus) As UserInfo
    /// 
    /// The ByRef loginStatus parameter is encapsulated in UserValidationResult.
    /// Verification code, portal name, and IP parameters are removed as they are available
    /// from HTTP context in the new architecture.
    /// 
    /// The implementation should:
    /// 1. Attempt authentication via membership provider
    /// 2. Log failed attempts to event log
    /// 3. Check for insecure admin/host passwords
    /// 4. Return appropriate status codes
    /// </remarks>
    Task<UserValidationResult> ValidateUserAsync(
        int portalId, 
        string username, 
        string password, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="oldPassword">The user's current password for verification.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing <c>true</c> if the password was changed successfully;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the new password does not meet the configured password policy requirements.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no user exists with the specified user ID.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Replaces UserController.ChangePassword(user, oldPassword, newPassword) from line 103.
    /// Original signature: Public Shared Function ChangePassword(ByVal user As UserInfo, 
    ///                     ByVal oldPassword As String, ByVal newPassword As String) As Boolean
    /// 
    /// The implementation should:
    /// 1. Validate the new password meets membership provider requirements
    /// 2. Verify old password is correct via membership provider
    /// 3. Change password via membership provider
    /// 4. Set UpdatePassword flag to false
    /// 5. Update the user record
    /// </remarks>
    Task<bool> ChangePasswordAsync(
        int userId, 
        string oldPassword, 
        string newPassword, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of users in a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to count users in.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the total number of users in the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces UserController.GetUserCountByPortal(portalId) from line 582.
    /// Original signature: Public Shared Function GetUserCountByPortal(ByVal portalId As Integer) As Integer
    /// 
    /// This provides an efficient count operation without loading all user records,
    /// useful for dashboard statistics and pagination calculations.
    /// </remarks>
    Task<int> GetUserCountAsync(int portalId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a user validation/authentication attempt.
/// Encapsulates both the validation status and the authenticated user information.
/// </summary>
/// <remarks>
/// MIGRATION: This record replaces the ByRef loginStatus pattern used in the legacy
/// ValidateUser method where the status was returned by reference alongside the UserInfo return value.
/// Combining both pieces of information in a single immutable result object provides clearer
/// semantics and is more suitable for async/await patterns.
/// </remarks>
/// <param name="Status">The login status indicating the result of the validation attempt.</param>
/// <param name="User">The authenticated user's information, or null if authentication failed.</param>
public record UserValidationResult(UserLoginStatus Status, UserDto? User)
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    /// <value>
    /// <c>true</c> if the status indicates a successful login (standard user or super user);
    /// otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// Considers both LoginSuccess and LoginSuperUser as successful outcomes.
    /// Warning statuses (InsecureAdminPassword, InsecureHostPassword) are also considered
    /// successful as the user is authenticated, but the caller should handle the warnings.
    /// </remarks>
    public bool IsSuccess => Status is UserLoginStatus.LoginSuccess 
                          or UserLoginStatus.LoginSuperUser
                          or UserLoginStatus.LoginInsecureAdminPassword
                          or UserLoginStatus.LoginInsecureHostPassword;

    /// <summary>
    /// Gets a value indicating whether the user needs to change their password.
    /// </summary>
    /// <value>
    /// <c>true</c> if the login succeeded but the password is insecure (default admin/host password);
    /// otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// MIGRATION: Maps to the LOGIN_INSECUREADMINPASSWORD and LOGIN_INSECUREHOSTPASSWORD
    /// status codes from the original UserController.ValidateUser method (lines 1144-1153).
    /// </remarks>
    public bool RequiresPasswordChange => Status is UserLoginStatus.LoginInsecureAdminPassword 
                                       or UserLoginStatus.LoginInsecureHostPassword;

    /// <summary>
    /// Gets a value indicating whether the user account is locked out.
    /// </summary>
    /// <value>
    /// <c>true</c> if the login failed due to account lockout; otherwise, <c>false</c>.
    /// </value>
    public bool IsLockedOut => Status is UserLoginStatus.LoginUserLockedOut;

    /// <summary>
    /// Gets a value indicating whether the user account is not approved.
    /// </summary>
    /// <value>
    /// <c>true</c> if the login failed due to the account not being approved; otherwise, <c>false</c>.
    /// </value>
    public bool IsNotApproved => Status is UserLoginStatus.LoginUserNotApproved;
}
