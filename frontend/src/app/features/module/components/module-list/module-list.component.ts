/**
 * Module List Component
 *
 * MIGRATION: Angular 19 standalone module list component displaying module instances
 * in a paginated data grid with filtering capabilities. Replaces DNN admin module
 * list patterns from Website/admin/Modules/ for managing module instances across portal tabs.
 *
 * Original DNN patterns replaced:
 * - Website/admin/Modules/ModuleSettings.ascx.vb - Module management UI
 * - Website/admin/Modules/Export.ascx.vb - Module export functionality
 * - Website/admin/Modules/Import.ascx.vb - Module import functionality
 *
 * Component features:
 * - Signal-based reactive state management (modules, loading, filter, currentPage, totalRecords)
 * - inject() function for dependency injection (ModuleService, Router)
 * - OnPush change detection strategy for performance
 * - @if/@for control flow syntax in template
 * - Supports delete operations with confirmation dialog
 * - Edit navigation to module-form route
 * - Settings navigation to module-settings route
 * - Integrates with shared data-table, loading-spinner, and confirmation-dialog components
 *
 * @source Library/Components/Modules/ModuleInfo.vb
 * @source Library/Components/Modules/ModuleController.vb
 * @source Website/admin/Modules/ModuleSettings.ascx.vb
 *
 * @example
 * // Use in template:
 * <app-module-list />
 *
 * // Use with route parameters:
 * { path: 'modules', component: ModuleListComponent }
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';

import { ModuleService } from '../../services/module.service';
import { Module, VisibilityState } from '../../models/module.model';
import {
  DataTableComponent,
  ColumnDefinition,
  PageEvent
} from '../../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { PagedResult } from '../../../portal/models/portal.model';

/**
 * ModuleListComponent
 *
 * Displays a paginated list of module instances with filtering, sorting, and actions.
 * Uses Angular 19 standalone architecture with signals for state management.
 *
 * MIGRATION: Replaces Website/admin/Modules/ VB.NET WebForms admin controls with
 * Angular 19 standalone component using signals and OnPush change detection.
 */
