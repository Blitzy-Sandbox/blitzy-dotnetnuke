/**
 * Role Management Service - Angular 19 HTTP Service for Role Management API Operations
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET data access patterns:
 * - Library/Components/Security/Roles/RoleController.vb (lines 100-842)
 * - Library/Components/Security/Roles/RoleInfo.vb (entity properties)
 * - Library/Components/Security/Roles/RoleGroupInfo.vb (entity properties)
 *
 * This service replaces the legacy SqlHelper/DataProvider patterns with modern
 * Angular HTTP client operations. All methods return RxJS Observables for
 * reactive data handling.
 *
 * Key transformations:
 * - VB.NET DataProvider.Instance() pattern replaced with HttpClient
 * - VB.NET ArrayList returns converted to typed arrays
 * - VB.NET Null.NullDate converted to null or undefined
 * - VB.NET ByRef parameters eliminated (responses contain all data)
 * - Portal context derived from JWT token in backend, not passed explicitly
 * - VB.NET RoleController.GetPortalRoles() → getRoles()
 * - VB.NET RoleController.GetRole() → getRole()
 * - VB.NET RoleController.AddRole() → createRole()
 * - VB.NET RoleController.UpdateRole() → updateRole()
 * - VB.NET RoleController.DeleteRole() → deleteRole()
 * - VB.NET RoleController.GetRoleGroups() → getRoleGroups()
 * - VB.NET RoleController.AddUserRole() → addUserToRole()
 * - VB.NET RoleController.DeleteUserRole() → removeUserFromRole()
 *
 * @fileoverview HTTP service for role CRUD operations, role group management,
 *               and user-role assignment operations
 * @module features/role/services/role.service
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

// Internal imports - from depends_on_files
import { environment } from '../../../../environments/environment';
import {
  Role,
  RoleGroup,
  CreateRoleRequest,
  UpdateRoleRequest
} from '../models/role.model';
import { User } from '../../../core/models/user.model';
import { PagedResult } from '../../user/services/user.service';

// Re-export types used by role feature components
export type { User } from '../../../core/models/user.model';
export type { PagedResult } from '../../user/services/user.service';

// ============================================================================
// EXPORTED INTERFACES
// ============================================================================

/**
 * Interface for adding user to role request.
 *
 * MIGRATION: Derived from RoleController.AddUserRole parameters (lines 295-315):
 * ```vb
 * Public Sub AddUserRole(ByVal PortalID As Integer, ByVal UserId As Integer,
 *     ByVal RoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date)
 * ```
 *
 * The EffectiveDate and ExpiryDate are optional in the modern API;
 * if omitted, the backend uses default values (Now() and null respectively).
 *
 * @interface AddUserRoleRequest
 */
export interface AddUserRoleRequest {
  /**
   * Date when role membership becomes effective.
   * MIGRATION: Maps to VB.NET EffectiveDate parameter.
   * If omitted, defaults to current date/time on the backend.
   */
  effectiveDate?: Date | string;

  /**
   * Date when role membership expires.
   * MIGRATION: Maps to VB.NET ExpiryDate parameter (Null.NullDate in VB = null here).
   * If omitted or null, the membership does not expire.
   */
  expiryDate?: Date | string;
}

/**
 * Interface for creating a role group.
 *
 * MIGRATION: Derived from RoleController.AddRoleGroup parameters (lines 626-630):
 * ```vb
 * Public Shared Function AddRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo) As Integer
 * ```
 *
 * Properties mapped from RoleGroupInfo.vb (lines 41-106).
 *
 * @interface CreateRoleGroupRequest
 */
export interface CreateRoleGroupRequest {
  /**
   * Name of the role group.
   * MIGRATION: Maps to RoleGroupInfo.RoleGroupName property (line 83-90).
   */
  roleGroupName: string;

  /**
   * Optional description of the role group.
   * MIGRATION: Maps to RoleGroupInfo.Description property (line 98-105).
   */
  description?: string;
}

/**
 * Interface for updating a role group.
 *
 * MIGRATION: Derived from RoleController.UpdateRoleGroup parameters (lines 838-842):
 * ```vb
 * Public Shared Sub UpdateRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo)
 * ```
 *
 * @interface UpdateRoleGroupRequest
 */
export interface UpdateRoleGroupRequest {
  /**
   * Name of the role group.
   * MIGRATION: Maps to RoleGroupInfo.RoleGroupName property.
   */
  roleGroupName: string;

