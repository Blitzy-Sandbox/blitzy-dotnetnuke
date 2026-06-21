// MIGRATION: Unit spec for UserListComponent — the Angular 19 standalone replacement for the legacy DNN
// Web Forms Admin > Users LIST screen (Website/admin/Users/Users.ascx[.vb] BindData + ManageUsers.ascx.vb
// orchestration). It validates the BEHAVIOUR-parity contract the component ports from the legacy grid:
//   - BindData filter strip (Users.ascx.vb L248-264): All / None / Unauthorized / OnLine / first-letter
//   - ddlSearchType free-text search (L173): Username / Email / Profile
//   - server paging, per-row Edit / UserRoles / Delete commands, and the delete-confirmation gate.
//
// SOURCE-AUTHORITATIVE NOTES (the authored user-list.component.ts is the contract; it intentionally
// DIVERGES from the original prompt, so the assertions below follow the SOURCE):
//   - Property casing is `userID` / `portalID` (System.Text.Json camelCase preserves the trailing
//     acronym), NOT `userId`.
//   - The Phase-1 REST surface (UserSearchQuery) carries ONLY { portalId, pageIndex, pageSize }. The
//     dedicated GetUnauthorizedUsers / GetOnlineUsers / GetUsersBy* listings are DEFERRED, so UserService
//     exposes NO getOnlineUsers/getUnauthorizedUsers. Every filter strip mode and every search type is
//     therefore reproduced CLIENT-SIDE over the single getUsers() page (applyClientView / matchesSearch).
//     Consequently the filter/search specs assert the resulting rows() effect — NOT non-existent query
//     fields — while paging (a real query field) is asserted on the getUsers argument.
//
// Testing strategy — true UNIT test in ISOLATION. The component imports three real shared standalone
// building blocks (DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent); the real
// DataTableComponent transitively pulls in HasPermissionDirective (which injects AuthService). To keep
// this spec free of those transitive dependencies, TestBed.overrideComponent swaps the real imports for
// lightweight standalone STUB doubles that share the same selectors and declare a SUPERSET of every
// input/output the real components expose (so the separately-authored component template compiles against
// them). All HTTP collaboration is replaced by a typed UserService spy; Router is spied; ActivatedRoute is
// a minimal stub; AuthService is a typed object exposing only the `currentUser` signal the component reads.
// Every collaborator observable is synchronous (of(...)), so assertions run immediately after the
// triggering call with no fakeAsync/tick.
import {
  Component,
  Directive,
  inject,
  input,
  output,
  signal,
  TemplateRef,
  ViewContainerRef,
  type WritableSignal,
} from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { UserListComponent } from './user-list.component';
import { UserService } from '../../services';
import type { UserListItem, UserSearchQuery } from '../../models';
import type {
  ApiResponseMeta,
  PagedResponse,
  ProblemDetails,
} from '../../../../core/services/api.service';
import type { User } from '../../../../core/models/user.model';
import { AuthService } from '../../../../core/auth/auth.service';
import type {
  DataTableAction,
  DataTableActionEvent,
  DataTableColumn,
  DataTableSearch,
  DataTableSort,
} from '../../../../shared/components/data-table';
import type { PermissionKey } from '../../../../shared/directives/has-permission';

// --- Standalone stub doubles (Angular 19 standalone-by-default) -------------------------------------
// Each stub mirrors the real shared component's selector and declares EVERY input/output the component
// template can bind (a superset of the real contract, typed concretely to UserListItem). The templates
// are intentionally empty: this spec asserts component-class behaviour (signals, service calls, routing),
// not rendered shared-component DOM.

