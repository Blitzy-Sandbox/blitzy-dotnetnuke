/**
 * Unit tests for {@link TooltipDirective}.
 *
 * Verifies the accessible-tooltip contract that backs the migrated DotNetNuke
 * admin UI (Gate 4: `ng test --watch=false --browsers=ChromeHeadless`):
 *  - shows a `role="tooltip"` element on `mouseenter`/`focus` and links it to the
 *    host via `aria-describedby`;
 *  - hides it (and clears `aria-describedby`) on `mouseleave`/`blur`/`Escape`;
 *  - renders nothing for empty/whitespace text;
 *  - cleans up the detached tooltip node when the host component is destroyed.
 *
 * Standalone-only TestBed setup (Angular 19): a tiny inline host component imports
 * the directive directly — no NgModule. The tooltip element is appended to
 * `document.body` (outside the fixture), so assertions query `document`.
 */
import { Component, DebugElement } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { TooltipDirective } from './tooltip.directive';

/**
 * Minimal standalone test host. The `[appTooltip]="text"` expression binding lets
 * individual specs mutate the tooltip text (e.g. to the empty-string case) and
 * re-run change detection so the signal input picks up the new value.
 */
@Component({
  template: `<button type="button" [appTooltip]="text">Action</button>`,
  imports: [TooltipDirective],
})
class TooltipHostComponent {
  text = 'Delete portal';
}

describe('TooltipDirective', () => {
  let fixture: ComponentFixture<TooltipHostComponent>;
  let directiveEl: DebugElement;
  let host: HTMLButtonElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TooltipHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TooltipHostComponent);
    fixture.detectChanges();

    directiveEl = fixture.debugElement.query(By.directive(TooltipDirective));
    host = directiveEl.nativeElement as HTMLButtonElement;
  });

  afterEach(() => {
    // Remove any tooltip nodes left attached to document.body so DOM state does
    // not leak between specs (the tooltip lives outside the fixture root).
    document.querySelectorAll('[role="tooltip"]').forEach((node) => node.remove());
  });

  /**
   * Fires a host event the directive listens for (mouseenter/mouseleave/focus/
   * blur) and flushes change detection. The directive's handlers ignore the event
   * payload, so an empty object is a sufficient stand-in.
   */
  function trigger(eventName: string): void {
    directiveEl.triggerEventHandler(eventName, {});
    fixture.detectChanges();
  }

  it('creates the host with the directive applied', () => {
    expect(host).toBeTruthy();
  });

  it('shows a tooltip on mouseenter and links it via aria-describedby', () => {
    trigger('mouseenter');

    const id = host.getAttribute('aria-describedby');
    expect(id).toBeTruthy();

    // `id` is guaranteed non-null by the assertion above; narrow with `as string`
    // (never `any`) so getElementById type-checks under strict null handling.
    const tooltip = document.getElementById(id as string);
    expect(tooltip).not.toBeNull();
    expect(tooltip?.getAttribute('role')).toBe('tooltip');
    expect(tooltip?.textContent).toContain('Delete portal');
  });

  it('hides the tooltip and clears aria-describedby on mouseleave', () => {
    trigger('mouseenter');
    trigger('mouseleave');

    expect(host.getAttribute('aria-describedby')).toBeNull();
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });

  it('shows on focus and hides on blur', () => {
    trigger('focus');
    expect(document.querySelector('[role="tooltip"]')).not.toBeNull();

    trigger('blur');
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });

  it('hides the tooltip when Escape is pressed', () => {
    trigger('mouseenter');
    expect(document.querySelector('[role="tooltip"]')).not.toBeNull();

    // Dispatch a real key-filtered event: `@HostListener('keydown.escape')` is
    // wired through Angular's key-events plugin, which matches on `event.key`.
    // A bare `triggerEventHandler('keydown.escape', {})` would not populate that,
    // so exercise the actual native listener instead.
    host.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });

  it('does not show a tooltip when the text is empty', () => {
    fixture.componentInstance.text = '';
    fixture.detectChanges();

    trigger('mouseenter');

    expect(host.getAttribute('aria-describedby')).toBeNull();
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });

  it('removes the tooltip when the host is destroyed', () => {
    trigger('mouseenter');
    expect(document.querySelector('[role="tooltip"]')).not.toBeNull();

    fixture.destroy();

    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });
});
