import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { LoadingSpinnerComponent } from './loading-spinner.component';

/**
 * Unit tests for {@link LoadingSpinnerComponent}.
 *
 * The component is a standalone, OnPush, presentation-only leaf with three signal
 * inputs (`loading`, `message`, `diameter`). It is therefore exercised through a
 * standalone `TestBed` (`imports: [LoadingSpinnerComponent]`, never `declarations`)
 * and asserted against its rendered DOM: the `role="status"` polite live region,
 * the visually-hidden `.loading-spinner__label`, and the `.loading-spinner__svg`
 * sizing binding. Testing the template output verifies the accessibility contract
 * and the input -> template wiring in a single pass.
 *
 * Signal inputs are read-only handles, so values are driven via
 * `fixture.componentRef.setInput(name, value)` followed by `fixture.detectChanges()`
 * — the verified Angular 19 API. With OnPush change detection, `setInput` marks the
 * view dirty and `detectChanges()` re-renders; the `booleanAttribute` /
 * `numberAttribute` transforms run inside `setInput`.
 */
describe('LoadingSpinnerComponent', () => {
  let component: LoadingSpinnerComponent;
  let fixture: ComponentFixture<LoadingSpinnerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoadingSpinnerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(LoadingSpinnerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders the status live region by default', () => {
    const status = fixture.debugElement.query(By.css('[role="status"]'));
    expect(status).not.toBeNull();
  });

  it('exposes polite live-region accessibility attributes', () => {
    const status = fixture.debugElement.query(By.css('[role="status"]'))
      .nativeElement as HTMLElement;
    expect(status.getAttribute('aria-live')).toBe('polite');
    expect(status.getAttribute('aria-busy')).toBe('true');
  });

  it('renders the default loading message for assistive technology', () => {
    const label = fixture.debugElement.query(By.css('.loading-spinner__label'))
      .nativeElement as HTMLElement;
    expect(label.textContent?.trim()).toBe('Loading...');
  });

  it('renders a custom message when provided', () => {
    fixture.componentRef.setInput('message', 'Saving portal');
    fixture.detectChanges();

    const label = fixture.debugElement.query(By.css('.loading-spinner__label'))
      .nativeElement as HTMLElement;
    expect(label.textContent?.trim()).toBe('Saving portal');
  });

  it('hides the spinner when loading is false', () => {
    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();

    const status = fixture.debugElement.query(By.css('[role="status"]'));
    expect(status).toBeNull();
  });

  it('applies the diameter to the spinner svg', () => {
    fixture.componentRef.setInput('diameter', 64);
    fixture.detectChanges();

    const svg = fixture.debugElement.query(By.css('.loading-spinner__svg'))
      .nativeElement as SVGElement;
    expect(svg.style.width).toBe('64px');
    expect(svg.style.height).toBe('64px');
  });
});