/** Stub for the real app-data-table (presentation-only grid; the parent owns all data access). */
@Component({ selector: 'app-data-table', template: '' })
class StubDataTableComponent {
  readonly rows = input<UserListItem[]>([]);
  readonly columns = input<DataTableColumn<UserListItem>[]>([]);
  readonly actions = input<DataTableAction<UserListItem>[]>([]);
  readonly meta = input<ApiResponseMeta | null>(null);
  readonly sort = input<DataTableSort | null>(null);
  readonly filters = input<readonly string[]>([]);
  readonly activeFilter = input<string>('All');
  readonly searchable = input<boolean>(false);
  readonly searchTypes = input<string[]>([]);
  readonly searchText = input<string>('');
  readonly searchType = input<string>('');
  readonly itemSize = input<number>(48);
  readonly viewportRows = input<number>(8);
  readonly loading = input<boolean>(false);
  readonly caption = input<string>('');
  readonly emptyMessage = input<string>('No records found.');
  readonly actionsLabel = input<string>('Actions');
  readonly searchPlaceholder = input<string>('');
  readonly searchButtonLabel = input<string>('Search');
  readonly rowClick = output<UserListItem>();
  readonly pageChange = output<number>();
  readonly filterChange = output<string>();
  readonly searchChange = output<DataTableSearch>();
  readonly sortChange = output<DataTableSort>();
  readonly actionClick = output<DataTableActionEvent<UserListItem>>();
}

/** Stub for the real app-confirmation-dialog (accessible delete-confirmation modal). */
@Component({ selector: 'app-confirmation-dialog', template: '' })
class StubConfirmationDialogComponent {
  readonly open = input(false);
  readonly title = input('Confirm');
  readonly message = input('');
  readonly confirmText = input('Delete');
  readonly cancelText = input('Cancel');
  readonly destructive = input(true);
  readonly confirm = output<void>();
  readonly cancel = output<void>();
}

/** Stub for the real app-loading-spinner. */
@Component({ selector: 'app-loading-spinner', template: '' })
class StubLoadingSpinnerComponent {
  readonly loading = input(true);
  readonly message = input('Loading...');
  readonly diameter = input(40);
}

/**
 * Stub for the real HasPermissionDirective (RBAC UI gate). The real directive injects AuthService and
 * conditionally renders its host based on the current user's roles; this isolation double simply renders
 * the gated content UNCONDITIONALLY so this unit test stays free of the AuthService role-lookup path
 * (component-class behaviour — signals, service calls, routing — is what is under test, not RBAC
 * rendering, which is covered by has-permission.directive.spec.ts). It declares the `appHasPermission`
 * required input with the SAME `PermissionKey` type as the real directive so the `*appHasPermission`
 * structural binding now present in the template compiles under strictTemplates / strictStandalone.
 */
@Directive({ selector: '[appHasPermission]' })
class StubHasPermissionDirective {
  private readonly templateRef = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly viewContainerRef = inject(ViewContainerRef);
  readonly appHasPermission = input.required<PermissionKey>();

  constructor() {
    this.viewContainerRef.createEmbeddedView(this.templateRef);
  }
}

// --- Typed fixtures (NO `any`) ----------------------------------------------------------------------

/**
 * Build a UserListItem grid row. Casing matches the core User model exactly (userID / portalID; the
 * trailing acronym is preserved by System.Text.Json camelCase). UserListItem extends User, so the same
 * factory also produces a valid `currentUser` (User) for the delete `hidden` guard.
 */
function makeUser(overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    userID: 1,
    username: 'jdoe',
    displayName: 'John Doe',
    firstName: 'John',
    lastName: 'Doe',
    email: 'jdoe@example.com',
    portalID: 0,
    isSuperUser: false,
    roles: [],
    approved: true,
    lockedOut: false,
    isOnline: false,
    ...overrides,
  };
}

/** Wrap rows in the unwrapped `{ data, meta }` paged envelope returned by UserService.getUsers. */
function makePage(items: UserListItem[]): PagedResponse<UserListItem> {
  return {
    data: items,
    meta: { pageIndex: 0, pageSize: 10, totalCount: items.length, totalPages: 1 },
  };
}

/** Fabricate a typed data-table action event for the per-row Edit / Roles / Delete commands. */
function actionEvent(id: string, row: UserListItem): DataTableActionEvent<UserListItem> {
  const action: DataTableAction<UserListItem> = { id, label: id };
  return { action, row };
}

