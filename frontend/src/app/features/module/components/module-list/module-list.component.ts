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
import { summarizeProblem } from '../../../../core/services/api.service';
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
 * replaced by a stateless REST query (GET /api/v1/modules) and signal-driven state.
 *
 * MIGRATION (DEV-070 / Finding 4): the list uses CLIENT-SIDE paging/search/filter/sort. The
 * legacy ModuleController.GetModules(PortalId) returns the full portal-scoped module set in one
 * shot (it was never server-paged), and the REST GET /api/v1/modules mirrors that contract
 * exactly — an unpaged { data, meta:{} } envelope scoped by portalId/tabId. This component is the
 * DATA OWNER: it fetches the full active set ONCE into `allModules`, then DERIVES the displayed
 * page via signals — letter filter + free-text search + sort + page slice — and builds a SYNTHETIC
 * ApiResponseMeta for the pager. Grid events (paging/search/filter/sort) mutate ONLY local signals;
 * they do NOT re-query the server. A data-changing delete is the sole trigger for a re-fetch. The
 * table, confirmation dialog, loading spinner, and RBAC gating are delegated to shared standalone
 * building blocks. See MIGRATION_NOTES.md DEV-070.
 *
 * MIGRATION: soft-delete is a SERVER concern. Legacy ModuleController.DeleteModule hard-deletes,
 * while DeleteTabModule soft-deletes (IsDeleted = True, TabID = NullInteger) when the module is
 * on no other tab, and the legacy GetModules hydrated IsDeleted (returning soft-deleted rows). In
 * the rewrite the grid only ever receives active (IsDeleted = false) modules - the server applies
 * the filter - so there is NO client-side isDeleted filtering. The client issues a single
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
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // MIGRATION: the legacy module-administration grid (Website/admin/Modules + ModuleController.GetModules)
  // is portal-scoped via PortalModuleBase.PortalId, which has no SPA equivalent; the portal id is derived
  // from the JWT-authenticated current user instead. GET /api/v1/modules REQUIRES a scope discriminator
  // (portalId or tabId) — ModulesController.Get returns 400 when neither is supplied — so the authenticated
  // user's portal is the authoritative scope for the admin grid.
  // NOTE: the wire field is `portalID` (System.Text.Json camel-cases only the first character of the C#
  // `PortalID`); see core/models/user.model.ts.
  private readonly currentPortalId = computed<number | null>(
    () => this.authService.currentUser()?.portalID ?? null,
  );

  /**
   * The FULL active (IsDeleted = false) module set for the current portal, fetched ONCE.
   * MIGRATION (DEV-070): the legacy GetModules returned the whole portal-scoped set unpaged; this
   * signal is the single source of truth from which the displayed page is derived client-side.
   */
  readonly allModules = signal<Module[]>([]);
  /** True while a fetch or delete is in flight. */
  readonly loading = signal<boolean>(false);
  /** RFC 7807 error message surfaced as a banner (never swallowed). */
  readonly error = signal<string | null>(null);
  /** Success message surfaced after a delete. */
  readonly successMessage = signal<string | null>(null);
  /** Active letter filter token: 'All' | a single letter A-Z. */
  readonly activeFilter = signal<string>(FILTER_ALL);
  /** Free-text search term (matched CLIENT-side against the visible text columns). */
  readonly searchText = signal<string>('');
  /** Current 0-based page index. */
  readonly pageIndex = signal<number>(0);
  /** Current sort descriptor (null = the server's insertion order). */
  readonly sort = signal<DataTableSort | null>(null);

  /**
   * The active set after the letter filter + free-text search + sort are applied, derived from
   * `allModules`. MIGRATION (DEV-070): sorting clones the array first (Array.prototype.sort mutates in
   * place) so the source signal is never mutated. Drives both the page slice (`rows`) and synthetic `meta`.
   */
  readonly processedModules = computed<Module[]>(() => {
    const letter = this.activeFilter();
    const text = this.searchText();
    const filtered = this.allModules().filter(
      (module) => this.matchesLetter(module, letter) && this.matchesSearch(module, text),
    );
    const sort = this.sort();
    if (sort === null) {
      return filtered;
    }
    return [...filtered].sort((a, b) => this.compareBy(a, b, sort));
  });

  /** Total rows AFTER filter/search — drives the pager total (NOT the unfiltered set size). */
  readonly totalCount = computed<number>(() => this.processedModules().length);

  /**
   * The page index clamped to the available range. MIGRATION (DEV-070): a filter or a delete can shrink
   * the set below the current page; clamping here keeps the slice (`rows`) and the pager highlight
   * (`meta.pageIndex`) consistent WITHOUT mutating a signal from inside a computed (which is forbidden).
   */
  private readonly effectivePageIndex = computed<number>(() => {
    const totalPages = Math.ceil(this.totalCount() / PAGE_SIZE);
    if (totalPages <= 0) {
      return 0;
    }
    return Math.min(this.pageIndex(), totalPages - 1);
  });

  /**
   * Grid rows: the current page slice of the processed set.
   * MIGRATION (DEV-070): replaces the former server page; the data-table renders these verbatim.
   */
  readonly rows = computed<Module[]>(() => {
    const start = this.effectivePageIndex() * PAGE_SIZE;
    return this.processedModules().slice(start, start + PAGE_SIZE);
  });

  /**
   * SYNTHETIC pagination metadata fed to the data-table pager.
   * MIGRATION (DEV-070): the server returns an empty `meta`, so the component computes the page geometry
   * from the filtered length and the fixed client PAGE_SIZE. This is what lets the pager
   * (showPager = totalPages > 1) and the "page X of Y" affordance work without a server paging contract.
   */
  readonly meta = computed<ApiResponseMeta>(() => {
    const totalCount = this.totalCount();
    return {
      pageIndex: this.effectivePageIndex(),
      pageSize: PAGE_SIZE,
      totalCount,
      totalPages: Math.ceil(totalCount / PAGE_SIZE),
    };
  });

  /** Whether the delete confirmation dialog is open. */
  readonly deleteDialogOpen = signal<boolean>(false);
  /** The module pending deletion (set when the delete action fires). */
  readonly moduleToDelete = signal<Module | null>(null);

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
   * Fetch the FULL active module set for the current portal ONCE; also the post-delete refresh path.
   * MIGRATION (DEV-070): GET /api/v1/modules returns the entire portal-scoped active set unpaged, so the
   * displayed page + search + filter + sort are all derived client-side from `allModules`. Grid events do
   * NOT call this method — only the initial load and a data-changing delete do.
   */
  loadModules(): void {
    // MIGRATION: GET /api/v1/modules REQUIRES a scope discriminator (portalId or tabId); ModulesController.Get
    // returns 400 when neither is supplied. The authenticated user's portal is the authoritative scope, so a
    // missing JWT portal claim is surfaced as an error rather than issuing a request the backend will reject.
    const portalId = this.currentPortalId();
    if (portalId === null) {
      this.error.set('Unable to determine the current portal for the signed-in user.');
      this.allModules.set([]);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: GET /api/v1/modules returns ONLY active (IsDeleted = false) modules - the soft-delete
    // filter is applied SERVER-SIDE - so there is no client-side isDeleted filtering here. The server's
    // empty `meta` is intentionally ignored; the component derives a SYNTHETIC meta for the pager (DEV-070).
    this.moduleService.getModules({ portalId }).subscribe({
      next: (response) => {
        this.allModules.set(response.data);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /**
   * DataTable filterChange: switch the letter filter and reset to the first page.
   * MIGRATION (DEV-070): updates local signals only; the displayed page is re-derived client-side from
   * `allModules` — NO server re-query.
   */
  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
    this.pageIndex.set(0);
  }

  /**
   * DataTable pageChange (0-based): change the displayed page.
   * MIGRATION (DEV-070): the page slice is re-derived client-side — NO server re-query.
   */
  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
  }

  /**
   * DataTable searchChange: apply the free-text term, clear the letter filter, reset paging.
   * MIGRATION (DEV-070): filtering is applied client-side over `allModules` — NO server re-query.
   */
  onSearchChange(search: DataTableSearch): void {
    this.searchText.set(search.text);
    this.activeFilter.set(FILTER_ALL);
    this.pageIndex.set(0);
  }

  /**
   * DataTable sortChange: store the sort descriptor and reset to the first page.
   * MIGRATION (DEV-070): sorting is applied client-side over the filtered set — NO server re-query.
   */
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
    // the server decides hard vs. soft delete. The client issues a single request, then re-fetches the full
    // active set (DEV-070) so the client-side page/filter/sort re-derive against fresh data.
    this.moduleService.deleteModule(module.moduleID).subscribe({
      next: () => {
        this.successMessage.set(`Module "${module.moduleTitle ?? ''}" was deleted.`);
        this.moduleToDelete.set(null);
        this.loadModules();
      },
      error: (problem: ProblemDetails) => {
        this.moduleToDelete.set(null);
        this.handleDeleteError(problem);
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
   * Letter-filter predicate: case-insensitive match on the FIRST character of the module title.
   * MIGRATION (DEV-070): mirrors the legacy DNN admin letter-bar, which filtered the grid by the leading
   * character of the item name. 'All' matches everything; a null/empty title never matches a specific letter.
   */
  private matchesLetter(module: Module, letter: string): boolean {
    if (letter === FILTER_ALL) {
      return true;
    }
    const title = module.moduleTitle ?? '';
    return title.toUpperCase().startsWith(letter.toUpperCase());
  }

  /**
   * Free-text search predicate: case-insensitive substring across the visible text columns
   * (module title + friendly name). An empty term matches everything.
   */
  private matchesSearch(module: Module, text: string): boolean {
    if (text === '') {
      return true;
    }
    const needle = text.toLowerCase();
    return [module.moduleTitle ?? '', module.friendlyName ?? ''].some((field) =>
      field.toLowerCase().includes(needle),
    );
  }

  /** Comparator for the active sort descriptor (reads the column key, honoring direction). */
  private compareBy(a: Module, b: Module, sort: DataTableSort): number {
    // Cast through `unknown` (the column key is a dynamic string index, not a known keyof Module).
    const left = (a as unknown as Record<string, unknown>)[sort.key];
    const right = (b as unknown as Record<string, unknown>)[sort.key];
    const result = this.compareValues(left, right);
    return sort.direction === 'asc' ? result : -result;
  }

  /**
   * Null-safe, type-aware value comparison: nulls/undefined sort last; numbers and booleans compare
   * numerically; everything else compares as a case-insensitive locale string.
   */
  private compareValues(a: unknown, b: unknown): number {
    const aMissing = a === null || a === undefined;
    const bMissing = b === null || b === undefined;
    if (aMissing || bMissing) {
      return aMissing === bMissing ? 0 : aMissing ? 1 : -1;
    }
    if (typeof a === 'number' && typeof b === 'number') {
      return a - b;
    }
    if (typeof a === 'boolean' && typeof b === 'boolean') {
      return a === b ? 0 : a ? 1 : -1;
    }
    return String(a).localeCompare(String(b), undefined, { sensitivity: 'base' });
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(summarizeProblem(problem, 'Failed to load modules.'));
    this.allModules.set([]);
    this.loading.set(false);
  }

  // MIGRATION (QA Finding C): a FAILED delete must surface the error WITHOUT
  // destroying the displayed grid. The delete did not mutate anything, so the
  // records still exist; blanking the list to the "No modules found." empty-state
  // would misrepresent server state. Unlike handleError (the LOAD path, where
  // clearing the grid is the correct empty/error treatment), this delete-error
  // handler leaves the current module set intact and only surfaces the banner.
  private handleDeleteError(problem: ProblemDetails): void {
    this.error.set(summarizeProblem(problem, 'Failed to delete the module.'));
    this.loading.set(false);
  }
}
