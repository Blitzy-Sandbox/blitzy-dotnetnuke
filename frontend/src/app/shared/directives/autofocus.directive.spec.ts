// MIGRATION: Spec for the net-new AutofocusDirective (no legacy equivalent). Verifies the directive
// moves focus to its host element after view init when enabled, and does not focus when disabled.
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AutofocusDirective } from './autofocus.directive';

// Host that enables autofocus via the bare attribute (default-on behavior).
@Component({
  selector: 'app-enabled-host',
  standalone: true,
  imports: [AutofocusDirective],
  template: `<input type="text" appAutofocus />`,
})
class EnabledHostComponent {}

// Host that explicitly disables autofocus via the bracket-bound input.
@Component({
  selector: 'app-disabled-host',
  standalone: true,
  imports: [AutofocusDirective],
  template: `<input type="text" [appAutofocus]="false" />`,
})
class DisabledHostComponent {}

describe('AutofocusDirective', () => {
  const attached: HTMLElement[] = [];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EnabledHostComponent, DisabledHostComponent],
    });
  });

  afterEach(() => {
    // Detach every element added to document.body and reset focus to isolate tests.
    while (attached.length > 0) {
      const el = attached.pop();
      el?.parentNode?.removeChild(el);
    }
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur();
    }
  });

  it('should create the directive on its host element', () => {
    const fixture = TestBed.createComponent(EnabledHostComponent);
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    attached.push(el);
    fixture.detectChanges();
    expect(el.querySelector('input')).toBeTruthy();
  });

  it('should focus the host element after view init (enabled by default)', () => {
    const fixture = TestBed.createComponent(EnabledHostComponent);
    const el = fixture.nativeElement as HTMLElement;
    // Must be in the live DOM for HTMLElement.focus() to update document.activeElement.
    document.body.appendChild(el);
    attached.push(el);
    fixture.detectChanges(); // first detectChanges runs ngAfterViewInit -> focus()
    const input = el.querySelector('input');
    expect(document.activeElement).toBe(input);
  });

  it('should not focus the host element when disabled via [appAutofocus]="false"', () => {
    const fixture = TestBed.createComponent(DisabledHostComponent);
    const el = fixture.nativeElement as HTMLElement;
    document.body.appendChild(el);
    attached.push(el);
    fixture.detectChanges();
    const input = el.querySelector('input');
    expect(document.activeElement).not.toBe(input);
  });
});
