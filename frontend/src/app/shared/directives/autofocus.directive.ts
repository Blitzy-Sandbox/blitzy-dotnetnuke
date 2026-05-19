/**
 * Angular 19 Standalone Autofocus Directive
 *
 * MIGRATION NOTE: This directive replaces DNN WebForms server-side focus handling patterns,
 * specifically:
 * - ClientAPI.RegisterClientVariable and JavaScript focus patterns
 * - SecurityRoles.ascx.vb line 329 calendar popup focus handling
 * - EditRoles.ascx.vb line ~112 delete confirm button focus with ClientAPI.AddButtonConfirm
 *
 * Provides declarative client-side focus control using Angular's attribute directive pattern.
 * Supports optional delay parameter for deferred focus after component rendering.
 *
 * Usage examples:
 * - Basic: <input appAutofocus />
 * - Conditional: <input [appAutofocus]="shouldFocus" />
 * - With delay: <input appAutofocus [focusDelay]="100" />
 * - Conditional with delay: <input [appAutofocus]="isActive" [focusDelay]="200" />
 *
 * @module SharedDirectives
 */

import {
  AfterViewInit,
  Directive,
  ElementRef,
  inject,
  Input,
} from '@angular/core';

/**
 * Autofocus directive for automatic focus management on HTML elements.
 *
 * This standalone directive provides declarative focus control, replacing
 * DNN's server-side ClientAPI focus handling patterns. It executes focus
 * logic after the view is initialized to ensure the element is rendered.
 *
 * The directive supports:
 * - Conditional focusing based on boolean or string input
 * - Delayed focus execution for async rendering scenarios
 * - Proper null/undefined handling for element references
 * - Optional scroll into view behavior
 *
 * @example
 * // Auto-focus on element when component loads
 * <input type="text" appAutofocus placeholder="Username" />
 *
 * @example
 * // Conditionally auto-focus based on component state
 * <input type="email" [appAutofocus]="isEditMode" />
 *
 * @example
 * // Auto-focus with delay for async content
 * <input type="text" appAutofocus [focusDelay]="150" />
 */
@Directive({
  selector: '[appAutofocus]',
  standalone: true,
})
export class AutofocusDirective implements AfterViewInit {
  /**
   * ElementRef injection using Angular 19 inject() pattern.
   * Provides access to the native DOM element for focus operations.
   */
  private readonly elementRef = inject(ElementRef);

  /**
   * Controls whether the autofocus should be applied.
   *
   * Accepts boolean or string values:
   * - true, 'true', '', or any truthy value: Focus is applied
   * - false, 'false', null, undefined: Focus is not applied
   *
   * When the attribute is present without a value (e.g., <input appAutofocus />),
   * it defaults to an empty string which is treated as truthy.
   *
   * MIGRATION NOTE: Replaces DNN's pattern of setting focus via server-side
   * ClientAPI.RegisterClientVariable calls and JavaScript focus scripts.
   */
  @Input() appAutofocus: boolean | string = '';

  /**
   * Optional delay in milliseconds before applying focus.
   *
   * Useful for scenarios where:
   * - The element is rendered asynchronously
   * - Animations need to complete before focus
   * - Multiple elements compete for initial focus
   *
   * Default is 0ms (immediate focus after view initialization).
   *
   * MIGRATION NOTE: Replaces DNN's setTimeout-based focus patterns used
   * in calendar popup initialization and modal dialog focus management.
   */
  @Input() focusDelay: number = 0;

  /**
   * Optional flag to control whether the element should be scrolled into view
   * when focused. Default is false to prevent unexpected page scrolling.
   */
  @Input() scrollIntoView: boolean = false;

  /**
   * AfterViewInit lifecycle hook implementation.
   *
   * Executes focus logic after Angular has fully initialized the component's view,
   * ensuring the native element is available in the DOM.
   *
   * MIGRATION NOTE: This replaces DNN's Page_Load event handler focus logic
   * and ClientAPI initialization scripts that ran after page render.
   */
  ngAfterViewInit(): void {
    // Check if focus should be applied based on the appAutofocus input value
    if (!this.shouldApplyFocus()) {
      return;
    }

    // Apply focus with optional delay
    this.applyFocusWithDelay();
  }

  /**
   * Determines whether focus should be applied based on the appAutofocus input.
   *
   * Handles various input scenarios:
   * - Empty string (attribute present without value): true
   * - Boolean true: true
   * - String 'true': true
   * - Boolean false: false
   * - String 'false': false
   * - null/undefined: false
   *
   * @returns True if focus should be applied, false otherwise
   */
  private shouldApplyFocus(): boolean {
    const value = this.appAutofocus;

    // Handle null/undefined
    if (value === null || value === undefined) {
      return false;
    }

    // Handle boolean values
    if (typeof value === 'boolean') {
      return value;
    }

    // Handle string values
    if (typeof value === 'string') {
      // Empty string (attribute present without value) is truthy
      if (value === '') {
        return true;
      }

      // Explicit 'false' string is falsy
      if (value.toLowerCase() === 'false') {
        return false;
      }

      // Any other non-empty string is truthy
      return true;
    }

    // Default to truthy for any other value
    return Boolean(value);
  }

  /**
   * Applies focus to the element with the configured delay.
   *
   * Uses setTimeout to ensure proper timing, especially for:
   * - Elements rendered with ngIf conditions
   * - Dynamic content loaded asynchronously
   * - Animation completion scenarios
   *
   * MIGRATION NOTE: Replaces DNN's JavaScript setTimeout patterns used
   * in SecurityRoles.ascx.vb for calendar popup focus.
   */
  private applyFocusWithDelay(): void {
    // Ensure delay is a valid non-negative number
    const delay = Math.max(0, this.focusDelay || 0);

    if (delay === 0) {
      // Apply focus immediately
      this.setFocus();
    } else {
      // Apply focus after the specified delay
      setTimeout(() => {
        this.setFocus();
      }, delay);
    }
  }

  /**
   * Sets focus on the native element.
   *
   * Performs null/undefined checks to prevent runtime errors,
   * and optionally scrolls the element into view.
   *
   * MIGRATION NOTE: This is the Angular equivalent of DNN's
   * document.getElementById(clientId).focus() JavaScript calls.
   */
  private setFocus(): void {
    // Get the native DOM element
    const element = this.elementRef?.nativeElement;

    // Validate element exists and has focus method
    if (!element || typeof element.focus !== 'function') {
      // Element not available or doesn't support focus - silently ignore
      // This handles cases where element may have been removed from DOM
      return;
    }

    try {
      // Apply focus to the element
      // Using preventScroll option by default to prevent unexpected page jumps
      // unless scrollIntoView is explicitly requested
      element.focus({ preventScroll: !this.scrollIntoView });

      // Optionally scroll element into view after focus
      if (this.scrollIntoView && typeof element.scrollIntoView === 'function') {
        element.scrollIntoView({
          behavior: 'smooth',
          block: 'center',
          inline: 'nearest',
        });
      }
    } catch (error) {
      // Silently handle any focus-related errors
      // This can occur in edge cases like:
      // - Element removed from DOM between check and focus call
      // - Focus prevented by browser security policies
      // - Element hidden or not focusable
      console.warn('AutofocusDirective: Unable to focus element', error);
    }
  }
}
