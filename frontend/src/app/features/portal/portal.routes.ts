// MIGRATION: Website/admin/Portal/Portals.ascx.vb (host portal grid -> Site Settings "edit" navigation, pid=KEYFIELD)
// + SiteSettings.ascx.vb (pid querystring => edit-an-existing-portal vs. new) -> Angular 19 lazy feature routes.
// Web Forms postback + DNN Tab/NavigateURL navigation is discarded; the SPA Router drives navigation. Mounted at
// 'portals' and guarded by authGuard in app.routes.ts (do not re-declare here). withComponentInputBinding() binds
// the :id param to each component's `id` input().
import { Routes } from '@angular/router';

export const PORTAL_ROUTES: Routes = [
  {
    // List = feature default (← Portals.ascx.vb grdPortals host portal grid).
    path: '',
    loadComponent: () =>
      import('./portal-list/portal-list.component').then(
        (m) => m.PortalListComponent,
      ),
  },
  {
    // Create mode (← SiteSettings.ascx.vb with no pid). MUST precede ':id' so '/portals/new' is not captured as an id.
    path: 'new',
    loadComponent: () =>
      import('./portal-form/portal-form.component').then(
        (m) => m.PortalFormComponent,
      ),
  },
  {
    // Read-only detail view. ':id' binds to PortalDetailComponent's `id` input via withComponentInputBinding().
    path: ':id',
    loadComponent: () =>
      import('./portal-detail/portal-detail.component').then(
        (m) => m.PortalDetailComponent,
      ),
  },
  {
    // Edit mode (← SiteSettings.ascx.vb pid querystring). ':id' binds to PortalFormComponent's `id` input.
    path: ':id/edit',
    loadComponent: () =>
      import('./portal-form/portal-form.component').then(
        (m) => m.PortalFormComponent,
      ),
  },
];
