import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';

import { FormControlsComponent } from './form-controls.component';

@Component({
  template: `
    <app-form-controls
      [control]="control"
      [controlId]="'email'"
      [label]="label"
      [type]="'email'"
      [serverErrors]="serverErrors"
      [messages]="messages"
    />
  `,
  imports: [FormControlsComponent],
})
class HostComponent {
  readonly control = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email],
  });
  label = 'Email';
  serverErrors: Record<string, string[]> | null = null;
  messages: Record<string, string> = {};
}

/**
 * Host for the number/currency `step` behavior (QA #2). The control holds a legitimate persisted
 * decimal (9.99); `step` is toggled to assert the attribute is emitted and that 9.99 is not a step
 * mismatch once `step="0.01"` is supplied (the integer-default step=1 wrongly flags 9.99 invalid).
 */
@Component({
  template: `
    <app-form-controls
      [control]="control"
      [controlId]="'serviceFee'"
      [label]="'Service Fee'"
      [type]="'number'"
      [step]="step"
    />
  `,
  imports: [FormControlsComponent],
})
class NumberHostComponent {
  readonly control = new FormControl<number | null>(9.99);
  step: string | number | null = null;
}

describe('FormControlsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function inputEl(): HTMLInputElement {
    return fixture.debugElement.query(By.css('input')).nativeElement as HTMLInputElement;
  }

  function labelEl(): HTMLLabelElement | null {
    const found = fixture.debugElement.query(By.css('label'));
    return found ? (found.nativeElement as HTMLLabelElement) : null;
  }

  function errorMessages(): string[] {
    return fixture.debugElement
      .queryAll(By.css('.form-controls__error'))
      .map((node) => (node.nativeElement as HTMLElement).textContent?.trim() ?? '');
  }

  it('renders the label associated with the input id and applies the type', () => {
    expect(labelEl()?.textContent?.trim()).toBe('Email');
    expect(labelEl()?.getAttribute('for')).toBe('email');
    expect(inputEl().id).toBe('email');
    expect(inputEl().type).toBe('email');
  });

  it('shows no errors while the control is untouched and pristine', () => {
    expect(errorMessages().length).toBe(0);
    expect(inputEl().getAttribute('aria-invalid')).toBeNull();
    expect(inputEl().getAttribute('aria-describedby')).toBeNull();
  });

  it('shows the required message and aria wiring once the field is blurred', () => {
    inputEl().dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(errorMessages()).toContain('This field is required.');
    expect(inputEl().getAttribute('aria-invalid')).toBe('true');
    expect(inputEl().getAttribute('aria-describedby')).toBe('email-errors');
  });

  it('shows the email message when an invalid email value is entered', () => {
    const el = inputEl();
    el.value = 'not-an-email';
    el.dispatchEvent(new Event('input'));
    el.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(errorMessages()).toContain('Please enter a valid email address.');
  });

  it('honors custom message overrides for a validator key', () => {
    host.messages = { required: 'Email is mandatory.' };
    fixture.detectChanges();

    inputEl().dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(errorMessages()).toContain('Email is mandatory.');
    expect(errorMessages()).not.toContain('This field is required.');
  });

  it('clears client errors once a valid value is entered', () => {
    const el = inputEl();
    el.value = 'user@example.com';
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(errorMessages().length).toBe(0);
    expect(el.getAttribute('aria-invalid')).toBeNull();
  });

  it('displays server-side ProblemDetails field errors for the control', () => {
    host.serverErrors = { email: ['Email already in use.'] };
    fixture.detectChanges();

    expect(errorMessages()).toContain('Email already in use.');
  });

  it('synchronizes error display on programmatic markAllAsTouched (OnPush)', () => {
    host.control.markAllAsTouched();
    fixture.detectChanges();

    expect(errorMessages()).toContain('This field is required.');
  });

  it('applies the validation-highlight directive to the input', () => {
    inputEl().dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(inputEl().classList.contains('is-invalid')).toBe(true);
  });

  it('paints the input border invalid on markAllAsTouched, matching the message + aria (QA #1a)', () => {
    // Empty + required → invalid; markAllAsTouched (the submit path) must paint the error border
    // even though the field was never individually blurred, so border, message and aria all agree.
    host.control.markAllAsTouched();
    fixture.detectChanges();

    const el = inputEl();
    expect(el.classList.contains('is-invalid')).toBe(true);
    expect(el.classList.contains('is-valid')).toBe(false);
    expect(el.getAttribute('aria-invalid')).toBe('true');
    expect(errorMessages()).toContain('This field is required.');
  });

  it('flips a client-valid field from the valid border to the error border when a server error arrives (QA #1b)', () => {
    // Enter a client-valid value and interact → green is-valid border.
    const el = inputEl();
    el.value = 'user@example.com';
    el.dispatchEvent(new Event('input'));
    el.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(el.classList.contains('is-valid')).toBe(true);
    expect(el.classList.contains('is-invalid')).toBe(false);

    // A server-side RFC 7807 field error then arrives for this control: the border must turn red
    // (matching the message + aria-invalid), never stay green.
    host.serverErrors = { email: ['Email already in use.'] };
    fixture.detectChanges();

    expect(el.classList.contains('is-invalid')).toBe(true);
    expect(el.classList.contains('is-valid')).toBe(false);
    expect(el.getAttribute('aria-invalid')).toBe('true');
    expect(errorMessages()).toContain('Email already in use.');
  });
});

describe('FormControlsComponent number/currency step (QA #2)', () => {
  let fixture: ComponentFixture<NumberHostComponent>;
  let host: NumberHostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [NumberHostComponent] });
    fixture = TestBed.createComponent(NumberHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function numberInput(): HTMLInputElement {
    return fixture.debugElement.query(By.css('input')).nativeElement as HTMLInputElement;
  }

  it('omits the step attribute by default so integer fields keep the whole-number step=1', () => {
    expect(numberInput().getAttribute('step')).toBeNull();
  });

  it('reports a step mismatch for a 9.99 decimal under the integer default (documents the bug being fixed)', () => {
    const el = numberInput();
    expect(el.value).toBe('9.99');
    expect(el.validity.stepMismatch).toBe(true);
  });

  it('emits step="0.01" and accepts the 9.99 decimal with no step mismatch (the fix)', () => {
    host.step = '0.01';
    fixture.detectChanges();

    const el = numberInput();
    expect(el.getAttribute('step')).toBe('0.01');
    expect(el.value).toBe('9.99');
    expect(el.validity.stepMismatch).toBe(false);
  });
});
