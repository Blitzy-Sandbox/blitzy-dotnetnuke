import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { AuthResponse } from '../../../core/models/auth.model';
import { User } from '../../../core/models/user.model';
import type { ProblemDetails } from '../../../core/services/api.service';
import { LoginComponent } from './login.component';

/**
 * Karma/Jasmine unit tests for {@link LoginComponent} (`features/auth/login`).
 *
 * Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless` -> exit 0,
 * 100% pass). The suite exercises the brand-new JWT login workflow that REPLACES
 * the legacy ASP.NET Forms Authentication + DES model in
 * `Library/Components/Security/PortalSecurity.vb` (`SignOut` L77-95, DES
 * `Encrypt`/`Decrypt` L138-211). JWT Bearer + server-side BCrypt is the single
 * sanctioned behavior change of the migration (AAP Section 0.6.2; root
 * `MIGRATION_NOTES.md` deviations D-001/D-002). The login UI itself is brand-new
 * (no legacy `.ascx` equivalent), so these tests cover NEW behavior rather than
 * verifying parity with a ported control.
 *
 * Testing strategy:
 *  - Standalone TestBed: `LoginComponent` is a standalone component, so it is
 *    registered via `imports` (NOT `declarations`). Its real, presentation-only
 *    shared dependencies (`FormControlsComponent`, `LoadingSpinnerComponent`)
 *    render transitively and inject nothing app-specific.
 *  - Collaborators are replaced with strictly typed Jasmine spies
 *    (`jasmine.SpyObj<AuthService>` / `jasmine.SpyObj<Router>`) plus a lightweight
 *    `ActivatedRoute` stub. The real `AuthService` is never instantiated (it would
 *    pull in `HttpClient`). Spying `AuthService.isAuthenticated` (a computed
 *    `Signal<boolean>`, i.e. a callable) as a method that returns a boolean is
 *    call-compatible with the component's `authService.isAuthenticated()`
 *    invocation - the same precedent used by `core/auth/auth.guard.spec.ts`.
 *  - `of(...)` / `throwError(...)` make `AuthService.login` emit synchronously, so
 *    `onSubmit()` completes within the same tick and assertions need no `fakeAsync`.
 *  - NO `any`: every spy and fixture is typed; the error fixture uses the type-only
 *    `ProblemDetails` import.
 *
 * NOTE (contract reconciliation): `mockUser` is built against the ACTUAL `User`
 * wire contract in `core/models/user.model.ts`, whose property names are the
 * `System.Text.Json` camelCase serialization of the backend `UserDto` - hence
 * `userID` / `portalID` / `affiliateID` (capitalized `ID`) and the required
 * `fullName` / `approved` / membership-timestamp fields. The nullable scalar
 * fields are modeled as `T | null`.
 */
describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let returnUrl: string | null;

  /**
   * Fully-populated, strictly typed `User` fixture matching the real wire shape.
   * Capitalized-`ID` keys and the required `fullName`/`approved`/timestamp fields
   * are mandated by `core/models/user.model.ts`; nullable fields are set to `null`.
   */
  const mockUser: User = {
    userID: 1,
    portalID: 0,
    affiliateID: null,
    username: 'admin',
    displayName: 'Administrator',
    email: 'admin@example.com',
    firstName: 'Ad',
    lastName: 'Min',
    fullName: 'Ad Min',
    isSuperUser: true,
    approved: true,
    roles: ['Administrators'],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
  };

  /** Successful login payload returned by the mocked `AuthService.login`. */
  const mockResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    user: mockUser,
  };

  /**
   * Create the component under test and run initial change detection. Called per
   * test (after any spy/`returnUrl` overrides) so the constructor observes the
   * desired `isAuthenticated()` / `returnUrl` state at creation time.
   */
  function createComponent(): void {
    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(async () => {
    returnUrl = null;
    authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'isAuthenticated']);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
    authServiceSpy.isAuthenticated.and.returnValue(false);
    authServiceSpy.login.and.returnValue(of(mockResponse));
    routerSpy.navigateByUrl.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          // The `returnUrl` getter is read LAZILY (inside `resolveReturnUrl()`), so a
          // test can set `returnUrl` BEFORE `createComponent()` and have the value
          // observed by both the constructor and `onSubmit()`. `convertToParamMap`
          // builds a real `ParamMap` so `.get('returnUrl')` behaves like production.
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get queryParamMap() {
                return convertToParamMap(returnUrl ? { returnUrl } : {});
              },
            },
          },
        },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    createComponent();
    expect(component).toBeTruthy();
  });

  it('starts with an invalid, empty form', () => {
    createComponent();
    expect(component.form.invalid).toBe(true);
    expect(component.form.controls.username.value).toBe('');
    expect(component.form.controls.password.value).toBe('');
  });

  it('does not call login when the form is invalid', () => {
    createComponent();
    component.onSubmit();
    expect(authServiceSpy.login).not.toHaveBeenCalled();
    expect(component.form.controls.username.touched).toBe(true);
  });

  it('logs in and redirects to /portals on success', () => {
    createComponent();
    component.form.setValue({ username: 'admin', password: 'secret' });

    component.onSubmit();

    expect(authServiceSpy.login).toHaveBeenCalledWith({ username: 'admin', password: 'secret' });
    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/portals');
    expect(component.submitting()).toBe(false);
    expect(component.errorMessage()).toBeNull();
  });

  it('redirects to the returnUrl query param when present', () => {
    returnUrl = '/users';
    createComponent();
    component.form.setValue({ username: 'admin', password: 'secret' });

    component.onSubmit();

    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/users');
  });

  it('redirects away when already authenticated', () => {
    authServiceSpy.isAuthenticated.and.returnValue(true);

    createComponent();

    expect(routerSpy.navigateByUrl).toHaveBeenCalledWith('/portals');
  });

  it('surfaces the ProblemDetails error and keeps the form populated', () => {
    const problem: ProblemDetails = {
      title: 'Unauthorized',
      detail: 'Invalid username or password.',
      status: 401,
      errors: { username: ['User not found.'] },
    };
    authServiceSpy.login.and.returnValue(throwError(() => problem));
    createComponent();
    component.form.setValue({ username: 'admin', password: 'wrong' });

    component.onSubmit();

    expect(component.errorMessage()).toBe('Invalid username or password.');
    expect(component.serverErrors()).toEqual({ username: ['User not found.'] });
    expect(component.submitting()).toBe(false);
    expect(component.form.controls.username.value).toBe('admin');
    expect(routerSpy.navigateByUrl).not.toHaveBeenCalled();
  });

  it('renders the error alert in the DOM after a failed login', () => {
    const problem: ProblemDetails = { detail: 'Invalid username or password.' };
    authServiceSpy.login.and.returnValue(throwError(() => problem));
    createComponent();
    component.form.setValue({ username: 'admin', password: 'wrong' });

    component.onSubmit();
    fixture.detectChanges();

    const alert = fixture.debugElement.query(By.css('.login__error'));
    expect(alert).not.toBeNull();
    expect((alert.nativeElement as HTMLElement).textContent?.trim()).toBe(
      'Invalid username or password.',
    );
  });
});
