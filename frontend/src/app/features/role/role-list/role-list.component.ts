// MIGRATION: Angular 19 standalone replacement for the legacy DNN admin roles grid
// (Website/admin/Security/Roles.ascx.vb [317] -- class Roles : PortalModuleBase, IActionable). The Web
// Forms DataGrid + ModuleActions + ViewState/postback + RoleController/DataCache machinery is discarded;
// only the list + delete (with system-role protection) behavior is re-expressed over the shared
// DataTableComponent. PortalId scoping is PRESERVED (AAP Section 0.7.1): list() carries the authenticated
// user's portalId, matching the backend RolesController required [FromQuery, BindRequired] portalId.
import { ChangeDetectionStrategy, Component, type OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../../core/auth/auth.service';
// MIGRATION: [QA F4-006] canonical RFC 7807 parser so a failed DELETE surfaces a friendly message (role-assignment
// gold-standard pattern) instead of being silently swallowed.
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import type { Role } from '../../../core/models';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import {
  DataTableComponent,
  type ColumnDef,
} from '../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { RoleService } from '../role.service';

// MIGRATION: legacy system roles (Administrators / Registered Users) could NOT be deleted -- the grid hid
// the delete icon for them (Roles.ascx.vb grdRoles_ItemDataBound, keyed off RoleController.vb
// L1390/L1393). The Role model carries no IsSystemRole flag, so detection uses the canonical DNN role
// names, consistent with features/role/role-form SYSTEM_ROLE_NAMES.
const SYSTEM_ROLE_NAMES: readonly string[] = ['Administrators', 'Registered Users'];

@Component({
  selector: 'app-role-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.scss',
})
export class RoleListComponent implements OnInit {
  // MIGRATION: components talk only to the feature service (never ApiService/HttpClient directly) and use
  // inject() DI (NOT constructor injection) -- Angular 19 conventions (AAP Section 0.7.3).
  protected readonly roleService = inject(RoleService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  // MIGRATION: standardized grid page size 20 (parity with the portal/user grids).
  protected readonly pageSize = 20;

  // MIGRATION: zero-based paging -- the shared DataTable emits/consumes zero-based page indexes (no +/-1
  // conversion, unlike the legacy 1-based CurrentPage).
  readonly currentPage = signal(0);

  // The row pending deletion drives the confirmation dialog (null = no dialog mounted).
  readonly pendingDelete = signal<Role | null>(null);

  // MIGRATION: [QA F4-006] friendly delete-failure message (role-assignment gold-standard pattern). The previous
  // error callback only cleared pendingDelete (closed the dialog) but surfaced NO message -- a silent swallow.
  // Now the error callback also sets this signal, rendered as a role="alert" banner. Null = no error.
  readonly actionError = signal<string | null>(null);

  // MIGRATION: [QA F4-014] in-flight guard for the confirmed delete; bound to the dialog's [busy] input and
  // consulted by onConfirmDelete so rapid repeated Confirm clicks fire exactly one DELETE.
  readonly deleting = signal<boolean>(false);

  // MIGRATION: canonical visible columns from the legacy roles grid (Roles.ascx.vb grdRoles): role name,
  // description, and the Public / Auto-assignment flags rendered through the shared yesNo display pipe.
  protected readonly columns: ColumnDef<Role>[] = [
    { key: 'roleName', header: 'Role Name' },
    { key: 'description', header: 'Description', truncate: 100 },
    { key: 'isPublic', header: 'Public', yesNo: true },
    { key: 'autoAssignment', header: 'Auto Assignment', yesNo: true },
  ];

  ngOnInit(): void {
    // MIGRATION: initial bind (Roles.ascx.vb Page_Load -> BindData).
    this.load();
  }

  // Zero-based throughout -- NO +/-1 conversion.
  protected onPageChange(pageIndex: number): void {
    this.currentPage.set(pageIndex);
    this.load();
  }

  // MIGRATION: legacy edit column navigated to EditRoles with the RoleID (Roles.ascx.vb).
  protected onEdit(role: Role): void {
    void this.router.navigate(['/roles', role.roleId, 'edit']);
  }

  // MIGRATION: legacy AddContent module action -> EditRoles in add mode (Roles.ascx.vb).
  protected onNew(): void {
    void this.router.navigate(['/roles', 'new']);
  }

  protected onDeleteRequest(role: Role): void {
    // MIGRATION: system-role protection (Roles.ascx.vb / RoleController.vb L1390/L1393). The shared
    // DataTable's showDelete is table-wide (no per-row hide), so system roles are short-circuited here
    // instead of being hidden at the column level.
    if (this.isSystemRole(role)) {
      // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #20] previously this was a SILENT no-op (bare `return`),
      // so clicking delete on a protected system role gave the user NO feedback. It now surfaces a clear,
      // user-facing reason through the existing actionError banner (role="alert") rather than silently doing
      // nothing, while still blocking the (disallowed) delete.
      this.actionError.set('System roles (Administrators and Registered Users) cannot be deleted.');
      return;
    }
    // MIGRATION: delete REQUIRES confirmation (legacy confirm() gate). Mounting the dialog via the
    // pendingDelete signal replaces the legacy client-side confirm() + server postback.
    // MIGRATION: [QA F4-006] clear any stale failure banner when opening a fresh delete dialog.
    this.actionError.set(null);
    this.pendingDelete.set(role);
  }

  protected onConfirmDelete(): void {
    const role = this.pendingDelete();
    if (role === null) {
      return;
    }
    // MIGRATION: [QA F4-014] re-entrancy guard -- ignore a confirm while a DELETE is already in flight so rapid
    // repeated Confirm clicks (the dialog stays mounted until the request resolves) fire exactly one request.
    if (this.deleting()) {
      return;
    }
    // MIGRATION: legacy grdRoles delete -> RoleController.DeleteRole + BindData refresh. The service
    // removes the row from local state on success; we clear the dialog and reload the current page.
    // MIGRATION: PortalId scoping (AAP Section 0.7.1) -- the backend RolesController DELETE requires
    // portalId as a [FromQuery, BindRequired] param; it is sourced from the authenticated user's portal.
    // MIGRATION: [QA F4-006/F4-014] subscribe with next AND a message-surfacing error callback. deleting() gates
    // the dialog's [busy] input. On success: close the dialog + reload. On failure: close the dialog, clear busy,
    // and surface a friendly RFC 7807 message (was a silent swallow). The error is HANDLED, not thrown globally.
    this.actionError.set(null);
    this.deleting.set(true);
    const portalId = this.auth.currentUser()?.portalId ?? 0;
    this.roleService.delete(role.roleId, portalId).subscribe({
      next: () => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.actionError.set(this.firstMessage(err, 'The role could not be deleted. Please try again.'));
      },
    });
  }

  protected onCancelDelete(): void {
    this.pendingDelete.set(null);
  }

  // MIGRATION: [QA F4-006] first user-facing message from a backend RFC 7807 failure (reuses the canonical
  // interceptor parser); falls back to the supplied default when the body carries no message (e.g. a status-0
  // transport failure whose err.error is a ProgressEvent). Mirrors role-assignment's firstMessage helper.
  private firstMessage(error: HttpErrorResponse, fallback: string): string {
    const parsed = parseProblemDetails(error.error);
    return parsed.messages.length > 0 ? parsed.messages[0] : fallback;
  }

  protected isSystemRole(role: Role): boolean {
    return SYSTEM_ROLE_NAMES.includes(role.roleName);
  }

  private load(): void {
    // MIGRATION: PortalId scoping (AAP Section 0.7.1). The backend RolesController requires portalId as a
    // [FromQuery, BindRequired] param; it is sourced from the authenticated user's portal (currentUser).
    const portalId = this.auth.currentUser()?.portalId ?? 0;
    this.roleService.list(portalId, this.currentPage(), this.pageSize).subscribe();
  }
}
