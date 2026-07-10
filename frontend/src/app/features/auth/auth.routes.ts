import { Routes } from '@angular/router';

/**
 * PUBLIC authentication feature routes (no route guard).
 *
 * Lazy-loaded child route table for the `/auth` area of the `dnn-migration`
 * Angular 19 SPA. The parent router (frontend/src/app/app.routes.ts) mounts this
 * table with:
 *
 *   {
 *     path: 'auth',
 *     loadChildren: () =>
 *       import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
 *   }
 *
 * There is deliberately NO `canActivate` guard on this area: sign-in must be
 * reachable while the user is unauthenticated. The exported constant MUST stay
 * named exactly `AUTH_ROUTES` because the parent router resolves it via
 * `.then((m) => m.AUTH_ROUTES)`.
 *
 * MIGRATION: replaces the legacy DotNetNuke 4.x ASP.NET Web Forms login entry
 * shell (Website/Default.aspx.vb — `DefaultPage : CDefault` implementing
 * IClientAPICallbackEventHandler) with a stateless Angular standalone route.
 * ViewState, postback, and IClientAPICallbackEventHandler are eliminated
 * (AAP §0.6.3). The login component is standalone (the Angular 19 default) and
 * is lazy-loaded so it splits into its own build chunk; this file therefore
 * intentionally contains ONLY routing configuration — no UI, no business logic,
 * and no HttpClient access.
 *
 * The legacy password-reminder screen (Website/admin/Security/SendPassword.ascx)
 * is intentionally NOT migrated: the authentication surface is limited to
 * login/refresh/logout/me (AAP §0.3.1), and the provider-based MembershipProvider
 * password-recovery path is explicitly out of scope (AAP §0.2.2). No placeholder
 * route or component is retained for it (no-stub rule).
 */
export const AUTH_ROUTES: Routes = [
  // Landing on `/auth` forwards to `/auth/login`. `pathMatch: 'full'` is
  // required so the empty path matches ONLY the exact `/auth` URL rather than
  // acting as a prefix for every child route.
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () =>
      import('./login/login.component').then((m) => m.LoginComponent),
    title: 'Sign In',
  },
];
