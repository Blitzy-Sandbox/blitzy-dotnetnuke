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
 * There is deliberately NO `canActivate` guard on this area: sign-in and
 * password recovery must be reachable while the user is unauthenticated. The
 * exported constant MUST stay named exactly `AUTH_ROUTES` because the parent
 * router resolves it via `.then((m) => m.AUTH_ROUTES)`.
 *
 * MIGRATION: replaces the legacy DotNetNuke 4.x ASP.NET Web Forms login entry
 * shell (Website/Default.aspx.vb — `DefaultPage : CDefault` implementing
 * IClientAPICallbackEventHandler) and the password-reminder screen
 * (Website/admin/Security/SendPassword.ascx) with stateless Angular standalone
 * routes. ViewState, postback, and IClientAPICallbackEventHandler are eliminated
 * (AAP §0.6.3). The two feature components are standalone (the Angular 19
 * default) and are lazy-loaded so each splits into its own build chunk; this
 * file therefore intentionally contains ONLY routing configuration — no UI, no
 * business logic, and no HttpClient access.
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
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
    title: 'Forgot Password',
  },
];
