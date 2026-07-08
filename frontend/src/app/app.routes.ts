import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

// MIGRATION: Legacy DNN navigation (tab/breadcrumb + admin .ascx screens under
// Website/admin/{Portal,Users,Modules,Security}) is re-expressed as lazy-loaded Angular
// routes. Postback/ViewState/IClientAPICallbackEventHandler (Website/Default.aspx.vb) is
// eliminated in favor of stateless client-side routing.
export const routes: Routes = [
  { path: '', redirectTo: 'portals', pathMatch: 'full' },

  // Public — reachable without authentication (login + password recovery)
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },

  // Protected admin areas — require a valid JWT (authGuard)
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

  { path: '**', redirectTo: 'portals' },
];
