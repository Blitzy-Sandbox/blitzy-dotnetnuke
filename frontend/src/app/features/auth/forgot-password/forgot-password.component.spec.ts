import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { ForgotPasswordComponent } from './forgot-password.component';
import { ApiService } from '../../../core/services/api.service';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj<ApiService>('ApiService', ['post']);

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ApiService, useValue: apiSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('does not POST when the form is invalid (empty value) and marks it touched', () => {
    component.submit();

    expect(apiSpy.post).not.toHaveBeenCalled();
    expect(component.form.touched).toBeTrue();
    expect(component.submitted()).toBeFalse();
    expect(component.submitting()).toBeFalse();
  });

  it('does not POST when the value looks like a malformed email', () => {
    component.form.controls.usernameOrEmail.setValue('not-an-@-email');

    component.submit();

    expect(apiSpy.post).not.toHaveBeenCalled();
    expect(component.submitted()).toBeFalse();
  });

  it('POSTs the relative path with a plain username and shows the generic confirmation on success', () => {
    apiSpy.post.and.returnValue(of(undefined));
    component.form.controls.usernameOrEmail.setValue('  admin  ');

    component.submit();

    expect(apiSpy.post).toHaveBeenCalledWith('auth/forgot-password', {
      usernameOrEmail: 'admin',
      portalId: 0,
    });
    expect(component.submitted()).toBeTrue();
    expect(component.submitting()).toBeFalse();
    expect(component.errorMessage()).toBeNull();
  });

  it('includes verificationCode only when it is non-empty', () => {
    apiSpy.post.and.returnValue(of(undefined));
    component.form.controls.usernameOrEmail.setValue('user@example.com');
    component.form.controls.verificationCode.setValue('  ABC123  ');

    component.submit();

    expect(apiSpy.post).toHaveBeenCalledWith('auth/forgot-password', {
      usernameOrEmail: 'user@example.com',
      portalId: 0,
      verificationCode: 'ABC123',
    });
  });

  it('still shows the generic confirmation on a benign 404 (non-enumeration, no error surfaced)', () => {
    apiSpy.post.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 404 })),
    );
    component.form.controls.usernameOrEmail.setValue('ghost');

    component.submit();

    expect(component.submitted()).toBeTrue();
    expect(component.errorMessage()).toBeNull();
    expect(component.submitting()).toBeFalse();
  });

  it('surfaces field/message errors from an RFC 7807 validation 400 without confirming', () => {
    apiSpy.post.and.returnValue(
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
    expect(component.fieldErrors()['usernameOrEmail']).toEqual([
      'Username or email is required.',
    ]);
    expect(component.submitting()).toBeFalse();
  });

  it('surfaces a generic error on a 429 rate-limit response and clears submitting', () => {
    apiSpy.post.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 429 })),
    );
    component.form.controls.usernameOrEmail.setValue('admin');

    component.submit();

    expect(component.submitted()).toBeFalse();
    expect(component.errorMessage()).not.toBeNull();
    expect(component.submitting()).toBeFalse();
  });
});
