// MIGRATION: Signal-based Angular service for Role management. Re-expresses the data-access and
// orchestration of the legacy DotNetNuke Admin > Security controls — Website/admin/Security/Roles.ascx.vb
// (list + delete), EditRoles.ascx.vb (create + update), and SecurityRoles.ascx.vb (user-role assignment)
// — as a thin, signal-backed REST client over the shared core ApiService. All postback/ViewState/
// DataCache/RoleController/event-log machinery is discarded (those are backend concerns now). Per AAP
// Section 0.7.3, Angular services are restricted to API communication only; no UI and no business logic
// beyond request shaping plus local signal state. PortalId scoping is preserved (AAP Section 0.7.1):
// list() requires a portalId, matching the backend RolesController's required portalId query param.
import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, tap, catchError } from 'rxjs';

import { ApiService, type ApiQueryParams } from '../../core/services/api.service';
// MIGRATION: [QA F3 #4] toProblemDetails normalises a failed list GET into an RFC 7807 envelope for the banner.
import { toProblemDetails } from '../../core/services/problem-details.util';
import type { Role, UserRole, Paged, ProblemDetails } from '../../core/models';

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

// MIGRATION (CP-final review - role assignment workflow parity): the user-role ASSIGNMENT WRITE contract is now
// IMPLEMENTED end-to-end. The backend RolesController exposes the assignment sub-resource (POST /api/roles/assignments
// to assign [upsert], PUT /api/roles/assignments to recompute expiry / cancel, DELETE /api/roles/{roleId}/users/{userId}
// to remove), backed by IRoleService.AssignUserRoleAsync / UpdateUserRoleAsync / RemoveUserRoleAsync (ported VERBATIM
// from RoleController.vb AddUserRole / UpdateUserRole / DeleteUserRole + the CanRemoveUserFromRole guard). These three
// service methods re-expose that contract; the role-assignment feature calls them instead of showing a deferral notice.

/**
 * Request payload to assign a user to a role (POST /api/roles/assignments). PortalId travels IN THE BODY (the
 * backend EnforceTenant validates it against the JWT "portalId" claim), so no query param is sent.
 * MIGRATION: mirrors AssignUserRoleRequest (RoleController.AddUserRole arguments). EffectiveDate omitted => the
 * service applies the legacy DateTime.Now default; ExpiryDate omitted => no expiry (legacy Null.NullDate).
 */
export interface AssignUserRoleRequest {
  portalId: number;
  userId: number;
  roleId: number;
  effectiveDate?: string | null;
  expiryDate?: string | null;
}

/**
 * Request payload to update (recompute expiry) or cancel a user-role assignment (PUT /api/roles/assignments).
 * PortalId travels IN THE BODY. MIGRATION: mirrors UpdateUserRoleRequest (RoleController.UpdateUserRole arguments);
 * `cancel` maps to the legacy Cancel flag (expire-if-paid-and-trial-used, otherwise remove).
 */
export interface UpdateUserRoleRequest {
  portalId: number;
  userId: number;
  roleId: number;
  cancel: boolean;
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
  // MIGRATION: [QA F3 #4] last list() failure as an RFC 7807 ProblemDetails (null when the last load succeeded).
  // Replaces the previous silent fall-back to the empty state; RoleListComponent binds [error]="roleService.error()".
  private readonly errorSignal = signal<ProblemDetails | null>(null);

