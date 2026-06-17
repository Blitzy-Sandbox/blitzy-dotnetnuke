import { inject } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';

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
 * The attempted destination is captured from `RouterStateSnapshot.url` and
 * appended to the login redirect as a `returnUrl` query parameter, so that after
 * a successful authentication the login component can restore the originally
 * requested deep link. The `returnUrl` CONSUMER already exists and is unit-tested
 * (`login.component.ts` `resolveReturnUrl()` reads `returnUrl` from the query map
 * and falls back to `/portals`); this guard is the PRODUCER that emits the param.
 * The `route` parameter is not inspected — this guard makes a global
 * authenticated/anonymous decision — but it is declared so the `state` argument
 * (required for `returnUrl` capture) can be accessed under the `CanActivateFn`
 * signature.
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
export const authGuard: CanActivateFn = (
  _route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
): boolean | UrlTree => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Capture the attempted URL so the login flow can redirect back after auth.
  // The consumer (`login.component.ts` `resolveReturnUrl()`) reads this param and
  // falls back to `/portals` when it is absent.
  return router.createUrlTree(['/auth/login'], {
    queryParams: { returnUrl: state.url },
  });
};
