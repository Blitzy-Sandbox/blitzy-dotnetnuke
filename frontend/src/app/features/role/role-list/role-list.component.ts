import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { RoleService } from '../role.service';
import { MAX_LIST_PAGE_SIZE } from '../../../core/services/api.service';
import { Role } from '../../../core/models';
import {
  DataTableComponent,
  ColumnDef,
  RowAction,
  RowActionEvent,
} from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * RoleListComponent — standalone Angular 19 list screen for security Roles.
 *
 * MIGRATION: replaces the legacy DotNetNuke Web Forms roles list
 * (Website/admin/Security/roles.ascx `grdRoles` DataGrid + Roles.ascx.vb code-behind).
 * The postback-bound server grid (BindData -> RoleController.GetPortalRoles(PortalId))
 * becomes a stateless client-side DataTableComponent fed by RoleService.getRoles() over
 * HTTP. ViewState/postback eliminated (AAP §0.6.3). Presentation + navigation +
 * API-orchestration only, NO business logic (AAP §0.7.1).
 *
 * MIGRATION: the legacy top role-group filter dropdown (cboRoleGroups: AllRoles=-2 /
 * GlobalRoles=-1 / portal groups; Roles.ascx.vb BindGroups) is OUT OF SCOPE — no
 * /api/rolegroups endpoint exists in the current API surface. Client-side filtering is
 * provided instead by DataTableComponent's built-in search box.
 * MIGRATION: the legacy "UserRoles" / Manage-Users grid command column (grdRoles
 * imagecommandcolumn CommandName="UserRoles") is OUT OF SCOPE — no role<->user
 * assignment endpoints exist in the current /api/roles surface.
 */
