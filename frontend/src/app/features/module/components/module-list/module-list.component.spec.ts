import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { ModuleListComponent } from './module-list.component';
import { ModuleService } from '../../services';
import { Module, VisibilityState } from '../../models';
import { PagedResponse, QueryParams } from '../../../../core/services/api.service';
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
 *   - every GET /api/v1/modules query carries the required `portalId` discriminator (CP5 Critical finding);
 *   - the discriminator survives a re-query event (paging) — not just the initial load;
 *   - a missing JWT portal claim is surfaced as an error and NO unscoped request is issued.
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

  it('keeps sending portalId on every re-query event (paging)', () => {
    currentUser.set(buildUser({ portalID: 3 }));

    const component = createComponent();
    component.ngOnInit(); // initial load (call 1)

    component.onPageChange(2); // re-query for page index 2 (call 2)

    expect(moduleServiceSpy.getModules).toHaveBeenCalledTimes(2);
    const params: QueryParams = moduleServiceSpy.getModules.calls.mostRecent().args[0]!;
    expect(params['portalId']).toBe(3);
    expect(params['pageIndex']).toBe(2);
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
});
