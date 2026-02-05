// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Users.UserController → C# 12 IUserRepository interface
// Source: Library/Components/Users/UserController.vb
// Changes:
// - Extracted interface from VB.NET UserController class
// - GetUser → GetByIdAsync with nullable return type
// - GetUsers → GetAllAsync with IEnumerable return
// - GetUserByName → GetByUsernameAsync for exact username lookup
// - GetUsersByEmail → GetByEmailAsync for email-based lookup
// - GetUsers(portalId) → GetByPortalIdAsync for portal-scoped queries
// - GetUsers(portalId, pageIndex, pageSize, totalRecords) → GetPagedAsync for pagination
// - GetUserCountByPortal → GetUserCountAsync
// - GetUnAuthorizedUsers → GetUnauthorizedUsersAsync
// - CreateUser → AddAsync returning the created entity
// - UpdateUser → UpdateAsync
// - DeleteUser → DeleteAsync
// - Added GetSuperUsersAsync for host-level admin queries
// - Added ExistsAsync for username existence checks
// - All methods use async/await with Task return types
// - All methods include CancellationToken with default value
// - Uses file-scoped namespace per C# 12 standards
// - Authentication methods excluded (handled by application/identity services per BFF pattern)
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for User entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts data access operations for the User entity,
/// enabling the Infrastructure layer to implement data access while maintaining
/// domain layer independence and separating concerns from authentication logic.
/// </para>
/// <para>
/// MIGRATION: Extracted from legacy VB.NET UserController.vb CRUD operations:
/// <list type="bullet">
///   <item><description>GetUser → <see cref="GetByIdAsync"/></description></item>
///   <item><description>GetUsers → <see cref="GetAllAsync"/></description></item>
///   <item><description>GetUsers(portalId) → <see cref="GetByPortalIdAsync"/></description></item>
///   <item><description>GetUserByName → <see cref="GetByUsernameAsync"/></description></item>
///   <item><description>GetUsersByEmail → <see cref="GetByEmailAsync"/></description></item>
///   <item><description>GetUsers(portalId, pageIndex, pageSize, totalRecords) → <see cref="GetPagedAsync"/></description></item>
///   <item><description>GetUserCountByPortal → <see cref="GetUserCountAsync"/></description></item>
///   <item><description>GetUnAuthorizedUsers → <see cref="GetUnauthorizedUsersAsync"/></description></item>
///   <item><description>CreateUser → <see cref="AddAsync"/></description></item>
///   <item><description>UpdateUser → <see cref="UpdateAsync"/></description></item>
///   <item><description>DeleteUser → <see cref="DeleteAsync"/></description></item>
/// </list>
/// </para>
/// <para>
/// Note: Authentication methods (ChangePassword, ResetPassword, ValidateUser, UserLogin, etc.)
/// are excluded from this repository interface and handled by application/identity services
/// per the BFF (Backend-for-Frontend) pattern.
/// </para>
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their unique identifier within a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal context.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUser(ByVal portalId As Integer, ByVal userId As Integer, ByVal isHydrated As Boolean).
    /// The original method had an isHydrated parameter for progressive hydration;
    /// EF Core lazy loading handles hydration automatically.
    /// </remarks>
    /// <example>
    /// <code>
    /// var user = await userRepository.GetByIdAsync(portalId: 1, userId: 100);
    /// if (user is not null)
    /// {
    ///     Console.WriteLine($"User: {user.Username}, Display Name: {user.DisplayName}");
    /// }
    /// </code>
    /// </example>
    Task<User?> GetByIdAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users in the system across all portals.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of all <see cref="User"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUsers() which returned an ArrayList.
    /// The return type is now strongly-typed as IEnumerable&lt;User&gt;.
    /// Use <see cref="GetByPortalIdAsync"/> for portal-scoped queries.
    /// </remarks>
    /// <example>
    /// <code>
    /// var allUsers = await userRepository.GetAllAsync();
    /// foreach (var user in allUsers)
    /// {
    ///     Console.WriteLine($"User: {user.Username}, Portal: {user.PortalId}");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users belonging to a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of <see cref="User"/> entities belonging to the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUsers(ByVal portalId As Integer).
    /// Returns all users for a portal without pagination. For large portals,
    /// use <see cref="GetPagedAsync"/> for better performance.
    /// </remarks>
    /// <example>
    /// <code>
    /// var portalUsers = await userRepository.GetByPortalIdAsync(portalId: 1);
    /// Console.WriteLine($"Found {portalUsers.Count()} users in portal 1");
    /// </code>
    /// </example>
    Task<IEnumerable<User>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their username within a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal context.</param>
    /// <param name="username">The username to search for. This is case-insensitive.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUserByName(ByVal portalId As Integer, ByVal username As String).
    /// The original method called memberProvider.GetUserByUserName internally.
    /// Username uniqueness is enforced per portal.
    /// </remarks>
    /// <example>
    /// <code>
    /// var user = await userRepository.GetByUsernameAsync(portalId: 1, username: "johndoe");
    /// if (user is not null)
    /// {
    ///     Console.WriteLine($"Found user: {user.DisplayName}");
    /// }
    /// </code>
    /// </example>
    Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by their email address within a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal context.</param>
    /// <param name="email">The email address to search for. This is case-insensitive.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUsersByEmail which returned multiple users.
    /// This method returns a single user assuming unique email per portal.
    /// If multiple users can share an email, use a different overload returning IEnumerable.
    /// </remarks>
    /// <example>
    /// <code>
    /// var user = await userRepository.GetByEmailAsync(portalId: 1, email: "john@example.com");
    /// if (user is not null)
    /// {
    ///     Console.WriteLine($"Found user: {user.Username}");
    /// }
    /// </code>
    /// </example>
    Task<User?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of users for a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="pageIndex">
    /// The zero-based index of the page to retrieve. Use -1 to retrieve all records.
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of records to return per page. When pageIndex is -1,
    /// this parameter is ignored and all matching records are returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a tuple with the matching <see cref="User"/> entities and the total record count
    /// for pagination purposes.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUsers(ByVal portalId As Integer, ByVal pageIndex As Integer, ByVal pageSize As Integer, ByRef totalRecords As Integer).
    /// The original method used ByRef for totalRecords; this version returns a tuple.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (users, totalCount) = await userRepository.GetPagedAsync(portalId: 1, pageIndex: 0, pageSize: 25);
    /// Console.WriteLine($"Showing {users.Count()} of {totalCount} total users");
    /// </code>
    /// </example>
    Task<(IEnumerable<User> Users, int TotalRecords)> GetPagedAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of users in a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the total number of users in the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUserCountByPortal(ByVal portalId As Integer).
    /// This method is optimized for counting without loading user entities.
    /// </remarks>
    /// <example>
    /// <code>
    /// var userCount = await userRepository.GetUserCountAsync(portalId: 1);
    /// Console.WriteLine($"Portal 1 has {userCount} registered users");
    /// </code>
    /// </example>
    Task<int> GetUserCountAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unauthorized (not approved) users for a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of <see cref="User"/> entities that are not approved.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET UserController.GetUnAuthorizedUsers(ByVal portalId As Integer).
    /// Returns users where IsApproved is false or LastLoginDate is null (never logged in).
    /// </remarks>
    /// <example>
    /// <code>
    /// var unauthorizedUsers = await userRepository.GetUnauthorizedUsersAsync(portalId: 1);
    /// foreach (var user in unauthorizedUsers)
    /// {
    ///     Console.WriteLine($"Pending approval: {user.Username}");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<User>> GetUnauthorizedUsersAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all super users (host administrators) in the system.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of <see cref="User"/> entities that are super users.
    /// </returns>
    /// <remarks>
    /// Super users have access to all portals and host-level settings.
    /// This method retrieves users where IsSuperUser is true.
    /// </remarks>
    /// <example>
    /// <code>
    /// var superUsers = await userRepository.GetSuperUsersAsync();
    /// Console.WriteLine($"Found {superUsers.Count()} super users");
    /// </code>
    /// </example>
    Task<IEnumerable<User>> GetSuperUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user with the specified username exists in a portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal context.</param>
    /// <param name="username">The username to check for existence.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is
    /// <c>true</c> if a user with the specified username exists in the portal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method is optimized for existence checking without loading the full entity.
    /// Use this instead of <see cref="GetByUsernameAsync"/> when you only need to verify existence.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (await userRepository.ExistsAsync(portalId: 1, username: "newuser"))
    /// {
    ///     Console.WriteLine("Username is already taken");
    /// }
    /// </code>
    /// </example>
    Task<bool> ExistsAsync(int portalId, string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the data store.
    /// </summary>
    /// <param name="user">The user entity to add.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the added <see cref="User"/> entity with its generated identifier populated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.CreateUser(ByRef objUser As UserInfo).
    /// The original method returned a UserCreateStatus enum and modified the user object by reference.
    /// </para>
    /// <para>
    /// This method handles only data persistence. Business logic such as auto-role assignment
    /// and email notifications should be handled in the application service layer.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var newUser = new User
    /// {
    ///     PortalId = 1,
    ///     Username = "johndoe",
    ///     Email = "john@example.com",
    ///     DisplayName = "John Doe",
    ///     FirstName = "John",
    ///     LastName = "Doe",
    ///     IsApproved = true
    /// };
    /// var createdUser = await userRepository.AddAsync(newUser);
    /// Console.WriteLine($"Created user with ID: {createdUser.UserId}");
    /// </code>
    /// </example>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the data store.
    /// </summary>
    /// <param name="user">The user entity with updated values.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.UpdateUser(ByVal PortalId As Integer, ByRef objUser As UserInfo).
    /// </para>
    /// <para>
    /// This method persists all changes to the user entity including profile updates.
    /// The UpdatedDate property should be set by the caller or the repository implementation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var user = await userRepository.GetByIdAsync(portalId: 1, userId: 100);
    /// if (user is not null)
    /// {
    ///     user.DisplayName = "John Q. Doe";
    ///     user.Email = "johnq@example.com";
    ///     await userRepository.UpdateAsync(user);
    /// }
    /// </code>
    /// </example>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user from the data store.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal context.</param>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET UserController.DeleteUser(ByRef objUser As UserInfo, ByVal notify As Boolean, ByVal deleteAdmin As Boolean).
    /// </para>
    /// <para>
    /// This method performs the data deletion only. Business logic such as:
    /// <list type="bullet">
    ///   <item><description>Checking if user is portal administrator</description></item>
    ///   <item><description>Deleting associated permissions</description></item>
    ///   <item><description>Sending notification emails</description></item>
    ///   <item><description>Clearing caches</description></item>
    /// </list>
    /// should be handled in the application service layer.
    /// </para>
    /// <para>
    /// The implementation may perform a soft delete (setting IsDeleted = true) rather than
    /// a hard delete, depending on business requirements.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await userRepository.DeleteAsync(portalId: 1, userId: 100);
    /// Console.WriteLine("User deleted successfully");
    /// </code>
    /// </example>
    Task DeleteAsync(int portalId, int userId, CancellationToken cancellationToken = default);
}
