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
 * recovery (anti-loop guard).
 *
 * The login and refresh endpoints are the two requests that issue/rotate the tokens
 * themselves: `/auth/login` is unauthenticated, and `AuthService.refresh()` fires a
 * brand-new `POST /api/auth/refresh` through `HttpClient`, which RE-ENTERS this
 * interceptor. Skipping these fragments prevents an infinite refresh recursion (a
 * 401 on the refresh call must propagate, not spawn another refresh).
 *
 * Note: `/auth/me` and `/auth/logout` are deliberately NOT skipped — they require the
 * caller's identity, so they receive the Bearer header and participate in 401 -> refresh.
 */
const AUTH_SKIP_FRAGMENTS = ['/auth/login', '/auth/refresh'] as const;

/**
 * Functional HTTP interceptor that (a) attaches the JWT `Authorization: Bearer`
 * header to outgoing API requests and (b) transparently recovers from a single
 * `401 Unauthorized` by rotating tokens via {@link AuthService.refresh} and retrying
 * the original request exactly ONCE.
 *
 * Registered in `app.config.ts` via `provideHttpClient(withInterceptors([authInterceptor]))`.
 * It is a functional `HttpInterceptorFn` (not a class-based `HttpInterceptor`, and not
 * registered through the legacy `HTTP_INTERCEPTORS` DI token); functional interceptors
 * run inside an injection context, so `inject(AuthService)` is valid here.
 *
 * Recovery flow (preserve exactly):
 *  - The outer `catchError` wraps `next(authReq)`. On a `401` — and only when a refresh
 *    token exists and the URL is not skipped — it returns
 *    `refresh().pipe(switchMap(retry), catchError(clearSession + rethrow))`.
 *  - Because `switchMap` errors propagate down its own pipe, the INNER `catchError`
 *    catches BOTH a failed `refresh()` AND a failed retried request, guaranteeing the
 *    retry happens at most once before the local session is cleared. It calls
 *    `AuthService.clearSession()` (local-only) rather than `logout()` so the cleanup
 *    never issues an intercepted `/api/auth/logout` request that could re-trigger the
 *    refresh -> logout cycle (M10/DEV-038).
 *  - Unauthenticated `401`s (no refresh token) simply propagate — no futile refresh.
 *
 * MIGRATION: replaces the legacy implicit ASP.NET Forms Authentication cookie — managed
 * transparently by the browser and cleared by `PortalSecurity.SignOut()`
 * (`Library/Components/Security/PortalSecurity.vb` L77-95) — with an explicit, stateless
 * `Authorization: Bearer <accessToken>` header plus refresh-token rotation. Part of the
 * single sanctioned auth change (DEV-001); see root `MIGRATION_NOTES.md` §3.1.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  // Anti-loop: never attach a token to, or attempt a refresh for, the login/refresh
  // endpoints — doing so would recurse through this very interceptor.
  if (AUTH_SKIP_FRAGMENTS.some((fragment) => req.url.includes(fragment))) {
    return next(req);
  }

  // Attach the current access token when one is available; otherwise pass through
  // unmodified (e.g. the very first request before login completes).
  const token = authService.accessToken();
  const authReq = token ? withBearer(req, token) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      // Only attempt recovery for an actual 401 AND when a refresh token is present.
      if (error.status === 401 && authService.refreshToken() !== null) {
        return authService.refresh().pipe(
          // On success, retry the ORIGINAL request re-cloned with the NEW access token.
          switchMap((response) => next(withBearer(req, response.accessToken))),
          // Catches a failed refresh OR a failed retry. MIGRATION (M10/DEV-038): clear the
          // session LOCALLY (no server call) instead of calling logout(). logout() POSTs to
          // /api/auth/logout, which is intentionally NOT on AUTH_SKIP_FRAGMENTS and therefore
          // RE-ENTERS this interceptor; with refresh already broken, that request's own 401
          // would spawn yet another refresh -> logout attempt (a refresh/logout recursion loop).
          // clearSession() performs only the local teardown + redirect to login, ending the
          // recovery deterministically. A normal user-initiated logout still uses logout().
          catchError((refreshError: unknown) => {
            authService.clearSession();
            return throwError(() => refreshError);
          }),
        );
      }
      // Non-401, or no refresh token: propagate the original error untouched.
      return throwError(() => error);
    }),
  );
};

/**
 * Clone a request, adding (or overriding) the `Authorization: Bearer <token>` header.
 *
 * `HttpRequest` is immutable, so `clone({ setHeaders })` is the canonical way to attach
 * a header without mutating the caller's request instance.
 */
function withBearer(
  req: HttpRequest<unknown>,
  token: string,
): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
