import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { User } from '../../../core/models';
import { DEFAULT_LIST_PAGE_SIZE } from '../../../core/services/api.service';
import { DataTableComponent, PageChangeEvent, RowActionEvent } from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { UserService } from '../user.service';
import { UserListComponent } from './user-list.component';

/** Minimal structural views of the child components' verified public contracts. */
interface DataTableLike {
  data(): unknown[];
  rowAction: { emit(event: RowActionEvent<{ userID: number }>): void };
  pageChange: { emit(event: PageChangeEvent): void };
}
interface ConfirmationDialogLike {
  confirm: { emit(): void };
}

function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 1,
    portalID: 0,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    isSuperUser: false,
    affiliateID: 0,
    roles: [],
    membership: {
      approved: true,
      lockedOut: false,
      isOnLine: true,
      updatePassword: false,
      createdDate: '2020-01-01T00:00:00Z',
      lastLoginDate: '2020-06-01T00:00:00Z',
      lastActivityDate: '2020-06-01T00:00:00Z',
      lastLockoutDate: '',
      lastPasswordChangeDate: '2020-01-01T00:00:00Z',
    },
    profile: {
      street: 'Main St',
      unit: 'Apt 1',
      city: 'Springfield',
      region: 'IL',
      country: 'USA',
      postalCode: '62704',
      telephone: '555-1234',
      cell: '',
      fax: '',
      website: '',
      im: '',
      timeZone: 0,
      preferredLocale: 'en-US',
    },
    ...overrides,
  };
}

