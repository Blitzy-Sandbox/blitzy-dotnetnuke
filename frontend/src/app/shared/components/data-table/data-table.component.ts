import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { DateFormatPipe, TruncatePipe, YesNoPipe } from '../../pipes';
import { HasPermissionDirective, TooltipDirective } from '../../directives';
import {
  ColumnDef,
  FilterChangeEvent,
  PageChangeEvent,
  RowAction,
  RowActionEvent,
  SortDirection,
  SortState,
} from './data-table.models';

// MIGRATION: The legacy DotNetNuke <asp:DataGrid AutoGenerateColumns="false"> server
// control (Website/admin/Portal/portals.ascx grdPortals, Website/admin/Users/users.ascx
// grdUsers, Website/admin/Security/roles.ascx grdRoles) becomes this Angular 19 standalone,
// generic, presentation-only component. It performs NO HTTP and NO business logic — feature
// components own data fetching and the actual edit/delete (AAP §0.7.1: Angular services handle
// API communication only). It only sorts/filters/pages in memory and emits user intents.
@Component({
  selector: 'app-data-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DateFormatPipe, YesNoPipe, TruncatePipe, TooltipDirective, HasPermissionDirective],
  template: `
    <div class="dt">
      @if (filterable()) {
        <!-- MIGRATION: users.ascx txtSearch + btnSearch (+ rptLetterSearch A–Z) -> one client filter box. Template-ref #searchBox avoids an @angular/forms dependency. -->
        <div class="dt__toolbar">
          <input
            #searchBox
            type="search"
            class="dt__search"
            placeholder="Search…"
            aria-label="Filter table"
            (input)="onFilter(searchBox.value)"
          />
        </div>
      }

      <!-- MIGRATION / F7 (responsive): horizontal-scroll wrapper so wide tables scroll within
           their own container on narrow viewports instead of overflowing the page layout. The
           toolbar and pager sit outside this wrapper so only the tabular data scrolls. -->
      <div class="dt__table-wrap">
      <table class="dt__table" role="grid" [attr.aria-busy]="loading() ? 'true' : 'false'">
        @if (caption()) {
          <caption class="dt__caption">{{ caption() }}</caption>
        }
        <thead role="rowgroup">
          <tr role="row">
            @for (col of columns(); track columnKey(col)) {
              <th
                role="columnheader"
                scope="col"
                [style.width]="col.width ?? null"
                [style.text-align]="col.align ?? null"
                [attr.aria-sort]="ariaSort(col)"
              >
                @if (isSortable(col)) {
                  <!-- MIGRATION / F6 (a11y): the sort arrow is aria-hidden, so the focused button
                       needs an explicit accessible name conveying both the sort action and the
                       current direction (the th's aria-sort is not announced on the inner button). -->
                  <button
                    type="button"
                    class="dt__sort-btn"
                    [attr.aria-label]="sortAriaLabel(col)"
                    (click)="onSort(col)"
                  >
                    <span>{{ col.header }}</span>
                    <span class="dt__sort-ind" aria-hidden="true">{{ sortIndicator(col) }}</span>
                  </button>
                } @else {
                  <span>{{ col.header }}</span>
                }
              </th>
            }
            @if (hasActions()) {
              <th role="columnheader" scope="col" class="dt__actions-head">{{ actionsHeader() }}</th>
            }
          </tr>
        </thead>
        <tbody role="rowgroup">
          @if (loading()) {
            <tr role="row">
              <td role="gridcell" class="dt__loading" [attr.colspan]="colSpan()">Loading…</td>
            </tr>
          } @else if (pagedData().length === 0) {
            <tr role="row">
              <td role="gridcell" class="dt__empty" [attr.colspan]="colSpan()">{{ emptyMessage() }}</td>
            </tr>
          } @else {
            @for (row of pagedData(); track trackRow(row)) {
              <tr role="row" class="dt__row">
                @for (col of columns(); track columnKey(col)) {
                  <td role="gridcell" [style.text-align]="col.align ?? null">
                    @switch (col.type) {
                      @case ('boolean') {
                        {{ getBoolean(row, col) | yesNo }}
                      }
                      @case ('date') {
                        {{ getDateValue(row, col) | dateFormat: (col.format ?? 'mediumDate') }}
                      }
                      @case ('currency') {
                        {{ formatCurrency(getNumber(row, col)) }}
                      }
                      @case ('number') {
                        {{ getNumber(row, col) }}
                      }
                      @default {
                        @if (col.truncate) {
                          {{ getText(row, col) | truncate: col.truncate }}
                        } @else {
                          {{ getText(row, col) }}
                        }
                      }
                    }
                  </td>
                }
                @if (hasActions()) {
                  <td role="gridcell" class="dt__actions-cell">
                    <span class="dt__actions">
                      @for (act of actions(); track act.action) {
                        @if (act.requiredRoles) {
                          <button
                            type="button"
                            class="dt__action-btn"
                            *appHasPermission="act.requiredRoles"
                            [appTooltip]="act.tooltip ?? act.label"
                            [attr.aria-label]="act.label"
                            (click)="triggerAction(act.action, row)"
                          >
                            @if (act.icon) {
                              <span [class]="act.icon" aria-hidden="true"></span>
                            } @else {
                              {{ act.label }}
                            }
                          </button>
                        } @else {
                          <button
                            type="button"
                            class="dt__action-btn"
                            [appTooltip]="act.tooltip ?? act.label"
                            [attr.aria-label]="act.label"
                            (click)="triggerAction(act.action, row)"
                          >
                            @if (act.icon) {
                              <span [class]="act.icon" aria-hidden="true"></span>
                            } @else {
                              {{ act.label }}
                            }
                          </button>
                        }
                      }
                    </span>
                  </td>
                }
              </tr>
            }
          }
        </tbody>
      </table>
      </div>

      @if (showPaging() && totalPages() > 1) {
        <!-- MIGRATION: dnn:pagingcontrol -> paging emitting pageChange. -->
        <div class="dt__pager">
          <button type="button" class="dt__pager-btn" (click)="prevPage()" [disabled]="currentPage() === 1" aria-label="Previous page">‹ Prev</button>
          <span class="dt__pager-info" aria-live="polite">Page {{ currentPage() }} of {{ totalPages() }}</span>
          <button type="button" class="dt__pager-btn" (click)="nextPage()" [disabled]="currentPage() === totalPages()" aria-label="Next page">Next ›</button>
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      color: var(--dt-fg, inherit);
    }
    .dt__toolbar {
      margin-bottom: 0.5rem;
    }
    .dt__search {
      width: 100%;
      max-width: 320px;
      padding: 0.375rem 0.5rem;
      border: 1px solid var(--dt-border, #ccc);
      border-radius: 4px;
      font: inherit;
    }
    /* MIGRATION / F7 (responsive): constrain the table to its container and allow horizontal
       scrolling on narrow viewports instead of overflowing the surrounding layout. */
    .dt__table-wrap {
      width: 100%;
      max-width: 100%;
      overflow-x: auto;
      -webkit-overflow-scrolling: touch;
    }
    .dt__table {
      width: 100%;
      border-collapse: collapse;
      font: inherit;
    }
    .dt__caption {
      padding: 0.25rem 0;
      text-align: left;
      font-weight: 600;
    }
    .dt__table th,
    .dt__table td {
      padding: 0.5rem 0.625rem;
      text-align: left;
      border-bottom: 1px solid var(--dt-border, #e0e0e0);
      vertical-align: middle;
    }
    .dt__table th {
      background: var(--dt-header-bg, #f5f5f5);
      font-weight: 600;
      white-space: nowrap;
    }
    .dt__sort-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0;
      background: none;
      border: 0;
      font: inherit;
      font-weight: 600;
      color: inherit;
      cursor: pointer;
    }
    .dt__sort-ind {
      font-size: 0.7em;
      opacity: 0.75;
    }
    .dt__row:hover {
      background: var(--dt-row-hover, #fafafa);
    }
    .dt__actions {
      display: inline-flex;
      gap: 0.25rem;
    }
    .dt__action-btn {
      padding: 0.125rem 0.5rem;
      background: none;
      border: 1px solid transparent;
      border-radius: 4px;
      color: var(--dt-action, #0066cc);
      font: inherit;
      cursor: pointer;
    }
    .dt__action-btn:hover,
    .dt__action-btn:focus {
      border-color: var(--dt-border, #ccc);
    }
    .dt__empty,
    .dt__loading {
      padding: 1rem;
      text-align: center;
      color: var(--dt-muted, #666);
    }
    .dt__pager {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .dt__pager-info {
      font-size: 0.875rem;
      color: var(--dt-muted, #666);
    }
    .dt__pager-btn {
      padding: 0.25rem 0.5rem;
      background: var(--dt-btn-bg, #fff);
      border: 1px solid var(--dt-border, #ccc);
      border-radius: 4px;
      font: inherit;
      cursor: pointer;
    }
    .dt__pager-btn:disabled {
      opacity: 0.5;
      cursor: default;
    }
  `],
})
export class DataTableComponent<T> {
  // ---- Inputs (public API; input() signals) ----
  readonly data = input<T[]>([]);
  readonly columns = input<ColumnDef<T>[]>([]);
  readonly loading = input(false);
  readonly pageSize = input(10);
  readonly filterable = input(true);
  readonly sortable = input(true);
  readonly showPaging = input(true);
  readonly rowKey = input<keyof T | undefined>(undefined);
  readonly trackBy = input<((row: T) => unknown) | undefined>(undefined);
  readonly actions = input<RowAction[]>([]);
  readonly emptyMessage = input('No records found.');
  readonly caption = input('');
  readonly actionsHeader = input('Actions');

