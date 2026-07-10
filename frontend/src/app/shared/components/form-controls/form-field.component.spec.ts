import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';

import { FormFieldComponent } from './form-field.component';

/**
 * Unit tests for {@link FormFieldComponent} — the reusable, presentation-only,
 * typed reactive form field used across the migrated Angular 19 SPA.
 *
 * These specs back Validation Gate 4 (`ng test --watch=false
 * --browsers=ChromeHeadless --code-coverage`, 100% pass) and prove the component
 * reproduces the legacy DotNetNuke Web Forms validator behaviour (UI functional
 * parity, AAP §0.7.1):
 *
 * - Display="Dynamic": the inline message is hidden until the bound control is
 *   invalid AND (touched OR dirty). Verified against the eight
 *   `RequiredFieldValidator` controls in `Website/admin/Portal/signup.ascx` and
 *   the `RequiredFieldValidator`/`CompareValidator` pairs in
 *   `Website/admin/Security/editroles.ascx`, all `Display="Dynamic"`.
 * - `required`, `pattern` (RegularExpression / CompareValidator DataTypeCheck),
 *   numeric `min` (CompareValidator GreaterThanEqual 0) and custom / cross-field
 *   messages surface with the correct text.
 * - The composed {@link ValidationHighlightDirective} toggles the `is-invalid`
 *   class and `aria-invalid` on the rendered control (legacy `CssClass="NormalRed"`).
 * - `aria-describedby` links the control to the error element for accessibility.
 *
 * The component exposes its state through `input()` signal inputs, which are
 * read-only from a consumer's perspective. In tests they are therefore set via
 * `fixture.componentRef.setInput(name, value)` — direct assignment
 * (`component.control = …`) is a compile error and is intentionally never used.
 * The required `control` input is always set BEFORE the first `detectChanges()`
 * so the component's `toObservable`/`toSignal` bridge and the directive's
 * `NgControl` initialise without an NG0950 missing-required-input throw.
 */
