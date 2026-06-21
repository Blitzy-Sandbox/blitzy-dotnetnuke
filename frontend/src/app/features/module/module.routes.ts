import { Routes } from '@angular/router';

/**
 * MODULE_ROUTES — 2nd-tier lazy route table for the Module administration feature.
 *
 * Lazy-loaded by the top-level app.routes.ts at path 'modules' via
 * `loadChildren: () => import('./features/module/module.routes').then((m) => m.MODULE_ROUTES)`.
 * Route protection (the parent route's `canActivate` auth guard) is declared on the
 * PARENT 'modules' route, so no guard is re-applied here. Every screen is independently
 * code-split through its own 2nd-tier `loadComponent`, keeping the initial bundle minimal.
 *
 * Route map (AAP §0.6.4 standard ASPX→Angular mapping):
 *   ''                   → ModuleListComponent     (grid; GET /api/v1/modules — soft-deleted excluded)
 *   'new'                → ModuleFormComponent     (create mode; POST /api/v1/modules)
 *   ':moduleId/edit'     → ModuleFormComponent     (edit mode; PUT /api/v1/modules/{id})
 *   ':moduleId/settings' → ModuleSettingsComponent (config editor from ModuleSettings.ascx.vb)
 *
 * The `:moduleId` route parameter name is shared by the edit and settings routes
 * for consistency; the form component reads it to decide create vs. edit mode.
 */

// MIGRATION: Web Forms postback navigation (Website/admin/Modules/*.ascx) replaced by
// client-side Angular lazy routes; each screen is code-split via 2nd-tier loadComponent.
export const MODULE_ROUTES: Routes = [
  {
    path: '',
    title: 'Modules',
    loadComponent: () =>
      import('./components/module-list/module-list.component').then(
        (m) => m.ModuleListComponent,
      ),
  },
  {
    path: 'new',
    title: 'New Module',
    loadComponent: () =>
      import('./components/module-form/module-form.component').then(
        (m) => m.ModuleFormComponent,
      ),
  },
  {
    path: ':moduleId/edit',
    title: 'Edit Module',
    loadComponent: () =>
      import('./components/module-form/module-form.component').then(
        (m) => m.ModuleFormComponent,
      ),
  },
  {
    path: ':moduleId/settings',
    title: 'Module Settings',
    loadComponent: () =>
      import('./components/module-settings/module-settings.component').then(
        (m) => m.ModuleSettingsComponent,
      ),
  },
];
