import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthResponse } from '../../../core/models';

/**
 * Unit tests for {@link LoginComponent} — the SPA sign-in screen (public route
 * `/auth/login`).
 *
 * These specs back Validation Gate 4 (`ng test --watch=false
 * --browsers=ChromeHeadless --code-coverage`, 100% pass) and prove the migrated
 * Angular 19 component preserves the behaviour of the legacy DotNetNuke Web Forms
 * login flow (UI functional parity, AAP §0.7.1). The lineage is the DNN login
 * control `Website/admin/Authentication/Login.ascx.vb` (whose
 * `UserController.ValidateUser(PortalId, txtUsername, txtPassword, …)` call this
 * form replaces) and the Web Forms entry shell `Website/Default.aspx.vb`. The
 * legacy ViewState / postback / `IClientAPICallbackEventHandler` mechanics are
 * ELIMINATED (AAP §0.6.3): a stateless, typed reactive form drives
 * `AuthService.login()`, `Page_Load` becomes `ngOnInit`, and the postback login
 * button becomes `(ngSubmit)`.
 *
 * Test design notes:
 * - `AuthService` is fully mocked with a single `login` spy — the component calls
 *   ONLY `AuthService.login()` (it never touches `HttpClient`, `loadCurrentUser`,
 *   `isAuthenticated`, etc.), so the spy is intentionally minimal to avoid
 *   over-specification. Because no real HTTP is performed, no Angular HTTP
 *   testing utilities are wired up and the SPA HTTP layer is never exercised.
 * - The `Router` is a REAL router provided via `provideRouter([])`; only
 *   `navigateByUrl` is spied (resolving to `true`) so success navigation can be
 *   asserted without exercising the real navigation cycle. The deprecated
 *   `RouterTestingModule` is intentionally avoided.
 * - `ActivatedRoute` is a lightweight stub whose `queryParamMap.get` returns the
 *   `returnUrl` under test; `ngOnInit` (fired by the first `detectChanges()`)
 *   reads it, defaulting to `/portals` when the param is absent (`null`).
 * - `LoginComponent` is standalone, so it is registered in TestBed `imports`;
 *   this transitively provides `ReactiveFormsModule`, `RouterLink`,
 *   `FormFieldComponent` and `LoadingSpinnerComponent`, so the rendered
 *   `<app-form-field [control]=…>` receives its required `control` input and the
 *   first `detectChanges()` does not throw NG0950.
 * - Determinism: `login()` returns a synchronous `of(...)` / `throwError(...)`
 *   and `navigateByUrl` is stubbed to resolve, so no `fakeAsync`/`tick` is needed
 *   and the specs are flake-free.
 */
describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let router: Router;

  /**
   * Typed token fixture returned by the mocked `AuthService.login()`. Mirrors the
   * backend `TokenResponseDto` exactly: `accessToken`, `refreshToken`, `expiresAt`
   * (ISO 8601 UTC) and `tokenType` are all non-null strings on the `AuthResponse`
   * contract, so every field is supplied.
   */
  const authResponse: AuthResponse = {
    accessToken: 'test-access-token',
    refreshToken: 'test-refresh-token',
    expiresAt: '2099-01-01T00:00:00.000Z',
    tokenType: 'Bearer',
  };

  /**
   * Configures the TestBed and creates the component under test.
   *
   * @param returnUrl the value the `ActivatedRoute` stub returns for the
   *   `returnUrl` query param. `null` (the default) simulates a MISSING
   *   `?returnUrl=…` param, exercising the component's `/portals` fallback.
   */
  function setup(returnUrl: string | null = null): void {
    authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['login']);
    authServiceSpy.login.and.returnValue(of(authResponse));

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: { get: (_key: string): string | null => returnUrl },
            },
          },
        },
      ],
    });

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    fixture.detectChanges(); // triggers ngOnInit -> captures returnUrl
  }

  it('creates the component', () => {
    setup();
    expect(component).toBeTruthy();
  });

  it('is invalid when username and password are empty (required validators)', () => {
    // MIGRATION: legacy RequiredFieldValidator on txtUsername/txtPassword ->
    // Validators.required (AAP §0.7.1 form-validation parity).
    setup();
    expect(component.form.invalid).toBe(true);
    expect(component.form.controls.username.hasError('required')).toBe(true);
    expect(component.form.controls.password.hasError('required')).toBe(true);
  });

  it('does not call AuthService.login and marks controls touched when submitted while invalid', () => {
    setup();

    component.onSubmit();

    expect(authServiceSpy.login).not.toHaveBeenCalled();
    expect(component.form.controls.username.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
  });

  it('becomes valid once username and password are provided', () => {
    setup();

    component.form.setValue({ username: 'jdoe', password: 'secret', portalId: null });

    expect(component.form.valid).toBe(true);
  });

  it('logs in with the credentials and navigates to the default returnUrl (/portals) on success', () => {
    setup(); // no returnUrl -> default /portals
    component.form.setValue({ username: 'jdoe', password: 'secret', portalId: null });

    component.onSubmit();

    // portalId is null, so the component omits it from the LoginRequest.
    expect(authServiceSpy.login).toHaveBeenCalledWith({ username: 'jdoe', password: 'secret' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/portals');
    expect(component.errorMessage()).toBeNull();
  });

  it('navigates to the provided returnUrl on success', () => {
    // MIGRATION: DNN postback redirect (returnurl query) -> SPA route navigation;
    // authGuard attaches ?returnUrl=<attempted-url> on a 401.
    setup('/users/42');
    component.form.setValue({ username: 'jdoe', password: 'secret', portalId: null });

    component.onSubmit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/users/42');
  });

  it('sets errorMessage and clears submitting when login fails', () => {
    setup();
    // Override the default success stub BEFORE submitting so the error branch runs.
    authServiceSpy.login.and.returnValue(throwError(() => new Error('401 Unauthorized')));
    component.form.setValue({ username: 'jdoe', password: 'wrong-password', portalId: null });

    component.onSubmit();

    expect(component.errorMessage()).not.toBeNull();
    expect(component.submitting()).toBe(false);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });
});
