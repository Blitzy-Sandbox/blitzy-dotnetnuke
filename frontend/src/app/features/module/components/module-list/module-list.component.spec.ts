import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ModuleListComponent } from './module-list.component';
import { ModuleService } from '../../services';
import { Module, VisibilityState } from '../../models';
import { PagedResponse, ProblemDetails, QueryParams } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';

/**
 * Unit tests for ModuleListComponent (Gate 4: ng test --watch=false --browsers=ChromeHeadless).
 *
 * MIGRATION: ModuleListComponent reinterprets the legacy portal-scoped DotNetNuke module-administration
 * grid (Website/admin/Modules + ModuleController.GetModules(PortalId)) as a standalone Angular 19 screen.
 * The legacy grid was always scoped to PortalModuleBase.PortalId; the rewrite derives that scope from the
 * JWT-authenticated current user. The backend ModulesController.Get REQUIRES a scope discriminator
 * (portalId or tabId) and returns 400 ("Either portalId or tabId query parameter is required.") when
 * neither is supplied, so these tests PIN the contract the screen depends on:
 *   - the single GET /api/v1/modules query carries the required `portalId` discriminator (CP5 Critical finding);
 *   - a missing JWT portal claim is surfaced as an error and NO unscoped request is issued;
 *   - MIGRATION (DEV-070 / Finding 4): paging, letter filtering, free-text search, and sorting are applied
 *     CLIENT-side — they derive the displayed page from the once-fetched full set and issue NO further
 *     requests — and the component exposes a SYNTHETIC pager meta computed from the filtered length.
 *
 * The component class is exercised in ISOLATION: TestBed.createComponent instantiates it and ngOnInit is
 * invoked MANUALLY. fixture.detectChanges() is intentionally NOT called — rendering the template would
 * require the real shared DataTable/ConfirmationDialog/LoadingSpinner the component imports, whereas these
 * specs only assert the component's observable behaviour. ModuleService is faked (of()) so subscriptions
 * resolve synchronously before the assertions run (no fakeAsync/tick required), and AuthService is provided
 * as a minimal { currentUser } signal stub (no real token storage is exercised).
 */

/** Builds a complete authenticated {@link User} so `authService.currentUser()` returns a realistic record. */
function buildUser(overrides: Partial<User> = {}): User {
  return {
    userID: 1,
    username: 'admin',
    displayName: 'System Administrator',
    firstName: 'System',
    lastName: 'Administrator',
    email: 'admin@dnnmigration.local',
    portalID: 7,
    isSuperUser: true,
    roles: ['Administrators'],
    ...overrides,
  };
}

/** Builds a minimal {@link Module} row so a non-empty list response can be asserted when needed. */
function buildModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleID: 1,
    portalID: 7,
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
    friendlyName: null,
    description: null,
    version: null,
    isDeleted: false,
    ...overrides,
  };
}

/**
 * Builds N modules with the given titles (moduleID auto-incremented), so client-side
 * paging/filter/search/sort can be asserted against a realistic full set. The optional
 * `friendlyName` per title lets the free-text search test match a non-title column.
 */
function buildModules(specs: ReadonlyArray<{ title: string; friendlyName?: string }>): Module[] {
  return specs.map((spec, index) =>
    buildModule({
      moduleID: index + 1,
      moduleTitle: spec.title,
      friendlyName: spec.friendlyName ?? null,
    }),
  );
}

/** Builds the unpaged server response (the API returns the full active set with an EMPTY meta). */
function fullPage(modules: Module[]): PagedResponse<Module> {
  return { data: modules, meta: {} };
}

/** Convenience: an empty paged response (the default getModules return). */
function emptyPage(): PagedResponse<Module> {
  return { data: [], meta: {} };
}

