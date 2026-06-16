import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/**
 * URL fragments that must NEVER carry a Bearer header or trigger a 401 -> refresh
 * recovery (anti-loop).
 *
 * The `AuthService.refresh()` call issues a brand-new `POST /api/auth/refresh`
 * through `HttpClient`, which re-enters THIS interceptor. Skipping these fragments
 * is what prevents infinite refresh recursion. (Note: retrying via `next(...)` does
 * NOT re-enter the interceptor, because `next` is the downstream handler, not the
 * `HttpClient` entry point.)
 *
 * Only `/auth/login` and `/auth/refresh` are skipped: `/auth/me` and `/auth/logout`
 * intentionally DO receive the Bearer header and DO participate in the 401 -> refresh
 * flow, because they act on the caller's identity.
 */
const AUTH_SKIP_FRAGMENTS: readonly string[] = ['/auth/login', '/auth/refresh'];

/**
 * Functional HTTP interceptor that attaches the JWT Bearer header to outgoing API
 * requests and performs a single `401 Unauthorized` -> refresh -> retry recovery.
 * Registered in `app.config.ts` via
 * `provideHttpClient(withInterceptors([authInterceptor]))`.
 *
 * 401 recovery flow (preserve this RxJS shape exactly):
 *  - The outer `catchError` wraps `next(authReq)`. On a `401` - and only when a
 *    refresh token exists and the URL is not skipped - it calls
 *    `AuthService.refresh()` and `switchMap`s to retry the ORIGINAL request,
 *    re-cloned with the freshly rotated access token.
 *  - The INNER `catchError` sits below the `switchMap`, so it catches BOTH a failed
 *    `refresh()` AND a failed retried request. Either way it logs out and rethrows,
 *    which guarantees the retry happens AT MOST ONCE (no retry loop).
 *  - Unauthenticated `401`s (no refresh token) simply propagate untouched, so the
 *    interceptor never issues a futile refresh.
 *
 * MIGRATION: replaces the legacy implicit ASP.NET Forms Authentication cookie -
 * managed transparently by the browser in `PortalSecurity.vb`
 * (`FormsAuthentication.SignOut()` + portal cookies, L77-L90) - with an EXPLICIT
 * `Authorization: Bearer <accessToken>` header plus stateless refresh-token
 * rotation. See MIGRATION_NOTES.md section 3.1 (deviation D-001: Forms Auth ->
 * stateless JWT Bearer).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  // Anti-loop: never attach a token to, or attempt a refresh for, the login /
  // refresh endpoints (the refresh request re-enters this interceptor).
  if (AUTH_SKIP_FRAGMENTS.some((fragment) => req.url.includes(fragment))) {
    return next(req);
  }

  const token = authService.accessToken();
  const authReq = token ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Only attempt recovery for a real 401 when a refresh token is available;
      // otherwise let the error propagate to the caller unchanged.
      if (error.status === 401 && authService.refreshToken() !== null) {
        return authService.refresh().pipe(
          // Retry the ORIGINAL request exactly once with the rotated access token.
          switchMap((response) => next(withBearer(req, response.accessToken))),
          // Catches a failed refresh OR a failed retry -> clear the local session
          // and rethrow. We call `clearSession()` (local-only) rather than
          // `logout()` ON PURPOSE: `logout()` would issue a protected
          // `POST /api/auth/logout` that re-enters this interceptor while the stale
          // refresh token is still present, producing a
          // logout -> 401 -> refresh -> logout loop. `clearSession()` issues NO HTTP
          // request - it clears the tokens/user and redirects to /auth/login exactly
          // once - so recovery terminates deterministically (CP2 auth-chain fix;
          // MIGRATION_NOTES.md §3.1).
          catchError((refreshError: unknown) => {
            authService.clearSession();
            return throwError(() => refreshError);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};

/**
 * Clone a request, adding (or overwriting) the `Authorization: Bearer <token>`
 * header. `HttpRequest` instances are immutable, so `clone` returns a new instance
 * and the original request is left untouched.
 */
function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
