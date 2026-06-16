import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import type { Module } from '../../models';
import { VisibilityState } from '../../models';
import { ModuleService } from '../../services';
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

/** Filter token for the unfiltered "show all modules" view. */
const FILTER_ALL = 'All';
/** MIGRATION: legacy DNN admin grid PageSize (Portals.ascx.vb L96); shared across admin grids. */
const PAGE_SIZE = 20;

/**
 * ModuleListComponent - administrative modules grid.
 *
 * MIGRATION: reinterprets the legacy DotNetNuke module-administration workflow
 * (Library/Components/Modules/ModuleController.vb grid + the Web Forms controls under
 * Website/admin/Modules) as a standalone Angular 19 screen with UI functional parity
 * (AAP 0.3.4 / 0.7.1). Web Forms postback + ViewState + server-side DataGrid rebinding are
 * replaced by a stateless REST query (GET /api/v1/modules?portalId=) and signal-driven state; this
 * component is the DATA OWNER. It fetches the full active portal set ONCE (on init and after a
 * delete) and derives the grid's filter/search/sort/paging CLIENT-SIDE via computed signals — the
 * backend GET /api/v1/modules accepts only the portalId/tabId discriminator and does NOT honor
 * paging/filter/search/sort params, so re-querying per grid event would have no effect. The table,
 * confirmation dialog, loading spinner, and RBAC gating are delegated to shared standalone blocks.
 *
 * MIGRATION: the legacy grid is PORTAL-scoped (ModuleController.GetModules(PortalID), L915). The
 * Angular route carries no portalId parameter, so the current portal is taken from the authenticated
 * session (User.portalID) via AuthService.currentUser() - the established sibling pattern shared by
 * role-form and portal-settings. The service requires exactly one list discriminator (portalId|tabId)
 * or the backend returns 400; getModulesByPortal(portalId) always supplies portalId.
 *
 * MIGRATION: soft-delete is a SERVER concern. Legacy ModuleController.DeleteModule (L819) hard-deletes,
 * while DeleteTabModule (L837) soft-deletes (IsDeleted = True, TabID = NullInteger, L851-L852) when the
 * module is on no other tab, and the legacy GetModules hydrated IsDeleted (L86, returning soft-deleted
 * rows). In the rewrite the grid only ever receives active (IsDeleted = false) modules - the server
 * applies the filter - so there is NO client-side isDeleted filtering. The client issues a single
 * DELETE /api/v1/modules/{id} (204) and the server decides hard vs. soft.
 */
