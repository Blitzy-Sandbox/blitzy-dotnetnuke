import { signal, type WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';

import { UserListComponent } from './user-list.component';
import { UserService } from '../../services';
import type { UserListItem, UserSearchQuery } from '../../models';
import type { PagedResponse } from '../../../../core/services/api.service';
import type { User } from '../../../../core/models/user.model';
import { AuthService } from '../../../../core/auth/auth.service';
import type {
  DataTableAction,
  DataTableActionEvent,
  DataTableSearch,
} from '../../../../shared/components/data-table';

/**
 * Unit spec for {@link UserListComponent}.
 *
 * The component is a thin orchestrator over {@link UserService}: it owns ALL data
 * access and re-queries the server in response to the shared data-table's events.
 * These tests exercise the component in isolation with fully faked dependencies —
 * there is NO real HTTP and NO HttpTestingController (the component injects
 * services, never HttpClient). Synchronous `of(...)` returns keep every spec
 * deterministic, so no `fakeAsync`/`tick` scheduling is required.
 *
 * NOTE: assertions are written against the AUTHORED component contract in
 * user-list.component.ts (the authoritative source). In particular the user
 * primary key is `userID` (capital ID) — the System.Text.Json camelCase
 * serialization of the backend C# `UserID` property — NOT `userId`.
 */

/**
 * Build a fully-typed {@link UserListItem} fixture. Every field required by the
 * core `User` contract (which `UserListItem` extends) is populated so the object
 * is assignable wherever a `User` or `UserListItem` is expected (e.g. the
 * AuthService `currentUser` signal). Callers override only what a spec cares about.
 */
function makeUser(overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    userID: 1,
    portalID: 0,
    affiliateID: null,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    approved: true,
    updatePassword: false,
    roles: [],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
    lockedOut: false,
    isOnline: false,
    ...overrides,
  };
}

/** Wrap a list of users in the unwrapped `{ data, meta }` paged envelope the service returns. */
function makePage(items: UserListItem[]): PagedResponse<UserListItem> {
  return {
    data: items,
    meta: { pageIndex: 0, pageSize: 10, totalCount: items.length, totalPages: 1 },
  };
}

/** Fabricate a {@link DataTableActionEvent} for a given action id + row (mirrors the grid's emit). */
function actionEvent(id: string, row: UserListItem): DataTableActionEvent<UserListItem> {
  const action: DataTableAction<UserListItem> = { id, label: id };
  return { action, row };
}

