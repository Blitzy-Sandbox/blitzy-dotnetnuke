import { Routes } from '@angular/router';

// MIGRATION: Replaces DNN Web Forms postback navigation for the admin Portal area
// (Website/admin/Portal/Portals.ascx.vb -> portal-list; SiteSettings.ascx.vb -> portal-form/portal-settings)
// with client-side Angular lazy routes. Each screen is a 2nd-tier loadComponent for small per-screen chunks.
// Consumed by app.routes.ts: { path: 'portals', canActivate: [authGuard], loadChildren: () => import(...).then(m => m.PORTAL_ROUTES) }.
// Documented in root MIGRATION_NOTES.md.
export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    title: 'Portals',
    loadComponent: () =>
      import('./components/portal-list/portal-list.component').then((m) => m.PortalListComponent),
  },
  {
    path: 'new',
    title: 'New Portal',
    loadComponent: () =>
      import('./components/portal-form/portal-form.component').then((m) => m.PortalFormComponent),
  },
  {
    path: ':id/edit',
    title: 'Edit Portal',
    loadComponent: () =>
      import('./components/portal-form/portal-form.component').then((m) => m.PortalFormComponent),
  },
  {
    path: ':id/settings',
    title: 'Portal Settings',
    loadComponent: () =>
      import('./components/portal-settings/portal-settings.component').then((m) => m.PortalSettingsComponent),
  },
];
