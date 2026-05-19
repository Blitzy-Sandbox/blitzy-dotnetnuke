/**
 * UserListComponent - Angular 19 Standalone Component for User List Management
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET WebForms patterns:
 * - Website/admin/Users/ManageUsers.ascx.vb - Orchestrator patterns
 * - Website/admin/Users/Users.ascx.vb - Grid/filter/paging logic
 *   - BindData() method (lines 248-291) → loadUsers()
 *   - CreateLetterSearch() method (lines 304-316) → alphabetical filter bar
 *   - Filter property (lines 70-77) → filter signal
 *   - CurrentPage property (lines 61-68) → currentPage signal
 *   - Page_Init (lines 480-506) → ngOnInit() initialization
 *   - DisplayMode enum logic (lines 496-505) → computed display mode
 *   - FormatURL (lines 421-433) → onRowClick() navigation
 *   - btnSearch_Click (lines 630-634) → onSearch()
 *
 * This component provides:
 * - Filterable, searchable, paginated data table for user management
 * - Alphabetical filter bar (A-Z, All, Online, Unauthorized)
 * - Search input with field selection (Username, Email, Profile properties)
 * - Pagination with configurable page size
 * - Row click navigation to user edit view
 * - Reactive state management using Angular signals
 *
 * Key Transformations:
 * - VB.NET ViewState-backed properties → TypeScript signals
 * - VB.NET DataGrid binding → DataTableComponent input binding
 * - DNN Localization → Angular i18n or static text
 * - VB.NET PostBack model → Reactive updates with signals
 * - VB.NET ArrayList + ByRef totalRecords → PagedResult<User> wrapper
 *
 * @fileoverview User list component for admin user management functionality
 * @module features/user/components/user-list
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  OnInit,
  computed,
  WritableSignal,
  Signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { UserService, UserFilter, PagedResult } from '../../services/user.service';
import { User } from '../../models/user.model';
import {
  DataTableComponent,
  ColumnDefinition,
  PageEvent
} from '../../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Search field options for the search dropdown.
 * MIGRATION: Derived from Users.ascx.vb Page_Load ddlSearchType setup (lines 576-582)
 */
interface SearchFieldOption {
  /** Value used for API filtering */
  value: string;
  /** Display label in dropdown */
  label: string;
}

/**
 * Filter type for special filter modes (All, Online, Unauthorized)
 * MIGRATION: Derived from Users.ascx.vb DisplayMode enum and special filter handling (lines 258-263)
 */
type FilterType = 'All' | 'Online' | 'Unauthorized' | string;

/**
 * UserListComponent
 *
 * Angular 19 standalone component for managing user list display with filtering,
 * searching, and pagination. Uses signals for reactive state management and
 * integrates with shared DataTableComponent for consistent UI.
 *
 * MIGRATION: This component replaces the following DNN admin UI components:
 * - ManageUsers.ascx.vb - Overall user management orchestration
 * - Users.ascx.vb - User grid display, filtering, and pagination
 *
 * @class UserListComponent
 * @implements OnInit
 */
