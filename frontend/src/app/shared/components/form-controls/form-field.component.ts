import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { switchMap } from 'rxjs';

import { AutofocusDirective, ValidationHighlightDirective } from '../../directives';

/**
 * Supported control renderings. `custom` projects a caller-supplied control
 * through <ng-content> (escape hatch); all others are rendered by this component
 * so the composed directives (appValidationHighlight/appAutofocus) are genuinely
 * used and Angular does not emit the NG8113 unused-standalone-import warning.
 */
export type FormFieldControlType =
  | 'text'
  | 'number'
  | 'email'
  | 'password'
  | 'tel'
  | 'url'
  | 'search'
  | 'textarea'
  | 'select'
  | 'custom';

/** A single option for the `select` control rendering. */
export interface FormFieldOption {
  readonly value: string | number;
  readonly label: string;
}

// Module-scoped counter guarantees a unique fallback id per instance so
// <label for> / control [id] / aria-describedby wiring is always valid.
let nextUniqueId = 0;

/**
 * FormFieldComponent — a reusable, PRESENTATION-ONLY, typed reactive form field.
 *
 * Renders a label + a typed control (input / textarea / select / projected custom)
 * + an inline validation message that appears only when the bound control is invalid
 * AND (touched OR dirty).
 *
 * MIGRATION: this is a behavioral re-expression (UI functional parity, AAP §0.7.1) of
 * the legacy DotNetNuke ASP.NET Web Forms input rows and their `asp:*Validator` server
 * controls — verified in `Website/admin/Portal/signup.ascx` (8 `RequiredFieldValidator`
 * controls, all `Display="Dynamic"`: PortalName, Template dropdown [InitialValue="-1"],
 * FirstName, LastName, Username, Password, Confirm, Email) and
 * `Website/admin/Security/editroles.ascx` (`RequiredFieldValidator` for RoleName plus the
 * ServiceFee/BillingPeriod/TrialFee/TrialPeriod `CompareValidator` pairs). It is NOT a
 * line-by-line port.
 *
 * PRESENTATION-ONLY (AAP §0.7.1): NO HTTP, NO business logic, NO validator wiring. The
 * parent feature component owns the `FormGroup`, attaches validators, and handles submit;
 * this component only DISPLAYS the state of the `FormControl` handed to it and renders the
 * correct message. Exact legacy validator text is supplied by the parent via
 * `[errorMessages]` when verbatim parity is required.
 *
 * ## Wiring a projected `custom` control (F8, a11y)
 * The built-in renderings (text/number/email/.../select/textarea) are wired for
 * accessibility automatically: the control receives `[id]="fieldId()"`, the label points at
 * it via `[for]`, and `aria-describedby`/`aria-required` link the hint and error. A projected
 * `custom` control, however, resolves in the PARENT template scope, so the parent must wire it
 * explicitly using the exported instance (`exportAs: 'appFormField'`):
 *
 * ```html
 * <app-form-field #ff="appFormField" controlType="custom"
 *                 label="Colour" hint="Pick one" [required]="true" [control]="colourCtrl">
 *   <input [id]="ff.fieldId()" [formControl]="colourCtrl"
 *          [attr.aria-describedby]="ff.describedBy()"
 *          [attr.aria-required]="ff.required() ? 'true' : null" />
 * </app-form-field>
 * ```
 *
 * The public members `fieldId()`, `describedBy()`, `required()`, `hintId()` and `errorId()`
 * exist specifically so callers can associate a projected control with this field's rendered
 * label, hint and error message.
 */
