// MIGRATION: Signal-based Angular service for Role management. Re-expresses the data-access and
// orchestration of the legacy DotNetNuke Admin > Security controls — Website/admin/Security/Roles.ascx.vb
// (list + delete), EditRoles.ascx.vb (create + update), and SecurityRoles.ascx.vb (user-role assignment)
// — as a thin, signal-backed REST client over the shared core ApiService. All postback/ViewState/
// DataCache/RoleController/event-log machinery is discarded (those are backend concerns now). Per AAP
// Section 0.7.3, Angular services are restricted to API communication only; no UI and no business logic
// beyond request shaping plus local signal state. PortalId scoping is preserved (AAP Section 0.7.1):
// list() requires a portalId, matching the backend RolesController's required portalId query param.
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { ApiService, type ApiQueryParams } from '../../core/services/api.service';
import type { Role, UserRole, Paged } from '../../core/models';

/**
 * Request payload to create a role. The server assigns `roleId`, so it is omitted from the body.
 * MIGRATION: mirrors the fields written by EditRoles.ascx.vb cmdUpdate_Click (add branch).
 */
export type CreateRoleRequest = Omit<Role, 'roleId'>;

/**
 * Request payload to update a role. `roleId` travels in the URL, not the body.
 * MIGRATION: mirrors EditRoles.ascx.vb cmdUpdate_Click (edit branch).
 */
export type UpdateRoleRequest = Omit<Role, 'roleId'>;

/**
 * Request payload to assign a user to a role.
 * MIGRATION: mirrors SecurityRoles.ascx.vb cmdAdd_Click -> AddUserRole(user, role, portalSettings,
 * effectiveDate, expiryDate, currentUserId, notify). `effectiveDate`/`expiryDate` are ISO date strings
 * (an empty value maps to the legacy null-date sentinel server-side); `notify` is the optional
 * notification flag (legacy chkNotify checkbox).
 */
