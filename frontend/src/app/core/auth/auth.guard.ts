import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Functional route guard protecting the administrative feature areas
 * (portals / modules / users / roles). Applied in `app.routes.ts` as
 * `canActivate: [authGuard]` on each protected feature route.
 *
 * As a functional `CanActivateFn` it runs inside an Angular injection context,
 * so it resolves its collaborators with `inject()` rather than constructor DI.
 * Authentication state is read from the {@link AuthService.isAuthenticated}
 * signal (true whenever a JWT access token is present).
 *
 * Return contract:
 *  - Authenticated  -> `true` (navigation proceeds).
 *  - Unauthenticated -> a {@link UrlTree} for `/auth/login`. Returning a
 *    `UrlTree` both cancels the in-flight navigation AND redirects in a single
 *    step, which is cleaner and race-free compared to returning `false` and
 *    imperatively calling `router.navigate()`.
 *
 * The `route` / `state` parameters of `CanActivateFn` are intentionally omitted
 * because this guard performs a purely authentication-based check that does not
 * depend on the target route. A zero-argument arrow remains assignable to
 * `CanActivateFn`, and omitting the parameters avoids `noUnusedParameters`
 * (TS6133) diagnostics under the project's strict TypeScript configuration.
 *
 * MIGRATION: Replaces the legacy server-side ASP.NET Forms Authentication
 * request gating from `Library/Components/Security/PortalSecurity.vb` (the
 * `HttpContext.Current.Request.IsAuthenticated` / `PortalSecurity.IsInRole`
 * checks that ran per request before serving a protected `.aspx`/`.ascx`) with
 * a client-side functional `CanActivateFn`. Authoritative authorization is
 * still enforced server-side by the JWT-secured API; this guard only gates
 * client navigation and redirects unauthenticated users to the JWT login route.
 * This deviation is recorded in the root `MIGRATION_NOTES.md` (§3.4).
 */
export const authGuard: CanActivateFn = (): boolean | UrlTree => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/auth/login']);
};
