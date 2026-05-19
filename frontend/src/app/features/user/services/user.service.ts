/**
 * User Service - Angular 19 HTTP Service for User Management API Operations
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET data access patterns:
 * - Library/Components/Users/UserController.vb (lines 156-185, 200-259, 458-480,
 *   497-501, 544-567, 582-586, 725-750, 769-843, 963-969)
 * - Website/admin/Users/User.ascx.vb - CreateUser method (lines 209-238)
 * - Website/admin/Users/ManageUsers.ascx.vb - User listing and filtering
 * - Website/admin/Users/Users.ascx.vb - Filter modes
 *
 * This service replaces the legacy SqlHelper/DataProvider patterns with modern
 * Angular HTTP client operations. All methods return RxJS Observables for
 * reactive data handling.
 *
 * Key transformations:
 * - VB.NET UserController.GetUsers() → HttpClient.get()
 * - VB.NET UserController.CreateUser() → HttpClient.post()
 * - VB.NET UserController.UpdateUser() → HttpClient.put()
 * - VB.NET UserController.DeleteUser() → HttpClient.delete()
 * - VB.NET ByRef totalRecords parameter → PagedResult wrapper
 * - VB.NET ArrayList returns → typed User[] arrays
 * - VB.NET Null.NullInteger → optional parameters with undefined
 * - VB.NET isHydrated boolean removed (always hydrate in API)
 * - DNN's portal-scoped queries handled by backend JWT portal context
 *
 * @fileoverview HTTP service for user CRUD operations, profile management,
 *               and user authorization
 */

import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ApiService } from '../../../core/services/api.service';
import { User, UserProfile, UserMembership } from '../../../core/models/user.model';

/**
 * Filter interface for user search and listing operations.
 *
 * MIGRATION: Derived from DNN Users.ascx.vb filter modes and
 * UserController.vb GetUsersByEmail/GetUsersByUserName methods.
 *
 * Original VB.NET patterns:
 * - GetUsersByEmail(portalId, emailToMatch, pageIndex, pageSize, totalRecords)
 * - GetUsersByUserName(portalId, userNameToMatch, pageIndex, pageSize, totalRecords)
 * - GetUnAuthorizedUsers(portalId) for isAuthorized filter
 *
 * @interface UserFilter
 */
export interface UserFilter {
  /**
   * General search term to match against multiple user fields.
   * Backend will search across username, displayName, email, and name fields.
   */
  search?: string;

  /**
   * Filter by email address (partial match supported).
   * MIGRATION: Replaces GetUsersByEmail() method (lines 769-797)
   */
  email?: string;

  /**
   * Filter by username (partial match supported).
   * MIGRATION: Replaces GetUsersByUserName() method (lines 816-844)
   */
  username?: string;

  /**
   * Filter users by role membership.
   * Returns only users that belong to the specified role.
   */
  roleId?: number;

  /**
   * Filter by authorization status.
   * When true, returns only authorized users.
   * When false, returns unauthorized users (pending approval).
   * MIGRATION: Replaces GetUnAuthorizedUsers() when set to false (lines 458-480)
   */
  isAuthorized?: boolean;
}

/**
 * Pagination parameters for list operations.
 *
 * MIGRATION: Derived from DNN UserController.GetUsers() pagination pattern:
 * - pageIndex: Zero-based page index (from pageIndex parameter line 725)
 * - pageSize: Number of items per page (from pageSize parameter line 725)
 *
 * @interface PaginationParams
 */
export interface PaginationParams {
  /**
   * Zero-based page index for pagination.
   * MIGRATION: Maps to VB.NET pageIndex parameter in GetUsers()
   */
  pageIndex: number;

  /**
   * Number of items per page.
   * MIGRATION: Maps to VB.NET pageSize parameter in GetUsers()
   */
  pageSize: number;
}

