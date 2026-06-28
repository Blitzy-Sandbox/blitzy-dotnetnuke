// MIGRATION: Spec for the net-new FormControlComponent (no legacy equivalent). Verifies label/hint
// rendering, the RFC 7807 errors duality (Record<string,string[]> vs flat string[]), and the ARIA
// attributes wired onto the projected control.
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
// MIGRATION: [QA F4-002 / F4-004] reactive-forms primitives for the client-validation regression host below.
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';

import type { ProblemDetails } from '../../../core/models';
import { FormControlComponent } from './form-control.component';

@Component({
  selector: 'app-form-control-host',
  standalone: true,
  imports: [FormControlComponent],
  template: `
    <app-form-control
      [label]="label()"
      [hint]="hint()"
      [fieldKey]="fieldKey()"
      [errors]="errors()"
    >
      <input type="text" />
    </app-form-control>
  `,
})
class HostComponent {
  readonly label = signal('Email Address');
  readonly hint = signal<string | undefined>(undefined);
  readonly fieldKey = signal('email');
  readonly errors = signal<ProblemDetails['errors'] | undefined>(undefined);
}

describe('FormControlComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FormControlComponent, HostComponent],
    });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
  });

  const root = (): HTMLElement => fixture.nativeElement as HTMLElement;

  const projectedInput = (): HTMLInputElement => {
    const el = root().querySelector('input');
    if (el === null) {
      throw new Error('Projected <input> was not found');
    }
    return el;
  };

  it('should create and render the label with a stable id', () => {
    fixture.detectChanges();

    const label = root().querySelector('.form-control__label');
    expect(label).not.toBeNull();
    expect(label?.textContent?.trim()).toBe('Email Address');
    expect(label?.getAttribute('id')).toBe('email-label');
  });

  it('should expose the label to the projected control via aria-labelledby', () => {
    fixture.detectChanges();

    expect(projectedInput().getAttribute('aria-labelledby')).toBe('email-label');
  });

  it('should natively associate the label with the projected control via for/id', () => {
    fixture.detectChanges();

    const input = projectedInput();
    expect(input.getAttribute('aria-labelledby')).toBe('email-label');

    // MIGRATION (accessibility enhancement): the projected control with no author id is assigned a
    // stable derived id, and the rendered <label> references it via `for` (native click-to-focus).
    const controlId = input.getAttribute('id');
    expect(controlId).toBe('email-control');

    const label = root().querySelector('.form-control__label');
    expect(label?.getAttribute('for')).toBe(controlId);
  });

  it('should reuse an author-supplied control id for the native label association', () => {
    // A control that already exposes an id must keep it; the label `for` must point at that id.
    host.fieldKey.set('email');
    fixture.detectChanges();
    const input = projectedInput();
    input.setAttribute('id', 'custom-email-input');

    // Re-trigger the wiring effect by toggling an input that it reads.
    host.label.set('Email');
    fixture.detectChanges();

    expect(input.getAttribute('id')).toBe('custom-email-input');
    const label = root().querySelector('.form-control__label');
    expect(label?.getAttribute('for')).toBe('custom-email-input');
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #15] a projected control must expose a stable id AND name
  // EVEN WITHOUT A LABEL, so the browser never raises "A form field element should have an id or name
  // attribute". Previously the id was assigned only inside the hasLabel branch and name was never set.
  it('assigns a stable id AND name to the projected control even when there is no label', () => {
    host.fieldKey.set('email');
    host.label.set(''); // hasLabel === false
    fixture.detectChanges();

    const input = projectedInput();
    expect(input.getAttribute('id')).toBe('email-control');
    expect(input.getAttribute('name')).toBe('email');
  });

  it('derives the projected control name from fieldKey when the host supplies none', () => {
    host.fieldKey.set('portalName');
    fixture.detectChanges();
    expect(projectedInput().getAttribute('name')).toBe('portalName');
  });

  it('preserves an author-supplied name on the projected control', () => {
    host.fieldKey.set('email');
    fixture.detectChanges();
    const input = projectedInput();
    input.setAttribute('name', 'customName');

    // Re-trigger the wiring effect by toggling an input it reads.
    host.label.set('Email Address');
    fixture.detectChanges();

    expect(input.getAttribute('name')).toBe('customName');
  });

  it('should render the hint and link it via aria-describedby when provided', () => {
    host.hint.set('We will never share your email.');
    fixture.detectChanges();

    const hintEl = root().querySelector('.form-control__hint');
    expect(hintEl?.textContent?.trim()).toBe('We will never share your email.');
    expect(hintEl?.getAttribute('id')).toBe('email-hint');
    expect(projectedInput().getAttribute('aria-describedby')).toBe('email-hint');
  });

  it('should not render a hint element when no hint is provided', () => {
    fixture.detectChanges();

    expect(root().querySelector('.form-control__hint')).toBeNull();
    expect(projectedInput().getAttribute('aria-describedby')).toBeNull();
  });

  it('should render field errors when errors is a Record containing the field key', () => {
    host.errors.set({ email: ['Email is required', 'Email must be valid'] });
    fixture.detectChanges();

    const errorEls = root().querySelectorAll('.form-control__error');
    expect(errorEls.length).toBe(2);
    expect(errorEls[0].textContent?.trim()).toBe('Email is required');
    expect(errorEls[1].textContent?.trim()).toBe('Email must be valid');

    const input = projectedInput();
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toContain('email-error');
  });

  it('should ignore Record entries that belong to other field keys', () => {
    host.errors.set({ username: ['Username is already taken'] });
    fixture.detectChanges();

    expect(root().querySelectorAll('.form-control__error').length).toBe(0);
    expect(root().querySelector('.form-control__errors')).toBeNull();
    expect(projectedInput().getAttribute('aria-invalid')).toBeNull();
  });

  it('should render nothing field-specific when errors is a flat string array', () => {
    host.errors.set(['A general failure occurred', 'Please try again later']);
    fixture.detectChanges();

    expect(root().querySelector('.form-control__errors')).toBeNull();
    expect(root().querySelectorAll('.form-control__error').length).toBe(0);

    const input = projectedInput();
    expect(input.getAttribute('aria-invalid')).toBeNull();
    expect(input.getAttribute('aria-describedby')).toBeNull();
  });

  it('should combine hint and error ids in aria-describedby when both are present', () => {
    host.hint.set('Use your work email.');
    host.errors.set({ email: ['Email is required'] });
    fixture.detectChanges();

    const describedBy = projectedInput().getAttribute('aria-describedby');
    expect(describedBy).toContain('email-hint');
    expect(describedBy).toContain('email-error');
  });

  it('should clear error ARIA attributes when errors are removed', () => {
    host.errors.set({ email: ['Email is required'] });
    fixture.detectChanges();
    expect(projectedInput().getAttribute('aria-invalid')).toBe('true');

    host.errors.set(undefined);
    fixture.detectChanges();
    expect(projectedInput().getAttribute('aria-invalid')).toBeNull();
    expect(projectedInput().getAttribute('aria-describedby')).toBeNull();
  });
});

