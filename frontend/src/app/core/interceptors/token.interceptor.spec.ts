// MIGRATION: Spec for the net-new functional tokenInterceptor (replaces DNN Forms-auth cookie attachment).
// Verifies the JWT Bearer header is attached to API-origin requests only, never leaked to a third-party host, and
// never attached when unauthenticated. Functional interceptor exercised through a real HttpClient configured with
// withInterceptors([tokenInterceptor]) (Gate 4, non-interactive ChromeHeadless).
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { tokenInterceptor } from './token.interceptor';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

describe('tokenInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let accessTokenValue: string | null;

  // Minimal AuthService stub exposing only what the interceptor reads (the accessToken() signal getter). Using a
  // stub means the real AuthService (which needs HttpClient/Router) is never constructed.
  const authServiceStub = {
    accessToken: (): string | null => accessTokenValue,
  };

  beforeEach(() => {
    accessTokenValue = 'test-access-token';
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authServiceStub },
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('attaches Authorization: Bearer to API-origin requests when authenticated', () => {
    httpClient.get(`${environment.apiUrl}/portals`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush({ data: [], meta: {} });
  });

  it('does NOT attach the token to a third-party origin (no token leakage)', () => {
    const thirdPartyUrl = 'https://third-party.example.com/data';
    httpClient.get(thirdPartyUrl).subscribe();

    const req = httpMock.expectOne(thirdPartyUrl);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('does NOT attach a header when unauthenticated (no access token in memory)', () => {
    accessTokenValue = null;
    httpClient.get(`${environment.apiUrl}/portals`).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: [], meta: {} });
  });

  // MIGRATION (Issue 10): auth-establishment endpoints must NOT carry a (possibly stale) in-memory access token.
  ['/auth/login', '/auth/refresh', '/auth/forgot-password'].forEach((endpoint) => {
    it(`does NOT attach the token to ${endpoint} even when authenticated`, () => {
      const url = `${environment.apiUrl}${endpoint}`;
      httpClient.post(url, {}).subscribe();

      const req = httpMock.expectOne(url);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({ data: {}, meta: {} });
    });
  });

  it('does NOT attach the token to /auth/login even with a query string (path-suffix match strips the query)', () => {
    const url = `${environment.apiUrl}/auth/login?returnUrl=%2Fportals`;
    httpClient.post(url, {}).subscribe();

    const req = httpMock.expectOne(url);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ data: {}, meta: {} });
  });

  it('STILL attaches the token to /auth/me (current-user identity is not exempt)', () => {
    const url = `${environment.apiUrl}/auth/me`;
    httpClient.get(url).subscribe();

    const req = httpMock.expectOne(url);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush({ data: {}, meta: {} });
  });

  it('STILL attaches the token to /auth/logout (session-scoped, not exempt)', () => {
    const url = `${environment.apiUrl}/auth/logout`;
    httpClient.post(url, {}).subscribe();

    const req = httpMock.expectOne(url);
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush(null);
  });
});
