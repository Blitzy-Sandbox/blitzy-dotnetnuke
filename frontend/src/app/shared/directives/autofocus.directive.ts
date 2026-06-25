// MIGRATION: Net-new presentational accessibility helper. The legacy DotNetNuke admin UI
// (Website/admin/**) used ASP.NET Web Forms postback/ViewState and performed NO client-side focus
// management (verified: zero .Focus()/SetFocus/autofocus usages under Website/admin). The migrated
// Angular admin dialogs/forms (shared/components: confirmation-dialog, form-controls) need initial
// focus management client-side (e.g. focus the Confirm button, or the first empty/invalid field).
// Re-expressed here as a standalone attribute directive. No 1:1 legacy file.
import {
  AfterViewInit,
  Directive,
  ElementRef,
  booleanAttribute,
  inject,
  input,
} from '@angular/core';

/**
 * Moves browser focus to the host element after its view initializes.
 *
 * Usage:
 *   <button appAutofocus>Confirm</button>          // focuses by default (bare attribute)
 *   <input [appAutofocus]="isFirstInvalid" />      // focuses only when the expression is true
 *
 * Presentational only: performs a DOM focus() and nothing else (no API/HttpClient/Router/state).
 */
@Directive({
  selector: '[appAutofocus]',
  standalone: true,
})
export class AutofocusDirective implements AfterViewInit {
  // ElementRef injected via inject() (NOT constructor injection) per AAP §0.7.3.
  // Typed as ElementRef<HTMLElement> so nativeElement.focus() is strongly typed (no `any`).
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /**
   * Optional control input aliased to the selector. Defaults to true so that a bare
   * `appAutofocus` attribute enables focusing. `booleanAttribute` coerces the empty-string value
   * of a bare attribute to `true`, and the string/boolean `"false"` to `false`, so both
   * `appAutofocus` and `[appAutofocus]="condition"` behave correctly.
   */
  readonly enabled = input(true, { alias: 'appAutofocus', transform: booleanAttribute });

  ngAfterViewInit(): void {
    if (this.enabled()) {
      this.host.nativeElement.focus();
    }
  }
}
