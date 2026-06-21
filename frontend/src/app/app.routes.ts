import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * Top-level (1st-tier) routing table for the DNN Migration Angular 19 SPA.
 *
 * This is the navigation backbone of the application. It is consumed by the
 * sibling `app.config.ts` through `provideRouter(routes)`; the export name
 * `routes` is therefore a hard contract with that file and must not change.
 *
 * Architecture — two-tier lazy loading (AAP §0.3.4 lazy-loading mandate):
 *   1st tier (THIS file): every feature area is attached with `loadChildren`,
 *     which dynamically `import()`s the feature's own `*.routes.ts` and returns
 *     its exported `Routes` array (`AUTH_ROUTES`, `PORTAL_ROUTES`, …). No feature
 *     route table or component is statically imported here, so none of them are
 *     pulled into the initial JavaScript bundle.
 *   2nd tier (each feature `*.routes.ts`): every screen is attached with
 *     `loadComponent`, code-splitting each standalone component into its own
 *     chunk that loads on demand.
 * The only static import below — `authGuard` — is a tiny functional
 * `CanActivateFn`, so it adds negligible weight to the initial bundle while
 * keeping route protection synchronous and declarative.
 *
 * Standalone-only / no NgModules: `loadChildren` resolves to a `Routes` array
 * (the functional, NgModule-free form). This is required by the project's
 * `strictStandalone` Angular compiler option, which forbids module-based
 * routing.
 *
 * Route protection: the public `auth` area is intentionally UNGUARDED so the
 * login page is reachable while unauthenticated. Each administrative feature
 * (`portals`, `modules`, `users`, `roles`) is gated by `canActivate: [authGuard]`,
 * which redirects unauthenticated visitors to `/auth/login` (the guard owns that
 * redirect — this table does not). There is deliberately NO `tabs` route: no
 * `tabs` frontend feature exists (AAP §0.3.1), so referencing one would break
 * the build.
 *
 * Match ordering is significant — Angular evaluates routes top-to-bottom: the
 * concrete feature paths come first, the empty-path default
 * (`pathMatch: 'full'`) lands the user on Portals, and the catch-all wildcard
 * (`**`) MUST remain the final entry.
 *
 * MIGRATION: Replaces the DNN Web Forms server-side navigation model
 * (ASP.NET `.aspx`/`.ascx` postback + ViewState, with per-request Forms
 * Authentication gating in `Library/Components/Security/PortalSecurity.vb`)
 * with a stateless client-side router. Server-side authorization is still
 * enforced by the JWT-secured API; `authGuard` only gates client navigation.
 * Recorded in the root `MIGRATION_NOTES.md`.
 */
export const routes: Routes = [
  // Public authentication area (login) — intentionally NO guard so that an
  // unauthenticated user can always reach the JWT login page.
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  // Guarded administrative feature areas (lazy-loaded child route trees).
  // `canActivate: [authGuard]` protects the whole subtree; the feature's own
  // child routes therefore do not re-declare the guard.
  {
    path: 'portals',
    canActivate: [authGuard],
    loadChildren: () => import('./features/portal/portal.routes').then((m) => m.PORTAL_ROUTES),
  },
  {
    path: 'modules',
    canActivate: [authGuard],
    loadChildren: () => import('./features/module/module.routes').then((m) => m.MODULE_ROUTES),
  },
  {
    path: 'users',
    canActivate: [authGuard],
    loadChildren: () => import('./features/user/user.routes').then((m) => m.USER_ROUTES),
  },
  {
    path: 'roles',
    canActivate: [authGuard],
    loadChildren: () => import('./features/role/role.routes').then((m) => m.ROLE_ROUTES),
  },
  // Default landing: an empty path is a full match only (so it never shadows the
  // feature paths above) and redirects to Portals. When unauthenticated, the
  // `authGuard` on `portals` takes over and redirects to the login route.
  { path: '', pathMatch: 'full', redirectTo: 'portals' },
  // Wildcard fallback — MUST be the last entry; catches any unknown URL.
  { path: '**', redirectTo: 'portals' },
];
