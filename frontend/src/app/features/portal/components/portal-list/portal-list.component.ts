/**
 * Portal List Component - Angular 19 Standalone Component
 *
 * MIGRATION: Replaces DNN Portals.ascx.vb WebForms control
 * Source: Website/admin/Portal/Portals.ascx.vb
 *
 * This component displays portals in a paginated data grid with A-Z letter filtering,
 * 'All' option, and 'Expired' filter functionality. Supports delete operations with
 * confirmation dialog and edit navigation to portal-form route.
 *
 * Key transformations from VB.NET to Angular 19:
 * - Private _Filter/_CurrentPage/_Portals converted to signals
 * - BindData() replaced with loadPortals() async method
 * - CreateLetterSearch() replaced with computed alphabetFilters signal
 * - grdPortals_DeleteCommand replaced with onDelete/confirmDelete methods
 * - FormatExpiryDate VB.NET function converted to TypeScript method
 * - ctlPagingControl replaced with DataTable pagination
 * - NavigateURL/EditUrl replaced with Angular Router navigation
 *
 * @fileoverview Angular 19 portal list component with A-Z filtering, pagination,
 *               delete confirmation, and edit navigation
 */

import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';

import { PortalService } from '../../services/portal.service';
import { Portal, PagedResult } from '../../models/portal.model';
import {
  DataTableComponent,
  ColumnDefinition,
  PageEvent,
} from '../../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

/**
 * PortalListComponent
 *
 * Angular 19 standalone component displaying portals in a paginated data grid
 * with A-Z letter filtering, 'All' option, and 'Expired' filter.
 *
 * MIGRATION: Replaces VB.NET WebForms Portals.ascx.vb control with the following
 * transformations:
 *
 * State Management:
 * - Private _Filter As String → filter = signal<string>('')
 * - Private _CurrentPage As Integer → currentPage = signal<number>(1)
 * - Private _Portals As ArrayList → portals = signal<Portal[]>([])
 * - Protected TotalRecords As Integer → totalRecords = signal<number>(0)
 *
 * Methods:
 * - BindData() → loadPortals()
 * - CreateLetterSearch() → alphabetFilters (computed signal)
 * - FilterURL() → onFilterChange()
 * - FormatExpiryDate() → formatExpiryDate()
 * - grdPortals_DeleteCommand → onDelete() / confirmDelete()
 * - Page_Load → ngOnInit()
 *
 * UI Components:
 * - grdPortals DataGrid → app-data-table
 * - ctlPagingControl → DataTable pagination
 * - rptLetterSearch Repeater → @for loop with filter buttons
 * - ClientAPI.AddButtonConfirm → app-confirmation-dialog
 *
 * @example
 * ```html
 * <app-portal-list />
 * ```
 */
