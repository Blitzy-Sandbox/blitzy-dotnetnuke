/**
 * @fileoverview Angular 19 Top-Level Application Routing Configuration
 * @description Defines the primary route structure for the DNN Migration SPA,
 * implementing lazy-loaded routes using the loadComponent pattern for standalone
 * components. This file replaces the DNN admin navigation system from Website/admin/
 * WebForms pages with modern Angular routing.
 *
 * MIGRATION NOTE:
 * This routing configuration replaces the URL-based navigation patterns found in
 * the original DNN 4.x WebForms pages:
 *
 * Original DNN Navigation Patterns (from source files):
 * - Portals.ascx.vb (line 309): NavigateURL(objTab.TabID, "", "pid=KEYFIELD")
 * - Portals.ascx.vb (line 435): EditUrl("Signup"), EditUrl("Template")
 * - ManageUsers.ascx.vb (line 93-96): NavigateURL(), NavigateURL(TabId)
 * - ManageUsers.ascx.vb (line 237): NavigateURL(PortalSettings.UserTabId, "", "UserID=" + UserInfo.UserID.ToString)
 * - Roles.ascx.vb (line 84): EditUrl("RoleGroupId", RoleGroupId.ToString, "EditGroup")
 *
 * New Angular Routing Structure:
 * - /portals - Portal list (replaces admin/Portal/Portals.ascx)
 * - /portals/:id - Portal edit form (replaces admin/Portal/SiteSettings.ascx)
 * - /portals/:id/settings - Portal settings (replaces admin/Portal/SiteSettings.ascx advanced)
 * - /modules - Module list (replaces admin/Modules/)
 * - /modules/:id - Module edit form (replaces admin/Modules/ModuleSettings.ascx)
 * - /modules/:id/settings - Module settings
 * - /users - User list (replaces admin/Users/ManageUsers.ascx)
 * - /users/:id - User edit form (replaces admin/Users/User.ascx)
 * - /users/:id/profile - User profile (replaces admin/Users/Membership.ascx)
 * - /roles - Role list (replaces admin/Security/Roles.ascx)
 * - /roles/:id - Role edit form (replaces admin/Security/EditRoles.ascx)
 * - /roles/:id/assignment - Role assignment (replaces admin/Security/SecurityRoles.ascx)
 * - /auth/login - Login page (replaces DNN login mechanism)
 *
 * @module app/app.routes
 * @see authGuard - Route protection for authenticated access
 */

import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * Application routes configuration array.
 *
 * Implements Angular 19 best practices:
 * - Lazy loading via loadComponent() for standalone components
 * - Functional route guards (authGuard) for route protection
 * - Hierarchical URL structure mirroring admin functionality
 *
 * All admin routes (portals, modules, users, roles) are protected by authGuard
 * which validates JWT authentication and redirects to login if needed.
 * The auth routes remain unprotected to allow user login.
 *
 * MIGRATION NOTE:
 * The DNN WebForms navigation used several patterns:
 * 1. NavigateURL() - General page navigation with query strings
 * 2. EditUrl() - Module-specific edit URLs
 * 3. TabController.GetTabByName() - Tab-based navigation
 *
 * All these are now consolidated into Angular Router path segments
 * with parameters (e.g., :id) replacing query string values (e.g., pid=KEYFIELD).
 *
 * @example
 * ```typescript
 * // In app.config.ts:
 * import { routes } from './app.routes';
 * import { provideRouter } from '@angular/router';
 *
 * export const appConfig: ApplicationConfig = {
 *   providers: [provideRouter(routes)]
 * };
 * ```
 */
