import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule, ValidationErrors } from '@angular/forms';

/**
 * Module-level monotonic counter used to mint a stable, unique fallback id when
 * the caller does not supply one. Guarantees `<label for>` / `<input id>` /
 * `aria-describedby` associations stay unique even with multiple instances.
 */
let uniqueControlId = 0;

/**
 * FormControlsComponent — the reusable "label + input + validation" primitive.
 *
 * Migrated from the repeated label/validator markup that every legacy Web Forms
 * admin control hand-rolled (e.g. `Website/admin/Modules/ModuleSettings.ascx`).
 * It is the single building block every feature form composes via
 * `<app-form-controls [control]="..." controlId="..." label="..." ...>`.
 *
 * Responsibilities:
 *  - Render an accessible label bound to the wrapped reactive control.
 *  - Render the input and reflect validity state visually (`is-invalid` /
 *    `is-valid`) and to assistive technology (`aria-invalid`,
 *    `aria-describedby`).
 *  - Surface BOTH client-side validation errors (derived from the control's
 *    `errors`) and server-side errors (the RFC 7807 `errors` dictionary the API
 *    returns — the authoritative, legacy-parity messages produced by the backend
 *    FluentValidation rules).
 *
 * Standalone by default (Angular 19) and OnPush for performance (AAP §0.3.4):
 * change detection runs for this component whenever the user interacts with the
 * hosted input or an input signal changes, which is exactly when the displayed
 * validity / error state can change.
 */
@Component({
  selector: 'app-form-controls',
  imports: [ReactiveFormsModule],
  templateUrl: './form-controls.component.html',
  styleUrl: './form-controls.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormControlsComponent {
  /** The reactive control this field wraps (required). */
  readonly control = input.required<FormControl>();

  /**
   * Stable id used for the `<label for>` / `<input id>` association and to look
   * up this field's server-side errors. Defaults to an auto-generated id so the
   * component is still accessible when a caller omits it.
   */
  readonly controlId = input<string>(`fc-${uniqueControlId++}`);

  /** Visible field label. */
  readonly label = input<string>('');

  /** Native input `type` (text, number, date, email, ...). */
  readonly type = input<string>('text');

  /** Optional input placeholder. */
  readonly placeholder = input<string>('');

  /**
   * Server-side validation errors keyed by control id, matching the RFC 7807
   * Problem Details `errors` shape (`field -> messages[]`) the API emits. These
   * carry the authoritative legacy-parity messages and are shown alongside any
   * client-side errors.
   */
  readonly serverErrors = input<Record<string, string[]> | null>(null);

  /** Id of the error container, derived from the control id. */
  get errorsId(): string {
    return `${this.controlId()}-errors`;
  }

  /** True when the control is invalid and the user has interacted with it. */
  get isInvalid(): boolean {
    const c = this.control();
    return c.invalid && (c.touched || c.dirty);
  }

  /** True when the control is valid and the user has interacted with it. */
  get isValid(): boolean {
    const c = this.control();
    return c.valid && (c.touched || c.dirty);
  }

  /** Server-side messages targeting this specific control. */
  get serverErrorMessages(): string[] {
    const all = this.serverErrors();
    if (!all) {
      return [];
    }
    return all[this.controlId()] ?? [];
  }

  /** Human-readable client-side messages derived from the control's errors. */
  get clientErrorMessages(): string[] {
    const c = this.control();
    if (!c.errors || !(c.touched || c.dirty)) {
      return [];
    }
    return this.mapErrors(c.errors);
  }

  /** All messages to display (server-side first, then client-side). */
  get allErrorMessages(): string[] {
    return [...this.serverErrorMessages, ...this.clientErrorMessages];
  }

  /** Whether any error message should currently be shown. */
  get showErrors(): boolean {
    return this.allErrorMessages.length > 0;
  }

  /**
   * Translate the control's {@link ValidationErrors} bag into display strings.
   * A validator may also supply a ready-made message (either a raw string value
   * or an object exposing a `message` property), which is surfaced verbatim so a
   * feature form can inject a precise, legacy-compatible message when needed.
   */
  private mapErrors(errors: ValidationErrors): string[] {
    return Object.keys(errors).map((key) => this.messageFor(key, errors[key]));
  }

  private messageFor(key: string, detail: unknown): string {
    switch (key) {
      case 'required':
        return `${this.label() || 'This field'} is required.`;
      case 'email':
        return 'Enter a valid email address.';
      case 'minlength': {
        const d = detail as { requiredLength?: number };
        return `Must be at least ${d.requiredLength} characters.`;
      }
      case 'maxlength': {
        const d = detail as { requiredLength?: number };
        return `Must be at most ${d.requiredLength} characters.`;
      }
      case 'min': {
        const d = detail as { min?: number };
        return `Must be at least ${d.min}.`;
      }
      case 'max': {
        const d = detail as { max?: number };
        return `Must be at most ${d.max}.`;
      }
      case 'pattern':
        return 'The value is not in the expected format.';
      default:
        if (typeof detail === 'string') {
          return detail;
        }
        if (detail && typeof detail === 'object' && 'message' in detail) {
          const message = (detail as { message?: unknown }).message;
          if (typeof message === 'string') {
            return message;
          }
        }
        return 'This field is invalid.';
    }
  }
}
