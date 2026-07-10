import { Routes } from '@angular/router';

/**
 * Portal (site) management feature routes. Lazy-loaded by the root router at
 * path `portals` (guarded by authGuard in app.routes.ts — do NOT re-guard here).
 *
 * MIGRATION: replaces the legacy DNN Web Forms admin portal screens under
 * Website/admin/Portal/** — Portals.ascx (list), Signup.ascx (create) and
 * SiteSettings.ascx (edit) — as stateless standalone components reached by URL.
 */
export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./portal-list/portal-list.component').then((m) => m.PortalListComponent),
    title: 'Portals',
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./portal-form/portal-form.component').then((m) => m.PortalFormComponent),
    title: 'Create Portal',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./portal-form/portal-form.component').then((m) => m.PortalFormComponent),
    title: 'Edit Portal',
  },
];
