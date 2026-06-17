import type { Routes } from '@angular/router';

// MIGRATION: DNN Web Forms had no client router. These routes reproduce the legacy
// Admin > Security navigation flow: Roles list -> Edit Role -> manage "User Roles".
export const ROLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/role-list/role-list.component').then((m) => m.RoleListComponent),
    title: 'Roles',
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Add Role',
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then((m) => m.RoleFormComponent),
    title: 'Edit Role',
  },
  {
    path: ':id/assignments',
    loadComponent: () =>
      import('./components/role-assignment/role-assignment.component').then(
        (m) => m.RoleAssignmentComponent,
      ),
    title: 'Role Assignments',
  },
];
