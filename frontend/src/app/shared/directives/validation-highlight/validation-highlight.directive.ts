import {
  Directive,
  DestroyRef,
  ElementRef,
  OnInit,
  Renderer2,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgControl } from '@angular/forms';

/**
 * ValidationHighlightDirective — toggles validation CSS classes on a reactive-form
 * control's host element so the field is visually highlighted once it is invalid AND
 * the user has interacted with it (touched or dirty).
 *
 * Usage: `<input formControlName="email" appValidationHighlight />`
 *
 * The directive adds `is-invalid` when the bound control is invalid after interaction,
 * and `is-valid` when it is valid after interaction. When applied without a bound form
 * control it degrades gracefully (does nothing, never throws). It performs visual
 * highlighting ONLY — it does not render error messages, evaluate domain validation
 * rules, or perform any HTTP/business logic.
 *
 * // MIGRATION: The legacy DNN admin edit controls (Website/admin/Users/User.ascx.vb,
 * // Website/admin/Portal/SiteSettings.ascx.vb) used ASP.NET validators
 * // (RequiredFieldValidator / RegularExpressionValidator / CustomValidator) that, after a
 * // postback, set an IsValid flag + ErrorMessage and applied a CSS class (e.g.
 * // CssClass="NormalRed") to mark invalid fields, gating save on Page.IsValid. This
 * // directive reproduces ONLY the client-side VISUAL marking by toggling `is-invalid`
 * // reactively (no postback). The validation RULES are reproduced in the feature
 * // reactive-form Validators + backend FluentValidation, and error MESSAGES are rendered
 * // by the shared form-controls component — not in this directive.
 */
@Directive({
  selector: '[appValidationHighlight]',
})
export class ValidationHighlightDirective implements OnInit {
  /** CSS class applied when the control is invalid after the user has interacted with it. */
  private static readonly INVALID_CLASS = 'is-invalid';
  /** CSS class applied when the control is valid after the user has interacted with it. */
  private static readonly VALID_CLASS = 'is-valid';

  // Optional NgControl so the directive degrades gracefully when applied without a bound
  // form control (e.g. on a plain element). Resolves to `NgControl | null`.
  private readonly ngControl = inject(NgControl, { optional: true });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    const control = this.ngControl?.control;
    if (!control) {
      // No bound form control (directive applied without a control) — degrade gracefully.
      return;
    }

    // Paint once on init (covers pre-populated edit forms that arrive dirty/touched).
    this.applyHighlight();

    // Validity transitions (e.g. value edits flipping valid<->invalid) emit on statusChanges;
    // value edits also mark the control dirty and emit on valueChanges.
    control.statusChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyHighlight());
    control.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyHighlight());

    // `touched` is set on blur and does NOT emit on statusChanges/valueChanges, so listen
    // for blur to reflect the touched transition. Registered after the value accessor's own
    // blur handler (which runs in the create phase), so `control.touched` is already updated
    // when this fires. Cleaned up with the directive via DestroyRef.
    const unlisten = this.renderer.listen(this.host.nativeElement, 'blur', () =>
      this.applyHighlight(),
    );
    this.destroyRef.onDestroy(unlisten);
  }

  /**
   * Recomputes and applies the validation highlight classes. Idempotent — safe to call on
   * every status/value/blur emission because Renderer2 add/remove class is a no-op when the
   * class is already in the desired state.
   */
  private applyHighlight(): void {
    const control = this.ngControl?.control;
    if (!control) {
      return;
    }

    const element = this.host.nativeElement;
    const interacted = control.touched || control.dirty;

    this.toggleClass(
      element,
      ValidationHighlightDirective.INVALID_CLASS,
      control.invalid && interacted,
    );
    this.toggleClass(
      element,
      ValidationHighlightDirective.VALID_CLASS,
      control.valid && interacted,
    );
  }

  /** Adds the class when `shouldApply` is true, otherwise removes it. Platform-agnostic via Renderer2. */
  private toggleClass(element: HTMLElement, className: string, shouldApply: boolean): void {
    if (shouldApply) {
      this.renderer.addClass(element, className);
    } else {
      this.renderer.removeClass(element, className);
    }
  }
}
