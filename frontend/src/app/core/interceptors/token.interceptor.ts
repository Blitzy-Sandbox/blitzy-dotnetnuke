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

export const tokenInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const accessToken = authService.accessToken();

  // MIGRATION: scope the bearer token to the API origin ONLY (environment.apiUrl) so it is never leaked to a
  // third-party host. When unauthenticated (no access token in memory) the request is passed through untouched.
  if (accessToken !== null && req.url.startsWith(environment.apiUrl)) {
    return next(
      req.clone({
        setHeaders: { Authorization: `Bearer ${accessToken}` },
      }),
    );
  }

  return next(req);
};
