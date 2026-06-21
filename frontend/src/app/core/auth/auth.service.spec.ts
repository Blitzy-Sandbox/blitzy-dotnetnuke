import { provideHttpClient } from '@angular/common/http';
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

/**
 * Gate 4 unit tests for the signal-based {@link AuthService} JWT singleton.
 *
 * Strategy (CRITICAL): the REAL {@link ApiService} is exercised over the HTTP
 * testing backend (`provideHttpClient()` + `provideHttpClientTesting()`), NOT a
 * hand-rolled spy. This drives the genuine `AuthService -> ApiService ->
 * HttpClient` path and sidesteps generic-spy typing pitfalls. Only the
 * {@link Router} is mocked, so the logout redirect can be asserted without real
 * navigation. The deprecated `HttpClientTestingModule` NgModule is intentionally
 * avoided in favour of the standalone `provideHttpClientTesting()` provider.
 *
 * Envelope rule (single most important correctness invariant): `ApiService`
 * unwraps the backend `{ data, meta }` success envelope, so EVERY `req.flush(...)`
 * body for the enveloped verbs MUST be wrapped as `{ data: <payload> }`. Flushing
 * a bare payload would make the unwrapped value `undefined` and the signals would
 * receive nothing. The `logout` endpoint is the lone exception: it returns an
 * empty body, which `ApiService.post` tolerates via `response?.data`.
 *
 * Secure-storage contract (MIGRATION Finding CP-FINAL-2 / CWE-922): tokens are
 * NEVER persisted to `localStorage`. The refresh token rides an `HttpOnly` cookie
 * the JS testing backend cannot see, and the access token lives ONLY in an
 * in-memory signal. The auth POSTs (login/refresh/logout) therefore set
 * `withCredentials: true`, and `refresh()` sends an EMPTY body. Only the non-secret
 * `currentUser` is mirrored to `localStorage` (`dnn.currentUser`), so storage is
 * cleared in BOTH `beforeEach` (empty-storage starting state) and `afterEach`
 * (no cross-test pollution). `httpMock.verify()` in `afterEach` asserts that every
 * test consumed exactly the requests it issued.
 *
 * Auth URLs are UNVERSIONED (`${environment.apiUrl}/auth/<action>`), unlike the
 * versioned resource endpoints; expectations are built from the imported
 * `environment` so the suite stays correct if the base URL changes.
 */
describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  /** Unversioned auth base, e.g. `http://localhost:5000/api/auth`. */
  const authBase = `${environment.apiUrl}/auth`;

  /**
   * Superuser fixture. Property names match the real {@link User} contract:
   * `userID` / `portalID` carry the trailing-acronym casing emitted by the .NET
   * API's System.Text.Json camelCase policy (NOT `userId` / `portalId`).
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

  /**
   * Token + user payload issued by `POST /auth/login` and `POST /auth/refresh`.
   * MIGRATION (Finding CP-FINAL-2): the server serializes `refreshToken` as `null`
   * because the refresh token is delivered out-of-band as an `HttpOnly` cookie.
   */
  const mockResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: null,
    user: mockUser,
  };

  beforeEach(() => {
    // Clear BEFORE constructing the service so it initialises from empty storage.
    localStorage.clear();

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    // navigate() returns Promise<boolean>; resolve true to satisfy the type the
    // service `void`-discards in completeLogout().
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('starts unauthenticated when storage is empty', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.hasSession()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('login() posts credentials with credentials, unwraps { data }, and stores the session', () => {
    let emitted: AuthResponse | undefined;
    service
      .login({ username: 'admin', password: 'secret' })
      .subscribe((response) => (emitted = response));

    const req = httpMock.expectOne(`${authBase}/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'admin', password: 'secret' });
    // withCredentials lets the browser store the HttpOnly refresh cookie the API sets.
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: mockResponse });

    expect(service.accessToken()).toBe('access-1');
    expect(service.currentUser()).toEqual(mockUser);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.hasSession()).toBe(true);
    expect(emitted).toEqual(mockResponse);
  });

  it('login() persists ONLY the user to localStorage (never the tokens)', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    expect(localStorage.getItem('dnn.currentUser')).toBe(JSON.stringify(mockUser));
    // Secure-storage hardening: no token is ever written to localStorage.
    expect(localStorage.getItem('dnn.accessToken')).toBeNull();
    expect(localStorage.getItem('dnn.refreshToken')).toBeNull();
  });

  it('refresh() posts an EMPTY body with credentials and rotates the access token', () => {
    // Establish a session so the access token can be observed rotating.
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    const rotated: AuthResponse = {
      accessToken: 'access-2',
      refreshToken: null,
      user: mockUser,
    };
    service.refresh().subscribe();

    const req = httpMock.expectOne(`${authBase}/refresh`);
    expect(req.request.method).toBe('POST');
    // The refresh token rides the HttpOnly cookie, so the body is empty and the request
    // opts into credentials so the browser attaches that cookie.
    expect(req.request.body).toBeNull();
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: rotated });

    expect(service.accessToken()).toBe('access-2');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('me() fetches and sets the current user', () => {
    let emitted: User | undefined;
    service.me().subscribe((user) => (emitted = user));

    const req = httpMock.expectOne(`${authBase}/me`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: mockUser });

    expect(service.currentUser()).toEqual(mockUser);
    expect(emitted).toEqual(mockUser);
  });

  it('logout() clears all session state, sends credentials, and redirects to /auth/login', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    service.logout();
    // The logout endpoint returns an empty body (no { data } envelope); ApiService
    // tolerates it via response?.data. The subscription's next runs synchronously
    // after flush, so post-conditions can be asserted immediately below.
    const req = httpMock.expectOne(`${authBase}/logout`);
    // withCredentials sends the HttpOnly refresh cookie so the server can expire it.
    expect(req.request.withCredentials).toBe(true);
    req.flush({});

    expect(service.accessToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.hasSession()).toBe(false);
    expect(localStorage.getItem('dnn.currentUser')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('initializeSession() issues no HTTP and stays unauthenticated when no user is persisted', () => {
    service.initializeSession().subscribe();

    // No persisted user -> short-circuit, no cookie-backed refresh attempt.
    httpMock.expectNone(`${authBase}/refresh`);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('initializeSession() re-mints the access token via a cookie-backed refresh when a user was persisted', () => {
    // Simulate a post-reload state: the non-secret user survived in the signal/storage,
    // but the in-memory access token is gone until re-minted from the refresh cookie.
    service.currentUser.set(mockUser);

    service.initializeSession().subscribe();

    const req = httpMock.expectOne(`${authBase}/refresh`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    expect(req.request.withCredentials).toBe(true);
    req.flush({
      data: { accessToken: 'access-restored', refreshToken: null, user: mockUser },
    });

    expect(service.accessToken()).toBe('access-restored');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('initializeSession() clears local state WITHOUT navigating when the cookie-backed refresh fails', () => {
    service.currentUser.set(mockUser);

    service.initializeSession().subscribe();

    httpMock
      .expectOne(`${authBase}/refresh`)
      .flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.hasSession()).toBe(false);
    // Bootstrap-time recovery must NOT navigate; the route guard handles redirects.
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('hasRole() returns false with no user and true for any role when superuser', () => {
    // No user has been loaded yet -> every role check is denied.
    expect(service.hasRole('Administrators')).toBe(false);

    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    // mockUser.isSuperUser === true -> the superuser short-circuit grants any role,
    // mirroring the legacy PortalSecurity.IsInRoles `objUserInfo.IsSuperUser` check.
    expect(service.hasRole('AnythingBecauseSuperUser')).toBe(true);
    expect(service.hasRole('Administrators')).toBe(true);
  });

  it('hasRole() honors contained roles and rejects non-members for a non-superuser', () => {
    const standardUser: User = {
      ...mockUser,
      isSuperUser: false,
      roles: ['Administrators', 'Editors'],
    };
    const standardResponse: AuthResponse = {
      accessToken: 'access-std',
      refreshToken: null,
      user: standardUser,
    };

    service.login({ username: 'editor', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: standardResponse });

    expect(service.hasRole('Administrators')).toBe(true);
    expect(service.hasRole('Editors')).toBe(true);
    expect(service.hasRole('Hosts')).toBe(false);
  });
});