/**
 * Generic wrapper for paginated API responses.
 *
 * MIGRATION: Replaces VB.NET pattern of ArrayList + ByRef totalRecords:
 * ```vb
 * Public Shared Function GetUsers(ByVal portalId As Integer,
 *     ByVal pageIndex As Integer, ByVal pageSize As Integer,
 *     ByRef totalRecords As Integer) As ArrayList
 * ```
 *
 * This wrapper consolidates the separate ArrayList and totalRecords
 * into a single typed response object.
 *
 * @interface PagedResult
 * @template T - The type of items in the result set
 */
export interface PagedResult<T> {
  /**
   * Array of items for the current page.
   * MIGRATION: Replaces VB.NET ArrayList return value
   */
  items: T[];

  /**
   * Total count of items matching the query (across all pages).
   * MIGRATION: Replaces VB.NET ByRef totalRecords parameter
   */
  totalCount: number;

  /**
   * Current page index (zero-based).
   */
  pageIndex: number;

  /**
   * Number of items per page.
   */
  pageSize: number;

  /**
   * Total number of pages available.
   * Calculated as: Math.ceil(totalCount / pageSize)
   */
  totalPages: number;
}

/**
 * Request interface for creating a new user.
 *
 * MIGRATION: Derived from DNN User.ascx.vb CreateUser method (lines 209-238)
 * and UserController.CreateUser(ByRef objUser As UserInfo) (lines 156-185)
 *
 * Original VB.NET UserInfo properties used in creation:
 * - Username, Email, DisplayName, FirstName, LastName
 * - Membership.Approved flag for initial authorization status
 *
 * @interface CreateUserRequest
 */
export interface CreateUserRequest {
  /**
   * Unique username for authentication.
   * MIGRATION: Maps to UserInfo.Username property (required)
   */
  username: string;

  /**
   * User's email address.
   * MIGRATION: Maps to UserInfo.Email property (required)
   */
  email: string;

  /**
   * Initial password for the user account.
   * MIGRATION: Handled by membership provider on backend
   */
  password: string;

  /**
   * Display name shown in the UI.
   * MIGRATION: Maps to UserInfo.DisplayName property (optional)
   * If not provided, backend typically generates from first/last name
   */
  displayName?: string;

  /**
   * User's first name.
   * MIGRATION: Maps to UserInfo.FirstName property (optional)
   */
  firstName?: string;

  /**
   * User's last name.
   * MIGRATION: Maps to UserInfo.LastName property (optional)
   */
  lastName?: string;

  /**
   * Whether the user account is approved for login.
   * MIGRATION: Maps to UserInfo.Membership.Approved property
   * Default behavior depends on portal registration settings
   */
  approved?: boolean;
}

/**
 * Request interface for updating an existing user.
 *
 * MIGRATION: Derived from DNN UserController.UpdateUser(portalId, objUser)
 * (line 963) and User.ascx.vb update operations
 *
 * All properties are optional to support partial updates.
 *
 * @interface UpdateUserRequest
 */
export interface UpdateUserRequest {
  /**
   * Updated display name.
   * MIGRATION: Maps to UserInfo.DisplayName property
   */
  displayName?: string;

  /**
   * Updated email address.
   * MIGRATION: Maps to UserInfo.Email property
   */
  email?: string;

  /**
   * Updated first name.
   * MIGRATION: Maps to UserInfo.FirstName (via Profile)
   */
  firstName?: string;

  /**
   * Updated last name.
   * MIGRATION: Maps to UserInfo.LastName (via Profile)
   */
  lastName?: string;

  /**
   * Updated user profile data.
   * MIGRATION: Maps to UserInfo.Profile object for extended profile fields
   */
  profile?: Partial<UserProfile>;

  /**
   * Updated membership status.
   * Used for approval/lockout status changes.
   * MIGRATION: Maps to UserInfo.Membership for status flags
   */
  membership?: Partial<UserMembership>;
}

