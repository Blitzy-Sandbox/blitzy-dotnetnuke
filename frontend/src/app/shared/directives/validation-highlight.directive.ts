import {
  Directive,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  OnInit,
  Renderer2,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgControl } from '@angular/forms';

/**
 * ValidationHighlightDirective — toggles error styling on a reactive-form control
 * host when the control is invalid AND has been interacted with (touched || dirty).
 *
 * Usage:
 * ```html
 * <input formControlName="email" appValidationHighlight>
 * ```
 *
 * The directive is opt-in: it activates only on elements that carry the
 * `appValidationHighlight` attribute AND resolve an {@link NgControl} on the same
 * element (via `formControlName` / `formControl`). Placing it on a non-form-control
 * element is a safe no-op. It never renders message text — that remains a component
 * concern; this directive owns only the visual highlight (`is-invalid` class) and the
 * `aria-invalid` screen-reader state.
 *
 * MIGRATION: re-expresses the legacy DotNetNuke ASP.NET Web Forms validator visual
 * feedback — `RequiredFieldValidator` / `RegularExpressionValidator` /
 * `CompareValidator` rendered with `CssClass="NormalRed"` and `Display="Dynamic"`
 * (verified in Website/admin/Portal/signup.ascx — valPortalName/valFirstName/
 * valLastName/valUsername/valPassword/valConfirm/valEmail — and
 * Website/admin/Security/editroles.ascx — valRoleName plus the valServiceFee/
 * valBillingPeriod/valTrialFee/valTrialPeriod CompareValidators). `Display="Dynamic"`
 * (show only when invalid) maps to the `invalid && (touched || dirty)` condition;
 * `CssClass="NormalRed"` maps to the `is-invalid` class (styled red in global styles,
 * the modern analog of the legacy `NormalRed` CSS class). The directive also sets
 * `aria-invalid` for screen-reader parity. This is a net-new implementation; no legacy
 * markup or code-behind is ported line-by-line.
 */
@Directive({ selector: '[appValidationHighlight]' })
export class ValidationHighlightDirective implements OnInit {
  /** CSS class applied to the host while the bound control is in an error state. */
  private static readonly INVALID_CLASS = 'is-invalid';

  /**
   * The form-control directive on the SAME element (`{ self: true }`). `{ optional: true }`
   * yields `null` when the host is not a form control, allowing the directive to no-op.
   */
  private readonly ngControl = inject(NgControl, { self: true, optional: true });

  /** The host element whose class / ARIA state this directive mutates. */
  private readonly host = inject(ElementRef) as ElementRef<HTMLElement>;

  /** Renderer used for all DOM mutations (class + attribute), keeping the directive platform-safe. */
  private readonly renderer = inject(Renderer2);

  /** Lifecycle handle used to auto-unsubscribe the control-events stream. */
  private readonly destroyRef = inject(DestroyRef);

  /** Reactive flag: `true` when the bound control should display its error state. */
  private readonly showError = signal(false);

  constructor() {
    // MIGRATION: Display="Dynamic" — reactively reflect validity into the DOM. The effect
    // only READS `showError` and mutates the DOM; it never writes a signal (no cyclic update).
    effect(() => this.applyState(this.showError()));
  }

  /**
   * Wires the bound control's state into the {@link showError} signal. Establishes the
   * initial state and subscribes to future changes.
   */
  ngOnInit(): void {
    const control = this.ngControl?.control;
    if (!control) {
      // No form control on the host — nothing to highlight; safe no-op.
      return;
    }

    // Seed the signal with the control's current state.
    this.updateFromControl();

    // AbstractControl.events (Angular 18+) is required here: `touched` transitions (on blur)
    // are NOT emitted by statusChanges / valueChanges, and `(touched || dirty)` parity with
    // Display="Dynamic" depends on observing touched changes. takeUntilDestroyed ties the
    // subscription lifetime to the directive instance. The callback takes no argument — the
    // emitted event is unused; we simply re-read the control's derived state.
    control.events
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateFromControl());
  }

  /**
   * Recomputes whether the error state should be shown from the current control snapshot and
   * pushes it into the signal. Defensively re-reads the control; when absent, clears the state.
   */
  private updateFromControl(): void {
    const control = this.ngControl?.control;
    const showError = control ? control.invalid && (control.touched || control.dirty) : false;
    this.showError.set(showError);
  }

  /**
   * Imperatively reflects the {@link showError} value onto the host: adds/removes the
   * `is-invalid` class and sets/removes `aria-invalid`. This is the single place that mutates
   * the DOM, invoked by the constructor's effect whenever the signal changes.
   *
   * @param showError `true` to apply the error styling and ARIA state, `false` to clear it.
   */
  private applyState(showError: boolean): void {
    const element = this.host.nativeElement;
    if (showError) {
      this.renderer.addClass(element, ValidationHighlightDirective.INVALID_CLASS);
      this.renderer.setAttribute(element, 'aria-invalid', 'true');
    } else {
      this.renderer.removeClass(element, ValidationHighlightDirective.INVALID_CLASS);
      this.renderer.removeAttribute(element, 'aria-invalid');
    }
  }
}
