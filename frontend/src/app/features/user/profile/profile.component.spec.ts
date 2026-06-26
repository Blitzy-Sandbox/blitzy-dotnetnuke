// MIGRATION: Spec for ProfileComponent (the Angular 19 replacement for DNN Profile.ascx.vb). Verifies the
// dynamic typed form built from ProfilePropertyValue definitions ordered by ViewOrder, per-property validators
// (required/pattern/maxLength), the admin "sees all + validation bypass" rules (Profile.ascx.vb IsValid L94-104
// / DataBind L164-168), non-admin visible-only rendering, the self-edit per-field visibility selector
// (ShowVisibility L58-63), submit -> UserService.updateProfile (cmdUpdate_Click L220-232), and RFC 7807
// ProblemDetails mapping. Gate 4: ng test --watch=false --browsers=ChromeHeadless --code-coverage (100% pass).
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { ProfileComponent } from './profile.component';
import { UserService, type ProfilePropertyValue } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';

interface StubUser {
  userId: number;
  isSuperUser: boolean;
  roles: string[];
}

function makeDef(overrides: Partial<ProfilePropertyValue> = {}): ProfilePropertyValue {
  const base: ProfilePropertyValue = {
    propertyDefinitionId: 1,
    propertyName: 'firstName',
    propertyCategory: 'Name',
    propertyValue: '',
    required: false,
    visible: true,
    viewOrder: 0,
    validationExpression: null,
    dataType: 0,
    length: 0,
    visibility: 2,
  };
  return { ...base, ...overrides };
}

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let getProfileSpy: jasmine.Spy;
  let updateProfileSpy: jasmine.Spy;
  let currentUser: WritableSignal<StubUser | null>;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getProfileSpy = jasmine.createSpy('getProfile').and.returnValue(of([]));
    updateProfileSpy = jasmine.createSpy('updateProfile').and.returnValue(of([]));
    currentUser = signal<StubUser | null>(null);

    const userServiceStub = {
      getProfile: getProfileSpy,
      updateProfile: updateProfileSpy,
      loading: signal(false).asReadonly(),
    };
    const authStub = { currentUser };

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: UserService, useValue: userServiceStub },
        { provide: AuthService, useValue: authStub },
      ],
    });

    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  function initComponent(
    defs: ProfilePropertyValue[],
    user: StubUser | null,
    idValue = '5',
  ): void {
    getProfileSpy.and.returnValue(of(defs));
    currentUser.set(user);
    fixture.componentRef.setInput('id', idValue);
    fixture.detectChanges();
  }

  it('creates the component', () => {
    initComponent([makeDef()], { userId: 5, isSuperUser: false, roles: [] });
    expect(component).toBeTruthy();
  });

  it('loads the profile and builds a form ordered by viewOrder', () => {
    const defs = [
      makeDef({ propertyDefinitionId: 2, propertyName: 'lastName', viewOrder: 2 }),
      makeDef({ propertyDefinitionId: 1, propertyName: 'firstName', viewOrder: 1 }),
    ];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    expect(getProfileSpy).toHaveBeenCalledWith(5);
    expect(component.orderedDefinitions().map((def) => def.propertyName)).toEqual([
      'firstName',
      'lastName',
    ]);
    expect(component.form.get('firstName')).not.toBeNull();
    expect(component.form.get('lastName')).not.toBeNull();
  });

  it('marks a required property invalid when empty for a non-admin', () => {
    const defs = [makeDef({ propertyName: 'firstName', required: true, visible: true, propertyValue: '' })];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    const valueControl = component.form.controls['firstName'].controls.value;
    expect(valueControl.hasError('required')).toBe(true);
    expect(component.form.invalid).toBe(true);
  });

  it('rejects values that do not match a property validationExpression', () => {
    const defs = [
      makeDef({ propertyName: 'age', validationExpression: '^[0-9]+$', visible: true, propertyValue: '' }),
    ];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    const valueControl = component.form.controls['age'].controls.value;
    valueControl.setValue('abc');
    expect(valueControl.hasError('pattern')).toBe(true);

    valueControl.setValue('42');
    expect(valueControl.valid).toBe(true);
  });

  it('renders all properties for an admin and treats the form valid despite blank required fields', () => {
    const defs = [
      makeDef({ propertyDefinitionId: 1, propertyName: 'secret', visible: false, required: true, propertyValue: '', viewOrder: 1 }),
      makeDef({ propertyDefinitionId: 2, propertyName: 'pub', visible: true, required: false, viewOrder: 2 }),
    ];
    initComponent(defs, { userId: 99, isSuperUser: true, roles: [] });

    expect(component.isAdmin()).toBe(true);
    expect(component.orderedDefinitions().map((def) => def.propertyName)).toEqual(['secret', 'pub']);
    expect(component.form.get('secret')).not.toBeNull();
    expect(component.form.valid).toBe(true);
  });

  it('renders only visible properties for a non-admin', () => {
    const defs = [
      makeDef({ propertyDefinitionId: 1, propertyName: 'secret', visible: false, viewOrder: 1 }),
      makeDef({ propertyDefinitionId: 2, propertyName: 'pub', visible: true, viewOrder: 2 }),
    ];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    expect(component.orderedDefinitions().map((def) => def.propertyName)).toEqual(['pub']);
    expect(component.form.get('secret')).toBeNull();
    expect(component.form.get('pub')).not.toBeNull();
  });

  it('shows the per-field visibility selector when self-editing as a non-admin', () => {
    initComponent([makeDef({ propertyName: 'firstName', visible: true })], {
      userId: 5,
      isSuperUser: false,
      roles: [],
    });
    fixture.detectChanges();

    expect(component.isSelf()).toBe(true);
    expect(component.showVisibilitySelector()).toBe(true);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.profile__visibility')).not.toBeNull();
  });

  it('hides the per-field visibility selector for an admin editing another user', () => {
    initComponent([makeDef({ propertyName: 'firstName', visible: true })], {
      userId: 99,
      isSuperUser: true,
      roles: [],
    });
    fixture.detectChanges();

    expect(component.showVisibilitySelector()).toBe(false);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.profile__visibility')).toBeNull();
  });

  it('submits the assembled profile properties via updateProfile', () => {
    const defs = [makeDef({ propertyDefinitionId: 7, propertyName: 'firstName', required: false, visible: true, propertyValue: '' })];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    component.form.controls['firstName'].controls.value.setValue('Jane');
    component.submit();

    expect(updateProfileSpy).toHaveBeenCalledTimes(1);
    const args = updateProfileSpy.calls.mostRecent().args;
    expect(args[0]).toBe(5);
    const properties = args[1] as ProfilePropertyValue[];
    expect(properties.length).toBe(1);
    expect(properties[0].propertyDefinitionId).toBe(7);
    expect(properties[0].propertyName).toBe('firstName');
    expect(properties[0].propertyValue).toBe('Jane');
    expect(component.saved()).toBe(true);
    expect(component.submitting()).toBe(false);
  });

  it('maps a server-side ProblemDetails error into the serverErrors signal', () => {
    const defs = [makeDef({ propertyName: 'firstName', required: false, visible: true })];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    const problem = {
      type: 'https://httpstatuses.io/400',
      title: 'Validation failed',
      status: 400,
      errors: { firstName: ['First name is invalid.'] },
    };
    updateProfileSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 400 })));

    component.submit();

    expect(component.serverErrors()?.status).toBe(400);
    const errors = component.serverErrors()?.errors;
    expect(Array.isArray(errors)).toBe(false);
    expect((errors as Record<string, string[]>)['firstName']).toEqual(['First name is invalid.']);
    expect(component.submitting()).toBe(false);
    expect(component.saved()).toBe(false);
  });

  it('exposes a flat error summary when ProblemDetails.errors is an array', () => {
    const defs = [makeDef({ propertyName: 'firstName', required: false, visible: true })];
    initComponent(defs, { userId: 5, isSuperUser: false, roles: [] });

    const problem = {
      type: 'about:blank',
      title: 'Bad Request',
      status: 400,
      errors: ['Profile update failed.'],
    };
    updateProfileSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 400 })));

    component.submit();

    expect(component.errorSummary()).toEqual(['Profile update failed.']);
  });

  it('navigates to the users list on cancel', () => {
    initComponent([makeDef()], { userId: 5, isSuperUser: false, roles: [] });

    component.cancel();

    expect(navigateSpy).toHaveBeenCalledWith(['/users']);
  });
});
