import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AutofocusDirective } from './autofocus.directive';

/**
 * Dedicated spec for {@link AutofocusDirective} (QA Report 10 Issue 2 -- shared
 * directives lacked dedicated specs).
 *
 * MIGRATION: the directive replaces the legacy DotNetNuke Web Forms client focus
 * scripting (Page.SetFocus on the first field of an edit/create screen). These
 * tests assert the two behaviours that matter: the bare `appAutofocus` attribute
 * (coerced to `true` by `booleanAttribute`) focuses the host on view init, and an
 * explicit `[appAutofocus]="false"` binding suppresses focus.
 *
 * The host element's `focus()` is spied on `HTMLInputElement.prototype` so the
 * assertion is deterministic and does not depend on `document.activeElement`
 * (which requires the element to be attached to the live document). Contributes
 * to Gate 4.
 */
@Component({
  imports: [AutofocusDirective],
  template: `<input type="text" appAutofocus />`,
})
class EnabledHostComponent {}

@Component({
  imports: [AutofocusDirective],
  template: `<input type="text" [appAutofocus]="false" />`,
})
class DisabledHostComponent {}

describe('AutofocusDirective', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EnabledHostComponent, DisabledHostComponent],
    });
  });

  it('focuses the host element on view init (bare attribute -> true)', () => {
    const focusSpy = spyOn(HTMLInputElement.prototype, 'focus');
    const fixture = TestBed.createComponent(EnabledHostComponent);

    // First change detection fires ngAfterViewInit, which invokes focus().
    fixture.detectChanges();

    expect(focusSpy).toHaveBeenCalledTimes(1);
  });

  it('does NOT focus when [appAutofocus]="false"', () => {
    const focusSpy = spyOn(HTMLInputElement.prototype, 'focus');
    const fixture = TestBed.createComponent(DisabledHostComponent);

    fixture.detectChanges();

    expect(focusSpy).not.toHaveBeenCalled();
  });
});