  /** The current page of roles. */
  readonly roles = this.rolesSignal.asReadonly();
  /** True while a fetch is in flight. */
  readonly loading = this.loadingSignal.asReadonly();
  /** Total role count across all pages (drives the data-table pager). */
  readonly totalCount = this.totalCountSignal.asReadonly();
  /** The role currently selected for view/edit. */
  readonly selected = this.selectedSignal.asReadonly();
  /** RFC 7807 problem describing the most recent failed `list()`, or null when it succeeded. */
  readonly error = this.errorSignal.asReadonly();

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
    // MIGRATION: [QA F3 #4] clear any prior error at the start of every load so a successful refresh removes the banner.
    this.errorSignal.set(null);
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
      // MIGRATION: [QA F3 #4] a failed roles list GET previously left the grid in the EMPTY state ("No roles found."),
      // hiding the failure. Capture the RFC 7807 envelope into errorSignal (drives the data-table danger banner),
      // clear the rows, and complete with a safe empty page so the stream does not error out. The tap error handler
      // above has already cleared the loading flag; the interceptor still logs the underlying error.
      catchError((err: unknown) => {
        this.errorSignal.set(toProblemDetails(err));
        this.rolesSignal.set([]);
        this.totalCountSignal.set(0);
        return of<Paged<Role>>({
          items: [],
          totalCount: 0,
          pageIndex,
          pageSize,
          totalPages: 0,
          hasPreviousPage: false,
          hasNextPage: false,
        });
      }),
    );
  }

  /**
   * Fetch a single role by id and store it as the selected role.
   * MIGRATION: EditRoles.ascx.vb edit-mode load (RoleController.GetRole) that pre-populated the form.
   * The backend RolesController requires the tenant `portalId` query for this protected, tenant-scoped
   * read (GET /api/roles/{id}?portalId=, [FromQuery, BindRequired], AAP Section 0.7.1).
   */
  getById(id: number, portalId: number): Observable<Role> {
    this.loadingSignal.set(true);
    return this.api.get<Role>(`${this.resource}/${id}`, { portalId }).pipe(
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
   * ROLE_UPDATED event-log write and the "GetRoles" cache clear are backend concerns now. The backend
   * requires the tenant `portalId` query for this protected, tenant-scoped write
   * (PUT /api/roles/{id}?portalId=, AAP Section 0.7.1).
   */
  update(id: number, portalId: number, request: UpdateRoleRequest): Observable<Role> {
    return this.api.put<Role>(`${this.resource}/${id}`, request, { portalId });
  }

  /**
   * Delete a role (expects HTTP 204) and remove it from local state.
   * MIGRATION: EditRoles.ascx.vb cmdDelete_Click (RoleController.DeleteRole). Delete REQUIRES
   * confirmation at the component layer (ConfirmationDialogComponent), preserving the legacy
   * ClientAPI.AddButtonConfirm gate; system-role protection (Administrator / Registered Users) is
   * enforced at the component layer. The backend requires the tenant `portalId` query for this
   * protected, tenant-scoped delete (DELETE /api/roles/{id}?portalId=, AAP Section 0.7.1).
   */
  delete(id: number, portalId: number): Observable<void> {
    return this.api.delete(`${this.resource}/${id}`, { portalId }).pipe(
      tap(() => {
        this.rolesSignal.update((roles) => roles.filter((role) => role.roleId !== id));
        this.totalCountSignal.update((count) => Math.max(0, count - 1));
      }),
    );
  }

  /**
   * Read-only lookup of the role assignments for a user (UNPAGED).
   * MIGRATION: SecurityRoles.ascx.vb user-focused grid bind. Maps to the confirmed backend endpoint
   * GET /api/roles/user/{userId}?portalId=, which returns { data: UserRole[], meta: { count } }. The
   * backend marks `portalId` as a [FromQuery, BindRequired] tenant discriminator (AAP Section 0.7.1).
   */
  getUserRoles(userId: number, portalId: number): Observable<UserRole[]> {
    return this.api.get<UserRole[]>(`${this.resource}/user/${userId}`, { portalId });
  }

  /**
   * Assign a user to a role (upsert; expects HTTP 201).
   * MIGRATION: SecurityRoles.ascx.vb cmdAdd_Click -> RoleController.AddUserRole. The backend AssignUserRoleAsync
   * is an UPSERT (refreshes the dates when the (user, role) pair already exists, else inserts), matching the legacy
   * AddUserRole, so the component's "Add User" / "Update Role" affordance maps to this single call. PortalId is in
   * the request body (validated against the JWT "portalId" claim server-side).
   */
  assignUserRole(request: AssignUserRoleRequest): Observable<UserRole> {
    return this.api.post<UserRole>(`${this.resource}/assignments`, request);
  }

  /**
   * Update (recompute expiry) or cancel a user-role assignment (expects HTTP 200).
   * MIGRATION: RoleController.UpdateUserRole — recomputes ExpiryDate from the role's trial/billing schedule
   * (N/O/D/W/M/Y), or on `cancel` expires (paid + trial-used) / removes the assignment. PortalId is in the body.
   */
  updateUserRole(request: UpdateUserRoleRequest): Observable<UserRole> {
    return this.api.put<UserRole>(`${this.resource}/assignments`, request);
  }

  /**
   * Remove a user from a role (expects HTTP 204).
   * MIGRATION: SecurityRoles.ascx.vb grdUserRoles_Delete -> RoleController.DeleteUserRole + the
   * CanRemoveUserFromRole guard (the portal Administrator cannot be removed from the Administrator role — the
   * backend returns 400 with a clear message, which the component surfaces). The backend requires the tenant
   * `portalId` query for this protected, tenant-scoped delete (DELETE /api/roles/{roleId}/users/{userId}?portalId=).
   */
  removeUserRole(portalId: number, roleId: number, userId: number): Observable<void> {
    return this.api.delete(`${this.resource}/${roleId}/users/${userId}`, { portalId });
  }

  /** Clear the selected role (e.g. when leaving an edit form). */
  clearSelected(): void {
    this.selectedSignal.set(null);
  }
}
