import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { TooltipDirective } from './tooltip.directive';

/**
 * Dedicated spec for {@link TooltipDirective} (QA Report 10 Issue 2 -- shared
 * directives lacked dedicated specs; tooltip sat at ~39%).
 *
 * MIGRATION: the directive re-expresses the legacy DotNetNuke help/hint
 * affordances (`<dnn:label>` help text on admin screens) as an accessible
 * hover/focus tooltip. These tests assert the accessibility-critical behaviours:
 * the tooltip is created on `mouseenter`/`focus` as a `role="tooltip"` node in
 * `document.body`, the host is wired via `aria-describedby`, it is removed on
 * `mouseleave`/`blur`/Escape, an empty text binding is a no-op, and the tooltip
 * is cleaned up on host destroy (no orphaned nodes). Contributes to Gate 4.
 */
@Component({
  imports: [TooltipDirective],
  template: `<button type="button" appTooltip="Help text">Btn</button>`,
})
class HostComponent {}

@Component({
  imports: [TooltipDirective],
  template: `<button type="button" appTooltip="">Btn</button>`,
})
class EmptyTooltipHostComponent {}

describe('TooltipDirective', () => {
  /** Any tooltip currently attached to document.body, or null. */
  function tooltipInBody(): HTMLElement | null {
    return document.body.querySelector('[role="tooltip"]');
  }

  afterEach(() => {
    // Defensive: remove any tooltip a test left attached to the body.
    document.body.querySelectorAll('[role="tooltip"]').forEach((n) => n.remove());
  });

  it('shows a role="tooltip" node with the text and wires aria-describedby on mouseenter', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new MouseEvent('mouseenter'));

    const tip = tooltipInBody();
    expect(tip).not.toBeNull();
    expect(tip?.textContent).toBe('Help text');
    expect(tip?.id).toBeTruthy();
    expect(btn.getAttribute('aria-describedby')).toBe(tip?.id ?? '');
  });

  it('hides the tooltip and drops aria-describedby on mouseleave', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new MouseEvent('mouseenter'));
    expect(tooltipInBody()).not.toBeNull();

    btn.dispatchEvent(new MouseEvent('mouseleave'));
    expect(tooltipInBody()).toBeNull();
    expect(btn.hasAttribute('aria-describedby')).toBeFalse();
  });

  it('shows on focus and hides on blur (keyboard parity)', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new FocusEvent('focus'));
    expect(tooltipInBody()).not.toBeNull();

    btn.dispatchEvent(new FocusEvent('blur'));
    expect(tooltipInBody()).toBeNull();
  });

  it('hides on the Escape key', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new FocusEvent('focus'));
    expect(tooltipInBody()).not.toBeNull();

    btn.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(tooltipInBody()).toBeNull();
  });

  it('does not re-create a second tooltip on repeated mouseenter', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new MouseEvent('mouseenter'));
    btn.dispatchEvent(new MouseEvent('mouseenter'));

    expect(document.body.querySelectorAll('[role="tooltip"]').length).toBe(1);
  });

  it('is a no-op when the bound text is empty', () => {
    const fixture = TestBed.createComponent(EmptyTooltipHostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new MouseEvent('mouseenter'));

    expect(tooltipInBody()).toBeNull();
    expect(btn.hasAttribute('aria-describedby')).toBeFalse();
  });

  it('cleans up the tooltip when the host is destroyed (ngOnDestroy)', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    btn.dispatchEvent(new MouseEvent('mouseenter'));
    expect(tooltipInBody()).not.toBeNull();

    fixture.destroy();
    expect(tooltipInBody()).toBeNull();
  });
});
