import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Functional route guard protecting the administrative feature areas
 * (portals / modules / users / roles). Applied in `app.routes.ts` as
 * `canActivate: [authGuard]` on the lazy-loaded feature routes.
 *
 * Authenticated requests are allowed through (`true`); unauthenticated requests
 * are both cancelled AND redirected to the JWT login route by returning a
 * `UrlTree` (which is cleaner than returning `false` plus a manual
 * `Router.navigate`). Authentication state is read from the signal-based
 * {@link AuthService} via its `isAuthenticated()` computed signal, so the guard
 * re-evaluates correctly even after a hard reload rehydrates the session.
 *
 * The `route`/`state` parameters of `CanActivateFn` are intentionally omitted:
 * this guard makes a global authenticated/anonymous decision and does not
 * inspect the activated route or router state. A zero-argument arrow function is
 * assignable to `CanActivateFn`, and omitting the unused parameters keeps the
 * implementation clean under strict TypeScript.
 *
 * MIGRATION: replaces the legacy server-side ASP.NET Forms Authentication request
 * gating from `Library/Components/Security/PortalSecurity.vb`
 * (`HttpContext.Current.Request.IsAuthenticated` checks plus
 * `PortalSecurity.IsInRole` / `IsInRoles`, L103-136, and the cookie-based session
 * established alongside `SignOut`, L77-95) with a client-side functional
 * `CanActivateFn`. Authoritative authorization still lives server-side via JWT
 * validation and ASP.NET Core policies; this guard only gates client navigation.
 * See the root `MIGRATION_NOTES.md` (Deviation D-001: Forms Authentication ->
 * stateless JWT Bearer) for the recorded deviation.
 */
export const authGuard: CanActivateFn = (): boolean | UrlTree => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/auth/login']);
};