describe('UserListComponent', () => {
  let userService: jasmine.SpyObj<UserService>;
  let router: jasmine.SpyObj<Router>;
  let currentUser: WritableSignal<User | null>;
  let authStub: Pick<AuthService, 'currentUser' | 'hasRole'>;
  let fixture: ComponentFixture<UserListComponent>;
  let component: UserListComponent;

  // A minimal ActivatedRoute stub: the component reads `route.parent ?? route`, so an
  // object with no `parent` is a safe, sufficient stand-in for relative navigation.
  const activatedRouteStub: Partial<ActivatedRoute> = {};

  beforeEach(async () => {
    userService = jasmine.createSpyObj<UserService>('UserService', [
      'getUsers',
      'getOnlineUsers',
      'getUnauthorizedUsers',
      'deleteUser',
    ]);
    userService.getUsers.and.returnValue(of(makePage([makeUser()])));
    userService.getOnlineUsers.and.returnValue(of(makePage([])));
    userService.getUnauthorizedUsers.and.returnValue(of(makePage([])));
    userService.deleteUser.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    // The delete action's `hidden` guard reads `authService.currentUser()`, and the
    // data-table's `*appHasPermission` directive (rendered for the DELETE-gated delete
    // action) additionally calls `authService.hasRole(...)`. Provide both so the real
    // child components render without throwing.
    // MIGRATION (reconcile/CP5): the grid is portal-scoped — loadUsers() derives the portalId from the
    // authenticated session (currentUser()?.portalID) and surfaces a banner instead of issuing a request
    // guaranteed to 400 when no portal is known. Seed a signed-in host user (makeUser() -> portalID = 0)
    // so the initial load and every grid-event re-query exercise the real UserService path.
    currentUser = signal<User | null>(makeUser());
    authStub = {
      currentUser,
      hasRole: () => true,
    };

    await TestBed.configureTestingModule({
      imports: [UserListComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: authStub },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers ngOnInit + the initial getUsers load
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initialization', () => {
    it('should load the first page of users on init via getUsers using the default All filter', () => {
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.rows().length).toBe(1);
      expect(component.rows()[0].username).toBe('jdoe');
      expect(component.meta()).toEqual({ pageIndex: 0, pageSize: 10, totalCount: 1, totalPages: 1 });
      expect(component.activeFilter()).toBe('All');
    });
  });

  describe('column and action definitions', () => {
    it('should expose the ten user grid columns in order', () => {
      expect(component.columns.map((column) => column.key)).toEqual([
        'username',
        'firstName',
        'lastName',
        'displayName',
        'email',
        'isOnline',
        'approved',
        'lockedOut',
        'createdDate',
        'lastLoginDate',
      ]);
    });

    it('should expose edit, roles and delete actions with DELETE permission on delete', () => {
      expect(component.actions.map((action) => action.id)).toEqual(['edit', 'roles', 'delete']);
      const deleteAction = component.actions.find((action) => action.id === 'delete');
      expect(deleteAction?.permission).toBe('DELETE');
    });
  });

  describe('filtering', () => {
    it('should query getUsers for the All filter', () => {
      userService.getUsers.calls.reset();
      component.onFilterChange('All');
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('All');
    });

    it('should clear rows and issue no query for the None filter', () => {
      userService.getUsers.calls.reset();
      userService.getOnlineUsers.calls.reset();
      userService.getUnauthorizedUsers.calls.reset();
      component.onFilterChange('None');
      expect(userService.getUsers).not.toHaveBeenCalled();
      expect(userService.getOnlineUsers).not.toHaveBeenCalled();
      expect(userService.getUnauthorizedUsers).not.toHaveBeenCalled();
      expect(component.rows()).toEqual([]);
      expect(component.activeFilter()).toBe('None');
    });

    it('should query getUnauthorizedUsers for the Unauthorized filter', () => {
      userService.getUnauthorizedUsers.calls.reset();
      component.onFilterChange('Unauthorized');
      expect(userService.getUnauthorizedUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('Unauthorized');
    });

    it('should query getOnlineUsers for the OnLine filter', () => {
      userService.getOnlineUsers.calls.reset();
      component.onFilterChange('OnLine');
      expect(userService.getOnlineUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('OnLine');
    });

    it('should query getUsers with the letter filter for a single-letter filter', () => {
      userService.getUsers.calls.reset();
      component.onFilterChange('B');
      expect(userService.getUsers).toHaveBeenCalledWith(
        jasmine.objectContaining<UserSearchQuery>({ filter: 'B' }),
      );
    });
  });

  describe('searching', () => {
    it('should query getUsers with searchType email when searching by Email', () => {
      userService.getUsers.calls.reset();
      const search: DataTableSearch = { text: 'jo', type: 'Email' };
      component.onSearchChange(search);
      expect(userService.getUsers).toHaveBeenCalledWith(
        jasmine.objectContaining<UserSearchQuery>({ searchText: 'jo', searchType: 'email' }),
      );
    });

    it('should query getUsers with searchType username when searching by Username', () => {
      userService.getUsers.calls.reset();
      const search: DataTableSearch = { text: 'jo', type: 'Username' };
      component.onSearchChange(search);
      expect(userService.getUsers).toHaveBeenCalledWith(
        jasmine.objectContaining<UserSearchQuery>({ searchText: 'jo', searchType: 'username' }),
      );
    });
  });

  describe('paging', () => {
    it('should re-query the service with the requested page index', () => {
      userService.getUsers.calls.reset();
      component.onPageChange(2);
      expect(userService.getUsers).toHaveBeenCalledWith(
        jasmine.objectContaining<UserSearchQuery>({ pageIndex: 2 }),
      );
    });
  });

  describe('navigation', () => {
    it('should navigate to the user editor on row click', () => {
      component.onRowClick(makeUser({ userID: 7 }));
      expect(router.navigate).toHaveBeenCalledWith([7], jasmine.anything());
    });

    it('should navigate to the editor for the edit action', () => {
      component.onActionClick(actionEvent('edit', makeUser({ userID: 3 })));
      expect(router.navigate).toHaveBeenCalledWith([3], jasmine.anything());
    });

    it('should navigate to the roles/profile sub-route for the roles action', () => {
      component.onActionClick(actionEvent('roles', makeUser({ userID: 9 })));
      expect(router.navigate).toHaveBeenCalledWith([9, 'profile'], jasmine.anything());
    });

    it('should navigate to the create route on create', () => {
      component.onCreate();
      expect(router.navigate).toHaveBeenCalledWith(['new'], jasmine.anything());
    });
  });

  describe('deletion', () => {
    it('should set the delete target without deleting on the delete action', () => {
      const row = makeUser({ userID: 4 });
      component.onActionClick(actionEvent('delete', row));
      expect(component.deleteTarget()).toBe(row);
      expect(userService.deleteUser).not.toHaveBeenCalled();
    });

    it('should produce a confirmation message naming the targeted user', () => {
      component.onActionClick(actionEvent('delete', makeUser({ username: 'jdoe' })));
      expect(component.deleteMessage()).toContain('jdoe');
    });

    it('should delete the targeted user, clear the target and refresh the list on confirm', () => {
      const row = makeUser({ userID: 8 });
      component.onActionClick(actionEvent('delete', row));
      userService.getUsers.calls.reset();

      component.onDeleteConfirm();

      expect(userService.deleteUser).toHaveBeenCalledWith(8);
      expect(component.deleteTarget()).toBeNull();
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
    });

    it('should clear the target without deleting on cancel', () => {
      component.onActionClick(actionEvent('delete', makeUser({ userID: 8 })));

      component.onDeleteCancel();

      expect(userService.deleteUser).not.toHaveBeenCalled();
      expect(component.deleteTarget()).toBeNull();
    });
  });

  describe('delete visibility guard', () => {
    it('should hide delete for the current super-user own row and show it for other rows', () => {
      const deleteAction = component.actions.find((action) => action.id === 'delete');
      const hiddenGuard = deleteAction?.hidden;
      expect(hiddenGuard).toBeDefined();
      if (hiddenGuard === undefined) {
        return;
      }

      currentUser.set(makeUser({ userID: 5, isSuperUser: true }));

      expect(hiddenGuard(makeUser({ userID: 5, isSuperUser: true }))).toBeTrue();
      expect(hiddenGuard(makeUser({ userID: 6, isSuperUser: true }))).toBeFalse();
    });
  });
});
