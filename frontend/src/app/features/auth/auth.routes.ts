import { Routes } from '@angular/router';

/**
 * AUTH_ROUTES — 2nd-tier lazy route table for the PUBLIC authentication feature.
 *
 * Lazy-loaded by `app.routes.ts` at path `'auth'` (via `loadChildren`) and
 * intentionally UNGUARDED so unauthenticated users can reach the login page.
 * The `login` route resolves to `/auth/login` — the canonical path that
 * `core/auth/auth.guard.ts` redirects to (`createUrlTree(['/auth/login'])`)
 * and `core/auth/auth.service.ts` `logout()` navigates to
 * (`navigate(['/auth/login'])`). The `login` path segment MUST stay exact.
 *
 * Two-tier lazy loading keeps the login UI in its own on-demand chunk:
 * `app.routes.ts` `loadChildren` → this table's `loadComponent` (AAP §0.3.4).
 * No guard, no eager `component:`, and no static import of `LoginComponent`
 * (a static import would defeat lazy chunking / bundle budgets).
 *
 * MIGRATION: This route exposes the NEW JWT login page that replaces the legacy
 * ASP.NET Forms Authentication login flow
 * (Library/Components/Security/PortalSecurity.vb: FormsAuthentication.SignOut L79,
 * DES Encrypt/Decrypt L138-211). JWT Bearer + BCrypt is the single sanctioned
 * behavior change of the migration (AAP §0.6.2). Recorded in root MIGRATION_NOTES.md.
 */
export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    // QA #10: route title so the default TitleStrategy sets document.title to the
    // login screen (previously left as the generic app title).
    title: 'Sign in',
    loadComponent: () =>
      import('./login/login.component').then((m) => m.LoginComponent),
  },
  // Bare /auth -> /auth/login for graceful entry.
  { path: '', pathMatch: 'full', redirectTo: 'login' },
];