// MIGRATION: [QA F4-002 / F4-004] regression suite for CLIENT-side validation accessibility. The original
// wrapper rendered text/aria ONLY from the server [errors] input; Angular client validators
// (required/email/...) produced a red border but NO error text, NO aria-invalid, NO aria-describedby and
// NO aria-required -- failing WCAG 1.4.1 / 3.3.1 / 3.3.2 / 4.1.2. These tests project a REAL Reactive-Forms
// control (so contentChild(NgControl) resolves) and lock in the fix: error text in a role="alert" region,
// aria-invalid, aria-describedby, and aria-required, surfaced only once the control is invalid AND touched.
@Component({
  selector: 'app-form-control-reactive-host',
  standalone: true,
  imports: [FormControlComponent, ReactiveFormsModule],
  template: `
    <app-form-control [label]="label()" [fieldKey]="fieldKey()" [messages]="messages()">
      <input type="text" [formControl]="control" />
    </app-form-control>
  `,
})
class ReactiveHostComponent {
  readonly label = signal('Email Address');
  readonly fieldKey = signal('email');
  readonly messages = signal<Record<string, string> | undefined>(undefined);
  // Default: a required field starts invalid (empty value) so touching it surfaces the required error.
  control = new FormControl<string>('', { nonNullable: true, validators: [Validators.required] });
}

