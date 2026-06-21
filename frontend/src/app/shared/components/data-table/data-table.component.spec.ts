import { ComponentFixture, TestBed, fakeAsync, flush, discardPeriodicTasks } from '@angular/core/testing';
import { WritableSignal, signal } from '@angular/core';
import { DataTableAction, DataTableColumn, DataTableComponent } from './data-table.component';
import { AuthService } from '../../../core/auth/auth.service';
import { User } from '../../../core/models/user.model';

interface FakeAuthService {
  currentUser: WritableSignal<User | null>;
  hasRole: jasmine.Spy<(role: string) => boolean>;
}

function buildUser(roles: string[] = [], isSuperUser = false): User {
  return {
    userID: 1,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    portalID: 0,
    isSuperUser,
    roles,
  };
}

describe('DataTableComponent', () => {
  let fixture: ComponentFixture<DataTableComponent<User>>;
  let component: DataTableComponent<User>;
  let auth: FakeAuthService;

  const columns: DataTableColumn<User>[] = [
    { key: 'username', header: 'Username', sortable: true },
    { key: 'email', header: 'Email' },
  ];

  beforeEach(async () => {
    auth = {
      currentUser: signal<User | null>(buildUser([], true)),
      hasRole: jasmine.createSpy('hasRole').and.returnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [DataTableComponent],
      providers: [{ provide: AuthService, useValue: auth }],
    }).compileComponents();

    fixture = TestBed.createComponent(DataTableComponent) as ComponentFixture<DataTableComponent<User>>;
    component = fixture.componentInstance;
    fixture.componentRef.setInput('columns', columns);
  });

  it('creates', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('shows only visible columns', () => {
    const withHidden: DataTableColumn<User>[] = [
      ...columns,
      { key: 'secret', header: 'Secret', visible: false },
    ];
    fixture.componentRef.setInput('columns', withHidden);
    fixture.detectChanges();
    expect(component.visibleColumns().length).toBe(2);
  });

  it('derives paging from meta and hides the pager for a single page', () => {
    fixture.componentRef.setInput('meta', { pageIndex: 0, pageSize: 10, totalCount: 5, totalPages: 1 });
    fixture.detectChanges();
    expect(component.totalPages()).toBe(1);
    expect(component.showPager()).toBeFalse();
  });

  it('computes totalPages from count and size when not provided', () => {
    fixture.componentRef.setInput('meta', { pageIndex: 0, pageSize: 10, totalCount: 25 });
    fixture.detectChanges();
    expect(component.totalPages()).toBe(3);
    expect(component.showPager()).toBeTrue();
  });

  it('emits filterChange', () => {
    const spy = jasmine.createSpy('filterChange');
    component.filterChange.subscribe(spy);
    component.onFilter('B');
    expect(spy).toHaveBeenCalledWith('B');
  });

  it('emits searchChange with the active search type', () => {
    fixture.componentRef.setInput('searchTypes', ['Email', 'Username']);
    fixture.detectChanges();
    const spy = jasmine.createSpy('searchChange');
    component.searchChange.subscribe(spy);
    component.onSearchTypeChange('Username');
    component.onSearch('abc');
    expect(spy).toHaveBeenCalledWith({ text: 'abc', type: 'Username' });
  });

  it('toggles sort direction on repeated sorts', () => {
    const spy = jasmine.createSpy('sortChange');
    component.sortChange.subscribe(spy);
    const first = columns[0];
    component.onSort(first);
    expect(spy).toHaveBeenCalledWith({ key: 'username', direction: 'asc' });
  });

  it('does not emit page changes outside the valid range', () => {
    fixture.componentRef.setInput('meta', { pageIndex: 0, pageSize: 10, totalCount: 25 });
    fixture.detectChanges();
    const spy = jasmine.createSpy('pageChange');
    component.pageChange.subscribe(spy);
    component.onPage(-1);
    component.onPage(99);
    expect(spy).not.toHaveBeenCalled();
    component.onPage(1);
    expect(spy).toHaveBeenCalledWith(1);
  });

  it('emits rowClick', () => {
    const user = buildUser();
    const spy = jasmine.createSpy('rowClick');
    component.rowClick.subscribe(spy);
    component.onRowClick(user);
    expect(spy).toHaveBeenCalledWith(user);
  });

  it('formats display values', () => {
    const user = buildUser(['Administrators']);
    const first = columns[0];
    expect(component.displayValue(user, first)).toBe('jdoe');
  });

  it('renders rows with permission-gated action buttons', fakeAsync(() => {
    const actions: DataTableAction<User>[] = [{ id: 'edit', label: 'Edit', permission: 'EDIT' }];
    fixture.componentRef.setInput('rows', [buildUser(['Administrators'])]);
    fixture.componentRef.setInput('actions', actions);
    // MIGRATION: rows render through CDK *cdkVirtualFor. The viewport measures itself and
    // computes its rendered range across deferred async cycles, so we attach the fixture to
    // the live DOM (for a real measured height) and drain those cycles with detectChanges +
    // flush, mirroring CDK's own finishInit pattern, so the *appHasPermission action button
    // (and its role check) is materialized before we assert.
    document.body.appendChild(fixture.nativeElement);
    try {
      fixture.detectChanges();
      flush();
      fixture.detectChanges();
      flush();
      fixture.detectChanges();
      expect(auth.hasRole).toHaveBeenCalled();
    } finally {
      discardPeriodicTasks();
      fixture.nativeElement.remove();
    }
  }));
});
