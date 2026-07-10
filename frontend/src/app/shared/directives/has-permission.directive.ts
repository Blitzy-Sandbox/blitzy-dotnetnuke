import {
  computed,
  Directive,
  effect,
  inject,
  Input,
  signal,
  TemplateRef,
  ViewContainerRef,
} from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * HasPermissionDirective — a standalone STRUCTURAL directive that renders its host
 * template ONLY when the current user is authorized for the required role(s).
 *
 * Usage:
 * ```html
 * <!-- single role (string) -->
 * <button *appHasPermission="'Administrators'">Edit</button>
 *
 * <!-- any-of several roles (array) -->
 * <a *appHasPermission="['Administrators', 'Editors']">Manage</a>
 *
 * <!-- ';'- or ','-delimited string is also accepted (legacy IsInRoles shape) -->
 * <li *appHasPermission="'Administrators;Editors'">Settings</li>
 * ```
 *
 * Consumed by `shared/components/*` (row-action visibility), `layout/*` (menu items),
 * and `features/*` to show/hide UI by permission. Presentation/visibility ONLY —
 * it reads auth state and never handles tokens, HTTP, or business logic.
 *
 * MIGRATION: mirrors the DotNetNuke `PortalSecurity` role-visibility model
 * (Library/Components/Security/PortalSecurity.vb, AAP §0.6.4). The legacy
 * `SecurityAccessLevel` enum (Anonymous/View/Edit/Admin/Host) gated UI by role, and
 * `IsInRole(role)` / `IsInRoles(roles)` decided membership. `IsInRoles` split a
 * ';'-delimited role string and returned TRUE if `objUserInfo.IsSuperUser` OR the user
 * was in ANY listed role, otherwise FALSE (OR-semantics + super-user override +
 * fail-closed). That decision is reproduced here EXACTLY: show if `isSuperUser()` OR
 * the user has ANY required role. This is a net-new (`from_scratch`) implementation;
 * no VB is transliterated line-by-line. The DNN provider-only pseudo-roles
 * ("All Users" / "Unauthenticated Users") are intentionally NOT carried into the JWT
 * model (the AAP replaces provider-based auth with JWT bearer). The directive READS
 * ONLY the `AuthService` auth-state signals (`roles()`, `isSuperUser()`) — NO token
 * handling, NO HTTP (those live in `core/auth`).
 */
@Directive({ selector: '[appHasPermission]' })
export class HasPermissionDirective {
  /**
   * The host template captured by the structural microsyntax (`*appHasPermission`).
   * MIGRATION: parameterized as `TemplateRef<unknown>` (TypeScript instantiation
   * expression) rather than `TemplateRef<any>` to keep the implementation strictly
   * typed under `strictInjectionParameters`; the context passed to
   * `createEmbeddedView` is `unknown` (i.e. none), which is optional.
   */
  private readonly templateRef = inject(TemplateRef<unknown>);

  /** The container into which the host template is (conditionally) embedded/cleared. */
  private readonly viewContainer = inject(ViewContainerRef);

  /** Root-singleton auth state provider; only its read-only signals are consumed. */
  private readonly authService = inject(AuthService);

  /**
   * The normalized set of roles that grant visibility. Backed by a signal so the
   * `canView` computed re-evaluates when a dynamic `[appHasPermission]` binding changes.
   * Starts empty (fail-closed: only super users pass until a role is supplied).
   */
  private readonly requiredRoles = signal<string[]>([]);

  /** Guards embed/clear so the embedded view is created/destroyed at most once per state. */
  private hasView = false;

  /**
   * The required role(s). Accepts a single role, an array of roles, or a
   * ';'- / ','-delimited string. The microsyntax `*appHasPermission="expr"` desugars to
   * `<ng-template [appHasPermission]="expr">`, so this input name MUST stay exactly
   * `appHasPermission`.
   */
  @Input()
  set appHasPermission(value: string | string[]) {
    this.requiredRoles.set(this.normalizeRoles(value));
  }

  /**
   * Authorization decision, recomputed reactively from the auth-state signals and the
   * required-role input.
   *
   * MIGRATION: reproduces `PortalSecurity.IsInRoles` OR-semantics plus the
   * `objUserInfo.IsSuperUser` override and the trailing `Return False` (fail-closed):
   *  - super user  -> always allowed;
   *  - no required roles specified -> denied (only super users pass);
   *  - otherwise    -> allowed if the user holds ANY of the required roles.
   */
  private readonly canView = computed<boolean>(() => {
    // Super-user override: mirrors `objUserInfo.IsSuperUser Or ...` short-circuit.
    if (this.authService.isSuperUser()) {
      return true;
    }
    const required = this.requiredRoles();
    // Fail-closed on an empty role set: mirrors IsInRoles' trailing `Return False`.
    if (required.length === 0) {
      return false;
    }
    // OR-semantics: allowed if the user is in ANY required role (IsInRole per role).
    const userRoles = this.authService.roles();
    return required.some((role) => userRoles.includes(role));
  });

  constructor() {
    // `effect` re-runs whenever `isSuperUser()`, `roles()`, or `requiredRoles()` change
    // (e.g. login/logout or a changed input), keeping visibility reactive WITHOUT any
    // manual subscription — the auth state is already exposed as signals by AuthService.
    effect(() => this.updateView(this.canView()));
  }

  /**
   * Normalizes the input into a clean list of non-empty, trimmed role names.
   * Arrays are used as-is (trimmed); strings are split on ';' or ',' — the ';' mirrors
   * the legacy `IsInRoles` `roles.Split(";"c)`, and ',' is accepted for convenience.
   */
  private normalizeRoles(value: string | string[]): string[] {
    const roles = Array.isArray(value) ? value : value.split(/[;,]/);
    return roles.map((role) => role.trim()).filter((role) => role.length > 0);
  }

  /**
   * Reflects the authorization decision into the DOM, creating the embedded view when
   * newly allowed and clearing it when newly denied. The `hasView` guard makes each
   * transition idempotent (no duplicate embeds, no redundant clears).
   *
   * @param allowed `true` to render the host template, `false` to remove it.
   */
  private updateView(allowed: boolean): void {
    if (allowed && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!allowed && this.hasView) {
      this.viewContainer.clear();
      this.hasView = false;
    }
  }
}
