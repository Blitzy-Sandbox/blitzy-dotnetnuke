import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * authGuard — protects the authenticated feature areas (portals, modules, users, roles).
 *
 * MIGRATION: replaces the DotNetNuke `SecurityAccessLevel` gating
 * (Anonymous/View/Edit/Admin/Host) that `Library/Components/Security/PortalSecurity.vb`
 * enforced server-side through `HasNecessaryPermission(...)` during the Web Forms page
 * lifecycle. In the stateless SPA there is no per-request server page to gate, so route
 * access is decided on the client from the presence of a valid JWT session: a
 * missing/expired session (`isAuthenticated() === false`) redirects to the public
 * `/auth` route, preserving the attempted URL as `returnUrl` so the login flow can
 * navigate back afterwards (functional parity with the legacy login-then-return flow).
 *
 * Angular 19 functional guard (`CanActivateFn`) — no class-based `CanActivate`/
 * `@Injectable` guard. Dependencies are obtained via `inject()` because a functional
 * guard is executed by the router inside an injection context.
 *
 * The sibling `app.routes.ts` imports this `authGuard` and applies
 * `canActivate: [authGuard]` to the protected areas; the public `auth` route is left
 * unguarded. The guard — not the route table — owns the unauthenticated redirect.
 */
export const authGuard: CanActivateFn = (_route, state): boolean | UrlTree => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // `isAuthenticated` is a read-only signal; invoking it reads the current value
  // synchronously, so no async/Observable handling is required here.
  if (authService.isAuthenticated()) {
    return true;
  }

  // Returning a `UrlTree` is the idiomatic Angular guard redirect: it cancels the
  // in-flight navigation and routes to `/auth` atomically, avoiding the race that an
  // imperative `router.navigate()` inside a guard can introduce. `returnUrl` carries
  // the originally requested URL for post-login navigation.
  return router.createUrlTree(['/auth'], {
    queryParams: { returnUrl: state.url },
  });
};
