import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { RoleService } from '../role.service';
import { DEFAULT_LIST_PAGE_SIZE } from '../../../core/services/api.service';
import { Role } from '../../../core/models';
import {
  DataTableComponent,
  ColumnDef,
  RowAction,
  RowActionEvent,
  FilterChangeEvent,
  PageChangeEvent,
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

      <div class="role-list__table-wrap">
        <!-- MIGRATION (QA Issue 13): server-side pagination + search. Previously the roles list wired NO
             filterChange and sent NO query, so its search box filtered only the first client-side window and
             a newly created role beyond it (the QA "role 2009") was undiscoverable. It now sends
             page/pageSize/query to the backend (RolesController now accepts ?query=) and consumes
             meta.totalCount for the pager, so every role is reachable by paging or a server-side search. -->
        <app-data-table
          [data]="roles()"
          [columns]="columns"
          [actions]="actions"
          [loading]="loading()"
          [rowKey]="'roleID'"
          [serverSide]="true"
          [page]="page()"
          [totalItems]="totalCount()"
          [pageSize]="pageSize"
          caption="Security Roles"
          emptyMessage="No roles found."
          (rowAction)="onRowAction($event)"
          (filterChange)="onFilter($event)"
          (pageChange)="onPageChange($event)" />
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
  /**
   * Total roles matching the current query across ALL server pages (meta.totalCount).
   * MIGRATION (QA Issue 13): passed to the data-table as `totalItems` to drive server-side pagination so
   * every role is reachable, not just the first client-side window.
   */
  readonly totalCount = signal<number>(0);
  /** Current 1-based page requested from the server (server-side pagination). */
  readonly page = signal<number>(1);
  /** Free-text search term sent to the server (server-side search). */
  readonly query = signal<string>('');
  /** Rows requested per server page. */
  readonly pageSize = DEFAULT_LIST_PAGE_SIZE;
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
  // MIGRATION (QA Issue 13): fetch ONE server page via getRolesWithMeta, sending the current page, per-page
  // size, and free-text query. meta.totalCount feeds the data-table pager. This replaces the old first-window
  // + client-only search (which never issued a backend query on filter, so a role beyond the window could
  // not be found) — RolesController now accepts ?query= and this component drives page/pageSize/query.
  loadRoles(): void {
    this.loading.set(true);
    this.error.set(null);
    this.roleService
      .getRolesWithMeta({ query: this.query(), page: this.page(), pageSize: this.pageSize })
      .subscribe({
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

  // MIGRATION (QA Issue 13): the data-table search box emits { term }; a new search resets to page 1 and
  // re-queries the server so the ENTIRE role set is searched (RolesController ?query=), not just the loaded
  // page. This is the wiring that was previously missing entirely on the roles list.
  onFilter(event: FilterChangeEvent): void {
    this.query.set(event.term);
    this.page.set(1);
    this.loadRoles();
  }

  // MIGRATION (QA Issue 13): Prev/Next emit the target page; update the page signal and re-query so roles
  // beyond the first page are reachable.
  onPageChange(event: PageChangeEvent): void {
    this.page.set(event.page);
    this.loadRoles();
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
        // R10 Issue 5 (consistency with module-list): after deletion, if the removed row was the
        // LAST row on the current page and we are beyond page 1, that page would now be empty; step
        // back one page so the user lands on a populated page instead of an empty "Page N of N-1".
        if (this.roles().length <= 1 && this.page() > 1) {
          this.page.set(this.page() - 1);
        }
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
