import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import { AuthService } from './auth.service';

/**
 * Auth-flow endpoints manage their own credentials and MUST be excluded from
 * bearer attachment + silent refresh. Excluding /auth/refresh is the critical
 * loop guard: a 401 from the refresh call must NOT trigger another refresh.
 * (/auth/me is intentionally NOT listed — it needs the token and may refresh.)
 *
 * MIGRATION: the legacy DotNetNuke Forms Authentication sign-in / renewal /
 * sign-out cookie endpoints (Library/Components/Security/PortalSecurity.vb,
 * FormsAuthentication.SignOut) are superseded by these stateless JWT auth-flow
 * routes; they exchange credentials/tokens directly and therefore never carry a
 * bearer header nor participate in the silent-refresh retry.
 */
const AUTH_FLOW_PATHS = ['/auth/login', '/auth/refresh', '/auth/logout'];

/**
 * True when the outgoing request targets one of the auth-flow endpoints above.
 * A substring match on the URL is used so it holds for both the absolute dev URL
 * (`http://localhost:8080/api/auth/refresh`) and the prod-relative URL
 * (`/api/auth/refresh`) produced by ApiService.buildUrl().
 */
function isAuthFlowRequest(url: string): boolean {
  return AUTH_FLOW_PATHS.some((path) => url.includes(path));
}

/**
 * Return a clone of {@link req} carrying `Authorization: Bearer <token>` when a
 * token is present; otherwise return the request unchanged. Kept as a typed
 * helper (over `HttpRequest<unknown>`) so both the initial attach and the
 * post-refresh retry share one implementation.
 */
function withBearer(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;
}

/**
 * authInterceptor — centralizes JWT bearer attachment and 401 silent-refresh.
 *
 * MIGRATION: replaces the DotNetNuke Forms Authentication cookie/session machinery
 * (Library/Components/Security/PortalSecurity.vb). The legacy FormsAuthentication
 * cookie + DES-encrypted session (SecurityAccessLevel Anonymous/View/Edit/Admin/Host)
 * and its server-side auth-cookie renewal are replaced by an in-memory JWT bearer
 * flow: (a) attaches `Authorization: Bearer <token>` to outgoing requests when an
 * in-memory access token exists (auth-flow endpoints are skipped). (b) On HTTP 401,
 * performs ONE silent refresh via AuthService.refresh() then retries the original
 * request with the new token; if refresh fails, the session is cleared and the user
 * is redirected to /auth. Token attachment/refresh is centralized ONLY here (feature
 * services never attach tokens — AAP §0.7.1). Angular 19 functional interceptor.
 */
export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  const authService = inject(AuthService);

  // Loop guard: never attach a token to / refresh on the auth-flow endpoints.
  if (isAuthFlowRequest(req.url)) {
    return next(req);
  }

  const authReq = withBearer(req, authService.getAccessToken());

  return next(authReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        // Attempt a single silent refresh, then retry the original request once.
        // The retry re-clones the ORIGINAL req with the NEW token (not the stale
        // authReq); AuthService.refresh()'s internal tap has already stored the
        // refreshed token, so getAccessToken() here returns it. The retry runs
        // inside switchMap (NOT re-wrapped by this catchError), so a subsequent
        // 401 cannot re-enter the refresh path — refresh + retry happen at most once.
        return authService.refresh().pipe(
          switchMap(() => next(withBearer(req, authService.getAccessToken()))),
          catchError((refreshError: unknown) => {
            // Refresh failed — clear session + redirect to /auth, then bubble the error.
            authService.logout();
            return throwError(() => refreshError);
          })
        );
      }
      return throwError(() => error);
    })
  );
};
