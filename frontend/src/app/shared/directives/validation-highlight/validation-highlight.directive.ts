import {
  Directive,
  DestroyRef,
  ElementRef,
  OnInit,
  Renderer2,
  booleanAttribute,
  effect,
  inject,
  input,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgControl } from '@angular/forms';

/**
 * ValidationHighlightDirective — toggles validation CSS classes on a reactive-form
 * control's host element so the field's BORDER stays in lock-step with the error state
 * the user actually sees (the inline messages and `aria-invalid`).
 *
 * Usage:
 * ```html
 * <!-- bare: border driven purely by the control's own client-side validity -->
 * <input formControlName="email" appValidationHighlight />
 *
 * <!-- driven: a parent (e.g. the shared form-controls component) can additionally force the
 *      error state so server-side field errors also paint the border -->
 * <input formControlName="email" appValidationHighlight [appValidationHighlightError]="hasVisibleErrors" />
 * ```
 *
 * Behavior:
 * - Adds `is-invalid` when EITHER the bound control is invalid AND has been interacted with
 *   (touched or dirty), OR the optional `appValidationHighlightError` input is `true`
 *   (an externally-supplied error signal — typically server-side RFC 7807 field errors that
 *   the control itself knows nothing about).
 * - Adds `is-valid` when the control is valid AND interacted with AND no external error is
 *   signalled (a field that is displaying an error must never show the green "valid" border).
 * - Reacts to the control's unified `events` stream (value, status, pristine AND touched), so a
 *   programmatic `markAllAsTouched()` on submit repaints the border just like a real blur does —
 *   and to changes of the `appValidationHighlightError` input, so server-side errors arriving
 *   after a submit repaint the border too.
 * - Paints once on init (covers pre-populated edit forms that arrive dirty/touched).
 * - Degrades gracefully when applied without a bound form control (does nothing, never throws).
 *
 * Scope: VISUAL marking only. The validation RULES (required, pattern, custom) live in the
 * feature reactive-form `Validators` + backend FluentValidation, and error MESSAGES are rendered
 * by the shared `form-controls` component — never in this directive.
 *
 * // MIGRATION: The legacy DNN admin edit controls (Website/admin/Users/User.ascx.vb,
 * // Website/admin/Portal/SiteSettings.ascx.vb) used ASP.NET validators
 * // (RequiredFieldValidator / RegularExpressionValidator / CustomValidator) that, after a
 * // postback, set an IsValid flag + ErrorMessage and applied a CSS class (e.g.
 * // CssClass="NormalRed") to mark EVERY invalid field — including server/CustomValidator
 * // failures — gating save on Page.IsValid. This directive reproduces that VISUAL marking
 * // client-side (no postback): the control's `events` stream covers the submit-time
 * // markAllAsTouched() transition, and the optional `appValidationHighlightError` input lets the
 * // shared form-controls component mark fields whose error came from the server (ProblemDetails.
 * // errors) — so a field that DISPLAYS an error always SHOWS the error border, exactly as the
 * // legacy NormalRed marking did. The validation RULES live in the feature reactive-form
 * // Validators + backend FluentValidation, and error MESSAGES are rendered by the shared
 * // form-controls component — not in this directive.
 */
@Directive({
  selector: '[appValidationHighlight]',
})
export class ValidationHighlightDirective implements OnInit {
  /** CSS class applied to a control that is invalid (client- or server-side) and interacted with. */
  private static readonly INVALID_CLASS = 'is-invalid';
  /** CSS class applied to a control that is valid, interacted with, and free of any error. */
  private static readonly VALID_CLASS = 'is-valid';

  /**
   * Optional external error signal. When `true` the host is marked invalid regardless of the
   * control's own client-side validity, and the "valid" (green) class is suppressed. This lets the
   * shared `form-controls` component drive the border off the SAME `visibleErrors()` state it uses
   * for the inline messages and `aria-invalid`, so a field that displays a server-side
   * (RFC 7807 ProblemDetails) error always shows an error border instead of a stale green/gray one.
   * Defaults to `false`, so bare `appValidationHighlight` usages (e.g. the module-form/-settings
   * checkboxes and selects) are entirely unaffected and keep their pure control-driven behavior.
   */
  readonly errorState = input(false, {
    alias: 'appValidationHighlightError',
    transform: booleanAttribute,
  });

  /** Optional so the directive degrades gracefully when no form control is bound. */
  private readonly ngControl = inject(NgControl, { optional: true });
  /** Host element wrapper; typed as `HTMLElement` so `nativeElement` is precise. */
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  /** Platform-agnostic DOM mutation (never write to `classList` directly). */
  private readonly renderer = inject(Renderer2);
  /** Tears down the control-events subscription with the directive. */
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    // Repaint whenever the external error signal changes — e.g. server-side ProblemDetails errors
    // arriving (or clearing) on a control the client considers valid, which emits nothing on the
    // control's own event stream. The effect also runs once on the first change-detection pass,
    // painting the initial state alongside the explicit init paint below.
    effect(() => {
      // Read the input so the effect re-runs on every change; applyHighlight() reads it again to
      // compute the classes (and reads the non-signal control state, which is intentionally untracked).
      this.errorState();
      this.applyHighlight();
    });
  }

  ngOnInit(): void {
    const control = this.ngControl?.control;
    if (!control) {
      // No bound form control (directive applied without a control) — degrade gracefully.
      return;
    }

    // Paint once on init (covers pre-populated edit forms that arrive dirty/touched).
    this.applyHighlight();

    // React to the control's UNIFIED event stream. Unlike the previous statusChanges + valueChanges
    // + manual `blur` listener, `events` also emits a TouchedChangeEvent — so a programmatic
    // markAllAsTouched() on submit (which changes neither value nor status, and fires no DOM blur)
    // now repaints the border, matching the inline message + aria-invalid the form-controls
    // component already shows. Cleaned up with the directive via DestroyRef.
    control.events
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyHighlight());
  }

  /**
   * Re-evaluates the bound control plus the external error signal and toggles the validation
   * classes. Idempotent: `Renderer2.addClass`/`removeClass` are safe to call on every emission.
   */
  private applyHighlight(): void {
    const control = this.ngControl?.control;
    if (!control) {
      return;
    }

    const element = this.host.nativeElement;
    const interacted = control.touched || control.dirty;
    const externalError = this.errorState();

    // A field is "in error" if its own validators failed once interacted, OR an external error
    // (e.g. a server-side field error) has been signalled — so the border always matches the
    // displayed message / aria-invalid.
    this.toggleClass(
      element,
      ValidationHighlightDirective.INVALID_CLASS,
      (control.invalid && interacted) || externalError,
    );

    // The green "valid" border is suppressed while any error (client or server) is in effect.
    this.toggleClass(
      element,
      ValidationHighlightDirective.VALID_CLASS,
      control.valid && interacted && !externalError,
    );
  }

  /** Adds or removes a single class on the host element via Renderer2. */
  private toggleClass(
    element: HTMLElement,
    className: string,
    shouldApply: boolean,
  ): void {
    if (shouldApply) {
      this.renderer.addClass(element, className);
    } else {
      this.renderer.removeClass(element, className);
    }
  }
}
