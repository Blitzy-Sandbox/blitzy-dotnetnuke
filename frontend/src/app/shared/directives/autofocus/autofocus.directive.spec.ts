import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { AutofocusDirective } from './autofocus.directive';

/**
 * Unit tests for {@link AutofocusDirective}.
 *
 * The directive is exercised through a standalone host component so the real Angular render
 * pipeline drives its `afterNextRender` hook. Focus is asserted by spying on the host element's
 * `focus()` method rather than by reading `document.activeElement`: the TestBed fixture is not
 * attached to `document.body`, so a genuine `focus()` call never updates `document.activeElement`
 * in the Karma harness (Angular issue #57313). The spy is therefore installed AFTER
 * `createComponent()` — which instantiates the directive and registers, but does not flush, the
 * `afterNextRender` hook — and BEFORE `detectChanges()` / `whenStable()`, which drive the render
 * that flushes the hook. This guarantees the spy is in place exactly when the hook fires and keeps
 * the assertion deterministic and independent of real DOM focus.
 */
@Component({
  template: `<input type="text" [appAutofocus]="enabled" />`,
  imports: [AutofocusDirective],
})
class TestHostComponent {
  enabled = true;
}

describe('AutofocusDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
  });

  it('should create the host with the directive applied', () => {
    const directiveEl = fixture.debugElement.query(By.directive(AutofocusDirective));
    expect(directiveEl).toBeTruthy();
  });

  it('should focus the host element after render when enabled', async () => {
    const input = fixture.debugElement.query(By.directive(AutofocusDirective)).nativeElement as HTMLInputElement;
    const focusSpy = spyOn(input, 'focus');

    fixture.detectChanges();
    await fixture.whenStable();

    expect(focusSpy).toHaveBeenCalledTimes(1);
  });

  it('should not focus the host element when disabled', async () => {
    fixture.componentInstance.enabled = false;
    const input = fixture.debugElement.query(By.directive(AutofocusDirective)).nativeElement as HTMLInputElement;
    const focusSpy = spyOn(input, 'focus');

    fixture.detectChanges();
    await fixture.whenStable();

    expect(focusSpy).not.toHaveBeenCalled();
  });
});
