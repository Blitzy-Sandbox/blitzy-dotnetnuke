import { Injectable, signal } from '@angular/core';

/**
 * Known feature/module permission keys honored across the admin UI. Mirrors the
 * backend authorization keys (AAP §0.6.2): VIEW / EDIT / DELETE / MANAGE_SETTINGS.
 * The closed union makes an unknown key a compile-time error at call sites and a
 * never-granted value at runtime (fail-closed).
 */
export type PermissionKey = 'VIEW' | 'EDIT' | 'DELETE' | 'MANAGE_SETTINGS';

/**
 * PermissionService — the single source of truth for the current user's granted
 * permissions in the SPA. It replaces the legacy
 * `PortalSecurity.HasNecessaryPermission` server-side gate (AAP §0.6.2) for the
 * purpose of UI affordance gating; the server remains the authoritative
 * enforcement point.
 *
 * Design:
 *  - Signal-based so consumers (e.g. the `*appHasPermission` structural
 *    directive) reactively show/hide affordances when permissions change.
 *  - FAIL-CLOSED: the granted set defaults to EMPTY. Every gated affordance is
 *    hidden until permissions are explicitly loaded (typically from the
 *    authenticated user's claims after login), so a missing/incomplete load can
 *    never accidentally expose a privileged control.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  /**
   * Backing set of granted permission keys. Starts EMPTY (fail-closed).
   * `ReadonlySet` prevents external mutation of the emitted value.
   */
  private readonly granted = signal<ReadonlySet<PermissionKey>>(new Set<PermissionKey>());

  /**
   * Read-only signal of the currently granted keys, for consumers that want to
   * react to permission changes.
   */
  readonly permissions = this.granted.asReadonly();

  /**
   * Replace the full set of granted permission keys (e.g. after login, from the
   * authenticated user's claims).
   */
  setPermissions(keys: Iterable<PermissionKey>): void {
    this.granted.set(new Set<PermissionKey>(keys));
  }

  /** Clear all granted permissions (e.g. on logout); returns to fail-closed. */
  clear(): void {
    this.granted.set(new Set<PermissionKey>());
  }

  /**
   * True only when `key` has been explicitly granted. Ungranted (and, by the
   * closed union, unknown) keys return false — fail-closed.
   */
  hasPermission(key: PermissionKey): boolean {
    return this.granted().has(key);
  }
}
