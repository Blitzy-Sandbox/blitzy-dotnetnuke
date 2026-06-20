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
 * Storage hygiene: `AuthService` persists/reads the token pair and user from
 * `localStorage` (`dnn.*` keys), so storage is cleared in BOTH `beforeEach`
 * (before the service is constructed, guaranteeing an empty-storage starting
 * state) and `afterEach` (preventing cross-test pollution). `httpMock.verify()`
 * in `afterEach` asserts that every test consumed exactly the requests it issued.
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

  /** Token + user payload issued by `POST /auth/login` and `POST /auth/refresh`. */
  const mockResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
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
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
    expect(service.refreshToken()).toBeNull();
  });

  it('login() posts credentials, unwraps { data }, and stores the session', () => {
    let emitted: AuthResponse | undefined;
    service
      .login({ username: 'admin', password: 'secret' })
      .subscribe((response) => (emitted = response));

    const req = httpMock.expectOne(`${authBase}/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ username: 'admin', password: 'secret' });
    req.flush({ data: mockResponse });

    expect(service.accessToken()).toBe('access-1');
    expect(service.refreshToken()).toBe('refresh-1');
    expect(service.currentUser()).toEqual(mockUser);
    expect(service.isAuthenticated()).toBe(true);
    expect(emitted).toEqual(mockResponse);
  });

  it('login() persists the token pair to localStorage', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    expect(localStorage.getItem('dnn.accessToken')).toBe('access-1');
    expect(localStorage.getItem('dnn.refreshToken')).toBe('refresh-1');
  });

  it('refresh() posts the current refresh token and rotates both tokens', () => {
    // Establish a session so refreshToken() is populated for the rotation call.
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    const rotated: AuthResponse = {
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      user: mockUser,
    };
    service.refresh().subscribe();

    const req = httpMock.expectOne(`${authBase}/refresh`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-1' });
    req.flush({ data: rotated });

    expect(service.accessToken()).toBe('access-2');
    expect(service.refreshToken()).toBe('refresh-2');
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

  it('logout() clears all session state and redirects to /auth/login', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    service.logout();
    // The logout endpoint returns an empty body (no { data } envelope); ApiService
    // tolerates it via response?.data. The subscription's next runs synchronously
    // after flush, so post-conditions can be asserted immediately below.
    httpMock.expectOne(`${authBase}/logout`).flush({});

    expect(service.accessToken()).toBeNull();
    expect(service.refreshToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('dnn.accessToken')).toBeNull();
    expect(localStorage.getItem('dnn.refreshToken')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
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
      refreshToken: 'refresh-std',
      user: standardUser,
    };

    service.login({ username: 'editor', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: standardResponse });

    expect(service.hasRole('Administrators')).toBe(true);
    expect(service.hasRole('Editors')).toBe(true);
    expect(service.hasRole('Hosts')).toBe(false);
  });
});
