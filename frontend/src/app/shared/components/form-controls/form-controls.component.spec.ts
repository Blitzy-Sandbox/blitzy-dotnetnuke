import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
import { By } from '@angular/platform-browser';

import { FormControlsComponent } from './form-controls.component';

/**
 * Specs for the shared <app-form-controls> wrapper (CP2 review M11).
 *
 * Covers the three behaviours the review required: label<->input association,
 * RFC 7807 ProblemDetails server-error rendering, and the ARIA wiring
 * (aria-invalid + aria-describedby -> the live error region). The component has
 * REQUIRED signal inputs (control, controlId) and a constructor effect() that reads
 * this.control(), so both are set BEFORE the first detectChanges() in every path.
 */
describe('FormControlsComponent', () => {
  let component: FormControlsComponent;
  let fixture: ComponentFixture<FormControlsComponent>;
  let control: FormControl;

  /** Query the single bound input. */
  function inputEl(): HTMLInputElement {
    return fixture.debugElement.query(By.css('input.form-controls__input'))
      .nativeElement as HTMLInputElement;
  }

  /** Query the rendered inline error messages. */
  function errorTexts(): string[] {
    return fixture.debugElement
      .queryAll(By.css('.form-controls__error'))
      .map((de) => (de.nativeElement as HTMLElement).textContent?.trim() ?? '');
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FormControlsComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FormControlsComponent);
    component = fixture.componentInstance;

    // Required inputs MUST be set before the first detectChanges() (the constructor
    // effect reads this.control()).
    control = new FormControl('');
    fixture.componentRef.setInput('control', control);
    fixture.componentRef.setInput('controlId', 'email');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('binds the reactive form control to the input value', () => {
    control.setValue('hello@dnn.test');
    fixture.detectChanges();
    expect(inputEl().value).toBe('hello@dnn.test');
  });

  it('associates the label with the input via matching for/id', () => {
    fixture.componentRef.setInput('label', 'Email address');
    fixture.detectChanges();

    const label = fixture.debugElement.query(By.css('label.form-controls__label'))
      .nativeElement as HTMLLabelElement;
    expect(label.textContent?.trim()).toBe('Email address');
    expect(label.getAttribute('for')).toBe('email');
    expect(inputEl().id).toBe('email');
  });

  it('omits the label element when no label text is provided', () => {
    // Default label input is '' -> the @if(label()) block renders nothing.
    expect(fixture.debugElement.query(By.css('label.form-controls__label'))).toBeNull();
  });

  it('applies the type and placeholder inputs to the native input', () => {
    fixture.componentRef.setInput('type', 'email');
    fixture.componentRef.setInput('placeholder', 'you@example.com');
    fixture.detectChanges();

    expect(inputEl().type).toBe('email');
    expect(inputEl().placeholder).toBe('you@example.com');
  });

  it('wires aria-describedby to the live error region (errorId)', () => {
    const region = fixture.debugElement.query(By.css('.form-controls__errors'))
      .nativeElement as HTMLElement;

    expect(inputEl().getAttribute('aria-describedby')).toBe('email-errors');
    expect(region.id).toBe('email-errors');
    expect(region.getAttribute('aria-live')).toBe('polite');
  });

  it('does not render validator messages while the control is untouched and pristine', () => {
    control = new FormControl('', { validators: Validators.required });
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    expect(errorTexts().length).toBe(0);
    // No visible errors -> aria-invalid attribute is omitted entirely.
    expect(inputEl().getAttribute('aria-invalid')).toBeNull();
  });

  it('renders client-side validator messages once touched and marks aria-invalid', () => {
    control = new FormControl('', { validators: Validators.required });
    fixture.componentRef.setInput('control', control);
    control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual(['This field is required.']);
    expect(inputEl().getAttribute('aria-invalid')).toBe('true');
  });

  it('honours a custom per-validator message override', () => {
    control = new FormControl('', { validators: Validators.required });
    fixture.componentRef.setInput('control', control);
    fixture.componentRef.setInput('messages', { required: 'Email is required.' });
    control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual(['Email is required.']);
  });

  it('renders RFC 7807 ProblemDetails server-side field errors for this controlId', () => {
    // Server errors are keyed by controlId and shown regardless of touched/dirty.
    fixture.componentRef.setInput('serverErrors', {
      email: ['Email is already registered.'],
    });
    fixture.detectChanges();

    expect(errorTexts()).toContain('Email is already registered.');
    expect(inputEl().getAttribute('aria-invalid')).toBe('true');
  });

  it('merges client-side and server-side errors, client message first', () => {
    control = new FormControl('', { validators: Validators.required });
    fixture.componentRef.setInput('control', control);
    fixture.componentRef.setInput('serverErrors', {
      email: ['Email is already registered.'],
    });
    control.markAsTouched();
    fixture.detectChanges();

    expect(errorTexts()).toEqual([
      'This field is required.',
      'Email is already registered.',
    ]);
  });
});
