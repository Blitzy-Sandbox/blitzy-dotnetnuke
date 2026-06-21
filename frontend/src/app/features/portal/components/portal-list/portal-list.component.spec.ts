import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import type { Portal } from '../../models';
import { PortalService } from '../../services';
import type { PagedResponse, ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import type { User } from '../../../../core/models/user.model';
import type {
  DataTableAction,
  DataTableActionEvent,
} from '../../../../shared/components/data-table';
import { PortalListComponent } from './portal-list.component';

/**
 * Karma/Jasmine unit-test suite for {@link PortalListComponent}, the host
 * administrative portals grid (MIGRATION: legacy `Website/admin/Portal/Portals.ascx.vb`).
 *
 * Strategy:
 *  - Standalone TestBed (`imports: [PortalListComponent]`); NO NgModule setup.
 *  - All data dependencies are mocked so NO real HTTP occurs: `PortalService` is a
 *    Jasmine spy, `Router` is a Jasmine spy, and `AuthService` is a minimal stub that
 *    exposes a REAL `signal` for `currentUser` (so the component's `currentPortalId`
 *    computed AND the imported `HasPermissionDirective` both resolve) plus a `hasRole`
 *    function.
 *  - Logic is driven by calling component methods directly and asserting on the public
 *    signals + the service-spy calls. Only the first ("render") test calls
 *    `fixture.detectChanges()` (to verify the template compiles and `app-data-table`
 *    renders); every other test avoids it to stay fast and prevent a re-entrant
 *    `ngOnInit`.
 */

/**
 * Build a fully-populated read `Portal` (the camelCase wire shape) with optional
 * field overrides for a given test. Mirrors the backend `PortalDto`.
 *
 * MIGRATION: the read `Portal` interface intentionally OMITS `processorPassword`
 * (the payment-processor credential is write-only on Create/Update requests and is
 * never echoed to the SPA), so it is absent here too.
 */
function makePortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalID: 1,
    portalName: 'Test Portal',
    logoFile: null,
    footerText: null,
    expiryDate: null,
    userRegistration: 0,
    bannerAdvertising: 0,
    administratorId: 1,
    currency: 'USD',
    hostFee: 0,
    hostSpace: 0,
    pageQuota: 0,
    userQuota: 0,
    administratorRoleId: 0,
    administratorRoleName: null,
    registeredRoleId: 0,
    registeredRoleName: null,
    description: null,
    keyWords: null,
    backgroundFile: null,
    guid: '00000000-0000-0000-0000-000000000000',
    paymentProcessor: null,
    processorUserId: null,
    siteLogHistory: 0,
    email: null,
    adminTabId: 0,
    superTabId: 0,
    users: 0,
    pages: 0,
    splashTabId: 0,
    homeTabId: 0,
    loginTabId: 0,
    userTabId: 0,
    defaultLanguage: null,
    timeZoneOffset: 0,
    homeDirectory: null,
    version: null,
    ...overrides,
  };
}

/**
 * Authenticated host user used to seed the `AuthService` stub.
 *
 * MIGRATION: the client `User` wire-shape exposes the identifiers as `userID` /
 * `portalID` (System.Text.Json camelCase preserves a trailing acronym), NOT
 * `userId` / `portalId`. `portalID` is intentionally `0` so the delete-current-portal
 * guard (`actions` delete `hidden(row) => row.portalID === currentPortalId()`) is
 * exercised against the active portal.
 */
const mockUser: User = {
  userID: 1,
  username: 'host',
  displayName: 'Host',
  firstName: 'Host',
  lastName: 'User',
  email: 'host@example.com',
  portalID: 0,
  isSuperUser: true,
  roles: ['Administrators'],
};

