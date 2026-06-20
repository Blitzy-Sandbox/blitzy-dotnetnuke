import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { LoadingSpinnerComponent } from './loading-spinner.component';

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
