// MIGRATION: Lazy route table for the Role feature. Re-expresses navigation among the legacy DotNetNuke
// Admin > Security role controls — Website/admin/Security/Roles.ascx.vb (list; ModuleActions "Add Role" /
// "User Settings"), EditRoles.ascx.vb (create/edit), and SecurityRoles.ascx.vb (user-role assignment) —
// as Angular standalone, lazily loaded routes. The parent app.routes.ts mounts these at 'roles' and
// applies authGuard there, so the guard is NOT re-applied here. withComponentInputBinding() (enabled
// app-wide) binds the ':id' path param to a component input() named `id`.
import type { Routes } from '@angular/router';

export const ROLE_ROUTES: Routes = [
  {
    path: '',
    title: 'Roles',
    loadComponent: () =>
      import('./role-list/role-list.component').then((m) => m.RoleListComponent),
  },
  {
    path: 'new',
    title: 'Add Role',
    loadComponent: () =>
      import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
  },
  {
    path: ':id/edit',
    title: 'Edit Role',
    loadComponent: () =>
      import('./role-form/role-form.component').then((m) => m.RoleFormComponent),
  },
  {
    path: ':id/assignments',
    title: 'Role Assignments',
    loadComponent: () =>
      import('./role-assignment/role-assignment.component').then(
        (m) => m.RoleAssignmentComponent,
      ),
  },
];
