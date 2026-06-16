import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ApiResponseMeta } from '../../../core/services/api.service';
import { DateFormatPipe } from '../../pipes';
import { TooltipDirective } from '../../directives/tooltip';
import { HasPermissionDirective, type PermissionKey } from '../../directives/has-permission';

export type SortDirection = 'asc' | 'desc';
export type DataTableColumnType = 'text' | 'number' | 'date' | 'boolean';
export type DataTableAlign = 'left' | 'center' | 'right';

export interface DataTableColumn<T> {
  key: string;
  header: string;
  type?: DataTableColumnType;
  sortable?: boolean;
  visible?: boolean;
  align?: DataTableAlign;
  value?: (row: T) => unknown;
  format?: string;
}

export interface DataTableAction<T> {
  id: string;
  label: string;
  icon?: string;
  permission?: PermissionKey;
  disabled?: (row: T) => boolean;
  hidden?: (row: T) => boolean;
}

export interface DataTableSort {
  key: string;
  direction: SortDirection;
}

export interface DataTableSearch {
  text: string;
  type: string;
}

export interface DataTableActionEvent<T> {
  action: DataTableAction<T>;
  row: T;
}

const DEFAULT_FILTERS: readonly string[] = [
  'All', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
  'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
];

@Component({
  selector: 'app-data-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ScrollingModule, DateFormatPipe, TooltipDirective, HasPermissionDirective],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent<T> {
  readonly rows = input<T[]>([]);
  readonly columns = input.required<DataTableColumn<T>[]>();
  readonly actions = input<DataTableAction<T>[]>([]);
  readonly meta = input<ApiResponseMeta | null>(null);
  readonly sort = input<DataTableSort | null>(null);
  readonly filters = input<readonly string[]>(DEFAULT_FILTERS);
  readonly activeFilter = input<string>('All');
  readonly searchable = input<boolean>(false);
  readonly searchTypes = input<string[]>([]);
  readonly searchText = input<string>('');
  readonly searchType = input<string>('');
  readonly itemSize = input<number>(48);
  readonly viewportRows = input<number>(8);
  readonly loading = input<boolean>(false);
  readonly caption = input<string>('');
  readonly emptyMessage = input<string>('No records found.');
  readonly actionsLabel = input<string>('Actions');
  readonly searchPlaceholder = input<string>('');
  readonly searchButtonLabel = input<string>('Search');

  readonly rowClick = output<T>();
  readonly pageChange = output<number>();
  readonly filterChange = output<string>();
  readonly searchChange = output<DataTableSearch>();
  readonly sortChange = output<DataTableSort>();
  readonly actionClick = output<DataTableActionEvent<T>>();

  private readonly searchTypeSelection = signal<string | null>(null);

  readonly visibleColumns = computed<DataTableColumn<T>[]>(() =>
    this.columns().filter((column) => column.visible !== false),
  );

  readonly pageIndex = computed<number>(() => this.meta()?.pageIndex ?? 0);
  readonly pageSize = computed<number>(() => this.meta()?.pageSize ?? 0);
  readonly totalCount = computed<number>(() => this.meta()?.totalCount ?? 0);

  readonly totalPages = computed<number>(() => {
    const meta = this.meta();
    if (meta?.totalPages !== undefined && meta.totalPages !== null) {
      return meta.totalPages;
    }
    const size = meta?.pageSize ?? 0;
    const count = meta?.totalCount ?? 0;
    return size > 0 ? Math.ceil(count / size) : 0;
  });

  readonly showPager = computed<boolean>(() => this.totalPages() > 1);
  readonly hasActions = computed<boolean>(() => this.actions().length > 0);
  readonly showSearch = computed<boolean>(() => this.searchable());
  readonly showFilters = computed<boolean>(() => this.filters().length > 0);
  readonly isEmpty = computed<boolean>(() => this.rows().length === 0);
  readonly viewportHeight = computed<number>(() => this.itemSize() * this.viewportRows());

  readonly displaySearchType = computed<string>(() => {
    const selected = this.searchTypeSelection();
    if (selected !== null) {
      return selected;
    }
    const fromInput = this.searchType();
    if (fromInput !== '') {
      return fromInput;
    }
    const types = this.searchTypes();
    return types.length > 0 ? types[0] : '';
  });

  readonly trackRow = (_index: number, row: T): T => row;

  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }

  onRowActivate(row: T, event: Event): void {
    event.preventDefault();
    this.rowClick.emit(row);
  }

  onFilter(value: string): void {
    this.filterChange.emit(value);
  }

  onSearch(text: string): void {
    this.searchChange.emit({ text, type: this.displaySearchType() });
  }

  onSearchTypeChange(value: string): void {
    this.searchTypeSelection.set(value);
  }

  onSort(column: DataTableColumn<T>): void {
    if (column.sortable !== true) {
      return;
    }
    const current = this.sort();
    const direction: SortDirection =
      current !== null && current.key === column.key && current.direction === 'asc' ? 'desc' : 'asc';
    this.sortChange.emit({ key: column.key, direction });
  }

  ariaSort(column: DataTableColumn<T>): 'ascending' | 'descending' | 'none' {
    const current = this.sort();
    if (current === null || current.key !== column.key) {
      return 'none';
    }
    return current.direction === 'asc' ? 'ascending' : 'descending';
  }

  onPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) {
      return;
    }
    this.pageChange.emit(index);
  }

  onAction(action: DataTableAction<T>, row: T, event: Event): void {
    event.stopPropagation();
    if (this.isActionDisabled(action, row)) {
      return;
    }
    this.actionClick.emit({ action, row });
  }

  isActionHidden(action: DataTableAction<T>, row: T): boolean {
    return action.hidden ? action.hidden(row) : false;
  }

  isActionDisabled(action: DataTableAction<T>, row: T): boolean {
    return action.disabled ? action.disabled(row) : false;
  }

  cellValue(row: T, column: DataTableColumn<T>): unknown {
    if (column.value) {
      return column.value(row);
    }
    return (row as Record<string, unknown>)[column.key];
  }

  displayValue(row: T, column: DataTableColumn<T>): string {
    const value = this.cellValue(row, column);
    if (value === null || value === undefined) {
      return '';
    }
    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }
    return String(value);
  }

  dateValue(row: T, column: DataTableColumn<T>): Date | string | number | null {
    const value = this.cellValue(row, column);
    if (value instanceof Date || typeof value === 'string' || typeof value === 'number') {
      return value;
    }
    return null;
  }
}
