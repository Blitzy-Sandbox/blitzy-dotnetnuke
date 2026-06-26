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
  effect,
  inject,
  input,
} from '@angular/core';

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
        <label class="form-control__label" [id]="labelId()">{{ label() }}</label>
      }
      <div class="form-control__field">
        <ng-content></ng-content>
      </div>
      @if (hint()) {
        <p class="form-control__hint" [id]="hintId()">{{ hint() }}</p>
      }
      @if (hasErrors()) {
        <div class="form-control__errors" [id]="errorId()" role="alert">
          @for (message of fieldErrors(); track message) {
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

  /** Whether this field currently has any error message. */
  readonly hasErrors = computed(() => this.fieldErrors().length > 0);

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
    // MIGRATION: ARIA wiring for the projected control (accessibility NFR). Presentational DOM
    // mutation only (Renderer2); re-runs reactively whenever label/hint/errors/fieldKey change.
    // The legacy DNN validators wired this implicitly via controltovalidate/controlname.
    effect(() => {
      const control = this.host.nativeElement.querySelector<HTMLElement>(
        PROJECTED_CONTROL_SELECTOR,
      );
      if (control === null) {
        return;
      }

      // Associate the rendered label with the control.
      if (this.label()) {
        this.renderer.setAttribute(control, 'aria-labelledby', this.labelId());
      } else {
        this.renderer.removeAttribute(control, 'aria-labelledby');
      }

      // Reflect the invalid state.
      if (this.hasErrors()) {
        this.renderer.setAttribute(control, 'aria-invalid', 'true');
      } else {
        this.renderer.removeAttribute(control, 'aria-invalid');
      }

      // Link the control to its hint/error descriptions.
      const ids = this.describedByIds();
      if (ids.length > 0) {
        this.renderer.setAttribute(control, 'aria-describedby', ids.join(' '));
      } else {
        this.renderer.removeAttribute(control, 'aria-describedby');
      }
    });
  }
}
