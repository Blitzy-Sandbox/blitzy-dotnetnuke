import { Routes } from '@angular/router';

/**
 * AUTH_ROUTES — 2nd-tier lazy route table for the PUBLIC authentication feature.
 *
 * Lazy-loaded by app.routes.ts at path 'auth' (loadChildren) and intentionally
 * UNGUARDED so unauthenticated users can reach the login page. The `login` route
 * resolves to /auth/login — the canonical path that core/auth/auth.guard.ts
 * redirects to and core/auth/auth.service.ts logout() navigates to.
 *
 * MIGRATION: This route exposes the NEW JWT login page that replaces the legacy
 * ASP.NET Forms Authentication login flow (Library/Components/Security/PortalSecurity.vb:
 * FormsAuthentication.SignOut L79, DES Encrypt/Decrypt L138-211). JWT Bearer + BCrypt is
 * the single sanctioned behavior change of the migration (AAP §0.6.2). Recorded in
 * root MIGRATION_NOTES.md.
 */
export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    title: 'Sign In',
    loadComponent: () =>
      import('./login/login.component').then((m) => m.LoginComponent),
  },
  // Bare /auth -> /auth/login for graceful entry.
  { path: '', pathMatch: 'full', redirectTo: 'login' },
];
