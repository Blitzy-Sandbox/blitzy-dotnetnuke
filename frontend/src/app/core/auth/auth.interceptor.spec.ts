import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { AuthResponse } from '../models';

/**
 * Unit spec for the functional {@link authInterceptor}, run through a real HttpClient
 * wired with the interceptor against {@link HttpTestingController}.
 *
 * MIGRATION (Checkpoint-8 Security/Token-Revocation finding): the interceptor keeps the
 * auth-flow routes (login/refresh/logout) out of bearer attachment + silent refresh.
 * With the revoke-by-token logout contract this is correct by design — logout carries
 * its refresh token in the body and needs no bearer. These tests lock: (a) a normal
 * request gets a Bearer header; (b) `/auth/logout` does NOT (and getAccessToken is not
 * even consulted for it); (c) a 401 triggers exactly one silent refresh + retry with the
 * new token; (d) a failed refresh logs out. Contributes to Gate 4.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', [
      'getAccessToken',
      'refresh',
      'logout',
    ]);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches a Bearer header to a normal (non-auth-flow) request', () => {
    auth.getAccessToken.and.returnValue('access-1');

    http.get('/api/portals').subscribe();

    const req = httpMock.expectOne('/api/portals');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush({ data: [] });
  });

  it('does NOT attach a Bearer header to the /auth/logout auth-flow route', () => {
    auth.getAccessToken.and.returnValue('access-1');

    http.post('/api/auth/logout', { refreshToken: 'r' }).subscribe();

    const req = httpMock.expectOne('/api/auth/logout');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    // The auth-flow short-circuit runs before the token is ever read.
    expect(auth.getAccessToken).not.toHaveBeenCalled();
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('on a 401 performs a single silent refresh and retries the original request', () => {
    // First attempt uses access-1; after refresh the retry uses access-2.
    auth.getAccessToken.and.returnValues('access-1', 'access-2');
    const refreshed: AuthResponse = {
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      expiresAt: '2030-01-01T00:00:00Z',
      tokenType: 'Bearer',
    };
    auth.refresh.and.returnValue(of(refreshed));

    const results: unknown[] = [];
    http.get('/api/portals').subscribe((r) => results.push(r));

    const first = httpMock.expectOne('/api/portals');
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush(
      { type: 'about:blank', title: 'Unauthorized', status: 401 },
      { status: 401, statusText: 'Unauthorized' }
    );

    // The interceptor refreshed once, then retried the ORIGINAL request with the new token.
    const retry = httpMock.expectOne('/api/portals');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ data: ['ok'] });

    expect(auth.refresh).toHaveBeenCalledTimes(1);
    expect(results.length).toBe(1);
  });

  it('logs out when the silent refresh itself fails', () => {
    auth.getAccessToken.and.returnValue('access-1');
    auth.refresh.and.returnValue(throwError(() => ({ status: 401 })));

    http.get('/api/portals').subscribe({ error: () => undefined });

    const first = httpMock.expectOne('/api/portals');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.logout).toHaveBeenCalledTimes(1);
  });
});
