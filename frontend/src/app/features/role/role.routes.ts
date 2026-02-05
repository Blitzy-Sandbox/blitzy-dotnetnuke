/**
 * @fileoverview Angular 19 Route Configuration for Role Feature Module
 * @description Defines lazy-loaded routes for role management functionality including
 * role list, role form (create/edit), and role-assignment components. All routes are
 * protected by authentication guard to ensure only authenticated users can access
 * role management features.
 *
 * MIGRATION NOTE:
 * Route paths and navigation patterns are derived from DNN admin navigation:
 *
 * Source Files:
 * - Website/admin/Security/Roles.ascx.vb
 *   * Role list grid with navigation to edit and user management
 *   * EditUrl("RoleID", RoleID.ToString, "Edit") → /:id/edit
 *   * NavigateURL(TabId, "User Roles", "RoleId=KEYFIELD") → /:id/users
 *   * ModuleActions.Add for AddContent → /new
 *
 * - Website/admin/Security/EditRoles.ascx.vb
 *   * Role create/edit form handling RoleID query parameter
 *   * NavigateURL(Me.TabId, "User Roles", "RoleId=" & RoleID) → /:id/users
 *   * Form submission redirects to role list
 *
 * - Website/admin/Security/SecurityRoles.ascx.vb
 *   * User-role assignment management
 *   * Accessed via RoleId query parameter
 *
 * Original DNN Security:
 * - SecurityAccessLevel.Admin was required for role management (Roles.ascx.vb line 307)
 * - HasNecessaryPermission checked user's administrative rights
 *
 * New Implementation:
 * - All routes protected by authGuard (functional CanActivateFn)
 * - Role-based authorization handled at API layer
 * - Uses Angular 19 standalone component patterns with loadComponent()
 * - Lazy loading for optimal bundle size and tree-shaking
 *
 * @module features/role/role.routes
 * @see Roles.ascx.vb - Original role list and navigation patterns
 * @see EditRoles.ascx.vb - Original role form handling
 * @see SecurityRoles.ascx.vb - Original user-role assignment
 * @see authGuard - Route protection guard
 */

import { Routes } from '@angular/router';

import { authGuard } from '../../core/auth/auth.guard';

/**
 * Route configuration for the role feature module.
 *
 * This constant defines all routes related to role management, implementing
 * lazy loading for each standalone component and authentication protection
 * via authGuard on all routes.
 *
 * Route Structure:
 * - '' (empty path) - Displays role list (default landing page for /roles)
 * - 'list' - Explicit path to role list (alternative to empty path)
 * - 'new' - Create new role form
 * - ':id/edit' - Edit existing role form (requires role ID)
 * - ':id/users' - Manage user-role assignments (requires role ID)
 *
 * MIGRATION NOTE:
 * The route structure maps to DNN's URL patterns:
 *
 * | DNN Pattern                                    | Angular Route    |
 * |-----------------------------------------------|------------------|
 * | /admin/Security/Roles.aspx                    | '' or 'list'     |
 * | /admin/Security/EditRoles.aspx                | 'new'            |
 * | /admin/Security/EditRoles.aspx?RoleID={id}    | ':id/edit'       |
 * | /admin/Security/SecurityRoles.aspx?RoleId={id}| ':id/users'      |
 *
 * Security:
 * All routes require authentication via authGuard, which:
 * - Checks JWT token validity through AuthService.isAuthenticated
 * - Redirects unauthenticated users to /auth/login with returnUrl
 * - Replaces DNN's SecurityAccessLevel.Admin checks from Roles.ascx.vb
 *
 * @example
 * ```typescript
 * // Main app.routes.ts configuration
 * import { Routes } from '@angular/router';
 *
 * export const routes: Routes = [
 *   {
 *     path: 'roles',
 *     loadChildren: () => import('./features/role/role.routes')
 *       .then(m => m.roleRoutes)
 *   }
 * ];
 *
 * // This enables routes like:
 * // /roles → Role list
 * // /roles/new → Create role
 * // /roles/5/edit → Edit role with ID 5
 * // /roles/5/users → Manage users for role with ID 5
 * ```
 *
 * @type {Routes}
 * @see authGuard - Authentication guard protecting all routes
 * @see RoleListComponent - Component for displaying role grid
 * @see RoleFormComponent - Component for create/edit role form
 * @see RoleAssignmentComponent - Component for user-role assignment management
 */
