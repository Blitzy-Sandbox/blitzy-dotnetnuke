// MIGRATION: unit tests for UserFormComponent (Gate 4). Verifies the validation + orchestration
// rules extracted from Website/admin/Users/User.ascx.vb. UserService is fully mocked (no HTTP);
// AuthService is stubbed with a writable currentUser signal.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { signal, type WritableSignal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { UserFormComponent } from './user-form.component';
import { UserService } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { CurrentUser, ProblemDetails, User } from '../../../core/models';

function buildUser(overrides: Partial<User> = {}): User {
  return {
    userId: 5,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    affiliateId: null,
    portalId: 0,
    isApproved: true,
    createdDate: null,
    lastLoginDate: null,
    lastActivityDate: null,
    lastLockoutDate: null,
    lockedOut: false,
    userRoles: [],
    ...overrides,
  };
}

function buildCurrentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    userId: 1,
    username: 'admin',
    email: 'admin@example.com',
    displayName: 'Administrator',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: true,
    portalId: 0,
    roles: ['Administrators'],
    ...overrides,
  };
}

describe('UserFormComponent', () => {
  let fixture: ComponentFixture<UserFormComponent>;
  let component: UserFormComponent;
  let userService: jasmine.SpyObj<UserService>;
  let currentUser: WritableSignal<CurrentUser | null>;
  let router: Router;

  beforeEach(() => {
    userService = jasmine.createSpyObj<UserService>('UserService', [
      'getById',
      'create',
      'update',
      'delete',
    ]);
    userService.getById.and.returnValue(of(buildUser()));
    userService.create.and.returnValue(of(buildUser()));
    userService.update.and.returnValue(of(buildUser()));
    userService.delete.and.returnValue(of(void 0));

    currentUser = signal<CurrentUser | null>(null);

    TestBed.configureTestingModule({
      imports: [UserFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: { currentUser } },
      ],
    });

    fixture = TestBed.createComponent(UserFormComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  it('creates the component', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('starts in create mode with an empty form', () => {
    fixture.detectChanges();
    expect(component.mode()).toBe('create');
    expect(component.form.controls.username.value).toBe('');
    expect(component.form.controls.email.value).toBe('');
    expect(userService.getById).not.toHaveBeenCalled();
  });

  it('rejects an invalid email and accepts a valid one', () => {
    fixture.detectChanges();
    const email = component.form.controls.email;
    email.setValue('not-an-email');
    expect(email.valid).toBeFalse();
    email.setValue('user@example.com');
    expect(email.valid).toBeTrue();
  });

  it('flags mismatched passwords at the group level and clears when they match', () => {
    fixture.detectChanges();
    component.form.controls.password.setValue('Secret1!');
    component.form.controls.confirmPassword.setValue('Different1!');
    expect(component.form.hasError('passwordMismatch')).toBeTrue();
    component.form.controls.confirmPassword.setValue('Secret1!');
    expect(component.form.hasError('passwordMismatch')).toBeFalse();
  });

  it('disables password and confirm when a random password is requested', () => {
    fixture.detectChanges();
    component.form.controls.randomPassword.setValue(true);
    expect(component.randomSelected()).toBeTrue();
    expect(component.form.controls.password.disabled).toBeTrue();
    expect(component.form.controls.confirmPassword.disabled).toBeTrue();
  });

  it('submits a create request carrying credentials', () => {
    fixture.detectChanges();
    component.form.setValue({
      username: 'newuser',
      email: 'new@example.com',
      displayName: 'New User',
      firstName: '',
      lastName: '',
      authorize: true,
      notify: true,
      password: 'Secret1!',
      confirmPassword: 'Secret1!',
      randomPassword: false,
      passwordQuestion: '',
      passwordAnswer: '',
      verificationCode: '',
    });

    component.submit();

    expect(userService.create).toHaveBeenCalledTimes(1);
    const dto = userService.create.calls.mostRecent().args[0];
    expect(dto.username).toBe('newuser');
    expect(dto.email).toBe('new@example.com');
    expect(dto.password).toBe('Secret1!');
    expect(dto.confirmPassword).toBe('Secret1!');
    expect(dto.authorize).toBeTrue();
    expect(router.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('omits the password from the create request when random is selected', () => {
    fixture.detectChanges();
    component.form.controls.username.setValue('newuser');
    component.form.controls.email.setValue('new@example.com');
    component.form.controls.displayName.setValue('New User');
    component.form.controls.randomPassword.setValue(true);

    component.submit();

    expect(userService.create).toHaveBeenCalledTimes(1);
    const dto = userService.create.calls.mostRecent().args[0];
    expect(dto.randomPassword).toBeTrue();
    expect(dto.password).toBeUndefined();
    expect(dto.confirmPassword).toBeUndefined();
  });

  it('does not submit when the form is invalid', () => {
    fixture.detectChanges();
    component.form.controls.email.setValue('invalid');
    component.submit();
    expect(userService.create).not.toHaveBeenCalled();
  });

  it('loads the user in edit mode and patches only non-credential fields', () => {
    userService.getById.and.returnValue(of(buildUser({ userId: 5 })));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(userService.getById).toHaveBeenCalledWith(5);
    expect(component.mode()).toBe('edit');
    expect(component.form.controls.username.value).toBe('jdoe');
    expect(component.form.controls.email.value).toBe('jdoe@example.com');
    expect(component.form.controls.username.disabled).toBeTrue();
    expect(component.form.controls.password.value).toBe('');
    expect(component.form.controls.confirmPassword.value).toBe('');
  });

  it('submits an update request in edit mode', () => {
    userService.getById.and.returnValue(of(buildUser({ userId: 5 })));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    component.form.controls.email.setValue('updated@example.com');
    component.submit();

    expect(userService.update).toHaveBeenCalledTimes(1);
    const [id, dto] = userService.update.calls.mostRecent().args;
    expect(id).toBe(5);
    expect(dto.email).toBe('updated@example.com');
    expect(router.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('maps an RFC 7807 error response to the serverErrors signal', () => {
    const problem: ProblemDetails = {
      type: 'about:blank',
      title: 'Validation Failed',
      status: 400,
      errors: { email: ['Email address is already in use.'] },
    };
    userService.create.and.returnValue(
      throwError(() => new HttpErrorResponse({ error: problem, status: 400, statusText: 'Bad Request' })),
    );
    fixture.detectChanges();
    component.form.setValue({
      username: 'newuser',
      email: 'new@example.com',
      displayName: 'New User',
      firstName: '',
      lastName: '',
      authorize: false,
      notify: false,
      password: 'Secret1!',
      confirmPassword: 'Secret1!',
      randomPassword: false,
      passwordQuestion: '',
      passwordAnswer: '',
      verificationCode: '',
    });

    component.submit();

    expect(component.serverErrors()).toEqual({ email: ['Email address is already in use.'] });
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('hides the delete affordance for a protected self superuser', () => {
    userService.getById.and.returnValue(of(buildUser({ userId: 5, isSuperUser: true })));
    currentUser.set(buildCurrentUser({ userId: 5, isSuperUser: true }));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(component.canDelete()).toBeFalse();
  });

  it('allows delete for a regular user and calls the service', () => {
    userService.getById.and.returnValue(of(buildUser({ userId: 5, isSuperUser: false })));
    currentUser.set(buildCurrentUser({ userId: 1 }));
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();

    expect(component.canDelete()).toBeTrue();
    expect(component.deleteLabel()).toBe('Delete');

    component.requestDelete();
    expect(component.showDeleteConfirm()).toBeTrue();

    component.delete();
    expect(userService.delete).toHaveBeenCalledWith(5);
    expect(router.navigate).toHaveBeenCalledWith(['/users']);
  });
});