/**
 * User Service
 *
 * Angular 19 HTTP service providing CRUD operations for user management,
 * profile management, and user authorization. Uses the inject() function
 * for dependency injection per Angular 19 standards.
 *
 * @Injectable with providedIn: 'root' ensures singleton pattern application-wide.
 *
 * MIGRATION: Replaces the following DNN components:
 * - UserController.vb - User CRUD operations
 * - MembershipProvider - User authentication/authorization
 *
 * API Endpoints:
 * - GET    /api/users              - List users (paginated, filtered)
 * - GET    /api/users/:id          - Get user by ID
 * - GET    /api/users/username/:username - Get user by username
 * - GET    /api/users/unauthorized - Get unauthorized users
 * - GET    /api/users/count        - Get user count
 * - POST   /api/users              - Create user
 * - PUT    /api/users/:id          - Update user
 * - PUT    /api/users/:id/profile  - Update user profile
 * - PUT    /api/users/:id/unlock   - Unlock user account
 * - DELETE /api/users/:id          - Delete user
 *
 * @class UserService
 */
@Injectable({
  providedIn: 'root',
})
export class UserService {
  /**
   * API Service instance injected via inject() function.
   * MIGRATION: Angular 19 prefers inject() over constructor injection.
   * Provides centralized HTTP methods with error handling.
   * @private
   */
  private readonly api = inject(ApiService);

  /**
   * API endpoint path for user operations.
   * MIGRATION: Replaces legacy DNN DataProvider qualified procedure names.
   * @private
   */
  private readonly endpoint = 'users';

  // ==========================================================================
  // USER LIST/SEARCH OPERATIONS
  // ==========================================================================

  /**
   * Retrieves a paginated and filtered list of users.
   *
   * MIGRATION: Replaces multiple VB.NET methods:
   * - UserController.GetUsers(portalId, pageIndex, pageSize, totalRecords) lines 725-729
   * - UserController.GetUsersByEmail(...) lines 769-797
   * - UserController.GetUsersByUserName(...) lines 816-844
   *
   * Original VB.NET patterns consolidated into single parameterized API call:
   * ```vb
   * Public Shared Function GetUsers(ByVal portalId As Integer,
   *     ByVal pageIndex As Integer, ByVal pageSize As Integer,
   *     ByRef totalRecords As Integer) As ArrayList
   *     Return memberProvider.GetUsers(portalId, False, pageIndex, pageSize, totalRecords)
   * End Function
   * ```
   *
   * @param filter - Optional filter criteria for searching users
   * @param pagination - Optional pagination parameters (defaults to page 0, size 10)
   * @returns Observable<PagedResult<User>> - Paginated list of users
   *
   * @example
   * ```typescript
   * // Get all users with default pagination
   * userService.getUsers().subscribe(result => {
   *   console.log(`Found ${result.totalCount} users`);
   * });
   *
   * // Search users by email with custom pagination
   * userService.getUsers(
   *   { email: 'admin@' },
   *   { pageIndex: 0, pageSize: 25 }
   * ).subscribe(result => {
   *   result.items.forEach(user => console.log(user.email));
   * });
   *
   * // Get authorized users only
   * userService.getUsers({ isAuthorized: true }).subscribe(result => {
   *   console.log(`${result.totalCount} authorized users`);
   * });
   * ```
   */
  getUsers(
    filter?: UserFilter,
    pagination?: PaginationParams
  ): Observable<PagedResult<User>> {
    const params = this.buildParams(filter, pagination);
    return this.api.get<PagedResult<User>>(this.endpoint, params);
  }

  /**
   * Retrieves a paginated list of unauthorized (pending approval) users.
   *
   * MIGRATION: Replaces VB.NET UserController.GetUnAuthorizedUsers() (lines 458-480):
   * ```vb
   * Public Shared Function GetUnAuthorizedUsers(ByVal portalId As Integer) As ArrayList
   *     Return memberProvider.GetUnAuthorizedUsers(portalId, False)
   * End Function
   * ```
   *
   * @param pagination - Optional pagination parameters
   * @returns Observable<PagedResult<User>> - Paginated list of unauthorized users
   *
   * @example
   * ```typescript
   * userService.getUnauthorizedUsers({ pageIndex: 0, pageSize: 50 })
   *   .subscribe(result => {
   *     console.log(`${result.totalCount} users pending approval`);
   *   });
   * ```
   */
  getUnauthorizedUsers(
    pagination?: PaginationParams
  ): Observable<PagedResult<User>> {
    const params = this.buildParams({ isAuthorized: false }, pagination);
    return this.api.get<PagedResult<User>>(`${this.endpoint}/unauthorized`, params);
  }

