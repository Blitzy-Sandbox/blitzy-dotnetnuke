import { afterNextRender, booleanAttribute, Directive, ElementRef, inject, input } from '@angular/core';

/**
 * `AutofocusDirective` moves keyboard focus to its host element once the view
 * has finished rendering. It is a generic, accessible UX enhancement intended
 * to focus the first field of administrative edit forms in the Angular SPA.
 *
 * Usage:
 * - Bare attribute (focus on render):      `<input appAutofocus>`
 * - Explicit binding (conditional focus):  `<input [appAutofocus]="shouldFocus">`
 * - Disabled (never steal focus):          `<input [appAutofocus]="false">`
 *
 * The directive performs no DOM manipulation beyond the standard `focus()` call
 * and only acts when enabled, so it never steals focus unexpectedly.
 */
@Directive({
  selector: '[appAutofocus]',
})
export class AutofocusDirective {
  /** Reference to the host element that will receive focus. */
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  /**
   * Controls whether the host element receives focus after render.
   * Defaults to `true`. `booleanAttribute` lets `<input appAutofocus>` (bare attribute -> '')
   * resolve to `true`, while `[appAutofocus]="false"` skips focusing.
   */
  readonly appAutofocus = input(true, { transform: booleanAttribute });

  constructor() {
    // `afterNextRender` runs once after the next render completes (by which time
    // the host element is in the DOM) and is SSR-safe -- it never executes during
    // server-side rendering, so `focus()` is only ever invoked in the browser.
    afterNextRender(() => {
      if (this.appAutofocus()) {
        this.elementRef.nativeElement.focus();
      }
    });
  }
}