@Component({
  selector: 'app-module-list',
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './module-list.component.html',
  styleUrl: './module-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ModuleListComponent implements OnInit {
  private readonly moduleService = inject(ModuleService);
  private readonly router = inject(Router);
  // MIGRATION: the modules grid is portal-scoped; the current portal id is read from the authenticated
  // user (User.portalID) exactly as role-form/portal-settings do. This is UI scoping only - the server
  // remains the authoritative scope/authorization boundary.
  private readonly auth = inject(AuthService);

  /**
   * The full set of ACTIVE modules for the current portal, returned by the API in a single
   * response. MIGRATION: GET /api/v1/modules accepts only the portalId/tabId discriminator and
   * does NOT page/filter/search/sort, so the complete set is held here and the grid's
   * filter/search/sort/paging are derived CLIENT-SIDE via the computed signals below.
   */
  private readonly allModules = signal<Module[]>([]);
  /** True while a fetch or delete is in flight. */
  readonly loading = signal<boolean>(false);
  /** RFC 7807 error message surfaced as a banner (never swallowed). */
  readonly error = signal<string | null>(null);
  /** Success message surfaced after a delete. */
  readonly successMessage = signal<string | null>(null);
  /** Active letter filter token: 'All' | a single letter A-Z; applied client-side. */
  readonly activeFilter = signal<string>(FILTER_ALL);
  /** Free-text search term; applied client-side over module title + module (friendly) name. */
  readonly searchText = signal<string>('');
  /** Current 0-based page index; the displayed page is sliced client-side. */
  readonly pageIndex = signal<number>(0);
  /** Current sort descriptor (null = load order); applied client-side. */
  readonly sort = signal<DataTableSort | null>(null);
  /** Whether the delete confirmation dialog is open. */
  readonly deleteDialogOpen = signal<boolean>(false);
  /** The module pending deletion (set when the delete action fires). */
  readonly moduleToDelete = signal<Module | null>(null);

  /**
   * The active set after the letter filter, free-text search, and sort are applied CLIENT-SIDE.
   * MIGRATION: the backend ignores filter/search/sort params, so these are derived here from the
   * full portal set (allModules) rather than re-queried per grid event.
   */
  private readonly filteredModules = computed<Module[]>(() => {
    const filter = this.activeFilter();
    const search = this.searchText().trim().toLowerCase();
    const sort = this.sort();

    let result = this.allModules();

    // Letter filter (A-Z): module title starts with the letter (legacy SearchText = <letter> + '%').
    if (filter !== FILTER_ALL) {
      const letter = filter.toLowerCase();
      result = result.filter((module) =>
        (module.moduleTitle ?? '').toLowerCase().startsWith(letter),
      );
    }

    // Free-text search across title + friendly (module) name (contains, case-insensitive).
    if (search !== '') {
      result = result.filter(
        (module) =>
          (module.moduleTitle ?? '').toLowerCase().includes(search) ||
          (module.friendlyName ?? '').toLowerCase().includes(search),
      );
    }

    // Sort (only the title column is sortable; the comparator is generic over the sort key).
    if (sort !== null) {
      const direction = sort.direction === 'asc' ? 1 : -1;
      result = [...result].sort((a, b) => this.compare(a, b, sort.key) * direction);
    }

    return result;
  });

  /** Total client-side page count for the filtered set (0 when empty). */
  private readonly totalPages = computed<number>(() => {
    const total = this.filteredModules().length;
    return total === 0 ? 0 : Math.ceil(total / PAGE_SIZE);
  });

  /** The requested page index clamped into the valid range for the current filtered set. */
  private readonly effectivePageIndex = computed<number>(() => {
    const pages = this.totalPages();
    if (pages === 0) {
      return 0;
    }
    return Math.min(this.pageIndex(), pages - 1);
  });

  /** Grid rows: the current page slice of the filtered/sorted set. */
  readonly rows = computed<Module[]>(() => {
    const start = this.effectivePageIndex() * PAGE_SIZE;
    return this.filteredModules().slice(start, start + PAGE_SIZE);
  });

  /** Pagination metadata derived from the filtered set (drives the table pager + auto-hide). */
  readonly meta = computed<ApiResponseMeta | null>(() => ({
    pageIndex: this.effectivePageIndex(),
    pageSize: PAGE_SIZE,
    totalCount: this.filteredModules().length,
    totalPages: this.totalPages(),
  }));

  /** Confirmation dialog message referencing the targeted module title. */
  readonly deleteMessage = computed<string>(() => {
    const module = this.moduleToDelete();
    return module
      ? `Are you sure you want to delete the module "${module.moduleTitle ?? ''}"?`
      : 'Are you sure you want to delete this module?';
  });

  /** Grid columns mirroring the legacy module listing. */
  readonly columns: DataTableColumn<Module>[] = [
    { key: 'moduleTitle', header: 'Title', sortable: true },
    { key: 'friendlyName', header: 'Module' },
    // MIGRATION: legacy Visibility is the VisibilityState enum (Maximized=0, Minimized=1, None=2);
    // render the enum name rather than the raw integer.
    {
      key: 'visibility',
      header: 'Visibility',
      value: (row: Module): string => VisibilityState[row.visibility] ?? '',
    },
    { key: 'allTabs', header: 'All Pages', type: 'boolean', align: 'center' },
  ];

  /** Per-row actions: edit -> /modules/:id/edit, settings -> /modules/:id/settings, delete -> confirm + DELETE. */
  readonly actions: DataTableAction<Module>[] = [
    { id: 'edit', label: 'Edit', icon: 'edit', permission: 'EDIT' },
    { id: 'settings', label: 'Settings', icon: 'settings', permission: 'MANAGE_SETTINGS' },
    { id: 'delete', label: 'Delete', icon: 'delete', permission: 'DELETE' },
  ];

  ngOnInit(): void {
    this.loadModules();
  }

  /**
   * Fetch the full set of active modules for the current portal. The grid's filter/search/sort/paging
   * are derived CLIENT-SIDE (see filteredModules/rows/meta), so this is invoked only on init and after a
   * delete — NOT on every grid event.
   *
   * MIGRATION: legacy ModuleController.GetModules(PortalID) (ModuleController.vb L915) is PORTAL-scoped.
   * The route carries no portalId param, so the current portal comes from the authenticated session
   * (User.portalID) — the same source role-form/portal-settings use (currentUser()?.portalID). Guarding
   * also narrows `number | undefined` to the `number` getModulesByPortal requires.
   */
  loadModules(): void {
    const portalId = this.auth.currentUser()?.portalID;
    if (portalId === undefined) {
      this.handleError({ detail: 'No active portal context is available. Please sign in again.' });
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: GET /api/v1/modules?portalId= returns ONLY active (IsDeleted = false) modules - the
    // soft-delete filter is applied SERVER-SIDE - so there is NO client-side isDeleted filtering here.
    // The backend accepts only the portalId/tabId discriminator (it ignores paging/filter/search/sort),
    // so NO extra query params are sent; those concerns are applied client-side from the returned set.
    this.moduleService.getModulesByPortal(portalId).subscribe({
      next: (response) => {
        this.allModules.set(response.data);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /**
   * DataTable filterChange: apply the letter filter client-side and reset to the first page. A letter
   * filter clears any active free-text search (parity with the legacy mutually-exclusive filter/search).
   */
  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
    this.searchText.set('');
    this.pageIndex.set(0);
  }

  /** DataTable pageChange (0-based): change the client-side page slice. */
  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
  }

  /** DataTable searchChange: apply the search term client-side, reset the letter filter and paging. */
  onSearchChange(search: DataTableSearch): void {
    this.searchText.set(search.text);
    this.activeFilter.set(FILTER_ALL);
    this.pageIndex.set(0);
  }

  /** DataTable sortChange: store the descriptor (applied client-side) and reset to the first page. */
  onSortChange(sort: DataTableSort): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
  }

  /** DataTable rowClick: open the edit screen for the row. */
  onRowClick(module: Module): void {
    void this.router.navigate(['/modules', module.moduleID, 'edit']);
  }

  /** DataTable actionClick: route edit/settings, or open the delete confirmation. */
  onActionClick(event: DataTableActionEvent<Module>): void {
    switch (event.action.id) {
      case 'edit':
        void this.router.navigate(['/modules', event.row.moduleID, 'edit']);
        break;
      case 'settings':
        void this.router.navigate(['/modules', event.row.moduleID, 'settings']);
        break;
      case 'delete':
        this.moduleToDelete.set(event.row);
        this.deleteDialogOpen.set(true);
        break;
    }
  }

  /** Confirmation dialog confirm: delete the targeted module (204), then re-query. */
  onConfirmDelete(): void {
    const module = this.moduleToDelete();
    this.deleteDialogOpen.set(false);
    if (module === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: legacy ModuleController.DeleteModule/DeleteTabModule -> DELETE /api/v1/modules/{id} (204);
    // the server decides hard vs. soft delete. The client issues a single request and re-queries.
    this.moduleService.deleteModule(module.moduleID).subscribe({
      next: () => {
        this.successMessage.set(`Module "${module.moduleTitle ?? ''}" was deleted.`);
        this.moduleToDelete.set(null);
        this.loadModules();
      },
      error: (problem: ProblemDetails) => {
        this.moduleToDelete.set(null);
        this.handleError(problem);
      },
    });
  }

  /** Confirmation dialog cancel: close without deleting. */
  onCancelDelete(): void {
    this.deleteDialogOpen.set(false);
    this.moduleToDelete.set(null);
  }

  /** Navigate to the create-module screen. */
  onNew(): void {
    void this.router.navigate(['/modules/new']);
  }

  /** Dismiss the error banner. */
  dismissError(): void {
    this.error.set(null);
  }

  /** Dismiss the success banner. */
  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  /**
   * Generic, null-safe comparator over a row property key, used for client-side sorting.
   * Nulls/undefined sort first; numbers and booleans compare naturally; everything else
   * compares as a locale-aware string.
   */
  private compare(a: Module, b: Module, key: string): number {
    const aValue = (a as unknown as Record<string, unknown>)[key];
    const bValue = (b as unknown as Record<string, unknown>)[key];

    if (aValue === bValue) {
      return 0;
    }
    if (aValue === null || aValue === undefined) {
      return -1;
    }
    if (bValue === null || bValue === undefined) {
      return 1;
    }
    if (typeof aValue === 'number' && typeof bValue === 'number') {
      return aValue - bValue;
    }
    if (typeof aValue === 'boolean' && typeof bValue === 'boolean') {
      return aValue === bValue ? 0 : aValue ? 1 : -1;
    }
    return String(aValue).localeCompare(String(bValue));
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'Failed to load modules.');
    this.allModules.set([]);
    this.loading.set(false);
  }
}