describe('UserListComponent', () => {
  let fixture: ComponentFixture<UserListComponent>;
  let userService: jasmine.SpyObj<UserService>;
  let router: jasmine.SpyObj<Router>;

  function dataTable(): DataTableLike {
    return fixture.debugElement.query(By.directive(DataTableComponent))
      .componentInstance as DataTableLike;
  }
  function dialog(): ConfirmationDialogLike {
    return fixture.debugElement.query(By.directive(ConfirmationDialogComponent))
      .componentInstance as ConfirmationDialogLike;
  }

  beforeEach(async () => {
    // MIGRATION (QA Issues 3 & 13): the list consumes the getUsersWithMeta ({ data, meta })
    // variant so it can read meta.totalCount to drive the server-side pager (totalItems).
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUsersWithMeta', 'deleteUser']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    userService.getUsersWithMeta.and.returnValue(of({ data: [makeUser()], meta: { totalCount: 1 } }));
    userService.deleteUser.and.returnValue(of(undefined));
    router.navigate.and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [UserListComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserListComponent);
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads the first server page of users on init', () => {
    expect(userService.getUsersWithMeta).toHaveBeenCalledTimes(1);
    // MIGRATION (QA Issues 3 & 13): the initial (unfiltered) load requests server page 1 with
    // the default per-page size. Server-side pagination replaced the old first-window
    // (MAX_LIST_PAGE_SIZE) load; the server-side toolbar search adds filterProperty/filter or query.
    expect(userService.getUsersWithMeta).toHaveBeenCalledWith({ page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  it('projects each user into a data-table row', () => {
    expect(dataTable().data().length).toBe(1);
  });

  it('composes the address from profile fields', () => {
    const rows = dataTable().data() as Array<{ address: string; telephone: string }>;
    expect(rows[0].address).toBe('Apt 1, Main St, Springfield, IL, USA, 62704');
    expect(rows[0].telephone).toBe('555-1234');
  });

  it('navigates to the editor on the edit row action', () => {
    dataTable().rowAction.emit({ action: 'edit', row: { userID: 1 } } as RowActionEvent<{
      userID: number;
    }>);
    expect(router.navigate).toHaveBeenCalledWith(['/users', 1]);
  });

  // QA finding F4: the Change Password row action routes to the (previously unreachable)
  // change-password screen for that user.
  it('navigates to the change-password screen on the changePassword row action', () => {
    dataTable().rowAction.emit({ action: 'changePassword', row: { userID: 1 } } as RowActionEvent<{
      userID: number;
    }>);
    expect(router.navigate).toHaveBeenCalledWith(['/users', 1, 'password']);
  });

  it('navigates to the create form when adding a new user', () => {
    const addButton = fixture.debugElement.query(By.css('.user-list__add'))
      .nativeElement as HTMLButtonElement;
    addButton.click();
    expect(router.navigate).toHaveBeenCalledWith(['/users', 'new']);
  });

  it('deletes after confirmation and reloads the list', () => {
    dataTable().rowAction.emit({ action: 'delete', row: { userID: 1 } } as RowActionEvent<{
      userID: number;
    }>);
    fixture.detectChanges();

    dialog().confirm.emit();

    expect(userService.deleteUser).toHaveBeenCalledWith(1);
    expect(userService.getUsersWithMeta).toHaveBeenCalledTimes(2);
  });

  it('passes the search term and field to the service alongside the server page params', () => {
    userService.getUsersWithMeta.calls.reset();

    const input = fixture.debugElement.query(By.css('.user-list__search-input'))
      .nativeElement as HTMLInputElement;
    const select = fixture.debugElement.query(By.css('.user-list__search-type'))
      .nativeElement as HTMLSelectElement;
    input.value = 'smith';
    select.value = 'Email';

    const searchButton = fixture.debugElement.query(By.css('.user-list__search-btn'))
      .nativeElement as HTMLButtonElement;
    searchButton.click();

    // MIGRATION (QA Issues 3 & 13): the field-specific server-side filter travels with the
    // server page params (page reset to 1 for a fresh search + default per-page size).
    expect(userService.getUsersWithMeta).toHaveBeenCalledWith({
      page: 1,
      pageSize: DEFAULT_LIST_PAGE_SIZE,
      filterProperty: 'Email',
      filter: 'smith',
    });
  });

  // QA finding (Report 4, Issue 1): a failed load must surface an accessible error alert
  // rather than silently falling through to the "No users found." empty state.
  it('surfaces an accessible error alert when the load fails', () => {
    userService.getUsersWithMeta.and.returnValue(throwError(() => ({ title: 'Server Error' })));

    const errorFixture = TestBed.createComponent(UserListComponent);
    errorFixture.detectChanges();

    const alert = errorFixture.debugElement.query(By.css('.user-list__error'));
    expect(alert).withContext('error banner should render on load failure').not.toBeNull();
    const el = alert.nativeElement as HTMLElement;
    expect(el.getAttribute('role')).toBe('alert');
    expect(el.textContent?.trim()).toBe('Server Error');
  });

  it('falls back to a generic message when the error carries no title', () => {
    userService.getUsersWithMeta.and.returnValue(throwError(() => ({})));

    const errorFixture = TestBed.createComponent(UserListComponent);
    errorFixture.detectChanges();

    const alert = errorFixture.debugElement.query(By.css('.user-list__error'));
    expect((alert.nativeElement as HTMLElement).textContent?.trim()).toBe('Failed to load users');
  });

  it('renders no error alert on a successful load', () => {
    // The beforeEach fixture was created with a successful getUsersWithMeta spy.
    expect(fixture.debugElement.query(By.css('.user-list__error'))).toBeNull();
  });

  // MIGRATION (QA Issues 3 & 13): the obsolete first-window "truncation hint" banner was removed.
  // Server-side pagination now makes every user reachable, so instead of warning that the view is
  // truncated the table's Prev/Next re-query the server for the requested page. Emitting the
  // data-table's pageChange must trigger a reload for that page (no client-side slicing).
  it('reloads the requested server page when the table emits pageChange', () => {
    userService.getUsersWithMeta.calls.reset();

    dataTable().pageChange.emit({ page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });

    expect(userService.getUsersWithMeta).toHaveBeenCalledTimes(1);
    // The second page is requested with the same per-page size; no active search term, so no
    // filterProperty/filter/query is attached.
    expect(userService.getUsersWithMeta).toHaveBeenCalledWith({ page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // MIGRATION (QA Issues 3 & 13): the grand total from meta.totalCount drives the data-table pager
  // (totalItems), which is how a user beyond the first loaded page stays reachable. Verify the
  // total is propagated to the table even when the loaded page holds fewer rows than the total.
  it('propagates meta.totalCount to the data-table pager (totalItems)', () => {
    userService.getUsersWithMeta.and.returnValue(
      of({ data: [makeUser()], meta: { totalCount: 250 } }),
    );

    const totalFixture = TestBed.createComponent(UserListComponent);
    totalFixture.detectChanges();

    const table = totalFixture.debugElement.query(By.directive(DataTableComponent))
      .componentInstance as { totalItems(): number | null };
    expect(table.totalItems()).toBe(250);
  });
});
