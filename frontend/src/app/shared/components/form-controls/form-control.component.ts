// MIGRATION: Net-new reusable typed-form-control wrapper. Generalizes the per-field
// "label + validation error" pattern that the legacy DotNetNuke admin user controls under
// Website/admin/** implemented with ASP.NET Web Forms server controls -- a <dnn:label
// controlname="..."> caption associated to an input, a <asp:RequiredFieldValidator
// controltovalidate="..." errormessage="..." cssclass="NormalRed" display="Dynamic"> inline
// error, and a <asp:ValidationSummary> form-level summary -- all driven by ViewState/postback.
// Re-expressed here as a PRESENTATIONAL Angular 19 standalone wrapper that renders a label,
// optional hint, and RFC 7807 field errors around a projected Reactive-Forms control, and wires
// the equivalent accessibility attributes. It NEVER fetches and holds no business logic. There is
// no 1:1 legacy file -- this is a cross-cutting UI pattern.
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Renderer2,
  computed,
  contentChild,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
// MIGRATION: [QA F4-002 / F4-004] reactive-forms primitives used to inspect the PROJECTED control's
// CLIENT validation state (NgControl), to recognise the built-in `required` validator (aria-required),
// and to type the ValidationErrors map. NgControl is provided by formControlName/formControl/ngModel,
// so a content query for it resolves the directive bound to the projected input.
import { NgControl, Validators, type ValidationErrors } from '@angular/forms';

import type { ProblemDetails } from '../../../core/models';

// The single interactive control projected into the wrapper. Covers native form controls and the
// common ARIA widget roles so the accessibility attributes can be wired onto whichever is present.
const PROJECTED_CONTROL_SELECTOR =
  'input, select, textarea, [contenteditable="true"], [role="textbox"], [role="combobox"], [role="spinbutton"], [role="listbox"]';

