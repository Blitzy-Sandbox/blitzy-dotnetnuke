import { afterNextRender, booleanAttribute, Directive, ElementRef, inject, input } from '@angular/core';

@Directive({
  selector: '[appAutofocus]',
})
export class AutofocusDirective {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  /**
   * Controls whether the host element receives focus after render.
   * Defaults to `true`. `booleanAttribute` lets `<input appAutofocus>` (bare attribute → '')
   * resolve to `true`, while `[appAutofocus]="false"` skips focusing.
   */
  readonly appAutofocus = input(true, { transform: booleanAttribute });

  constructor() {
    afterNextRender(() => {
      if (this.appAutofocus()) {
        this.elementRef.nativeElement.focus();
      }
    });
  }
}
