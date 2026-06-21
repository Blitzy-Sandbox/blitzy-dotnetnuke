import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PortalSettingsComponent } from './portal-settings.component';
import { PortalService } from '../../services';
import { Portal, UpdatePortalRequest } from '../../models';
import { AuthService } from '../../../../core/auth/auth.service';
import { User } from '../../../../core/models/user.model';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Unit tests for PortalSettingsComponent (Gate 4: ng test --watch=false --browsers=ChromeHeadless).
 *
 * MIGRATION: PortalSettingsComponent reproduces the in-scope behaviour of the legacy Web Forms
 * control Website/admin/Portal/SiteSettings.ascx.vb (Page_Load bind + cmdUpdate_Click save +
 * cmdDelete flow) as a standalone Angular 19 reactive form. These specs pin the class-level
 * contract that the sibling portal.routes.ts and the backend PortalsController depend on:
 *   - the portal is loaded by route :id and patched into the form on init;
 *   - the GUID is displayed uppercased (lblGUID.Text = objPortal.GUID.ToString.ToUpper, L273);
 *   - save issues an UpdatePortalRequest whose portalID is FORCED to the route id and which omits
 *     the read-only server-derived metrics users/pages;
 *   - RFC 7807 ProblemDetails field errors surface through the serverErrors signal;
 *   - the delete affordance is hidden for the operator's own portal
 *     (cmdDelete.Visible = (intPortalId <> PortalId), L503).
 *
 * The component is exercised with the standard fixture.detectChanges() flow (which triggers
 * ngOnInit) so the real child components (FormControls / HasPermission / ConfirmationDialog /
 * LoadingSpinner) the component imports are also rendered (template integration). All PortalService
 * responses are synchronous (of()/throwError()) so subscriptions resolve before the assertions run
 * (no fakeAsync/tick required).
 */

/** Builds a complete Portal entity (all 37 wire fields) so getPortal returns a realistic record. */
function buildMockPortal(): Portal {
  return {
    portalID: 1,
    portalName: 'Test Portal',
    logoFile: 'logo.png',
    footerText: 'Copyright 2024',
    expiryDate: null,
    userRegistration: 2,
    bannerAdvertising: 1,
    administratorId: 2,
    currency: 'USD',
    hostFee: 0,
    hostSpace: 100,
    pageQuota: 50,
    userQuota: 200,
    administratorRoleId: 0,
    administratorRoleName: 'Administrators',
    registeredRoleId: 1,
    registeredRoleName: 'Registered Users',
    description: 'A test portal',
    keyWords: 'test, portal',
    backgroundFile: 'background.png',
    guid: 'abc-123',
    paymentProcessor: 'PayPal',
    processorUserId: 'merchant',
    siteLogHistory: 60,
    email: 'host@example.com',
    adminTabId: 10,
    superTabId: 0,
    users: 5,
    pages: 12,
    splashTabId: 20,
    homeTabId: 30,
    loginTabId: 40,
    userTabId: 50,
    defaultLanguage: 'en-US',
    timeZoneOffset: -480,
    homeDirectory: 'Portals/0',
    version: '04.09.00',
  };
}

/** Builds a typed authenticated User; portalID drives the canDelete() comparison. */
function buildMockUser(portalID = 99): User {
  return {
    userID: 1,
    username: 'admin',
    displayName: 'Administrator',
    firstName: 'Ad',
    lastName: 'Min',
    email: 'admin@example.com',
    portalID,
    isSuperUser: false,
    roles: ['Administrators'],
  };
}

