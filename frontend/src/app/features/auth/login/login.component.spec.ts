// MIGRATION: Spec for the net-new LoginComponent (re-expressing Website/admin/Authentication/Login.ascx.vb).
// Verifies form-validation gating, the LoginRequest contract (portalId === 0), post-login redirect
// resolution (returnUrl vs default, with open-redirect guard), and RFC 7807 error surfacing.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  ActivatedRoute,
  Router,
  convertToParamMap,
  provideRouter,
  type ParamMap,
} from '@angular/router';
import { of, throwError } from 'rxjs';

import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import type { AuthResponse } from '../../../core/models';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authSpy: jasmine.SpyObj<AuthService>;
  let router: Router;
  let returnUrlParam: string | null = null;

  const authResponse: AuthResponse = {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    tokenType: 'Bearer',
    expiresIn: 3600,
    expiresAt: '2030-01-01T00:00:00.000Z',
    user: null,
  };

  const activatedRouteStub = {
    snapshot: {
      get queryParamMap(): ParamMap {
        return convertToParamMap(
          returnUrlParam !== null ? { returnUrl: returnUrlParam } : {},
        );
      },
    },
  };

  beforeEach(async () => {
    returnUrlParam = null;
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['login']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authSpy },
        { provide: ActivatedRoute, useValue: activatedRouteStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not call AuthService.login when the form is invalid', () => {
    component.onSubmit();

    expect(authSpy.login).not.toHaveBeenCalled();
    expect(component.form.touched).toBeTrue();
  });

  it('should call AuthService.login with a LoginRequest whose portalId is 0', () => {
    authSpy.login.and.returnValue(of(authResponse));
    component.form.setValue({ username: 'admin', password: 'secret', rememberMe: true });

    component.onSubmit();

    expect(authSpy.login).toHaveBeenCalledTimes(1);
    const request = authSpy.login.calls.mostRecent().args[0];
    expect(request.portalId).toBe(0);
    expect(request.username).toBe('admin');
    expect(request.password).toBe('secret');
    expect(request.rememberMe).toBeTrue();
  });

  it('should navigate to a safe returnUrl from the query string on success', () => {
    returnUrlParam = '/users';
    authSpy.login.and.returnValue(of(authResponse));
    component.form.setValue({ username: 'admin', password: 'secret', rememberMe: false });

    component.onSubmit();

    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/users');
    expect(component.submitting()).toBeFalse();
  });

  it('should navigate to /portals when no returnUrl is present', () => {
    authSpy.login.and.returnValue(of(authResponse));
    component.form.setValue({ username: 'admin', password: 'secret', rememberMe: false });

    component.onSubmit();

    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/portals');
  });

  it('should prefer the returnUrl component input when provided', () => {
    fixture.componentRef.setInput('returnUrl', '/dashboard');
    authSpy.login.and.returnValue(of(authResponse));
    component.form.setValue({ username: 'admin', password: 'secret', rememberMe: false });

    component.onSubmit();

    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/dashboard');
  });

  it('should reject an open-redirect returnUrl and fall back to /portals', () => {
    returnUrlParam = '//evil.example.com';
    authSpy.login.and.returnValue(of(authResponse));
    component.form.setValue({ username: 'admin', password: 'secret', rememberMe: false });

    component.onSubmit();

    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/portals');
  });

  it('should surface the ProblemDetails message and clear submitting on failure', () => {
    authSpy.login.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { title: 'Bad Request', detail: 'Invalid credentials' },
          }),
      ),
    );
    component.form.setValue({ username: 'admin', password: 'wrong', rememberMe: false });

    component.onSubmit();

    expect(component.errorMessage()).toContain('Invalid credentials');
    expect(component.submitting()).toBeFalse();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('should show a generic message when the failure carries no ProblemDetails text', () => {
    authSpy.login.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: null })),
    );
    component.form.setValue({ username: 'admin', password: 'wrong', rememberMe: false });

    component.onSubmit();

    expect(component.errorMessage()).toContain('Invalid login attempt');
    expect(component.submitting()).toBeFalse();
  });
});