@Component({
  selector: 'app-form-field',
  // MIGRATION / F8 (a11y): exposes the component instance to the template so a caller
  // using controlType='custom' can obtain a reference (#ff="appFormField") and wire its
  // projected control to this field's generated id + ARIA metadata. See the class-level
  // "Wiring a projected `custom` control" doc block and the @case('custom') comment below.
  exportAs: 'appFormField',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, ValidationHighlightDirective, AutofocusDirective],
  template: `
    <div class="form-field">
      @if (label()) {
        <label class="form-field__label" [for]="fieldId()">
          <span class="form-field__label-text">{{ label() }}</span>
          @if (required()) {
            <span class="form-field__required" aria-hidden="true">*</span>
          }
        </label>
      }

      @switch (controlType()) {
        @case ('select') {
          <select
            class="form-field__control"
            [id]="fieldId()"
            [formControl]="control()"
            appValidationHighlight
            [appAutofocus]="autofocus()"
            [attr.aria-describedby]="describedBy()"
            [attr.aria-required]="required() ? 'true' : null"
          >
            @if (placeholder()) {
              <option value="" disabled>{{ placeholder() }}</option>
            }
            @for (option of options(); track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        }
        @case ('textarea') {
          <textarea
            class="form-field__control"
            [id]="fieldId()"
            [formControl]="control()"
            appValidationHighlight
            [appAutofocus]="autofocus()"
            [rows]="rows()"
            [attr.placeholder]="placeholder() || null"
            [attr.aria-describedby]="describedBy()"
            [attr.aria-required]="required() ? 'true' : null"
          ></textarea>
        }
        @case ('custom') {
          <!-- MIGRATION / F8 (a11y): escape hatch for a caller-projected control. The
               projected control resolves in the PARENT template scope, so this component
               cannot set its id / aria-* automatically. The caller obtains this instance
               via exportAs ('appFormField') and binds the exposed metadata so the projected
               control is associated with the rendered <label for>, hint and error:

                 <app-form-field #ff="appFormField" controlType="custom"
                                 label="Colour" hint="Pick one" [control]="colourCtrl">
                   <input [id]="ff.fieldId()" [formControl]="colourCtrl"
                          [attr.aria-describedby]="ff.describedBy()"
                          [attr.aria-required]="ff.required() ? 'true' : null" />
                 </app-form-field>

               fieldId(), describedBy(), required() (plus hintId()/errorId()) are all public. -->
          <ng-content></ng-content>
        }
        @default {
          <input
            class="form-field__control"
            [type]="controlType()"
            [id]="fieldId()"
            [formControl]="control()"
            appValidationHighlight
            [appAutofocus]="autofocus()"
            [attr.placeholder]="placeholder() || null"
            [attr.aria-describedby]="describedBy()"
            [attr.aria-required]="required() ? 'true' : null"
          />
        }
      }

      @if (hint()) {
        <small class="form-field__hint" [id]="hintId()">{{ hint() }}</small>
      }

      <!-- MIGRATION: legacy asp:*Validator Display="Dynamic" -> message rendered
           only when the control is invalid AND (touched OR dirty). -->
      @if (showError()) {
        <p class="form-field__error" [id]="errorId()" role="alert">{{ errorText() }}</p>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-bottom: var(--space-3, 0.75rem);
        font-family: var(--font-family-base, inherit);
      }

      .form-field__label {
        display: block;
        margin-bottom: var(--space-1, 0.25rem);
        font-weight: 600;
        color: var(--color-text, #1a1a1a);
      }

      .form-field__required {
        margin-left: 0.125rem;
        color: var(--color-danger, #dc3545);
      }

      .form-field__control {
        display: block;
        width: 100%;
        box-sizing: border-box;
        padding: var(--space-2, 0.5rem);
        font: inherit;
        color: var(--color-text, #1a1a1a);
        background-color: var(--color-surface, #ffffff);
        border: 1px solid var(--color-border, #ced4da);
        border-radius: var(--radius, 4px);
      }

      .form-field__control:focus {
        outline: 2px solid var(--color-primary, #0d6efd);
        outline-offset: 1px;
      }

      /* MIGRATION: legacy CssClass="NormalRed" invalid highlight. The
         ValidationHighlightDirective adds .is-invalid; styled defensively here
         (component-scoped) in case the global .is-invalid rule is absent. */
      .form-field__control.is-invalid {
        border-color: var(--color-danger, #dc3545);
      }

      .form-field__control.is-invalid:focus {
        outline-color: var(--color-danger, #dc3545);
      }

      .form-field__hint {
        display: block;
        margin-top: var(--space-1, 0.25rem);
        color: var(--color-muted, #6c757d);
      }

      /* MIGRATION: legacy validator inline red message text. */
      .form-field__error {
        margin: var(--space-1, 0.25rem) 0 0;
        color: var(--color-danger, #dc3545);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class FormFieldComponent {
  /** The parent-owned typed reactive control this field renders. Required. */
  readonly control = input.required<FormControl>();
  readonly label = input('');
  /** Optional explicit id; falls back to a generated unique id. */
  readonly controlId = input('');
  readonly controlType = input<FormFieldControlType>('text');
  /** Options for controlType 'select'. */
  readonly options = input<readonly FormFieldOption[]>([]);
  readonly placeholder = input('');
  readonly hint = input('');
  readonly rows = input(3);
  /** Renders the visual asterisk + aria-required (does NOT add a validator). */
  readonly required = input(false, { transform: booleanAttribute });
  readonly autofocus = input(false, { transform: booleanAttribute });
  /**
   * Overrides / additions to the default validation messages, keyed by the
   * ValidationErrors key (e.g. { required: '...', mismatch: 'Passwords ...' }).
   * Use this to reproduce EXACT legacy validator text for parity.
   */
  readonly errorMessages = input<Record<string, string>>({});

  private readonly uid = `app-form-field-${(nextUniqueId += 1)}`;

  readonly fieldId = computed(() => this.controlId() || this.uid);
  readonly errorId = computed(() => `${this.fieldId()}-error`);
  readonly hintId = computed(() => `${this.fieldId()}-hint`);

  // Bridges non-signal reactive-form state (status/touched/dirty/value) into a
  // signal so OnPush computeds recompute for user AND programmatic changes
  // (e.g. markAllAsTouched() on submit). AbstractControl.events (Angular 18+)
  // emits Value/Status/Pristine/Touched change events. toObservable re-subscribes
  // if the control input reference changes; switchMap cancels the prior stream.
  private readonly controlEvents = toSignal(
    toObservable(this.control).pipe(switchMap((control) => control.events)),
    { initialValue: null },
  );

  readonly showError = computed<boolean>(() => {
    // Register dependency on live control events (reactive-form state is not
    // signal-based) before reading derived control state.
    this.controlEvents();
    const control = this.control();
    return control.invalid && (control.touched || control.dirty);
  });

  readonly errorText = computed<string | null>(() => {
    if (!this.showError()) {
      return null;
    }
    const errors = this.control().errors;
    if (!errors) {
      return null;
    }
    const keys = Object.keys(errors);
    if (keys.length === 0) {
      return null;
    }
    // Bracket notation is mandatory: noPropertyAccessFromIndexSignature forbids
    // dot access on ValidationErrors / Record index signatures.
    const key = keys[0];
    const overrides = this.errorMessages();
    const override = overrides[key];
    if (override) {
      return override;
    }
    const errorValue: unknown = errors[key];
    return this.defaultMessage(key, errorValue);
  });

  readonly describedBy = computed<string | null>(() => {
    const ids: string[] = [];
    if (this.hint()) {
      ids.push(this.hintId());
    }
    if (this.showError()) {
      ids.push(this.errorId());
    }
    return ids.length > 0 ? ids.join(' ') : null;
  });

  // MIGRATION: default parity messages for the legacy validator families. Parents
  // supply exact legacy text via [errorMessages] when it must match verbatim
  // (e.g. editroles.ascx CompareValidator messages such as "Service Fee Value
  // Entered Is Not Valid", with the legacy leading <br> stripped for XSS-safe
  // interpolation).
  private defaultMessage(key: string, value: unknown): string {
    const label = this.label() || 'This field';
    switch (key) {
      case 'required':
        // MIGRATION: asp:RequiredFieldValidator (signup.ascx valPortalName/
        // valFirstName/valLastName/valUsername/valPassword/valConfirm/valEmail;
        // editroles.ascx valRoleName).
        return `${label} is required.`;
      case 'email':
        return 'Please enter a valid email address.';
      case 'pattern':
        // MIGRATION: asp:RegularExpressionValidator / CompareValidator
        // Operator="DataTypeCheck" (numeric/currency type parity).
        return `${label} is not in a valid format.`;
      case 'min': {
        // MIGRATION: asp:CompareValidator Operator="GreaterThanEqual"
        // ValueToCompare="0" (editroles.ascx ServiceFee/TrialFee) -> Validators.min(0).
        const min = this.numberFrom(value, 'min');
        return min !== null
          ? `${label} must be greater than or equal to ${min}.`
          : `${label} is below the minimum allowed value.`;
      }
      case 'max': {
        const max = this.numberFrom(value, 'max');
        return max !== null
          ? `${label} must be less than or equal to ${max}.`
          : `${label} exceeds the maximum allowed value.`;
      }
      case 'minlength': {
        const requiredLength = this.numberFrom(value, 'requiredLength');
        return requiredLength !== null
          ? `${label} must be at least ${requiredLength} characters.`
          : `${label} is too short.`;
      }
      case 'maxlength': {
        const requiredLength = this.numberFrom(value, 'requiredLength');
        return requiredLength !== null
          ? `${label} must be no more than ${requiredLength} characters.`
          : `${label} is too long.`;
      }
      default:
        // Unknown/custom key with no override: generic fallback.
        return `${label} is invalid.`;
    }
  }

  // Safely reads a numeric property from an unknown ValidationErrors payload
  // without using `any` (satisfies strict TS + no-explicit-any).
  private numberFrom(value: unknown, property: string): number | null {
    if (value !== null && typeof value === 'object' && property in value) {
      const raw = (value as Record<string, unknown>)[property];
      return typeof raw === 'number' ? raw : null;
    }
    return null;
  }
}
