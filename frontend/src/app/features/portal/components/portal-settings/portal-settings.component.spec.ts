// MIGRATION: Karma/Jasmine unit spec for PortalSettingsComponent (Gate 4 -> ng test
// --watch=false --browsers=ChromeHeadless). Verifies the in-scope behavior of the portal
// configuration editor (load -> patch form, save -> UpdatePortalRequest, server-error mapping,
// host-only delete gating, and navigation) that the legacy Website/admin/Portal/SiteSettings.ascx.vb
// control performed via Web Forms postback. NO `any`; relative imports only.
//
// The component is standalone, so it is registered in TestBed `imports` (NOT `declarations`) and its
// child standalone components/directives (app-form-controls, app-confirmation-dialog,
// app-loading-spinner, *appHasPermission) are pulled in transitively. All injected collaborators
// (PortalService, AuthService, Router, ActivatedRoute) are replaced with precisely-typed test
// doubles so no real HTTP/router wiring is exercised. The AuthService double exposes BOTH the
// `currentUser` signal (read by `canDelete`) AND `hasRole` (called by HasPermissionDirective) so the
// template renders deterministically.

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { signal, WritableSignal } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';

import { PortalSettingsComponent } from './portal-settings.component';
import { PortalService } from '../../services';
import { AuthService } from '../../../../core/auth/auth.service';
import { Portal, UpdatePortalRequest } from '../../models';
import { User } from '../../../../core/models/user.model';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Build a fully-populated `Portal` read-model fixture (the GET projection — 37 fields; the write-only
 * `processorPassword` credential is intentionally absent from the read model, and `users`/`pages` are
 * server-derived metrics present only on reads). Every field is given a deterministic, non-null value
 * so the form-patch and save-request assertions are meaningful and type-checking of `Portal` is total.
 */
function buildMockPortal(): Portal {
  return {
    portalID: 1,
    portalName: 'Test Portal',
    logoFile: 'logo.gif',
    footerText: 'Copyright 2024',
    expiryDate: '2099-12-31T00:00:00.000Z',
    userRegistration: 2,
    bannerAdvertising: 1,
    administratorId: 7,
    currency: 'USD',
    hostFee: 25,
    hostSpace: 100,
    pageQuota: 50,
    userQuota: 500,
    administratorRoleId: 3,
    administratorRoleName: 'Administrators',
    registeredRoleId: 4,
    registeredRoleName: 'Registered Users',
    description: 'A test portal',
    keyWords: 'test, portal',
    backgroundFile: 'bg.png',
    guid: 'abc-123',
    paymentProcessor: 'PayPal',
    processorUserId: 'merchant-1',
    siteLogHistory: 30,
    email: 'admin@test.com',
    adminTabId: 10,
    superTabId: 11,
    users: 42,
    pages: 17,
    splashTabId: 12,
    homeTabId: 13,
    loginTabId: 14,
    userTabId: 15,
    defaultLanguage: 'en-US',
    timeZoneOffset: -480,
    homeDirectory: 'Portals/0',
    version: '09.00.00',
  };
}

/**
 * Build a fully-typed `User` fixture (16 fields). `portalID` (the camelCase serialization of the C#
 * `PortalID` property — note the capital ID) drives `canDelete`: the legacy delete affordance is
 * hidden when an admin edits their OWN portal.
 */
function buildMockUser(portalID: number): User {
  return {
    userID: 100,
    portalID,
    affiliateID: null,
    username: 'admin',
    displayName: 'Admin User',
    email: 'admin@test.com',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: false,
    approved: true,
    roles: ['Administrators'],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
  };
}