describe('FormFieldComponent', () => {
  let fixture: ComponentFixture<FormFieldComponent>;
  let component: FormFieldComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      // Standalone component → register in `imports`, never `declarations`.
      // Its own imports (ReactiveFormsModule + the two directives) come along
      // automatically, so CommonModule / FormsModule must NOT be added here.
      imports: [FormFieldComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FormFieldComponent);
    component = fixture.componentInstance;
  });

  /** Sets the required `control` signal input via the read-only-input test API. */
  function setControl(control: FormControl): void {
    fixture.componentRef.setInput('control', control);
  }

  /**
   * Null-safe accessor for the rendered inline error element. Returns `null`
   * when the `@if (showError())` guard keeps it out of the DOM (Display="Dynamic").
   */
  function errorEl(): HTMLElement | null {
    const de = fixture.debugElement.query(By.css('.form-field__error'));
    return de ? (de.nativeElement as HTMLElement) : null;
  }

  /** Strongly-typed accessor for the rendered control element. */
  function inputEl(): HTMLInputElement {
    return fixture.debugElement.query(By.css('.form-field__control'))
      .nativeElement as HTMLInputElement;
  }

  it('creates', () => {
    setControl(new FormControl(''));
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('associates the label with the rendered control id', () => {
    setControl(new FormControl(''));
    fixture.componentRef.setInput('label', 'Email');
    fixture.componentRef.setInput('controlId', 'email-field');
    fixture.detectChanges();

    const label = fixture.debugElement.query(By.css('.form-field__label'))
      .nativeElement as HTMLLabelElement;
    expect(label.textContent).toContain('Email');
    expect(label.getAttribute('for')).toBe('email-field');
    expect(inputEl().id).toBe('email-field');
  });

  it('does NOT show the message while invalid but pristine + untouched (Display="Dynamic")', () => {
    // MIGRATION: signup.ascx valPortalName RequiredFieldValidator Display="Dynamic".
    setControl(new FormControl('', Validators.required));
    fixture.componentRef.setInput('label', 'Portal Name');
    fixture.detectChanges();

    expect(errorEl()).toBeNull();
  });

  it('shows the required message once the control is touched', () => {
    const control = new FormControl('', Validators.required);
    setControl(control);
    fixture.componentRef.setInput('label', 'Portal Name');
    fixture.detectChanges();

    control.markAsTouched();
    fixture.detectChanges();

    expect(errorEl()?.textContent).toContain('Portal Name is required.');
  });

  it('shows the pattern message when a RegularExpression-style validator fails (once dirty)', () => {
    // MIGRATION: editroles.ascx CompareValidator Operator="DataTypeCheck" -> pattern.
    // The control is constructed already-invalid ('abc' fails /^\d+$/), so only
    // markAsDirty() + detectChanges() is needed to surface it.
    const control = new FormControl('abc', Validators.pattern(/^\d+$/));
    setControl(control);
    fixture.componentRef.setInput('label', 'Code');
    fixture.detectChanges();

    control.markAsDirty();
    fixture.detectChanges();

    expect(errorEl()?.textContent).toContain('Code is not in a valid format.');
  });

  it('shows the numeric/min message (CompareValidator GreaterThanEqual 0 -> Validators.min(0))', () => {
    // MIGRATION: editroles.ascx valServiceFee2 CompareValidator
    // Operator="GreaterThanEqual" ValueToCompare="0" -> Validators.min(0).
    const control = new FormControl(-5, Validators.min(0));
    setControl(control);
    fixture.componentRef.setInput('label', 'Service Fee');
    fixture.detectChanges();

    control.markAsTouched();
    fixture.detectChanges();

    expect(errorEl()?.textContent).toContain(
      'Service Fee must be greater than or equal to 0.',
    );
  });

  it('shows an exact legacy / cross-field message supplied via errorMessages', () => {
    // MIGRATION: signup.ascx Confirm password parity — a cross-field mismatch
    // error whose exact text the parent supplies verbatim via [errorMessages].
    const control = new FormControl('');
    setControl(control);
    fixture.componentRef.setInput('label', 'Confirm Password');
    fixture.componentRef.setInput('errorMessages', {
      mismatch: 'Passwords do not match.',
    });
    fixture.detectChanges();

    control.setErrors({ mismatch: true });
    control.markAsTouched();
    fixture.detectChanges();

    expect(errorEl()?.textContent).toContain('Passwords do not match.');
  });

  it('toggles is-invalid + aria-invalid on the control via appValidationHighlight', () => {
    const control = new FormControl('', Validators.required);
    setControl(control);
    fixture.componentRef.setInput('label', 'Username');
    fixture.detectChanges();

    // pristine + untouched -> no highlight
    expect(inputEl().classList.contains('is-invalid')).toBe(false);
    expect(inputEl().getAttribute('aria-invalid')).toBeNull();

    control.markAsTouched();
    fixture.detectChanges();

    expect(inputEl().classList.contains('is-invalid')).toBe(true);
    expect(inputEl().getAttribute('aria-invalid')).toBe('true');

    // becomes valid -> highlight cleared
    control.setValue('jdoe');
    fixture.detectChanges();

    expect(inputEl().classList.contains('is-invalid')).toBe(false);
    expect(inputEl().getAttribute('aria-invalid')).toBeNull();
  });

  it('links the error element via aria-describedby when the message is shown', () => {
    const control = new FormControl('', Validators.required);
    setControl(control);
    fixture.componentRef.setInput('label', 'Email');
    fixture.componentRef.setInput('controlId', 'email');
    fixture.detectChanges();

    control.markAsTouched();
    fixture.detectChanges();

    expect(inputEl().getAttribute('aria-describedby')).toContain('email-error');
    expect(errorEl()?.id).toBe('email-error');
  });

  // MIGRATION / QA finding P6-1: the `step` input controls the native step attribute so currency /
  // decimal fields (portal Host Fee, role Service Fee / Trial Fee) accept fractional amounts without
  // tripping the browser's stepMismatch constraint.
  it('omits the step attribute by default so existing callers keep the implicit integer step', () => {
    setControl(new FormControl(0));
    fixture.componentRef.setInput('controlType', 'number');
    fixture.detectChanges();

    expect(inputEl().hasAttribute('step')).toBe(false);
  });

  it('renders step="any" when the step input is set, and a decimal value is NOT a stepMismatch', () => {
    setControl(new FormControl(25.5));
    fixture.componentRef.setInput('controlType', 'number');
    fixture.componentRef.setInput('step', 'any');
    fixture.detectChanges();

    const el = inputEl();
    expect(el.getAttribute('step')).toBe('any');
    expect(el.type).toBe('number');
    // With step="any" the browser accepts the fractional 25.50 (9.99 likewise) — no stepMismatch.
    expect(el.validity.stepMismatch).toBe(false);
  });

  it('flags a decimal as stepMismatch WITHOUT the fix (default integer step) — contrast case', () => {
    // Guards P6-1: proves the step="any" fix is load-bearing. A number input left at the implicit
    // integer step reports stepMismatch for a fractional value, which is exactly what mis-reported
    // aria-invalid for the currency fields before the fix.
    setControl(new FormControl(25.5));
    fixture.componentRef.setInput('controlType', 'number');
    fixture.detectChanges();

    expect(inputEl().hasAttribute('step')).toBe(false);
    expect(inputEl().validity.stepMismatch).toBe(true);
  });
});
