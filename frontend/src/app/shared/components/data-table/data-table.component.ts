/**
 * MIGRATION: DataTableComponent
 * 
 * Angular 19 standalone data table component implementing a reusable, generic table
 * for displaying entity lists throughout the application.
 * 
 * MIGRATION: Replaces legacy DNN WebForms DataGrid/DataList patterns from admin screens:
 * - Users.ascx.vb
 * - Roles.ascx.vb
 * - Portals.ascx.vb
 * 
 * Features:
 * - Column configuration via TypeScript interfaces
 * - Client-side sorting with configurable sort direction
 * - Pagination with page size and current page inputs
 * - Row click event emission for navigation
 * - Uses signals for reactive state management
 * - @for control flow syntax for row iteration
 * - OnPush change detection strategy for optimal performance
 */

import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  signal,
  computed,
  effect
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoadingSpinnerComponent } from '../loading-spinner/loading-spinner.component';

/**
 * Column definition interface for configuring table columns.
 * MIGRATION: Replaces DNN BoundColumn and TemplateColumn configurations.
 */
export interface ColumnDefinition {
  /** The field name in the data object to display */
  field: string;
  /** The column header text to display */
  header: string;
  /** Whether this column is sortable */
  sortable?: boolean;
  /** Optional CSS width for the column */
  width?: string;
  /** Optional formatter function to transform the value for display */
  formatter?: (value: unknown, row: unknown) => string;
}

/**
 * Page change event interface.
 * MIGRATION: Replaces DNN ctlPagingControl page change events.
 */
export interface PageEvent {
  /** The zero-based page index */
  pageIndex: number;
  /** Number of items per page */
  pageSize: number;
  /** Total number of records */
  totalRecords: number;
}

/**
 * Sort change event interface.
 * MIGRATION: Replaces DNN DataGrid Sort handlers.
 */
export interface SortEvent {
  /** The field being sorted */
  field: string;
  /** The sort direction */
  direction: 'asc' | 'desc';
}

/**
 * Internal sort state interface.
 */
export interface SortState {
  /** Current sort field */
  field: string;
  /** Current sort direction */
  direction: 'asc' | 'desc';
}

/**
 * DataTableComponent
 * 
 * A reusable, generic data table component that displays entity lists with support for:
 * - Configurable columns with custom formatters
 * - Client-side sorting (toggleable per column)
 * - Pagination controls
 * - Row click events for navigation
 * - Loading state indicator
 * - Empty state display
 */
