/**
 * @fileoverview Angular 19 Route Guard for Authentication Protection
 * @description Functional route guard implementing CanActivateFn pattern to protect
 * routes requiring authentication. Replaces DNN's PortalSecurity.HasNecessaryPermission
 * and SecurityAccessLevel-based access control with JWT token-based route protection.
 *
 * MIGRATION NOTE:
 * This guard replaces DNN's multi-level security access control system found in
 * Library/Components/Security/PortalSecurity.vb (lines 45-53, 469-550):
 *
 * Original DNN SecurityAccessLevel enum:
 * - ControlPanel = -3: Internal control panel access
 * - SkinObject = -2: Skin rendering components
 * - Anonymous = -1: No authentication required
 * - View = 0: Basic view permission
 * - Edit = 1: Edit permission on module
 * - Admin = 2: Administrator access
 * - Host = 3: Super user / host access
 *
 * Original DNN permission check (HasNecessaryPermission):
 * - Checked FormsAuthentication.IsAuthenticated
 * - Evaluated user roles against portal/module/tab permissions
 * - Used cookie-based session state
 *
 * New Angular implementation:
 * - Uses stateless JWT token validation
 * - Checks AuthService.isAuthenticated computed signal
 * - Role-based authorization delegated to service layer
 * - Preserves intended destination via returnUrl query parameter
 *
 * The SecurityAccessLevel granularity is now handled at the API layer,
 * with this guard providing binary authenticated/unauthenticated protection.
 * For role-specific route protection, additional guards can be created that
 * check specific roles from the User model.
 *
 * @module core/auth/auth.guard
 * @see PortalSecurity.vb - Original DNN security implementation
 * @see AuthService - JWT authentication state management
 */

import { inject } from '@angular/core';
import { Router, CanActivateFn, UrlTree, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Functional route guard that protects routes requiring authentication.
 *
 * This guard implements the Angular 19 preferred functional pattern (CanActivateFn)
 * instead of class-based guards with CanActivate interface. It checks the
 * authentication state via AuthService and redirects unauthenticated users
 * to the login page while preserving their intended destination.
 *
 * MIGRATION NOTE:
 * Replaces DNN's PortalSecurity.HasNecessaryPermission method which evaluated:
 * - User's IsSuperUser flag (UserInfo.IsSuperUser)
 * - IsInRole checks for administrator roles
 * - ModulePermissionController.HasModulePermission for module access
 * - Tab.AdministratorRoles for page-level permissions
 *
 * In the new architecture:
 * - Authentication state is checked via JWT token presence
 * - Authorization (role checks) is handled by:
 *   1. API-level authorization attributes
 *   2. Separate role-based guards if needed
 *   3. Service layer permission validation
 *
 * @example
 * ```typescript
 * // Usage in route configuration (app.routes.ts)
 * import { authGuard } from './core/auth/auth.guard';
 *
 * export const routes: Routes = [
 *   {
 *     path: 'portals',
 *     loadComponent: () => import('./features/portal/components/portal-list/portal-list.component')
 *       .then(m => m.PortalListComponent),
 *     canActivate: [authGuard]
 *   },
 *   {
 *     path: 'admin',
 *     loadChildren: () => import('./features/admin/admin.routes')
 *       .then(m => m.ADMIN_ROUTES),
 *     canActivate: [authGuard]
 *   }
 * ];
 * ```
 *
 * @param route - The activated route snapshot containing route parameters and data
 * @param state - The router state snapshot containing the target URL
 * @returns {boolean | UrlTree} Returns true if authenticated, otherwise returns
 *          a UrlTree redirecting to /auth/login with returnUrl query parameter
 *
 * @see AuthService.isAuthenticated - Signal checked for authentication state
 * @see Router.createUrlTree - Used to construct redirect URL with query params
 */
export const authGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot
): boolean | UrlTree => {
  // Inject dependencies using Angular 19 functional inject() pattern
  // This replaces constructor-based injection per Angular 19 standalone guidelines
  const authService = inject(AuthService);
  const router = inject(Router);

  /**
   * Check authentication state using AuthService's computed signal.
   *
   * MIGRATION NOTE:
   * This replaces DNN's FormsAuthentication.IsAuthenticated check
   * (referenced in UserController.vb and throughout DNN codebase).
   * The signal provides reactive updates when auth state changes.
   */
  const isAuthenticated = authService.isAuthenticated();

  if (isAuthenticated) {
    // User is authenticated - allow route activation
    // MIGRATION: Equivalent to DNN returning true from HasNecessaryPermission
    // when user meets the SecurityAccessLevel.Anonymous or higher requirement
    return true;
  }

  /**
   * User is not authenticated - redirect to login page.
   *
   * MIGRATION NOTE:
   * In DNN, unauthenticated users were redirected via:
   * - Response.Redirect(Globals.NavigateURL("Login"))
   * - FormsAuthentication.RedirectToLoginPage()
   *
   * The new implementation:
   * 1. Captures the intended destination URL
   * 2. Redirects to /auth/login
   * 3. Passes returnUrl as query parameter for post-login navigation
   *
   * This pattern ensures users return to their original destination
   * after successful authentication, improving user experience.
   */
  const returnUrl = state.url;

  // Create UrlTree for navigation to login page with returnUrl
  // UrlTree is the preferred return type for guards when redirecting,
  // as it allows the router to handle the navigation atomically
  return router.createUrlTree(['/auth/login'], {
    queryParams: { returnUrl }
  });
};