@Component({
  selector: 'app-portal-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    DataTableComponent,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent,
  ],
  providers: [DatePipe], // MIGRATION: Required for inject(DatePipe) to work
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="portal-list-container">
      <!-- Page Header -->
      <div class="portal-list-header">
        <h1 class="portal-list-title">Portals</h1>
        <button
          type="button"
          class="btn btn-primary btn-add-portal"
          (click)="onAddPortal()"
        >
          <span class="btn-icon">+</span>
          Add Portal
        </button>
      </div>

      <!-- MIGRATION: Replaces CreateLetterSearch() method (lines 170-179) -->
      <!-- A-Z Filter Navigation -->
      <nav class="filter-nav" aria-label="Portal name filter">
        <div class="filter-buttons">
          @for (letter of alphabetFilters(); track letter) {
            <button
              type="button"
              class="filter-button"
              [class.filter-button--active]="filter() === letter || (letter === 'All' && filter() === '')"
              (click)="onFilterChange(letter)"
              [attr.aria-pressed]="filter() === letter || (letter === 'All' && filter() === '')"
            >
              {{ letter }}
            </button>
          }
        </div>
      </nav>

      <!-- Loading State -->
      @if (loading()) {
        <div class="loading-container">
          <app-loading-spinner
            size="large"
            message="Loading portals..."
          />
        </div>
      } @else {
        <!-- Portal Data Table -->
        <!-- MIGRATION: Replaces grdPortals DataGrid structure from Page_Init (lines 294-318) -->
        <app-data-table
          [columns]="columns"
          [data]="portals()"
          [loading]="loading()"
          [pageSize]="pageSize()"
          [currentPage]="currentPage()"
          [totalRecords]="totalRecords()"
          [selectable]="true"
          (rowClick)="onEdit($event)"
          (pageChange)="onPageChange($event)"
        />

        <!-- Empty State -->
        @if (portals().length === 0 && !loading()) {
          <div class="empty-state">
            <p class="empty-state-message">
              @if (filter() === 'Expired') {
                No expired portals found.
              } @else if (filter()) {
                No portals found starting with "{{ filter() }}".
              } @else {
                No portals found.
              }
            </p>
          </div>
        }

        <!-- Pagination Info -->
        @if (totalRecords() > 0) {
          <div class="pagination-info">
            Showing {{ getStartRecord() }}-{{ getEndRecord() }} of {{ totalRecords() }} portals
          </div>
        }
      }

      <!-- MIGRATION: Replaces ClientAPI.AddButtonConfirm pattern from Portals.ascx.vb Page_Init (lines 299-300) -->
      <!-- Delete Confirmation Dialog -->
      <app-confirmation-dialog
        #deleteDialog
        [title]="'Delete Portal'"
        [message]="getDeleteConfirmationMessage()"
        [confirmText]="'Delete'"
        [cancelText]="'Cancel'"
        [confirmButtonType]="'danger'"
        (confirmed)="confirmDelete()"
        (cancelled)="onDeleteCancelled()"
      />
    </div>
  `,
  styles: [
    `
      /* Container Styles */
      .portal-list-container {
        padding: 24px;
        max-width: 1400px;
        margin: 0 auto;
      }

      /* Header Styles */
      .portal-list-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
        flex-wrap: wrap;
        gap: 16px;
      }

      .portal-list-title {
        margin: 0;
        font-size: 1.75rem;
        font-weight: 600;
        color: #333333;
      }

      /* Add Button Styles */
      .btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 10px 20px;
        font-size: 0.9375rem;
        font-weight: 500;
        border: none;
        border-radius: 6px;
        cursor: pointer;
        transition: background-color 0.15s ease, transform 0.1s ease,
          box-shadow 0.15s ease;
        text-decoration: none;
      }

      .btn:focus {
        outline: 2px solid #0d6efd;
        outline-offset: 2px;
      }

      .btn:active {
        transform: scale(0.98);
      }

      .btn-primary {
        background-color: #0d6efd;
        color: #ffffff;
      }

      .btn-primary:hover {
        background-color: #0b5ed7;
        box-shadow: 0 2px 8px rgba(13, 110, 253, 0.3);
      }

      .btn-add-portal .btn-icon {
        margin-right: 6px;
        font-size: 1.25rem;
        line-height: 1;
      }

      /* Filter Navigation Styles */
      /* MIGRATION: Replaces rptLetterSearch Repeater styling from Portals.ascx */
      .filter-nav {
        margin-bottom: 24px;
        padding: 16px;
        background-color: #f8f9fa;
        border-radius: 8px;
        border: 1px solid #e9ecef;
      }

      .filter-buttons {
        display: flex;
        flex-wrap: wrap;
        gap: 4px;
        justify-content: flex-start;
      }

      .filter-button {
        min-width: 36px;
        height: 36px;
        padding: 4px 8px;
        font-size: 0.875rem;
        font-weight: 500;
        background-color: #ffffff;
        color: #495057;
        border: 1px solid #ced4da;
        border-radius: 4px;
        cursor: pointer;
        transition: all 0.15s ease;
      }

      .filter-button:hover {
        background-color: #e9ecef;
        border-color: #adb5bd;
      }

      .filter-button:focus {
        outline: 2px solid #0d6efd;
        outline-offset: 1px;
      }

      .filter-button--active {
        background-color: #0d6efd;
        color: #ffffff;
        border-color: #0d6efd;
      }

      .filter-button--active:hover {
        background-color: #0b5ed7;
        border-color: #0b5ed7;
      }

      /* Loading Container */
      .loading-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 300px;
        padding: 40px;
      }

      /* Empty State Styles */
      .empty-state {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        padding: 60px 20px;
        text-align: center;
        background-color: #f8f9fa;
        border-radius: 8px;
        border: 1px dashed #ced4da;
        margin-top: 24px;
      }

      .empty-state-message {
        margin: 0;
        font-size: 1rem;
        color: #6c757d;
      }

      /* Pagination Info */
      .pagination-info {
        margin-top: 16px;
        text-align: center;
        font-size: 0.875rem;
        color: #6c757d;
      }

      /* Responsive Styles */
      @media (max-width: 768px) {
        .portal-list-container {
          padding: 16px;
        }

        .portal-list-header {
          flex-direction: column;
          align-items: stretch;
        }

        .portal-list-title {
          font-size: 1.5rem;
        }

        .btn-add-portal {
          width: 100%;
        }

        .filter-button {
          min-width: 32px;
          height: 32px;
          font-size: 0.8125rem;
        }

        .filter-nav {
          padding: 12px;
        }
      }

      @media (max-width: 480px) {
        .filter-buttons {
          gap: 2px;
        }

        .filter-button {
          min-width: 28px;
          height: 28px;
          padding: 2px 4px;
          font-size: 0.75rem;
        }
      }
    `,
  ],
})
export class PortalListComponent implements OnInit {
  // ==========================================================================
  // DEPENDENCY INJECTION (using inject() per Angular 19 standards)
  // MIGRATION: Angular 19 prefers inject() over constructor injection
  // ==========================================================================

  /**
   * Portal service for API operations.
   * MIGRATION: Replaces VB.NET PortalController calls from original
   * Portals.ascx.vb BindData() method (lines 131-158).
   */
  private readonly portalService = inject(PortalService);

  /**
   * Angular Router for navigation.
   * MIGRATION: Replaces DNN NavigateURL/EditUrl patterns from
   * ImageCommandColumn (lines 304-311).
   */
  private readonly router = inject(Router);

  /**
   * DatePipe for date formatting.
   * MIGRATION: Used in formatExpiryDate() to replace VB.NET
   * DateTime.ToShortDateString (lines 250-260).
   */
  private readonly datePipe = inject(DatePipe);

  // ==========================================================================
  // VIEW CHILD REFERENCES
  // ==========================================================================

  /**
   * Reference to the delete confirmation dialog.
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm pattern.
   */
  readonly deleteDialog = viewChild<ConfirmationDialogComponent>('deleteDialog');

  // ==========================================================================
  // SIGNAL-BASED STATE (replaces VB.NET Private Members lines 44-49)
  // MIGRATION: VB.NET Private _Portals/Filter/CurrentPage converted to signals
  // ==========================================================================

  /**
   * Array of portals displayed in the data table.
   * MIGRATION: Replaces Private _Portals As ArrayList = New ArrayList
   */
  readonly portals = signal<Portal[]>([]);

  /**
   * Loading state indicator.
   * MIGRATION: Replaces implicit WebForms postback waiting states.
   */
  readonly loading = signal<boolean>(true);

  /**
   * Current filter value (letter or 'Expired').
   * MIGRATION: Replaces Private _Filter As String = ""
   */
  readonly filter = signal<string>('');

  /**
   * Current page number (1-based).
   * MIGRATION: Replaces Private _CurrentPage As Integer = 1
   */
  readonly currentPage = signal<number>(1);

  /**
   * Total number of portal records.
   * MIGRATION: Replaces Protected TotalRecords As Integer
   */
  readonly totalRecords = signal<number>(0);

  /**
   * Number of records per page.
   * MIGRATION: Derived from Protected ReadOnly Property PageSize() (line 92-98)
   * Original returned 20.
   */
  readonly pageSize = signal<number>(20);

  /**
   * Portal selected for deletion (held temporarily during confirmation).
   */
  private portalToDelete = signal<Portal | null>(null);

  /**
   * Current portal ID (for preventing self-deletion).
   * In a real implementation, this would come from an auth/context service.
   * MIGRATION: Replaces PortalSettings.PortalId reference (line 423).
   */
  private readonly currentPortalId = signal<number | null>(null);

  // ==========================================================================
  // COMPUTED SIGNALS
  // MIGRATION: Replaces CreateLetterSearch() method (lines 170-179)
  // ==========================================================================

  /**
   * Computed signal generating the alphabet filter array.
   * MIGRATION: Replaces CreateLetterSearch() which built A-Z + All + Expired.
   * Original VB.NET:
   * ```vb
   * Dim filters As String = Localization.GetString("Filter.Text", Me.LocalResourceFile)
   * filters += "," + Localization.GetString("All")
   * filters += "," + Localization.GetString("Expired", LocalResourceFile)
   * ```
   */
  readonly alphabetFilters = computed(() => {
    // Generate A-Z letters
    const letters = Array.from({ length: 26 }, (_, i) =>
      String.fromCharCode(65 + i)
    );
    // Return: All, A-Z, Expired
    return ['All', ...letters, 'Expired'];
  });

  // ==========================================================================
  // COLUMN CONFIGURATION
  // MIGRATION: Derived from grdPortals DataGrid column structure in Page_Init (lines 294-318)
  // ==========================================================================

  /**
   * Column definitions for the data table.
   * MIGRATION: Replaces the BoundColumn and ImageCommandColumn definitions
   * from the ASPX markup and Page_Init configuration.
   */
  readonly columns: ColumnDefinition[] = [
    {
      field: 'portalName',
      header: 'Portal Name',
      sortable: true,
      width: '25%',
    },
    {
      field: 'portalAliases',
      header: 'Portal Aliases',
      sortable: false,
      width: '35%',
      // MIGRATION: Replaces FormatPortalAliases() method (lines 273-288)
      formatter: (value: unknown): string => {
        if (Array.isArray(value)) {
          return value.join(', ');
        }
        return String(value || '-');
      },
    },
    {
      field: 'expiryDate',
      header: 'Expiry Date',
      sortable: true,
      width: '15%',
      // MIGRATION: Uses formatExpiryDate method
      formatter: (value: unknown): string => {
        return this.formatExpiryDate(value as string | null);
      },
    },
    {
      field: 'users',
      header: 'Users',
      sortable: true,
      width: '10%',
      formatter: (value: unknown): string => {
        const numValue = value as number;
        return numValue >= 0 ? numValue.toString() : '-';
      },
    },
    {
      field: 'pages',
      header: 'Pages',
      sortable: true,
      width: '10%',
      formatter: (value: unknown): string => {
        const numValue = value as number;
        return numValue >= 0 ? numValue.toString() : '-';
      },
    },
  ];

  // ==========================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: Replaces Page_Load event handler (lines 333-365)
  // ==========================================================================

  /**
   * OnInit lifecycle hook - loads initial portal data.
   * MIGRATION: Replaces VB.NET Page_Load event handler.
   * Original:
   * ```vb
   * Private Sub Page_Load(...) Handles MyBase.Load
   *     If Not Page.IsPostBack Then
   *         Localization.LocalizeDataGrid(grdPortals, Me.LocalResourceFile)
   *         BindData()
   *     End If
   * End Sub
   * ```
   */
  ngOnInit(): void {
    // Load initial portal data
    this.loadPortals();
  }

  // ==========================================================================
  // DATA LOADING METHODS
  // MIGRATION: Replaces BindData() method (lines 131-158)
  // ==========================================================================

  /**
   * Loads portals from the API with current filter and pagination settings.
   *
   * MIGRATION: Replaces VB.NET BindData() method.
   * Original VB.NET:
   * ```vb
   * Private Sub BindData()
   *     CreateLetterSearch()
   *     If Filter = Localization.GetString("Expired", LocalResourceFile) Then
   *         Portals = PortalController.GetExpiredPortals()
   *         ctlPagingControl.Visible = False
   *     Else
   *         Portals = PortalController.GetPortalsByName(Filter + "%", CurrentPage - 1, PageSize, TotalRecords)
   *     End If
   *     grdPortals.DataSource = Portals
   *     grdPortals.DataBind()
   * End Sub
   * ```
   */
  loadPortals(): void {
    this.loading.set(true);

    // MIGRATION: Check for 'Expired' filter case
    const currentFilter = this.filter();

    // Build the API filter parameter
    // For 'Expired', we pass 'Expired' as the filter
    // For letter filters (A-Z), we pass the letter
    // For 'All', we pass empty string
    let apiFilter: string | undefined;

    if (currentFilter === 'Expired') {
      apiFilter = 'Expired';
    } else if (currentFilter && currentFilter !== 'All') {
      apiFilter = currentFilter;
    } else {
      apiFilter = undefined;
    }

    this.portalService
      .getPortals(
        this.currentPage() - 1, // API uses 0-based page index
        this.pageSize(),
        apiFilter
      )
      .pipe(
        finalize(() => {
          this.loading.set(false);
        })
      )
      .subscribe({
        next: (result: PagedResult<Portal>) => {
          this.portals.set(result.items);
          this.totalRecords.set(result.totalCount);
        },
        error: (error: Error) => {
          console.error('Error loading portals:', error);
          this.portals.set([]);
          this.totalRecords.set(0);
        },
      });
  }

  // ==========================================================================
  // FILTER HANDLING
  // MIGRATION: Replaces FilterURL method (lines 215-232) and querystring parsing in Page_Load
  // ==========================================================================

  /**
   * Handles filter button clicks.
   *
   * MIGRATION: Replaces VB.NET FilterURL method and querystring parsing.
   * Original VB.NET:
   * ```vb
   * Protected Function FilterURL(ByVal Filter As String, ByVal CurrentPage As String) As String
   *     If Filter <> "" Then
   *         _URL = Common.Globals.NavigateURL(TabId, "", "filter=" & Filter)
   *     End If
   *     Return _URL
   * End Function
   * ```
   *
   * @param letter - The filter letter clicked ('All', 'A'-'Z', or 'Expired')
   */
  onFilterChange(letter: string): void {
    // MIGRATION: 'All' filter should clear the filter (empty string)
    // Original VB.NET: If Filter = Localization.GetString("All") Then Filter = ""
    if (letter === 'All') {
      this.filter.set('');
    } else {
      this.filter.set(letter);
    }

    // Reset to first page when filter changes
    this.currentPage.set(1);

    // Reload data with new filter
    this.loadPortals();
  }

  // ==========================================================================
  // PAGINATION HANDLING
  // MIGRATION: Replaces ctlPagingControl events and ViewState management
  // ==========================================================================

  /**
   * Handles page change events from the data table.
   *
   * MIGRATION: Replaces VB.NET ctlPagingControl page change handling.
   * Original managed pagination through ViewState and QueryString parameters.
   *
   * @param event - Page event from the data table containing pageIndex
   */
  onPageChange(event: PageEvent): void {
    // DataTable emits 1-based pageIndex (the new page number)
    this.currentPage.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadPortals();
  }

  // ==========================================================================
  // DELETE OPERATIONS
  // MIGRATION: Replaces grdPortals_DeleteCommand event handler (lines 388-409)
  // ==========================================================================

  /**
   * Initiates delete operation - shows confirmation dialog.
   *
   * MIGRATION: Replaces the initial click handling that triggered
   * ClientAPI.AddButtonConfirm dialog.
   *
   * @param portal - The portal to delete
   */
  onDelete(portal: Portal): void {
    // MIGRATION: Check if trying to delete current portal (line 423)
    // Original: delImage.Visible = Not (portal.PortalID = PortalSettings.PortalId)
    if (
      this.currentPortalId() !== null &&
      portal.portalId === this.currentPortalId()
    ) {
      console.warn('Cannot delete the current portal');
      return;
    }

    // Store the portal for deletion after confirmation
    this.portalToDelete.set(portal);

    // Open confirmation dialog
    const dialog = this.deleteDialog();
    if (dialog) {
      dialog.open();
    }
  }

  /**
   * Confirms and executes the portal deletion.
   *
   * MIGRATION: Replaces VB.NET grdPortals_DeleteCommand handler.
   * Original VB.NET:
   * ```vb
   * Private Sub grdPortals_DeleteCommand(...) Handles grdPortals.DeleteCommand
   *     Dim portal As PortalInfo = objPortalController.GetPortal(Int32.Parse(e.CommandArgument))
   *     If Not portal Is Nothing Then
   *         Dim strMessage As String = PortalController.DeletePortal(portal, GetAbsoluteServerPath(Request))
   *         If String.IsNullOrEmpty(strMessage) Then
   *             ' Log deletion
   *             UI.Skins.Skin.AddModuleMessage(Me, "PortalDeleted", ModuleMessageType.GreenSuccess)
   *         Else
   *             UI.Skins.Skin.AddModuleMessage(Me, strMessage, ModuleMessageType.RedError)
   *         End If
   *     End If
   *     BindData()
   * End Sub
   * ```
   */
  confirmDelete(): void {
    const portal = this.portalToDelete();
    if (!portal) {
      return;
    }

    this.loading.set(true);

    this.portalService
      .deletePortal(portal.portalId)
      .pipe(
        finalize(() => {
          this.loading.set(false);
          this.portalToDelete.set(null);
        })
      )
      .subscribe({
        next: () => {
          // Reload data after successful deletion
          this.loadPortals();
        },
        error: (error: Error) => {
          console.error('Error deleting portal:', error);
        },
      });
  }

  /**
   * Handles deletion cancellation.
   */
  onDeleteCancelled(): void {
    this.portalToDelete.set(null);
  }

  /**
   * Generates the delete confirmation message for the dialog.
   *
   * @returns The confirmation message including the portal name
   */
  getDeleteConfirmationMessage(): string {
    const portal = this.portalToDelete();
    if (portal) {
      return `Are you sure you want to delete the portal "${portal.portalName}"? This action cannot be undone.`;
    }
    return 'Are you sure you want to delete this portal?';
  }

  // ==========================================================================
  // EDIT NAVIGATION
  // MIGRATION: Replaces ImageCommandColumn Edit NavigateURLFormatString (lines 304-311)
  // ==========================================================================

  /**
   * Navigates to the portal edit/form route.
   *
   * MIGRATION: Replaces VB.NET NavigateURL pattern for edit navigation.
   * Original VB.NET:
   * ```vb
   * If imageColumn.CommandName = "Edit" Then
   *     Dim objTab As TabInfo = objTabs.GetTabByName("Site Settings", PortalSettings.PortalId)
   *     Dim formatString As String = NavigateURL(objTab.TabID, "", "pid=KEYFIELD")
   *     imageColumn.NavigateURLFormatString = formatString
   * End If
   * ```
   *
   * @param portal - The portal to edit
   */
  onEdit(portal: Portal): void {
    this.router.navigate(['/portals', portal.portalId]);
  }

  /**
   * Navigates to the add portal route.
   */
  onAddPortal(): void {
    this.router.navigate(['/portals', 'new']);
  }

  // ==========================================================================
  // EXPIRY DATE FORMATTING
  // MIGRATION: Replaces FormatExpiryDate function (lines 250-260)
  // ==========================================================================

  /**
   * Formats a portal expiry date for display.
   *
   * MIGRATION: Replaces VB.NET FormatExpiryDate function.
   * Original VB.NET:
   * ```vb
   * Public Function FormatExpiryDate(ByVal DateTime As Date) As String
   *     Dim strDate As String = String.Empty
   *     Try
   *         If Not Null.IsNull(DateTime) Then
   *             strDate = DateTime.ToShortDateString
   *         End If
   *     Catch exc As Exception
   *         ProcessModuleLoadException(Me, exc)
   *     End Try
   *     Return strDate
   * End Function
   * ```
   *
   * @param date - The expiry date string (ISO 8601 format) or null
   * @returns Formatted date string or empty string for null dates
   */
  formatExpiryDate(date: string | null): string {
    if (!date) {
      return '-';
    }

    try {
      const parsedDate = new Date(date);

      // Check for invalid date
      if (isNaN(parsedDate.getTime())) {
        return '-';
      }

      // Use DatePipe for localized date formatting
      // MIGRATION: Replaces DateTime.ToShortDateString
      const formatted = this.datePipe.transform(parsedDate, 'shortDate');
      return formatted || '-';
    } catch {
      return '-';
    }
  }

  // ==========================================================================
  // PAGINATION HELPERS
  // ==========================================================================

  /**
   * Calculates the starting record number for pagination display.
   *
   * @returns The 1-based index of the first record on the current page
   */
  getStartRecord(): number {
    if (this.totalRecords() === 0) {
      return 0;
    }
    return (this.currentPage() - 1) * this.pageSize() + 1;
  }

  /**
   * Calculates the ending record number for pagination display.
   *
   * @returns The 1-based index of the last record on the current page
   */
  getEndRecord(): number {
    const end = this.currentPage() * this.pageSize();
    return Math.min(end, this.totalRecords());
  }
}
