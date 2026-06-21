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
  QueryParams,
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
 * replaced by a stateless REST query (GET /api/v1/modules) and signal-driven state; this
 * component is the DATA OWNER and re-queries ModuleService on every grid event
 * (paging/search/filter/sort). The table, confirmation dialog, loading spinner, and RBAC
 * gating are delegated to shared standalone building blocks.
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

  /** Grid rows (current page of active modules). */
  readonly rows = signal<Module[]>([]);
  /** Pagination metadata fed to the table for server paging; null when absent. */
  readonly meta = signal<ApiResponseMeta | null>(null);
  /** True while a fetch or delete is in flight. */
  readonly loading = signal<boolean>(false);
  /** RFC 7807 error message surfaced as a banner (never swallowed). */
  readonly error = signal<string | null>(null);
  /** Success message surfaced after a delete. */
  readonly successMessage = signal<string | null>(null);
  /** Active letter filter token: 'All' | a single letter A-Z. */
  readonly activeFilter = signal<string>(FILTER_ALL);
  /** Free-text search term (matched server-side). */
  readonly searchText = signal<string>('');
  /** Current 0-based page index. */
  readonly pageIndex = signal<number>(0);
  /** Current sort descriptor (null = server default order). */
  readonly sort = signal<DataTableSort | null>(null);
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

  /** Re-query ModuleService using the current filter, search, page, and sort state. */
  loadModules(): void {
    // MIGRATION: GET /api/v1/modules REQUIRES a scope discriminator (portalId or tabId); ModulesController.Get
    // returns 400 when neither is supplied. The authenticated user's portal is the authoritative scope, so a
    // missing JWT portal claim is surfaced as an error rather than issuing a request the backend will reject.
    const portalId = this.currentPortalId();
    if (portalId === null) {
      this.error.set('Unable to determine the current portal for the signed-in user.');
      this.rows.set([]);
      this.meta.set(null);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: GET /api/v1/modules returns ONLY active (IsDeleted = false) modules - the soft-delete
    // filter is applied SERVER-SIDE - so there is no client-side isDeleted filtering here.
    this.moduleService.getModules(this.buildParams(portalId)).subscribe({
      next: (response) => {
        this.rows.set(response.data);
        this.meta.set(response.meta);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /** DataTable filterChange: switch the letter filter, reset to the first page, re-query. */
  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
    this.pageIndex.set(0);
    this.loadModules();
  }

  /** DataTable pageChange (0-based): change page, re-query. */
  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
    this.loadModules();
  }

  /** DataTable searchChange: apply the search term, reset the filter and paging, re-query. */
  onSearchChange(search: DataTableSearch): void {
    this.searchText.set(search.text);
    this.activeFilter.set(FILTER_ALL);
    this.pageIndex.set(0);
    this.loadModules();
  }

  /** DataTable sortChange: store the descriptor, reset to the first page, re-query. */
  onSortChange(sort: DataTableSort): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
    this.loadModules();
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
   * Build the REST query params from the current grid state (omitting empty/default values).
   *
   * MIGRATION: `portalId` is the REQUIRED scope discriminator for GET /api/v1/modules
   * (ModulesController.Get returns 400 when neither portalId nor tabId is supplied). It is sent on
   * EVERY query — the initial load and every paging/search/filter/sort re-query — so the grid never
   * issues an unscoped request.
   */
  private buildParams(portalId: number): QueryParams {
    const params: QueryParams = {
      portalId,
      pageIndex: this.pageIndex(),
      pageSize: PAGE_SIZE,
    };
    const filter = this.activeFilter();
    if (filter !== FILTER_ALL) {
      params['filter'] = filter;
    }
    const search = this.searchText();
    if (search !== '') {
      params['search'] = search;
    }
    const sort = this.sort();
    if (sort !== null) {
      params['sortKey'] = sort.key;
      params['sortDirection'] = sort.direction;
    }
    return params;
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'Failed to load modules.');
    this.rows.set([]);
    this.meta.set(null);
    this.loading.set(false);
  }
}
