/**
 * Standalone, presentation-only async loading indicator (`<app-loading-spinner>`).
 *
 * A reusable, accessible spinner shared across every feature area (portal,
 * module, user, role, auth) to signal in-flight asynchronous work. It is driven
 * entirely by its inputs and contains no HTTP calls, no domain logic and no
 * injected services -- a pure leaf component (AAP Section 0.3.4: standalone
 * components, signals, OnPush change detection).
 *
 * The template and styles live in the sibling `loading-spinner.component.html`
 * and `loading-spinner.component.scss` files (referenced via `templateUrl` /
 * `styleUrl`); this file owns only the component class and its public input
 * contract. The companion `index.ts` re-exports the class and
 * `loading-spinner.component.spec.ts` tests it.
 *
 * MIGRATION: NEW SPA affordance with no legacy DotNetNuke equivalent -- the
 * Web Forms admin controls (e.g. Portals.ascx.vb, ManageUsers.ascx.vb) used
 * server-rendered postbacks and contain no client-side loading/spinner
 * construct. This component replaces that implicit full-page-postback wait with
 * an explicit, accessible client-side indicator.
 *
 * @example Bare element (defaults: visible, "Loading...", 40px):
 *   <app-loading-spinner />
 * @example Parent-driven visibility and custom message/size:
 *   <app-loading-spinner [loading]="isSaving()" message="Saving portal..." [diameter]="64" />
 */
import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
  numberAttribute,
} from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.component.html',
  styleUrl: './loading-spinner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadingSpinnerComponent {
  /** When truthy, the spinner and its `role="status"` live region are rendered. Defaults to `true`. */
  readonly loading = input(true, { transform: booleanAttribute });

  /** Visually-hidden status text announced to assistive technology. Defaults to `'Loading...'`. */
  readonly message = input('Loading...');

  /** Outer diameter of the spinner in pixels. Defaults to `40`. */
  readonly diameter = input(40, { transform: numberAttribute });
}
