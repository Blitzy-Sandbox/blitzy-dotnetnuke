import { Routes } from '@angular/router';

/**
 * PORTAL_ROUTES — 2nd-tier lazy route table for the Portal (site) administration feature.
 *
 * Consumed by the top-level `app.routes.ts`, which lazy-loads this table at path
 * `'portals'` and applies the feature guard there:
 *
 *   {
 *     path: 'portals',
 *     canActivate: [authGuard],
 *     loadChildren: () => import('./features/portal/portal.routes').then((m) => m.PORTAL_ROUTES),
 *   }
 *
 * Because the parent route already applies `authGuard`, this table intentionally
 * declares NO `canActivate` (re-guarding here would be redundant and the
 * `authGuard` import would be unused — a Gate 3 warning-as-error). Each screen is
 * a separate `loadComponent` dynamic import so the Angular esbuild toolchain emits
 * one small on-demand chunk per screen (AAP §0.3.4 small-chunk budgets). Every
 * route carries a `title` that the default `TitleStrategy` writes to
 * `document.title` (browser-tab UX + screen-reader page announcement).
 *
 * Route table:
 *   ''             -> PortalListComponent     (host portals grid)        title 'Portals'
 *   'new'          -> PortalFormComponent     (create portal form)       title 'New Portal'
 *   ':id/edit'     -> PortalFormComponent     (edit portal form)         title 'Edit Portal'
 *   ':id/settings' -> PortalSettingsComponent (portal configuration)     title 'Portal Settings'
 *
 * The single-segment literal `'new'` is listed before the two-segment
 * `':id/edit'` / `':id/settings'` routes (the safe ordering convention); there is
 * no bare `':id'` route, so `'new'` can never be captured as an id.
 *
 * MIGRATION: Replaces DNN Web Forms postback navigation for the admin Portal area
 * (Website/admin/Portal/Portals.ascx.vb -> portal-list; SiteSettings.ascx.vb ->
 * portal-form / portal-settings) with client-side Angular lazy routes. Routing is
 * a new Angular-SPA concept with no legacy equivalent. Recorded in root
 * MIGRATION_NOTES.md.
 */
export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    title: 'Portals',
    loadComponent: () =>
      import('./components/portal-list/portal-list.component').then(
        (m) => m.PortalListComponent,
      ),
  },
  {
    path: 'new',
    title: 'New Portal',
    loadComponent: () =>
      import('./components/portal-form/portal-form.component').then(
        (m) => m.PortalFormComponent,
      ),
  },
  {
    path: ':id/edit',
    title: 'Edit Portal',
    loadComponent: () =>
      import('./components/portal-form/portal-form.component').then(
        (m) => m.PortalFormComponent,
      ),
  },
  {
    path: ':id/settings',
    title: 'Portal Settings',
    loadComponent: () =>
      import('./components/portal-settings/portal-settings.component').then(
        (m) => m.PortalSettingsComponent,
      ),
  },
];
