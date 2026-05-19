/**
 * @fileoverview Angular 19 User Feature Routing Configuration
 * @description Defines lazy-loaded routes for user management features including
 * user list, user creation/editing, and profile management. This routing module
 * replaces the DNN 4.x admin user navigation patterns with modern Angular 19
 * declarative routing using standalone components.
 *
 * MIGRATION NOTE:
 * This file replaces the navigation patterns from the following DNN source files:
 *
 * 1. ManageUsers.ascx.vb (lines 74-97, 126-160):
 *    - NavigateURL() calls for user list navigation
 *    - NavigateURL(TabId, "", UserFilter) for filtered list views
 *    - NavigateURL(PortalSettings.UserTabId, "", "UserID=" + UserInfo.UserID.ToString)
 *      for viewing/editing specific users
 *
 * 2. User.ascx.vb:
 *    - IsRegister mode -> '/new' route for user creation
 *    - Edit mode with UserId parameter -> '/:id' route for user editing
 *    - DNN's querystring parameters (userid) become Angular route parameters (:id)
 *
 * 3. Membership.ascx.vb and Profile.ascx.vb patterns:
 *    - User membership/profile management -> '/:id/profile' route
 *
 * 4. Users.ascx.vb:
 *    - User listing and navigation patterns -> '/list' route
 *
 * Original DNN Security Checks Replaced:
 * - IsAdmin check -> authGuard with JWT validation
 * - IsUser check -> authGuard combined with route-level data
 * - IsProfile check -> now part of '/:id/profile' route
 * - HasNecessaryPermission -> API-level authorization
 *
 * Route Structure:
 * - /users -> redirects to /users/list
 * - /users/list -> User list with filtering (UserListComponent)
 * - /users/new -> Create new user form (UserFormComponent)
 * - /users/:id -> Edit existing user form (UserFormComponent)
 * - /users/:id/profile -> User profile management (UserProfileComponent)
 *
 * @module features/user/user.routes
 * @see ManageUsers.ascx.vb - Original DNN user management page
 * @see User.ascx.vb - Original DNN user edit/create form
 * @see Membership.ascx.vb - Original DNN membership management
 */

import { Routes } from '@angular/router';

import { authGuard } from '../../core/auth/auth.guard';

/**
 * User feature routes configuration for lazy loading.
 *
 * This routes array defines the complete navigation structure for user management
 * features in the application. All routes are protected by the authGuard to ensure
 * only authenticated users can access user management functionality.
 *
 * MIGRATION NOTE:
 * Replaces DNN's NavigateURL/EditUrl patterns with Angular's declarative routing:
 *
 * Original DNN pattern (ManageUsers.ascx.vb):
 * ```vb
 * ' List navigation with filter
 * NavigateURL(TabId, "", IIf(UserFilter <> "", UserFilter, "").ToString())
 *
 * ' Edit user navigation
 * NavigateURL(PortalSettings.UserTabId, "", "UserID=" + UserInfo.UserID.ToString)
 *
 * ' After registration redirect
 * _RedirectURL = NavigateURL(CType(setting, Integer))
 * ```
 *
 * New Angular pattern:
 * ```typescript
 * // List navigation
 * router.navigate(['/users/list']);
 *
 * // Edit user navigation
 * router.navigate(['/users', userId]);
 *
 * // Create user navigation
 * router.navigate(['/users/new']);
 *
 * // Profile navigation
 * router.navigate(['/users', userId, 'profile']);
 * ```
 *
 * Each route includes:
 * - path: URL segment for the route
 * - loadComponent: Dynamic import for lazy loading the component
 * - canActivate: Guards array including authGuard for authentication
 * - data: Route metadata including title for browser/UI display
 *
 * @example
 * ```typescript
 * // Usage in parent app.routes.ts for lazy loading
 * export const routes: Routes = [
 *   {
 *     path: 'users',
 *     loadChildren: () => import('./features/user/user.routes')
 *       .then(m => m.userRoutes)
 *   }
 * ];
 *
 * // Usage in a component for navigation
 * @Component({ ... })
 * export class SomeComponent {
 *   private router = inject(Router);
 *
 *   navigateToUserList(): void {
 *     this.router.navigate(['/users/list']);
 *   }
 *
 *   editUser(userId: number): void {
 *     this.router.navigate(['/users', userId]);
 *   }
 *
 *   createNewUser(): void {
 *     this.router.navigate(['/users/new']);
 *   }
 *
 *   manageProfile(userId: number): void {
 *     this.router.navigate(['/users', userId, 'profile']);
 *   }
 * }
 * ```
 */
