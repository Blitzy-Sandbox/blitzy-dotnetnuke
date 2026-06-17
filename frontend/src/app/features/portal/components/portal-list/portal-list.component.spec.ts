// Karma/Jasmine unit suite for PortalListComponent (./portal-list.component).
//
// Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless` -> 100% pass) and contributes
// ZERO errors/warnings to Gate 3 (Angular-19-strict type-check; spec files are excluded from the
// production `ng build` via tsconfig.app.json but must still type-check cleanly under tsconfig.spec.json).
//
// The component is standalone, so it is registered in TestBed `imports` (NOT `declarations`); its child
// standalone blocks (app-data-table, app-confirmation-dialog, app-loading-spinner, *appHasPermission)
// are pulled in transitively via the component's own `imports`. Every injected collaborator
// (PortalService, Router, AuthService) is replaced with a precisely-typed test double so NO real HTTP
// or routing is exercised. The synchronous `of(...)` stubs make each `subscribe` callback run inline,
// so post-conditions are asserted immediately after the call (no fakeAsync/tick).
//
// DESIGN DECISION (per the file mandate): only the FIRST spec calls `fixture.detectChanges()` (to prove
// the template compiles and renders `app-data-table`). All other specs drive the component class
// directly through its public methods and assert on its signals + the service spies — keeping the
// suite fast and avoiding a re-entrant `ngOnInit`.
//
// RECONCILIATION NOTES (the real dependency files are the source of truth, NOT the prompt sketch):
//   1. `User` (core/models/user.model.ts) is the 16-field backend wire shape with System.Text.Json
//      camelCase + acronym casing: `userID` / `portalID` / `affiliateID` (CAPITAL ID). The component's
//      delete-current-portal guard reads `currentUser()?.portalID`, so `mockUser.portalID` (0) drives it.
//   2. The real `Portal` interface has 37 fields: `processorPassword` is INTENTIONALLY ABSENT from the
//      read model (write-only credential, never projected by the API), so `makePortal` omits it.
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
 * Build a COMPLETE, fully-typed `Portal` read-model fixture (all 37 wire fields, no `any`). Field
 * names, types, and nullability mirror the real `../../models` `Portal` interface exactly. The
 * write-only `processorPassword` credential is deliberately NOT a member of the read model and is
 * therefore absent here. `Partial<Portal>` overrides are spread last so a spec tweaks only the fields
 * it asserts on while every required member stays populated.
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
 * Fully-typed `User` fixture (16 fields) matching the real `core/models/user.model.ts` contract,
 * including the backend acronym casing `userID` / `portalID` / `affiliateID`. `portalID` is fixed to 0
 * so the component's delete-current-portal guard (`row.portalID === currentUser()?.portalID`) hides the
 * delete action for portal 0 and shows it for any other portal.
 */
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
  roles: ['Administrators'],
  createdDate: null,
  lastLoginDate: null,
  lastPasswordChangeDate: null,
  lastActivityDate: null,
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

    // Minimal, precisely-typed AuthService double: a REAL writable `currentUser` signal (read by the
    // component's `currentPortalId` computed AND the imported HasPermissionDirective) plus a `hasRole`
    // that always grants (so any permission-gated template affordance renders deterministically).
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
    const deleteAction = component.actions.find((action) => action.id === 'delete') as
      | DataTableAction<Portal>
      | undefined;
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
});
