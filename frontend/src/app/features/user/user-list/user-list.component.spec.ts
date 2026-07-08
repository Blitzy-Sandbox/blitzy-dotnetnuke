import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { User } from '../../../core/models';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import {
  DataTableComponent,
  RowActionEvent,
} from '../../../shared/components/data-table';
import { UserService } from '../user.service';
import { UserListComponent } from './user-list.component';

/**
 * Unit tests for {@link UserListComponent} — the Angular 19 standalone screen that
 * reproduces the legacy DotNetNuke Users admin DataGrid
 * (Website/admin/Users/users.ascx + Users.ascx.vb).
 *
 * Backs Validation Gate 4 (`ng test --watch=false --browsers=ChromeHeadless
 * --code-coverage`). The component's handlers and view-model signals are `protected`
 * (template-only surface), so these specs exercise behaviour BLACK-BOX through the
 * rendered DOM (search toolbar, Add button) and the real child components
 * (`app-data-table` row actions, `app-confirmation-dialog` confirm/cancel) rather than
 * by reaching into private/protected members. Collaborators are mocked: `UserService`
 * (transport) and `Router` (navigation). No HttpClient/AuthService providers are
 * required because the rendered action buttons carry no `requiredRoles`, so the
 * data-table renders them via its plain (`@else`) branch without the
 * permission directive.
 */

/**
 * Structural view of the flat row the component projects onto the data-table. The
 * component's own `UserListRow` interface is file-private, so this mirror is used only
 * to read the child table's `data()` input in a strongly-typed way.
 */
interface TestRow {
  userID: number;
  username: string;
  firstName: string;
  lastName: string;
  displayName: string;
  address: string;
  telephone: string;
  email: string;
  createdDate: string;
  lastLoginDate: string;
  approved: boolean;
  isOnLine: boolean;
}