describe('FormControlComponent — client validation (F4-002/F4-004)', () => {
  let fixture: ComponentFixture<ReactiveHostComponent>;
  let host: ReactiveHostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FormControlComponent, ReactiveHostComponent, ReactiveFormsModule],
    });
    fixture = TestBed.createComponent(ReactiveHostComponent);
    host = fixture.componentInstance;
  });

  const root = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const projectedInput = (): HTMLInputElement => {
    const el = root().querySelector('input');
    if (el === null) {
      throw new Error('Projected <input> was not found');
    }
    return el;
  };
  const errorTexts = (): string[] =>
    Array.from(root().querySelectorAll('.form-control__error')).map((el) =>
      (el.textContent ?? '').trim(),
    );

  it('shows NO client error text or aria-invalid while the control is pristine (untouched)', () => {
    fixture.detectChanges();

    // Invalid but untouched -> mirrors the CSS affordance; nothing should surface yet.
    expect(host.control.invalid).toBe(true);
    expect(host.control.touched).toBe(false);
    expect(errorTexts().length).toBe(0);
    expect(root().querySelector('.form-control__errors')).toBeNull();
    expect(projectedInput().getAttribute('aria-invalid')).toBeNull();
  });

  it('renders the required error text in a role="alert" region with aria-invalid + aria-describedby once touched', () => {
    fixture.detectChanges();

    // Touch the control: AbstractControl.events emits a TouchedChangeEvent which bumps the internal tick.
    host.control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual(['Email Address is required.']);

    const alertRegion = root().querySelector('.form-control__errors');
    expect(alertRegion).not.toBeNull();
    expect(alertRegion?.getAttribute('role')).toBe('alert');
    expect(alertRegion?.getAttribute('id')).toBe('email-error');

    const input = projectedInput();
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toContain('email-error');
  });

  it('maps the email validator to a friendly client message once touched', () => {
    host.control = new FormControl<string>('not-an-email', {
      nonNullable: true,
      validators: [Validators.email],
    });
    fixture.detectChanges();

    host.control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual(['Enter a valid email address.']);
  });

  it('prefers a host-supplied [messages] override over the built-in default', () => {
    host.messages.set({ required: 'You must provide an email address.' });
    fixture.detectChanges();

    host.control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual(['You must provide an email address.']);
  });

  it('clears the client error text and aria-invalid once the control becomes valid', () => {
    fixture.detectChanges();
    host.control.markAsTouched();
    fixture.detectChanges();
    expect(projectedInput().getAttribute('aria-invalid')).toBe('true');

    // Provide a valid value -> the required error resolves; a status change re-evaluates the computeds.
    host.control.setValue('user@example.com');
    fixture.detectChanges();

    expect(errorTexts().length).toBe(0);
    expect(projectedInput().getAttribute('aria-invalid')).toBeNull();
  });

  it('exposes aria-required="true" and the native required attribute when the control has Validators.required', () => {
    fixture.detectChanges();

    const input = projectedInput();
    expect(input.getAttribute('aria-required')).toBe('true');
    expect(input.hasAttribute('required')).toBe(true);
  });

  it('omits aria-required when the control has no required validator', () => {
    host.control = new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.email],
    });
    fixture.detectChanges();

    const input = projectedInput();
    expect(input.getAttribute('aria-required')).toBeNull();
    expect(input.hasAttribute('required')).toBe(false);
  });
});