  // ---- Outputs (public API) ----
  // MIGRATION: dnn:imagecommandcolumn CommandName -> rowAction; grid sort/filter/paging intents.
  readonly rowAction = output<RowActionEvent<T>>();
  readonly sortChange = output<SortState<T>>();
  readonly filterChange = output<FilterChangeEvent>();
  readonly pageChange = output<PageChangeEvent>();

  // ---- Internal state ----
  private readonly sortState = signal<SortState<T> | null>(null);
  private readonly filterTerm = signal('');
  protected readonly currentPage = signal(1);

  // ---- Derived state (computed) ----
  private readonly filteredData = computed<T[]>(() => {
    const term = this.filterTerm().trim().toLowerCase();
    const rows = this.data();
    if (!this.filterable() || term === '') {
      return rows;
    }
    const cols = this.columns();
    return rows.filter((row) =>
      cols.some((col) => this.getText(row, col).toLowerCase().includes(term)),
    );
  });

  private readonly sortedData = computed<T[]>(() => {
    const rows = this.filteredData();
    const sort = this.sortState();
    if (!this.sortable() || sort === null) {
      return rows;
    }
    const field = sort.field;
    const factor = sort.direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => factor * this.compareValues(a[field], b[field]));
  });

  protected readonly pagedData = computed<T[]>(() => {
    const rows = this.sortedData();
    if (!this.showPaging()) {
      return rows;
    }
    const size = Math.max(1, this.pageSize());
    const totalPages = Math.max(1, Math.ceil(rows.length / size));
    const page = Math.min(this.currentPage(), totalPages);
    const start = (page - 1) * size;
    return rows.slice(start, start + size);
  });

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.sortedData().length / Math.max(1, this.pageSize()))),
  );

  protected readonly hasActions = computed(() => this.actions().length > 0);
  protected readonly colSpan = computed(() => this.columns().length + (this.hasActions() ? 1 : 0));

  // ---- Sorting ----
  // MIGRATION: column header click -> client sort + sortChange emit (server-driven mode: host reacts to sortChange).
  protected isSortable(col: ColumnDef<T>): boolean {
    return this.sortable() && col.sortable === true;
  }

  protected onSort(col: ColumnDef<T>): void {
    if (!this.isSortable(col)) {
      return;
    }
    const current = this.sortState();
    let direction: SortDirection = 'asc';
    if (current !== null && current.field === col.field) {
      direction = current.direction === 'asc' ? 'desc' : 'asc';
    }
    const next: SortState<T> = { field: col.field, direction };
    this.sortState.set(next);
    this.currentPage.set(1);
    this.sortChange.emit(next);
  }

  protected sortIndicator(col: ColumnDef<T>): string {
    const sort = this.sortState();
    if (sort === null || sort.field !== col.field) {
      return '';
    }
    return sort.direction === 'asc' ? '\u25B2' : '\u25BC';
  }

  protected ariaSort(col: ColumnDef<T>): 'ascending' | 'descending' | 'none' | null {
    if (!this.isSortable(col)) {
      return null;
    }
    const sort = this.sortState();
    if (sort === null || sort.field !== col.field) {
      return 'none';
    }
    return sort.direction === 'asc' ? 'ascending' : 'descending';
  }

  // MIGRATION / F6 (a11y): accessible name for the sortable-column button. The visible sort
  // arrow is aria-hidden and the th's aria-sort is not conveyed on the inner <button>, so the
  // button itself must announce the sort action and, when active, the current direction.
  protected sortAriaLabel(col: ColumnDef<T>): string {
    const sort = this.sortState();
    if (sort === null || sort.field !== col.field) {
      return `Sort by ${col.header}`;
    }
    return sort.direction === 'asc'
      ? `Sort by ${col.header}, currently sorted ascending`
      : `Sort by ${col.header}, currently sorted descending`;
  }

  // ---- Filtering ----
  // MIGRATION: users.ascx txtSearch/btnSearch + rptLetterSearch A–Z -> single client filter box + filterChange emit.
  protected onFilter(term: string): void {
    this.filterTerm.set(term);
    this.currentPage.set(1);
    this.filterChange.emit({ term });
  }

  // ---- Paging ----
  // MIGRATION: dnn:pagingcontrol -> client paging + pageChange emit.
  protected goToPage(page: number): void {
    const total = this.totalPages();
    const clamped = Math.min(Math.max(1, page), total);
    if (clamped === this.currentPage()) {
      return;
    }
    this.currentPage.set(clamped);
    this.pageChange.emit({ page: clamped, pageSize: this.pageSize() });
  }

  protected prevPage(): void {
    this.goToPage(this.currentPage() - 1);
  }

  protected nextPage(): void {
    this.goToPage(this.currentPage() + 1);
  }

  // ---- Row actions ----
  protected triggerAction(action: string, row: T): void {
    this.rowAction.emit({ action, row });
  }

  // ---- Row / column tracking ----
  protected trackRow(row: T): unknown {
    const tb = this.trackBy();
    if (tb !== undefined) {
      return tb(row);
    }
    const key = this.rowKey();
    if (key !== undefined) {
      return row[key];
    }
    return row;
  }

  protected columnKey(col: ColumnDef<T>): string {
    return String(col.field);
  }

  // ---- Cell value access & typed getters (keep pipes strict-template-safe) ----
  private getCellValue(row: T, col: ColumnDef<T>): unknown {
    const accessor = col.value;
    if (accessor !== undefined) {
      return accessor(row);
    }
    return row[col.field];
  }

  protected getText(row: T, col: ColumnDef<T>): string {
    const value = this.getCellValue(row, col);
    if (value === null || value === undefined) {
      return '';
    }
    return String(value);
  }

  protected getNumber(row: T, col: ColumnDef<T>): number | null {
    const value = this.getCellValue(row, col);
    if (typeof value === 'number') {
      return value;
    }
    if (typeof value === 'string' && value.trim() !== '') {
      const parsed = Number(value);
      return Number.isNaN(parsed) ? null : parsed;
    }
    return null;
  }

  protected getBoolean(row: T, col: ColumnDef<T>): boolean | null {
    const value = this.getCellValue(row, col);
    if (typeof value === 'boolean') {
      return value;
    }
    return null;
  }

  protected getDateValue(row: T, col: ColumnDef<T>): string | number | Date | null {
    const value = this.getCellValue(row, col);
    if (value === null || value === undefined) {
      return null;
    }
    if (typeof value === 'string' || typeof value === 'number' || value instanceof Date) {
      return value;
    }
    return null;
  }

  // MIGRATION: DataFormatString "{0:0.00}" (portals.ascx HostFee, roles.ascx ServiceFee/TrialFee) -> fixed 2-decimal formatting.
  protected formatCurrency(value: number | null): string {
    if (value === null) {
      return '';
    }
    return value.toFixed(2);
  }

  // ---- Sorting comparator ----
  private compareValues(a: unknown, b: unknown): number {
    if (a === b) {
      return 0;
    }
    if (a === null || a === undefined) {
      return -1;
    }
    if (b === null || b === undefined) {
      return 1;
    }
    if (typeof a === 'number' && typeof b === 'number') {
      return a - b;
    }
    if (typeof a === 'boolean' && typeof b === 'boolean') {
      return a === b ? 0 : a ? 1 : -1;
    }
    return String(a).localeCompare(String(b));
  }
}