@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule, LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="data-table-container">
      <!-- Loading State -->
      @if (loading()) {
        <div class="table-loading">
          <app-loading-spinner />
        </div>
      } @else {
        <!-- Table Content -->
        @if (displayData().length > 0) {
          <div class="table-wrapper">
            <table class="data-table" role="grid">
              <thead>
                <tr>
                  @for (column of columns(); track column.field) {
                    <th 
                      [class.sortable]="column.sortable"
                      [class.sorted]="sortState().field === column.field"
                      [class.asc]="sortState().field === column.field && sortState().direction === 'asc'"
                      [class.desc]="sortState().field === column.field && sortState().direction === 'desc'"
                      [style.width]="column.width"
                      (click)="column.sortable ? onSort(column.field) : null"
                      [attr.aria-sort]="sortState().field === column.field ? sortState().direction + 'ending' : null"
                      scope="col">
                      {{ column.header }}
                      @if (column.sortable) {
                        <span class="sort-indicator" aria-hidden="true">
                          @if (sortState().field === column.field) {
                            {{ sortState().direction === 'asc' ? '▲' : '▼' }}
                          } @else {
                            ⇅
                          }
                        </span>
                      }
                    </th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (row of displayData(); track $index) {
                  <tr 
                    [class.selectable]="selectable()"
                    [class.clickable]="selectable()"
                    (click)="selectable() ? onRowClick(row) : null"
                    [attr.tabindex]="selectable() ? 0 : null"
                    (keydown.enter)="selectable() ? onRowClick(row) : null"
                    role="row">
                    @for (column of columns(); track column.field) {
                      <td [attr.data-label]="column.header">
                        {{ getCellValue(row, column) }}
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Pagination Controls -->
          @if (totalRecords() > pageSize()) {
            <div class="pagination-controls">
              <button 
                class="btn btn-sm"
                [disabled]="currentPage() <= 1"
                (click)="onPageChange(currentPage() - 1)"
                aria-label="Previous page">
                ← Previous
              </button>
              <span class="page-info">
                Page {{ currentPage() }} of {{ totalPages() }}
              </span>
              <button 
                class="btn btn-sm"
                [disabled]="currentPage() >= totalPages()"
                (click)="onPageChange(currentPage() + 1)"
                aria-label="Next page">
                Next →
              </button>
            </div>
          }
        } @else {
          <!-- Empty State -->
          <div class="empty-state">
            <p class="empty-message">No data available</p>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .data-table-container {
      width: 100%;
    }

    .table-loading {
      display: flex;
      justify-content: center;
      align-items: center;
      padding: 2rem;
      min-height: 200px;
    }

    .table-wrapper {
      overflow-x: auto;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
    }

    .data-table thead {
      background-color: #f5f5f5;
      border-bottom: 2px solid #e0e0e0;
    }

    .data-table th {
      padding: 12px 16px;
      text-align: left;
      font-weight: 600;
      color: #333;
      white-space: nowrap;
      user-select: none;
    }

    .data-table th.sortable {
      cursor: pointer;
    }

    .data-table th.sortable:hover {
      background-color: #ebebeb;
    }

    .data-table th.sorted {
      background-color: #e3e3e3;
    }

    .sort-indicator {
      margin-left: 4px;
      font-size: 10px;
      color: #666;
    }

    .data-table tbody tr {
      border-bottom: 1px solid #e0e0e0;
      transition: background-color 0.15s ease;
    }

    .data-table tbody tr:last-child {
      border-bottom: none;
    }

    .data-table tbody tr:hover {
      background-color: #f9f9f9;
    }

    .data-table tbody tr.selectable {
      cursor: pointer;
    }

    .data-table tbody tr.selectable:hover {
      background-color: #f0f7ff;
    }

    .data-table tbody tr.selectable:focus {
      outline: 2px solid #3f51b5;
      outline-offset: -2px;
    }

    .data-table td {
      padding: 12px 16px;
      color: #555;
    }

    .pagination-controls {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 16px;
      padding: 16px;
      border-top: 1px solid #e0e0e0;
    }

    .page-info {
      font-size: 14px;
      color: #666;
    }

    .btn {
      padding: 6px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      background-color: #fff;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .btn:hover:not(:disabled) {
      background-color: #f0f0f0;
    }

    .btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .btn-sm {
      padding: 4px 8px;
      font-size: 12px;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      min-height: 200px;
      background-color: #fafafa;
      border-radius: 8px;
      border: 1px dashed #e0e0e0;
    }

    .empty-message {
      color: #666;
      font-size: 14px;
      margin: 0;
    }

    /* Responsive table styles */
    @media (max-width: 768px) {
      .data-table thead {
        display: none;
      }

      .data-table tbody tr {
        display: block;
        margin-bottom: 16px;
        border: 1px solid #e0e0e0;
        border-radius: 8px;
      }

      .data-table td {
        display: flex;
        justify-content: space-between;
        padding: 8px 16px;
        border-bottom: 1px solid #f0f0f0;
      }

      .data-table td::before {
        content: attr(data-label);
        font-weight: 600;
        color: #333;
      }

      .data-table td:last-child {
        border-bottom: none;
      }
    }
  `]
})
export class DataTableComponent<T = unknown> {
  // ============================================================================
  // INPUTS (using input() signal function per Angular 19)
  // ============================================================================

  /**
   * Array defining table columns with configuration options.
   * MIGRATION: Replaces DNN BoundColumn and TemplateColumn configurations.
   */
  readonly columns = input<ColumnDefinition[]>([]);

  /**
   * Generic typed array of data items to display.
   * MIGRATION: Replaces DNN DataGrid.DataSource binding.
   */
  readonly data = input<T[]>([]);

  /**
   * Loading state indicator.
   */
  readonly loading = input<boolean>(false);

  /**
   * Number of items per page.
   * MIGRATION: Default 10 matches DNN Records_PerPage pattern.
   */
  readonly pageSize = input<number>(10);

  /**
   * Current page index (1-based like DNN ctlPagingControl.CurrentPage).
   */
  readonly currentPage = input<number>(1);

  /**
   * Total record count for pagination.
   */
  readonly totalRecords = input<number>(0);

  /**
   * Current sort field.
   */
  readonly sortField = input<string>('');

  /**
   * Current sort direction.
   */
  readonly sortDirection = input<'asc' | 'desc'>('asc');

  /**
   * Whether rows are clickable/selectable.
   */
  readonly selectable = input<boolean>(false);

  // ============================================================================
  // OUTPUTS (using output() function per Angular 19)
  // ============================================================================

  /**
   * Emits when a row is clicked.
   * MIGRATION: Replaces DNN grid ItemCommand event.
   */
  readonly rowClick = output<T>();

  /**
   * Emits page change event.
   * MIGRATION: Replaces DNN ctlPagingControl events.
   */
  readonly pageChange = output<PageEvent>();

  /**
   * Emits sort change event.
   * MIGRATION: Replaces DNN grid Sort handlers.
   */
  readonly sortChange = output<SortEvent>();

  // ============================================================================
  // INTERNAL SIGNALS
  // ============================================================================

  /**
   * Internal sort state tracking.
   */
  readonly sortState = signal<SortState>({ field: '', direction: 'asc' });

  /**
   * Computed displayed data based on sorting.
   */
  readonly displayData = computed(() => {
    const data = this.data();
    const state = this.sortState();

    if (!state.field || data.length === 0) {
      return data;
    }

    // Create a copy to avoid mutating the original array
    return [...data].sort((a, b) => {
      const aVal = this.getNestedValue(a, state.field);
      const bVal = this.getNestedValue(b, state.field);

      let comparison = 0;
      if (aVal == null && bVal == null) comparison = 0;
      else if (aVal == null) comparison = 1;
      else if (bVal == null) comparison = -1;
      else if (typeof aVal === 'string' && typeof bVal === 'string') {
        comparison = aVal.localeCompare(bVal);
      } else if (typeof aVal === 'number' && typeof bVal === 'number') {
        comparison = aVal - bVal;
      } else {
        comparison = String(aVal).localeCompare(String(bVal));
      }

      return state.direction === 'asc' ? comparison : -comparison;
    });
  });

  /**
   * Computed total pages based on total records and page size.
   */
  readonly totalPages = computed(() => {
    const total = this.totalRecords() || this.data().length;
    const size = this.pageSize();
    return Math.ceil(total / size) || 1;
  });

  /**
   * Constructor with effect to sync external sort inputs.
   */
  constructor() {
    // Sync external sort inputs with internal state
    effect(() => {
      const field = this.sortField();
      const direction = this.sortDirection();
      if (field) {
        this.sortState.set({ field, direction });
      }
    });
  }

  // ============================================================================
  // EVENT HANDLERS
  // ============================================================================

  /**
   * Handle row click events.
   * MIGRATION: Replaces DNN grid ItemCommand event handler.
   * @param row The clicked row data
   */
  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }

  /**
   * Handle page change events.
   * MIGRATION: Replaces DNN ctlPagingControl page change handler.
   * @param newPage The new page index (1-based)
   */
  onPageChange(newPage: number): void {
    this.pageChange.emit({
      pageIndex: newPage,
      pageSize: this.pageSize(),
      totalRecords: this.totalRecords() || this.data().length
    });
  }

  /**
   * Handle column sort events.
   * MIGRATION: Replaces DNN grid Sort event handler.
   * @param field The field to sort by
   */
  onSort(field: string): void {
    const currentState = this.sortState();
    let newDirection: 'asc' | 'desc' = 'asc';

    if (currentState.field === field) {
      // Toggle direction if same field
      newDirection = currentState.direction === 'asc' ? 'desc' : 'asc';
    }

    this.sortState.set({ field, direction: newDirection });
    this.sortChange.emit({ field, direction: newDirection });
  }

  // ============================================================================
  // HELPER METHODS
  // ============================================================================

  /**
   * Get cell value for display, applying formatter if defined.
   * @param row The data row
   * @param column The column definition
   * @returns Formatted string value for display
   */
  getCellValue(row: T, column: ColumnDefinition): string {
    const value = this.getNestedValue(row, column.field);

    if (column.formatter) {
      return column.formatter(value, row);
    }

    if (value == null) {
      return '';
    }

    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }

    return String(value);
  }

  /**
   * Get nested property value from an object using dot notation.
   * @param obj The object to extract value from
   * @param path The property path (supports dot notation)
   * @returns The property value or undefined
   */
  private getNestedValue(obj: unknown, path: string): unknown {
    if (obj == null || !path) {
      return undefined;
    }

    const keys = path.split('.');
    let current: unknown = obj;

    for (const key of keys) {
      if (current == null || typeof current !== 'object') {
        return undefined;
      }
      current = (current as Record<string, unknown>)[key];
    }

    return current;
  }
}
