import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { DataTableComponent } from './data-table.component';
import {
  ColumnDef,
  FilterChangeEvent,
  PageChangeEvent,
  RowAction,
  RowActionEvent,
  SortState,
} from './data-table.models';

interface TestRow {
  id: number;
  name: string;
  fee: number;
  active: boolean;
  created: string;
}

describe('DataTableComponent', () => {
  let fixture: ComponentFixture<DataTableComponent<TestRow>>;
  let component: DataTableComponent<TestRow>;

  const columns: ColumnDef<TestRow>[] = [
    { field: 'name', header: 'Name', sortable: true, type: 'text' },
    { field: 'fee', header: 'Fee', sortable: true, type: 'currency' },
    { field: 'active', header: 'Active', type: 'boolean' },
    { field: 'created', header: 'Created', type: 'date' },
  ];

  const rows: TestRow[] = [
    { id: 1, name: 'Charlie', fee: 30, active: true, created: '2020-03-01' },
    { id: 2, name: 'Alpha', fee: 10.5, active: false, created: '2020-01-01' },
    { id: 3, name: 'Bravo', fee: 20, active: true, created: '2020-02-01' },
    { id: 4, name: 'Delta', fee: 40, active: false, created: '2020-04-01' },
    { id: 5, name: 'Echo', fee: 50, active: true, created: '2020-05-01' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DataTableComponent],
    }).compileComponents();

    // DataTableComponent is generic; createComponent yields ComponentFixture<DataTableComponent<unknown>>.
    // Cast through `unknown` to the TestRow-typed fixture (always compiles under strict mode).
    fixture = TestBed.createComponent(DataTableComponent) as unknown as ComponentFixture<
      DataTableComponent<TestRow>
    >;
    component = fixture.componentInstance;
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('data', rows);
    fixture.componentRef.setInput('rowKey', 'id');
    fixture.componentRef.setInput('pageSize', 3);
    fixture.detectChanges();
  });

  function bodyRows(): HTMLElement[] {
    return Array.from(
      fixture.nativeElement.querySelectorAll('tbody tr[role="row"]'),
    ) as HTMLElement[];
  }

  function firstCellText(row: HTMLElement): string {
    const cell = row.querySelector('td[role="gridcell"]');
    return (cell?.textContent ?? '').trim();
  }

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('renders ARIA grid roles', () => {
    expect(fixture.nativeElement.querySelector('[role="grid"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('thead[role="rowgroup"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('[role="columnheader"]').length).toBe(
      columns.length,
    );
    expect(
      fixture.nativeElement.querySelectorAll('tbody tr[role="row"] td[role="gridcell"]').length,
    ).toBeGreaterThan(0);
  });

  it('pages the in-memory data using pageSize', () => {
    expect(bodyRows().length).toBe(3);
  });

  it('emits pageChange and advances the page when Next is clicked', () => {
    let event: PageChangeEvent | undefined;
    component.pageChange.subscribe((e) => (event = e));

    const pagerBtns = fixture.debugElement.queryAll(By.css('.dt__pager-btn'));
    const nextBtn = pagerBtns[pagerBtns.length - 1].nativeElement as HTMLButtonElement;
    nextBtn.click();
    fixture.detectChanges();

    expect(event).toEqual({ page: 2, pageSize: 3 });
    expect(bodyRows().length).toBe(2);
  });

  it('sorts ascending then descending on header click and emits sortChange', () => {
    const captured: SortState<TestRow>[] = [];
    component.sortChange.subscribe((e) => captured.push(e));

    const nameHeaderBtn = fixture.debugElement.queryAll(By.css('thead .dt__sort-btn'))[0]
      .nativeElement as HTMLButtonElement;

    nameHeaderBtn.click();
    fixture.detectChanges();
    expect(captured[0]).toEqual({ field: 'name', direction: 'asc' });
    expect(firstCellText(bodyRows()[0])).toBe('Alpha');

    nameHeaderBtn.click();
    fixture.detectChanges();
    expect(captured[1]).toEqual({ field: 'name', direction: 'desc' });
    expect(firstCellText(bodyRows()[0])).toBe('Echo');
  });

  it('filters rows via the search box and emits filterChange', () => {
    const captured: FilterChangeEvent[] = [];
    component.filterChange.subscribe((e) => captured.push(e));

    const searchInput = fixture.debugElement.query(By.css('.dt__search'))
      .nativeElement as HTMLInputElement;
    searchInput.value = 'alp';
    searchInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(captured[captured.length - 1]).toEqual({ term: 'alp' });
    expect(bodyRows().length).toBe(1);
    expect(firstCellText(bodyRows()[0])).toBe('Alpha');
  });

  it('emits rowAction with the clicked row when an action button is clicked', () => {
    const actions: RowAction[] = [{ action: 'edit', label: 'Edit' }];
    fixture.componentRef.setInput('actions', actions);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.detectChanges();

    let event: RowActionEvent<TestRow> | undefined;
    component.rowAction.subscribe((e) => (event = e));

    const actionBtn = fixture.debugElement.query(By.css('tbody .dt__action-btn'))
      .nativeElement as HTMLButtonElement;
    actionBtn.click();

    expect(event).toBeTruthy();
    expect(event?.action).toBe('edit');
    expect(event?.row.id).toBe(rows[0].id);
  });
});
