import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import type { Portal } from '../../models';
import { PortalService } from '../../services';
import type {
  ApiResponseMeta,
  ProblemDetails,
} from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  DataTableComponent,
  type DataTableAction,
  type DataTableActionEvent,
  type DataTableColumn,
  type DataTableSearch,
  type DataTableSort,
} from '../../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/** Filter token for the unpaged "show all portals" view. */
const FILTER_ALL = 'All';
/** Filter token for the client-side "expired portals" view. */
const FILTER_EXPIRED = 'Expired';
/** MIGRATION: legacy grid PageSize (Portals.ascx.vb L96). */
const PAGE_SIZE = 20;

/**
 * PortalListComponent - host administrative portals grid.
 *
 * MIGRATION: reinterprets the legacy Web Forms control
 * `Website/admin/Portal/Portals.ascx.vb` (DotNetNuke.Modules.Admin.Portals.Portals)
 * as a standalone Angular 19 screen with UI functional parity (AAP 0.3.4 / 0.7.1):
 * letter filter (A-Z) + All + Expired, server paging (PageSize=20), per-row delete
 * with a confirmation dialog, and the delete-current-portal guard. This component is
 * the DATA OWNER and re-queries PortalService on every grid event; the table,
 * confirmation dialog, loading spinner, and RBAC gating are delegated to shared blocks.
 */
