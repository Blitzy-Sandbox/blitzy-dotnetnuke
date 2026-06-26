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
import { PortalService, type PortalRequest } from '../portal.service';
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

    component.form.controls.portalName.setValue('New Portal');
    component.submit();

    expect(createSpy).toHaveBeenCalledTimes(1);
    const dto = createSpy.calls.mostRecent().args[0] as PortalRequest;
    expect(dto.portalName).toBe('New Portal');
    expect(dto.currency).toBe('USD');
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
    expect((args[1] as PortalRequest).portalName).toBe('Existing');
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
    component.form.controls.portalName.setValue('Dupe');
    component.submit();

    expect(component.problem()?.status).toBe(400);
    const errors = component.problem()?.errors;
    expect(Array.isArray(errors)).toBe(false);
    expect((errors as Record<string, string[]>)['portalName']).toEqual(['Portal name already exists.']);
    expect(component.submitting()).toBe(false);
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