@Component({
  selector: 'app-module-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    DataTableComponent,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="module-list-container">
      <!-- Page Header -->
      <header class="page-header">
        <h1 class="page-title">Modules</h1>
        <p class="page-description">
          Manage module instances across portal pages
        </p>
      </header>

      <!-- Filter Section -->
      <section class="filter-section">
        <div class="filter-controls">
          <div class="search-box">
            <label for="moduleSearch" class="visually-hidden">Search modules</label>
            <input
              id="moduleSearch"
              type="text"
              class="search-input"
              placeholder="Search by title or type..."
              [value]="filter()"
              (input)="onFilterChange($any($event.target).value)"
              aria-label="Search modules by title or type" />
          </div>

          <div class="filter-dropdowns">
            <label for="portalFilter" class="visually-hidden">Filter by portal</label>
            <select
              id="portalFilter"
              class="filter-select"
              (change)="onPortalFilterChange($any($event.target).value)"
              aria-label="Filter modules by portal">
              <option value="">All Portals</option>
            </select>

            <label for="tabFilter" class="visually-hidden">Filter by tab</label>
            <select
              id="tabFilter"
              class="filter-select"
              (change)="onTabFilterChange($any($event.target).value)"
              aria-label="Filter modules by tab">
              <option value="">All Tabs</option>
            </select>
          </div>
        </div>
      </section>

      <!-- Loading State -->
      @if (loading()) {
        <div class="loading-container">
          <app-loading-spinner size="large" message="Loading modules..." />
        </div>
      }

      <!-- Data Table - Modules List -->
      @if (!loading() && hasModules()) {
        <section class="table-section">
          <app-data-table
            [columns]="columns"
            [data]="modules()"
            [loading]="loading()"
            [pageSize]="pageSize()"
            [currentPage]="currentPage()"
            [totalRecords]="totalRecords()"
            [selectable]="true"
            (rowClick)="onEdit($event)"
            (pageChange)="onPageChange($event)">
          </app-data-table>
        </section>

        <!-- Actions Row - Rendered below the table for accessibility -->
        <section class="actions-section" aria-label="Module actions">
          <div class="module-actions-info">
            <p class="actions-hint">
              Click on a row to edit the module. Use the action buttons for additional operations.
            </p>
          </div>
        </section>
      }

      <!-- Empty State -->
      @if (!loading() && !hasModules()) {
        <section class="empty-state">
          <div class="empty-state-content">
            <div class="empty-state-icon" aria-hidden="true">📦</div>
            <h2 class="empty-state-title">No Modules Found</h2>
            <p class="empty-state-message">
              @if (filter()) {
                No modules match your search criteria. Try adjusting your filters.
              } @else {
                There are no modules in this portal yet. Add modules to pages to get started.
              }
            </p>
            @if (filter()) {
              <button
                type="button"
                class="btn btn-secondary"
                (click)="clearFilters()">
                Clear Filters
              </button>
            }
          </div>
        </section>
      }

      <!-- Delete Confirmation Dialog -->
      @if (showDeleteDialog()) {
        <app-confirmation-dialog
          #deleteDialog
          [title]="'Delete Module'"
          [message]="getDeleteMessage()"
          [confirmText]="'Delete'"
          [cancelText]="'Cancel'"
          [confirmButtonType]="'danger'"
          (confirmed)="confirmDelete()"
          (cancelled)="cancelDelete()">
        </app-confirmation-dialog>
      }
    </div>
  `,
  styles: [`
    /* Container */
    .module-list-container {
      padding: 24px;
      max-width: 1400px;
      margin: 0 auto;
    }

    /* Page Header */
    .page-header {
      margin-bottom: 24px;
    }

    .page-title {
      font-size: 1.75rem;
      font-weight: 600;
      color: #333;
      margin: 0 0 8px 0;
    }

    .page-description {
      font-size: 0.875rem;
      color: #666;
      margin: 0;
    }

    /* Filter Section */
    .filter-section {
      margin-bottom: 24px;
      padding: 16px;
      background-color: #f8f9fa;
      border-radius: 8px;
      border: 1px solid #e9ecef;
    }

    .filter-controls {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      align-items: center;
    }

    .search-box {
      flex: 1;
      min-width: 200px;
    }

    .search-input {
      width: 100%;
      padding: 10px 14px;
      font-size: 0.875rem;
      border: 1px solid #ced4da;
      border-radius: 6px;
      outline: none;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .search-input:focus {
      border-color: #0d6efd;
      box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.15);
    }

    .search-input::placeholder {
      color: #999;
    }

    .filter-dropdowns {
      display: flex;
      gap: 12px;
    }

    .filter-select {
      padding: 10px 14px;
      font-size: 0.875rem;
      border: 1px solid #ced4da;
      border-radius: 6px;
      background-color: #fff;
      cursor: pointer;
      outline: none;
      min-width: 140px;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .filter-select:focus {
      border-color: #0d6efd;
      box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.15);
    }

    /* Loading State */
    .loading-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 300px;
      padding: 48px;
    }

    /* Table Section */
    .table-section {
      background-color: #fff;
      border-radius: 8px;
      border: 1px solid #e9ecef;
      overflow: hidden;
    }

    /* Actions Section */
    .actions-section {
      margin-top: 16px;
      padding: 12px 16px;
      background-color: #f8f9fa;
      border-radius: 6px;
    }

    .actions-hint {
      margin: 0;
      font-size: 0.8125rem;
      color: #666;
    }

    /* Empty State */
    .empty-state {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 300px;
      padding: 48px 24px;
      background-color: #f8f9fa;
      border-radius: 8px;
      border: 1px dashed #dee2e6;
    }

    .empty-state-content {
      text-align: center;
      max-width: 400px;
    }

    .empty-state-icon {
      font-size: 3rem;
      margin-bottom: 16px;
    }

    .empty-state-title {
      font-size: 1.25rem;
      font-weight: 600;
      color: #333;
      margin: 0 0 8px 0;
    }

    .empty-state-message {
      font-size: 0.875rem;
      color: #666;
      margin: 0 0 24px 0;
      line-height: 1.5;
    }

    /* Button Styles */
    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      padding: 10px 20px;
      font-size: 0.875rem;
      font-weight: 500;
      border-radius: 6px;
      border: none;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .btn-secondary {
      background-color: #6c757d;
      color: #fff;
    }

    .btn-secondary:hover {
      background-color: #5a6268;
    }

    .btn-secondary:focus {
      outline: none;
      box-shadow: 0 0 0 3px rgba(108, 117, 125, 0.3);
    }

    /* Visually Hidden (Accessibility) */
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    /* Responsive Styles */
    @media (max-width: 768px) {
      .module-list-container {
        padding: 16px;
      }

      .filter-controls {
        flex-direction: column;
        align-items: stretch;
      }

      .filter-dropdowns {
        flex-direction: column;
      }

      .filter-select {
        width: 100%;
      }
    }
  `]
})
export class ModuleListComponent implements OnInit {
  // ============================================================================
  // DEPENDENCY INJECTION (using inject() per Angular 19 standards)
  // MIGRATION: Replaces constructor injection pattern for cleaner code
  // ============================================================================

  /**
   * Module service for API operations
   * MIGRATION: Replaces DNN ModuleController.vb data access patterns
   */
  private readonly moduleService = inject(ModuleService);

  /**
   * Angular Router for navigation
   * MIGRATION: Replaces DNN NavigateURL patterns
   */
  private readonly router = inject(Router);

  /**
   * Reference to the delete confirmation dialog
   */
  @ViewChild('deleteDialog') deleteDialog?: ConfirmationDialogComponent;

  // ============================================================================
  // SIGNAL-BASED STATE (derived from DNN module management patterns)
  // MIGRATION: Replaces Page_Load and DataBind patterns from WebForms
  // ============================================================================

  /**
   * List of module instances
   * MIGRATION: From ModuleController.GetModules() DataReader results
   */
  readonly modules = signal<Module[]>([]);

  /**
   * Loading indicator state
   * MIGRATION: Replaces implicit WebForms postback waiting state
   */
  readonly loading = signal<boolean>(true);

  /**
   * Current search/filter term
   * MIGRATION: From admin page filter TextBox controls
   */
  readonly filter = signal<string>('');

  /**
   * Current page number (1-based like DNN ctlPagingControl)
   * MIGRATION: From DNN Records_PerPage pattern
   */
  readonly currentPage = signal<number>(1);

  /**
   * Total record count for pagination
   * MIGRATION: From DataReader total count
   */
  readonly totalRecords = signal<number>(0);

  /**
   * Page size for pagination
   * MIGRATION: Matches DNN Records_PerPage pattern (default 20)
   */
  readonly pageSize = signal<number>(20);

  /**
   * Selected portal ID for filtering
   */
  readonly selectedPortalId = signal<number | null>(null);

  /**
   * Selected tab ID for filtering
   */
  readonly selectedTabId = signal<number | null>(null);

  /**
   * Controls visibility of delete confirmation dialog
   * MIGRATION: Replaces DNN ClientAPI.AddButtonConfirm pattern
   */
  readonly showDeleteDialog = signal<boolean>(false);

  /**
   * Module pending deletion
   */
  readonly moduleToDelete = signal<Module | null>(null);

  // ============================================================================
  // COMPUTED SIGNALS
  // ============================================================================

  /**
   * Total number of pages based on total records and page size
   */
  readonly totalPages = computed(() =>
    Math.ceil(this.totalRecords() / this.pageSize()) || 1
  );

  /**
   * Indicates if there are modules to display
   */
  readonly hasModules = computed(() => this.modules().length > 0);

  // ============================================================================
  // COLUMN CONFIGURATION (derived from ModuleInfo.vb properties)
  // MIGRATION: Replaces DNN DataGrid BoundColumn definitions
  // ============================================================================

  /**
   * Column definitions for the data table
   * MIGRATION: Module grid columns derived from ModuleInfo.vb properties
   * - moduleTitle from _ModuleTitle (line 50)
   * - friendlyName from _FriendlyName (line 74)
   * - paneName from _PaneName (line 49)
   * - visibility from _Visibility (line 59)
   */
  readonly columns: ColumnDefinition[] = [
    {
      field: 'moduleTitle',
      header: 'Title',
      sortable: true,
      width: '25%',
      formatter: (value: unknown) => (value as string) || '(Untitled)'
    },
    {
      field: 'friendlyName',
      header: 'Module Type',
      sortable: true,
      width: '20%',
      formatter: (value: unknown, row: unknown) => {
        const module = row as Module;
        let name = (value as string) || 'Unknown';
        // MIGRATION: Module type badge derived from DesktopModuleInfo _IsPremium, _IsAdmin
        if (module.isAdmin) {
          name += ' [Admin]';
        }
        if (module.isPremium) {
          name += ' [Premium]';
        }
        return name;
      }
    },
    {
      field: 'paneName',
      header: 'Pane',
      sortable: true,
      width: '15%'
    },
    {
      field: 'visibility',
      header: 'Visibility',
      sortable: true,
      width: '12%',
      formatter: (value: unknown) =>
        this.formatVisibility(value as VisibilityState)
    },
    {
      field: 'tabId',
      header: 'Tab ID',
      sortable: true,
      width: '10%'
    },
    {
      field: 'actions',
      header: 'Actions',
      sortable: false,
      width: '18%',
      formatter: () => 'Edit | Settings | Delete'
    }
  ];

  // ============================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: Replaces Page_Load patterns from DNN admin controls
  // ============================================================================

  /**
   * Component initialization
   * MIGRATION: Replaces Page_Load event handler from WebForms
   */
  ngOnInit(): void {
    this.loadModules();
  }

  // ============================================================================
  // DATA LOADING METHODS
  // MIGRATION: Replaces ModuleController.GetModules/GetAllModules patterns
  // ============================================================================

  /**
   * Loads modules from the API with current filter and pagination settings
   *
   * MIGRATION: Replaces DNN ModuleController.GetModules pattern (lines 1044-1063)
   * Original VB.NET patterns replaced:
   * - DataProvider.Instance().GetTabModules(TabId)
   * - FillModuleInfoDictionary(DataReader)
   */
  loadModules(): void {
    this.loading.set(true);

    // Convert 1-based page to 0-based index for API
    const pageIndex = this.currentPage() - 1;
    const portalId = this.selectedPortalId() ?? undefined;

    this.moduleService.getModules(pageIndex, this.pageSize(), portalId)
      .subscribe({
        next: (result: PagedResult<Module>) => {
          // Apply client-side filtering if filter term is set
          let filteredModules = result.items;
          const filterTerm = this.filter().toLowerCase().trim();

          if (filterTerm) {
            filteredModules = result.items.filter(module =>
              (module.moduleTitle?.toLowerCase().includes(filterTerm)) ||
              (module.friendlyName?.toLowerCase().includes(filterTerm)) ||
              (module.paneName?.toLowerCase().includes(filterTerm))
            );
          }

          // Apply tab filter if selected
          const selectedTab = this.selectedTabId();
          if (selectedTab !== null) {
            filteredModules = filteredModules.filter(
              module => module.tabId === selectedTab
            );
          }

          this.modules.set(filteredModules);
          this.totalRecords.set(result.totalCount);
          this.loading.set(false);
        },
        error: (error: Error) => {
          console.error('Error loading modules:', error);
          this.modules.set([]);
          this.totalRecords.set(0);
          this.loading.set(false);
        }
      });
  }

  // ============================================================================
  // FILTER HANDLING
  // MIGRATION: Replaces WebForms filter control event handlers
  // ============================================================================

  /**
   * Handles search/filter input changes
   *
   * @param term - The search term entered by the user
   */
  onFilterChange(term: string): void {
    this.filter.set(term);
    this.currentPage.set(1);
    this.loadModules();
  }

  /**
   * Handles portal filter dropdown changes
   *
   * @param portalIdStr - The selected portal ID as string
   */
  onPortalFilterChange(portalIdStr: string): void {
    const portalId = portalIdStr ? parseInt(portalIdStr, 10) : null;
    this.selectedPortalId.set(portalId);
    this.currentPage.set(1);
    this.loadModules();
  }

  /**
   * Handles tab filter dropdown changes
   *
   * @param tabIdStr - The selected tab ID as string
   */
  onTabFilterChange(tabIdStr: string): void {
    const tabId = tabIdStr ? parseInt(tabIdStr, 10) : null;
    this.selectedTabId.set(tabId);
    this.currentPage.set(1);
    this.loadModules();
  }

  /**
   * Clears all filters and reloads data
   */
  clearFilters(): void {
    this.filter.set('');
    this.selectedPortalId.set(null);
    this.selectedTabId.set(null);
    this.currentPage.set(1);
    this.loadModules();
  }

  // ============================================================================
  // PAGINATION HANDLING
  // MIGRATION: Replaces DNN ctlPagingControl event handlers
  // ============================================================================

  /**
   * Handles page change events from the data table
   *
   * @param event - The page change event containing new page info
   */
  onPageChange(event: PageEvent): void {
    // Convert 0-based pageIndex to 1-based page number
    this.currentPage.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.loadModules();
  }

  // ============================================================================
  // DELETE OPERATIONS
  // MIGRATION: Derived from ModuleSettings.ascx.vb cmdDelete_Click (lines 300-312)
  // ============================================================================

  /**
   * Initiates the delete operation for a module
   *
   * MIGRATION: Replaces DNN ClientAPI.AddButtonConfirm pattern
   * Original VB.NET: cmdDelete.Click handler showing browser confirm()
   *
   * @param module - The module to delete
   */
  onDelete(module: Module): void {
    this.moduleToDelete.set(module);
    this.showDeleteDialog.set(true);

    // Open the dialog after Angular renders it
    setTimeout(() => {
      this.deleteDialog?.open();
    }, 0);
  }

  /**
   * Confirms and executes the module deletion
   *
   * MIGRATION: Replaces objModules.DeleteTabModule(TabId, ModuleId) pattern
   * from ModuleSettings.ascx.vb (line 305)
   */
  confirmDelete(): void {
    const module = this.moduleToDelete();
    if (!module) {
      this.cancelDelete();
      return;
    }

    // Call the delete API
    this.moduleService.deleteModule(module.moduleId).subscribe({
      next: () => {
        // Reload the modules list
        this.loadModules();
        this.cancelDelete();
      },
      error: (error: Error) => {
        console.error('Error deleting module:', error);
        // Still close the dialog on error
        this.cancelDelete();
      }
    });
  }

  /**
   * Cancels the delete operation and closes the dialog
   */
  cancelDelete(): void {
    this.showDeleteDialog.set(false);
    this.moduleToDelete.set(null);
    this.deleteDialog?.close();
  }

  /**
   * Gets the confirmation message for the delete dialog
   *
   * @returns The confirmation message string
   */
  getDeleteMessage(): string {
    const module = this.moduleToDelete();
    if (module) {
      return `Are you sure you want to delete the module "${module.moduleTitle || 'Untitled'}"? This action cannot be undone.`;
    }
    return 'Are you sure you want to delete this module?';
  }

  // ============================================================================
  // NAVIGATION METHODS
  // MIGRATION: Replaces DNN NavigateURL patterns with Angular Router
  // ============================================================================

  /**
   * Navigates to the module edit form
   *
   * MIGRATION: Replaces DNN NavigateURL patterns with Angular Router navigation
   *
   * @param module - The module to edit
   */
  onEdit(module: Module): void {
    this.router.navigate(['/modules', module.moduleId]);
  }

  /**
   * Navigates to the module settings page
   *
   * MIGRATION: Replaces DNN ModuleSettings.ascx control with Angular route
   *
   * @param module - The module to configure
   */
  onSettings(module: Module): void {
    this.router.navigate(['/modules', module.moduleId, 'settings']);
  }

  /**
   * Navigates to the module export page
   *
   * MIGRATION: Derived from Export.ascx.vb functionality
   *
   * @param module - The module to export
   */
  onExport(module: Module): void {
    this.router.navigate(['/modules', module.moduleId, 'export']);
  }

  /**
   * Navigates to the module import page
   *
   * MIGRATION: Derived from Import.ascx.vb functionality
   *
   * @param module - The module to import content into
   */
  onImport(module: Module): void {
    this.router.navigate(['/modules', module.moduleId, 'import']);
  }

  // ============================================================================
  // FORMATTING METHODS
  // MIGRATION: Derived from ModuleInfo.vb VisibilityState enum (lines 30-34)
  // ============================================================================

  /**
   * Formats the visibility state enum to a human-readable string
   *
   * MIGRATION: Maps from VB.NET VisibilityState enum (ModuleInfo.vb lines 30-34)
   * - Maximized = 0 (default, fully visible)
   * - Minimized = 1 (collapsed showing only header)
   * - None = 2 (hidden from view)
   *
   * @param visibility - The visibility state enum value
   * @returns Human-readable visibility string
   */
  formatVisibility(visibility: VisibilityState): string {
    switch (visibility) {
      case VisibilityState.Maximized:
        return 'Maximized';
      case VisibilityState.Minimized:
        return 'Minimized';
      case VisibilityState.None:
        return 'None';
      default:
        return 'Unknown';
    }
  }

  /**
   * Gets the CSS class for the module type badge based on premium/admin flags
   *
   * MIGRATION: Derived from DesktopModuleInfo.vb _IsPremium, _IsAdmin properties
   *
   * @param module - The module to get badge class for
   * @returns CSS class string for the badge
   */
  getModuleTypeBadge(module: Module): string {
    if (module.isAdmin) {
      return 'badge badge-admin';
    }
    if (module.isPremium) {
      return 'badge badge-premium';
    }
    return 'badge badge-standard';
  }
}
