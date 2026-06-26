// MIGRATION: PUBLIC authentication route table for the migrated DotNetNuke auth workflows. Re-expresses
// the legacy Forms-auth login (Website/admin/Authentication/Login.ascx.vb; orchestration in
// Library/Components/Security/PortalSecurity.vb and Library/Components/Users/Membership/UserMembership.vb)
// and the password-reminder workflow (Website/admin/Security/SendPassword.ascx.vb) as lazy-loaded
// Angular 19 standalone routes. This is the ONLY feature area mounted WITHOUT authGuard (PUBLIC): the
// login + forgot-password screens must be reachable by unauthenticated users. The legacy ViewState/postback
// machinery is discarded; only navigation is modeled here.
import { Routes } from '@angular/router';

/**
 * Child routes for the `auth` feature, lazy-loaded by the root router at the `auth` path
 * (`app.routes.ts` → `loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES)`).
 *
 * Resolved absolute URLs:
 *   /auth            → redirects to /auth/login
 *   /auth/login      → LoginComponent          (authGuard redirect target — MUST resolve)
 *   /auth/forgot-password → ForgotPasswordComponent
 */
export const AUTH_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
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
