import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import { type Tab, TabType } from '../../models';
import { TabService } from '../../services';
import type { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  DataTableComponent,
  type DataTableAction,
  type DataTableActionEvent,
  type DataTableColumn,
} from '../../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

// MIGRATION: Legacy Website/admin/Tabs admin grid listed a portal's pages (tabs). There is no
// role-group-style server filter for tabs; instead the legacy grid distinguished visible vs hidden
// pages. The filter is reconstructed client-side over the loaded tabs (the shared DataTable renders
// each token verbatim as its button label and emits it unchanged, so these human-readable strings
// double as token + label).
const FILTER_ALL = 'All Tabs';
const FILTER_VISIBLE = 'Visible';
const FILTER_HIDDEN = 'Hidden';

/** Display labels for the numeric TabType ordinals (the API emits the enum as a number). */
const TAB_TYPE_LABELS: Record<TabType, string> = {
  [TabType.File]: 'File',
  [TabType.Normal]: 'Normal',
  [TabType.Tab]: 'Tab',
  [TabType.Url]: 'URL',
  [TabType.Member]: 'Member',
};

/**
 * Standalone list screen for Tabs (Pages). DATA OWNER for the feature list view: it loads the current
 * portal's tabs, filters them client-side (All / Visible / Hidden), and delegates rendering, the
 * delete-confirmation dialog and RBAC gating to shared blocks.
 *
 * MIGRATION: reinterprets the legacy DNN Web Forms admin Tabs grid as an Angular 19 screen with UI
 * functional parity (AAP §0.3.4 / §0.7.1): a page grid, a visible/hidden filter, per-row
 * Edit / Settings / Delete actions, and a delete-confirmation dialog. The legacy postback/ViewState
 * model is replaced by stateless REST calls through TabService.
 */
@Component({
  selector: 'app-tab-list',
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './tab-list.component.html',
  styleUrl: './tab-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TabListComponent implements OnInit {
  private readonly tabService = inject(TabService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly tabs = signal<Tab[]>([]);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly activeFilter = signal<string>(FILTER_ALL);
  readonly deleteDialogOpen = signal<boolean>(false);
  readonly tabToDelete = signal<Tab | null>(null);

  // MIGRATION: Legacy PortalModuleBase.PortalId has no SPA equivalent; the portal id is derived from
  // the JWT-authenticated current user instead. Tab endpoints are portal-scoped (the backend returns
  // 400 without portalId), so a missing portal claim is surfaced as an error rather than a bad request.
  private readonly currentPortalId = computed<number | null>(
    () => this.authService.currentUser()?.portalID ?? null,
  );

  readonly filters: readonly string[] = [FILTER_ALL, FILTER_VISIBLE, FILTER_HIDDEN];

  readonly displayedRows = computed<Tab[]>(() => {
    const filter = this.activeFilter();
    const tabs = this.tabs();

    if (filter === FILTER_VISIBLE) {
      return tabs.filter((tab) => tab.isVisible);
    }
    if (filter === FILTER_HIDDEN) {
      return tabs.filter((tab) => !tab.isVisible);
    }
    return tabs;
  });

  readonly deleteMessage = computed<string>(() => {
    const tab = this.tabToDelete();
    return tab
      ? `Are you sure you want to delete the page "${tab.tabName ?? ''}"?`
      : 'Are you sure you want to delete this page?';
  });

  readonly columns: DataTableColumn<Tab>[] = [
    { key: 'tabName', header: 'Page Name' },
    { key: 'title', header: 'Title' },
    { key: 'tabOrder', header: 'Order', type: 'number', align: 'right' },
    { key: 'isVisible', header: 'Visible', type: 'boolean' },
    {
      key: 'tabType',
      header: 'Type',
      // The API serializes TabType as its numeric ordinal; map it to a readable label for display.
      value: (tab: Tab): string => this.tabTypeLabel(tab.tabType),
    },
  ];

  readonly actions: DataTableAction<Tab>[] = [
    { id: 'edit', label: 'Edit', icon: 'edit', permission: 'EDIT' },
    { id: 'settings', label: 'Settings', icon: 'settings', permission: 'EDIT' },
    { id: 'delete', label: 'Delete', icon: 'delete', permission: 'DELETE' },
  ];

  ngOnInit(): void {
    this.loadTabs();
  }

  loadTabs(): void {
    const portalId = this.currentPortalId();
    if (portalId === null) {
      this.error.set('Unable to determine the current portal for the signed-in user.');
      this.tabs.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.tabService.getTabs(portalId).subscribe({
      next: (tabs) => {
        this.tabs.set(tabs);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
  }

  onActionClick(event: DataTableActionEvent<Tab>): void {
    switch (event.action.id) {
      case 'edit':
        void this.router.navigate(['/tabs', event.row.tabID, 'edit']);
        break;
      case 'settings':
        void this.router.navigate(['/tabs', event.row.tabID, 'settings']);
        break;
      case 'delete':
        this.tabToDelete.set(event.row);
        this.deleteDialogOpen.set(true);
        break;
    }
  }

  onConfirmDelete(): void {
    const tab = this.tabToDelete();
    this.deleteDialogOpen.set(false);
    if (tab === null) {
      return;
    }
    const portalId = this.currentPortalId();
    if (portalId === null) {
      this.error.set('Unable to determine the current portal for the signed-in user.');
      this.tabToDelete.set(null);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: backend Tab delete is a SOFT delete (sets the deleted flag) and is portal-scoped.
    this.tabService.deleteTab(tab.tabID, portalId).subscribe({
      next: () => {
        this.successMessage.set(`Page "${tab.tabName ?? ''}" was deleted.`);
        this.tabToDelete.set(null);
        this.loadTabs();
      },
      error: (problem: ProblemDetails) => {
        this.tabToDelete.set(null);
        this.handleError(problem);
      },
    });
  }

  onCancelDelete(): void {
    this.deleteDialogOpen.set(false);
    this.tabToDelete.set(null);
  }

  onAddTab(): void {
    void this.router.navigate(['/tabs/new']);
  }

  dismissError(): void {
    this.error.set(null);
  }

  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  /** Maps a numeric TabType ordinal to its display label. */
  tabTypeLabel(type: TabType): string {
    return TAB_TYPE_LABELS[type] ?? 'Normal';
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'Failed to load pages.');
    this.tabs.set([]);
    this.loading.set(false);
  }
}
