// MIGRATION: Gate-4 spec for RoleFormComponent (the Angular 19 replacement for DNN EditRoles.ascx.vb).
// Verifies create vs. edit mode selection via route input binding, edit pre-load + form patch (Page_Load
// L131-169), the fee/trial default-and-parse rules (cmdUpdate_Click L212-248), the system-role guard
// (L174-178), the Registered-Users Manage-Users rule (L180-182), and RFC 7807 ProblemDetails error
// mapping (incl. the DuplicateRole roleName error, L252-258). Runs non-interactively:
// ng test --watch=false --browsers=ChromeHeadless --code-coverage.
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { RoleFormComponent } from './role-form.component';
import { RoleService, type CreateRoleRequest } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { Role } from '../../../core/models';

interface StubUser {
  portalId: number;
}

function makeRole(overrides: Partial<Role> = {}): Role {
  const base: Role = {
    roleId: 5,
    portalId: 0,
    roleGroupId: null,
    roleName: 'Editors',
    description: 'Content editors',
    serviceFee: 0,
    billingFrequency: 'N',
    trialPeriod: 0,
    trialFrequency: 'N',
    billingPeriod: 1,
    trialFee: 0,
    isPublic: false,
    autoAssignment: false,
    rsvpCode: null,
    iconFile: null,
  };
  return { ...base, ...overrides };
}

