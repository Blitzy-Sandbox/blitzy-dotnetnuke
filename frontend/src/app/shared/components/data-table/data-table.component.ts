// MIGRATION: Derived from the legacy DNN admin grid Website/admin/Portal/Portals.ascx.vb
// (corroborated by Website/admin/Users/Users.ascx.vb, identical paging logic). The Web Forms
// DataGrid + PagingControl + letter-search Repeater are re-expressed as a generic, presentational,
// standalone Angular 19 data-table that communicates purely via signal input()/output(). No VB file
// is edited; ViewState/postback machinery (Handles, ItemDataBound, DeleteCommand) is discarded.
import {
  ChangeDetectionStrategy,
  Component,
  TemplateRef,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';

import { TruncatePipe } from '../../pipes/truncate.pipe';
import { YesNoPipe } from '../../pipes/yes-no.pipe';

/**
 * Column definition for {@link DataTableComponent}, generic over the row type `T`.
 *
 * MIGRATION: replaces the per-column DataGrid `BoundColumn`/`TemplateColumn` definitions and the
 * legacy `Format*` helpers (e.g. Portals.ascx.vb `FormatExpiryDate`) with a declarative, typed
 * column model. `cell` renders a custom template (the equivalent of a `TemplateColumn`); `truncate`
 * and `yesNo` opt the default cell into the shared display pipes for long free-text / boolean values.
 */
export interface ColumnDef<T> {
  /** Property name on the row to render (a `keyof T`, or a free string for projected values). */
  key: keyof T | string;
  /** Header text rendered inside `<th scope="col">`. */
  header: string;
  /** Optional custom-cell template; receives `{ $implicit: row, value, column }` as its context. */
  cell?: TemplateRef<unknown>;
  /** When set, the default cell renders the value through the shared `truncate` pipe at this length. */
  truncate?: number;
  /** When true, the default cell renders the boolean value through the shared `yesNo` pipe. */
  yesNo?: boolean;
}

/**
 * Generic, presentational data grid: list + paging + free-text/category filtering + row actions.
 *
 * Presentational-only (AAP Section 0.3.6/0.3.7): NO HttpClient, NO Router, NO business logic and NO
 * `@angular/cdk`. All I/O flows through signal `input()`/`output()`. Virtual scrolling is achieved
 * with a native scroll container plus `@for` + a stable `track` under OnPush change detection.
 */
@Component({
  selector: 'app-data-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, TruncatePipe, YesNoPipe],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent<T> {
  // ----- Inputs (signal-based) -----
  /** The current page of rows to render. */
  readonly rows = input<T[]>([]);
  /** Column definitions describing how each cell is rendered. */
  readonly columns = input<ColumnDef<T>[]>([]);
  /** Total number of records across all pages (drives the pager). */
  readonly totalRecords = input(0);
  // MIGRATION: default page size 20 mirrors the legacy `PageSize` ReadOnly property
  // (Portals.ascx.vb L92-98 / Users.ascx.vb L114, both hard-return 20).
  readonly pageSize = input(20);
  // MIGRATION: zero-based page index on the wire. The legacy control passed `CurrentPage - 1`
  // to the data layer (Portals.ascx.vb L142 / Users.ascx.vb L265), matching core/models
  // Paged<T>.pageIndex (zero-based). `pageChange` emits this same zero-based index.
  readonly currentPage = input(0);
  // MIGRATION: the SPECIFIC filter values (legacy A-Z + "All" + "Expired" built by
  // CreateLetterSearch(), Portals.ascx.vb L170-179) are PASSED IN by the feature, never hardcoded here.
  readonly filters = input<string[]>([]);
  /** Optional stable track key (a property name on the row); falls back to the row index. */
  readonly rowKey = input<keyof T | string | null>(null);
  // MIGRATION: row-action visibility (legacy hid Delete for the current portal, Portals.ascx.vb L423)
  // is the FEATURE's concern. These presentational toggles let a feature hide whole action buttons;
  // per-row conditional hiding is done by the feature via a custom `cell` template.
  readonly showView = input(true);
  readonly showEdit = input(true);
  readonly showDelete = input(true);
  /** Empty-state message rendered when there are no rows. */
  readonly emptyMessage = input('No records found.');

  // ----- Outputs (signal-based) -----
  // MIGRATION: emits a ZERO-BASED page index (see `currentPage`).
  readonly pageChange = output<number>();
  /** Emits the active free-text or category filter value. */
  readonly filterChange = output<string>();
  // MIGRATION: row actions are emit-only (the feature decides what to do / what is visible).
  readonly edit = output<T>();
  readonly delete = output<T>();
  readonly view = output<T>();

  // ----- Local presentational state -----
  /** The currently active filter value; used only to highlight the matching category button. */
  protected readonly activeFilter = signal<string | null>(null);

  // ----- Derived state -----
  protected readonly totalPages = computed(() => {
    const size = this.pageSize();
    return size > 0 ? Math.ceil(this.totalRecords() / size) : 0;
  });
  // MIGRATION: pager suppression. Legacy `ctlPagingControl.Visible = (PageSize < TotalRecords)`
  // (Portals.ascx.vb L155-156 / Users.ascx.vb L278-279) -> hide the pager when pageSize >= totalRecords.
  protected readonly showPager = computed(() => this.pageSize() < this.totalRecords());
  protected readonly canPrevious = computed(() => this.currentPage() > 0);
  protected readonly canNext = computed(() => this.currentPage() < this.totalPages() - 1);
  protected readonly hasActions = computed(
    () => this.showView() || this.showEdit() || this.showDelete(),
  );
  protected readonly columnCount = computed(
    () => this.columns().length + (this.hasActions() ? 1 : 0),
  );

  // ----- Track / cell helpers -----
  protected trackRow(index: number, row: T): unknown {
    const key = this.rowKey();
    if (key !== null) {
      const value = (row as Record<string, unknown>)[String(key)];
      if (value !== undefined && value !== null) {
        return value;
      }
    }
    return index;
  }

  protected cellValue(row: T, col: ColumnDef<T>): unknown {
    return (row as Record<string, unknown>)[String(col.key)];
  }

  protected cellContext(
    row: T,
    col: ColumnDef<T>,
  ): { $implicit: T; value: unknown; column: ColumnDef<T> } {
    return { $implicit: row, value: this.cellValue(row, col), column: col };
  }

  protected asString(value: unknown): string {
    return value === null || value === undefined ? '' : String(value);
  }

  protected asBoolean(value: unknown): boolean {
    return value === true;
  }

  // ----- Filter handlers -----
  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.activeFilter.set(value);
    this.filterChange.emit(value);
  }

  protected onFilterSelect(value: string): void {
    this.activeFilter.set(value);
    this.filterChange.emit(value);
  }

  // ----- Pager handlers (all emit ZERO-BASED indices) -----
  protected onFirst(): void {
    if (this.currentPage() > 0) {
      this.pageChange.emit(0);
    }
  }

  protected onPrevious(): void {
    if (this.currentPage() > 0) {
      this.pageChange.emit(this.currentPage() - 1);
    }
  }

  protected onNext(): void {
    if (this.currentPage() < this.totalPages() - 1) {
      this.pageChange.emit(this.currentPage() + 1);
    }
  }

  protected onLast(): void {
    const last = this.totalPages() - 1;
    if (this.currentPage() < last) {
      this.pageChange.emit(last);
    }
  }

  // ----- Row-action handlers (emit-only) -----
  protected onView(row: T): void {
    this.view.emit(row);
  }

  protected onEdit(row: T): void {
    this.edit.emit(row);
  }

  protected onDelete(row: T): void {
    this.delete.emit(row);
  }
}
