// MIGRATION: Lazy route table for the Module admin feature, re-expressing the legacy DotNetNuke
// Admin -> Modules navigation (Website/admin/Modules/ModuleSettings.ascx.vb [485], Import.ascx.vb [245],
// Export.ascx.vb [227]). The legacy controls reached a module by its ModuleId query string; here the
// module id is a route parameter (:id) bound to each component's `id` input via withComponentInputBinding()
// (configured in app.config.ts). Postback/ViewState navigation is discarded. No module-list route exists
// per AAP 0.4.2 (only the module-settings and import/export workflows are mapped).
import type { Routes } from '@angular/router';

export const MODULE_ROUTES: Routes = [
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