describe('PortalSettingsComponent', () => {
  let component: PortalSettingsComponent;
  let fixture: ComponentFixture<PortalSettingsComponent>;
  let portalService: jasmine.SpyObj<PortalService>;
  let router: jasmine.SpyObj<Router>;
  // Declared at suite scope but re-created in beforeEach so each spec starts from a known
  // (portalID 99) state regardless of Jasmine's randomized spec order.
  let currentUser: WritableSignal<User | null>;

  beforeEach(async () => {
    portalService = jasmine.createSpyObj<PortalService>('PortalService', [
      'getPortal',
      'updatePortal',
      'deletePortal',
    ]);
    portalService.getPortal.and.returnValue(of(buildMockPortal()));
    portalService.updatePortal.and.returnValue(of(buildMockPortal()));
    portalService.deletePortal.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    currentUser = signal<User | null>(buildMockUser(99));
    // Minimal, precisely-typed AuthService double: `currentUser` (read by canDelete) and `hasRole`
    // (called by HasPermissionDirective for *appHasPermission) are the only members the component
    // and its template touch. `hasRole` returns true so the permission-gated template sections render.
    const authStub: Pick<AuthService, 'currentUser' | 'hasRole'> = {
      currentUser,
      hasRole: (): boolean => true,
    };
    // ActivatedRoute double: `snapshot.paramMap` supplies `:id = '1'` so ngOnInit's Number('1') path
    // runs (not the NaN guard). Left structurally inferred and supplied via `useValue` so no `any`
    // annotation and no type assertion are needed.
    const activatedRouteStub = {
      snapshot: { paramMap: convertToParamMap({ id: '1' }) },
    };

    await TestBed.configureTestingModule({
      imports: [PortalSettingsComponent],
      providers: [
        { provide: PortalService, useValue: portalService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers ngOnInit -> loadPortal(1)
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('loads the portal on init and patches the form', () => {
    expect(portalService.getPortal).toHaveBeenCalledOnceWith(1);
    expect(component.portal()).toEqual(buildMockPortal());
    expect(component.form.controls.portalName.value).toBe('Test Portal');
    expect(component.loading()).toBeFalse();
  });

  it('exposes the GUID uppercased via guidDisplay', () => {
    expect(component.guidDisplay()).toBe('ABC-123');
  });

  it('requires portalName and guards onSubmit when the form is invalid', () => {
    component.form.controls.portalName.setValue('');

    expect(component.form.controls.portalName.invalid).toBeTrue();

    component.onSubmit();

    expect(portalService.updatePortal).not.toHaveBeenCalled();
  });

  it('builds a valid UpdatePortalRequest carrying the route id and edited values', () => {
    // portalName remains valid ('Test Portal' from the load); edit a host field to prove form
    // values flow into the request.
    component.form.controls.hostFee.setValue(999);

    component.onSubmit();

    expect(portalService.updatePortal).toHaveBeenCalledTimes(1);
    const args = portalService.updatePortal.calls.mostRecent().args;
    expect(args[0]).toBe(1);
    const request: UpdatePortalRequest = args[1];
    expect(request.portalID).toBe(1);
    expect(request.hostFee).toBe(999);
    // The request is the write contract: server-derived read-only metrics must not be sent back.
    expect('users' in request).toBeFalse();
    expect('pages' in request).toBeFalse();
    expect(component.saved()).toBeTrue();
    expect(component.saving()).toBeFalse();
  });

  it('maps ProblemDetails.errors to serverErrors when the save fails', () => {
    portalService.updatePortal.and.returnValue(
      throwError(() => ({ status: 400, errors: { portalName: ['bad'] } }) as ProblemDetails),
    );

    component.onSubmit();

    expect(component.serverErrors()).toEqual({ portalName: ['bad'] });
    expect(component.saving()).toBeFalse();
  });

  it('sets loadError when the portal fails to load', () => {
    portalService.getPortal.and.returnValue(
      throwError(() => ({ title: 'Not found' }) as ProblemDetails),
    );

    // Re-run init with the failing stub in place (deterministic; no second fixture required).
    component.ngOnInit();

    expect(component.loadError()).toBeTruthy();
    expect(component.loading()).toBeFalse();
  });

  it('reflects the current portal in canDelete', () => {
    // Editing a DIFFERENT portal (route id 1, current user's portal 99): delete is allowed.
    expect(component.canDelete()).toBeTrue();

    // Editing one's OWN portal (current user's portal becomes 1): delete is hidden.
    currentUser.set(buildMockUser(1));
    expect(component.canDelete()).toBeFalse();
  });

  it('opens, confirms, and cancels the delete dialog', () => {
    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();

    component.onConfirmDelete();
    expect(component.showDeleteDialog()).toBeFalse();
    expect(portalService.deletePortal).toHaveBeenCalledOnceWith(1);
    expect(router.navigate).toHaveBeenCalledWith(['/portals']);

    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();
    component.onCancelDelete();
    expect(component.showDeleteDialog()).toBeFalse();
  });

  it('navigates back to the portal list on cancel', () => {
    component.onCancel();

    expect(router.navigate).toHaveBeenCalledWith(['/portals']);
  });
});
