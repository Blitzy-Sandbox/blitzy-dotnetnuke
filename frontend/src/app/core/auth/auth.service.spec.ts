import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from './auth.service';
import { ApiService } from '../services/api.service';
import { AuthResponse, CurrentUser, MeResponse, ProblemDetails } from '../models';

/**
 * Unit spec for {@link AuthService}, focused on the logout revocation contract.
 *
 * MIGRATION (Checkpoint-8 API-contract finding): previously `logout()` POSTed an empty
 * body to an [Authorize] endpoint that the auth interceptor never bearer-authenticated,
 * so the request 401'd and the server-side refresh tokens were never revoked — while
 * `post<void>()` also mis-read the 204. The fix sends the refresh token in the request
 * body via `postNoContent()` (revoke-by-token) and clears the client session
 * deterministically. These tests lock that contract: the stored refresh token is
 * forwarded, and the session is cleared + navigation happens on both success and error.
 * {@link ApiService} and {@link Router} are mocked (jasmine.SpyObj) so no real HTTP runs;
 * `of(...)` stubs make `subscribe()` synchronous. Contributes to Gate 4.
 */
describe('AuthService', () => {
  let service: AuthService;
  let api: jasmine.SpyObj<ApiService>;
  let router: jasmine.SpyObj<Router>;

  const tokenResponse: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    expiresAt: '2030-01-01T00:00:00Z',
    tokenType: 'Bearer',
  };
  const meResponse: MeResponse = {
    user: {
      userID: 1,
      username: 'admin',
      roles: ['Administrators'],
      isSuperUser: false,
    } as CurrentUser,
  };

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['post', 'get', 'postNoContent']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: ApiService, useValue: api },
        { provide: Router, useValue: router },
      ],
    });
    service = TestBed.inject(AuthService);
  });

  /** Drive a real login so the private refresh-token signal is populated. */
  function seedSession(): void {
    api.post.and.returnValue(of(tokenResponse));
    api.get.and.returnValue(of(meResponse));
    service.login({ username: 'admin', password: 'p' }).subscribe();
    expect(service.isAuthenticated()).toBeTrue();
  }

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('logout()', () => {
    it('revokes by sending the stored refresh token in the body via postNoContent', () => {
      seedSession();
      api.postNoContent.and.returnValue(of(void 0));

      service.logout();

      expect(api.postNoContent).toHaveBeenCalledWith('auth/logout', { refreshToken: 'refresh-1' });
    });

    it('clears the client session and navigates to /auth on success', () => {
      seedSession();
      api.postNoContent.and.returnValue(of(void 0));

      service.logout();

      expect(service.isAuthenticated()).toBeFalse();
      expect(service.currentUser()).toBeNull();
      expect(service.getAccessToken()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/auth']);
    });

    it('still clears the session and navigates when the revocation call errors', () => {
      seedSession();
      const problem: ProblemDetails = { type: 'about:blank', title: 'Server Error', status: 500 };
      api.postNoContent.and.returnValue(throwError(() => problem));

      service.logout();

      expect(service.isAuthenticated()).toBeFalse();
      expect(service.getAccessToken()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/auth']);
    });

    it('sends an empty refresh token when there is no active session', () => {
      api.postNoContent.and.returnValue(of(void 0));

      service.logout();

      expect(api.postNoContent).toHaveBeenCalledWith('auth/logout', { refreshToken: '' });
      expect(router.navigate).toHaveBeenCalledWith(['/auth']);
    });
  });
});