/** Builds a fully-populated {@link User} (all fields required/non-null) for tests. */
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userID: 1,
    portalID: 0,
    username: 'jsmith',
    displayName: 'John Smith',
    firstName: 'John',
    lastName: 'Smith',
    email: 'jsmith@example.com',
    isSuperUser: false,
    affiliateID: 0,
    roles: [],
    membership: {
      approved: true,
      lockedOut: false,
      isOnLine: true,
      updatePassword: false,
      createdDate: '2020-01-15T10:00:00.000Z',
      lastLoginDate: '2023-06-01T08:30:00.000Z',
      lastActivityDate: '2023-06-01T09:00:00.000Z',
      lastLockoutDate: '2020-01-15T10:00:00.000Z',
      lastPasswordChangeDate: '2020-01-15T10:00:00.000Z',
    },
    profile: {
      street: '123 Main St',
      unit: 'Apt 4',
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

  /** Strongly-typed handle to the single rendered child data-table. */
  const dataTable = (): DataTableComponent<TestRow> =>
    fixture.debugElement.query(By.directive(DataTableComponent))
      .componentInstance as DataTableComponent<TestRow>;

  /** Strongly-typed handle to the single rendered child confirmation dialog. */
  const dialog = (): ConfirmationDialogComponent =>
    fixture.debugElement.query(By.directive(ConfirmationDialogComponent))
      .componentInstance as ConfirmationDialogComponent;

  const host = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const query = <E extends HTMLElement>(selector: string): E => {
    const el = host().querySelector<E>(selector);
    expect(el).withContext(`expected to find "${selector}"`).not.toBeNull();
    return el as E;
  };

  beforeEach(async () => {
    userService = jasmine.createSpyObj<UserService>('UserService', [
      'getUsers',
      'deleteUser',
    ]);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    // Default happy-path stubs (synchronous observables keep the specs fakeAsync-free).
    userService.getUsers.and.returnValue(of([makeUser()]));
    userService.deleteUser.and.returnValue(of(undefined));
    router.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      // Standalone component -> register in `imports`, never `declarations`.
      imports: [UserListComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserListComponent);
    // NB: detectChanges() (which fires ngOnInit) is invoked per-test so error/empty
    // scenarios can override the getUsers stub BEFORE the initial load runs.
  });

  it('creates the component', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads all users on init with no filter params', () => {
    fixture.detectChanges();

    expect(userService.getUsers).toHaveBeenCalledTimes(1);
    expect(userService.getUsers).toHaveBeenCalledWith(undefined);
  });

  it('projects User -> flat row and formats the address (Unit..PostalCode)', () => {
    fixture.detectChanges();

    const rows = dataTable().data();
    expect(rows.length).toBe(1);
    expect(rows[0].username).toBe('jsmith');
    expect(rows[0].displayName).toBe('John Smith');
    expect(rows[0].telephone).toBe('555-1234');
    expect(rows[0].email).toBe('jsmith@example.com');
    expect(rows[0].approved).toBeTrue();
    expect(rows[0].isOnLine).toBeTrue();
    // DisplayAddress(Unit, Street, City, Region, Country, PostalCode) -> ", "-joined.
    expect(rows[0].address).toBe(
      'Apt 4, 123 Main St, Springfield, IL, USA, 62704',
    );
  });

  it('formats an empty address when every profile part is blank/whitespace', () => {
    userService.getUsers.and.returnValue(
      of([
        makeUser({
          userID: 2,
          profile: {
            street: '   ',
            unit: '',
            city: '',
            region: '',
            country: '',
            postalCode: '',
            telephone: '555-0000',
            cell: '',
            fax: '',
            website: '',
            im: '',
            timeZone: 0,
            preferredLocale: 'en-US',
          },
        }),
      ]),
    );

    fixture.detectChanges();

    expect(dataTable().data()[0].address).toBe('');
  });

  it('exposes the 11 legacy grid columns and the edit/delete row actions', () => {
    fixture.detectChanges();

    const table = dataTable();
    const headers = table.columns().map((c) => c.header);
    expect(headers).toEqual([
      'Username',
      'First Name',
      'Last Name',
      'Display Name',
      'Address',
      'Telephone',
      'Email',
      'Created Date',
      'Last Login',
      'Authorized',
      'Online',
    ]);

    const actionIds = table.actions().map((a) => a.action);
    expect(actionIds).toEqual(['edit', 'delete']);
    // The legacy UserRoles command is intentionally omitted (MIGRATION_NOTES.md).
    expect(actionIds).not.toContain('userRoles');

    // The server-side search toolbar is authoritative -> client filter disabled.
    expect(table.filterable()).toBeFalse();
    expect(table.rowKey()).toBe('userID');
  });

  it('searches a specific field (Username) -> { filterProperty, filter } (trimmed)', () => {
    fixture.detectChanges();
    userService.getUsers.calls.reset();

    query<HTMLSelectElement>('.user-list__search-type').value = 'Username';
    query<HTMLInputElement>('.user-list__search-input').value = '  smith  ';
    query<HTMLButtonElement>('.user-list__search-btn').click();
    fixture.detectChanges();

    expect(userService.getUsers).toHaveBeenCalledTimes(1);
    expect(userService.getUsers).toHaveBeenCalledWith({
      filterProperty: 'Username',
      filter: 'smith',
    });
  });

  it('searches all fields (default) -> { query }', () => {
    fixture.detectChanges();
    userService.getUsers.calls.reset();

    query<HTMLInputElement>('.user-list__search-input').value = 'john';
    // The type dropdown stays at its default first option ('query' / All Fields).
    query<HTMLButtonElement>('.user-list__search-btn').click();
    fixture.detectChanges();

    expect(userService.getUsers).toHaveBeenCalledOnceWith({ query: 'john' });
  });

  it('clears the search, reloads all users, and resets the input', () => {
    fixture.detectChanges();

    const input = query<HTMLInputElement>('.user-list__search-input');
    input.value = 'john';
    query<HTMLButtonElement>('.user-list__search-btn').click();
    fixture.detectChanges();

    userService.getUsers.calls.reset();
    query<HTMLButtonElement>('.user-list__clear-btn').click();
    fixture.detectChanges();

    expect(userService.getUsers).toHaveBeenCalledOnceWith(undefined);
    // [value]="searchTerm()" reflects the cleared signal back into the DOM input.
    expect(input.value).toBe('');
  });

  it('navigates to the user editor on the "edit" row action', () => {
    fixture.detectChanges();

    const event: RowActionEvent<TestRow> = {
      action: 'edit',
      row: makeRow({ userID: 42 }),
    };
    dataTable().rowAction.emit(event);

    expect(router.navigate).toHaveBeenCalledOnceWith(['/users', 42]);
  });

  it('opens the confirmation dialog on "delete" WITHOUT deleting yet', () => {
    fixture.detectChanges();

    dataTable().rowAction.emit({
      action: 'delete',
      row: makeRow({ userID: 7, username: 'todelete' }),
    });
    fixture.detectChanges();

    const dlg = dialog();
    expect(dlg.open()).toBeTrue();
    expect(dlg.message()).toContain('todelete');
    expect(userService.deleteUser).not.toHaveBeenCalled();
  });

  it('deletes the user then reloads the list when the dialog is confirmed', () => {
    fixture.detectChanges();

    dataTable().rowAction.emit({
      action: 'delete',
      row: makeRow({ userID: 7 }),
    });
    fixture.detectChanges();

    userService.getUsers.calls.reset();
    dialog().confirm.emit();
    fixture.detectChanges();

    // MIGRATION: DELETE /api/users/{id} (204) then refresh the grid.
    expect(userService.deleteUser).toHaveBeenCalledOnceWith(7);
    expect(userService.getUsers).toHaveBeenCalledOnceWith(undefined);
  });

  it('does NOT delete when the dialog is cancelled', () => {
    fixture.detectChanges();

    dataTable().rowAction.emit({
      action: 'delete',
      row: makeRow({ userID: 7 }),
    });
    fixture.detectChanges();

    dialog().cancel.emit();
    fixture.detectChanges();

    expect(userService.deleteUser).not.toHaveBeenCalled();
  });

  it('navigates to the create form when "Add New User" is clicked', () => {
    fixture.detectChanges();

    query<HTMLButtonElement>('.user-list__add').click();

    expect(router.navigate).toHaveBeenCalledOnceWith(['/users', 'new']);
  });

  it('renders an empty grid when the users request fails', () => {
    userService.getUsers.and.returnValue(throwError(() => new Error('boom')));

    fixture.detectChanges();

    expect(dataTable().data()).toEqual([]);
  });
});

/** Convenience builder for the flat row shape consumed by the data-table actions. */
function makeRow(overrides: Partial<TestRow> = {}): TestRow {
  return {
    userID: 1,
    username: 'jsmith',
    firstName: 'John',
    lastName: 'Smith',
    displayName: 'John Smith',
    address: '',
    telephone: '',
    email: 'jsmith@example.com',
    createdDate: '2020-01-15T10:00:00.000Z',
    lastLoginDate: '2023-06-01T08:30:00.000Z',
    approved: true,
    isOnLine: false,
    ...overrides,
  };
}
