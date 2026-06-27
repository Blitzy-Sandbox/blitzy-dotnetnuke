// MIGRATION: Spec for PortalFormComponent (the Angular 19 replacement for DNN SiteSettings.ascx.vb). Verifies
// create vs. edit mode selection via route input binding, edit pre-load + form patch (Page_Load L266-419),
// host-field superuser gating (L498-516 / L759-770), the create/update service calls (cmdUpdate_Click ->
// UpdatePortalInfo), and RFC 7807 ProblemDetails error mapping. Gate 4:
// ng test --watch=false --browsers=ChromeHeadless --code-coverage (100% pass, non-interactive).
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { PortalFormComponent } from './portal-form.component';
import {
  PortalService,
  type CreatePortalRequest,
  type UpdatePortalRequest,
} from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { Portal } from '../../../core/models';

interface StubUser {
  isSuperUser: boolean;
}

function makePortal(overrides: Partial<Portal> = {}): Portal {
  const base: Portal = {
    portalId: 5,
    portalName: 'Acme Portal',
    guid: '11111111-1111-1111-1111-111111111111',
    userRegistration: 0,
    bannerAdvertising: 0,
    administratorId: 1,
    hostFee: 0,
    hostSpace: 0,
    pageQuota: 0,
    userQuota: 0,
    administratorRoleId: 0,
    registeredRoleId: 0,
    siteLogHistory: -1,
    adminTabId: 0,
    superTabId: 0,
    splashTabId: -1,
    homeTabId: -1,
    loginTabId: -1,
    userTabId: -1,
    timeZoneOffset: 0,
  };
  return { ...base, ...overrides };
}

// MIGRATION: in create mode the backend CreatePortalValidator REQUIRES the portal email + the admin* provisioning
// group, so the typed form is invalid until they are filled. This helper populates them (plus portalName) so
// submit() proceeds to the create() call.
function fillRequiredCreateFields(component: PortalFormComponent): void {
  component.form.controls.portalName.setValue('New Portal');
  component.form.controls.email.setValue('portal@example.com');
  component.form.controls.adminUsername.setValue('admin');
  component.form.controls.adminPassword.setValue('P@ssw0rd!');
  component.form.controls.adminFirstName.setValue('Ada');
  component.form.controls.adminLastName.setValue('Min');
  component.form.controls.adminEmail.setValue('admin@example.com');
}

describe('PortalFormComponent', () => {
  let fixture: ComponentFixture<PortalFormComponent>;
  let component: PortalFormComponent;
  let getByIdSpy: jasmine.Spy;
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let currentUser: WritableSignal<StubUser | null>;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makePortal()));
    createSpy = jasmine.createSpy('create').and.returnValue(of(makePortal()));
    updateSpy = jasmine.createSpy('update').and.returnValue(of(makePortal()));
    currentUser = signal<StubUser | null>(null);

    const portalServiceStub = { getById: getByIdSpy, create: createSpy, update: updateSpy };
    const authStub = { currentUser };

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PortalService, useValue: portalServiceStub },
        { provide: AuthService, useValue: authStub },
      ],
    });

    fixture = TestBed.createComponent(PortalFormComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('creates the component', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('starts empty in create mode and submits via create()', () => {
    fixture.detectChanges();

    expect(component.isEditMode()).toBe(false);
    expect(getByIdSpy).not.toHaveBeenCalled();
    expect(component.form.controls.portalName.value).toBe('');
    expect(component.form.controls.currency.value).toBe('USD');

    // MIGRATION: create requires email + admin* (CreatePortalValidator) -> fill them so the form is valid.
    fillRequiredCreateFields(component);
    component.submit();

    expect(createSpy).toHaveBeenCalledTimes(1);
    const dto = createSpy.calls.mostRecent().args[0] as CreatePortalRequest;
    expect(dto.portalName).toBe('New Portal');
    expect(dto.currency).toBe('USD');
    // MIGRATION: the create payload must carry the backend-REQUIRED email + admin* provisioning fields.
    expect(dto.email).toBe('portal@example.com');
    expect(dto.adminUsername).toBe('admin');
    expect(dto.adminEmail).toBe('admin@example.com');
    expect(updateSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/portals', 5]);
  });

  it('pre-loads and patches the form in edit mode, then submits via update()', () => {
    getByIdSpy.and.returnValue(of(makePortal({ portalId: 5, portalName: 'Existing', currency: 'EUR' })));

    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(component.isEditMode()).toBe(true);
    expect(getByIdSpy).toHaveBeenCalledWith('5');
    expect(component.form.controls.portalName.value).toBe('Existing');
    expect(component.form.controls.currency.value).toBe('EUR');

    component.submit();

    expect(updateSpy).toHaveBeenCalledTimes(1);
    const args = updateSpy.calls.mostRecent().args;
    expect(args[0]).toBe('5');
    const updateDto = args[1] as UpdatePortalRequest;
    expect(updateDto.portalName).toBe('Existing');
    // MIGRATION: portalId is echoed from the route into the update body; email is create-only (absent here).
    expect(updateDto.portalId).toBe(5);
    expect('email' in updateDto).toBe(false);
    expect(createSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/portals', 5]);
  });

  it('disables and hides host-only fields for non-superusers', () => {
    currentUser.set({ isSuperUser: false });
    fixture.detectChanges();

    expect(component.isSuperUser()).toBe(false);
    expect(component.form.controls.hostFee.disabled).toBe(true);
    expect(component.form.controls.expiryDate.disabled).toBe(true);

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('input[formControlName="hostFee"]')).toBeNull();
  });

  it('enables and shows host-only fields for superusers', () => {
    currentUser.set({ isSuperUser: true });
    fixture.detectChanges();

    expect(component.isSuperUser()).toBe(true);
    expect(component.form.controls.hostFee.enabled).toBe(true);

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('input[formControlName="hostFee"]')).not.toBeNull();
  });

  it('maps a server-side ProblemDetails error into the problem signal', () => {
    const problem = {
      type: 'https://httpstatuses.io/400',
      title: 'Validation failed',
      status: 400,
      errors: { portalName: ['Portal name already exists.'] },
    };
    createSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 400 })));

    fixture.detectChanges();
    // MIGRATION: fill the required create fields (email + admin*) so the form is valid and submit() reaches create().
    fillRequiredCreateFields(component);
    component.form.controls.portalName.setValue('Dupe');
    component.submit();

    expect(component.problem()?.status).toBe(400);
    const errors = component.problem()?.errors;
    expect(Array.isArray(errors)).toBe(false);
    expect((errors as Record<string, string[]>)['portalName']).toEqual(['Portal name already exists.']);
    expect(component.submitting()).toBe(false);
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  // MIGRATION: (QA Issue 1, secondary impact) a 500 ProblemDetails carries an EMPTY `errors` object (not a flat
  // array), so the form-level summary must fall back to title + detail. Previously errorSummary returned [] for an
  // object-shaped `errors` and the server error rendered nowhere. This locks in the never-silent behavior.
  it('surfaces the RFC 7807 title and detail in the error summary on a 500 (no field errors)', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'An unexpected error occurred.',
      status: 500,
      detail: 'Object reference not set to an instance of an object.',
      errors: {},
    };
    createSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 500 })));

    fixture.detectChanges();
    fillRequiredCreateFields(component);
    component.submit();

    expect(component.problem()?.status).toBe(500);
    expect(component.errorSummary()).toEqual([
      'An unexpected error occurred.',
      'Object reference not set to an instance of an object.',
    ]);

    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const alert = host.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('An unexpected error occurred.');
    expect(alert?.textContent).toContain('Object reference not set to an instance of an object.');
  });
});
