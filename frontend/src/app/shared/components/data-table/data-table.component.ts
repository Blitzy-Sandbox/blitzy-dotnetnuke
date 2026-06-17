import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ApiResponseMeta } from '../../../core/services/api.service';
import { DateFormatPipe } from '../../pipes';
import { TooltipDirective } from '../../directives/tooltip';
import { HasPermissionDirective, type PermissionKey } from '../../directives/has-permission';
import { IconComponent, type IconName } from '../icon';

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
  icon?: IconName;
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
  imports: [ScrollingModule, DateFormatPipe, TooltipDirective, HasPermissionDirective, IconComponent],
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
  /**
   * Accessible name for the table and its horizontal-scroll region. Unlike
   * `caption` (which renders a VISIBLE caption row), `label` is used only for
   * assistive technology, so list screens that already show a visible page
   * heading can name the scrollable grid without duplicating that heading
   * on-screen. Consumed by `scrollRegionLabel` and the grid's `aria-label`.
   */
  readonly label = input<string>('');
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

  // ---------------------------------------------------------------------------
  // Shared column geometry (QA Issues #3 & #4).
  //
  // The grid is laid out with CSS Grid so the header row and every body row
  // share a single set of column tracks. Previously each row was an independent
  // flexbox, so a row with fewer action buttons let its data cells grow wider,
  // pushing the same logical column to a different x-position on every row
  // (Issue #3). The actions column is sized to a single fixed width wide enough
  // for the worst case (all declared actions visible), so columns stay aligned
  // regardless of how many actions a given row shows. A computed minimum width
  // lets the whole grid overflow horizontally inside a scroll container on
  // narrow viewports instead of clipping the action buttons off-screen
  // (Issue #4).
  // ---------------------------------------------------------------------------

  /** Minimum px width for a data column before horizontal scrolling engages. */
  private static readonly DATA_COLUMN_MIN = 96;

  /**
   * Fixed px width reserved for the trailing actions column. Sized to fit ALL
   * declared actions shown at once (the widest possible row) using a simple
   * character-width heuristic, so 1-action and 3-action rows align identically.
   */
  readonly actionsTrackWidth = computed<number>(() => {
    const actions = this.actions();
    if (actions.length === 0) {
      return 0;
    }
    const CHAR_PX = 7.5; // approx advance width per char at 0.875rem system font
    const ICON_PX = 20; // glyph box (1em) + spacing allowance
    const ICON_GAP_PX = 4; // gap between icon and label
    const BTN_PAD_BORDER_PX = 18; // 0.5rem*2 padding + 1px*2 border
    const BTN_GAP_PX = 4; // gap between adjacent action buttons
    const CELL_PAD_PX = 24; // 0.75rem*2 cell padding
    let width = CELL_PAD_PX + Math.max(0, actions.length - 1) * BTN_GAP_PX;
    for (const action of actions) {
      width +=
        BTN_PAD_BORDER_PX +
        (action.icon ? ICON_PX + ICON_GAP_PX : 0) +
        Math.ceil(action.label.length * CHAR_PX);
    }
    return Math.ceil(width);
  });

  /** `grid-template-columns` value shared by the header and all body rows. */
  readonly gridTemplate = computed<string>(() => {
    const tracks = this.visibleColumns().map(
      () => `minmax(${DataTableComponent.DATA_COLUMN_MIN}px, 1fr)`,
    );
    if (this.hasActions()) {
      tracks.push(`${this.actionsTrackWidth()}px`);
    }
    return tracks.join(' ');
  });

  /** Minimum px width of the whole grid (drives horizontal scroll at <breakpoint). */
  readonly gridMinWidth = computed<number>(() => {
    const dataMin = this.visibleColumns().length * DataTableComponent.DATA_COLUMN_MIN;
    return dataMin + (this.hasActions() ? this.actionsTrackWidth() : 0);
  });

  /**
   * Accessible name for the horizontal-scroll region wrapper (`.dt-scroll`).
   *
   * The `.dt-scroll` element is made keyboard-focusable (`tabindex="0"`) and is
   * exposed as a `role="region"` so keyboard-only and screen-reader users can
   * reach columns — including the trailing Actions column — that overflow the
   * viewport on narrow screens (QA responsive DataTable finding). A focusable
   * region must have an accessible name, so this falls back through
   * `label -> caption -> 'Data table'` to guarantee a non-empty label.
   */
  readonly scrollRegionLabel = computed<string>(() => {
    const explicit = this.label().trim();
    if (explicit.length > 0) {
      return explicit;
    }
    const caption = this.caption().trim();
    return caption.length > 0 ? caption : 'Data table';
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

  /**
   * Maps a column's logical text alignment to a flexbox `justify-content`
   * value. Cells are flex containers (so an icon/label can sit centred
   * vertically), which means `text-align` alone does not move a single
   * content item horizontally; `justify-content` does. Header and body cells
   * use the same value per column so a right-aligned numeric column lines up
   * header-to-body.
   */
  cellJustify(column: DataTableColumn<T>): 'flex-start' | 'center' | 'flex-end' {
    switch (column.align) {
      case 'right':
        return 'flex-end';
      case 'center':
        return 'center';
      default:
        return 'flex-start';
    }
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
