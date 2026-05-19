/**
 * @fileoverview Angular 19 Standalone Structural Directive for Permission-Based Visibility
 * @description Replaces DNN's server-side PortalSecurity.IsInRoles() checks and Visible property
 * toggling with client-side role-based DOM rendering. Conditionally renders or removes elements
 * from the DOM based on user permissions validated against JWT claims.
 *
 * MIGRATION NOTE: This directive replaces the following DNN server-side patterns:
 * - PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) (SecurityRoles.ascx.vb line 322)
 * - cboRoles.Visible = False (SecurityRoles.ascx.vb line 195)
 * - btnDelete.Visible checks for role-based button visibility
 * - Server-side control visibility toggling based on user roles
 *
 * The original VB.NET IsInRoles pattern (PortalSecurity.vb lines 115-136):
 * ```vb
 * Public Shared Function IsInRoles(ByVal roles As String) As Boolean
 *     For Each role In roles.Split(New Char() {";"c})
 *         If objUserInfo.IsSuperUser Or objUserInfo.IsInRole(role) Then
 *             Return True
 *         End If
 *     Next role
 *     Return False
 * End Function
 * ```
 *
 * Role names map directly to JWT claims roles array from the backend authentication system.
 *
 * @example
 * ```html
 * <!-- Single role check -->
 * <button *appHasPermission="'Administrator'">Delete All</button>
 *
 * <!-- Multiple roles (user needs ANY of these) -->
 * <div *appHasPermission="['Administrator', 'Editor']">
 *   <app-edit-controls />
 * </div>
 *
 * <!-- With else template for fallback content -->
 * <ng-container *appHasPermission="['Administrator']; else noAccess">
 *   <app-admin-panel />
 * </ng-container>
 * <ng-template #noAccess>
 *   <p>You do not have permission to view this content.</p>
 * </ng-template>
 * ```
 *
 * @module shared/directives/has-permission.directive
 */

import {
  Directive,
  Input,
  TemplateRef,
  ViewContainerRef,
  inject,
  OnDestroy
} from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Structural directive that conditionally renders content based on user permissions.
 *
 * This directive provides client-side role-based access control for Angular templates,
 * replacing DNN's server-side PortalSecurity.IsInRoles() visibility patterns with
 * reactive DOM manipulation based on JWT authentication claims.
 *
 * Key features:
 * - Accepts single role (string) or multiple roles (string array)
 * - Renders content if user has ANY of the specified roles
 * - SuperUser/Host users implicitly have all permissions (checked via isSuperUser flag)
 * - Gracefully handles unauthenticated state (hides content)
 * - Supports optional else template for fallback content
 * - Prevents duplicate DOM insertions with state tracking
 *
 * Implementation follows Angular 19 best practices:
 * - Standalone directive (no NgModule required)
 * - Functional inject() pattern for dependency injection
 * - Reactive state management with proper cleanup
 *
 * MIGRATION NOTE: Replaces DNN patterns like:
 * - `If PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) = False Then`
 *   (SecurityRoles.ascx.vb line 322)
 * - `cboRoles.Visible = False` conditional visibility (SecurityRoles.ascx.vb line 195)
 * - `btnDelete.Visible = RoleController.CanRemoveUserFromRole(...)` permission checks
 *
 * @class HasPermissionDirective
 * @implements {OnDestroy}
 *
 * @example
 * ```typescript
 * // In a component's imports array
 * @Component({
 *   selector: 'app-admin-page',
 *   standalone: true,
 *   imports: [HasPermissionDirective],
 *   template: `
 *     <button *appHasPermission="['Administrator']">Admin Action</button>
 *   `
 * })
 * export class AdminPageComponent {}
 * ```
 */
@Directive({
  selector: '[appHasPermission]',
  standalone: true
})
export class HasPermissionDirective implements OnDestroy {
  /**
   * Reference to the host ng-template element.
   * Used to create embedded views when permission check passes.
   *
   * Injected using Angular 19's functional inject() pattern.
   */
  private readonly templateRef = inject(TemplateRef<unknown>);

  /**
   * Reference to the view container for DOM manipulation.
   * Provides createEmbeddedView() and clear() methods for conditional rendering.
   *
   * Injected using Angular 19's functional inject() pattern.
   */
  private readonly viewContainer = inject(ViewContainerRef);

  /**
   * Authentication service for retrieving current user information.
   * Provides getCurrentUser() method to access user roles for permission validation.
   *
   * MIGRATION NOTE: Replaces DNN's UserController.GetCurrentUserInfo() pattern
   * which was used in PortalSecurity.IsInRoles (PortalSecurity.vb line 119).
   */
  private readonly authService = inject(AuthService);

