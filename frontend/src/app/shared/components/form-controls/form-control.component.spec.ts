// MIGRATION: Spec for the net-new FormControlComponent (no legacy equivalent). Verifies label/hint
// rendering, the RFC 7807 errors duality (Record<string,string[]> vs flat string[]), and the ARIA
// attributes wired onto the projected control.
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

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