@Component({
  selector: 'app-portal-list',
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './portal-list.component.html',
  styleUrl: './portal-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalListComponent implements OnInit {
  private readonly portalService = inject(PortalService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  /** Grid rows (current page for the letter filter; full set for All/Expired). */
  readonly rows = signal<Portal[]>([]);
  /** Pagination metadata; null for the unpaged All/Expired views (pager auto-hides). */
  readonly meta = signal<ApiResponseMeta | null>(null);
  /** True while a fetch or delete is in flight. */
  readonly loading = signal<boolean>(false);
  /** RFC 7807 error message surfaced as a banner (never swallowed). */
  readonly error = signal<string | null>(null);
  /** Success message surfaced after a delete (parity with legacy "PortalDeleted"). */
  readonly successMessage = signal<string | null>(null);
  /** Active filter token: 'All' | 'Expired' | a single letter A-Z. */
  readonly activeFilter = signal<string>(FILTER_ALL);
  /** Current 0-based page index (legacy CurrentPage - 1). */
  readonly pageIndex = signal<number>(0);
  /** Current sort descriptor (reserved; legacy grid has no column sorting). */
  readonly sort = signal<DataTableSort | null>(null);
  /** Whether the delete confirmation dialog is open. */
  readonly deleteDialogOpen = signal<boolean>(false);
  /** The portal pending deletion (set when the delete action fires). */
  readonly portalToDelete = signal<Portal | null>(null);

  /**
   * The active portal id (used to hide the delete action for the current portal).
   * MIGRATION: legacy grdPortals_ItemDataBound L423
   * (delImage.Visible = Not (portal.PortalID = PortalSettings.PortalId)).
   * MIGRATION: the client `User` wire-shape exposes the owning portal as `portalID`
   * (System.Text.Json camelCase preserves the trailing acronym; see core/models/user.model.ts),
   * so the active-portal id is read from `currentUser()?.portalID`.
   */
  private readonly currentPortalId = computed<number | null>(
    () => this.authService.currentUser()?.portalID ?? null,
  );

  /** Confirmation dialog message referencing the targeted portal name. */
  readonly deleteMessage = computed<string>(() => {
    const portal = this.portalToDelete();
    return portal
      ? `Are you sure you want to delete the portal "${portal.portalName ?? ''}"?`
      : 'Are you sure you want to delete this portal?';
  });

  /**
   * Filter bar tokens. MIGRATION: the shared DataTable DEFAULT_FILTERS is ['All','A'..'Z']
   * and OMITS 'Expired', so the legacy 'Expired' option (CreateLetterSearch L170-178)
   * is supplied explicitly here.
   */
  readonly filters: readonly string[] = [
    FILTER_ALL,
    FILTER_EXPIRED,
    ...Array.from({ length: 26 }, (_, index) => String.fromCharCode(65 + index)),
  ];

  /** Grid columns mirroring the legacy DataGrid. */
  readonly columns: DataTableColumn<Portal>[] = [
    { key: 'portalName', header: 'Portal Name' },
    { key: 'description', header: 'Description' },
    // MIGRATION: legacy FormatExpiryDate (L250-260) renders null dates as empty; DateFormatPipe handles null.
    { key: 'expiryDate', header: 'Expires', type: 'date' },
    { key: 'users', header: 'Users', type: 'number', align: 'right' },
    { key: 'pages', header: 'Pages', type: 'number', align: 'right' },
    { key: 'hostFee', header: 'Host Fee', type: 'number', align: 'right' },
  ];

  /** Per-row actions: edit -> /portals/:id/edit, settings -> /portals/:id/settings, delete -> confirm + DELETE. */
  readonly actions: DataTableAction<Portal>[] = [
    { id: 'edit', label: 'Edit', icon: 'edit', permission: 'EDIT' },
    { id: 'settings', label: 'Settings', icon: 'settings', permission: 'MANAGE_SETTINGS' },
    {
      id: 'delete',
      label: 'Delete',
      icon: 'delete',
      permission: 'DELETE',
      // MIGRATION: hide the delete action for the active portal (legacy L423).
      hidden: (row: Portal): boolean => row.portalID === this.currentPortalId(),
    },
  ];

  ngOnInit(): void {
    this.loadPortals();
  }

  /** Re-query PortalService based on the active filter, page index, and page size. */
  loadPortals(): void {
    this.loading.set(true);
    this.error.set(null);
    const filter = this.activeFilter();

    if (filter === FILTER_ALL) {
      // MIGRATION: legacy paged-All (GetPortalsByName("%", ...)) is served by an unpaged getAllPortals().
      this.portalService.getAllPortals().subscribe({
        next: (portals) => this.applyUnpaged(portals),
        error: (problem: ProblemDetails) => this.handleError(problem),
      });
      return;
    }

    if (filter === FILTER_EXPIRED) {
      // MIGRATION: backend exposes NO expired endpoint (legacy GetExpiredPortals/DeleteExpiredPortals
      // are not in the REST surface); filter client-side on expiryDate.
      this.portalService.getAllPortals().subscribe({
        next: (portals) => this.applyUnpaged(this.filterExpired(portals)),
        error: (problem: ProblemDetails) => this.handleError(problem),
      });
      return;
    }

    // Letter filter A-Z (paged). Legacy: GetPortalsByName(Filter + "%", CurrentPage - 1, PageSize).
    this.portalService.getPortals(filter, this.pageIndex(), PAGE_SIZE).subscribe({
      next: (response) => {
        this.rows.set(response.data);
        this.meta.set(response.meta);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /** DataTable filterChange: switch filter, reset to the first page, re-query. */
  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
    this.pageIndex.set(0);
    this.loadPortals();
  }

  /** DataTable pageChange (0-based): change page, re-query. */
  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
    this.loadPortals();
  }

  /** DataTable searchChange: treat the search text as the paged query (parity with the letter filter). */
  onSearchChange(search: DataTableSearch): void {
    this.activeFilter.set(search.text === '' ? FILTER_ALL : search.text);
    this.pageIndex.set(0);
    this.loadPortals();
  }

  /** DataTable sortChange: store the descriptor and re-query. */
  onSortChange(sort: DataTableSort): void {
    this.sort.set(sort);
    this.loadPortals();
  }

  /** DataTable actionClick: route edit/settings, or open the delete confirmation. */
  onActionClick(event: DataTableActionEvent<Portal>): void {
    switch (event.action.id) {
      case 'edit':
        void this.router.navigate(['/portals', event.row.portalID, 'edit']);
        break;
      case 'settings':
        void this.router.navigate(['/portals', event.row.portalID, 'settings']);
        break;
      case 'delete':
        this.portalToDelete.set(event.row);
        this.deleteDialogOpen.set(true);
        break;
    }
  }

  /** Confirmation dialog confirm: delete the targeted portal (204), then re-query. */
  onConfirmDelete(): void {
    const portal = this.portalToDelete();
    this.deleteDialogOpen.set(false);
    if (portal === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: legacy grdPortals_DeleteCommand (L388-409) -> DELETE /api/v1/portals/{id} (204) then re-bind.
    this.portalService.deletePortal(portal.portalID).subscribe({
      next: () => {
        this.successMessage.set(`Portal "${portal.portalName ?? ''}" was deleted.`);
        this.portalToDelete.set(null);
        this.loadPortals();
      },
      error: (problem: ProblemDetails) => {
        this.portalToDelete.set(null);
        this.handleDeleteError(problem);
      },
    });
  }

  /** Confirmation dialog cancel: close without deleting. */
  onCancelDelete(): void {
    this.deleteDialogOpen.set(false);
    this.portalToDelete.set(null);
  }

  /** Navigate to the create-portal screen. */
  onNew(): void {
    void this.router.navigate(['/portals/new']);
  }

  /** Dismiss the error banner. */
  dismissError(): void {
    this.error.set(null);
  }

  /** Dismiss the success banner. */
  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  private applyUnpaged(portals: Portal[]): void {
    this.rows.set(portals);
    this.meta.set(null);
    this.loading.set(false);
  }

  private filterExpired(portals: Portal[]): Portal[] {
    const now = Date.now();
    return portals.filter(
      (portal) => portal.expiryDate !== null && new Date(portal.expiryDate).getTime() < now,
    );
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'Failed to load portals.');
    this.rows.set([]);
    this.meta.set(null);
    this.loading.set(false);
  }

  // MIGRATION (QA Finding C): a FAILED delete must surface the error WITHOUT
  // destroying the displayed grid. The delete did not mutate anything, so the
  // records still exist; blanking the list to the "No portals found." empty-state
  // would misrepresent server state. Unlike handleError (the LOAD path, where
  // clearing the grid is the correct empty/error treatment), this delete-error
  // handler leaves the current rows/meta intact and only surfaces the banner.
  private handleDeleteError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'Failed to delete the portal.');
    this.loading.set(false);
  }
}
