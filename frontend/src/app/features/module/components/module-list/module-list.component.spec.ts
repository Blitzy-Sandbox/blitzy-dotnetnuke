/**
 * Unit tests for {@link ModuleListComponent} — the administrative modules grid.
 *
 * Exercised by Gate 4 (`ng test --watch=false --browsers=ChromeHeadless`). Uses a standalone
 * `TestBed` (`imports: [ModuleListComponent]`), a `jasmine.createSpyObj` mock of `ModuleService`,
 * a `Router` spy, and an `AuthService` stub. The `AuthService` stub is REQUIRED because the rendered
 * template gates affordances with `*appHasPermission`, and `HasPermissionDirective` injects
 * `AuthService` (it reads `currentUser()` and `hasRole()`). Typed fixtures only — NO `any`.
 *
 * MIGRATION: the legacy admin grid is PORTAL-scoped (ModuleController.GetModules(PortalID),
 * ModuleController.vb L915). The Angular route carries no portalId, so the component takes the current
 * portal from the authenticated session (AuthService.currentUser()?.portalID) and calls the
 * portal-discriminated ModuleService.getModulesByPortal(portalId, params) — NOT an undiscriminated
 * getModules(params). Every load assertion therefore expects the leading portalId argument
 * (mockUser.portalID = 0) followed by the exact QueryParams object the component builds.
 */
import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { type Module, VisibilityState } from '../../models';
import { ModuleService } from '../../services';
import type { PagedResponse, ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type {
  DataTableActionEvent,
  DataTableSearch,
  DataTableSort,
} from '../../../../shared/components/data-table';
import { ModuleListComponent } from './module-list.component';

/** Builds a fully-typed {@link Module} fixture; nullable fields default to `null` (never `undefined`). */
function makeModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleID: 1,
    portalID: 0,
    tabID: 0,
    tabModuleID: 0,
    moduleDefID: 0,
    moduleOrder: 1,
    paneName: 'ContentPane',
    moduleTitle: 'Sample Module',
    cacheTime: 0,
    alignment: null,
    color: null,
    border: null,
    iconFile: null,
    allTabs: false,
    visibility: VisibilityState.Maximized,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    header: null,
    footer: null,
    startDate: null,
    endDate: null,
    containerSrc: null,
    inheritViewPermissions: false,
    desktopModuleID: 0,
    friendlyName: 'Sample',
    description: null,
    version: null,
    isDeleted: false,
    ...overrides,
  };
}

// MIGRATION: the User shape mirrors core/models/user.model.ts (System.Text.Json camelCase of the
// backend UserDto: UserID -> userID, PortalID -> portalID). portalID drives the portal scope the grid
// queries with, so it is set to the host portal (0). isSuperUser=true makes the stubbed hasRole grant
// every *appHasPermission affordance.
const mockUser: User = {
  userID: 1,
  portalID: 0,
  affiliateID: null,
  username: 'host',
  displayName: 'Host',
  email: 'host@example.com',
  firstName: 'Host',
  lastName: 'User',
  fullName: 'Host User',
  isSuperUser: true,
  approved: true,
  updatePassword: false,
  roles: ['Administrators'],
  createdDate: null,
  lastLoginDate: null,
  lastPasswordChangeDate: null,
  lastActivityDate: null,
};