export interface AssignUserRoleRequest {
  userId: number;
  roleId: number;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  notify?: boolean;
}

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly api = inject(ApiService);

  /** Relative resource path; ApiService roots it at environment.apiUrl. */
  private readonly resource = 'roles';

  // ----- Signal state -----
  private readonly rolesSignal = signal<Role[]>([]);
  private readonly loadingSignal = signal(false);
  private readonly totalCountSignal = signal(0);
  private readonly selectedSignal = signal<Role | null>(null);

  /** The current page of roles. */
  readonly roles = this.rolesSignal.asReadonly();
  /** True while a fetch is in flight. */
  readonly loading = this.loadingSignal.asReadonly();
  /** Total role count across all pages (drives the data-table pager). */
  readonly totalCount = this.totalCountSignal.asReadonly();
  /** The role currently selected for view/edit. */
  readonly selected = this.selectedSignal.asReadonly();

  /**
   * List roles for a portal (paged, zero-based).
   * MIGRATION: Website/admin/Security/Roles.ascx.vb BindData. The legacy control chose between
   * RoleController.GetPortalRoles(PortalId) (flat) and GetRolesByGroup(PortalId, RoleGroupId) (grouped)
   * based on the selected role group. PortalId scoping is PRESERVED (AAP Section 0.7.1) — the backend
   * RolesController requires portalId as a [FromQuery, BindRequired] param.
   */
  list(
    portalId: number,
    pageIndex = 0,
    pageSize = 20,
    filter?: string,
    roleGroupId?: number,
  ): Observable<Paged<Role>> {
    this.loadingSignal.set(true);
    const params: ApiQueryParams = { portalId, pageIndex, pageSize };
    // MIGRATION: `filter` (free text) and `roleGroupId` (legacy BindGroups dropdown, including the
    // AllRoles = -2 and GlobalRoles = -1 sentinels) are forwarded as extra query params. The backend
    // RolesController does NOT yet implement server-side text filtering or role-group grouping in this
    // phase; the sentinel semantics are preserved at the component layer, and these params are sent for
    // forward-compatibility (the API ignores unknown params). Tracked in MIGRATION_NOTES.md.
    if (filter !== undefined && filter !== '') {
      params['filter'] = filter;
    }
    if (roleGroupId !== undefined) {
      params['roleGroupId'] = roleGroupId;
    }
    return this.api.getPaged<Role>(this.resource, params).pipe(
      tap({
        next: (page) => {
          this.rolesSignal.set(page.items);
          this.totalCountSignal.set(page.totalCount);
          this.loadingSignal.set(false);
        },
        error: () => this.loadingSignal.set(false),
      }),
    );
  }

  /**
   * Fetch a single role by id and store it as the selected role.
   * MIGRATION: EditRoles.ascx.vb edit-mode load (RoleController.GetRole) that pre-populated the form.
   */
  getById(id: number): Observable<Role> {
    this.loadingSignal.set(true);
    return this.api.get<Role>(`${this.resource}/${id}`).pipe(
      tap({
        next: (role) => {
          this.selectedSignal.set(role);
          this.loadingSignal.set(false);
        },
        error: () => this.loadingSignal.set(false),
      }),
    );
  }

  /**
   * Create a role (expects HTTP 201).
   * MIGRATION: EditRoles.ascx.vb cmdUpdate_Click add branch (RoleController.AddRole). The legacy
   * duplicate-name check (GetRoleByName -> "DuplicateRole" error) and the ROLE_CREATED event-log write
   * are now backend concerns; duplicate errors surface via RFC 7807 validation on the roleName field.
   */
  create(request: CreateRoleRequest): Observable<Role> {
    return this.api.post<Role>(this.resource, request);
  }

  /**
   * Update a role (expects HTTP 200).
   * MIGRATION: EditRoles.ascx.vb cmdUpdate_Click edit branch (RoleController.UpdateRole). The
   * ROLE_UPDATED event-log write and the "GetRoles" cache clear are backend concerns now.
   */
  update(id: number, request: UpdateRoleRequest): Observable<Role> {
    return this.api.put<Role>(`${this.resource}/${id}`, request);
  }

  /**
   * Delete a role (expects HTTP 204) and remove it from local state.
   * MIGRATION: EditRoles.ascx.vb cmdDelete_Click (RoleController.DeleteRole). Delete REQUIRES
   * confirmation at the component layer (ConfirmationDialogComponent), preserving the legacy
   * ClientAPI.AddButtonConfirm gate; system-role protection (Administrator / Registered Users) is
   * enforced at the component layer.
   */
  delete(id: number): Observable<void> {
    return this.api.delete(`${this.resource}/${id}`).pipe(
      tap(() => {
        this.rolesSignal.update((roles) => roles.filter((role) => role.roleId !== id));
        this.totalCountSignal.update((count) => Math.max(0, count - 1));
      }),
    );
  }

  /**
   * Read-only lookup of the role assignments for a user (UNPAGED).
   * MIGRATION: SecurityRoles.ascx.vb user-focused grid bind. Maps to the confirmed backend endpoint
   * GET /api/roles/user/{userId}, which returns { data: UserRole[], meta: { count } }.
   */
  getUserRoles(userId: number): Observable<UserRole[]> {
    return this.api.get<UserRole[]>(`${this.resource}/user/${userId}`);
  }

  /**
   * Assign a user to a role.
   * MIGRATION: PROVISIONAL — SecurityRoles.ascx.vb cmdAdd_Click -> AddUserRole(...). The backend
   * RolesController in this phase exposes ONLY the read-only GET /api/roles/user/{userId} lookup; there
   * is NO user-role assignment WRITE endpoint, IRoleService write method, or DTO yet (explicitly
   * DEFERRED). This method targets a sensible REST sub-path (POST roles/assignments) so the
   * role-assignment feature compiles and is fully wired; it must be implemented backend-side before it
   * functions at runtime. Documented in MIGRATION_NOTES.md.
   */
  assignUserRole(request: AssignUserRoleRequest): Observable<UserRole> {
    return this.api.post<UserRole>(`${this.resource}/assignments`, request);
  }

  /**
   * Remove a user-role assignment.
   * MIGRATION: PROVISIONAL companion to assignUserRole — SecurityRoles.ascx.vb grdUserRoles_Delete,
   * which was permission-guarded by RoleController.CanRemoveUserFromRole. No backend DELETE assignment
   * endpoint exists yet (DEFERRED); see MIGRATION_NOTES.md. The permission check and delete confirmation
   * are enforced at the component layer.
   */
  removeUserRole(userRoleId: number): Observable<void> {
    return this.api.delete(`${this.resource}/assignments/${userRoleId}`);
  }

  /** Clear the selected role (e.g. when leaving an edit form). */
  clearSelected(): void {
    this.selectedSignal.set(null);
  }
}
