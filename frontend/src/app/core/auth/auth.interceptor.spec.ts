import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { environment } from '../../../environments/environment';
import { AuthResponse } from '../models/auth.model';
import { User } from '../models/user.model';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

/**
 * Unit tests for the functional {@link authInterceptor} (`core/auth/auth.interceptor.ts`).
 * Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless` -> 100% pass).
 *
 * Testing strategy:
 *  - Wire the interceptor through the REAL HTTP stack. Provider ORDER is load-bearing:
 *    `provideHttpClient(withInterceptors([authInterceptor]))` FIRST so the interceptor
 *    chain is installed, then `provideHttpClientTesting()`, which swaps the real network
 *    backend for the test backend WITHOUT removing the interceptor. This exercises the
 *    genuine `authInterceptor` -> real `AuthService` -> real `ApiService` collaboration
 *    end-to-end (not a hand-rolled stub of any of them).
 *  - Use the REAL `AuthService` (never mocked): seed auth state by writing its PUBLIC
 *    writable `accessToken` signal (`accessToken.set(...)`). The 401 -> refresh recovery is
 *    gated on `isAuthenticated()` (an access token being present); the refresh token itself
 *    is an HttpOnly cookie the browser would send, so there is no JS-readable refresh signal
 *    to seed.
 *  - Only `Router` is replaced (a Jasmine spy) so the redirect inside
 *    `AuthService.clearSession()` never performs real navigation. `Router` is provided
 *    because `AuthService` injects it in its constructor.
 *
 * Two envelope rules govern the `req.flush(...)` bodies below:
 *  - The DRIVING requests use the RAW `HttpClient` (`http.get` / `http.post`), so their
 *    flushed bodies are delivered to the subscriber VERBATIM (e.g. `flush({ ok: true })`
 *    => `body === { ok: true }`). They are NOT unwrapped.
 *  - The interceptor's `401` recovery calls `AuthService.refresh()`, which routes through
 *    `ApiService.post(...)` and UNWRAPS the backend `{ data, meta }` success envelope.
 *    Hence the refresh response MUST be flushed as `{ data: <AuthResponse> }`.
 *
 * MIGRATION (SANCTIONED): the interceptor is the client half of the JWT Bearer model
 * that replaces the legacy DotNetNuke Forms-Authentication cookie scheme
 * (`Library/Components/Security/PortalSecurity.vb` `SignOut()` L77-L95, which called
 * `FormsAuthentication.SignOut()` and expired portal cookies). See MIGRATION_NOTES.md
 * §3.1 (deviation D-001: Forms Auth -> stateless JWT Bearer).
 *
 * NOTE (real-contract alignment): on a FAILED refresh the interceptor calls
 * `AuthService.clearSession()` (a local-only teardown) and rethrows - it deliberately
 * does NOT call `logout()`, because `logout()` issues a protected `POST /api/auth/logout`
 * that would re-enter the interceptor while the stale refresh token is still present,
 * risking a `logout -> 401 -> refresh -> logout` loop (the "CP2 auth-chain fix"). The
 * failure spec therefore asserts `clearSession()` was invoked.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let routerSpy: jasmine.SpyObj<Router>;

  /** Versioned resource endpoint that participates in the Bearer + 401-refresh flow. */
  const resourceUrl = `${environment.apiUrl}/v1/portals`;
  /** UNVERSIONED refresh endpoint - matches the `/auth/refresh` anti-loop skip fragment. */
  const refreshUrl = `${environment.apiUrl}/auth/refresh`;
  /** UNVERSIONED login endpoint - matches the `/auth/login` anti-loop skip fragment. */
  const loginUrl = `${environment.apiUrl}/auth/login`;

  /**
   * Fully-typed super-user fixture. Field names and nullability match the REAL `User`
   * interface exactly (`userID`/`portalID` casing, every member present), keeping the
   * suite strict-TS clean with no `any`.
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
    updatePassword: false,
    roles: ['Administrators'],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
  };

  beforeEach(() => {
    // AuthService keeps tokens in memory only (never web storage); clear localStorage purely
    // as defensive isolation so no unrelated origin state leaks between tests.
    localStorage.clear();

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    // `navigate` returns Promise<boolean>; resolve it to satisfy the typing of the
    // `void this.router.navigate(...)` call inside AuthService.clearSession().
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        // ORDER MATTERS: install the interceptor chain FIRST, then swap in the testing
        // backend (which preserves the interceptor while capturing outgoing requests).
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => {
    // Proves there were no extra/unmatched requests - e.g. no stray or duplicate refresh.
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches the Bearer header to a resource request when an access token is present', () => {
    authService.accessToken.set('access-1');

    http.get(resourceUrl).subscribe();

    const req = httpMock.expectOne(resourceUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush({ data: [] });
  });

  it('does NOT attach the Bearer header for the login endpoint (anti-loop skip)', () => {
    // Even with a token present, /auth/login is a skip fragment and must stay bare.
    authService.accessToken.set('access-1');

    http.post(loginUrl, { username: 'a', password: 'b' }).subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: {} });
  });

  it('does NOT attach the Bearer header when there is no access token (pass-through)', () => {
    http.get(resourceUrl).subscribe();

    const req = httpMock.expectOne(resourceUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: [] });
  });

  it('refreshes on 401 and retries the original request once with the rotated token', () => {
    // Seeding only the access token is sufficient: the 401 recovery gates on
    // isAuthenticated() (access token present), and refresh() carries the HttpOnly cookie.
    authService.accessToken.set('access-1');

    let body: unknown;
    http.get(resourceUrl).subscribe((result) => (body = result));

    // 1) Original request carries the seeded token and is rejected with 401.
    const first = httpMock.expectOne(resourceUrl);
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });

    // 2) Interceptor triggers refresh(); the refresh POST is a skip fragment, so it
    //    carries NO Authorization header (the anti-loop guarantee). It routes through
    //    ApiService, which unwraps `{ data }`, so the body MUST be `{ data: rotated }`.
    const refreshReq = httpMock.expectOne(refreshUrl);
    expect(refreshReq.request.method).toBe('POST');
    expect(refreshReq.request.headers.has('Authorization')).toBe(false);
    // The refresh token rides the HttpOnly cookie (withCredentials), not the body.
    expect(refreshReq.request.withCredentials).toBe(true);
    expect(refreshReq.request.body).toEqual({});
    const rotated: AuthResponse = {
      accessToken: 'access-2',
      refreshToken: null,
      user: mockUser,
    };
    refreshReq.flush({ data: rotated });

    // 3) The ORIGINAL request is retried exactly once, now with the rotated token.
    const retry = httpMock.expectOne(resourceUrl);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ ok: true });

    // Raw HttpClient returns the retry body verbatim (NOT unwrapped); the rotated
    // access token is now the live session value persisted by AuthService.refresh().
    expect(body).toEqual({ ok: true });
    expect(authService.accessToken()).toBe('access-2');
  });

  it('clears the session and propagates the error when the refresh itself fails', () => {
    authService.accessToken.set('access-1');
    // Real-contract alignment: the interceptor's refresh-FAILURE branch calls
    // `clearSession()` (local-only teardown), NOT `logout()`. Spying on it both proves
    // the teardown happened and suppresses the real `router.navigate` redirect side
    // effect.
    const clearSessionSpy = spyOn(authService, 'clearSession');

    let errored = false;
    http.get(resourceUrl).subscribe({
      next: () => fail('expected the request to error'),
      error: () => (errored = true),
    });

    // Original 401 -> interceptor refreshes -> the refresh ALSO returns 401.
    httpMock
      .expectOne(resourceUrl)
      .flush({}, { status: 401, statusText: 'Unauthorized' });
    httpMock
      .expectOne(refreshUrl)
      .flush({}, { status: 401, statusText: 'Unauthorized' });

    // The inner catchError tore down the session and rethrew; no SECOND refresh was
    // attempted (afterEach `httpMock.verify()` would fail if one were), so recovery
    // terminates deterministically and the original error reaches the caller.
    expect(clearSessionSpy).toHaveBeenCalled();
    expect(errored).toBe(true);
  });
});
