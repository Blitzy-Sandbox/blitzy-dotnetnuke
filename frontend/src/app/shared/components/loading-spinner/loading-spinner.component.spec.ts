// MIGRATION: Spec for the net-new, purely presentational LoadingSpinnerComponent. No legacy equivalent
// (DNN Web Forms used full-page server postbacks with no client-side busy indicator; AAP 0.6.2 / 0.3.7).
// Validates the accessibility contract (role="status" + aria-live, aria-hidden glyph, .sr-only label) and
// signal-input rendering. Presentational only: no HttpClient/router/animations providers required.
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LoadingSpinnerComponent } from './loading-spinner.component';

describe('LoadingSpinnerComponent', () => {
  let fixture: ComponentFixture<LoadingSpinnerComponent>;
  let component: LoadingSpinnerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoadingSpinnerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(LoadingSpinnerComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render nothing when loading() is false', () => {
    // Default loading() is false — render and assert the status region is absent.
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement | null;
    expect(status).toBeNull();
  });

  it('should render an accessible spinner when loading() is true', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement | null;
    expect(status).not.toBeNull();
    expect(status?.getAttribute('aria-live')).toBe('polite');

    const glyph = fixture.nativeElement.querySelector('.loading-spinner__glyph') as HTMLElement | null;
    expect(glyph).not.toBeNull();
    expect(glyph?.getAttribute('aria-hidden')).toBe('true');

    const srLabel = fixture.nativeElement.querySelector('.sr-only') as HTMLElement | null;
    expect(srLabel).not.toBeNull();
    // Read the default from the signal to avoid hard-coding the ellipsis glyph.
    expect(srLabel?.textContent?.trim()).toBe(component.label());
  });

  it('should render a custom accessible label when provided', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.componentRef.setInput('label', 'Saving changes');
    fixture.detectChanges();

    const srLabel = fixture.nativeElement.querySelector('.sr-only') as HTMLElement | null;
    expect(srLabel?.textContent?.trim()).toBe('Saving changes');
  });
});
