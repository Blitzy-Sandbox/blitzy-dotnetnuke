/**
 * MIGRATION: RoleService
 * 
 * Angular 19 service for role management API communication with the ASP.NET Core backend.
 * Replaces DNN RoleController.vb business logic with REST API calls.
 * 
 * MIGRATION SOURCE: Library/Components/Security/Roles/RoleController.vb
 * 
 * Key transformations:
 * - VB.NET DataProvider.Instance() pattern replaced with HttpClient
 * - VB.NET ArrayList returns converted to typed arrays
 * - VB.NET Null.NullDate converted to null or undefined
 * - VB.NET ByRef parameters eliminated (responses contain all data)
 * - Portal context derived from JWT token in backend, not passed explicitly
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../../../environments/environment';
import { Role, RoleGroup, CreateRoleRequest, UpdateRoleRequest } from '../models/role.model';

/**
 * Interface for paginated API responses.
 * MIGRATION: Replaces VB.NET ArrayList + ByRef totalRecords pattern.
 */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Interface for adding user to role request.
 * MIGRATION: Derived from RoleController.AddUserRole parameters (lines 295-315).
 */
export interface AddUserRoleRequest {
  effectiveDate?: Date | string;
  expiryDate?: Date | string;
}

/**
 * Interface for creating a role group.
 * MIGRATION: Derived from RoleController.AddRoleGroup parameters (lines 626-630).
 */
export interface CreateRoleGroupRequest {
  roleGroupName: string;
  description?: string;
}

/**
 * Interface for updating a role group.
 */
export interface UpdateRoleGroupRequest {
  roleGroupName: string;
  description?: string;
}

/**
 * Interface for user-role relationship.
 * MIGRATION: Derived from UserRoleInfo properties.
 */
export interface UserRole {
  userRoleId: number;
  userId: number;
  roleId: number;
  effectiveDate: Date | null;
  expiryDate: Date | null;
  isTrialUsed: boolean;
}

/**
 * Interface for filtering roles.
 */
export interface RoleFilter {
  portalId?: number;
  roleGroupId?: number;
  pageIndex?: number;
  pageSize?: number;
}

/**
 * User interface (simplified for role service needs).
 * MIGRATION: Derived from UserInfo properties used in role assignments.
 */
export interface User {
  userId: number;
  username: string;
  displayName: string;
  email: string;
}