  /**
   * Optional description of the role group.
   * MIGRATION: Maps to RoleGroupInfo.Description property.
   */
  description?: string;
}

/**
 * Interface for user-role relationship details.
 *
 * MIGRATION: Derived from UserRoleInfo properties in RoleController.vb.
 * The GetUserRole method (lines 362-364) returns this information:
 * ```vb
 * Public Function GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer,
 *     ByVal RoleId As Integer) As UserRoleInfo
 * ```
 *
 * @interface UserRole
 */
export interface UserRole {
  /**
   * Unique identifier for the user-role assignment.
   * MIGRATION: Maps to UserRoleInfo.UserRoleID property.
   */
  userRoleId: number;

  /**
   * User identifier.
   * MIGRATION: Maps to UserRoleInfo.UserID property.
   */
  userId: number;

  /**
   * Role identifier.
   * MIGRATION: Maps to UserRoleInfo.RoleID property.
   */
  roleId: number;

  /**
   * Date when role membership became effective.
   * MIGRATION: Maps to UserRoleInfo.EffectiveDate property.
   * Null indicates the membership was always effective.
   */
  effectiveDate: Date | null;

  /**
   * Date when role membership expires.
   * MIGRATION: Maps to UserRoleInfo.ExpiryDate property.
   * Null indicates no expiration (Null.NullDate in VB).
   */
  expiryDate: Date | null;

  /**
   * Indicates whether the trial period has been used.
   * MIGRATION: Maps to UserRoleInfo.IsTrialUsed property.
   * Used for subscription-based roles with trial periods.
   */
  isTrialUsed: boolean;
}

/**
 * Interface for filtering roles in list operations.
 *
 * MIGRATION: Consolidated from various VB.NET GetRoles methods:
 * - GetPortalRoles(ByVal PortalId As Integer) - portalId filter
 * - GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) - roleGroupId filter
 *
 * @interface RoleFilter
 */
export interface RoleFilter {
  /**
   * Filter by portal ID.
   * MIGRATION: Maps to PortalId parameter in GetPortalRoles().
   */
  portalId?: number;

  /**
   * Filter by role group ID.
   * MIGRATION: Maps to roleGroupId parameter in GetRolesByGroup().
   * Special values: -1 for global roles (no group), -2 for all roles.
   */
  roleGroupId?: number;

  /**
   * Zero-based page index for pagination.
   */
  pageIndex?: number;

  /**
   * Number of items per page.
   */
  pageSize?: number;
}

// ============================================================================
// ROLE SERVICE CLASS
// ============================================================================

