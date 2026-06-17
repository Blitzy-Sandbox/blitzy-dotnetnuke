import type { CanDeactivateFn } from '@angular/router';
import type { Observable } from 'rxjs';

/**
 * Contract a routed component opts into so the {@link unsavedChangesGuard} can ask
 * it whether it is safe to navigate away. A component returns:
 *   - `true`  — allow navigation (nothing to lose, or the user confirmed leaving);
 *   - `false` — block navigation (the user chose to stay);
 *   - a `Promise`/`Observable<boolean>` — when the decision is asynchronous
 *     (e.g. awaiting a confirmation dialog).
 *
 * The current consumer ({@link RoleFormComponent}) implements this synchronously
 * with a native `window.confirm`, but the wider return type keeps the guard
 * reusable for components that defer to an async dialog later.
 */
export interface CanComponentDeactivate {
  canDeactivate(): boolean | Promise<boolean> | Observable<boolean>;
}

/**
 * Functional CanDeactivate route guard that protects forms with unsaved edits
 * from being abandoned by accident (QA — Role form: dirty values were silently
 * lost on browser Back/Forward without any warning).
 *
 * It is intentionally generic: it owns NO form/dirty logic itself, instead
 * delegating the decision to the deactivating component's `canDeactivate()`
 * (the {@link CanComponentDeactivate} contract). This keeps per-form rules
 * (what counts as "dirty", whether a save just completed, what the prompt says)
 * co-located with the component that knows them, while this guard simply wires
 * the decision into the router. Applied in a feature route table as
 * `canDeactivate: [unsavedChangesGuard]`.
 *
 * Because the guard runs for ALL navigations away from the route — sidebar
 * links, programmatic navigation, and crucially browser Back/Forward (popstate)
 * — it closes the exact gap the QA finding reported. Angular 19's default
 * `canceledNavigationResolution: 'replace'` keeps the address bar in sync when a
 * back/forward navigation is cancelled.
 *
 * MIGRATION: The legacy DNN Web Forms "Edit Roles" control performed full-page
 * postbacks with no client router and therefore had no unsaved-changes
 * protection; this guard is a new Angular-only safeguard with no 1:1 legacy
 * source (recorded in the root `MIGRATION_NOTES.md`).
 *
 * The guard is defensive: if a component is wired to the guard but does not
 * implement `canDeactivate`, navigation is allowed (fail-open) so the guard can
 * never strand a user on a screen it does not understand.
 */
export const unsavedChangesGuard: CanDeactivateFn<CanComponentDeactivate> = (component) => {
  return typeof component?.canDeactivate === 'function' ? component.canDeactivate() : true;
};
