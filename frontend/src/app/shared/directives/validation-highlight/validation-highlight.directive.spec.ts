import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';

import { ValidationHighlightDirective } from './validation-highlight.directive';

/**
 * Unit tests for {@link ValidationHighlightDirective}.
 *
 * The directive is exercised through two tiny in-file STANDALONE host components (standalone is the
 * Angular 19 default — `standalone: false` is never set, and `strictStandalone` would reject a
 * non-standalone declaration). NO NgModule-based testing setup is used: each host declares its own
 * `imports`, and `TestBed.configureTestingModule({ imports: [<HostComponent>] })` pulls the directive
 * in transitively.
 *
 * Interaction is driven through the real DOM so the reactive-form value accessor wires `touched` and
 * `dirty` exactly as it does in production:
 * - A `blur` event marks the control `touched` (the value accessor's own blur handler runs first,
 *   then the directive's blur listener re-evaluates the highlight) → `is-invalid` appears for an
 *   invalid field.
 * - An `input` event marks the control `dirty`, sets its value and emits `valueChanges`/`statusChanges`
 *   (the directive re-evaluates) → `is-valid` appears once the value satisfies the validators.
 *
 * The unbound host verifies graceful degradation: applied without a form control, the directive must
 * neither throw nor add any class. All fixtures and elements are precisely typed — there is no `any`.
 */

/** Host with a required reactive-form control bound to the directive (empty value is invalid). */
@Component({
  template: `<input type="text" [formControl]="control" appValidationHighlight />`,
  imports: [ReactiveFormsModule, ValidationHighlightDirective],
})
class BoundHostComponent {
  readonly control = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
}

/** Host that applies the directive WITHOUT a form control (exercises graceful degradation). */
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
      fixture.detectChanges();
      input = fixture.debugElement.query(By.directive(ValidationHighlightDirective))
        .nativeElement as HTMLInputElement;
    });

    it('creates the host with the directive applied', () => {
      expect(input).toBeTruthy();
    });

    it('does not highlight while untouched and pristine', () => {
      // Required + empty is invalid, but the field has not been interacted with yet, so no class.
      expect(input.classList.contains('is-invalid')).toBe(false);
      expect(input.classList.contains('is-valid')).toBe(false);
    });

    it('adds is-invalid after the invalid field is blurred', () => {
      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      expect(input.classList.contains('is-invalid')).toBe(true);
      expect(input.classList.contains('is-valid')).toBe(false);
    });

    it('adds is-valid (and removes is-invalid) once a valid value is entered', () => {
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

      expect(() => fixture.detectChanges()).not.toThrow();

      const input = fixture.debugElement.query(By.directive(ValidationHighlightDirective))
        .nativeElement as HTMLInputElement;
      expect(input.classList.contains('is-invalid')).toBe(false);
      expect(input.classList.contains('is-valid')).toBe(false);
    });
  });
});
