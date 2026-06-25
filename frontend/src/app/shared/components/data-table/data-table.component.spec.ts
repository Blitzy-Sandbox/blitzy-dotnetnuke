// MIGRATION: Net-new Jasmine unit tests (Gate 4) for the net-new DataTableComponent. The component
// is presentational, so tests use TestBed with the standalone component only — NO HttpClient, NO
// animations and NO Router providers. The behavioural assertions mirror the legacy paging contract
// from Website/admin/Portal/Portals.ascx.vb (default page size 20, zero-based paging, pager suppression).
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataTableComponent } from './data-table.component';
import type { ColumnDef } from './data-table.component';

interface TestRow {
  id: number;
  name: string;
  active: boolean;
}

describe('DataTableComponent', () => {
  let fixture: ComponentFixture<DataTableComponent<unknown>>;
  let component: DataTableComponent<unknown>;

  const columns: ColumnDef<TestRow>[] = [
    { key: 'id', header: 'ID' },
    { key: 'name', header: 'Name' },
    { key: 'active', header: 'Active', yesNo: true },
  ];

  const rows: TestRow[] = [
    { id: 1, name: 'Alpha', active: true },
    { id: 2, name: 'Beta', active: false },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DataTableComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DataTableComponent);
    component = fixture.componentInstance;
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('defaults the page size to 20 (legacy PageSize ReadOnly property)', () => {
    expect(component.pageSize()).toBe(20);
  });

  it('defaults the current page to 0 (zero-based)', () => {
    expect(component.currentPage()).toBe(0);
  });

  it('hides the pager when pageSize >= totalRecords', () => {
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalRecords', 20);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dt-pager')).toBeNull();
  });

  it('shows the pager when pageSize < totalRecords', () => {
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalRecords', 100);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dt-pager')).not.toBeNull();
  });

  it('emits a zero-based page index when navigating Next', () => {
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalRecords', 100); // 5 pages (index 0..4)
    fixture.componentRef.setInput('currentPage', 2);
    fixture.detectChanges();

    let emitted: number | undefined;
    component.pageChange.subscribe((value: number) => (emitted = value));

    const next = fixture.nativeElement.querySelector('[aria-label="Next page"]') as HTMLButtonElement;
    next.click();

    expect(emitted).toBe(3);
  });

  it('emits a zero-based page index when navigating Previous', () => {
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalRecords', 100);
    fixture.componentRef.setInput('currentPage', 2);
    fixture.detectChanges();

    let emitted: number | undefined;
    component.pageChange.subscribe((value: number) => (emitted = value));

    const prev = fixture.nativeElement.querySelector('[aria-label="Previous page"]') as HTMLButtonElement;
    prev.click();

    expect(emitted).toBe(1);
  });

  it('disables Previous/First on the first page', () => {
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('totalRecords', 100);
    fixture.componentRef.setInput('currentPage', 0);
    fixture.detectChanges();

    const prev = fixture.nativeElement.querySelector('[aria-label="Previous page"]') as HTMLButtonElement;
    const first = fixture.nativeElement.querySelector('[aria-label="First page"]') as HTMLButtonElement;
    expect(prev.disabled).toBeTrue();
    expect(first.disabled).toBeTrue();
  });

  it('emits the selected category value via filterChange', () => {
    fixture.componentRef.setInput('filters', ['A', 'B', 'All']);
    fixture.detectChanges();

    let emitted: string | undefined;
    component.filterChange.subscribe((value: string) => (emitted = value));

    const firstFilter = fixture.nativeElement.querySelector('.dt-filter') as HTMLButtonElement;
    firstFilter.click();

    expect(emitted).toBe('A');
  });

  it('emits the free-text value via filterChange', () => {
    fixture.detectChanges();

    let emitted: string | undefined;
    component.filterChange.subscribe((value: string) => (emitted = value));

    const search = fixture.nativeElement.querySelector('.dt-search') as HTMLInputElement;
    search.value = 'portal';
    search.dispatchEvent(new Event('input'));

    expect(emitted).toBe('portal');
  });

  it('emits the row via view/edit/delete on action clicks', () => {
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('rowKey', 'id');
    fixture.detectChanges();

    let viewed: unknown;
    let edited: unknown;
    let deleted: unknown;
    component.view.subscribe((row: unknown) => (viewed = row));
    component.edit.subscribe((row: unknown) => (edited = row));
    component.delete.subscribe((row: unknown) => (deleted = row));

    (fixture.nativeElement.querySelector('.dt-action--view') as HTMLButtonElement).click();
    (fixture.nativeElement.querySelector('.dt-action--edit') as HTMLButtonElement).click();
    (fixture.nativeElement.querySelector('.dt-action--delete') as HTMLButtonElement).click();

    expect(viewed).toEqual(rows[0]);
    expect(edited).toEqual(rows[0]);
    expect(deleted).toEqual(rows[0]);
  });

  it('renders one header per column plus an actions header', () => {
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();

    const headerCells = fixture.nativeElement.querySelectorAll('thead th');
    expect(headerCells.length).toBe(4); // 3 columns + actions

    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('renders the empty-state row when there are no rows', () => {
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('rows', []);
    fixture.detectChanges();

    const empty = fixture.nativeElement.querySelector('.dt-empty');
    expect(empty).not.toBeNull();
    expect(empty.textContent.trim()).toBe('No records found.');
  });

  it('renders a boolean column through the yesNo pipe', () => {
    fixture.componentRef.setInput('columns', columns);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();

    const firstRowCells = fixture.nativeElement.querySelectorAll('tbody tr:first-child td');
    expect(firstRowCells[2].textContent.trim()).toBe('Yes');
  });
});
