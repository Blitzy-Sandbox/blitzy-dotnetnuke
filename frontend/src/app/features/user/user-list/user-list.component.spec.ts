// MIGRATION: Karma/Jasmine spec for UserListComponent (Gate 4). Validates the migrated
// list / paging / filter / delete behavior from Website/admin/Users/Users.ascx.vb [742]:
// zero-based paging (legacy CurrentPage-1 L265), BindData filter dispatch (L248-291), delete
// confirmation (grdUsers_DeleteCommand L646-669) and the protected-user rule (L693-694).
import { signal, type WritableSignal } from '@angular/core';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { NEVER, of, throwError } from 'rxjs';

import type { ProblemDetails, User } from '../../../core/models';
import { AuthService } from '../../../core/auth/auth.service';
import { UserService } from '../user.service';
import { UserListComponent } from './user-list.component';

// Typed User fixture — the 17 model props, ZERO credential fields.
function makeUser(overrides: Partial<User> = {}): User {
  return {
    userId: 1,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    affiliateId: null,
    portalId: 0,
    isApproved: true,
    createdDate: '2024-01-01T00:00:00Z',
    lastLoginDate: null,
    lastActivityDate: null,
    lastLockoutDate: null,
    lockedOut: false,
    roles: [],
    ...overrides,
  };
}

interface CurrentUserLike {
  userId: number;
  username: string;
  email: string;
  displayName: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isSuperUser: boolean;
  portalId: number;
  roles: string[];
}

function makeCurrentUser(overrides: Partial<CurrentUserLike> = {}): CurrentUserLike {
  return {
    userId: 99,
    username: 'host',
    email: 'host@example.com',
    displayName: 'Host User',
    firstName: 'Host',
    lastName: 'User',
    fullName: 'Host User',
    isSuperUser: true,
    portalId: 0,
    roles: ['Administrators'],
    ...overrides,
  };
}

interface UserServiceStub {
  users: WritableSignal<User[]>;
  loading: WritableSignal<boolean>;
  totalCount: WritableSignal<number>;
  // MIGRATION: [QA F3 #4] the error() signal the list template now binds via [error].
  error: WritableSignal<ProblemDetails | null>;
  list: jasmine.Spy;
  delete: jasmine.Spy;
}

interface AuthServiceStub {
  currentUser: WritableSignal<CurrentUserLike | null>;
}

