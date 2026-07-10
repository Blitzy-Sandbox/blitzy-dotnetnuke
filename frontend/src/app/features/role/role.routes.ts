import { Routes } from '@angular/router';

/**
 * ROLE_ROUTES — lazy child routes for the Role (Security) management feature.
 *
 * Consumed by frontend/src/app/app.routes.ts:
 *   { path: 'roles', canActivate: [authGuard],
 *     loadChildren: () => import('./features/role/role.routes').then((m) => m.ROLE_ROUTES) }
 * The authGuard is applied by the PARENT route — do NOT re-declare it here.
 *
 * MIGRATION: replaces the legacy Website/admin/Security role screens — roles.ascx
 * (grdRoles grid/list) and editroles.ascx (add/edit form) — with client-side routed
 * standalone components. ViewState/postback navigation is eliminated (AAP §0.6.3).
 */
export const ROLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./role-list/role-list.component').then((m) => m.RoleListComponent),
    title: 'Roles',
  },
  {
    // MIGRATION: static 'new' MUST precede ':id' so /roles/new is not matched as an id.
    // Maps to editroles.ascx in add mode.
    path: 'new',
    loadComponent: () =>
      import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Add Role',
  },
  {
    // MIGRATION: maps to editroles.ascx in edit mode (RoleFormComponent reads :id).
    path: ':id',
    loadComponent: () =>
      import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Edit Role',
  },
];