  /**
   * Tracks whether the main view is currently rendered.
   * Prevents duplicate view insertions and enables proper cleanup.
   *
   * MIGRATION NOTE: In DNN, visibility state was managed by the WebForms control
   * lifecycle. In Angular, we must explicitly track render state to avoid
   * creating duplicate DOM elements.
   */
  private hasView = false;

  /**
   * Tracks whether the else template is currently rendered.
   * Mutually exclusive with hasView - only one should be true at a time.
   */
  private hasElseView = false;

  /**
   * Optional reference to an else template for fallback content.
   * When the user lacks required permissions, this template is rendered instead.
   *
   * @example
   * ```html
   * <div *appHasPermission="['Admin']; else noAccessTpl">
   *   Admin content here
   * </div>
   * <ng-template #noAccessTpl>
   *   <p>Access denied</p>
   * </ng-template>
   * ```
   */
  private elseTemplateRef: TemplateRef<unknown> | null = null;

  /**
   * Sets the required permission roles for content visibility.
   *
   * Accepts either a single role name (string) or an array of role names.
   * If a string is provided, it is normalized to a single-element array.
   * Permission check passes if the user has ANY of the specified roles.
   *
   * The setter performs the complete permission evaluation and DOM manipulation:
   * 1. Normalizes input to array format
   * 2. Retrieves current user from AuthService
   * 3. Checks if user has any required role (or is SuperUser)
   * 4. Renders or clears the view based on permission result
   *
   * MIGRATION NOTE: This setter replaces DNN's inline PortalSecurity.IsInRoles()
   * calls that were evaluated on each page request/postback. In Angular, this
   * is evaluated when the input binding changes.
   *
   * @param roles - Single role name or array of role names to check against user roles
   *
   * @example
   * ```html
   * <!-- String input -->
   * <button *appHasPermission="'Administrator'">Delete</button>
   *
   * <!-- Array input -->
   * <div *appHasPermission="['Administrator', 'Editor', 'Moderator']">
   *   Editable content
   * </div>
   *
   * <!-- Bound to component property -->
   * <section *appHasPermission="requiredRoles">
   *   Protected section
   * </section>
   * ```
   */
  @Input()
  set appHasPermission(roles: string | string[]) {
    // Normalize roles input to array format
    // MIGRATION NOTE: DNN's IsInRoles accepted semicolon-delimited string;
    // we support both string and array for flexibility
    const requiredRoles = this.normalizeRoles(roles);

    // Check if user has permission
    const hasPermission = this.checkPermission(requiredRoles);

    // Update view based on permission result
    this.updateView(hasPermission);
  }

  /**
   * Sets the optional else template reference for fallback content.
   *
   * When the user lacks the required permissions, this template is rendered
   * instead of the main content. This enables graceful degradation with
   * informative messages rather than simply hiding content.
   *
   * @param templateRef - Template reference to render when permission check fails
   *
   * @example
   * ```html
   * <ng-container *appHasPermission="['Administrator']; else accessDenied">
   *   <app-admin-dashboard />
   * </ng-container>
   * <ng-template #accessDenied>
   *   <app-access-denied-message />
   * </ng-template>
   * ```
   */
  @Input()
  set appHasPermissionElse(templateRef: TemplateRef<unknown> | null) {
    this.elseTemplateRef = templateRef;

    // Re-evaluate view if we already know the permission state
    // This handles cases where else template is set after main permission
    if (!this.hasView && this.elseTemplateRef && !this.hasElseView) {
      this.showElseView();
    }
  }

  /**
   * Cleanup method called when directive is destroyed.
   * Clears any rendered views to prevent memory leaks.
   */
  ngOnDestroy(): void {
    this.viewContainer.clear();
    this.hasView = false;
    this.hasElseView = false;
  }

  /**
   * Normalizes the roles input to an array of trimmed, non-empty role names.
   *
   * Handles multiple input formats:
   * - Single string role: 'Administrator' -> ['Administrator']
   * - String array: ['Administrator', 'Editor'] -> ['Administrator', 'Editor']
   * - Empty/null values: null/undefined/'' -> []
   *
   * MIGRATION NOTE: DNN's IsInRoles used semicolon-delimited strings:
   * `roles.Split(New Char() {";"c})`. This method provides more flexibility
   * while maintaining similar normalization behavior.
   *
   * @param roles - Input roles value (string, string array, or null/undefined)
   * @returns Normalized array of role names with whitespace trimmed
   */
  private normalizeRoles(roles: string | string[] | null | undefined): string[] {
    // Handle null/undefined input
    if (roles === null || roles === undefined) {
      return [];
    }

    // Convert string to array
    if (typeof roles === 'string') {
      // Handle empty string
      const trimmed = roles.trim();
      if (trimmed === '') {
        return [];
      }

      // Support semicolon-delimited strings for DNN compatibility
      // MIGRATION NOTE: DNN used semicolon delimiter in IsInRoles pattern
      if (trimmed.includes(';')) {
        return trimmed
          .split(';')
          .map(role => role.trim())
          .filter(role => role !== '');
      }

      return [trimmed];
    }

    // Array input - filter and trim each element
    return roles
      .map(role => (typeof role === 'string' ? role.trim() : ''))
      .filter(role => role !== '');
  }

