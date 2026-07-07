import {
  AfterViewInit,
  booleanAttribute,
  Directive,
  ElementRef,
  inject,
  Input,
} from '@angular/core';

/**
 * AutofocusDirective — focuses the host element once the view is initialized.
 *
 * Usage:
 *   <input appAutofocus>                     <!-- always focuses -->
 *   <input [appAutofocus]="isEditMode">      <!-- focuses conditionally -->
 *
 * MIGRATION: replaces the legacy DotNetNuke Web Forms client focus scripting
 * that put the cursor on the first field of edit/create screens (e.g. the
 * Portal setup / signup admin screen's first textbox). Web Forms emitted a
 * client focus script (Page.SetFocus); here it is a declarative standalone
 * directive with no postback involvement.
 */
@Directive({ selector: '[appAutofocus]' })
export class AutofocusDirective implements AfterViewInit {
  private readonly host = inject(ElementRef) as ElementRef<HTMLElement>;

  /**
   * When true (the default, incl. the bare `appAutofocus` attribute), the host
   * is focused on view init. `booleanAttribute` coerces `""` (bare attribute) to
   * `true` and accepts an explicit boolean binding.
   */
  @Input({ transform: booleanAttribute }) appAutofocus = true;

  ngAfterViewInit(): void {
    if (this.appAutofocus) {
      this.host.nativeElement.focus();
    }
  }
}