describe('UserListComponent', () => {
  let fixture: ComponentFixture<UserListComponent>;
  let component: UserListComponent;
  let userService: UserServiceStub;
  let authService: AuthServiceStub;
  let router: Router;

  const emptyPage = {
    items: [] as User[],
    totalCount: 0,
    pageIndex: 0,
    pageSize: 20,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    userService = {
      users: signal<User[]>([]),
      loading: signal(false),
      totalCount: signal(0),
      // MIGRATION: [QA F3 #4] expose error() (defaults to null) so the data-table [error] binding resolves.
      error: signal<ProblemDetails | null>(null),
      list: jasmine.createSpy('list').and.returnValue(of(emptyPage)),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
    authService = {
      currentUser: signal<CurrentUserLike | null>(makeCurrentUser()),
    };

    await TestBed.configureTestingModule({
      imports: [UserListComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: authService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserListComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges(); // ngOnInit -> initial load
  });

  it('creates the component and renders the data table', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-data-table')).toBeTruthy();
  });

  // MIGRATION: the backend UsersController list endpoint demands the tenant `portalId` query
  // ([FromQuery, BindRequired], AAP Section 0.7.1). The component threads it as the 5th arg on
  // EVERY list() call, sourced from auth.currentUser().portalId (0 in this spec's principal).
  it('loads the first page with pageSize 20 for the default "All" filter on init', () => {
    expect(userService.list).toHaveBeenCalledWith(0, 20, undefined, undefined, 0);
  });

  it('onPageChange passes the ZERO-BASED index through with pageSize 20', () => {
    userService.list.calls.reset();
    component.onPageChange(3);
    expect(component.currentPage()).toBe(3);
    expect(userService.list).toHaveBeenCalledWith(3, 20, undefined, undefined, 0);
  });

  it('onFilterChange("All") lists with no filter args and resets to page 0', () => {
    component.onPageChange(2);
    userService.list.calls.reset();
    component.onFilterChange('All');
    expect(component.currentPage()).toBe(0);
    expect(userService.list).toHaveBeenCalledWith(0, 20, undefined, undefined, 0);
  });

  it('onFilterChange("A") performs a first-letter Username search', () => {
    userService.list.calls.reset();
    component.onFilterChange('A');
    expect(userService.list).toHaveBeenCalledWith(0, 20, 'A', 'Username', 0);
  });

  it('onFilterChange("Unauthorized") lists with the status token (no search field)', () => {
    userService.list.calls.reset();
    component.onFilterChange('Unauthorized');
    expect(userService.list).toHaveBeenCalledWith(0, 20, 'Unauthorized', undefined, 0);
  });

  it('opens the confirmation dialog for a non-protected user delete request', () => {
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);
    fixture.detectChanges();
    expect(component.pendingDelete()).toEqual(user);
    expect(fixture.nativeElement.querySelector('app-confirmation-dialog')).toBeTruthy();
  });

  it('confirmDelete deletes the user, clears the dialog and reloads', () => {
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);
    userService.list.calls.reset();
    component.confirmDelete();
    // MIGRATION: tenant-scoped delete(id, portalId); portalId sourced from the principal (0 here).
    expect(userService.delete).toHaveBeenCalledWith(5, 0);
    expect(component.pendingDelete()).toBeNull();
    expect(userService.list).toHaveBeenCalledWith(0, 20, undefined, undefined, 0);
  });

  it('cancelDelete dismisses the dialog without deleting', () => {
    const user = makeUser({ userId: 5 });
    component.onDeleteRequest(user);
    component.cancelDelete();
    expect(component.pendingDelete()).toBeNull();
    expect(userService.delete).not.toHaveBeenCalled();
  });

  it('suppresses delete for a protected (superuser) row', () => {
    const superUser = makeUser({ userId: 7, isSuperUser: true });
    component.onDeleteRequest(superUser);
    expect(component.pendingDelete()).toBeNull();
  });

  it('allows deleting the current (non-superuser) user, matching legacy behavior', () => {
    authService.currentUser.set(makeCurrentUser({ userId: 5, isSuperUser: false }));
    const self = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(self);
    expect(component.pendingDelete()).toEqual(self);
  });

  it('navigates to edit / view / profile / new with the correct router paths', () => {
    const navigate = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    const user = makeUser({ userId: 42 });

    component.onEdit(user);
    expect(navigate).toHaveBeenCalledWith(['/users', 42, 'edit']);

    component.onView(user);
    expect(navigate).toHaveBeenCalledWith(['/users', 42]);

    component.onProfile(user);
    expect(navigate).toHaveBeenCalledWith(['/users', 42, 'profile']);

    component.onAddUser();
    expect(navigate).toHaveBeenCalledWith(['/users', 'new']);
  });

  // MIGRATION: [QA F7 — Issue #3] cover the previously-untested filter / delete-failure / re-entrancy /
  // no-pending / null-user branches that held user-list's branch coverage at 65%.

  it('onFilterChange("Online") lists with the Online status token (no search field)', () => {
    userService.list.calls.reset();
    component.onFilterChange('Online');
    expect(userService.list).toHaveBeenCalledWith(0, 20, 'Online', undefined, 0);
  });

  it('surfaces a friendly RFC 7807 message and closes the dialog when DELETE fails', () => {
    userService.delete.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            error: { title: 'Conflict', detail: 'User owns content.' },
            status: 409,
            statusText: 'Conflict',
          }),
      ),
    );
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);
    component.confirmDelete();

    // firstMessage -> parseProblemDetails -> messages[0] is the ProblemDetails title.
    expect(component.actionError()).toBe('Conflict');
    expect(component.pendingDelete()).toBeNull();
    expect(component.deleting()).toBeFalse();
  });

  it('falls back to the default message when a transport failure carries no RFC 7807 body', () => {
    userService.delete.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            error: new ProgressEvent('error'),
            status: 0,
            statusText: 'Unknown Error',
          }),
      ),
    );
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);
    component.confirmDelete();

    expect(component.actionError()).toBe('The user could not be deleted. Please try again.');
    expect(component.pendingDelete()).toBeNull();
  });

  it('fires exactly ONE delete when confirm is invoked repeatedly while a delete is in flight', () => {
    userService.delete.and.returnValue(NEVER); // never completes -> the request stays in flight
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);

    component.confirmDelete();
    component.confirmDelete();
    component.confirmDelete();

    expect(userService.delete).toHaveBeenCalledTimes(1);
    expect(component.deleting()).toBeTrue();
  });

  it('confirmDelete is a no-op when there is no pending delete', () => {
    // pendingDelete() is null after init; confirmDelete must short-circuit.
    component.confirmDelete();
    expect(userService.delete).not.toHaveBeenCalled();
  });

  it('uses portalId -1 on list and delete when there is no authenticated user', () => {
    authService.currentUser.set(null);
    userService.list.calls.reset();

    component.onPageChange(0); // load() -> portalId fallback -1
    expect(userService.list).toHaveBeenCalledWith(0, 20, undefined, undefined, -1);

    // A null current user makes isProtectedUser fall to `user.isSuperUser` only (current !== null is false),
    // so a non-superuser row is still deletable and the tenant query falls back to -1.
    const user = makeUser({ userId: 5, isSuperUser: false });
    component.onDeleteRequest(user);
    component.confirmDelete();
    expect(userService.delete).toHaveBeenCalledWith(5, -1);
  });

  it('still protects a superuser row even when there is no authenticated user', () => {
    authService.currentUser.set(null);
    const superUser = makeUser({ userId: 7, isSuperUser: true });
    component.onDeleteRequest(superUser);
    expect(component.pendingDelete()).toBeNull();
  });
});