export const routes: Routes = [
  /**
   * Default route - redirects to portals list
   * MIGRATION: Replaces DNN's default portal navigation
   */
  {
    path: '',
    redirectTo: 'portals',
    pathMatch: 'full'
  },

  // ============================================================================
  // Portal Routes
  // MIGRATION: Replaces Website/admin/Portal/*.ascx.vb navigation
  // Original patterns:
  // - Portals.ascx (list) → NavigateURL(TabId, "")
  // - SiteSettings.ascx (edit) → NavigateURL(objTab.TabID, "", "pid={0}")
  // - Signup.ascx (create) → EditUrl("Signup")
  // ============================================================================

  /**
   * Portal list route
   * MIGRATION: Replaces admin/Portal/Portals.ascx
   * Original navigation: grdPortals with NavigateURLFormatString for edit
   */
  {
    path: 'portals',
    loadComponent: () =>
      import('./features/portal/components/portal-list/portal-list.component').then(
        m => m.PortalListComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Portal create route
   * MIGRATION: Replaces admin/Portal/Signup.ascx (EditUrl("Signup"))
   * Provides new portal creation form
   */
  {
    path: 'portals/new',
    loadComponent: () =>
      import('./features/portal/components/portal-form/portal-form.component').then(
        m => m.PortalFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Portal edit form route
   * MIGRATION: Replaces admin/Portal/SiteSettings.ascx
   * Original: NavigateURL(objTab.TabID, "", "pid=KEYFIELD")
   * Now uses route parameter :id instead of query string pid
   */
  {
    path: 'portals/:id',
    loadComponent: () =>
      import('./features/portal/components/portal-form/portal-form.component').then(
        m => m.PortalFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Portal settings route
   * MIGRATION: Replaces advanced settings section of admin/Portal/SiteSettings.ascx
   * Provides detailed portal configuration options
   */
  {
    path: 'portals/:id/settings',
    loadComponent: () =>
      import('./features/portal/components/portal-settings/portal-settings.component').then(
        m => m.PortalSettingsComponent
      ),
    canActivate: [authGuard]
  },

  // ============================================================================
  // Module Routes
  // MIGRATION: Replaces Website/admin/Modules/*.ascx.vb navigation
  // Original patterns:
  // - ModuleSettings.ascx → EditUrl with module ID
  // - Export.ascx, Import.ascx → Additional module operations
  // ============================================================================

  /**
   * Module list route
   * MIGRATION: Replaces module listing functionality from admin panel
   */
  {
    path: 'modules',
    loadComponent: () =>
      import('./features/module/components/module-list/module-list.component').then(
        m => m.ModuleListComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Module create route
   * Provides new module creation form
   */
  {
    path: 'modules/new',
    loadComponent: () =>
      import('./features/module/components/module-form/module-form.component').then(
        m => m.ModuleFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Module edit form route
   * MIGRATION: Replaces admin/Modules/ModuleSettings.ascx
   * Original: EditUrl with ModuleId parameter
   */
  {
    path: 'modules/:id',
    loadComponent: () =>
      import('./features/module/components/module-form/module-form.component').then(
        m => m.ModuleFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Module settings route
   * MIGRATION: Replaces detailed configuration from admin/Modules/ModuleSettings.ascx
   */
  {
    path: 'modules/:id/settings',
    loadComponent: () =>
      import('./features/module/components/module-settings/module-settings.component').then(
        m => m.ModuleSettingsComponent
      ),
    canActivate: [authGuard]
  },

  // ============================================================================
  // User Routes
  // MIGRATION: Replaces Website/admin/Users/*.ascx.vb navigation
  // Original patterns:
  // - ManageUsers.ascx (list) → User grid with filtering
  // - User.ascx (edit) → NavigateURL with UserID parameter
  // - Membership.ascx → Profile management
  // ============================================================================

  /**
   * User list route
   * MIGRATION: Replaces admin/Users/ManageUsers.ascx
   * Original features: filtering, pagination, letter search (similar to Portals.ascx)
   */
  {
    path: 'users',
    loadComponent: () =>
      import('./features/user/components/user-list/user-list.component').then(
        m => m.UserListComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * User create route
   * MIGRATION: Replaces admin/Users/User.ascx in AddUser mode
   */
  {
    path: 'users/new',
    loadComponent: () =>
      import('./features/user/components/user-form/user-form.component').then(
        m => m.UserFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * User edit form route
   * MIGRATION: Replaces admin/Users/User.ascx
   * Original: NavigateURL with UserID query parameter
   * ManageUsers.ascx line 237: NavigateURL(PortalSettings.UserTabId, "", "UserID=" + UserInfo.UserID.ToString)
   */
  {
    path: 'users/:id',
    loadComponent: () =>
      import('./features/user/components/user-form/user-form.component').then(
        m => m.UserFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * User profile route
   * MIGRATION: Replaces admin/Users/Membership.ascx profile management
   * Provides user profile editing and membership information
   */
  {
    path: 'users/:id/profile',
    loadComponent: () =>
      import('./features/user/components/user-profile/user-profile.component').then(
        m => m.UserProfileComponent
      ),
    canActivate: [authGuard]
  },

  // ============================================================================
  // Role Routes
  // MIGRATION: Replaces Website/admin/Security/*.ascx.vb navigation
  // Original patterns:
  // - Roles.ascx (list) → Role grid with group filtering
  // - EditRoles.ascx (edit) → EditUrl with RoleId
  // - SecurityRoles.ascx → User-role assignments
  // ============================================================================

  /**
   * Role list route
   * MIGRATION: Replaces admin/Security/Roles.ascx
   * Original: grdRoles data grid with role group dropdown
   */
  {
    path: 'roles',
    loadComponent: () =>
      import('./features/role/components/role-list/role-list.component').then(
        m => m.RoleListComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Role create route
   * MIGRATION: Replaces admin/Security/EditRoles.ascx in add mode
   */
  {
    path: 'roles/new',
    loadComponent: () =>
      import('./features/role/components/role-form/role-form.component').then(
        m => m.RoleFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Role edit form route
   * MIGRATION: Replaces admin/Security/EditRoles.ascx
   * Original: EditUrl("RoleGroupId", RoleGroupId.ToString, "EditGroup")
   */
  {
    path: 'roles/:id',
    loadComponent: () =>
      import('./features/role/components/role-form/role-form.component').then(
        m => m.RoleFormComponent
      ),
    canActivate: [authGuard]
  },

  /**
   * Role assignment route
   * MIGRATION: Replaces admin/Security/SecurityRoles.ascx
   * Provides user-to-role assignment functionality
   */
  {
    path: 'roles/:id/assignment',
    loadComponent: () =>
      import('./features/role/components/role-assignment/role-assignment.component').then(
        m => m.RoleAssignmentComponent
      ),
    canActivate: [authGuard]
  },

  // ============================================================================
  // Authentication Routes
  // MIGRATION: Replaces DNN Forms Authentication login mechanism
  // Original: Cookie-based FormsAuthentication with Login.aspx
  // New: JWT-based authentication with SPA login component
  // ============================================================================

  /**
   * Auth routes group - unprotected to allow user login
   * Note: No authGuard on auth routes to allow unauthenticated access
   */
  {
    path: 'auth',
    children: [
      /**
       * Login route
       * MIGRATION: Replaces DNN's Login.aspx and FormsAuthentication.RedirectToLoginPage()
       * Handles JWT token acquisition and user authentication
       */
      {
        path: 'login',
        loadComponent: () =>
          import('./features/auth/login/login.component').then(
            m => m.LoginComponent
          )
      },
      /**
       * Default auth redirect to login
       */
      {
        path: '',
        redirectTo: 'login',
        pathMatch: 'full'
      }
    ]
  },

  // ============================================================================
  // Wildcard Route - Catch-all for undefined paths
  // Redirects to the default portal list view
  // ============================================================================

  /**
   * Wildcard route for undefined paths
   * Redirects unknown URLs to the portals list (default view)
   * This ensures users see meaningful content even with typos in URLs
   */
  {
    path: '**',
    redirectTo: 'portals'
  }
];