describe('RoleFormComponent', () => {
  let fixture: ComponentFixture<RoleFormComponent>;
  let component: RoleFormComponent;
  let getByIdSpy: jasmine.Spy;
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let currentUser: WritableSignal<StubUser | null>;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeRole()));
    createSpy = jasmine.createSpy('create').and.returnValue(of(makeRole()));
    updateSpy = jasmine.createSpy('update').and.returnValue(of(makeRole()));
    currentUser = signal<StubUser | null>({ portalId: 2 });

    const roleServiceStub = { getById: getByIdSpy, create: createSpy, update: updateSpy };
    const authStub = { currentUser };

    TestBed.configureTestingModule({
      imports: [RoleFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RoleService, useValue: roleServiceStub },
        { provide: AuthService, useValue: authStub },
      ],
    });

    fixture = TestBed.createComponent(RoleFormComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('creates the component', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('starts with legacy defaults in create mode and submits via create()', () => {
    fixture.detectChanges();

    expect(component.isEditMode()).toBe(false);
    expect(getByIdSpy).not.toHaveBeenCalled();
    expect(component.form.controls.roleName.value).toBe('');
    expect(component.form.controls.billingFrequency.value).toBe('N');
    expect(component.form.controls.trialFrequency.value).toBe('N');
    expect(component.form.controls.roleGroupId.value).toBe(-1);

    component.form.controls.roleName.setValue('Editors');
    component.submit();

    expect(createSpy).toHaveBeenCalledTimes(1);
    const dto = createSpy.calls.mostRecent().args[0] as CreateRoleRequest;
    expect(dto.roleName).toBe('Editors');
    // MIGRATION: cmdUpdate_Click defaults (EditRoles.ascx.vb L212-230) when no fee/trial entered.
    expect(dto.serviceFee).toBe(0);
    expect(dto.billingPeriod).toBe(1);
    expect(dto.billingFrequency).toBe('N');
    expect(dto.trialFee).toBe(0);
    expect(dto.trialPeriod).toBe(1);
    expect(dto.trialFrequency).toBe('N');
    // portalId resolved from the current user's portal (multi-tenant scoping, AAP 0.7.1).
    expect(dto.portalId).toBe(2);
    expect(updateSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/roles']);
  });

  it('parses billing + trial values once a billing frequency is chosen (free trial fee preserved)', () => {
    fixture.detectChanges();

    component.form.controls.billingFrequency.setValue('M');
    expect(component.showTrialSection()).toBe(true);

    component.form.controls.roleName.setValue('Premium');
    component.form.controls.serviceFee.setValue(9.99);
    component.form.controls.billingPeriod.setValue(1);
    component.form.controls.trialFee.setValue(0);
    component.form.controls.trialPeriod.setValue(14);
    component.form.controls.trialFrequency.setValue('D');
    component.submit();

    const dto = createSpy.calls.mostRecent().args[0] as CreateRoleRequest;
    expect(dto.serviceFee).toBe(9.99);
    expect(dto.billingPeriod).toBe(1);
    expect(dto.billingFrequency).toBe('M');
    // MIGRATION: trial parses when sglServiceFee<>0 && cboTrialFrequency<>'N' (L226); a free trial
    // (trialFee 0) is preserved, matching the legacy txtTrialFee.Text<>'' check that permits "0".
    expect(dto.trialFee).toBe(0);
    expect(dto.trialPeriod).toBe(14);
    expect(dto.trialFrequency).toBe('D');
  });

  it('pre-loads and patches the form in edit mode, then submits via update()', () => {
    getByIdSpy.and.returnValue(
      of(makeRole({ roleId: 5, roleName: 'Existing', portalId: 0, description: 'Desc' })),
    );

    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(component.isEditMode()).toBe(true);
    // MIGRATION: the edit-mode load carries the required tenant portalId (AAP 0.7.1), sourced from the
    // authenticated principal ONLY on the load path (currentUser.portalId === 2 here).
    expect(getByIdSpy).toHaveBeenCalledWith(5, 2);
    expect(component.form.controls.roleName.value).toBe('Existing');
    expect(component.form.controls.description.value).toBe('Desc');

    component.submit();

    expect(updateSpy).toHaveBeenCalledTimes(1);
    const args = updateSpy.calls.mostRecent().args;
    expect(args[0]).toBe(5);
    // MIGRATION: PUT /roles/{id} carries the tenant portalId as the 2nd arg (AAP 0.7.1). In the button
    // handler the loaded role's portalId (0) wins over the principal's (2); the nullish-coalescing
    // correctly preserves the 0 instead of falling through to currentUser.
    expect(args[1]).toBe(0);
    // MIGRATION: roleName preserved from the loaded role (avoids the legacy empty-name bug, L237).
    expect((args[2] as CreateRoleRequest).roleName).toBe('Existing');
    expect(createSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/roles']);
  });

  it('disables every editable control and hides Update when editing a system role', () => {
    getByIdSpy.and.returnValue(of(makeRole({ roleId: 1, roleName: 'Administrators' })));

    fixture.componentRef.setInput('id', '1');
    fixture.detectChanges();
    fixture.detectChanges();

    expect(component.isSystemRole()).toBe(true);
    expect(component.form.controls.description.disabled).toBe(true);
    expect(component.form.controls.serviceFee.disabled).toBe(true);
    expect(component.form.controls.roleGroupId.disabled).toBe(true);

    const host = fixture.nativeElement as HTMLElement;
    // MIGRATION: cmdUpdate.Visible=False for system roles (EditRoles.ascx.vb L176).
    expect(host.querySelector('button[type="submit"]')).toBeNull();
  });

  it('hides the Manage Users action for the Registered Users role', () => {
    getByIdSpy.and.returnValue(of(makeRole({ roleId: 2, roleName: 'Registered Users' })));

    fixture.componentRef.setInput('id', '2');
    fixture.detectChanges();

    expect(component.isSystemRole()).toBe(true);
    // MIGRATION: cmdManage hidden for Registered Users (EditRoles.ascx.vb L180-182).
    expect(component.canManageUsers()).toBe(false);
  });

  it('maps a server-side ProblemDetails (DuplicateRole) into the problem signal', () => {
    const problem = {
      type: 'https://httpstatuses.io/400',
      title: 'Validation failed',
      status: 400,
      errors: { roleName: ['A role with this name already exists.'] },
    };
    createSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ error: problem, status: 400 })),
    );

    fixture.detectChanges();
    component.form.controls.roleName.setValue('Administrators');
    component.submit();

    expect(component.problem()?.status).toBe(400);
    const errors = component.problem()?.errors;
    expect(Array.isArray(errors)).toBe(false);
    expect((errors as Record<string, string[]>)['roleName']).toEqual([
      'A role with this name already exists.',
    ]);
    expect(component.submitting()).toBe(false);
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  // MIGRATION: (QA Issue 1, secondary impact) a 500 carries an empty `errors` object (not a flat array), so
  // the form-level summary must fall back to the RFC 7807 title + detail. Previously errorSummary returned []
  // for an object-shaped `errors` and the server error rendered nowhere.
  it('surfaces the RFC 7807 title and detail in the error summary on a 500', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'An unexpected error occurred.',
      status: 500,
      detail: 'Boom.',
      errors: {},
    };
    createSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 500 })));

    fixture.detectChanges();
    component.form.controls.roleName.setValue('Editors');
    component.submit();

    expect(component.problem()?.status).toBe(500);
    expect(component.errorSummary()).toEqual(['An unexpected error occurred.', 'Boom.']);

    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const alert = host.querySelector('.role-form__errors');
    expect(alert?.textContent).toContain('An unexpected error occurred.');
    expect(alert?.textContent).toContain('Boom.');
  });
});
