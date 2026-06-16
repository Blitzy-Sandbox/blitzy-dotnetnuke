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
 * Unit tests for {@link AuthService} — the signal-based JWT authentication
 * singleton (`core/auth/auth.service.ts`). Satisfies Gate 4
 * (`ng test --watch=false --browsers=ChromeHeadless` -> 100% pass).
 *
 * Testing strategy (per the AAP and the sibling `core/services/api.service.spec.ts`):
 *  - Standalone testing setup: wire the REAL `ApiService` over the HTTP testing
 *    backend with `provideHttpClient()` + `provideHttpClientTesting()` (NOT the
 *    deprecated `HttpClientTestingModule`). This exercises the real
 *    `AuthService -> ApiService -> HttpClient` path so envelope unwrapping and
 *    URL composition are validated end-to-end.
 *  - Only `Router` is mocked (a Jasmine spy), so the logout redirect can be
 *    asserted without performing real navigation.
 *  - `ApiService` UNWRAPS the backend `{ data, meta }` success envelope, so every
 *    `req.flush(...)` body is wrapped as `{ data: <payload> }`; flushing the bare
 *    payload would deliver `undefined` to the service signals.
 *  - `AuthService` persists/reads the session from `localStorage`, so it is
 *    cleared BEFORE `TestBed.inject(AuthService)` (so the service constructs with
 *    empty storage) and again in `afterEach` to prevent cross-test pollution.
 *
 * MIGRATION: `AuthService` is the sanctioned replacement for the legacy
 * `Library/Components/Security/PortalSecurity.vb` Forms-Authentication + DES
 * model. The `hasRole()` super-user shortcut asserted below preserves the legacy
 * `IsInRoles` semantics (`If objUserInfo.IsSuperUser Or (...)`, PortalSecurity.vb
 * L123): a super user is granted every role.
 */
describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  /** Auth endpoints are UNVERSIONED: `${apiUrl}/auth/<action>` (apiUrl ends at `/api`). */
  const authBase = `${environment.apiUrl}/auth`;

  /**
   * Fully-typed super-user fixture. Field names and nullability match the REAL
   * `User` interface exactly (`userID`/`portalID` casing, every member present),
   * keeping the suite strict-TS clean with no `any`.
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

  /** Non-super-user fixture used to exercise explicit role-membership checks. */
  const standardUser: User = {
    ...mockUser,
    userID: 2,
    username: 'editor',
    displayName: 'Editor',
    email: 'editor@example.com',
    firstName: 'Ed',
    lastName: 'Itor',
    fullName: 'Ed Itor',
    isSuperUser: false,
    roles: ['Editors'],
  };

  /** Login/refresh success payload for the super user. */
  const mockResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    user: mockUser,
  };

  /** Login success payload for the non-super (standard) user. */
  const standardResponse: AuthResponse = {
    accessToken: 'access-std',
    refreshToken: 'refresh-std',
    user: standardUser,
  };

  beforeEach(() => {
    // Clear BEFORE injecting the service so it constructs with empty storage.
    localStorage.clear();

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    // `navigate` returns Promise<boolean>; resolve it to satisfy the typing of
    // the `void this.router.navigate(...)` call inside the service.
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
    // No stray/unmatched HTTP requests should remain.
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

  it('login() persists tokens to localStorage', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    expect(localStorage.getItem('dnn.accessToken')).toBe('access-1');
    expect(localStorage.getItem('dnn.refreshToken')).toBe('refresh-1');
  });

  it('refresh() posts the current refresh token and rotates both tokens', () => {
    // Establish an authenticated session whose refresh token will be rotated.
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

  it('logout() clears state and redirects to /auth/login', () => {
    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    service.logout();
    // The API returns an empty (non-enveloped) body for logout; `ApiService.post`
    // tolerates it via `response?.data`. The logout subscription completes
    // synchronously after the flush, so the post-conditions hold immediately.
    const req = httpMock.expectOne(`${authBase}/logout`);
    expect(req.request.method).toBe('POST');
    req.flush({});

    expect(service.accessToken()).toBeNull();
    expect(service.refreshToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('hasRole() grants every role to a super user and denies when no user is present', () => {
    // No authenticated user yet -> always false.
    expect(service.hasRole('Administrators')).toBe(false);

    service.login({ username: 'admin', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse });

    // MIGRATION: super-user shortcut (PortalSecurity.vb L123) -> any role granted.
    expect(service.hasRole('AnythingBecauseSuperUser')).toBe(true);
    expect(service.hasRole('Administrators')).toBe(true);
  });

  it('hasRole() honors explicit role membership for non-super users', () => {
    service.login({ username: 'editor', password: 'secret' }).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: standardResponse });

    // Contained role -> true; any other role -> false.
    expect(service.hasRole('Editors')).toBe(true);
    expect(service.hasRole('Administrators')).toBe(false);
  });
});