  /**
   * Gets the total count of users in the portal.
   *
   * MIGRATION: Replaces VB.NET UserController.GetUserCountByPortal() (lines 582-586):
   * ```vb
   * Public Shared Function GetUserCountByPortal(ByVal portalId As Integer) As Integer
   *     Return memberProvider.GetUserCountByPortal(portalId)
   * End Function
   * ```
   *
   * @returns Observable<number> - Total user count for the current portal
   *
   * @example
   * ```typescript
   * userService.getUserCount().subscribe(count => {
   *   console.log(`Portal has ${count} registered users`);
   * });
   * ```
   */
  getUserCount(): Observable<number> {
    return this.api.get<number>(`${this.endpoint}/count`);
  }

  // ==========================================================================
  // USER DETAIL OPERATIONS
  // ==========================================================================

  /**
   * Retrieves a single user by their unique identifier.
   *
   * MIGRATION: Replaces VB.NET UserController.GetUser() (lines 497-501):
   * ```vb
   * Public Shared Function GetUser(ByVal portalId As Integer,
   *     ByVal userId As Integer, ByVal isHydrated As Boolean) As UserInfo
   *     Return memberProvider.GetUser(portalId, userId, isHydrated)
   * End Function
   * ```
   *
   * Note: The isHydrated parameter is removed as the API always returns
   * fully hydrated user objects including profile and membership data.
   *
   * @param id - The unique user ID to retrieve
   * @returns Observable<User> - The user entity with all related data
   *
   * @example
   * ```typescript
   * userService.getUserById(123).subscribe(
   *   user => console.log(`Retrieved user: ${user.displayName}`),
   *   error => console.error('User not found', error)
   * );
   * ```
   */
  getUserById(id: number): Observable<User> {
    return this.api.get<User>(`${this.endpoint}/${id}`);
  }

  /**
   * Retrieves a user by their username.
   *
   * MIGRATION: Replaces VB.NET UserController.GetUserByName() (lines 544-567):
   * ```vb
   * Public Shared Function GetUserByName(ByVal portalId As Integer,
   *     ByVal username As String) As UserInfo
   *     Return memberProvider.GetUserByUserName(portalId, username, False)
   * End Function
   * ```
   *
   * @param username - The username to search for (case-insensitive)
   * @returns Observable<User | null> - The user if found, null otherwise
   *
   * @example
   * ```typescript
   * userService.getUserByUsername('admin').subscribe(
   *   user => {
   *     if (user) {
   *       console.log(`Found user: ${user.displayName}`);
   *     } else {
   *       console.log('User not found');
   *     }
   *   }
   * );
   * ```
   */
  getUserByUsername(username: string): Observable<User | null> {
    const params = new HttpParams().set('username', username);
    return this.api.get<User | null>(`${this.endpoint}/by-username`, params);
  }

  // ==========================================================================
  // USER CRUD OPERATIONS
  // ==========================================================================

