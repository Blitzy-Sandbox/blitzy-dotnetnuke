// MIGRATION: Spec for the net-new signal-based AuthService (replaces PortalSecurity / UserMembership Forms-auth).
// Verifies login/refresh/logout/me hit the correct /api/v1/auth URLs + HTTP verbs with the correct bodies
// (including the multi-tenant portalId on login), signal-state updates (accessToken, isAuthenticated, currentUser),
// and refresh-token rotation (a refreshed response's new refreshToken replaces the stored one).
// MIGRATION: the backend wraps every success response in the { data, meta } envelope (AAP Section 0.1.2), so the
// login/refresh/me mocks flush ENVELOPED bodies ({ data: <payload>, meta: {} }); the service unwraps `data` itself
// (AuthService is the documented HttpClient exception). logout returns 204/no body, so it is flushed as null.
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import type { AuthResponse, CurrentUser, LoginRequest } from '../models/auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const authBase = `${environment.apiUrl}/auth`;

  const mockUser: CurrentUser = {
    userId: 1,
    username: 'admin',
    email: 'admin@example.com',
    displayName: 'Administrator',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: true,
    portalId: 0,
    roles: ['Administrators'],
  };

  const mockResponse: AuthResponse = {
    accessToken: 'access-token-1',
    refreshToken: 'refresh-token-1',
    tokenType: 'Bearer',
    expiresIn: 3600,
    expiresAt: '2026-01-01T00:00:00.000Z',
    user: mockUser,
  };

  const credentials: LoginRequest = {
    username: 'admin',
    password: 'P@ssw0rd',
    portalId: 0,
    rememberMe: true,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created and start unauthenticated', () => {
    expect(service).toBeTruthy();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
  });

  it('login() should POST credentials (including portalId) to auth/login and set session state', () => {
    let result: AuthResponse | undefined;
    service.login(credentials).subscribe((response) => (result = response));

    const req = httpMock.expectOne(`${authBase}/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(credentials);
    expect((req.request.body as LoginRequest).portalId).toBe(0);
    req.flush({ data: mockResponse, meta: {} });

    expect(result).toEqual(mockResponse);
    expect(service.accessToken()).toBe('access-token-1');
    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()).toEqual(mockUser);
  });

  it('refresh() should POST the stored refresh token and rotate it', () => {
    // Seed a session so refresh-token-1 is stored.
    service.login(credentials).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse, meta: {} });

    const rotated: AuthResponse = {
      ...mockResponse,
      accessToken: 'access-token-2',
      refreshToken: 'refresh-token-2',
    };
    service.refresh().subscribe();
    const req = httpMock.expectOne(`${authBase}/refresh`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-token-1' });
    req.flush({ data: rotated, meta: {} });
    expect(service.accessToken()).toBe('access-token-2');

    // A subsequent refresh MUST use the rotated token (refresh-token-2).
    service.refresh().subscribe();
    const req2 = httpMock.expectOne(`${authBase}/refresh`);
    expect(req2.request.body).toEqual({ refreshToken: 'refresh-token-2' });
    req2.flush({
      data: { ...rotated, accessToken: 'access-token-3', refreshToken: 'refresh-token-3' },
      meta: {},
    });
    expect(service.accessToken()).toBe('access-token-3');
  });

  it('me() should GET auth/me and hydrate the currentUser signal', () => {
    let result: CurrentUser | undefined;
    service.me().subscribe((user) => (result = user));

    const req = httpMock.expectOne(`${authBase}/me`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: mockUser, meta: {} });

    expect(result).toEqual(mockUser);
    expect(service.currentUser()).toEqual(mockUser);
  });

  it('logout() should POST auth/logout, clear session state, and redirect to /auth/login', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    // Seed a session.
    service.login(credentials).subscribe();
    httpMock.expectOne(`${authBase}/login`).flush({ data: mockResponse, meta: {} });
    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();
    const req = httpMock.expectOne(`${authBase}/logout`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-token-1' });
    req.flush(null);

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith(['/auth/login']);
  });
});
