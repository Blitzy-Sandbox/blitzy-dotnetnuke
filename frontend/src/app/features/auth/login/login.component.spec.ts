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
 * Karma/Jasmine unit tests for {@link LoginComponent} (Gate 4:
 * `ng test --watch=false --browsers=ChromeHeadless` -> exit 0, 100% pass).
 *
 * MIGRATION: LoginComponent is the brand-new entry point to the JWT Bearer
 * authentication flow that REPLACES the legacy ASP.NET Forms Authentication +
 * DES model in `Library/Components/Security/PortalSecurity.vb`
 * (FormsAuthentication.SignOut L79; DES Encrypt/Decrypt L138-211). JWT + BCrypt
 * is the single sanctioned behavior change of the migration (AAP §0.6.2). There
 * was no legacy `.ascx` login control, so this contract is authored fresh.
 *
 * Testing strategy (mandated):
 *  - Standalone TestBed: the component is standalone, so it is added to `imports`
 *    (NOT `declarations`). Its real shared-component dependencies
 *    (`FormControlsComponent`, `LoadingSpinnerComponent`) are presentation-only
 *    and render transitively with no extra providers.
 *  - Mock collaborators only: `jasmine.SpyObj` doubles for `AuthService` and
 *    `Router`, plus a lightweight `ActivatedRoute` stub. The real `AuthService`
 *    is never instantiated (it would pull in `HttpClient`).
 *  - `AuthService.isAuthenticated` is a `computed` SIGNAL (a callable). A spy
 *    method returning a boolean is call-compatible with the component's
 *    `authService.isAuthenticated()` invocation and is fully type-compatible
 *    (precedent: `core/auth/auth.guard.spec.ts`).
 *  - NO `any`: typed `jasmine.SpyObj<...>`, typed `User`/`AuthResponse`
 *    fixtures, and a type-only `ProblemDetails` import for the error fixture.
 *
 * NOTE: `User` uses the .NET 8 System.Text.Json camelCase-of-PascalCase wire
 * names (`userID` / `portalID` / `affiliateID`, capital `ID`), matching the real
 * `core/models/user.model.ts` contract.
 */
describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let returnUrl: string | null;

  const mockUser: User = {
    userID: 1,
    username: 'admin',
    displayName: 'Administrator',
    firstName: 'Ad',
    lastName: 'Min',
    email: 'admin@example.com',
    portalID: 0,
    isSuperUser: true,
    roles: ['Administrators'],
  };

  const mockResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    user: mockUser,
  };

  /**
   * Instantiate the component and run first change detection. Call AFTER any
   * pre-construction setup (e.g. setting `returnUrl` or the `isAuthenticated`
   * spy), because the constructor reads both eagerly.
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
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              // Getter so a test can set `returnUrl` BEFORE createComponent()
              // and have the value read lazily inside resolveReturnUrl().
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