describe('ModuleListComponent', () => {
  let moduleServiceSpy: jasmine.SpyObj<ModuleService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let currentUser: WritableSignal<User | null>;

  function createComponent(): ModuleListComponent {
    return TestBed.createComponent(ModuleListComponent).componentInstance;
  }

  beforeEach(() => {
    // Default: an authenticated admin in portal 7. The null-portal test overrides this BEFORE ngOnInit.
    currentUser = signal<User | null>(buildUser());

    moduleServiceSpy = jasmine.createSpyObj<ModuleService>('ModuleService', [
      'getModules',
      'deleteModule',
    ]);
    moduleServiceSpy.getModules.and.returnValue(of(emptyPage()));

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [ModuleListComponent],
      providers: [
        { provide: ModuleService, useValue: moduleServiceSpy },
        { provide: Router, useValue: routerSpy },
        // Minimal AuthService stub: the component only reads the `currentUser` signal. useValue is `any`,
        // so the signal satisfies the injected AuthService shape without a cast; each test can set the
        // signal value BEFORE createComponent()/ngOnInit().
        { provide: AuthService, useValue: { currentUser } },
      ],
    });
  });

  it('sends the REQUIRED portalId discriminator to getModules on initial load', () => {
    currentUser.set(buildUser({ portalID: 7 }));

    const component = createComponent();
    component.ngOnInit();

    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1);
    const params: QueryParams = moduleServiceSpy.getModules.calls.mostRecent().args[0]!;
    // The wire key is the camelCased query param `portalId` (matches backend [FromQuery] int? portalId).
    expect(params['portalId']).toBe(7);
    // No error banner on the happy path.
    expect(component.error()).toBeNull();
  });

  it('does NOT re-query on paging — the page slice is derived client-side (DEV-070)', () => {
    currentUser.set(buildUser({ portalID: 3 }));
    // 25 modules => 2 pages at PAGE_SIZE = 20.
    const titles = Array.from({ length: 25 }, (_, i) => ({ title: `Module ${i + 1}` }));
    moduleServiceSpy.getModules.and.returnValue(of(fullPage(buildModules(titles))));

    const component = createComponent();
    component.ngOnInit(); // single full-set fetch

    // First page: 20 rows, synthetic meta reflects 25 items across 2 pages.
    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1);
    expect(component.rows().length).toBe(20);
    expect(component.meta().totalCount).toBe(25);
    expect(component.meta().totalPages).toBe(2);
    expect(component.meta().pageIndex).toBe(0);

    component.onPageChange(1); // advance to page 2

    // No re-query; the second-page slice (the remaining 5) is derived locally.
    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1);
    expect(component.rows().length).toBe(5);
    expect(component.meta().pageIndex).toBe(1);
  });

  it('populates the grid rows from the response when a portal is present', () => {
    currentUser.set(buildUser({ portalID: 0 }));
    moduleServiceSpy.getModules.and.returnValue(
      of<PagedResponse<Module>>({ data: [buildModule({ moduleID: 42 })], meta: {} }),
    );

    const component = createComponent();
    component.ngOnInit();

    const params: QueryParams = moduleServiceSpy.getModules.calls.mostRecent().args[0]!;
    // portalId === 0 (the default/host portal) is a VALID scope and MUST be sent (not treated as "missing").
    expect(params['portalId']).toBe(0);
    expect(component.rows().length).toBe(1);
    expect(component.rows()[0].moduleID).toBe(42);
    expect(component.loading()).toBeFalse();
  });

  it('surfaces an error and issues NO request when the current portal cannot be determined', () => {
    currentUser.set(null);

    const component = createComponent();
    component.ngOnInit();

    expect(moduleServiceSpy.getModules).not.toHaveBeenCalled();
    expect(component.error()).toBe(
      'Unable to determine the current portal for the signed-in user.',
    );
    expect(component.rows().length).toBe(0);
    expect(component.loading()).toBeFalse();
  });

  it('filters by leading letter CLIENT-side without re-querying (DEV-070)', () => {
    currentUser.set(buildUser({ portalID: 7 }));
    moduleServiceSpy.getModules.and.returnValue(
      of(fullPage(buildModules([{ title: 'Alpha' }, { title: 'Beta' }, { title: 'Apex' }]))),
    );

    const component = createComponent();
    component.ngOnInit();

    component.onFilterChange('A');

    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1); // no re-query
    expect(component.activeFilter()).toBe('A');
    expect(component.rows().map((m) => m.moduleTitle)).toEqual(['Alpha', 'Apex']);
    expect(component.meta().totalCount).toBe(2);
  });

  it('searches free-text across title and friendly name CLIENT-side and clears the letter filter (DEV-070)', () => {
    currentUser.set(buildUser({ portalID: 7 }));
    moduleServiceSpy.getModules.and.returnValue(
      of(
        fullPage(
          buildModules([
            { title: 'Announcements' },
            { title: 'Contact Us', friendlyName: 'FeedbackForm' },
            { title: 'Links' },
          ]),
        ),
      ),
    );

    const component = createComponent();
    component.ngOnInit();
    component.onFilterChange('A'); // pre-set a letter filter to prove search clears it

    component.onSearchChange({ text: 'feedback', type: '' });

    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1); // no re-query
    expect(component.activeFilter()).toBe('All'); // search resets the letter filter
    // matched via the friendlyName 'FeedbackForm', proving search spans more than the title column
    expect(component.rows().map((m) => m.moduleTitle)).toEqual(['Contact Us']);
  });

  it('sorts by the moduleTitle column CLIENT-side honoring direction (DEV-070)', () => {
    currentUser.set(buildUser({ portalID: 7 }));
    moduleServiceSpy.getModules.and.returnValue(
      of(fullPage(buildModules([{ title: 'Charlie' }, { title: 'Alpha' }, { title: 'Bravo' }]))),
    );

    const component = createComponent();
    component.ngOnInit();

    component.onSortChange({ key: 'moduleTitle', direction: 'asc' });
    expect(component.rows().map((m) => m.moduleTitle)).toEqual(['Alpha', 'Bravo', 'Charlie']);

    component.onSortChange({ key: 'moduleTitle', direction: 'desc' });
    expect(component.rows().map((m) => m.moduleTitle)).toEqual(['Charlie', 'Bravo', 'Alpha']);

    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1); // no re-query for either sort
  });

  it('re-fetches the full set after a delete (the sole re-query trigger) (DEV-070)', () => {
    currentUser.set(buildUser({ portalID: 7 }));
    const module = buildModule({ moduleID: 9, moduleTitle: 'Doomed' });
    moduleServiceSpy.getModules.and.returnValue(of(fullPage([module])));
    moduleServiceSpy.deleteModule.and.returnValue(of(void 0));

    const component = createComponent();
    component.ngOnInit(); // fetch 1

    // Drive the real delete wiring: the delete action selects the row, then confirm issues DELETE.
    component.onActionClick({ action: component.actions[2], row: module });
    component.onConfirmDelete();

    expect(moduleServiceSpy.deleteModule).toHaveBeenCalledOnceWith(9);
    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(2); // delete is the sole re-fetch trigger
    expect(component.successMessage()).toBe('Module "Doomed" was deleted.');
  });

  // MIGRATION (QA Finding C): a FAILED delete must surface the error WITHOUT
  // wiping the grid. The delete did not mutate anything, so the displayed modules
  // must be retained rather than blanked to "No modules found.", and NO re-fetch
  // is issued on the error path.
  it('preserves the grid on a failed delete and surfaces the error (QA Finding C)', () => {
    currentUser.set(buildUser({ portalID: 7 }));
    const module = buildModule({ moduleID: 9, moduleTitle: 'Keep Me' });
    moduleServiceSpy.getModules.and.returnValue(of(fullPage([module])));

    const component = createComponent();
    component.ngOnInit(); // populate the grid (fetch 1)
    expect(component.rows().length).toBe(1);

    const problem: ProblemDetails = {
      title: 'Service Unavailable',
      status: 503,
      detail: 'Database unreachable.',
    };
    moduleServiceSpy.deleteModule.and.returnValue(throwError(() => problem));

    component.onActionClick({ action: component.actions[2], row: module });
    component.onConfirmDelete();

    expect(moduleServiceSpy.deleteModule).toHaveBeenCalledOnceWith(9);
    // The error banner is surfaced...
    expect(component.error()).toBe('Database unreachable.');
    expect(component.loading()).toBeFalse();
    // ...but the grid is NOT wiped: the records still exist, so the rows are retained.
    expect(component.rows().length).toBe(1);
    expect(component.rows()[0].moduleID).toBe(9);
    // ...and NO re-fetch happens on the delete-error path (only the initial load occurred).
    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(1);
  });
});
