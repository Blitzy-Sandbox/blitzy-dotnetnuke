// MIGRATION: Gate-4 unit tests for ForgotPasswordComponent, the PUBLIC password-reset screen that
// re-expresses Website/admin/Security/SendPassword.ascx.vb. They assert the migrated behavior: the component
// delegates to the AUTH FEATURE service (AuthService.requestPasswordReset -- NOT the generic ApiService, AAP
// Section 0.7.3), validates the combined username/email field, sends portalId + optional verificationCode, and
// preserves the non-enumeration policy (success AND benign errors show the same confirmation; only
// transport/validation/rate-limit errors surface). The service POSTs to /api/v1/auth/forgot-password; these specs
// mock it so the success branch and every error branch (benign 404, validation 400, rate-limit 429) are covered.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { ForgotPasswordComponent } from './forgot-password.component';
import { AuthService } from '../../../core/auth/auth.service';
import type { PasswordResetRequest } from '../../../core/models/auth.model';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;
  let authSpy: jasmine.SpyObj<Pick<AuthService, 'requestPasswordReset'>>;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj<Pick<AuthService, 'requestPasswordReset'>>('AuthService', [
      'requestPasswordReset',
    ]);

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('does not call the service when the form is invalid (empty value) and marks it touched', () => {
    component.submit();

    expect(authSpy.requestPasswordReset).not.toHaveBeenCalled();
    expect(component.form.touched).toBeTrue();
    expect(component.submitted()).toBeFalse();
    expect(component.submitting()).toBeFalse();
  });

  it('does not call the service when the value looks like a malformed email', () => {
    component.form.controls.usernameOrEmail.setValue('not-an-@-email');

    component.submit();

    expect(authSpy.requestPasswordReset).not.toHaveBeenCalled();
    expect(component.submitted()).toBeFalse();
  });

  it('delegates to AuthService.requestPasswordReset with a plain username and shows the confirmation', () => {
    authSpy.requestPasswordReset.and.returnValue(of(void 0));
    component.form.controls.usernameOrEmail.setValue('  admin  ');

    component.submit();

    const expected: PasswordResetRequest = { usernameOrEmail: 'admin', portalId: 0 };
    expect(authSpy.requestPasswordReset).toHaveBeenCalledWith(expected);
    expect(component.submitted()).toBeTrue();
    expect(component.submitting()).toBeFalse();
    expect(component.errorMessage()).toBeNull();
  });

  it('includes verificationCode only when it is non-empty', () => {
    authSpy.requestPasswordReset.and.returnValue(of(void 0));
    component.form.controls.usernameOrEmail.setValue('user@example.com');
    component.form.controls.verificationCode.setValue('  ABC123  ');

    component.submit();

    const expected: PasswordResetRequest = {
      usernameOrEmail: 'user@example.com',
      portalId: 0,
      verificationCode: 'ABC123',
    };
    expect(authSpy.requestPasswordReset).toHaveBeenCalledWith(expected);
  });

  it('still shows the generic confirmation on a benign 404 (non-enumeration, no error surfaced)', () => {
    authSpy.requestPasswordReset.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    component.form.controls.usernameOrEmail.setValue('ghost');

    component.submit();

    expect(component.submitted()).toBeTrue();
    expect(component.errorMessage()).toBeNull();
    expect(component.submitting()).toBeFalse();
  });

  it('surfaces field/message errors from an RFC 7807 validation 400 without confirming', () => {
    authSpy.requestPasswordReset.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: {
              title: 'Validation failed',
              status: 400,
              errors: { usernameOrEmail: ['Username or email is required.'] },
            },
          }),
      ),
    );
    component.form.controls.usernameOrEmail.setValue('admin');

    component.submit();

    expect(component.submitted()).toBeFalse();
    expect(component.errorMessage()).toBe('Validation failed');
    expect(component.fieldErrors()['usernameOrEmail']).toEqual(['Username or email is required.']);
    expect(component.submitting()).toBeFalse();
  });

  it('surfaces a generic error on a 429 rate-limit response and clears submitting', () => {
    authSpy.requestPasswordReset.and.returnValue(throwError(() => new HttpErrorResponse({ status: 429 })));
    component.form.controls.usernameOrEmail.setValue('admin');

    component.submit();

    expect(component.submitted()).toBeFalse();
    expect(component.errorMessage()).not.toBeNull();
    expect(component.submitting()).toBeFalse();
  });
});
