import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
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

  // F4: the search controls (search-type <select> + search <input>) carried aria-label but
  // no id/name, which Chrome flags as "A form field element should have an id or name
  // attribute". A per-instance counter yields stable, unique ids so multiple data-tables on
  // one page never collide. Used for both [id] and [attr.name] on the two controls.
  private static instanceCount = 0;
  private readonly instanceId = `dt-${(DataTableComponent.instanceCount += 1)}`;
  readonly searchInputId = `${this.instanceId}-search`;
  readonly searchTypeId = `${this.instanceId}-search-type`;

  // -----------------------------------------------------------------------------------------------
  // QA Finding 5 (responsive): below 480px the grid switches from a horizontal, multi-column row to
  // a stacked card so mobile users get a readable list instead of a horizontally-scrolling table.
  // The card shows every visible column as a single-line "label: value" field plus the action strip,
  // all at FIXED per-element heights (see the mobile block in the SCSS). That makes the card height a
  // deterministic function of the visible-column count, which is fed back to the CDK virtual scroller
  // as the mobile itemSize so row positioning stays exact (no drift / overlap).
  // -----------------------------------------------------------------------------------------------
  /** Media query marking the mobile (stacked-card) breakpoint; mirrors the SCSS `@media` value. */
  private static readonly MOBILE_MEDIA_QUERY = '(max-width: 480px)';
  /** Fixed pixel height of one stacked "label: value" field on mobile (mirrors the SCSS cell height). */
  private static readonly MOBILE_FIELD_HEIGHT = 28;
  /** Fixed pixel height of the mobile action strip when a row has actions (mirrors the SCSS). */
  private static readonly MOBILE_ACTIONS_HEIGHT = 48;
  /** Combined top + bottom padding of a mobile card (mirrors the SCSS `.data-table__row` padding). */
  private static readonly MOBILE_CARD_PADDING = 16;
  /** Cards shown in the mobile viewport before it scrolls (keeps the page a manageable length). */
  private static readonly MOBILE_VIEWPORT_ROWS = 4;

  private readonly destroyRef = inject(DestroyRef);

  /** True while the viewport matches the mobile breakpoint; drives the stacked-card layout. */
  protected readonly isMobile = signal<boolean>(false);

  constructor() {
    // Track the breakpoint reactively. Guarded for environments without matchMedia; the change
    // listener is detached automatically when the component is destroyed.
    if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
      const mediaQuery = window.matchMedia(DataTableComponent.MOBILE_MEDIA_QUERY);
      this.isMobile.set(mediaQuery.matches);
      const onChange = (event: MediaQueryListEvent): void => this.isMobile.set(event.matches);
      mediaQuery.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => mediaQuery.removeEventListener('change', onChange));
    }
  }

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
  /**
   * Pixel height of one mobile stacked card: one fixed-height field per visible column, plus the
   * action strip when present, plus the card's vertical padding. Deterministic, so it can serve as
   * the CDK virtual-scroll itemSize on mobile without row drift.
   */
  readonly mobileItemSize = computed<number>(
    () =>
      this.visibleColumns().length * DataTableComponent.MOBILE_FIELD_HEIGHT +
      (this.hasActions() ? DataTableComponent.MOBILE_ACTIONS_HEIGHT : 0) +
      DataTableComponent.MOBILE_CARD_PADDING,
  );

  /** itemSize handed to the CDK viewport: the mobile card height on mobile, else the desktop row. */
  readonly effectiveItemSize = computed<number>(() =>
    this.isMobile() ? this.mobileItemSize() : this.itemSize(),
  );

  readonly viewportHeight = computed<number>(() => {
    const rows = this.isMobile()
      ? Math.min(this.viewportRows(), DataTableComponent.MOBILE_VIEWPORT_ROWS)
      : this.viewportRows();
    return this.effectiveItemSize() * rows;
  });

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