  /**
   * Creates a new user with the specified information.
   *
   * MIGRATION: Replaces VB.NET UserController.CreateUser() (lines 156-185):
   * ```vb
   * Public Shared Function CreateUser(ByRef objUser As UserInfo) As UserCreateStatus
   *     Dim createStatus As UserCreateStatus = UserCreateStatus.AddUser
   *     createStatus = memberProvider.CreateUser(objUser)
   *     If createStatus = UserCreateStatus.Success Then
   *         DataCache.ClearPortalCache(objUser.PortalID, False)
   *         ' ... auto-assign roles
   *     End If
   *     Return createStatus
   * End Function
   * ```
   *
   * The backend handles:
   * - Creating the user database record
   * - Setting up membership credentials
   * - Auto-assigning portal roles
   * - Sending notification emails if configured
   * - Cache invalidation
   *
   * @param request - CreateUserRequest containing user information
   * @returns Observable<User> - The newly created user
   *
   * @example
   * ```typescript
   * const newUser: CreateUserRequest = {
   *   username: 'john.doe',
   *   email: 'john.doe@example.com',
   *   password: 'SecurePassword123!',
   *   firstName: 'John',
   *   lastName: 'Doe',
   *   displayName: 'John Doe',
   *   approved: true
   * };
   *
   * userService.createUser(newUser).subscribe(
   *   user => console.log(`Created user: ${user.userId}`),
   *   error => console.error('Failed to create user', error)
   * );
   * ```
   */
  createUser(request: CreateUserRequest): Observable<User> {
    return this.api.post<User>(this.endpoint, request);
  }

  /**
   * Updates an existing user with the specified information.
   *
   * MIGRATION: Replaces VB.NET UserController.UpdateUser() (lines 963-969):
   * ```vb
   * Public Shared Sub UpdateUser(ByVal portalId As Integer, ByVal objUser As UserInfo)
   *     memberProvider.UpdateUser(objUser)
   *     DataCache.ClearUserCache(portalId, objUser.Username)
   * End Sub
   * ```
   *
   * The backend handles:
   * - Updating user database record
   * - Updating profile fields if provided
   * - Updating membership status if provided
   * - Cache invalidation
   *
   * @param id - The user ID to update
   * @param request - UpdateUserRequest containing fields to update
   * @returns Observable<User> - The updated user
   *
   * @example
   * ```typescript
   * const updates: UpdateUserRequest = {
   *   displayName: 'Jonathan Doe',
   *   email: 'jonathan.doe@example.com',
   *   profile: {
   *     telephone: '555-1234',
   *     city: 'New York'
   *   }
   * };
   *
   * userService.updateUser(123, updates).subscribe(
   *   user => console.log(`Updated user: ${user.displayName}`),
   *   error => console.error('Failed to update user', error)
   * );
   * ```
   */
  updateUser(id: number, request: UpdateUserRequest): Observable<User> {
    return this.api.put<User>(`${this.endpoint}/${id}`, request);
  }

