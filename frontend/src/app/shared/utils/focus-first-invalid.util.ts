// MIGRATION: [QA F4-003] Net-new shared accessibility helper (no legacy VB analog). The legacy DNN
// admin user controls relied on ASP.NET ValidationSummary + per-field RequiredFieldValidator, which
// (via postback) re-rendered the page so the validation messages were visible. The Angular SPA does
// NOT reload on an invalid submit, so on a long form (e.g. portal-form, whose Create button sits well
// below seven required fields) clicking submit produced ZERO visible feedback -- the first invalid
// control was off-screen and focus stayed on the button. This helper restores the expected behavior:
// after markAllAsTouched(), it moves focus to the first invalid control and scrolls it into view so
// the user (keyboard, screen-reader, or sighted) immediately sees WHY submission failed.
//
// It is intentionally a plain function (not a service) taking the component host element, so each
// standalone form can call it with `focusFirstInvalidControl(this.host.nativeElement)` after
// markAllAsTouched(). Querying the component's own host (rather than `document`) scopes the search to
// the active form and avoids matching invalid controls elsewhere in the app shell.

// The interactive controls that can carry the Angular `.ng-invalid` class. Mirrors the projected-control
// selector used by FormControlComponent so the first invalid Reactive-Forms control is reliably found.
const INVALID_CONTROL_SELECTOR = [
  'input.ng-invalid',
  'select.ng-invalid',
  'textarea.ng-invalid',
  '[contenteditable="true"].ng-invalid',
  '[role="textbox"].ng-invalid',
  '[role="combobox"].ng-invalid',
  '[role="spinbutton"].ng-invalid',
  '[role="listbox"].ng-invalid',
].join(', ');

/**
 * Move focus to (and scroll into view) the first invalid Reactive-Forms control inside `host`.
 *
 * Call this AFTER `form.markAllAsTouched()` on the invalid-submit path. When every control is valid
 * (nothing matches `.ng-invalid`) it is a no-op. `scrollIntoView` is guarded so the helper is safe in
 * non-browser/test environments that may not implement it.
 *
 * @param host The component's host element (`ElementRef.nativeElement`).
 */
export function focusFirstInvalidControl(host: HTMLElement | null | undefined): void {
  if (host == null) {
    return;
  }
  const firstInvalid = host.querySelector<HTMLElement>(INVALID_CONTROL_SELECTOR);
  if (firstInvalid === null) {
    return;
  }
  firstInvalid.focus();
  // `block: 'center'` (default 'auto' behavior) brings the field to the middle of the viewport without
  // an animated scroll -- intentionally NOT { behavior: 'smooth' } so users with a reduced-motion
  // preference are not subjected to a motion the app otherwise suppresses (AAP Section 0.7.7).
  if (typeof firstInvalid.scrollIntoView === 'function') {
    firstInvalid.scrollIntoView({ block: 'center' });
  }
}
