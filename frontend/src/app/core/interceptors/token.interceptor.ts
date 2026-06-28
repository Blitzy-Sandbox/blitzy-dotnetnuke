// MIGRATION: Net-new functional JWT Bearer token interceptor. There is NO direct legacy analog -- it replaces the
// DNN ASP.NET 2.0 Forms-authentication cookie machinery (Website/release.config <authentication mode="Forms"> +
// AspNetSqlMembershipProvider, and PortalSecurity.vb SignOut()/sign-in which relied on the browser auto-sending the
// FormsAuthentication cookie). Outbound API requests now carry an explicit JWT instead of an auth cookie.
// MIGRATION: chosen token strategy = "Authorization: Bearer" header sourced from the MEMORY-ONLY access-token signal
// exposed by core/auth/AuthService (AAP Section 0.7.6 permits memory-only storage; AuthService deliberately holds the
// access token in a JS-readable signal for exactly this purpose). The httpOnly-cookie alternative (AAP Section 0.7.6)
// would instead set { withCredentials: true } and omit this header; that path is intentionally NOT taken here.
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

// MIGRATION: auth-establishment endpoints that must NOT carry a (possibly stale/expired) in-memory access token.
// `/auth/login` and `/auth/forgot-password` are AllowAnonymous credential/identity-recovery flows, and `/auth/refresh`
// authenticates with the REFRESH token (request body / httpOnly cookie) rather than the access token. Attaching a
// stale `Authorization: Bearer` header to these is incorrect and could cause the server to evaluate the wrong
// credential. `/auth/me` (current-user identity) and `/auth/logout` (session-scoped) intentionally KEEP the token.
const TOKEN_EXEMPT_AUTH_PATHS = ['/auth/login', '/auth/refresh', '/auth/forgot-password'] as const;

/**
 * MIGRATION: returns true when the outbound request targets an auth-establishment endpoint that must be sent
 * WITHOUT the Authorization header. The query string / fragment are stripped first, and an exact path-suffix match
 * is used so that, e.g., `/auth/login` does NOT also match a hypothetical `/auth/login-history`.
 */
function isTokenExemptAuthEndpoint(url: string): boolean {
  const path = url.split('?')[0].split('#')[0];
  return TOKEN_EXEMPT_AUTH_PATHS.some((endpoint) => path.endsWith(endpoint));
}

export const tokenInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const accessToken = authService.accessToken();

  // MIGRATION: scope the bearer token to the API origin ONLY (environment.apiUrl) so it is never leaked to a
  // third-party host. When unauthenticated (no access token in memory) the request is passed through untouched.
  // MIGRATION (Issue 10): auth-establishment endpoints (login/refresh/forgot-password) are explicitly excluded so a
  // stale in-memory token is never attached to them; all other API calls (incl. /auth/me, /auth/logout) still carry it.
  if (
    accessToken !== null &&
    req.url.startsWith(environment.apiUrl) &&
    !isTokenExemptAuthEndpoint(req.url)
  ) {
    return next(
      req.clone({
        setHeaders: { Authorization: `Bearer ${accessToken}` },
      }),
    );
  }

  return next(req);
};
