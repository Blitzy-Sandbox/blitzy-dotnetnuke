import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from '../../../core/services/api.service';
import type { User } from '../../../core/models/user.model';
import type { AddUserRoleRequest, CreateRole, Role, UpdateRole } from '../models';

/**
 * RoleService — the single Angular data service for the Role/Security feature.
 *
 * All HTTP flows through the core `ApiService` (NEVER `HttpClient` directly); every
 * method returns a clean domain type (`Role`/`User`), unwrapping the backend
 * `{ data, meta }` envelope where applicable.
 *
 * Mirrors backend `RolesController` (`/api/v1/roles`) + `IRoleService`. Behavior
 * reference (read-only): legacy `Website/admin/Security/{Roles,EditRoles,SecurityRoles}.ascx.vb`.
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly api = inject(ApiService);

  /** Resource segment for all role endpoints (`/api/v1/roles`). */
  private readonly resource = 'roles';

  // --- Role CRUD (mirrors RolesController) ---

  /**
   * GET `/api/v1/roles?portalId={portalId}` — list roles for a portal.
   *
   * The list endpoint is UNPAGED: the wire shape is `{ data: Role[], meta: null }`;
   * `getList` tolerates the null `meta`, and we map to the `data` array.
   * `portalId` is REQUIRED — the backend returns 400 (Problem Details) if absent.
   * The caller supplies it from the authenticated user (`AuthService.currentUser()?.portalId`).
   */
  getRoles(portalId: number): Observable<Role[]> {
    return this.api
      .getList<Role>(this.api.resourceUrl(this.resource), { portalId })
      .pipe(map((response) => response.data));
  }

  /** GET `/api/v1/roles/{id}` — a single role. */
  getRole(id: number): Observable<Role> {
    return this.api.get<Role>(this.api.resourceUrl(this.resource, id));
  }

  /** POST `/api/v1/roles` — create a role (201). */
  createRole(dto: CreateRole): Observable<Role> {
    return this.api.post<Role>(this.api.resourceUrl(this.resource), dto);
  }

  /**
   * PUT `/api/v1/roles/{id}` — update a role (200).
   *
   * The backend guards that the path `id` equals the body `roleID` (else 400);
   * the caller MUST set `dto.roleID === id`. The DTO is forwarded verbatim.
   */
  updateRole(id: number, dto: UpdateRole): Observable<Role> {
    return this.api.put<Role>(this.api.resourceUrl(this.resource, id), dto);
  }

  /** DELETE `/api/v1/roles/{id}` — delete a role (204). */
  deleteRole(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }

  // --- Membership sub-resources (mirrors RolesController user-role routes) ---

  /** GET `/api/v1/roles/{roleId}/users` — users assigned to a role. */
  getUsersInRole(roleId: number): Observable<User[]> {
    const url = `${this.api.resourceUrl(this.resource, roleId)}/users`;
    return this.api.getList<User>(url).pipe(map((response) => response.data));
  }

  /** GET `/api/v1/roles/user/{userId}` — roles assigned to a user. */
  getUserRoles(userId: number): Observable<Role[]> {
    const url = `${this.api.resourceUrl(this.resource)}/user/${userId}`;
    return this.api.getList<Role>(url).pipe(map((response) => response.data));
  }

  /**
   * POST `/api/v1/roles/{roleId}/users/{userId}` — assign a user to a role (204).
   *
   * PATH-SEGMENT ORDER: `{roleId}` first, then `{userId}` (do NOT transpose).
   * MIGRATION (DEV-069 / Finding 5): the legacy `SecurityRoles.ascx.vb` admin workflow passed
   * EffectiveDate/ExpiryDate plus a "notify user" flag to `RoleController.AddUserRole`. The API now
   * accepts an OPTIONAL `AddUserRoleRequest` body: OMIT it for the prior subscription-style assignment
   * (the server computes ExpiryDate from the role's trial/billing schedule), or SUPPLY effective/expiry
   * dates for a direct operator-dated assignment. `notify` is a documented server-side NO-OP (no mail
   * subsystem in scope). When `request` is undefined NO body is sent, preserving the bodyless wire form
   * the backend's `EmptyBodyBehavior.Allow` accepts.
   */
  assignUserToRole(roleId: number, userId: number, request?: AddUserRoleRequest): Observable<void> {
    const url = `${this.api.resourceUrl(this.resource, roleId)}/users/${userId}`;
    return this.api.post<void>(url, request);
  }

  /**
   * DELETE `/api/v1/roles/{roleId}/users/{userId}` — remove a user from a role (204).
   *
   * PATH-SEGMENT ORDER: `{roleId}` first, then `{userId}` (do NOT transpose).
   * MIGRATION: legacy `RoleController.DeleteUserRole` passed a notify flag; the new
   * API takes NO body.
   */
  removeUserFromRole(roleId: number, userId: number): Observable<void> {
    const url = `${this.api.resourceUrl(this.resource, roleId)}/users/${userId}`;
    return this.api.delete(url);
  }
}
