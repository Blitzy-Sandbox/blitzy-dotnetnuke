import { Component, DebugElement } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { TooltipDirective } from './tooltip.directive';

/**
 * Unit tests for {@link TooltipDirective}.
 *
 * The directive is an accessible, attribute-driven tooltip: it shows on
 * `mouseenter`/`focus` and hides on `mouseleave`/`blur`/`Escape`. While visible it
 * appends a `role="tooltip"` element (class `app-tooltip`, unique id) to
 * `document.body` and wires the host's `aria-describedby` to that id; empty or
 * whitespace-only text shows nothing. These specs drive that behaviour through a
 * minimal standalone host component so the real `@HostListener` plumbing is covered.
 *
 * Event-triggering strategy:
 *  - `mouseenter`/`mouseleave`/`focus`/`blur` are raised through
 *    `DebugElement.triggerEventHandler`; the directive's handlers ignore the event
 *    object, so an empty object is sufficient.
 *  - `Escape` is dispatched as a real `KeyboardEvent` on the host element, because
 *    Angular's key-events plugin filters `keydown.escape` by `event.key`. A synthetic
 *    empty event object would not satisfy that filter, which is the most common cause
 *    of a flaky tooltip-directive spec.
 *
 * Assertions read from `document` because the tooltip element is appended to
 * `document.body`, i.e. outside the component fixture's DOM subtree.
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
    // Remove any stray tooltip nodes so DOM state never leaks between specs.
    document.querySelectorAll('[role="tooltip"]').forEach((node) => node.remove());
  });

  /** Raise a host event the directive listens to, then flush change detection. */
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
