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
 * Two further paths are covered without a DOM blur, because they have no DOM event of their own:
 * - A programmatic `markAllAsTouched()` (the submit path, QA #1a) — verified to add `is-invalid`
 *   via the control's `events` (TouchedChangeEvent) stream.
 * - The optional `appValidationHighlightError` input (server-side errors, QA #1b) — a DrivenHost
 *   verifies that a valid, interacted control shows the error (red) border, never the valid (green)
 *   one, while the signal is set, and returns to green once it clears.
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

/**
 * Host that drives the optional external error signal (`appValidationHighlightError`) on a control
 * whose own client-side value is VALID. Exercises the server-side-error border path (QA #1b): a
 * field that displays a server error must show the error (red) border, never the valid (green) one.
 */
@Component({
  template: `
    <input
      type="text"
      [formControl]="control"
      appValidationHighlight
      [appValidationHighlightError]="errorState"
    />
  `,
  imports: [ReactiveFormsModule, ValidationHighlightDirective],
})
class DrivenHostComponent {
  readonly control = new FormControl('valid value', {
    nonNullable: true,
    validators: [Validators.required],
  });
  errorState = false;
}

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

    it('adds is-invalid after a programmatic markAllAsTouched() with no DOM blur (QA #1a submit path)', () => {
      // A submit handler calls markAllAsTouched(): it changes neither value nor status and fires no
      // DOM blur, yet the field must still be marked invalid. The directive listens to the control's
      // unified `events` stream (TouchedChangeEvent), so the border now follows the touched transition.
      fixture.componentInstance.control.markAllAsTouched();
      fixture.detectChanges();

      expect(input.classList.contains('is-invalid')).toBe(true);
      expect(input.classList.contains('is-valid')).toBe(false);
    });
  });

  describe('with an external error signal (appValidationHighlightError)', () => {
    let fixture: ComponentFixture<DrivenHostComponent>;
    let host: DrivenHostComponent;
    let input: HTMLInputElement;

    beforeEach(() => {
      TestBed.configureTestingModule({ imports: [DrivenHostComponent] });
      fixture = TestBed.createComponent(DrivenHostComponent);
      host = fixture.componentInstance;
      fixture.detectChanges();
      input = fixture.debugElement.query(By.directive(ValidationHighlightDirective))
        .nativeElement as HTMLInputElement;
    });

    it('marks a valid, interacted control invalid (not valid) when the external error signal is true (QA #1b)', () => {
      // Simulate a server-side field error on a control the client considers valid: the field is
      // touched + client-valid (would be green) but a server error has arrived → must show red, not green.
      host.control.markAsTouched();
      host.errorState = true;
      fixture.detectChanges();

      expect(input.classList.contains('is-invalid')).toBe(true);
      expect(input.classList.contains('is-valid')).toBe(false);
    });

    it('restores control-driven validity once the external error signal clears', () => {
      host.control.markAsTouched();
      host.errorState = true;
      fixture.detectChanges();
      expect(input.classList.contains('is-invalid')).toBe(true);

      // The server error is resolved/cleared: a valid, interacted control returns to the green border.
      host.errorState = false;
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