  /**
   * Deletes a user from the system.
   *
   * MIGRATION: Replaces VB.NET UserController.DeleteUser() (lines 200-259):
   * ```vb
   * Public Shared Function DeleteUser(ByRef objUser As UserInfo,
   *     ByVal notify As Boolean, ByVal deleteAdmin As Boolean) As Boolean
   *     ' ... permission checks
   *     ' Delete folder/module/tab permissions
   *     CanDelete = memberProvider.DeleteUser(objUser)
   *     ' ... notification and cache clear
   *     Return CanDelete
   * End Function
   * ```
   *
   * The backend handles:
   * - Permission validation (cannot delete portal admin unless authorized)
   * - Removing user from all roles
   * - Deleting associated permissions (folder, module, tab)
   * - Removing user membership record
   * - Event logging
   * - Optional notification to administrators
   * - Cache invalidation
   *
   * @param id - The user ID to delete
   * @returns Observable<void> - Completes on success
   *
   * @example
   * ```typescript
   * userService.deleteUser(123).subscribe({
   *   complete: () => console.log('User deleted successfully'),
   *   error: (err) => console.error('Failed to delete user', err)
   * });
   * ```
   */
  deleteUser(id: number): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }

  // ==========================================================================
  // USER PROFILE OPERATIONS
  // ==========================================================================

  /**
   * Updates a user's profile information.
   *
   * MIGRATION: Derived from DNN ProfileController.UpdateUserProfile() and
   * UserInfo.Profile property handling. Profile updates in DNN 4.x were
   * handled separately from core user updates.
   *
   * @param id - The user ID whose profile to update
   * @param profile - Partial profile data to update
   * @returns Observable<User> - The user with updated profile
   *
   * @example
   * ```typescript
   * userService.updateProfile(123, {
   *   firstName: 'John',
   *   lastName: 'Smith',
   *   telephone: '555-0123',
   *   city: 'Seattle',
   *   region: 'WA',
   *   country: 'USA'
   * }).subscribe(user => {
   *   console.log(`Profile updated for ${user.displayName}`);
   * });
   * ```
   */
  updateProfile(id: number, profile: Partial<UserProfile>): Observable<User> {
    return this.api.put<User>(`${this.endpoint}/${id}/profile`, profile);
  }

  // ==========================================================================
  // USER AUTHORIZATION OPERATIONS
  // ==========================================================================

  /**
   * Unlocks a user account that has been locked out.
   *
   * MIGRATION: Replaces DNN membership provider unlock functionality.
   * In DNN 4.x, locked users were managed through the membership provider
   * with the UserMembership.LockedOut property.
   *
   * Original VB.NET pattern (via membership provider):
   * ```vb
   * objUser.Membership.LockedOut = False
   * memberProvider.UpdateUser(objUser)
   * ```
   *
   * @param id - The user ID to unlock
   * @returns Observable<User> - The user with updated lockout status
   *
   * @example
   * ```typescript
   * // Unlock a user who has been locked out due to failed login attempts
   * userService.unlockUser(123).subscribe(
   *   user => {
   *     console.log(`User ${user.username} has been unlocked`);
   *     console.log(`Locked out: ${user.membership?.lockedOut}`);
   *   },
   *   error => console.error('Failed to unlock user', error)
   * );
   * ```
   */
  unlockUser(id: number): Observable<User> {
    // MIGRATION: Sets membership.lockedOut = false and membership.approved = true
    return this.api.put<User>(`${this.endpoint}/${id}/unlock`, {});
  }

  // ==========================================================================
  // PRIVATE HELPER METHODS
  // ==========================================================================

  /**
   * Builds HttpParams from filter and pagination options.
   *
   * MIGRATION: Converts TypeScript filter/pagination objects into HTTP query
   * parameters for the REST API. This replaces the multiple VB.NET method
   * overloads that accepted different combinations of filter parameters.
   *
   * @param filter - Optional filter criteria
   * @param pagination - Optional pagination parameters
   * @returns HttpParams - Query parameters for the HTTP request
   * @private
   */
  private buildParams(
    filter?: UserFilter,
    pagination?: PaginationParams
  ): HttpParams {
    let params = new HttpParams();

    // Add pagination parameters with defaults
    // MIGRATION: Default pagination matches DNN admin UI defaults
    params = params.set('pageIndex', (pagination?.pageIndex ?? 0).toString());
    params = params.set('pageSize', (pagination?.pageSize ?? 10).toString());

    // Add filter parameters if provided
    if (filter) {
      // General search term
      if (filter.search && filter.search.trim()) {
        params = params.set('search', filter.search.trim());
      }

      // Email filter
      // MIGRATION: Replaces separate GetUsersByEmail() call
      if (filter.email && filter.email.trim()) {
        params = params.set('email', filter.email.trim());
      }

      // Username filter
      // MIGRATION: Replaces separate GetUsersByUserName() call
      if (filter.username && filter.username.trim()) {
        params = params.set('username', filter.username.trim());
      }

      // Role filter
      if (filter.roleId !== undefined && filter.roleId !== null) {
        params = params.set('roleId', filter.roleId.toString());
      }

      // Authorization status filter
      // MIGRATION: When false, replaces GetUnAuthorizedUsers() call
      if (filter.isAuthorized !== undefined && filter.isAuthorized !== null) {
        params = params.set('isAuthorized', filter.isAuthorized.toString());
      }
    }

    return params;
  }
}
