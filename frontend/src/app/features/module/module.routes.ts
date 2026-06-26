// MIGRATION: Lazy route table for the Module admin feature, re-expressing the legacy DotNetNuke
// Admin -> Modules navigation (Website/admin/Modules/ModuleSettings.ascx.vb [485], Import.ascx.vb [245],
// Export.ascx.vb [227]). The legacy controls reached a module by its ModuleId query string; here the
// module id is a route parameter (:id) bound to each component's `id` input via withComponentInputBinding()
// (configured in app.config.ts). Postback/ViewState navigation is discarded. No module-list route exists
// per AAP 0.4.2 (only the module-settings and import/export workflows are mapped).
import type { Routes } from '@angular/router';

export const MODULE_ROUTES: Routes = [
  // MIGRATION: [CP4 review — Frontend Routing] Defense-in-depth empty-path guard. Per AAP 0.4.2 there is NO
  // module-list landing page, and the in-app entry points always carry a module :id (e.g. ['/modules', id,
  // 'settings'] from import/export). A bare /modules navigation therefore had no matching child and left the
  // router outlet blank; redirect the empty path to the portals workspace (a safe existing route) so /modules
  // never dead-ends. The sidebar's standalone /modules nav item is also removed (no advertised contentless route).
  {
    path: '',
    redirectTo: '/portals',
    pathMatch: 'full',
  },
  {
    path: ':id/settings',
    loadComponent: () =>
      import('./module-form/module-form.component').then((m) => m.ModuleFormComponent),
    title: 'Module Settings',
  },
  {
    path: ':id/import-export',
    loadComponent: () =>
      import('./import-export/import-export.component').then((m) => m.ImportExportComponent),
    title: 'Module Import / Export',
  },
  // MIGRATION: Angular Router substitutes the matched :id into the redirect target, so /modules/:id
  // lands on the settings workflow (the legacy default when opening a module's admin context).
  {
    path: ':id',
    redirectTo: ':id/settings',
    pathMatch: 'full',
  },
];
