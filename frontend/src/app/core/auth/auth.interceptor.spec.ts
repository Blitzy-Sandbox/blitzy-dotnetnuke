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
 * Gate 4 unit tests for the functional {@link authInterceptor} (`HttpInterceptorFn`).
 *
 * Strategy (CRITICAL): the interceptor is wired through the REAL HTTP stack rather
 * than invoked in isolation. `provideHttpClient(withInterceptors([authInterceptor]))`
 * registers the genuine interceptor chain and MUST come BEFORE
 * `provideHttpClientTesting()`, which then swaps in the testing backend while leaving
 * the interceptor chain intact. Consequently every `http.get`/`http.post` below drives
 * the REAL `authInterceptor` + REAL `AuthService` + REAL `ApiService` together — the
 * `AuthService` is NOT mocked. Auth state is seeded by writing the service's PUBLIC
 * writable signals (`accessToken.set(...)` / `currentUser.set(...)`), which both
 * avoids generic-spy typing pain and exercises the authentic refresh flow. The refresh
 * token itself is an `HttpOnly` cookie the test backend cannot see, so the interceptor's
 * 401 -> refresh recovery is gated on `hasSession()` (a present `currentUser`).
 *
 * Envelope rule: `AuthService.refresh()` routes through `ApiService`, which unwraps the
 * backend `{ data, meta }` success envelope, so the refresh response MUST be flushed as
 * `{ data: <AuthResponse> }`. By contrast the DRIVING requests use the RAW `HttpClient`
 * (not `ApiService`), so their flushed bodies are returned verbatim (NOT unwrapped) —
 * e.g. `retry.flush({ ok: true })` yields `body === { ok: true }`.
 *
 * Anti-loop guarantee: the `POST /auth/refresh` request issued by `refresh()` re-enters
 * this very interceptor; because `/auth/refresh` (and `/auth/login`) are skip fragments,
 * that request carries NO `Authorization` header and a 401 on it does NOT spawn a second
 * refresh. `httpMock.verify()` in `afterEach` asserts the original request is retried at
 * most once with no stray refresh attempts.
 *
 * Storage hygiene: the real `AuthService` mirrors only the non-secret `currentUser` to
 * `localStorage` (`dnn.currentUser`; tokens are NEVER persisted), so storage is cleared in
 * BOTH `beforeEach` (clean starting state) and `afterEach` (no cross-test pollution). Only
 * the `Router` is mocked, satisfying the service's redirect dependency without real
 * navigation.
 *
 * MIGRATION: this suite validates the explicit `Authorization: Bearer <accessToken>`
 * header + refresh-token rotation that replaces the legacy implicit ASP.NET Forms
 * Authentication cookie (`PortalSecurity.SignOut`, `Library/Components/Security/
 * PortalSecurity.vb` L77-95) and DES encryption (`Encrypt`/`Decrypt`, L138-211). Part of
 * the single sanctioned auth change (DEV-001); see root `MIGRATION_NOTES.md` §3.1.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let routerSpy: jasmine.SpyObj<Router>;

  /** Versioned resource endpoint — receives the Bearer header and 401 -> refresh recovery. */
  const resourceUrl = `${environment.apiUrl}/v1/portals`;
  /** Unversioned refresh endpoint — a skip fragment (never bearer-tagged, never re-refreshed). */
  const refreshUrl = `${environment.apiUrl}/auth/refresh`;
  /** Unversioned login endpoint — a skip fragment (unauthenticated). */
  const loginUrl = `${environment.apiUrl}/auth/login`;

  /**
   * Superuser fixture for the rotated session. Property names follow the real
   * {@link User} contract: `userID` / `portalID` carry the trailing-acronym casing the
   * .NET API's System.Text.Json camelCase policy emits (NOT `userId` / `portalId`).
   */
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

  beforeEach(() => {
    // Clear BEFORE the service is constructed so it initialises from empty storage.
    localStorage.clear();

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    // navigate() returns Promise<boolean>; resolve true to satisfy the type the service
    // `void`-discards in its logout/clear-session redirect.
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        // ORDER MATTERS: register the real interceptor chain first, then let the testing
        // backend replace the network handler while preserving that chain.
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
    // Proves there were no extra/duplicate requests (e.g. a second, illegal refresh).
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches the Bearer header when a token is present', () => {
    authService.accessToken.set('access-1');

    http.get(resourceUrl).subscribe();

    const req = httpMock.expectOne(resourceUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush({ data: [] });
  });

  it('does NOT attach the Bearer header for the login endpoint', () => {
    // Even with a token present, the /auth/login skip fragment must pass through bare.
    authService.accessToken.set('access-1');

    http.post(loginUrl, { username: 'a', password: 'b' }).subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: {} });
  });

  it('does NOT attach the Bearer header when there is no token', () => {
    // No access token seeded -> the request passes through unmodified.
    http.get(resourceUrl).subscribe();

    const req = httpMock.expectOne(resourceUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: [] });
  });

  it('refreshes on 401 and retries the original request once with the new token', () => {
    authService.accessToken.set('access-1');
    // Seed a session via currentUser (the interceptor gates 401 -> refresh on hasSession()).
    authService.currentUser.set(mockUser);

    let body: unknown;
    http.get(resourceUrl).subscribe((result) => (body = result));

    // 1) Original request goes out with the current token and is rejected with 401.
    const first = httpMock.expectOne(resourceUrl);
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });

    // 2) The interceptor triggers refresh(). That POST re-enters the interceptor but, as a
    //    skip fragment, carries NO Authorization header. It is routed through ApiService,
    //    so its body MUST be the unwrappable `{ data: <AuthResponse> }` envelope.
    const refreshReq = httpMock.expectOne(refreshUrl);
    expect(refreshReq.request.method).toBe('POST');
    expect(refreshReq.request.headers.has('Authorization')).toBe(false);
    // The refresh token rides an HttpOnly cookie, so the body is empty and the request
    // opts into credentials so the browser attaches that cookie.
    expect(refreshReq.request.body).toBeNull();
    expect(refreshReq.request.withCredentials).toBe(true);
    const rotated: AuthResponse = {
      accessToken: 'access-2',
      refreshToken: null,
      user: mockUser,
    };
    refreshReq.flush({ data: rotated });

    // 3) The original request is retried exactly once, now bearing the rotated token.
    const retry = httpMock.expectOne(resourceUrl);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ ok: true });

    // Raw HttpClient call -> the retry body is returned verbatim (not envelope-unwrapped).
    expect(body).toEqual({ ok: true });
    // refresh() persisted the rotated pair through the real AuthService.
    expect(authService.accessToken()).toBe('access-2');
  });

  it('clears the session and propagates the error when refresh fails', () => {
    authService.accessToken.set('access-1');
    // Seed a session via currentUser (the interceptor gates 401 -> refresh on hasSession()).
    authService.currentUser.set(mockUser);

    // The interceptor's inner catchError invokes AuthService.clearSession() (local-only
    // teardown) — NOT logout() — when a refresh has already failed. This is the M10/DEV-038
    // anti-recursion fix: logout() POSTs to /api/auth/logout, which is intentionally NOT a
    // skip fragment and would re-enter the interceptor, so its own 401 could spawn another
    // refresh -> logout loop. Spying on clearSession() both asserts that contract and
    // suppresses the real redirect side effect.
    const clearSessionSpy = spyOn(authService, 'clearSession');

    let errored = false;
    http.get(resourceUrl).subscribe({
      next: () => fail('expected the request to error'),
      error: () => (errored = true),
    });

    // Original request -> 401, then the single refresh attempt -> 401. The exact error body
    // is irrelevant to these assertions, so an empty body suffices.
    httpMock
      .expectOne(resourceUrl)
      .flush({}, { status: 401, statusText: 'Unauthorized' });
    httpMock
      .expectOne(refreshUrl)
      .flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(clearSessionSpy).toHaveBeenCalled();
    expect(errored).toBe(true);
  });
});