describe('ModuleListComponent', () => {
  let component: ModuleListComponent;
  let fixture: ComponentFixture<ModuleListComponent>;
  let moduleServiceSpy: jasmine.SpyObj<ModuleService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    moduleServiceSpy = jasmine.createSpyObj<ModuleService>('ModuleService', [
      'getModulesByPortal',
      'deleteModule',
    ]);
    const emptyPage: PagedResponse<Module> = { data: [], meta: {} };
    moduleServiceSpy.getModulesByPortal.and.returnValue(of(emptyPage));
    moduleServiceSpy.deleteModule.and.returnValue(of(void 0));

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [ModuleListComponent],
      providers: [
        { provide: ModuleService, useValue: moduleServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ModuleListComponent);
    component = fixture.componentInstance;
  });

  it('creates and renders the data table', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    const dataTable = (fixture.nativeElement as HTMLElement).querySelector('app-data-table');
    expect(dataTable).toBeTruthy();
  });

  it('loads modules on init via the portal-scoped getModulesByPortal(portalId) and exposes the first page', () => {
    const modules = [makeModule({ moduleID: 1 }), makeModule({ moduleID: 2 })];
    const page: PagedResponse<Module> = { data: modules, meta: {} };
    moduleServiceSpy.getModulesByPortal.and.returnValue(of(page));

    component.ngOnInit();

    // MIGRATION (reconcile/CP5): GET /api/v1/modules?portalId= accepts ONLY the portalId discriminator —
    // the backend ignores paging/filter/search/sort — so the load sends the session portalID
    // (mockUser.portalID = 0) as the SINGLE argument; paging/filter/search/sort are derived CLIENT-SIDE
    // from the returned set (rows()/meta() are computed signals over allModules).
    expect(moduleServiceSpy.getModulesByPortal).toHaveBeenCalledTimes(1);
    expect(moduleServiceSpy.getModulesByPortal).toHaveBeenCalledWith(0);
    expect(component.rows()).toEqual(modules);
    expect(component.meta()).toEqual({ pageIndex: 0, pageSize: 20, totalCount: 2, totalPages: 1 });
    expect(component.loading()).toBe(false);
  });

  it('applies the letter filter CLIENT-SIDE without re-querying the service', () => {
    const modules = [
      makeModule({ moduleID: 1, moduleTitle: 'Announcements' }),
      makeModule({ moduleID: 2, moduleTitle: 'Blog' }),
    ];
    moduleServiceSpy.getModulesByPortal.and.returnValue(of({ data: modules, meta: {} }));
    component.ngOnInit();
    moduleServiceSpy.getModulesByPortal.calls.reset();

    component.onFilterChange('A');

    // MIGRATION: the backend ignores filter params, so the letter filter narrows the already-loaded
    // allModules set in the browser (moduleTitle starts-with the letter); the service is NOT re-queried.
    expect(moduleServiceSpy.getModulesByPortal).not.toHaveBeenCalled();
    expect(component.activeFilter()).toBe('A');
    expect(component.rows().map((module) => module.moduleID)).toEqual([1]);
  });

  it('changes the page slice CLIENT-SIDE without re-querying the service', () => {
    const modules = Array.from({ length: 25 }, (_, index) => makeModule({ moduleID: index + 1 }));
    moduleServiceSpy.getModulesByPortal.and.returnValue(of({ data: modules, meta: {} }));
    component.ngOnInit();
    moduleServiceSpy.getModulesByPortal.calls.reset();

    component.onPageChange(1);

    // MIGRATION: paging is a client-side slice (PAGE_SIZE = 20) over the filtered set, not a re-query.
    expect(moduleServiceSpy.getModulesByPortal).not.toHaveBeenCalled();
    expect(component.pageIndex()).toBe(1);
    expect(component.rows().length).toBe(5);
    expect(component.rows().map((module) => module.moduleID)).toEqual([21, 22, 23, 24, 25]);
    expect(component.meta()).toEqual({ pageIndex: 1, pageSize: 20, totalCount: 25, totalPages: 2 });
  });

  it('applies the free-text search CLIENT-SIDE, resets the letter filter, and does not re-query', () => {
    const modules = [
      makeModule({ moduleID: 1, moduleTitle: 'Latest News', friendlyName: 'News' }),
      makeModule({ moduleID: 2, moduleTitle: 'Photo Gallery', friendlyName: 'Gallery' }),
    ];
    moduleServiceSpy.getModulesByPortal.and.returnValue(of({ data: modules, meta: {} }));
    component.ngOnInit();
    component.onFilterChange('P'); // a pre-existing letter filter to prove the search clears it
    moduleServiceSpy.getModulesByPortal.calls.reset();

    component.onSearchChange({ text: 'news', type: '' });

    // MIGRATION: search is mutually exclusive with the letter filter (legacy parity) and is applied
    // client-side over title + friendly name (contains, case-insensitive); the service is NOT re-queried.
    expect(moduleServiceSpy.getModulesByPortal).not.toHaveBeenCalled();
    expect(component.searchText()).toBe('news');
    expect(component.activeFilter()).toBe('All');
    expect(component.rows().map((module) => module.moduleID)).toEqual([1]);
  });

  it('sorts the rows CLIENT-SIDE by the chosen column without re-querying', () => {
    const modules = [
      makeModule({ moduleID: 1, moduleTitle: 'Charlie' }),
      makeModule({ moduleID: 2, moduleTitle: 'Alpha' }),
      makeModule({ moduleID: 3, moduleTitle: 'Bravo' }),
    ];
    moduleServiceSpy.getModulesByPortal.and.returnValue(of({ data: modules, meta: {} }));
    component.ngOnInit();
    moduleServiceSpy.getModulesByPortal.calls.reset();

    component.onSortChange({ key: 'moduleTitle', direction: 'asc' });

    // MIGRATION: sorting reorders the client-side set (locale-aware comparator); no re-query is issued.
    expect(moduleServiceSpy.getModulesByPortal).not.toHaveBeenCalled();
    expect(component.rows().map((module) => module.moduleTitle)).toEqual(['Alpha', 'Bravo', 'Charlie']);
  });

  it('opens the confirmation dialog on a delete action', () => {
    const row = makeModule({ moduleID: 7 });
    const event: DataTableActionEvent<Module> = {
      action: { id: 'delete', label: 'Delete' },
      row,
    };

    component.onActionClick(event);

    expect(component.deleteDialogOpen()).toBe(true);
    expect(component.moduleToDelete()).toEqual(row);
  });

  it('deletes the module on confirm and re-queries (server decides hard vs soft)', () => {
    const row = makeModule({ moduleID: 7, moduleTitle: 'Doomed' });
    component.moduleToDelete.set(row);
    component.deleteDialogOpen.set(true);

    component.onConfirmDelete();

    expect(moduleServiceSpy.deleteModule).toHaveBeenCalledWith(7);
    expect(component.deleteDialogOpen()).toBe(false);
    expect(component.moduleToDelete()).toBeNull();
    expect(moduleServiceSpy.getModulesByPortal).toHaveBeenCalled();
    expect(component.successMessage()).toContain('Doomed');
  });

  it('navigates to edit and settings via row actions', () => {
    component.onActionClick({
      action: { id: 'edit', label: 'Edit' },
      row: makeModule({ moduleID: 4 }),
    });
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules', 4, 'edit']);

    component.onActionClick({
      action: { id: 'settings', label: 'Settings' },
      row: makeModule({ moduleID: 4 }),
    });
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules', 4, 'settings']);
  });

  it('navigates to edit on row click', () => {
    component.onRowClick(makeModule({ moduleID: 9 }));
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules', 9, 'edit']);
  });

  it('navigates to the new-module screen', () => {
    component.onNew();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules/new']);
  });

  it('surfaces an RFC 7807 error when loading fails', () => {
    const problem: ProblemDetails = { title: 'Server Error', status: 500, detail: 'Boom' };
    moduleServiceSpy.getModulesByPortal.and.returnValue(throwError(() => problem));

    component.ngOnInit();

    expect(component.error()).toBe('Boom');
    expect(component.rows()).toEqual([]);
    expect(component.loading()).toBe(false);
  });
});
