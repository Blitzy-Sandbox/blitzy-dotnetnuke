import { Component } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ValidationHighlightDirective } from './validation-highlight.directive';

/**
 * Dedicated spec for {@link ValidationHighlightDirective} (QA Report 10 Issue 2 --
 * shared directives lacked dedicated specs).
 *
 * MIGRATION: the directive re-expresses the legacy DotNetNuke Web Forms validator
 * visual feedback (`CssClass="NormalRed"` + `Display="Dynamic"`) as the modern
 * `is-invalid` class, applied only when the bound control is `invalid &&
 * (touched || dirty)` (the Dynamic condition), and also toggles `aria-invalid`
 * for screen-reader parity. These tests assert the show/clear branches and the
 * safe no-op when the directive is placed on a non-form-control host.
 * Contributes to Gate 4.
 */
const INVALID_CLASS = 'is-invalid';

@Component({
  imports: [ReactiveFormsModule, ValidationHighlightDirective],
  template: `<input [formControl]="ctrl" appValidationHighlight />`,
})
class ControlHostComponent {
  ctrl = new FormControl('', { nonNullable: true, validators: [Validators.required] });
}

@Component({
  imports: [ValidationHighlightDirective],
  template: `<div appValidationHighlight>not a control</div>`,
})
class NonControlHostComponent {}

describe('ValidationHighlightDirective', () => {
  describe('bound to a reactive form control', () => {
    let fixture: ComponentFixture<ControlHostComponent>;

    function inputEl(): HTMLInputElement {
      return fixture.nativeElement.querySelector('input') as HTMLInputElement;
    }

    beforeEach(() => {
      TestBed.configureTestingModule({ imports: [ControlHostComponent] });
      fixture = TestBed.createComponent(ControlHostComponent);
      fixture.detectChanges();
    });

    it('does not highlight an invalid but untouched/pristine control', () => {
      const el = inputEl();
      // Control is invalid (required + empty) but the user has not interacted.
      expect(el.classList.contains(INVALID_CLASS)).toBeFalse();
      expect(el.hasAttribute('aria-invalid')).toBeFalse();
    });

    it('applies is-invalid + aria-invalid once the invalid control is touched', () => {
      fixture.componentInstance.ctrl.markAsTouched();
      fixture.detectChanges();

      const el = inputEl();
      expect(el.classList.contains(INVALID_CLASS)).toBeTrue();
      expect(el.getAttribute('aria-invalid')).toBe('true');
    });

    it('clears the highlight when the control becomes valid', () => {
      fixture.componentInstance.ctrl.markAsTouched();
      fixture.detectChanges();
      expect(inputEl().classList.contains(INVALID_CLASS)).toBeTrue();

      // Satisfy the required validator -> the error state must clear.
      fixture.componentInstance.ctrl.setValue('now valid');
      fixture.detectChanges();

      const el = inputEl();
      expect(el.classList.contains(INVALID_CLASS)).toBeFalse();
      expect(el.hasAttribute('aria-invalid')).toBeFalse();
    });
  });

  describe('placed on a non-control host', () => {
    it('is a safe no-op (never adds is-invalid)', () => {
      TestBed.configureTestingModule({ imports: [NonControlHostComponent] });
      const fixture = TestBed.createComponent(NonControlHostComponent);
      fixture.detectChanges();

      const div = fixture.nativeElement.querySelector('div') as HTMLElement;
      expect(div.classList.contains(INVALID_CLASS)).toBeFalse();
      expect(div.hasAttribute('aria-invalid')).toBeFalse();
    });
  });
});
