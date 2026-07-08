import { Routes } from '@angular/router';

/**
 * MODULE_ROUTES — lazy child routes for the Module Management feature area.
 *
 * This is the lazy-loaded child route table for the protected `modules` area of the
 * Angular 19 `dnn-migration` SPA. The already-created parent `app.routes.ts` wires it in with:
 *   {
 *     path: 'modules',
 *     canActivate: [authGuard],
 *     loadChildren: () =>
 *       import('./features/module/module.routes').then((m) => m.MODULE_ROUTES),
 *   }
 * so the export name (`MODULE_ROUTES`) and type (`Routes`) are a hard contract with the parent.
 *
 * MIGRATION: the legacy DNN admin module workflows (Website/admin/Modules/** — the module
 * inventory list + ModuleSettings.ascx.vb create/edit settings) become stateless Angular
 * routes. Web Forms postback/ViewState/IClientAPICallbackEventHandler navigation is ELIMINATED
 * (AAP §0.6.3): the list screen is a GET-backed component and the settings screen is a
 * reactive form that POSTs (create) / PUTs (edit) via the injected ModuleService.
 *
 * MIGRATION: the parent route already applies `canActivate: [authGuard]` to the whole
 * `modules` area, so NO guard is re-declared on the child routes below — this avoids redundant
 * double-evaluation of the guard on every intra-feature navigation.
 *
 * Route ordering is significant: the static `'new'` route is declared BEFORE the dynamic
 * `':id'` route so that `/modules/new` resolves to the create form and is never captured as an
 * edit-by-id. `ModuleFormComponent` is intentionally reused for both `'new'` and `':id'`; it
 * detects create-vs-edit mode from the presence of the `:id` route param.
 */
export const MODULE_ROUTES: Routes = [
  {
    // /modules — module inventory list/grid (default child). MIGRATION: List/Grid page -> GET /api/modules.
    path: '',
    title: 'Modules',
    loadComponent: () =>
      import('./module-list/module-list.component').then((m) => m.ModuleListComponent),
  },
  {
    // /modules/new — settings form in CREATE mode. MIGRATION: Create page -> POST /api/modules.
    path: 'new',
    title: 'New Module',
    loadComponent: () =>
      import('./module-form/module-form.component').then((m) => m.ModuleFormComponent),
  },
  {
    // /modules/:id — settings form in EDIT mode. MIGRATION: Edit page -> GET/PUT (+ DELETE) /api/modules/{id}.
    path: ':id',
    title: 'Edit Module',
    loadComponent: () =>
      import('./module-form/module-form.component').then((m) => m.ModuleFormComponent),
  },
];
