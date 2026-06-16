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
});
