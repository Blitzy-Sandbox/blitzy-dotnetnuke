// MIGRATION: Replaces the legacy ASP.NET Forms-authentication redirect (unauthenticated requests were sent to
// the portal Login tab) and the PortalSecurity.IsInRoles membership/role checks. This BASE guard verifies the
// AUTHENTICATED state only; role-gating (legacy IsInRoles) is intentionally out of base scope -- CurrentUser.roles
// is available for a future role-aware guard. Functional CanActivateFn (Angular 19), not a class-based guard.
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // MIGRATION: redirect unauthenticated users to the Angular '/auth/login' route, preserving the attempted URL
  // as a returnUrl query parameter (the auth route is intentionally left unguarded in app.routes.ts).
  return router.createUrlTree(['/auth/login'], {
    queryParams: { returnUrl: state.url },
  });
};