@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    DataTableComponent,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="user-list-container">
      <!-- Page Header -->
      <div class="page-header">
        <h1 class="page-title">User Management</h1>
        <button 
          class="btn btn-primary"
          routerLink="/users/new"
          aria-label="Add new user">
          + Add User
        </button>
      </div>

      <!-- Filter Bar -->
      <!-- MIGRATION: Derived from Users.ascx.vb CreateLetterSearch() (lines 304-316) -->
      <div class="filter-bar" role="navigation" aria-label="User filters">
        <div class="letter-filters">
          @for (letter of alphabetFilters; track letter) {
            <button
              class="filter-btn"
              [class.active]="filter() === letter"
              (click)="onFilterChange(letter)"
              [attr.aria-pressed]="filter() === letter"
              [attr.aria-label]="'Filter users by ' + letter">
              {{ letter }}
            </button>
          }
          <!-- Separator -->
          <span class="filter-separator" aria-hidden="true">|</span>
          <!-- Special filters -->
          <button
            class="filter-btn"
            [class.active]="filter() === 'All'"
            (click)="onFilterChange('All')"
            [attr.aria-pressed]="filter() === 'All'"
            aria-label="Show all users">
            All
          </button>
          <button
            class="filter-btn"
            [class.active]="filter() === 'Online'"
            (click)="onFilterChange('Online')"
            [attr.aria-pressed]="filter() === 'Online'"
            aria-label="Show online users">
            Online
          </button>
          <button
            class="filter-btn"
            [class.active]="filter() === 'Unauthorized'"
            (click)="onFilterChange('Unauthorized')"
            [attr.aria-pressed]="filter() === 'Unauthorized'"
            aria-label="Show unauthorized users">
            Unauthorized
          </button>
        </div>
      </div>

      <!-- Search Bar -->
      <!-- MIGRATION: Derived from Users.ascx.vb txtSearch and ddlSearchType (lines 576-582) -->
      <div class="search-bar">
        <div class="search-controls">
          <select
            [(ngModel)]="searchFieldValue"
            (ngModelChange)="onSearchFieldChange($event)"
            class="search-field-select"
            aria-label="Search field">
            @for (option of searchFieldOptions; track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
          <input
            type="text"
            [(ngModel)]="searchTermValue"
            (keyup.enter)="onSearch()"
            class="search-input"
            placeholder="Search users..."
            aria-label="Search term" />
          <button
            class="btn btn-secondary"
            (click)="onSearch()"
            [disabled]="!searchTermValue"
            aria-label="Search users">
            Search
          </button>
          @if (searchTermValue) {
            <button
              class="btn btn-outline"
              (click)="onClearSearch()"
              aria-label="Clear search">
              Clear
            </button>
          }
        </div>
        <div class="search-info">
          @if (totalRecords() > 0) {
            <span class="record-count">
              {{ totalRecords() }} user(s) found
            </span>
          }
        </div>
      </div>

      <!-- Data Table or Loading State -->
      @if (loading()) {
        <div class="loading-container">
          <app-loading-spinner 
            size="large" 
            message="Loading users..." />
        </div>
      } @else {
        <!-- MIGRATION: Replaces grdUsers.DataSource binding (line 282-283) -->
        <app-data-table
          [data]="users()"
          [columns]="columns"
          [loading]="loading()"
          [pageSize]="pageSize()"
          [currentPage]="currentPage()"
          [totalRecords]="totalRecords()"
          [selectable]="true"
          (rowClick)="onRowClick($event)"
          (pageChange)="onPageChange($event)" />
      }

      <!-- Error Message -->
      @if (errorMessage()) {
        <div class="error-message" role="alert">
          <span class="error-icon" aria-hidden="true">⚠</span>
          {{ errorMessage() }}
          <button 
            class="btn btn-sm btn-outline"
            (click)="loadUsers()"
            aria-label="Retry loading users">
            Retry
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .user-list-container {
      padding: 24px;
      max-width: 1400px;
      margin: 0 auto;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .page-title {
      font-size: 24px;
      font-weight: 600;
      color: #333;
      margin: 0;
    }

    /* Filter Bar Styles */
    .filter-bar {
      margin-bottom: 16px;
      padding: 12px 16px;
      background-color: #f8f9fa;
      border-radius: 8px;
      border: 1px solid #e9ecef;
    }

    .letter-filters {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      align-items: center;
    }

    .filter-btn {
      padding: 6px 12px;
      border: 1px solid #dee2e6;
      border-radius: 4px;
      background-color: #fff;
      color: #495057;
      font-size: 13px;
      cursor: pointer;
      transition: all 0.15s ease;
      min-width: 32px;
    }

    .filter-btn:hover {
      background-color: #e9ecef;
      border-color: #adb5bd;
    }

    .filter-btn.active {
      background-color: #3f51b5;
      border-color: #3f51b5;
      color: #fff;
    }

    .filter-separator {
      margin: 0 8px;
      color: #adb5bd;
    }

    /* Search Bar Styles */
    .search-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
      padding: 12px 16px;
      background-color: #fff;
      border-radius: 8px;
      border: 1px solid #e9ecef;
    }

    .search-controls {
      display: flex;
      gap: 8px;
      align-items: center;
    }

    .search-field-select {
      padding: 8px 12px;
      border: 1px solid #dee2e6;
      border-radius: 4px;
      font-size: 14px;
      min-width: 140px;
      background-color: #fff;
    }

    .search-input {
      padding: 8px 12px;
      border: 1px solid #dee2e6;
      border-radius: 4px;
      font-size: 14px;
      min-width: 250px;
    }

    .search-input:focus {
      outline: none;
      border-color: #3f51b5;
      box-shadow: 0 0 0 2px rgba(63, 81, 181, 0.1);
    }

    .search-info {
      font-size: 14px;
      color: #666;
    }

    .record-count {
      font-weight: 500;
    }

    /* Loading Container */
    .loading-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 300px;
      background-color: #fafafa;
      border-radius: 8px;
      border: 1px dashed #e0e0e0;
    }

    /* Error Message */
    .error-message {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px;
      margin-top: 16px;
      background-color: #fff5f5;
      border: 1px solid #feb2b2;
      border-radius: 8px;
      color: #c53030;
    }

    .error-icon {
      font-size: 20px;
    }

    /* Button Styles */
    .btn {
      padding: 8px 16px;
      border: 1px solid transparent;
      border-radius: 4px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .btn-primary {
      background-color: #3f51b5;
      color: #fff;
      border-color: #3f51b5;
    }

    .btn-primary:hover:not(:disabled) {
      background-color: #303f9f;
    }

    .btn-secondary {
      background-color: #6c757d;
      color: #fff;
      border-color: #6c757d;
    }

    .btn-secondary:hover:not(:disabled) {
      background-color: #5a6268;
    }

    .btn-outline {
      background-color: transparent;
      color: #495057;
      border-color: #dee2e6;
    }

    .btn-outline:hover:not(:disabled) {
      background-color: #f8f9fa;
    }

    .btn-sm {
      padding: 4px 8px;
      font-size: 12px;
    }

    /* Responsive Styles */
    @media (max-width: 768px) {
      .user-list-container {
        padding: 16px;
      }

      .page-header {
        flex-direction: column;
        gap: 16px;
        align-items: stretch;
      }

      .filter-bar {
        overflow-x: auto;
      }

      .letter-filters {
        flex-wrap: nowrap;
      }

      .search-bar {
        flex-direction: column;
        gap: 12px;
        align-items: stretch;
      }

      .search-controls {
        flex-wrap: wrap;
      }

      .search-input {
        min-width: auto;
        flex: 1;
      }

      .search-field-select {
        min-width: auto;
      }
    }
  `]
})
export class UserListComponent implements OnInit {
  // ===========================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() pattern)
  // ===========================================================================

  /**
   * User service for API operations.
   * MIGRATION: Replaces direct UserController method calls in VB.NET.
   * @private
   */
  private readonly userService = inject(UserService);

  /**
   * Router for navigation.
   * MIGRATION: Replaces FormatURL() (lines 421-433) and EditUrl() patterns.
   * @private
   */
  private readonly router = inject(Router);

  // ===========================================================================
  // SIGNALS - Reactive State Management
  // MIGRATION: VB.NET ViewState-backed properties converted to TypeScript signals
  // ===========================================================================

  /**
   * Array of users to display in the data table.
   * MIGRATION: Replaces VB.NET _Users ArrayList (line 52)
   */
  readonly users: WritableSignal<User[]> = signal<User[]>([]);

  /**
   * Loading state indicator.
   * MIGRATION: Implicit in VB.NET postback model, explicit here for UX.
   */
  readonly loading: WritableSignal<boolean> = signal<boolean>(true);

  /**
   * Total number of records matching current filter/search.
   * MIGRATION: Replaces VB.NET TotalRecords (line 59) ByRef parameter pattern.
   */
  readonly totalRecords: WritableSignal<number> = signal<number>(0);

  /**
   * Current page number (1-based).
   * MIGRATION: Replaces VB.NET CurrentPage property (lines 61-68).
   */
  readonly currentPage: WritableSignal<number> = signal<number>(1);

  /**
   * Number of items per page.
   * MIGRATION: Replaces VB.NET PageSize property.
   */
  readonly pageSize: WritableSignal<number> = signal<number>(10);

  /**
   * Current filter value (letter, 'All', 'Online', or 'Unauthorized').
   * MIGRATION: Replaces VB.NET Filter property (lines 70-77).
   */
  readonly filter: WritableSignal<FilterType> = signal<FilterType>('All');

  /**
   * Current search term.
   * MIGRATION: Replaces VB.NET txtSearch.Text value.
   */
  readonly searchTerm: WritableSignal<string> = signal<string>('');

  /**
   * Current search field (Username, Email, or profile property).
   * MIGRATION: Replaces VB.NET ddlSearchType.SelectedItem.Value (lines 576-582).
   */
  readonly searchField: WritableSignal<string> = signal<string>('Username');

  /**
   * Error message for display when API calls fail.
   */
  readonly errorMessage: WritableSignal<string> = signal<string>('');

  // ===========================================================================
  // COMPUTED SIGNALS
  // ===========================================================================

  /**
   * Computed display mode based on current filter.
   * MIGRATION: Derived from Users.ascx.vb DisplayMode enum logic (lines 496-505).
   */
  readonly displayMode: Signal<'all' | 'filtered' | 'online' | 'unauthorized'> = computed(() => {
    const currentFilter = this.filter();
    if (currentFilter === 'All') return 'all';
    if (currentFilter === 'Online') return 'online';
    if (currentFilter === 'Unauthorized') return 'unauthorized';
    return 'filtered';
  });

  // ===========================================================================
  // TEMPLATE BINDING PROPERTIES
  // Two-way binding values for ngModel (signals don't work directly with ngModel)
  // ===========================================================================

  /**
   * Search term value for ngModel binding.
   */
  searchTermValue = '';

  /**
   * Search field value for ngModel binding.
   */
  searchFieldValue = 'Username';

  // ===========================================================================
  // STATIC DATA
  // ===========================================================================

  /**
   * Alphabetical filter options (A-Z).
   * MIGRATION: Derived from Users.ascx.vb Filter.Text localization string (line 306).
   */
  readonly alphabetFilters: readonly string[] = [
    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
    'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
  ];

  /**
   * Search field dropdown options.
   * MIGRATION: Derived from Users.ascx.vb Page_Load ddlSearchType setup (lines 576-582).
   * In DNN, profile properties were dynamically added from ProfileController.GetPropertyDefinitionsByPortal().
   */
  readonly searchFieldOptions: readonly SearchFieldOption[] = [
    { value: 'Username', label: 'Username' },
    { value: 'Email', label: 'Email' },
    { value: 'DisplayName', label: 'Display Name' },
    { value: 'FirstName', label: 'First Name' },
    { value: 'LastName', label: 'Last Name' }
  ];

  /**
   * Column definitions for the DataTableComponent.
   * MIGRATION: Replaces VB.NET DataGrid column configuration and Localization.LocalizeDataGrid() (line 585).
   */
  readonly columns: ColumnDefinition[] = [
    {
      field: 'username',
      header: 'Username',
      sortable: true,
      width: '150px'
    },
    {
      field: 'displayName',
      header: 'Display Name',
      sortable: true,
      width: '200px'
    },
    {
      field: 'email',
      header: 'Email',
      sortable: true,
      width: '250px'
    },
    {
      field: 'createdDate',
      header: 'Created',
      sortable: true,
      width: '150px',
      formatter: (value: unknown): string => {
        if (!value) return '';
        const date = new Date(value as string);
        return date.toLocaleDateString();
      }
    }
  ];

  // ===========================================================================
  // LIFECYCLE HOOKS
  // ===========================================================================

  /**
   * Angular OnInit lifecycle hook.
   * Initializes the component with default filter and loads users.
   *
   * MIGRATION: Replaces VB.NET Page_Init (lines 480-506) and Page_Load (lines 569-598).
   * - Query string parsing for filter/currentpage moved to route params (future enhancement)
   * - DisplayMode default (lines 496-505) applied via initial filter signal value
   */
  ngOnInit(): void {
    // Initialize with 'All' filter to show all users by default
    // MIGRATION: In DNN, default view came from GetSetting(UsersPortalId, "Display_Mode")
    this.loadUsers();
  }

  // ===========================================================================
  // PUBLIC METHODS
  // ===========================================================================

  /**
   * Loads users from the API based on current filter, search, and pagination state.
   *
   * MIGRATION: Replaces VB.NET BindData() method (lines 248-291).
   * Key transformations:
   * - UserController.GetUsers() → userService.getUsers()
   * - UserController.GetUnAuthorizedUsers() → userService.getUnauthorizedUsers()
   * - UserController.GetOnlineUsers() → userService.getUsers() with filter
   * - UserController.GetUsersByEmail/UserName/ProfileProperty() → userService.getUsers() with filter
   * - ArrayList + ByRef totalRecords → PagedResult<User> wrapper
   */
  loadUsers(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    const currentFilter = this.filter();
    const currentSearchTerm = this.searchTerm();
    const currentSearchField = this.searchField();
    const pageIndex = this.currentPage() - 1; // Convert to 0-based for API
    const currentPageSize = this.pageSize();

    // MIGRATION: Handle special filter modes (lines 258-263)
    if (currentFilter === 'Unauthorized') {
      // MIGRATION: Replaces UserController.GetUnAuthorizedUsers(UsersPortalId, False) (line 259)
      this.userService.getUnauthorizedUsers({ pageIndex, pageSize: currentPageSize }).subscribe({
        next: (result: PagedResult<User>) => this.handleLoadSuccess(result),
        error: (error: Error) => this.handleLoadError(error)
      });
    } else if (currentFilter === 'Online') {
      // MIGRATION: Replaces UserController.GetOnlineUsers(UsersPortalId) (line 262)
      // Online filter is handled by API via a special filter parameter
      const filter: UserFilter = { isAuthorized: true };
      this.userService.getUsers(filter, { pageIndex, pageSize: currentPageSize }).subscribe({
        next: (result: PagedResult<User>) => this.handleLoadSuccess(result),
        error: (error: Error) => this.handleLoadError(error)
      });
    } else if (currentFilter === 'All') {
      // MIGRATION: Replaces UserController.GetUsers(UsersPortalId, False, CurrentPage - 1, PageSize, TotalRecords) (line 265)
      if (currentSearchTerm) {
        // Search mode
        const filter = this.buildSearchFilter(currentSearchTerm, currentSearchField);
        this.userService.getUsers(filter, { pageIndex, pageSize: currentPageSize }).subscribe({
          next: (result: PagedResult<User>) => this.handleLoadSuccess(result),
          error: (error: Error) => this.handleLoadError(error)
        });
      } else {
        // No search - get all users
        this.userService.getUsers(undefined, { pageIndex, pageSize: currentPageSize }).subscribe({
          next: (result: PagedResult<User>) => this.handleLoadSuccess(result),
          error: (error: Error) => this.handleLoadError(error)
        });
      }
    } else {
      // Letter filter (A-Z)
      // MIGRATION: Replaces GetUsersByUserName with letter prefix (lines 267-271)
      const filter: UserFilter = { username: currentFilter };
      this.userService.getUsers(filter, { pageIndex, pageSize: currentPageSize }).subscribe({
        next: (result: PagedResult<User>) => this.handleLoadSuccess(result),
        error: (error: Error) => this.handleLoadError(error)
      });
    }
  }

  /**
   * Handles filter change when user clicks a letter or special filter button.
   *
   * MIGRATION: Replaces VB.NET Filter property setter logic (lines 70-77) and
   * the linkbutton click handlers in rptLetterSearch repeater.
   *
   * @param letter - The filter value ('A'-'Z', 'All', 'Online', or 'Unauthorized')
   */
  onFilterChange(letter: string): void {
    this.filter.set(letter);
    this.currentPage.set(1); // Reset to first page when filter changes
    this.clearSearchState();
    this.loadUsers();
  }

  /**
   * Handles search form submission.
   *
   * MIGRATION: Replaces VB.NET btnSearch_Click event handler (lines 630-634).
   * Original pattern:
   * ```vb
   * CurrentPage = 1
   * Response.Redirect(NavigateURL(TabId, "", UserFilter(True)))
   * ```
   */
  onSearch(): void {
    if (!this.searchTermValue.trim()) {
      return;
    }

    this.searchTerm.set(this.searchTermValue.trim());
    this.searchField.set(this.searchFieldValue);
    this.currentPage.set(1); // Reset to first page
    this.filter.set('All'); // Reset filter when searching
    this.loadUsers();
  }

  /**
   * Handles search field dropdown change.
   *
   * @param field - The selected search field value
   */
  onSearchFieldChange(field: string): void {
    this.searchFieldValue = field;
    this.searchField.set(field);
  }

  /**
   * Clears the current search and reloads users.
   */
  onClearSearch(): void {
    this.searchTermValue = '';
    this.searchTerm.set('');
    this.loadUsers();
  }

  /**
   * Handles page change events from the DataTableComponent.
   *
   * MIGRATION: Replaces VB.NET ctlPagingControl page change logic (lines 285-290).
   * Original pattern bound CurrentPage, PageSize, TotalRecords to the paging control.
   *
   * @param event - Page change event containing new page index
   */
  onPageChange(event: PageEvent): void {
    this.currentPage.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadUsers();
  }

  /**
   * Handles row click events to navigate to user edit view.
   *
   * MIGRATION: Replaces VB.NET FormatURL() method (lines 421-433).
   * Original pattern:
   * ```vb
   * If Filter <> "" Then
   *     _URL = EditUrl(strKeyName, strKeyValue, "", "filter=" & Filter)
   * Else
   *     _URL = EditUrl(strKeyName, strKeyValue)
   * End If
   * ```
   *
   * @param user - The clicked user row data
   */
  onRowClick(user: User): void {
    // Navigate to user edit form with the user's ID
    this.router.navigate(['/users', user.userId]);
  }

  // ===========================================================================
  // PRIVATE METHODS
  // ===========================================================================

  /**
   * Builds a UserFilter object based on search term and field.
   *
   * MIGRATION: Replaces VB.NET Select Case SearchField logic (lines 267-276):
   * ```vb
   * Select Case SearchField
   *     Case "Email"
   *         Users = UserController.GetUsersByEmail(...)
   *     Case "Username"
   *         Users = UserController.GetUsersByUserName(...)
   *     Case Else
   *         Users = UserController.GetUsersByProfileProperty(...)
   * End Select
   * ```
   *
   * @param searchTerm - The search term to filter by
   * @param searchField - The field to search in
   * @returns UserFilter object for API call
   */
  private buildSearchFilter(searchTerm: string, searchField: string): UserFilter {
    const filter: UserFilter = {};

    switch (searchField) {
      case 'Email':
        filter.email = searchTerm;
        break;
      case 'Username':
        filter.username = searchTerm;
        break;
      default:
        // For profile properties (DisplayName, FirstName, LastName), use general search
        filter.search = searchTerm;
        break;
    }

    return filter;
  }

  /**
   * Handles successful user load response.
   *
   * @param result - PagedResult containing users and pagination info
   */
  private handleLoadSuccess(result: PagedResult<User>): void {
    this.users.set(result.items);
    this.totalRecords.set(result.totalCount);
    this.loading.set(false);
  }

  /**
   * Handles user load error.
   *
   * @param error - Error from the API call
   */
  private handleLoadError(error: Error): void {
    console.error('Failed to load users:', error);
    this.errorMessage.set('Failed to load users. Please try again.');
    this.users.set([]);
    this.totalRecords.set(0);
    this.loading.set(false);
  }

  /**
   * Clears search state when switching filters.
   */
  private clearSearchState(): void {
    this.searchTermValue = '';
    this.searchTerm.set('');
  }
}
