/**
 * Unit tests for {@link ValidationHighlightDirective}.
 *
 * The specs exercise the directive's full public contract under the Angular 19
 * standalone + Reactive Forms runtime, driving every interaction through real DOM
 * events so the bound `ControlValueAccessor` wires `touched`/`dirty` exactly as it
 * does in production:
 *
 *  - `is-invalid` is applied when the control is invalid AND the user has interacted
 *    with it (touched or dirty).
 *  - `is-valid` is applied when the control is valid AND interacted.
 *  - When the directive is applied to an element with NO bound control it degrades
 *    gracefully: it never throws and applies no validation classes.
 *
 * Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless`) and type-checks
 * clean under Angular-19-strict TypeScript (Gate 3, via `tsconfig.spec.json`).
 *
 * // MIGRATION: These tests verify the client-side replacement for the legacy DNN
 * // ASP.NET validator CSS-class marking (e.g. CssClass="NormalRed" applied on postback
 * // by RequiredFieldValidator/RegularExpressionValidator in
 * // Website/admin/Users/User.ascx.vb). Only the VISUAL marking is reproduced here;
 * // validation RULES live in the feature reactive-form validators + backend
 * // FluentValidation, and error MESSAGES are rendered by the shared form-controls
 * // component — none of which this directive (or these tests) is responsible for.
 */
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';

import { ValidationHighlightDirective } from './validation-highlight.directive';

/**
 * Standalone test host that binds the directive to an input wired to a required,
 * non-nullable reactive-form control. The initial empty value is therefore invalid
 * until a non-empty value is entered.
 */
@Component({
  template: `<input type="text" [formControl]="control" appValidationHighlight />`,
  imports: [ReactiveFormsModule, ValidationHighlightDirective],
})
class BoundHostComponent {
  /** Required control: the empty (initial) value is invalid; any non-empty value is valid. */
  readonly control = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
}

/**
 * Standalone test host that applies the directive WITHOUT a bound form control,
 * exercising the directive's graceful-degradation path.
 */
@Component({
  template: `<input type="text" appValidationHighlight />`,
  imports: [ValidationHighlightDirective],
})
class UnboundHostComponent {}

describe('ValidationHighlightDirective', () => {
  describe('with a bound reactive-form control', () => {
    let fixture: ComponentFixture<BoundHostComponent>;
    let input: HTMLInputElement;

    beforeEach(() => {
      TestBed.configureTestingModule({ imports: [BoundHostComponent] });
      fixture = TestBed.createComponent(BoundHostComponent);
      // Initial paint: the control is invalid (required + empty) but still untouched
      // and pristine, so no highlight class should be present yet.
      fixture.detectChanges();
      input = fixture.debugElement.query(
        By.directive(ValidationHighlightDirective),
      ).nativeElement as HTMLInputElement;
    });

    it('creates the host with the directive applied', () => {
      expect(input).toBeTruthy();
    });

    it('does not highlight while untouched and pristine', () => {
      // Invalid, but the user has not interacted yet -> neither class is applied.
      expect(input.classList.contains('is-invalid')).toBe(false);
      expect(input.classList.contains('is-valid')).toBe(false);
    });

    it('adds is-invalid after the invalid field is blurred', () => {
      // The value accessor's own blur handler (registered first, during the create
      // phase) marks the control touched, then the directive's blur listener
      // re-evaluates the highlight against the now-touched, still-invalid control.
      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(input.classList.contains('is-invalid')).toBe(true);
      expect(input.classList.contains('is-valid')).toBe(false);
    });

    it('adds is-valid (and removes is-invalid) once a valid value is entered', () => {
      // The DOM `input` event sets the value, marks the control dirty, and emits
      // valueChanges/statusChanges -> the directive re-evaluates and flips to the
      // valid highlight (interacted = dirty), removing any invalid highlight.
      input.value = 'valid value';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(input.classList.contains('is-valid')).toBe(true);
      expect(input.classList.contains('is-invalid')).toBe(false);
    });
  });

  describe('without a bound control', () => {
    it('does not throw and applies no validation classes', () => {
      TestBed.configureTestingModule({ imports: [UnboundHostComponent] });
      const fixture = TestBed.createComponent(UnboundHostComponent);

      // No NgControl is bound, so the directive must degrade gracefully on init.
      expect(() => fixture.detectChanges()).not.toThrow();

      const input = fixture.debugElement.query(
        By.directive(ValidationHighlightDirective),
      ).nativeElement as HTMLInputElement;
      expect(input.classList.contains('is-invalid')).toBe(false);
      expect(input.classList.contains('is-valid')).toBe(false);
    });
  });
});
