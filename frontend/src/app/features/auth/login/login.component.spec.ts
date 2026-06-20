import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';

import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthResponse, LoginRequest } from '../../../core/models/auth.model';
import { User } from '../../../core/models/user.model';
import type { ProblemDetails } from '../../../core/services/api.service';

/**
 * Unit tests for LoginComponent (Gate 4: ng test --watch=false --browsers=ChromeHeadless).
 *
 * MIGRATION: LoginComponent is the single sanctioned behavior change of the DNN migration —
 * JWT Bearer + BCrypt replaces legacy Forms Authentication + DES (PortalSecurity.vb). These
 * tests pin the contract the sibling auth.routes.ts and AuthService depend on: the component
 * delegates auth to AuthService (no token storage here), redirects on success, and surfaces
 * RFC 7807 ProblemDetails errors.
 */
describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;

  let loginSpy: jasmine.Spy<(credentials: LoginRequest) => Observable<AuthResponse>>;
  let navigateByUrlSpy: jasmine.Spy<Router['navigateByUrl']>;
  let isAuthenticated: WritableSignal<boolean>;
  let activatedRouteMock: { snapshot: { queryParamMap: ParamMap } };

  const user: User = {
    userID: 1,
    username: 'admin',
    displayName: 'Administrator',
    firstName: 'Admin',
    lastName: 'User',
    email: 'admin@example.com',
    portalID: 0,
    isSuperUser: true,
    roles: ['Administrators'],
  };

  const authResponse: AuthResponse = {
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    user,
    expiresIn: 3600,
  };

  /** Create the component + run first change detection. Call AFTER any pre-construction setup. */
  function createComponent(): void {
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(async () => {
    loginSpy = jasmine.createSpy<(credentials: LoginRequest) => Observable<AuthResponse>>('login');
    navigateByUrlSpy = jasmine
      .createSpy<Router['navigateByUrl']>('navigateByUrl')
      .and.returnValue(Promise.resolve(true));
    isAuthenticated = signal(false);
    activatedRouteMock = { snapshot: { queryParamMap: convertToParamMap({}) } };

    const authServiceMock: Pick<AuthService, 'login' | 'isAuthenticated'> = {
      login: loginSpy,
      isAuthenticated,
    };

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authServiceMock },
        { provide: Router, useValue: { navigateByUrl: navigateByUrlSpy } },
        { provide: ActivatedRoute, useValue: activatedRouteMock },
      ],
    }).compileComponents();
  });

  it('should create and start with an invalid, empty, required form', () => {
    createComponent();

    expect(component).toBeTruthy();
    expect(component.form.controls.username.value).toBe('');
    expect(component.form.controls.password.value).toBe('');
    expect(component.form.invalid).toBeTrue();
    expect(component.submitting()).toBeFalse();
    expect(component.errorMessage()).toBeNull();
    expect(component.serverErrors()).toBeNull();
  });

  it('does not call AuthService.login when the form is invalid (validation gating)', () => {
    createComponent();

    component.onSubmit();

    expect(loginSpy).not.toHaveBeenCalled();
    expect(component.form.touched).toBeTrue();
    expect(component.submitting()).toBeFalse();
  });

  it('submits trimmed credentials and redirects to /portals on success', () => {
    createComponent();
    loginSpy.and.returnValue(of(authResponse));

    component.form.setValue({ username: '  admin  ', password: 'secret' });
    component.onSubmit();

    expect(loginSpy).toHaveBeenCalledOnceWith({ username: 'admin', password: 'secret' });
    expect(navigateByUrlSpy).toHaveBeenCalledOnceWith('/portals');
    expect(component.submitting()).toBeFalse();
    expect(component.errorMessage()).toBeNull();
  });

  it('redirects to the returnUrl query param when present', () => {
    createComponent();
    activatedRouteMock.snapshot.queryParamMap = convertToParamMap({ returnUrl: '/users' });
    loginSpy.and.returnValue(of(authResponse));

    component.form.setValue({ username: 'admin', password: 'secret' });
    component.onSubmit();

    expect(navigateByUrlSpy).toHaveBeenCalledOnceWith('/users');
  });

  it('surfaces ProblemDetails detail and field errors on a failed login', () => {
    createComponent();
    const problem: ProblemDetails = {
      title: 'Unauthorized',
      status: 401,
      detail: 'The username or password is incorrect.',
      errors: { username: ['Account is locked.'] },
    };
    loginSpy.and.returnValue(throwError(() => problem));

    component.form.setValue({ username: 'admin', password: 'wrong' });
    component.onSubmit();

    expect(component.errorMessage()).toBe('The username or password is incorrect.');
    expect(component.serverErrors()).toEqual({ username: ['Account is locked.'] });
    expect(component.submitting()).toBeFalse();
    expect(navigateByUrlSpy).not.toHaveBeenCalled();
  });

  it('falls back to a generic message when ProblemDetails has no detail or title', () => {
    createComponent();
    loginSpy.and.returnValue(throwError(() => ({}) as ProblemDetails));

    component.form.setValue({ username: 'admin', password: 'wrong' });
    component.onSubmit();

    expect(component.errorMessage()).toBe('Invalid username or password.');
    expect(component.serverErrors()).toBeNull();
  });

  it('redirects away from the login page when already authenticated', () => {
    isAuthenticated.set(true);

    createComponent();

    expect(navigateByUrlSpy).toHaveBeenCalledOnceWith('/portals');
  });
});