/**
 * RoleService - Angular 19 service for role management API communication.
 *
 * This service provides typed role-specific operations:
 * - Role CRUD operations (getRoles, getRole, createRole, updateRole, deleteRole)
 * - Role group operations (getRoleGroups, getRoleGroup, createRoleGroup, updateRoleGroup, deleteRoleGroup)
 * - User-role assignment management (getUsersInRole, getRolesByUser, getUserRole, addUserToRole, removeUserFromRole)
 *
 * Uses HttpClient via inject() function per Angular 19 standards and RxJS Observables
 * for reactive API communication.
 *
 * MIGRATION: Replaces DNN RoleController.vb business logic with REST API calls.
 * All data access patterns using SqlHelper/DataProvider are replaced with HTTP operations.
 *
 * @Injectable with providedIn: 'root' for singleton pattern
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  // ==========================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() function)
  // ==========================================================================

  /**
   * HttpClient injected using Angular 19 inject() function.
   * MIGRATION: Replaces VB.NET DataProvider.Instance() pattern.
   */
  private readonly http = inject(HttpClient);

  // ==========================================================================
  // CONFIGURATION
  // ==========================================================================

  /**
   * Base API URL from environment configuration.
   * MIGRATION: Environment-based URL replaces web.config connection strings.
   */
  private readonly apiUrl = environment.apiBaseUrl;

  /**
   * Roles API endpoint path.
   */
  private readonly endpoint = '/roles';

  /**
   * Role groups API endpoint path.
   */
  private readonly groupsEndpoint = '/roles/groups';

  // ==========================================================================
  // ROLE CRUD METHODS
  // Derived from RoleController.vb lines 100-258
  // ==========================================================================

  /**
   * Get paginated list of roles with optional filtering.
   *
   * MIGRATION: Replaces VB.NET methods (RoleController.vb):
   * - GetPortalRoles(ByVal PortalId As Integer) As ArrayList (lines 146-148)
   * - GetRolesByGroup(ByVal portalId As Integer, ByVal roleGroupId As Integer) As ArrayList (lines 224-226)
   *
   * The VB.NET ArrayList return type is replaced with typed PagedResult<Role>.
   *
   * @param portalId - Optional portal ID filter
   * @param roleGroupId - Optional role group ID filter (-1 for global roles, -2 for all)
   * @param pageIndex - Page number (0-based), defaults to 0
   * @param pageSize - Items per page, defaults to 10
   * @returns Observable of paginated role results
   */
  getRoles(
    portalId?: number,
    roleGroupId?: number,
    pageIndex: number = 0,
    pageSize: number = 10
  ): Observable<PagedResult<Role>> {
    let params = this.buildParams({
      pageIndex,
      pageSize,
      portalId,
      roleGroupId
    });

    return this.http.get<PagedResult<Role>>(
      this.buildUrl(this.endpoint),
      { params }
    );
  }

  /**
   * Get a single role by ID.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 163-165):
   * ```vb
   * Public Function GetRole(ByVal RoleID As Integer, ByVal PortalID As Integer) As RoleInfo
   *     Return provider.GetRole(PortalID, RoleID)
   * End Function
   * ```
   *
   * Portal context is now derived from JWT token in the backend.
   *
   * @param id - Role ID to retrieve
   * @returns Observable of Role
   */
  getRole(id: number): Observable<Role> {
    return this.http.get<Role>(this.buildUrl(`${this.endpoint}/${id}`));
  }

  /**
   * Get a role by name within a portal.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 179-181):
   * ```vb
   * Public Function GetRoleByName(ByVal PortalId As Integer, ByVal RoleName As String) As RoleInfo
   *     Return provider.GetRole(PortalId, RoleName)
   * End Function
   * ```
   *
   * @param portalId - Portal ID to search within
   * @param roleName - Role name to find
   * @returns Observable of Role or null if not found
   */
  getRoleByName(portalId: number, roleName: string): Observable<Role | null> {
    const params = this.buildParams({ portalId, roleName });

    return this.http.get<Role>(
      this.buildUrl(`${this.endpoint}/by-name`),
      { params }
    ).pipe(
      catchError(() => of(null))
    );
  }

  /**
   * Create a new role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 100-112):
   * ```vb
   * Public Function AddRole(ByVal objRoleInfo As RoleInfo) As Integer
   *     Dim roleId As Integer = -1
   *     Dim success As Boolean = provider.CreateRole(objRoleInfo.PortalID, objRoleInfo)
   *     If success Then
   *         AutoAssignUsers(objRoleInfo)
   *         roleId = objRoleInfo.RoleID
   *     End If
   *     Return roleId
   * End Function
   * ```
   *
   * Auto-assignment logic is handled by the backend API.
   *
   * @param request - Role creation request DTO
   * @returns Observable of created Role with assigned ID
   */
  createRole(request: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(this.buildUrl(this.endpoint), request);
  }

  /**
   * Update an existing role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 254-257):
   * ```vb
   * Public Sub UpdateRole(ByVal objRoleInfo As RoleInfo)
   *     provider.UpdateRole(objRoleInfo)
   *     AutoAssignUsers(objRoleInfo)
   * End Sub
   * ```
   *
   * Auto-assignment logic is handled by the backend API.
   *
   * @param id - Role ID to update
   * @param request - Role update request DTO
   * @returns Observable of updated Role
   */
  updateRole(id: number, request: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(this.buildUrl(`${this.endpoint}/${id}`), request);
  }

  /**
   * Delete a role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 125-133):
   * ```vb
   * Public Sub DeleteRole(ByVal RoleId As Integer, ByVal PortalId As Integer)
   *     Dim objRole As RoleInfo = GetRole(RoleId, PortalId)
   *     If Not objRole Is Nothing Then
   *         provider.DeleteRole(PortalId, objRole)
   *     End If
   * End Sub
   * ```
   *
   * @param id - Role ID to delete
   * @returns Observable of void
   */
  deleteRole(id: number): Observable<void> {
    return this.http.delete<void>(this.buildUrl(`${this.endpoint}/${id}`));
  }

  // ==========================================================================
  // ROLE GROUP METHODS
  // Derived from RoleController.vb lines 626-842
  // ==========================================================================

  /**
   * Get all role groups for a portal.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 825-827):
   * ```vb
   * Public Shared Function GetRoleGroups(ByVal PortalID As Integer) As ArrayList
   *     Return provider.GetRoleGroups(PortalID)
   * End Function
   * ```
   *
   * @param portalId - Optional portal ID filter
   * @returns Observable of RoleGroup array
   */
  getRoleGroups(portalId?: number): Observable<RoleGroup[]> {
    const params = this.buildParams({ portalId });
    return this.http.get<RoleGroup[]>(
      this.buildUrl(this.groupsEndpoint),
      { params }
    );
  }

  /**
   * Get a single role group by ID.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 811-813):
   * ```vb
   * Public Shared Function GetRoleGroup(ByVal PortalID As Integer,
   *     ByVal RoleGroupID As Integer) As RoleGroupInfo
   *     Return provider.GetRoleGroup(PortalID, RoleGroupID)
   * End Function
   * ```
   *
   * @param id - Role group ID to retrieve
   * @returns Observable of RoleGroup
   */
  getRoleGroup(id: number): Observable<RoleGroup> {
    return this.http.get<RoleGroup>(this.buildUrl(`${this.groupsEndpoint}/${id}`));
  }

  /**
   * Create a new role group.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 626-630):
   * ```vb
   * Public Shared Function AddRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo) As Integer
   *     Return provider.CreateRoleGroup(objRoleGroupInfo)
   * End Function
   * ```
   *
   * @param request - Role group creation request DTO
   * @returns Observable of created RoleGroup with assigned ID
   */
  createRoleGroup(request: CreateRoleGroupRequest): Observable<RoleGroup> {
    return this.http.post<RoleGroup>(this.buildUrl(this.groupsEndpoint), request);
  }

  /**
   * Update an existing role group.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 838-842):
   * ```vb
   * Public Shared Sub UpdateRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo)
   *     provider.UpdateRoleGroup(objRoleGroupInfo)
   * End Sub
   * ```
   *
   * @param id - Role group ID to update
   * @param request - Role group update request DTO
   * @returns Observable of updated RoleGroup
   */
  updateRoleGroup(id: number, request: UpdateRoleGroupRequest): Observable<RoleGroup> {
    return this.http.put<RoleGroup>(
      this.buildUrl(`${this.groupsEndpoint}/${id}`),
      request
    );
  }

  /**
   * Delete a role group.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 779-783):
   * ```vb
   * Public Shared Sub DeleteRoleGroup(ByVal PortalID As Integer, ByVal RoleGroupId As Integer)
   *     DeleteRoleGroup(GetRoleGroup(PortalID, RoleGroupId))
   * End Sub
   * ```
   *
   * @param id - Role group ID to delete
   * @returns Observable of void
   */
  deleteRoleGroup(id: number): Observable<void> {
    return this.http.delete<void>(this.buildUrl(`${this.groupsEndpoint}/${id}`));
  }

  // ==========================================================================
  // USER-ROLE ASSIGNMENT METHODS
  // Derived from RoleController.vb lines 261-558
  // ==========================================================================

  /**
   * Get all users assigned to a specific role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 457-459):
   * ```vb
   * Public Function GetUsersByRoleName(ByVal PortalID As Integer,
   *     ByVal RoleName As String) As ArrayList
   *     Return provider.GetUsersByRoleName(PortalID, RoleName)
   * End Function
   * ```
   *
   * The modern API uses role ID instead of role name for better performance.
   *
   * @param roleId - Role ID to get users for
   * @returns Observable of User array (uses User from core/models/user.model.ts)
   */
  getUsersInRole(roleId: number): Observable<User[]> {
    return this.http.get<User[]>(this.buildUrl(`${this.endpoint}/${roleId}/users`));
  }

  /**
   * Get all roles assigned to a specific user.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 240-242):
   * ```vb
   * Public Function GetRolesByUser(ByVal UserId As Integer,
   *     ByVal PortalId As Integer) As String()
   *     Return provider.GetRoleNames(PortalId, UserId)
   * End Function
   * ```
   *
   * Returns full Role objects instead of just role names for richer data.
   *
   * @param userId - User ID to get roles for
   * @returns Observable of Role array
   */
  getRolesByUser(userId: number): Observable<Role[]> {
    return this.http.get<Role[]>(this.buildUrl(`/users/${userId}/roles`));
  }

  /**
   * Get user-role assignment details.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 362-364):
   * ```vb
   * Public Function GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer,
   *     ByVal RoleId As Integer) As UserRoleInfo
   *     Return provider.GetUserRole(PortalID, UserId, RoleId)
   * End Function
   * ```
   *
   * Returns null if the user is not assigned to the role.
   *
   * @param userId - User ID
   * @param roleId - Role ID
   * @returns Observable of UserRole or null if not assigned
   */
  getUserRole(userId: number, roleId: number): Observable<UserRole | null> {
    return this.http.get<UserRole>(
      this.buildUrl(`${this.endpoint}/${roleId}/users/${userId}`)
    ).pipe(
      catchError(() => of(null))
    );
  }

  /**
   * Add a user to a role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 295-315):
   * ```vb
   * Public Sub AddUserRole(ByVal PortalID As Integer, ByVal UserId As Integer,
   *     ByVal RoleId As Integer, ByVal EffectiveDate As Date, ByVal ExpiryDate As Date)
   *     Dim objUser As UserInfo = UserController.GetUser(PortalID, UserId, False)
   *     Dim objUserRole As UserRoleInfo = GetUserRole(PortalID, UserId, RoleId)
   *     If objUserRole Is Nothing Then
   *         'Create new UserRole
   *         objUserRole = New UserRoleInfo
   *         objUserRole.UserID = UserId
   *         objUserRole.RoleID = RoleId
   *         objUserRole.PortalID = PortalID
   *         objUserRole.EffectiveDate = EffectiveDate
   *         objUserRole.ExpiryDate = ExpiryDate
   *         provider.AddUserToRole(PortalID, objUser, objUserRole)
   *     Else
   *         objUserRole.EffectiveDate = EffectiveDate
   *         objUserRole.ExpiryDate = ExpiryDate
   *         provider.UpdateUserRole(objUserRole)
   *     End If
   * End Sub
   * ```
   *
   * The backend handles the create-or-update logic internally.
   *
   * @param userId - User ID to add to role
   * @param roleId - Role ID to add user to
   * @param request - Optional request with effective/expiry dates
   * @returns Observable of void
   */
  addUserToRole(
    userId: number,
    roleId: number,
    request?: AddUserRoleRequest
  ): Observable<void> {
    return this.http.post<void>(
      this.buildUrl(`${this.endpoint}/${roleId}/users/${userId}`),
      request ?? {}
    );
  }

  /**
   * Remove a user from a role.
   *
   * MIGRATION: Replaces VB.NET method (RoleController.vb lines 330-347):
   * ```vb
   * Public Function DeleteUserRole(ByVal PortalId As Integer, ByVal UserId As Integer,
   *     ByVal RoleId As Integer) As Boolean
   *     Dim objUser As UserInfo = UserController.GetUser(PortalId, UserId, False)
   *     Dim objUserRole As UserRoleInfo = GetUserRole(PortalId, UserId, RoleId)
   *     Dim objPortals As New PortalController
   *     Dim blnDelete As Boolean = True
   *     Dim objPortal As PortalInfo = objPortals.GetPortal(PortalId)
   *     If Not (objPortal Is Nothing OrElse objUserRole Is Nothing) Then
   *         If CanRemoveUserFromRole(objPortal, UserId, RoleId) Then
   *             provider.RemoveUserFromRole(PortalId, objUser, objUserRole)
   *         Else
   *             blnDelete = False
   *         End If
   *     End If
   *     Return blnDelete
   * End Function
   * ```
   *
   * The CanRemoveUserFromRole validation is handled by the backend.
   *
   * @param userId - User ID to remove from role
   * @param roleId - Role ID to remove user from
   * @returns Observable of void
   */
  removeUserFromRole(userId: number, roleId: number): Observable<void> {
    return this.http.delete<void>(
      this.buildUrl(`${this.endpoint}/${roleId}/users/${userId}`)
    );
  }

  // ==========================================================================
  // PRIVATE HELPER METHODS
  // ==========================================================================

  /**
   * Build HttpParams from a filter object, excluding undefined/null values.
   *
   * @param filters - Object with filter key-value pairs
   * @returns HttpParams instance with non-null values
   */
  private buildParams(filters: Record<string, unknown>): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null) {
        params = params.set(key, String(value));
      }
    }

    return params;
  }

  /**
   * Build full API URL from a path segment.
   *
   * @param path - API endpoint path (e.g., '/roles', '/roles/1')
   * @returns Full URL (e.g., 'http://localhost:5000/api/roles')
   */
  private buildUrl(path: string): string {
    return `${this.apiUrl}${path}`;
  }
}
