// MIGRATION: top-level route table for the SPA. Replaces the legacy DNN tab/URL routing
// (TabController + FriendlyUrlProvider + Website/web.config <urlMappings>, where each admin .ascx was
// reached through a portal "tab"/page) with Angular lazy feature routes (AAP Section 0.3.6). The root
// AppComponent is the fixed application shell (header + sidebar + footer around <router-outlet/>), so
// these are FLAT feature routes that render into that single outlet -- no separate layout-wrapper route.
import { type Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * The application route table consumed by `provideRouter` in `app.config.ts`.
 *
 * MIGRATION: feature folders are lazy-loaded via `loadChildren` (AAP Section 0.3.6 -- lazy-loaded routes,
 * AAP Section 0.7.7 -- performance). The `auth` feature is PUBLIC (login / forgot-password must be
 * reachable while unauthenticated); the portals / users / roles / modules features are protected by
 * `authGuard`, which redirects unauthenticated users to `/auth/login` with a `returnUrl` (replaces the
 * legacy ASP.NET Forms-authentication redirect to the portal Login tab).
 */
export const routes: Routes = [
  // MIGRATION: default landing -> the portals workspace. When unauthenticated, authGuard on the
  // 'portals' route redirects to '/auth/login' (so no separate unauthenticated landing route is needed).
  { path: '', redirectTo: 'portals', pathMatch: 'full' },

  // MIGRATION: PUBLIC auth feature (login + forgot-password). NO authGuard -- these screens are the
  // entry point for unauthenticated users (legacy portal Login tab / SendPassword control).
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },

  // MIGRATION: HOST-level portal administration (Website/admin/Portal/**). Protected by authGuard; the
  // host-only UI gating is enforced inside the feature components (defense-in-depth).
  {
    path: 'portals',
    canActivate: [authGuard],
    loadChildren: () => import('./features/portal/portal.routes').then((m) => m.PORTAL_ROUTES),
  },

  // MIGRATION: portal-administrator user management (Website/admin/Users/**). Tenant-scoped server-side.
  {
    path: 'users',
    canActivate: [authGuard],
    loadChildren: () => import('./features/user/user.routes').then((m) => m.USER_ROUTES),
  },

  // MIGRATION: portal-administrator security-role management (Website/admin/Security/**). Tenant-scoped.
  {
    path: 'roles',
    canActivate: [authGuard],
    loadChildren: () => import('./features/role/role.routes').then((m) => m.ROLE_ROUTES),
  },

  // MIGRATION: portal-administrator module management (Website/admin/Modules/**). Tenant-scoped.
  {
    path: 'modules',
    canActivate: [authGuard],
    loadChildren: () => import('./features/module/module.routes').then((m) => m.MODULE_ROUTES),
  },

  // MIGRATION: unknown URLs fall back to the default workspace (legacy DNN served the portal home tab
  // for unmapped requests). authGuard on 'portals' still applies after the redirect.
  { path: '**', redirectTo: 'portals' },
];