export const userRoutes: Routes = [
  /**
   * Default route - redirects to user list.
   *
   * MIGRATION NOTE:
   * In DNN, the default landing page for user administration was ManageUsers.ascx
   * which displayed the user grid. This redirect ensures the same user experience
   * by navigating users to the list view when they access /users directly.
   */
  {
    path: '',
    redirectTo: 'list',
    pathMatch: 'full'
  },

  /**
   * User list route - displays paginated user grid with filtering.
   *
   * MIGRATION NOTE:
   * Derived from ManageUsers.ascx.vb and Users.ascx.vb patterns:
   *
   * Original DNN features replaced:
   * - User grid display with pagination (ManageUsers.ascx.vb BindData)
   * - User filtering by properties (UserFilter property lines 140-160)
   * - Sort functionality on user properties
   * - Delete user action with confirmation
   * - Navigate to edit (NavigateURL with userid parameter)
   *
   * Original DNN navigation (ManageUsers.ascx.vb line 93):
   * ```vb
   * _RedirectURL = NavigateURL() ' Current page (user list)
   * ```
   *
   * Query parameters in original:
   * - filter: Text search filter
   * - filterproperty: Property to filter on
   * - currentpage: Pagination page number
   *
   * These are now handled by Angular query params or component state.
   */
  {
    path: 'list',
    loadComponent: () =>
      import('./components/user-list/user-list.component').then(
        (m) => m.UserListComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Manage Users',
      /**
       * Additional route data for component configuration.
       * Can be accessed via ActivatedRoute.data observable.
       */
      breadcrumb: 'Users',
      permissions: ['View Users']
    }
  },

  /**
   * New user creation route.
   *
   * MIGRATION NOTE:
   * Derived from User.ascx.vb in IsRegister mode:
   *
   * Original DNN pattern (User.ascx.vb):
   * ```vb
   * ' IsRegister mode indicates new user creation
   * Protected ReadOnly Property UseCaptcha() As Boolean
   *     Get
   *         Return CType(setting, Boolean) And IsRegister
   *     End Get
   * End Property
   * ```
   *
   * Original DNN navigation (ManageUsers.ascx.vb lines 248-254):
   * ```vb
   * If AddUser Then
   *     If Not Request.IsAuthenticated Then
   *         BindRegister()
   *     Else
   *         cmdRegister.Text = Localization.GetString("AddUser", LocalResourceFile)
   *     End If
   * End If
   * ```
   *
   * The UserFormComponent will detect the absence of :id parameter
   * and operate in creation mode.
   */
  {
    path: 'new',
    loadComponent: () =>
      import('./components/user-form/user-form.component').then(
        (m) => m.UserFormComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Create User',
      breadcrumb: 'New User',
      mode: 'create',
      permissions: ['Add User']
    }
  },

  /**
   * User edit route with dynamic user ID parameter.
   *
   * MIGRATION NOTE:
   * Derived from User.ascx.vb edit mode with UserId parameter:
   *
   * Original DNN pattern (ManageUsers.ascx.vb line 237):
   * ```vb
   * Response.Redirect(NavigateURL(PortalSettings.UserTabId, "", "UserID=" + UserInfo.UserID.ToString), True)
   * ```
   *
   * DNN's querystring parameter (userid) becomes Angular route parameter (:id).
   *
   * Original DNN validation (ManageUsers.ascx.vb lines 226-246):
   * ```vb
   * If IsEdit Then
   *     'Check if user has admin rights
   *     If Not IsAdmin Then
   *         AddModuleMessage("NotAuthorized", ModuleMessageType.YellowWarning, True)
   *         DisableForm()
   *     End If
   * End If
   * ```
   *
   * Authorization is now handled by authGuard at route level and
   * API authorization at the service layer.
   *
   * The UserFormComponent will read the :id parameter from ActivatedRoute
   * and load the corresponding user for editing.
   */
  {
    path: ':id',
    loadComponent: () =>
      import('./components/user-form/user-form.component').then(
        (m) => m.UserFormComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'Edit User',
      breadcrumb: 'Edit User',
      mode: 'edit',
      permissions: ['Edit User']
    }
  },

  /**
   * User profile management route.
   *
   * MIGRATION NOTE:
   * Derived from Membership.ascx.vb and Profile.ascx.vb patterns:
   *
   * Original DNN features (Membership.ascx.vb):
   * - UserMembership property access (lines 64-72)
   * - MembershipAuthorized/UnAuthorized events (lines 48-51)
   * - IsOnLine status display
   * - LockedOut status management
   * - Password management
   *
   * Original DNN pattern (ManageUsers.ascx.vb lines 259-260):
   * ```vb
   * If IsUser And IsProfile Then
   *     trTitle.Visible = False
   * End If
   * ```
   *
   * This route provides access to:
   * - User profile properties editing
   * - Membership status management
   * - Account settings (locked, authorized status)
   * - Password reset functionality
   *
   * The ':id' parameter identifies which user's profile to manage.
   * The UserProfileComponent handles all profile-related operations.
   */
  {
    path: ':id/profile',
    loadComponent: () =>
      import('./components/user-profile/user-profile.component').then(
        (m) => m.UserProfileComponent
      ),
    canActivate: [authGuard],
    data: {
      title: 'User Profile',
      breadcrumb: 'Profile',
      permissions: ['Manage Profile']
    }
  }
];