describe('UserListComponent', () => {
  let fixture: ComponentFixture<UserListComponent>;
  let component: UserListComponent;
  let userService: jasmine.SpyObj<UserService>;
  let router: jasmine.SpyObj<Router>;
  let currentUser: WritableSignal<User | null>;
  // Minimal ActivatedRoute stub. The component computes `route.parent ?? route`; with no `parent`
  // this resolves to the stub itself, so the navigation specs assert `relativeTo: activatedRouteStub`.
  const activatedRouteStub = {} as unknown as ActivatedRoute;

  beforeEach(() => {
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUsers', 'deleteUser']);
    userService.getUsers.and.returnValue(of(makePage([makeUser()])));
    userService.deleteUser.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.returnValue(Promise.resolve(true));

    currentUser = signal<User | null>(null);
    const authStub: Pick<AuthService, 'currentUser'> = { currentUser };

    TestBed.configureTestingModule({
      imports: [UserListComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: authStub },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    });
    // Swap the real shared children for the stub doubles so no transitive collaborator (e.g. the
    // data-table's HasPermissionDirective -> AuthService, or the toolbar's own *appHasPermission gate)
    // is exercised by this unit test. StubHasPermissionDirective renders its content unconditionally,
    // so the "Add User" toolbar gate compiles and materializes without consulting AuthService.hasRole.
    TestBed.overrideComponent(UserListComponent, {
      set: {
        imports: [
          StubDataTableComponent,
          StubConfirmationDialogComponent,
          StubLoadingSpinnerComponent,
          StubHasPermissionDirective,
        ],
      },
    });

    fixture = TestBed.createComponent(UserListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // runs ngOnInit -> initial getUsers
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('initialization (ngOnInit / legacy BindData parity)', () => {
    it('should load the first page via getUsers and populate rows/meta with the default All filter', () => {
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.rows().length).toBe(1);
      expect(component.rows()[0].username).toBe('jdoe');
      expect(component.meta()).not.toBeNull();
      expect(component.activeFilter()).toBe('All');
    });
  });

  describe('column and action definitions', () => {
    it('should expose the ten legacy user-grid columns in order', () => {
      expect(component.columns.map((c) => c.key)).toEqual([
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

    it('should expose edit/roles/delete row actions, each RBAC-gated (edit/roles by EDIT, delete by DELETE)', () => {
      expect(component.actions.map((a) => a.id)).toEqual(['edit', 'roles', 'delete']);
      // Every row action must carry a permission key so data-table gates it via *appHasPermission,
      // consistent with the portal/module/role lists (QA F4 Finding #1: previously edit/roles were ungated).
      const edit = component.actions.find((a) => a.id === 'edit');
      const roles = component.actions.find((a) => a.id === 'roles');
      const del = component.actions.find((a) => a.id === 'delete');
      expect(edit?.permission).toBe('EDIT');
      expect(roles?.permission).toBe('EDIT');
      expect(del).toBeTruthy();
      expect(del?.permission).toBe('DELETE');
    });
  });

  describe('filtering (client-side reproduction of the legacy filter strip)', () => {
    it('should re-query getUsers and keep the All filter active', () => {
      userService.getUsers.calls.reset();
      component.onFilterChange('All');
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('All');
    });

    it('should clear rows and issue NO request for the None filter', () => {
      userService.getUsers.calls.reset();
      component.onFilterChange('None');
      expect(userService.getUsers).not.toHaveBeenCalled();
      expect(component.rows()).toEqual([]);
      expect(component.activeFilter()).toBe('None');
    });

    it('should fetch via getUsers and retain only unauthorized rows for the Unauthorized filter', () => {
      userService.getUsers.and.returnValue(
        of(
          makePage([
            makeUser({ userID: 1, username: 'amy', approved: true }),
            makeUser({ userID: 2, username: 'ben', approved: false }),
          ]),
        ),
      );
      userService.getUsers.calls.reset();
      component.onFilterChange('Unauthorized');
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('Unauthorized');
      expect(component.rows().map((r) => r.userID)).toEqual([2]);
    });

    it('should fetch via getUsers and retain only online rows for the OnLine filter', () => {
      userService.getUsers.and.returnValue(
        of(
          makePage([
            makeUser({ userID: 1, username: 'amy', isOnline: false }),
            makeUser({ userID: 2, username: 'ben', isOnline: true }),
          ]),
        ),
      );
      userService.getUsers.calls.reset();
      component.onFilterChange('OnLine');
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.rows().map((r) => r.userID)).toEqual([2]);
    });

    it('should fetch via getUsers and retain only first-letter matches for a letter filter', () => {
      userService.getUsers.and.returnValue(
        of(
          makePage([
            makeUser({ userID: 1, username: 'bob' }),
            makeUser({ userID: 2, username: 'alice' }),
          ]),
        ),
      );
      userService.getUsers.calls.reset();
      component.onFilterChange('B');
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.activeFilter()).toBe('B');
      expect(component.rows().map((r) => r.userID)).toEqual([1]);
    });
  });

  describe('searching (client-side ddlSearchType reproduction)', () => {
    it('should match on the email column when searching by Email', () => {
      userService.getUsers.and.returnValue(
        of(
          makePage([
            makeUser({ userID: 1, username: 'zzz', email: 'john@example.com' }),
            makeUser({ userID: 2, username: 'jother', email: 'amy@example.com' }),
          ]),
        ),
      );
      userService.getUsers.calls.reset();
      const search: DataTableSearch = { text: 'jo', type: 'Email' };
      component.onSearchChange(search);
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(component.searchText()).toBe('jo');
      expect(component.activeFilter()).toBe('All');
      expect(component.rows().map((r) => r.userID)).toEqual([1]);
    });

    it('should match on the username column when searching by Username', () => {
      userService.getUsers.and.returnValue(
        of(
          makePage([
            makeUser({ userID: 1, username: 'zzz', email: 'john@example.com' }),
            makeUser({ userID: 2, username: 'jother', email: 'amy@example.com' }),
          ]),
        ),
      );
      userService.getUsers.calls.reset();
      const search: DataTableSearch = { text: 'jo', type: 'Username' };
      component.onSearchChange(search);
      expect(component.searchText()).toBe('jo');
      expect(component.rows().map((r) => r.userID)).toEqual([2]);
    });
  });

  describe('paging', () => {
    it('should re-query getUsers with the requested zero-based page index', () => {
      userService.getUsers.calls.reset();
      component.onPageChange(2);
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
      expect(userService.getUsers).toHaveBeenCalledWith(
        jasmine.objectContaining<UserSearchQuery>({ pageIndex: 2 }),
      );
    });
  });

  describe('navigation', () => {
    it('should navigate to the user editor on row click', () => {
      component.onRowClick(makeUser({ userID: 7 }));
      expect(router.navigate).toHaveBeenCalledWith([7], { relativeTo: activatedRouteStub });
    });

    it('should navigate to the user editor for the edit action', () => {
      component.onActionClick(actionEvent('edit', makeUser({ userID: 7 })));
      expect(router.navigate).toHaveBeenCalledWith([7], { relativeTo: activatedRouteStub });
    });

    it('should navigate to the per-user profile sub-route for the roles action', () => {
      component.onActionClick(actionEvent('roles', makeUser({ userID: 7 })));
      expect(router.navigate).toHaveBeenCalledWith([7, 'profile'], { relativeTo: activatedRouteStub });
    });

    it('should navigate to the create route on create', () => {
      component.onCreate();
      expect(router.navigate).toHaveBeenCalledWith(['new'], { relativeTo: activatedRouteStub });
    });
  });

  describe('deletion (confirmation gate)', () => {
    it('should arm the delete confirmation without calling deleteUser for the delete action', () => {
      const row = makeUser({ userID: 7 });
      component.onActionClick(actionEvent('delete', row));
      expect(component.deleteTarget()).toBe(row);
      expect(userService.deleteUser).not.toHaveBeenCalled();
    });

    it('should name the targeted user in the delete-confirmation message', () => {
      component.onActionClick(actionEvent('delete', makeUser({ username: 'jdoe' })));
      expect(component.deleteMessage()).toContain('jdoe');
    });

    it('should delete the armed user, clear the target, and refresh the list on confirm', () => {
      const row = makeUser({ userID: 7 });
      component.onActionClick(actionEvent('delete', row));
      userService.getUsers.calls.reset();
      component.onDeleteConfirm();
      expect(userService.deleteUser).toHaveBeenCalledWith(7);
      expect(component.deleteTarget()).toBeNull();
      expect(userService.getUsers).toHaveBeenCalledTimes(1);
    });

    it('should clear the target without calling deleteUser on cancel', () => {
      component.onActionClick(actionEvent('delete', makeUser({ userID: 7 })));
      component.onDeleteCancel();
      expect(component.deleteTarget()).toBeNull();
      expect(userService.deleteUser).not.toHaveBeenCalled();
    });

    it('should hide the delete action only for the current super-user own row', () => {
      currentUser.set(makeUser({ userID: 5, isSuperUser: true }));
      const del = component.actions.find((a) => a.id === 'delete');
      const hidden = del?.hidden;
      expect(hidden).toBeDefined();
      if (hidden) {
        expect(hidden(makeUser({ userID: 5, isSuperUser: true }))).toBeTrue();
        expect(hidden(makeUser({ userID: 9, isSuperUser: true }))).toBeFalse();
      }
    });
  });

  // ---------------------------------------------------------------------------------------------------
  // MIGRATION (QA Finding D, MAJOR): the user list must SURFACE a server failure (e.g. a 503 when the
  // database is unreachable) via an error banner instead of silently rendering the "No users found."
  // empty state. These specs lock in the fix: error() is set on a load failure, the previously
  // displayed rows are PRESERVED (an error is not misrepresented as empty), the banner is dismissible,
  // and a stale error is cleared on a successful retry. Contrast the legacy silent behaviour where the
  // error handler only did rows.set([]) + meta.set(null) with no error indication.
  // ---------------------------------------------------------------------------------------------------
  describe('load error handling (QA Finding D)', () => {
    const problem: ProblemDetails = {
      status: 503,
      title: 'Service Unavailable',
      detail: 'The database is currently unavailable.',
    };

    it('surfaces a server error via the error() signal instead of silently emptying the grid', () => {
      userService.getUsers.and.returnValue(throwError(() => problem));
      component.onFilterChange('All'); // re-query -> error path
      expect(component.error()).toBe('The database is currently unavailable.');
    });

    it('falls back to the title (then a generic message) when no detail is present', () => {
      userService.getUsers.and.returnValue(
        throwError(() => ({ status: 503, title: 'Service Unavailable' }) as ProblemDetails),
      );
      component.onFilterChange('All');
      expect(component.error()).toBe('Service Unavailable');
    });

    it('PRESERVES the currently displayed rows on a load error (distinguishes error from empty)', () => {
      // beforeEach already loaded one row; a subsequent failing reload must NOT clear the grid.
      expect(component.rows().length).toBe(1);
      userService.getUsers.and.returnValue(throwError(() => problem));
      component.onPageChange(1); // triggers loadUsers -> error path
      expect(component.error()).toBe('The database is currently unavailable.');
      expect(component.rows().length).toBe(1); // rows retained, NOT cleared to []
      expect(component.meta()).not.toBeNull(); // meta retained too
    });

    it('dismissError() clears the banner', () => {
      userService.getUsers.and.returnValue(throwError(() => problem));
      component.onFilterChange('All');
      expect(component.error()).not.toBeNull();
      component.dismissError();
      expect(component.error()).toBeNull();
    });

    it('clears a stale error on a successful reload', () => {
      userService.getUsers.and.returnValue(throwError(() => problem));
      component.onFilterChange('All');
      expect(component.error()).not.toBeNull();
      // Recover: the next query succeeds and the banner must clear.
      userService.getUsers.and.returnValue(
        of(makePage([makeUser({ userID: 9, username: 'recovered' })])),
      );
      component.onFilterChange('All');
      expect(component.error()).toBeNull();
      expect(component.rows().map((r) => r.username)).toEqual(['recovered']);
    });
  });
});