@Component({
  selector: 'app-form-control',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="form-control">
      @if (label()) {
        <label #labelEl class="form-control__label" [id]="labelId()">{{ label() }}</label>
      }
      <div class="form-control__field">
        <ng-content></ng-content>
      </div>
      @if (hint()) {
        <p class="form-control__hint" [id]="hintId()">{{ hint() }}</p>
      }
      @if (hasErrors()) {
        <div class="form-control__errors" [id]="errorId()" role="alert">
          @for (message of allErrors(); track message) {
            <p class="form-control__error">{{ message }}</p>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      .form-control {
        display: block;
        margin-bottom: var(--space-4);
      }

      .form-control__label {
        display: block;
        margin-bottom: var(--space-1);
        font-weight: 600;
        color: var(--color-text);
      }

      .form-control__field {
        display: block;
      }

      .form-control__hint {
        margin: var(--space-1) 0 0;
        color: var(--color-text-muted);
        font-size: 0.875rem;
      }

      .form-control__errors {
        margin: var(--space-1) 0 0;
      }

      .form-control__error {
        margin: 0;
        color: var(--color-danger);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class FormControlComponent {
  // Angular primitives injected via inject() (AAP 0.7.3). Used ONLY for presentational ARIA
  // wiring on the projected control -- no business logic, no data access. The explicit
  // ElementRef<HTMLElement> generic avoids an implicit-any nativeElement under strict mode.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);

  // Reactive handle to the rendered <label> element. Because it is inside an @if (label())
  // block, the query result is undefined until the template actually renders the label. Reading
  // this signal inside the wiring effect ties the effect's re-run to the label's REAL DOM
  // presence (not merely to the label() input value), eliminating the signal-vs-view race where
  // the effect observed the new label() value before the @if had created the <label> node.
  private readonly labelRef = viewChild<ElementRef<HTMLElement>>('labelEl');

  // MIGRATION: [QA F4-002 / F4-004] reactive handle to the PROJECTED Reactive-Forms control. NgControl is
  // provided by formControlName/formControl/ngModel, so this content query resolves the directive bound to
  // the projected <input>/<select>/<textarea>. It is `undefined` when the consumer projects a control with
  // NO NgControl (e.g. a plain <input> in a unit test) -- in which case the client-error path stays inert and
  // the component behaves exactly as before (server-[errors]-only). `descendants: true` finds the control
  // even when a consumer wraps it in a container element.
  private readonly projectedNgControl = contentChild(NgControl, { descendants: true });

  // MIGRATION: [QA F4-002] a monotonically-increasing tick bumped on every projected-control event
  // (value/status/touched). AbstractControl exposes `valid`/`invalid`/`touched`/`errors` as plain getters
  // (NOT signals), so an explicit subscription (see constructor) drives this signal to make the client-error
  // computeds re-evaluate when the control's validity or touched state changes (e.g. after markAllAsTouched()).
  private readonly controlTick = signal(0);

  /** Visible field label (also exposed to the projected control via aria-labelledby). */
  readonly label = input<string>('');

  /** Optional helper text rendered beneath the control. */
  readonly hint = input<string>();

  /** Field key used to look up RFC 7807 errors and to derive stable element ids. */
  readonly fieldKey = input.required<string>();

  /**
   * RFC 7807 ProblemDetails.errors. DUALITY: a Record<string, string[]> for [ApiController]
   * model-validation 400s, OR a flat string[] for Result.Errors failures (which carry NO
   * per-field entries -- those are surfaced by a form-level summary, not by this wrapper).
   */
  readonly errors = input<ProblemDetails['errors'] | undefined>();

  /**
   * MIGRATION: [QA F4-002] OPTIONAL per-validator client-error message overrides, keyed by the Angular
   * ValidationErrors key (e.g. `{ pattern: 'Enter a valid email address.' }`). Lets a host keep a more
   * meaningful, field-specific message than the generic default WITHOUT re-introducing per-form manual
   * error markup (which would now double-render against this component's own client-error output).
   * Unspecified keys fall back to the built-in friendly messages in {@link messageForError}.
   */
  readonly messages = input<Record<string, string>>();

  // Stable element ids derived from the field key (used for ARIA association).
  readonly labelId = computed(() => `${this.fieldKey()}-label`);
  readonly hintId = computed(() => `${this.fieldKey()}-hint`);
  readonly errorId = computed(() => `${this.fieldKey()}-error`);

  /**
   * Field-specific error messages, resilient to the errors duality.
   * - undefined            -> no errors.
   * - flat string[]        -> no per-field entries (surfaced by a form-level summary).
   * - Record<string,string[]> -> the messages stored under this field key.
   */
  readonly fieldErrors = computed<string[]>(() => {
    const errs = this.errors();
    if (!errs || Array.isArray(errs)) {
      return [];
    }
    // errs is narrowed to Record<string, string[]>; bracket access is required by
    // noPropertyAccessFromIndexSignature. The ?? [] guards a missing key at runtime.
    return errs[this.fieldKey()] ?? [];
  });

  /**
   * MIGRATION: [QA F4-002] CLIENT-side validation messages for the projected control. Previously this
   * wrapper rendered ONLY the server RFC 7807 errors fed via [errors]; Angular client validators
   * (required/email/min/â€¦) produced a red border (.ng-invalid.ng-touched in styles.scss) but NO text,
   * NO aria-invalid, and NO aria-describedby -- failing WCAG 1.4.1 (use of colour), 3.3.1 (error
   * identification) and 4.1.2. Because client validators block submit (form.invalid -> markAllAsTouched ->
   * return) the server path is never reached for these, so they were permanently text-less.
   *
   * The messages surface ONLY once the control is BOTH invalid AND touched, mirroring the existing CSS
   * affordance, so a pristine field is never prematurely flagged. Reads `controlTick()` so it re-evaluates
   * when the control's events fire (touched/status/value changes).
   */
  readonly clientErrors = computed<string[]>(() => {
    this.controlTick();
    const control = this.projectedNgControl()?.control ?? null;
    if (control === null || !control.invalid || !control.touched) {
      return [];
    }
    const errors = control.errors;
    if (errors === null) {
      return [];
    }
    return this.mapValidationErrors(errors, this.fieldLabel());
  });

  /**
   * MIGRATION: [QA F4-002] the COMBINED, de-duplicated error list rendered in the role="alert" region:
   * server RFC 7807 field errors first, then client-validator messages. De-duplication (by message text)
   * avoids a doubled line when the server echoes a rule the client already reported.
   */
  readonly allErrors = computed<string[]>(() => {
    const merged = [...this.fieldErrors(), ...this.clientErrors()];
    return [...new Set(merged)];
  });

  /** Whether this field currently has any error message (server OR client). */
  readonly hasErrors = computed(() => this.allErrors().length > 0);

  /**
   * MIGRATION: [QA F4-004] whether the projected control carries the built-in `required` validator, used to
   * expose aria-required="true" (so assistive tech announces the field as required, WCAG 1.3.1 / 3.3.2).
   * Depends only on the resolved control (validators are set at construction), so it does not re-run per
   * keystroke. Returns false when there is no NgControl (plain projected element) -> aria-required is omitted.
   */
  readonly isRequired = computed<boolean>(() => {
    const control = this.projectedNgControl()?.control ?? null;
    return control?.hasValidator(Validators.required) ?? false;
  });

  /** Human label used to prefix generated client-error messages (falls back to the field key). */
  private readonly fieldLabel = computed<string>(() => {
    const label = this.label();
    return label.length > 0 ? label : this.fieldKey();
  });

  /** Ids the projected control should be described by (hint + errors, when present). */
  readonly describedByIds = computed<string[]>(() => {
    const ids: string[] = [];
    if (this.hint()) {
      ids.push(this.hintId());
    }
    if (this.hasErrors()) {
      ids.push(this.errorId());
    }
    return ids;
  });

  constructor() {
    // MIGRATION: [QA F4-002] (re)subscribe to the projected control's event stream whenever it resolves,
    // bumping `controlTick` so the client-error computeds re-evaluate on touched/status/value changes. The
    // control's invalid/touched/errors are plain getters (not signals), so this explicit subscription is the
    // bridge that makes markAllAsTouched() (which emits a TouchedChangeEvent) surface the client errors. The
    // signal write happens INSIDE the (async) subscription callback -- never synchronously during the effect
    // -- so it is safe and does not create a reactive cycle. onCleanup unsubscribes on control change/destroy.
    effect((onCleanup) => {
      const control = this.projectedNgControl()?.control ?? null;
      if (control === null) {
        return;
      }
      const subscription = control.events.subscribe(() => {
        this.controlTick.update((tick) => tick + 1);
      });
      onCleanup(() => subscription.unsubscribe());
    });

    // MIGRATION: ARIA wiring for the projected control (accessibility NFR). Presentational DOM
    // mutation only (Renderer2); re-runs reactively whenever label/hint/errors/fieldKey change.
    // The legacy DNN validators wired this implicitly via controltovalidate/controlname.
    effect(() => {
      // Read every reactive dependency UP FRONT so the effect re-runs on any of them and so the
      // dependency set is registered even on early-return passes (e.g. before the control has been
      // projected). `labelRef()` in particular tracks the REAL <label> DOM node: it is undefined
      // until the @if (label()) block renders, and updates (re-triggering this effect) the moment
      // the label is created -- which is what makes the native `for`/`id` wiring below race-free.
      const labelEl = this.labelRef()?.nativeElement ?? null;
      const hasLabel = this.label() !== '';
      const labelledById = this.labelId();
      const invalid = this.hasErrors();
      const describedByIds = this.describedByIds();
      const fieldKey = this.fieldKey();
      // MIGRATION: [QA F4-004] read the required state up front so the effect re-runs when it resolves.
      const required = this.isRequired();

      const control = this.host.nativeElement.querySelector<HTMLElement>(
        PROJECTED_CONTROL_SELECTOR,
      );
      if (control === null) {
        return;
      }

      // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #15] EVERY projected interactive control must expose a
      // stable id AND (for native form fields) a name, so the browser does NOT raise "A form field element
      // should have an id or name attribute" and so autofill / label association work. Previously the id was
      // assigned ONLY inside the hasLabel branch (and name was never set), so a label-less projected control
      // -- or one whose host supplied neither -- tripped the issue. Author-supplied values are preserved.
      let controlId = control.getAttribute('id');
      if (controlId === null || controlId === '') {
        controlId = `${fieldKey}-control`;
        this.renderer.setAttribute(control, 'id', controlId);
      }
      const controlTag = control.tagName.toLowerCase();
      if (controlTag === 'input' || controlTag === 'select' || controlTag === 'textarea') {
        const controlName = control.getAttribute('name');
        if (controlName === null || controlName === '') {
          this.renderer.setAttribute(control, 'name', fieldKey);
        }
      }

      // Associate the rendered label with the control.
      if (hasLabel) {
        // MIGRATION (accessibility enhancement): prefer NATIVE <label for> / control-id association --
        // it provides click-to-focus and stronger browser + assistive-technology behavior than ARIA alone.
        // aria-labelledby is ALSO retained (belt-and-suspenders); aria-describedby (below) keeps the
        // hint/error descriptors -- per the review guidance ("prefer for/id; keep ARIA descriptors").
        if (labelEl !== null) {
          this.renderer.setAttribute(labelEl, 'for', controlId);
        }
        this.renderer.setAttribute(control, 'aria-labelledby', labelledById);
      } else {
        this.renderer.removeAttribute(control, 'aria-labelledby');
      }

      // Reflect the invalid state.
      if (invalid) {
        this.renderer.setAttribute(control, 'aria-invalid', 'true');
      } else {
        this.renderer.removeAttribute(control, 'aria-invalid');
      }

      // MIGRATION: [QA F4-004] expose the required state to assistive tech. The native `required`
      // attribute is set ALONGSIDE aria-required so both AT and native UA validation are aware. No
      // visual asterisk is added here (the label text is asserted verbatim by the spec); consumers can
      // still render their own required affordance.
      if (required) {
        this.renderer.setAttribute(control, 'aria-required', 'true');
        this.renderer.setAttribute(control, 'required', '');
      } else {
        this.renderer.removeAttribute(control, 'aria-required');
        this.renderer.removeAttribute(control, 'required');
      }

      // Link the control to its hint/error descriptions.
      if (describedByIds.length > 0) {
        this.renderer.setAttribute(control, 'aria-describedby', describedByIds.join(' '));
      } else {
        this.renderer.removeAttribute(control, 'aria-describedby');
      }
    });
  }

  /**
   * MIGRATION: [QA F4-002] maps Angular's built-in ValidationErrors keys to friendly, human-readable
   * messages prefixed with the field label. Covers the validators used across the migrated forms
   * (required/email/min/max/minlength/maxlength/pattern); any unrecognised key falls back to a generic
   * "is not valid" message so a custom validator is never silently text-less.
   */
  private mapValidationErrors(errors: ValidationErrors, label: string): string[] {
    // Object.keys preserves insertion order; bracket access is required by noPropertyAccessFromIndexSignature.
    return Object.keys(errors).map((key) => this.messageForError(key, errors[key], label));
  }

  private messageForError(key: string, detail: unknown, label: string): string {
    // MIGRATION: [QA F4-002] a host-supplied override for this validator key wins over the built-in
    // defaults, preserving field-specific copy (e.g. an email-pattern field's "Enter a valid email
    // address.") that would otherwise be lost when removing per-form manual error blocks.
    const override = this.messages()?.[key];
    if (override !== undefined) {
      return override;
    }
    switch (key) {
      case 'required':
        return `${label} is required.`;
      case 'email':
        return 'Enter a valid email address.';
      case 'min': {
        const min = (detail as { min?: unknown } | null)?.min;
        return min !== undefined ? `${label} must be at least ${min}.` : `${label} is too small.`;
      }
      case 'max': {
        const max = (detail as { max?: unknown } | null)?.max;
        return max !== undefined ? `${label} must be at most ${max}.` : `${label} is too large.`;
      }
      case 'minlength': {
        const required = (detail as { requiredLength?: unknown } | null)?.requiredLength;
        return required !== undefined
          ? `${label} must be at least ${required} characters.`
          : `${label} is too short.`;
      }
      case 'maxlength': {
        const required = (detail as { requiredLength?: unknown } | null)?.requiredLength;
        return required !== undefined
          ? `${label} must be at most ${required} characters.`
          : `${label} is too long.`;
      }
      case 'pattern':
        return `${label} is not valid.`;
      default:
        return `${label} is not valid.`;
    }
  }
}