describe('PortalListComponent', () => {
  let component: PortalListComponent;
  let fixture: ComponentFixture<PortalListComponent>;
  let portalServiceSpy: jasmine.SpyObj<PortalService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    portalServiceSpy = jasmine.createSpyObj<PortalService>('PortalService', [
      'getPortals',
      'getAllPortals',
      'deletePortal',
    ]);
    const emptyPage: PagedResponse<Portal> = { data: [], meta: {} };
    portalServiceSpy.getPortals.and.returnValue(of(emptyPage));
    portalServiceSpy.getAllPortals.and.returnValue(of([]));
    portalServiceSpy.deletePortal.and.returnValue(of(void 0));

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    const authStub = {
      currentUser: signal<User | null>(mockUser),
      hasRole: (_role: string): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [PortalListComponent],
      providers: [
        { provide: PortalService, useValue: portalServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: authStub as unknown as AuthService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalListComponent);
    component = fixture.componentInstance;
  });

  it('creates and renders the data table', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
    const dataTable = (fixture.nativeElement as HTMLElement).querySelector('app-data-table');
    expect(dataTable).toBeTruthy();
  });

  it('loads all portals on init (All filter -> getAllPortals)', () => {
    const portals = [makePortal({ portalID: 1 }), makePortal({ portalID: 2 })];
    portalServiceSpy.getAllPortals.and.returnValue(of(portals));

    component.ngOnInit();

    expect(portalServiceSpy.getAllPortals).toHaveBeenCalled();
    expect(component.rows()).toEqual(portals);
    expect(component.meta()).toBeNull();
    expect(component.loading()).toBe(false);
  });

  it('letter filter triggers a paged getPortals query (0-based index, pageSize 20)', () => {
    const page: PagedResponse<Portal> = {
      data: [makePortal({ portalID: 3 })],
      meta: { pageIndex: 0, pageSize: 20, totalCount: 1, totalPages: 1 },
    };
    portalServiceSpy.getPortals.and.returnValue(of(page));

    component.onFilterChange('A');

    expect(portalServiceSpy.getPortals).toHaveBeenCalledWith('A', 0, 20);
    expect(component.rows()).toEqual(page.data);
    expect(component.meta()).toEqual(page.meta);
  });

  it('paging re-queries getPortals with the new 0-based index', () => {
    portalServiceSpy.getPortals.and.returnValue(of({ data: [], meta: {} }));
    component.onFilterChange('B');
    portalServiceSpy.getPortals.calls.reset();

    component.onPageChange(2);

    expect(portalServiceSpy.getPortals).toHaveBeenCalledWith('B', 2, 20);
  });

  it('Expired filter loads all portals and filters client-side on expiryDate', () => {
    const portals = [
      makePortal({ portalID: 1, expiryDate: '2000-01-01T00:00:00Z' }),
      makePortal({ portalID: 2, expiryDate: '2999-01-01T00:00:00Z' }),
      makePortal({ portalID: 3, expiryDate: null }),
    ];
    portalServiceSpy.getAllPortals.and.returnValue(of(portals));

    component.onFilterChange('Expired');

    expect(portalServiceSpy.getAllPortals).toHaveBeenCalled();
    expect(component.rows().length).toBe(1);
    expect(component.rows()[0].portalID).toBe(1);
  });

  it('hides the delete action for the current portal (delete-current-portal guard)', () => {
    const deleteAction: DataTableAction<Portal> | undefined = component.actions.find(
      (action) => action.id === 'delete',
    );
    expect(deleteAction).toBeTruthy();
    expect(deleteAction?.hidden?.(makePortal({ portalID: 0 }))).toBe(true);
    expect(deleteAction?.hidden?.(makePortal({ portalID: 5 }))).toBe(false);
  });

  it('opens the confirmation dialog on a delete action', () => {
    const row = makePortal({ portalID: 7 });
    const event: DataTableActionEvent<Portal> = {
      action: { id: 'delete', label: 'Delete' },
      row,
    };

    component.onActionClick(event);

    expect(component.deleteDialogOpen()).toBe(true);
    expect(component.portalToDelete()).toEqual(row);
  });

  it('deletes the portal on confirm and re-queries', () => {
    const row = makePortal({ portalID: 7, portalName: 'Doomed' });
    component.portalToDelete.set(row);
    component.deleteDialogOpen.set(true);

    component.onConfirmDelete();

    expect(portalServiceSpy.deletePortal).toHaveBeenCalledWith(7);
    expect(component.deleteDialogOpen()).toBe(false);
    expect(component.portalToDelete()).toBeNull();
    expect(portalServiceSpy.getAllPortals).toHaveBeenCalled();
    expect(component.successMessage()).toContain('Doomed');
  });

  it('navigates to edit and settings via row actions', () => {
    component.onActionClick({
      action: { id: 'edit', label: 'Edit' },
      row: makePortal({ portalID: 4 }),
    });
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals', 4, 'edit']);

    component.onActionClick({
      action: { id: 'settings', label: 'Settings' },
      row: makePortal({ portalID: 4 }),
    });
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals', 4, 'settings']);
  });

  it('navigates to the new-portal screen', () => {
    component.onNew();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals/new']);
  });

  it('surfaces an RFC 7807 error when loading fails', () => {
    const problem: ProblemDetails = { title: 'Server Error', status: 500, detail: 'Boom' };
    portalServiceSpy.getAllPortals.and.returnValue(throwError(() => problem));

    component.ngOnInit();

    expect(component.error()).toBe('Boom');
    expect(component.rows()).toEqual([]);
    expect(component.loading()).toBe(false);
  });

  // MIGRATION (QA Finding C): a FAILED delete must surface the error WITHOUT
  // wiping the grid. Contrast the load-error test above (rows correctly cleared)
  // with this delete-error test: the delete did not mutate anything, so the
  // displayed records must be retained rather than blanked to "No portals found.".
  it('preserves the grid on a failed delete and surfaces the error (QA Finding C)', () => {
    const portals = [
      makePortal({ portalID: 7, portalName: 'Keep Me' }),
      makePortal({ portalID: 8, portalName: 'Also Keep' }),
    ];
    component.rows.set(portals);
    component.meta.set(null);

    const problem: ProblemDetails = {
      title: 'Service Unavailable',
      status: 503,
      detail: 'Database unreachable.',
    };
    portalServiceSpy.deletePortal.and.returnValue(throwError(() => problem));
    component.portalToDelete.set(portals[0]);
    component.deleteDialogOpen.set(true);

    component.onConfirmDelete();

    // The error banner is surfaced...
    expect(component.error()).toBe('Database unreachable.');
    expect(component.loading()).toBe(false);
    // ...but the grid is NOT wiped: the records still exist, so the rows are retained.
    expect(component.rows()).toEqual(portals);
    expect(component.rows().length).toBe(2);
  });
});
