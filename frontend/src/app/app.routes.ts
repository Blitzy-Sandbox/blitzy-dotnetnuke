import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * routes - TOP-LEVEL (1st-tier) routing table for the Angular 19 standalone SPA.
 *
 * This is the navigation backbone of the application. It is consumed by the
 * sibling `app.config.ts` via `provideRouter(routes)`; the export name `routes`
 * is therefore a HARD CONTRACT (`import { routes } from './app.routes'`) and MUST
 * NOT be renamed (AAP 0.3.1, 0.3.4).
 *
 * Two-tier lazy loading (AAP 0.3.4):
 *   - Every feature area is mounted here with `loadChildren`, which lazily imports
 *     that feature's own `*.routes.ts` route table (`AUTH_ROUTES`, `PORTAL_ROUTES`,
 *     `MODULE_ROUTES`, `USER_ROUTES`, `ROLE_ROUTES`).
 *   - Each of those feature tables then uses `loadComponent` to lazily import its
 *     individual standalone screen components.
 *   This keeps the initial JavaScript bundle minimal (one small on-demand chunk per
 *   feature, and a further chunk per screen) and satisfies the angular.json budgets
 *   (1mb warn / 2mb error). Only `authGuard` - a tiny functional `CanActivateFn` -
 *   is statically imported; no feature component is ever eagerly imported here.
 *
 * Authorization model:
 *   - `auth` is PUBLIC and intentionally carries NO `canActivate`, so the login page
 *     is reachable while unauthenticated.
 *   - `portals`, `modules`, `users`, and `roles` are each gated by
 *     `canActivate: [authGuard]`. The guard (see `core/auth/auth.guard.ts`) allows
 *     authenticated navigation through and otherwise returns a `UrlTree` redirecting
 *     to `/auth/login`. Redirect-to-login is owned by the guard, NOT by this file.
 *
 * Route-order correctness:
 *   - Concrete feature paths are declared first.
 *   - The empty-path default `{ path: '', pathMatch: 'full', redirectTo: 'portals' }`
 *     lands the user on the Portals area (per docs/technical-specifications.md L680).
 *     `pathMatch: 'full'` is required so the redirect only fires for the exact empty
 *     URL and does not shadow the feature routes.
 *   - The wildcard `{ path: '**', redirectTo: 'portals' }` MUST remain the LAST entry
 *     so it only catches genuinely unknown URLs.
 *
 * Intentional omission - there is NO `tabs` route: the frontend has no `tabs` feature
 * folder (only portal/module/user/role/auth exist per AAP 0.3.1). The backend exposes
 * a TabsController and the sidebar lists "Tabs", but adding a `tabs` route here would
 * reference a non-existent `./features/tab/...` module and break the build.
 *
 * MIGRATION: The legacy DNN Web Forms application had no client-side router -
 * navigation was driven by full-page postbacks / ViewState across `.aspx`/`.ascx`
 * pages under `Website/admin/`. Client-side SPA routing is a new Angular concept with
 * no one-to-one legacy equivalent; this table reproduces the in-scope admin navigation
 * (Portals, Modules, Users, Roles) plus a new JWT login area. Recorded in the root
 * `MIGRATION_NOTES.md`.
 */
export const routes: Routes = [
  // Public authentication area (login) - NO guard so it is reachable while unauthenticated.
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  // Guarded administrative feature areas (lazy-loaded child route trees).
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
  // Default landing + wildcard fallback (wildcard MUST stay last).
  { path: '', pathMatch: 'full', redirectTo: 'portals' },
  { path: '**', redirectTo: 'portals' },
];