@Component({
  selector: 'app-role-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  template: `
    <div class="role-list">
      <div class="role-list__header">
        <h1 class="role-list__title">Security Roles</h1>
        <button type="button" class="role-list__add" (click)="onAddRole()" aria-label="Add role">
          Add Role
        </button>
      </div>

      @if (error(); as message) {
        <div class="role-list__error" role="alert">{{ message }}</div>
      }

      <!-- MIGRATION (R6 Issue 1): truncation hint when the server capped the result set. -->
      @if (truncationHint(); as hint) {
        <div class="role-list__truncation" role="status">{{ hint }}</div>
      }

      <div class="role-list__table-wrap">
        <app-data-table
          [data]="roles()"
          [columns]="columns"
          [actions]="actions"
          [loading]="loading()"
          [rowKey]="'roleID'"
          caption="Security Roles"
          emptyMessage="No roles found."
          (rowAction)="onRowAction($event)" />
        <app-loading-spinner [loading]="loading()" [overlay]="true" message="Loading roles…" />
      </div>

      <app-confirmation-dialog
        [(open)]="showDeleteDialog"
        title="Delete Role"
        [message]="deleteMessage()"
        confirmLabel="Delete"
        [danger]="true"
        (confirm)="confirmDelete()"
        (cancel)="cancelDelete()" />
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .role-list__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 1rem;
      }

      .role-list__title {
        margin: 0;
        font-size: 1.25rem;
        font-weight: 600;
      }

      .role-list__add {
        padding: 0.5rem 1rem;
        font: inherit;
        color: var(--color-primary-contrast);
        background: var(--color-primary);
        border: 1px solid transparent;
        border-radius: var(--radius);
        cursor: pointer;
      }

      .role-list__add:hover,
      .role-list__add:focus {
        background: var(--color-primary-hover);
      }

      .role-list__error {
        margin-bottom: 1rem;
        padding: 0.75rem 1rem;
        color: var(--color-danger);
        background: var(--color-danger-bg);
        border: 1px solid var(--color-danger);
        border-radius: var(--radius);
      }

      /* QA R6 Issue 1: informational (non-error) truncation banner. */
      .role-list__truncation {
        margin-bottom: 1rem;
        padding: 0.5rem 0.75rem;
        color: var(--color-text-muted, #4b5563);
        background: var(--color-surface-muted, #f9fafb);
        border: 1px solid var(--color-border, #e5e7eb);
        border-radius: var(--radius);
        font-size: 0.875rem;
      }

      .role-list__table-wrap {
        position: relative;
      }
    `,
  ],
})
export class RoleListComponent implements OnInit {
  private readonly roleService = inject(RoleService);
  private readonly router = inject(Router);

  // ---- State (Signals) — PUBLIC (spec reads/sets these) ----
  readonly roles = signal<Role[]>([]);
  /** Total roles matching the current query across ALL server pages (meta.totalCount) — R6 Issue 1. */
  readonly totalCount = signal<number>(0);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly showDeleteDialog = signal<boolean>(false);
  readonly roleToDelete = signal<Role | null>(null);

  /** Confirmation-dialog message; recomputes from the pending role. */
  readonly deleteMessage = computed(() => {
    const role = this.roleToDelete();
    return role
      ? `Are you sure you want to delete the role "${role.roleName}"? This action cannot be undone.`
      : '';
  });

  /**
   * MIGRATION (R6 Issue 1): truncation hint text, or `null` when the full result set is shown (server
   * bounds the response to at most {@link MAX_LIST_PAGE_SIZE} rows).
   */
  readonly truncationHint = computed<string | null>(() => {
    const loaded = this.roles().length;
    const total = this.totalCount();
    return total > loaded
      ? `Showing the first ${loaded} of ${total} roles. Refine your search to narrow the results.`
      : null;
  });

  // ---- Column definitions — order preserved 1:1 from legacy grdRoles (roles.ascx). ----
  // MIGRATION: grdRoles bound/template columns -> ColumnDef<Role>[]. The legacy Edit
  // imagecommandcolumn -> the 'edit' row action; the "UserRoles" command column is omitted
  // (out of scope, see class comment). Fee columns used FormatPrice("##0.00") -> type 'currency'
  // (DataTable renders 2-decimal fixed). Public/Auto used checked/unchecked.gif -> type 'boolean'.
  // NOTE: the element type is the mutable `ColumnDef<Role>[]` (not `readonly ColumnDef<Role>[]`)
  // to match DataTableComponent's `columns = input<ColumnDef<T>[]>()` write contract; a
  // ReadonlyArray is not assignable to that input under strictTemplates (TS4104). The `readonly`
  // field modifier still prevents reassignment and the array is never mutated here.
  readonly columns: ColumnDef<Role>[] = [
    { field: 'roleName', header: 'Name', sortable: true, type: 'text' },
    { field: 'description', header: 'Description', type: 'text', truncate: 100 },
    { field: 'serviceFee', header: 'Fee', type: 'currency' },
    // MIGRATION / QA F-F: disambiguate the billing vs. trial recurrence columns.
    // The grid previously showed two "Every" and two "Period" headers (billing
    // and trial share the same period/frequency shape), which is ambiguous. The
    // field->column mapping is unchanged; only the header labels are prefixed.
    { field: 'billingPeriod', header: 'Billing Every', type: 'number' },
    { field: 'billingFrequency', header: 'Billing Period', type: 'text' },
    { field: 'trialFee', header: 'Trial', type: 'currency' },
    { field: 'trialPeriod', header: 'Trial Every', type: 'number' },
    { field: 'trialFrequency', header: 'Trial Period', type: 'text' },
    { field: 'isPublic', header: 'Public', type: 'boolean' },
    { field: 'autoAssignment', header: 'Auto', type: 'boolean' },
  ];

  // ---- Row actions ----
  // NOTE: mutable `RowAction[]` element type (see the columns note) to match
  // DataTableComponent's `actions = input<RowAction[]>()` write contract.
  readonly actions: RowAction[] = [
    // MIGRATION / QA F-B: render label text (no `icon`) so the buttons are
    // visible and meet the WCAG 2.5.8 target size (see portal-list note).
    { action: 'edit', label: 'Edit', tooltip: 'Edit' },
    { action: 'delete', label: 'Delete', tooltip: 'Delete' },
  ];

  // MIGRATION: Roles.ascx.vb Page_Load (non-postback) -> BindGroups()/BindData() -> ngOnInit.
  ngOnInit(): void {
    this.loadRoles();
  }

  // MIGRATION: Roles.ascx.vb BindData() -> RoleController.GetPortalRoles(PortalId) -> single getRoles() call.
  // MIGRATION (R6 Issue 1): fetch the BOUNDED page via getRolesWithMeta with pageSize = MAX_LIST_PAGE_SIZE
  // (the server caps at that maximum) and read meta.totalCount so the truncation hint appears when the full
  // set exceeds the loaded rows. The client-side DataTable still filters/sorts/pages over the loaded page.
  loadRoles(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleService.getRolesWithMeta({ pageSize: MAX_LIST_PAGE_SIZE }).subscribe({
      next: (result) => {
        this.roles.set(result.data);
        this.totalCount.set(result.meta?.totalCount ?? result.data.length);
        this.loading.set(false);
      },
      error: (err) => {
        // err is the RFC 7807 ProblemDetails rethrown by ApiService; err?.title is a string.
        this.error.set(err?.title ?? 'Failed to load roles');
        this.loading.set(false);
      },
    });
  }

  onRowAction(event: RowActionEvent<Role>): void {
    if (event.action === 'edit') {
      // MIGRATION: legacy Edit imagecommandcolumn (EditUrl "RoleID") -> navigate to role-form edit mode.
      this.router.navigate(['/roles', event.row.roleID]);
    } else if (event.action === 'delete') {
      // MIGRATION: legacy ClientAPI.AddButtonConfirm("DeleteItem") -> ConfirmationDialogComponent.
      this.roleToDelete.set(event.row);
      this.showDeleteDialog.set(true);
    }
  }

  // MIGRATION: the DELETE is issued here after the user confirms; the confirmation dialog is
  // purely presentational. Maps to DELETE /api/roles/{id}.
  confirmDelete(): void {
    const role = this.roleToDelete();
    if (!role) {
      return;
    }
    this.roleService.deleteRole(role.roleID).subscribe({
      next: () => {
        this.roleToDelete.set(null);
        this.showDeleteDialog.set(false);
        this.loadRoles();
      },
      error: (err) => {
        this.error.set(err?.title ?? 'Delete failed');
        this.showDeleteDialog.set(false);
      },
    });
  }

  cancelDelete(): void {
    this.roleToDelete.set(null);
  }

  // MIGRATION: legacy ModuleActions "AddContent" (Add Role) EditUrl() -> navigate to role-form create mode.
  onAddRole(): void {
    this.router.navigate(['/roles', 'new']);
  }
}