describe('PortalSettingsComponent', () => {
  let component: PortalSettingsComponent;
  let fixture: ComponentFixture<PortalSettingsComponent>;
  let portalService: jasmine.SpyObj<PortalService>;
  let router: jasmine.SpyObj<Router>;
  const currentUser = signal<User | null>(buildMockUser());

  beforeEach(async () => {
    // Reset the shared signal so every spec starts from a known operator (portalID 99 != route 1).
    currentUser.set(buildMockUser());

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

    // Typed minimal AuthService double: the component reads currentUser()?.portalID and the
    // HasPermissionDirective in the template reads currentUser() + calls hasRole(role).
    const authStub: Pick<AuthService, 'currentUser' | 'hasRole'> = {
      currentUser,
      hasRole: (): boolean => true,
    };

    await TestBed.configureTestingModule({
      imports: [PortalSettingsComponent],
      providers: [
        { provide: PortalService, useValue: portalService },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers ngOnInit + the (synchronous) success load
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('loads the portal on init and patches the form', () => {
    expect(portalService.getPortal).toHaveBeenCalledWith(1);
    expect(component.portal()).toEqual(buildMockPortal());
    expect(component.form.controls.portalName.value).toBe('Test Portal');
    expect(component.loading()).toBeFalse();
  });

  it('exposes the GUID uppercased', () => {
    expect(component.guidDisplay()).toBe('ABC-123');
  });

  it('requires portalName and does not save an invalid form', () => {
    component.form.controls.portalName.setValue(null);

    expect(component.form.controls.portalName.invalid).toBeTrue();
    expect(component.form.invalid).toBeTrue();

    component.onSubmit();

    expect(portalService.updatePortal).not.toHaveBeenCalled();
  });

  it('builds an UpdatePortalRequest with the route id and omits users/pages on save', () => {
    component.form.controls.hostFee.setValue(999);
    component.onSubmit();

    expect(portalService.updatePortal).toHaveBeenCalledTimes(1);
    const [id, request] = portalService.updatePortal.calls.mostRecent().args;
    expect(id).toBe(1);
    expect(request.portalID).toBe(1);
    expect(request.hostFee).toBe(999);
    expect(Object.keys(request)).not.toContain('users');
    expect(Object.keys(request)).not.toContain('pages');
    expect(component.saved()).toBeTrue();
    expect(component.saving()).toBeFalse();
  });

  it('maps RFC 7807 ProblemDetails.errors into the serverErrors signal on a failed save', () => {
    portalService.updatePortal.and.returnValue(
      throwError(() => ({ status: 400, errors: { portalName: ['bad'] } }) as ProblemDetails),
    );

    component.onSubmit();

    expect(component.serverErrors()).toEqual({ portalName: ['bad'] });
    expect(component.saving()).toBeFalse();
  });

  it('reflects whether the operator may delete the current portal', () => {
    // route id 1, operator portalID 99 -> a different portal -> deletable.
    expect(component.canDelete()).toBeTrue();

    // operator now owns portal 1 (the one being edited) -> not deletable (fail-closed parity).
    currentUser.set(buildMockUser(1));
    expect(component.canDelete()).toBeFalse();
  });

  it('opens then confirms the delete flow', () => {
    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();

    component.onConfirmDelete();

    expect(component.showDeleteDialog()).toBeFalse();
    expect(portalService.deletePortal).toHaveBeenCalledWith(1);
    expect(router.navigate).toHaveBeenCalledWith(['/portals']);
  });

  it('closes the delete dialog on cancel without deleting', () => {
    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();

    component.onCancelDelete();

    expect(component.showDeleteDialog()).toBeFalse();
    expect(portalService.deletePortal).not.toHaveBeenCalled();
  });

  it('navigates back to the list on cancel', () => {
    component.onCancel();
    expect(router.navigate).toHaveBeenCalledWith(['/portals']);
  });

  describe('when the portal fails to load', () => {
    beforeEach(() => {
      // Reconfigure the load to fail, then re-create the fixture so ngOnInit takes the error path.
      portalService.getPortal.and.returnValue(
        throwError(() => ({ title: 'Not found' }) as ProblemDetails),
      );
      fixture = TestBed.createComponent(PortalSettingsComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    });

    it('sets loadError and clears loading', () => {
      expect(component.loadError()).toBeTruthy();
      expect(component.loadError()).toBe('Not found');
      expect(component.loading()).toBeFalse();
    });
  });
});
