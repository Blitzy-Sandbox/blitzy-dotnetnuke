import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LoadingSpinnerComponent } from './loading-spinner.component';

/**
 * Unit tests for {@link LoadingSpinnerComponent} — the presentation-only,
 * accessible busy indicator used across the migrated Angular 19 SPA.
 *
 * These specs back Validation Gate 4 (`ng test --watch=false
 * --browsers=ChromeHeadless --code-coverage`) and assert the component's
 * conditional-render and ARIA behaviour without any network, service, or mock
 * dependencies (the component is a pure leaf with no injected collaborators).
 *
 * The component exposes its state through `input()` signal inputs, which are
 * read-only from a consumer's perspective. In tests they must therefore be set
 * via `fixture.componentRef.setInput(name, value)` followed by
 * `fixture.detectChanges()` — direct assignment (`component.loading = …`) is a
 * compile error and is intentionally never used here.
 */
describe('LoadingSpinnerComponent', () => {
  let fixture: ComponentFixture<LoadingSpinnerComponent>;
  let component: LoadingSpinnerComponent;

  /**
   * Convenience accessor for the strongly-typed host element so each assertion
   * can query the rendered DOM without repeating the cast on every call.
   */
  const host = (): HTMLElement => fixture.nativeElement as HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      // Standalone component → register in `imports`, never `declarations`.
      imports: [LoadingSpinnerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(LoadingSpinnerComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders nothing when "loading" is false (the default)', () => {
    // No input is set, so `loading()` defaults to false and the `@if` guard
    // keeps the spinner out of the DOM entirely.
    fixture.detectChanges();

    expect(host().querySelector('.loading-spinner')).toBeNull();
  });

  it('renders the spinner only when "loading" is true', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const el = host().querySelector('.loading-spinner');
    expect(el).not.toBeNull();
  });

  it('exposes role="status" and aria-live="polite" on the live region', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const el = host().querySelector('.loading-spinner');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('role')).toBe('status');
    expect(el!.getAttribute('aria-live')).toBe('polite');
  });

  it('toggles aria-busy with the "loading" state', () => {
    // When busy, the live region advertises aria-busy="true".
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const el = host().querySelector('.loading-spinner');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('aria-busy')).toBe('true');

    // When no longer busy the whole region is removed from the DOM, so the
    // aria-busy attribute is no longer present — this is the observable toggle.
    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();

    expect(host().querySelector('.loading-spinner')).toBeNull();
  });

  it('renders visually-hidden screen-reader text with the default label', () => {
    // No `message` is supplied, so the SR-only node falls back to the
    // component's default announcement.
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const sr = host().querySelector('.loading-spinner__sr-only');
    expect(sr).not.toBeNull();
    expect(sr!.textContent?.trim()).toBe('Loading…');
  });

  it('renders the optional visible message and mirrors it in the SR-only text', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.componentRef.setInput('message', 'Saving…');
    fixture.detectChanges();

    const msg = host().querySelector('.loading-spinner__message');
    expect(msg).not.toBeNull();
    expect(msg!.textContent?.trim()).toBe('Saving…');

    // With a message supplied, the screen-reader text announces that message
    // instead of the default label.
    expect(
      host().querySelector('.loading-spinner__sr-only')?.textContent?.trim(),
    ).toBe('Saving…');
  });
});
