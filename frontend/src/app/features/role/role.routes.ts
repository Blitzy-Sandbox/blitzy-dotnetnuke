import type { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

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
    // QA (Role form): guard the create form so unsaved edits are not silently lost
    // on browser Back/Forward (or any navigation away) — the user is prompted to
    // confirm. RoleFormComponent implements CanComponentDeactivate.
    path: 'new',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then((m) => m.RoleFormComponent),
    canDeactivate: [unsavedChangesGuard],
    title: 'Add Role',
  },
  {
    // QA (Role form): same unsaved-changes protection for the edit form.
    path: ':id/edit',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then((m) => m.RoleFormComponent),
    canDeactivate: [unsavedChangesGuard],
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
