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
 * Explicit mapping from each UI permission key to the role name(s) that grant it.
 *
 * // MIGRATION: The legacy PortalSecurity.HasNecessaryPermission switched on the
 * // SecurityAccessLevel enum (Anonymous/View/Edit/Admin/Host) against per-object
 * // permission collections held SERVER-SIDE (PortalSecurity.vb L469-535). The client
 * // cannot see those permission collections; it only knows the authenticated user's
 * // role NAMES (User.roles, sourced from the JWT ClaimTypes.Role claims emitted by
 * // JwtService.cs L88-96). For this administrative SPA every gated affordance is an
 * // administrative action, so each permission key maps to the canonical DNN portal
 * // "Administrators" security role (Portal.AdministratorRoleName). Host / super users
 * // are granted independently of this map by AuthService.hasRole (its isSuperUser
 * // short-circuit), mirroring the legacy IsSuperUser shortcut.
 * //
 * // This map is the SINGLE source of truth for key->role resolution. A runtime
 * // permission key that is NOT a member of this map is treated as UNKNOWN and is
 * // denied fail-closed (see HasPermissionDirective.isAuthorized) BEFORE AuthService is
 * // consulted, so a permission key can never be mistaken for a role name. This is UI
 * // gating ONLY; the API remains the authoritative authorization boundary and
 * // re-checks every request (AAP Section 0.6.2).
 */
const PERMISSION_ROLE_MAP: ReadonlyMap<PermissionKey, readonly string[]> = new Map<
  PermissionKey,
  readonly string[]
>([
  ['VIEW', ['Administrators']],
  ['EDIT', ['Administrators']],
  ['DELETE', ['Administrators']],
  ['MANAGE_SETTINGS', ['Administrators']],
]);

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
  standalone: true,
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
      this.updateView(this.isAuthorized(key));
    });
  }

  /**
   * Resolve whether the current user is authorized for the given permission key.
   *
   * Fail-closed: an UNKNOWN runtime key (one not present in {@link PERMISSION_ROLE_MAP})
   * is denied before {@link AuthService} is consulted, so a raw permission key is never
   * passed to {@link AuthService.hasRole} as if it were a role name. A known key is granted
   * when the user holds ANY of the mapped role names; super users are granted independently
   * via hasRole's isSuperUser short-circuit.
   */
  private isAuthorized(key: PermissionKey): boolean {
    const allowedRoles = PERMISSION_ROLE_MAP.get(key);
    if (allowedRoles === undefined || allowedRoles.length === 0) {
      // Unknown / unmapped permission key -> deny (fail-closed).
      return false;
    }
    return allowedRoles.some((role) => this.authService.hasRole(role));
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