export const roleRoutes: Routes = [
  /**
   * Default route - Role List
   *
   * MIGRATION: Maps to Roles.ascx.vb which displays the role grid.
   * Original file loaded role data via RoleController.GetPortalRoles or
   * RoleController.GetRolesByGroup based on RoleGroupId selection.
   *
   * Features migrated:
   * - Role list with pagination (grdRoles DataGrid)
   * - Role group filtering (cboRoleGroups dropdown)
   * - Navigation to edit role and manage users
   * - Add new role button (ModuleActions)
   */
  {
    path: '',
    loadComponent: () =>
      import('./components/role-list/role-list.component').then(
        (m) => m.RoleListComponent
      ),
    canActivate: [authGuard],
    title: 'Roles - Security Management'
  },

  /**
   * Explicit list route - Alternative path to role list
   *
   * MIGRATION: Provides explicit /roles/list path for clarity.
   * Loads the same RoleListComponent as the default route.
   */
  {
    path: 'list',
    loadComponent: () =>
      import('./components/role-list/role-list.component').then(
        (m) => m.RoleListComponent
      ),
    canActivate: [authGuard],
    title: 'Roles - Security Management'
  },

  /**
   * Create new role route
   *
   * MIGRATION: Maps to EditRoles.ascx.vb when RoleID is -1 (new role mode).
   * Original behavior from EditRoles.ascx.vb lines 183-188:
   * - cmdDelete.Visible = False
   * - cmdManage.Visible = False
   * - lblRoleName.Visible = False
   * - txtRoleName.Visible = True (input field for new role name)
   *
   * In DNN, accessed via EditUrl() from Roles.ascx.vb ModuleActions (line 307).
   * Route data { mode: 'create' } helps component determine form behavior.
   */
  {
    path: 'new',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then(
        (m) => m.RoleFormComponent
      ),
    canActivate: [authGuard],
    data: { mode: 'create' },
    title: 'Create Role - Security Management'
  },

  /**
   * Edit existing role route
   *
   * MIGRATION: Maps to EditRoles.ascx.vb when RoleID is provided via query string.
   * Original URL pattern: EditUrl("RoleID", RoleID.ToString, "Edit")
   * Generated format string: "EditRoles.aspx?RoleID={0}"
   *
   * Original behavior from EditRoles.ascx.vb lines 131-178:
   * - Loads existing role data via RoleController.GetRole(RoleID, PortalId)
   * - Displays role name as label (not editable for existing roles)
   * - Populates all form fields with existing values
   * - Special handling for Administrator and Registered roles (read-only)
   *
   * Route parameter ':id' replaces DNN's RoleID query string parameter.
   * Route data { mode: 'edit' } helps component determine form behavior.
   */
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./components/role-form/role-form.component').then(
        (m) => m.RoleFormComponent
      ),
    canActivate: [authGuard],
    data: { mode: 'edit' },
    title: 'Edit Role - Security Management'
  },

  /**
   * User-role assignment management route
   *
   * MIGRATION: Maps to SecurityRoles.ascx.vb for managing users in a role.
   * Original URL patterns:
   * - From Roles.ascx.vb line 225: NavigateURL(TabId, "User Roles", "RoleId=KEYFIELD")
   * - From EditRoles.ascx.vb line 338: NavigateURL(Me.TabId, "User Roles", "RoleId=" & RoleID)
   *
   * SecurityRoles.ascx.vb functionality:
   * - Lists users assigned to the selected role
   * - Allows adding/removing users from role
   * - Manages effective dates for role assignments
   * - Supports user search and filtering
   *
   * Route parameter ':id' replaces DNN's RoleId query string parameter.
   * Path 'users' chosen to clearly indicate user management context.
   */
  {
    path: ':id/users',
    loadComponent: () =>
      import('./components/role-assignment/role-assignment.component').then(
        (m) => m.RoleAssignmentComponent
      ),
    canActivate: [authGuard],
    title: 'Role Users - Security Management'
  }
];
