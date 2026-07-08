import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { User } from '../../../core/models';
import { DataTableComponent, RowActionEvent } from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { UserService } from '../user.service';
import { UserListComponent } from './user-list.component';

/** Minimal structural views of the child components' verified public contracts. */
interface DataTableLike {
  data(): unknown[];
  rowAction: { emit(event: RowActionEvent<{ userID: number }>): void };
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
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUsers', 'deleteUser']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    userService.getUsers.and.returnValue(of([makeUser()]));
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

  it('loads users on init', () => {
    expect(userService.getUsers).toHaveBeenCalledTimes(1);
    expect(userService.getUsers).toHaveBeenCalledWith(undefined);
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
    expect(userService.getUsers).toHaveBeenCalledTimes(2);
  });

  it('passes the search term and field to the service', () => {
    userService.getUsers.calls.reset();

    const input = fixture.debugElement.query(By.css('.user-list__search-input'))
      .nativeElement as HTMLInputElement;
    const select = fixture.debugElement.query(By.css('.user-list__search-type'))
      .nativeElement as HTMLSelectElement;
    input.value = 'smith';
    select.value = 'Email';

    const searchButton = fixture.debugElement.query(By.css('.user-list__search-btn'))
      .nativeElement as HTMLButtonElement;
    searchButton.click();

    expect(userService.getUsers).toHaveBeenCalledWith({ filterProperty: 'Email', filter: 'smith' });
  });
});
