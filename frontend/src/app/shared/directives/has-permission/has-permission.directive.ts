import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { AuthService } from '../../../core/auth/auth.service';

/**
 * Permission keys accepted by {@link HasPermissionDirective}.
 *
 * // MIGRATION: Derived from the legacy SecurityAccessLevel enum and the
 * // HasNecessaryPermission overloads in
 * // Library/Components/Security/PortalSecurity.vb (L45-L53, L469-L535).
 * // The legacy server-side access tiers are reduced to the four UI-gating
 * // permission keys mandated by the migration plan (VIEW/EDIT/DELETE/
 * // MANAGE_SETTINGS). This directive performs UI gating ONLY; server-side
 * // authorization remains the authoritative enforcement point.
 */
export type PermissionKey = 'VIEW' | 'EDIT' | 'DELETE' | 'MANAGE_SETTINGS';

/**
 * Structural directive that conditionally renders its host template based on
 * the current user's role-based access.
 *
 * Usage: `<button *appHasPermission="'EDIT'">Save</button>`
 *
 * // MIGRATION: Replaces PortalSecurity.HasNecessaryPermission. Superusers are
 * // always granted because AuthService.hasRole returns true for isSuperUser,
 * // mirroring the legacy `If User.IsSuperUser Then blnAuthorized = True`
 * // shortcut (PortalSecurity.vb L524-L526).
 */
@Directive({
  selector: '[appHasPermission]',
})
export class HasPermissionDirective {
  private readonly templateRef: TemplateRef<unknown> = inject(TemplateRef);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private readonly authService = inject(AuthService);

  /** Required permission key; bound via the `*appHasPermission` microsyntax. */
  readonly appHasPermission = input.required<PermissionKey>();

  private hasView = false;

  constructor() {
    effect(() => {
      const key: PermissionKey = this.appHasPermission();
      // Explicitly read the currentUser signal so the effect re-evaluates
      // reactively on login/logout, even in tests where hasRole is mocked and
      // therefore does not itself read the signal.
      this.authService.currentUser();
      this.updateView(this.authService.hasRole(key));
    });
  }

  private updateView(authorized: boolean): void {
    if (authorized && !this.hasView) {
      this.viewContainerRef.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!authorized && this.hasView) {
      this.viewContainerRef.clear();
      this.hasView = false;
    }
  }
}
