import { Routes } from '@angular/router';

/**
 * MODULE_ROUTES — 2nd-tier lazy route table for the Module administration feature.
 *
 * Lazy-loaded by the top-level `app.routes.ts` at path `'modules'` (via `loadChildren`),
 * where the parent route also attaches the application's authentication guard through its
 * own `canActivate`. That guard is applied ONCE on the PARENT `'modules'` route, so these
 * children declare NO guard here (re-applying it would be redundant). The exported symbol
 * name is a HARD CONTRACT consumed by the parent router and MUST NOT be renamed
 * (AAP §0.3.1, §0.3.4, §0.4.1).
 *
 * Two-tier lazy loading keeps each Module screen in its own on-demand chunk:
 * `app.routes.ts` `loadChildren` → this table's `loadComponent` (AAP §0.3.4). No eager
 * `component:` and no static component `import` (a static import would defeat lazy chunking /
 * bundle budgets and the standalone lazy-route expectations).
 *
 * Route → screen mapping (AAP §0.6.4 standard ASPX→Angular CRUD mapping):
 *   ''                   → ModuleListComponent      (grid;   GET    /api/v1/modules)
 *   'new'                → ModuleFormComponent      (create; POST   /api/v1/modules)
 *   ':moduleId/edit'     → ModuleFormComponent      (edit;   PUT    /api/v1/modules/{id})
 *   ':moduleId/settings' → ModuleSettingsComponent  (config; module-settings editor)
 *
 * The `:moduleId` route-param name is shared by the edit + settings routes for consistency;
 * ModuleFormComponent derives create-vs-edit mode from the presence of that param.
 */
// MIGRATION: Web Forms postback navigation (Website/admin/Modules/*.ascx) replaced by
// client-side Angular lazy routes; each screen is code-split via 2nd-tier loadComponent.
export const MODULE_ROUTES: Routes = [
  {
    path: '',
    // QA #10: set a route title so the default TitleStrategy writes document.title
    // on SPA navigation (was left stale at the previously-visited screen's title).
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