/**
 * RoleService
 * 
 * Angular 19 service providing typed role-specific operations:
 * - Role CRUD operations
 * - Role group operations
 * - User-role assignment management
 * 
 * Uses HttpClient via inject() function per Angular 19 standards
 * and RxJS Observables for reactive API communication.
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  // ============================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() function)
  // ============================================================================

  private readonly http = inject(HttpClient);

  // ============================================================================
  // CONFIGURATION
  // ============================================================================

  private readonly apiUrl = environment.apiUrl;
  private readonly endpoint = '/roles';
  private readonly groupsEndpoint = '/roles/groups';

  // ============================================================================
  // ROLE CRUD METHODS (derived from RoleController.vb lines 100-258)
  // ============================================================================

  /**
   * Get paginated list of roles with optional filtering.
   * MIGRATION: Replaces GetPortalRoles() and GetRolesByGroup() (lines 191-242).
   * 
   * @param portalId Optional portal ID filter
   * @param roleGroupId Optional role group ID filter (-2 for all, -1 for global)
   * @param pageIndex Page number (0-based)
   * @param pageSize Items per page
   * @returns Observable of paginated role results
   */
  getRoles(
    portalId?: number,
    roleGroupId?: number,
    pageIndex: number = 0,
    pageSize: number = 10
  ): Observable<PagedResult<Role>> {
    let params = new HttpParams()
      .set('pageIndex', pageIndex.toString())
      .set('pageSize', pageSize.toString());

    if (portalId !== undefined) {
      params = params.set('portalId', portalId.toString());
    }

    if (roleGroupId !== undefined) {
      params = params.set('roleGroupId', roleGroupId.toString());
    }

    return this.http.get<PagedResult<Role>>(`${this.apiUrl}${this.endpoint}`, { params });
  }

  /**
   * Get a single role by ID.
   * MIGRATION: Replaces GetRole(ByVal RoleID As Integer, ByVal PortalID As Integer) (lines 163-165).
   * 
   * @param id Role ID
   * @returns Observable of Role
   */
  getRole(id: number): Observable<Role> {
    return this.http.get<Role>(`${this.apiUrl}${this.endpoint}/${id}`);
  }

  /**
   * Get a role by name.
   * MIGRATION: Replaces GetRoleByName() (lines 179-181).
   * 
   * @param portalId Portal ID
   * @param roleName Role name
   * @returns Observable of Role or null if not found
   */
  getRoleByName(portalId: number, roleName: string): Observable<Role | null> {
    const params = new HttpParams()
      .set('portalId', portalId.toString())
      .set('roleName', roleName);

    return this.http.get<Role>(`${this.apiUrl}${this.endpoint}/by-name`, { params }).pipe(
      catchError(() => of(null))
    );
  }

  /**
   * Create a new role.
   * MIGRATION: Replaces AddRole(ByVal objRoleInfo As RoleInfo) (lines 100-112).
   * 
   * @param request Role creation request
   * @returns Observable of created Role
   */
  createRole(request: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(`${this.apiUrl}${this.endpoint}`, request);
  }

  /**
   * Update an existing role.
   * MIGRATION: Replaces UpdateRole(ByVal objRoleInfo As RoleInfo) (lines 254-257).
   * 
   * @param id Role ID
   * @param request Role update request
   * @returns Observable of updated Role
   */
  updateRole(id: number, request: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.apiUrl}${this.endpoint}/${id}`, request);
  }

  /**
   * Delete a role.
   * MIGRATION: Replaces DeleteRole(ByVal RoleId As Integer, ByVal PortalId As Integer) (lines 125-133).
   * 
   * @param id Role ID
   * @returns Observable of void
   */
  deleteRole(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}${this.endpoint}/${id}`);
  }

  // ============================================================================
  // ROLE GROUP METHODS (derived from RoleController.vb lines 626-842)
  // ============================================================================

  /**
   * Get all role groups for a portal.
   * MIGRATION: Replaces GetRoleGroups(ByVal PortalID As Integer) (lines 825-827).
   * 
   * @param portalId Optional portal ID filter
   * @returns Observable of RoleGroup array
   */
  getRoleGroups(portalId?: number): Observable<RoleGroup[]> {
    let params = new HttpParams();
    if (portalId !== undefined) {
      params = params.set('portalId', portalId.toString());
    }

    return this.http.get<RoleGroup[]>(`${this.apiUrl}${this.groupsEndpoint}`, { params });
  }

  /**
   * Get a single role group by ID.
   * MIGRATION: Replaces GetRoleGroup(ByVal PortalID As Integer, ByVal RoleGroupID As Integer) (lines 811-813).
   * 
   * @param id Role group ID
   * @returns Observable of RoleGroup
   */
  getRoleGroup(id: number): Observable<RoleGroup> {
    return this.http.get<RoleGroup>(`${this.apiUrl}${this.groupsEndpoint}/${id}`);
  }

  /**
   * Create a new role group.
   * MIGRATION: Replaces AddRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo) (lines 626-630).
   * 
   * @param request Role group creation request
   * @returns Observable of created RoleGroup
   */
  createRoleGroup(request: CreateRoleGroupRequest): Observable<RoleGroup> {
    return this.http.post<RoleGroup>(`${this.apiUrl}${this.groupsEndpoint}`, request);
  }

  /**
   * Update an existing role group.
   * MIGRATION: Replaces UpdateRoleGroup(ByVal objRoleGroupInfo As RoleGroupInfo) (lines 838-842).
   * 
   * @param id Role group ID
   * @param request Role group update request
   * @returns Observable of updated RoleGroup
   */
  updateRoleGroup(id: number, request: UpdateRoleGroupRequest): Observable<RoleGroup> {
    return this.http.put<RoleGroup>(`${this.apiUrl}${this.groupsEndpoint}/${id}`, request);
  }

  /**
   * Delete a role group.
   * MIGRATION: Replaces DeleteRoleGroup(ByVal PortalID As Integer, ByVal RoleGroupId As Integer) (lines 779-783).
   * 
   * @param id Role group ID
   * @returns Observable of void
   */
  deleteRoleGroup(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}${this.groupsEndpoint}/${id}`);
  }

  // ============================================================================
  // USER-ROLE ASSIGNMENT METHODS (derived from RoleController.vb lines 261-558)
  // ============================================================================

  /**
   * Get all users in a specific role.
   * MIGRATION: Replaces GetUsersByRoleName(ByVal PortalID As Integer, ByVal RoleName As String) (lines 457-459).
   * 
   * @param roleId Role ID
   * @returns Observable of User array
   */
  getUsersInRole(roleId: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.apiUrl}${this.endpoint}/${roleId}/users`);
  }

  /**
   * Get all roles for a specific user.
   * MIGRATION: Replaces GetRolesByUser(ByVal UserId As Integer, ByVal PortalId As Integer) (lines 240-242).
   * 
   * @param userId User ID
   * @returns Observable of Role array
   */
  getRolesByUser(userId: number): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.apiUrl}/users/${userId}/roles`);
  }

  /**
   * Get user-role assignment details.
   * MIGRATION: Replaces GetUserRole(ByVal PortalID As Integer, ByVal UserId As Integer, ByVal RoleId As Integer) (lines 362-364).
   * 
   * @param userId User ID
   * @param roleId Role ID
   * @returns Observable of UserRole or null
   */
  getUserRole(userId: number, roleId: number): Observable<UserRole | null> {
    return this.http.get<UserRole>(`${this.apiUrl}${this.endpoint}/${roleId}/users/${userId}`).pipe(
      catchError(() => of(null))
    );
  }

  /**
   * Add a user to a role.
   * MIGRATION: Replaces AddUserRole(ByVal PortalID, ByVal UserId, ByVal RoleId, ByVal EffectiveDate, ByVal ExpiryDate) (lines 295-315).
   * 
   * @param userId User ID
   * @param roleId Role ID
   * @param request Optional request with effective/expiry dates
   * @returns Observable of void
   */
  addUserToRole(userId: number, roleId: number, request?: AddUserRoleRequest): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}${this.endpoint}/${roleId}/users/${userId}`,
      request || {}
    );
  }

  /**
   * Remove a user from a role.
   * MIGRATION: Replaces DeleteUserRole(ByVal PortalId, ByVal UserId, ByVal RoleId) (lines 330-347).
   * 
   * @param userId User ID
   * @param roleId Role ID
   * @returns Observable of void
   */
  removeUserFromRole(userId: number, roleId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}${this.endpoint}/${roleId}/users/${userId}`);
  }

  // ============================================================================
  // HELPER METHODS
  // ============================================================================

  /**
   * Build HttpParams from a filter object.
   * @param filters Filter object with optional properties
   * @returns HttpParams instance
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
   * Build full API URL from path.
   * @param path API endpoint path
   * @returns Full URL
   */
  private buildUrl(path: string): string {
    return `${this.apiUrl}${path}`;
  }
}
