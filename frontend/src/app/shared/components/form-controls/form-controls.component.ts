import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  booleanAttribute,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

import { AutofocusDirective } from '../../directives/autofocus';
import { ValidationHighlightDirective } from '../../directives/validation-highlight';

/**
 * Reusable, presentation-only label + input + inline-validation wrapper bound to a
 * typed Angular Reactive Forms control. Used by every feature create/edit form so
 * field-level validation (and RFC 7807 server-side field errors) render consistently.
 *
 * // MIGRATION: Reproduces the field-level validation feedback of the legacy DNN admin
 * // edit controls (Website/admin/Users/User.ascx.vb Validate()/valPassword.ErrorMessage +
 * // Security_EmailValidation ValidationExpression; Website/admin/Portal/SiteSettings.ascx.vb
 * // Page.IsValid-gated field validation) as inline reactive-form messages. The legacy
 * // ASP.NET validators + postback/ViewState model is replaced by client-side reactive
 * // validation; backend FluentValidation field errors arrive via ProblemDetails.errors
 * // (the serverErrors input). Presentation-only: no HTTP and no business logic here.
 */
@Component({
  selector: 'app-form-controls',
  templateUrl: './form-controls.component.html',
  styleUrl: './form-controls.component.scss',
  imports: [ReactiveFormsModule, ValidationHighlightDirective, AutofocusDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormControlsComponent {
  /** The bound reactive form control (a leaf FormControl). Required. */
  readonly control = input.required<FormControl>();

  /** Visible field label text. Rendered only when non-empty. */
  readonly label = input('');

  /** Stable DOM id used for the input id, the label `for`, the aria wiring and the server-error key. Required. */
  readonly controlId = input.required<string>();

  /** Native input type (e.g. text, email, password, number, tel, url, search). */
  readonly type = input('text');

  /**
   * Optional native `step` for `type="number"` inputs. QA #2: currency/decimal fields
   * (e.g. Service Fee, Trial Fee, Host Fee) must advertise `step="0.01"` so a legitimate
   * persisted value like `9.99` is NOT a step mismatch (the browser default `step=1` makes
   * `9.99` fail `validity.stepMismatch`, wrongly exposing `invalid="true"` to assistive tech
   * and stepping the spinner by whole units). The default (`null`) omits the attribute entirely
   * so integer fields (Users, Pages, Host Space, quotas, Time Zone Offset, …) keep the correct
   * whole-number `step=1` and existing fields are unaffected. `'any'` is also accepted to lift
   * step validation completely.
   */
  readonly step = input<string | number | null>(null);

  /** Optional placeholder text. */
  readonly placeholder = input('');

  /**
   * Optional native `autocomplete` hint (e.g. 'username', 'current-password',
   * 'new-password', 'email', 'off'). QA #11: lets identity/password fields advertise
   * the correct autocomplete semantics to browsers and password managers. The default
   * (null) omits the attribute entirely so existing fields are unaffected.
   */
  readonly autocomplete = input<string | null>(null);

  /** When true, focuses this field after the first render (drives [appAutofocus]). */
  readonly autofocus = input(false, { transform: booleanAttribute });

  /** Optional RFC 7807 ProblemDetails.errors map (field name -> messages) returned by the API. */
  readonly serverErrors = input<Record<string, string[]> | null>(null);

  /** Optional per-validator-key message overrides, e.g. { required: 'Name is required.' }. */
  readonly messages = input<Record<string, string>>({});

  /** Id of the inline error container, used for aria-describedby. */
  readonly errorId = computed(() => `${this.controlId()}-errors`);

  private static readonly FALLBACK_MESSAGE = 'This field is invalid.';

  private static readonly DEFAULT_MESSAGES = new Map<string, string>([
    ['required', 'This field is required.'],
    ['email', 'Please enter a valid email address.'],
    ['pattern', 'The value is not in the expected format.'],
    ['minlength', 'The value is too short.'],
    ['maxlength', 'The value is too long.'],
    ['min', 'The value is too small.'],
    ['max', 'The value is too large.'],
  ]);

  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  constructor() {
    // Reactive-form status/touched/pristine changes are exposed via RxJS, not signals.
    // Mark this OnPush component for check on every control event (value/status/touched/
    // pristine) — including markAllAsTouched() on submit and programmatic patchValue —
    // so the inline error messages stay in sync. The effect re-subscribes if the bound
    // control instance changes and cleans up on destroy.
    effect((onCleanup) => {
      const subscription = this.control().events.subscribe(() => this.changeDetectorRef.markForCheck());
      onCleanup(() => subscription.unsubscribe());
    });
  }

  /**
   * Builds the list of messages to display: client-side validator messages (only once the
   * control has been touched or modified, mirroring legacy post-submit display) followed by
   * any server-side field errors for this controlId. Implemented as a method (not a computed)
   * because it reads non-signal FormControl state (errors/touched/dirty) that must be
   * re-evaluated every change-detection pass.
   */
  visibleErrors(): string[] {
    const control = this.control();
    const messages: string[] = [];

    if (control.invalid && (control.touched || control.dirty)) {
      const errors = control.errors;
      if (errors) {
        for (const key of Object.keys(errors)) {
          messages.push(this.resolveMessage(key));
        }
      }
    }

    const server = this.serverErrors();
    if (server) {
      const fieldErrors = server[this.controlId()];
      if (fieldErrors) {
        for (const message of fieldErrors) {
          messages.push(message);
        }
      }
    }

    return messages;
  }

  private resolveMessage(key: string): string {
    const override = this.messages()[key];
    if (override) {
      return override;
    }
    return FormControlsComponent.DEFAULT_MESSAGES.get(key) ?? FormControlsComponent.FALLBACK_MESSAGE;
  }
}
