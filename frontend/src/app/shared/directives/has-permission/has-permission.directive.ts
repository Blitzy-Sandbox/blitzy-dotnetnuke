import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
} from '@angular/core';

import { PermissionKey, PermissionService } from '../../../core/services/permission.service';

/**
 * HasPermissionDirective — structural directive that renders its host template
 * only when the current user holds the required permission key. Mirrors the
 * legacy `PortalSecurity.HasNecessaryPermission` gate (AAP §0.6.2) for the
 * Angular UI; the server remains the authoritative enforcement point.
 *
 * Usage:
 *   `<button *appHasPermission="'DELETE'">Delete</button>`
 *   `<section *appHasPermission="'MANAGE_SETTINGS'"> ... </section>`
 *
 * Reactivity & safety:
 *  - Standalone by default (Angular 19).
 *  - An `effect` re-evaluates whenever EITHER the required key OR the granted
 *    permission set (a signal on {@link PermissionService}) changes, attaching or
 *    detaching the embedded view accordingly.
 *  - FAIL-CLOSED: because {@link PermissionService} starts with an empty granted
 *    set, the host template stays hidden until permissions are explicitly loaded.
 */
@Directive({
  selector: '[appHasPermission]',
})
export class HasPermissionDirective {
  private readonly templateRef: TemplateRef<unknown> = inject(TemplateRef);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly permissions = inject(PermissionService);

  /** Required permission key — the value bound to `*appHasPermission`. */
  readonly appHasPermission = input.required<PermissionKey>();

  /** Tracks whether the embedded view is currently attached. */
  private hasView = false;

  constructor() {
    effect(() => {
      // Reading both signals registers this effect as their dependent, so the
      // view is re-synced whenever the required key or the granted set changes.
      const granted = this.permissions.hasPermission(this.appHasPermission());

      if (granted && !this.hasView) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.hasView = true;
      } else if (!granted && this.hasView) {
        this.viewContainer.clear();
        this.hasView = false;
      }
    });
  }
}
