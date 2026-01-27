/**
 * MIGRATION: Role Feature Routes
 * 
 * Angular 19 route configuration for the role feature module.
 * Defines lazy-loaded routes for role-list, role-form, and role-assignment components.
 * 
 * MIGRATION SOURCE: Website/admin/Security/Roles.ascx.vb, EditRoles.ascx.vb, SecurityRoles.ascx.vb
 * 
 * Key transformations:
 * - VB.NET NavigateURL patterns → Angular Router paths
 * - VB.NET SecurityAccessLevel → authGuard route protection (when available)
 * - Uses loadComponent for lazy loading and tree-shaking optimization
 */

import { Routes } from '@angular/router';

/**
 * Role feature routes.
 * MIGRATION: Route paths derived from DNN admin navigation patterns.
 */
export const roleRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/role-list/role-list.component')
      .then(m => m.RoleListComponent)
  },
  {
    path: 'list',
    loadComponent: () => import('./components/role-list/role-list.component')
      .then(m => m.RoleListComponent)
  },
  {
    path: 'new',
    loadComponent: () => import('./components/role-form/role-form.component')
      .then(m => m.RoleFormComponent)
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./components/role-form/role-form.component')
      .then(m => m.RoleFormComponent)
  },
  {
    path: ':id/users',
    loadComponent: () => import('./components/role-assignment/role-assignment.component')
      .then(m => m.RoleAssignmentComponent)
  },
  {
    path: 'groups/new',
    loadComponent: () => import('./components/role-form/role-form.component')
      .then(m => m.RoleFormComponent),
    data: { mode: 'group' }
  },
  {
    path: 'groups/:id/edit',
    loadComponent: () => import('./components/role-form/role-form.component')
      .then(m => m.RoleFormComponent),
    data: { mode: 'group' }
  }
];