  /**
   * Checks if the current user has any of the required permissions.
   *
   * Permission check logic (mirrors DNN's PortalSecurity.IsInRoles):
   * 1. If no roles required, return true (content visible to all)
   * 2. Get current user from AuthService
   * 3. If user is null/undefined, return false (unauthenticated)
   * 4. If user is SuperUser, return true (host has all permissions)
   * 5. Check if user has ANY of the required roles
   *
   * MIGRATION NOTE: Original VB.NET logic from PortalSecurity.vb lines 115-136:
   * ```vb
   * For Each role In roles.Split(New Char() {";"c})
   *     If objUserInfo.IsSuperUser Or objUserInfo.IsInRole(role) Then
   *         Return True
   *     End If
   * Next role
   * Return False
   * ```
   *
   * @param requiredRoles - Array of role names to check
   * @returns true if user has permission, false otherwise
   */
  private checkPermission(requiredRoles: string[]): boolean {
    // No roles required means content is visible to everyone
    // MIGRATION NOTE: In DNN, passing null/empty to IsInRoles returned false;
    // here we interpret empty requirements as "no restriction"
    if (requiredRoles.length === 0) {
      return true;
    }

    // Get current user from AuthService
    // MIGRATION NOTE: Replaces UserController.GetCurrentUserInfo() call
    const user = this.authService.getCurrentUser();

    // No user means not authenticated - hide protected content
    // MIGRATION NOTE: In DNN, context.Request.IsAuthenticated was checked first
    if (user === null || user === undefined) {
      return false;
    }

    // SuperUser (Host) has all permissions
    // MIGRATION NOTE: Direct port of `If objUserInfo.IsSuperUser` check
    // from PortalSecurity.IsInRoles (line 123)
    if (user.isSuperUser === true) {
      return true;
    }

    // Check if user has any of the required roles
    // User roles come from JWT claims as string array
    const userRoles = user.roles ?? [];

    // Return true if user has ANY of the required roles
    // MIGRATION NOTE: Direct port of `objUserInfo.IsInRole(role)` check
    // Performs case-insensitive comparison for role matching
    return requiredRoles.some(requiredRole =>
      userRoles.some(userRole =>
        userRole.toLowerCase() === requiredRole.toLowerCase()
      )
    );
  }

  /**
   * Updates the DOM based on permission check result.
   *
   * State transitions:
   * - Permission granted + not showing: Create embedded view
   * - Permission granted + showing else: Clear and show main view
   * - Permission denied + showing main: Clear main view
   * - Permission denied + not showing else + has else template: Show else view
   *
   * MIGRATION NOTE: In DNN WebForms, this was handled by setting the Visible
   * property on server controls. In Angular, we must manually manage the
   * ViewContainerRef to add/remove views from the DOM.
   *
   * @param hasPermission - Result of permission check
   */
  private updateView(hasPermission: boolean): void {
    if (hasPermission) {
      // User has permission - show main content
      this.showMainView();
    } else {
      // User lacks permission - show else content or nothing
      this.hideMainView();
    }
  }

  /**
   * Renders the main template content and clears any else content.
   *
   * Uses ViewContainerRef.createEmbeddedView() to instantiate the template
   * and insert it into the DOM. Tracks state to prevent duplicate insertions.
   */
  private showMainView(): void {
    // Clear else view if showing
    if (this.hasElseView) {
      this.viewContainer.clear();
      this.hasElseView = false;
    }

    // Create main view if not already showing
    if (!this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    }
  }

  /**
   * Removes the main template content and optionally shows else content.
   *
   * Uses ViewContainerRef.clear() to remove the view from the DOM.
   * If an else template is configured, renders that instead.
   */
  private hideMainView(): void {
    // Clear main view if showing
    if (this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }

    // Show else view if available
    if (this.elseTemplateRef && !this.hasElseView) {
      this.showElseView();
    }
  }

  /**
   * Renders the else template content.
   *
   * Called when permission check fails and an else template is provided.
   * Clears any existing content before rendering.
   */
  private showElseView(): void {
    if (this.elseTemplateRef && !this.hasElseView) {
      // Ensure main view is cleared
      if (this.hasView) {
        this.viewContainer.clear();
        this.hasView = false;
      }

      this.viewContainer.createEmbeddedView(this.elseTemplateRef);
      this.hasElseView = true;
    }
  }
}
