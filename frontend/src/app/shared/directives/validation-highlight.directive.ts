/**
 * @fileoverview Angular 19 standalone directive for automatic form field validation highlighting.
 * 
 * MIGRATION NOTE: This directive replaces DNN WebForms validation control visual feedback patterns:
 * - DNN Skin.AddModuleMessage with ModuleMessageType.RedError (EditGroups.ascx.vb line ~117)
 * - WebForms RequiredFieldValidator ErrorMessage CSS styling
 * - valPassword.ErrorMessage and valPassword.IsValid patterns (User.ascx.vb lines 187-188)
 * 
 * The directive integrates with Angular reactive forms FormControl status property and validators,
 * applying CSS classes based on control validation state (VALID, INVALID, PENDING).
 * 
 * @example
 * ```html
 * <!-- Basic usage with default Bootstrap classes -->
 * <input formControlName="username" appValidationHighlight />
 * 
 * <!-- Custom CSS classes -->
 * <input formControlName="email" 
 *        appValidationHighlight 
 *        errorClass="custom-error" 
 *        successClass="custom-success" />
 * 
 * <!-- With warning state -->
 * <input formControlName="password" 
 *        appValidationHighlight 
 *        warningClass="password-weak" />
 * ```
 */

import {
  Directive,
  ElementRef,
  inject,
  Renderer2,
  Input,
  OnInit,
  OnDestroy
} from '@angular/core';
import { NgControl } from '@angular/forms';
import { Subscription } from 'rxjs';

/**
 * Directive for automatic form field validation highlighting.
 * 
 * Applies CSS classes to form controls based on their validation state,
 * providing visual feedback to users. The directive listens to the form
 * control's statusChanges observable and applies/removes classes based on:
 * - Control validity (valid/invalid)
 * - User interaction (dirty/touched states)
 * 
 * This replaces the legacy DNN WebForms RequiredFieldValidator and 
 * CustomValidator visual feedback patterns with reactive form-aware 
 * CSS class application.
 * 
 * @selector [appValidationHighlight]
 * @standalone true
 */
@Directive({
  selector: '[appValidationHighlight]',
  standalone: true
})
export class ValidationHighlightDirective implements OnInit, OnDestroy {
  /**
   * CSS class to apply when the form control is in an invalid state
   * and has been touched or modified by the user.
   * 
   * Default is 'is-invalid' for Bootstrap compatibility.
   * 
   * MIGRATION NOTE: Replaces the CSS styling applied by WebForms validators
   * like valPassword.ErrorMessage display (User.ascx.vb line 187)
   */
  @Input() errorClass: string = 'is-invalid';

  /**
   * CSS class to apply when the form control is in a valid state
   * and has been touched or modified by the user.
   * 
   * Default is 'is-valid' for Bootstrap compatibility.
   * 
   * MIGRATION NOTE: Provides positive visual feedback that was not
   * standard in DNN WebForms validation patterns.
   */
  @Input() successClass: string = 'is-valid';

  /**
   * CSS class to apply when the form control has a warning state.
   * 
   * This can be used for scenarios like password strength indicators
   * or fields that pass basic validation but have potential issues.
   * 
   * Default is 'is-warning' which can be styled via custom CSS.
   * 
   * MIGRATION NOTE: Extends beyond basic DNN validation to support
   * modern UX patterns for progressive validation feedback.
   */
  @Input() warningClass: string = 'is-warning';

  /**
   * Reference to the host element for CSS class manipulation.
   * Uses Angular 19's inject() function pattern.
   */
  private readonly elementRef = inject(ElementRef);

  /**
   * Renderer for safe DOM manipulation following Angular best practices.
   * Provides addClass/removeClass methods for CSS class application.
   */
  private readonly renderer = inject(Renderer2);

  /**
   * Optional injection of NgControl to access the associated form control.
   * Uses { optional: true, self: true } to:
   * - optional: Allow directive to work even without a form control
   * - self: Only inject the control on the same element, not parent
   * 
   * MIGRATION NOTE: NgControl is the abstract base class for NgModel,
   * FormControlDirective, and FormControlName - supporting all Angular
   * form binding patterns.
   */
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  /**
   * Subscription to the form control's statusChanges observable.
   * Stored for proper cleanup in ngOnDestroy to prevent memory leaks.
   */
  private subscription: Subscription | null = null;

  /**
   * Lifecycle hook called after directive initialization.
   * 
   * Subscribes to the form control's statusChanges observable if a control
   * exists. On each status change, the directive evaluates the control's
   * state and applies appropriate CSS classes.
   * 
   * MIGRATION NOTE: Replaces the server-side validation rendering in
   * DNN WebForms with client-side reactive updates.
   */
  ngOnInit(): void {
    // Guard: Only proceed if we have a form control attached
    if (!this.ngControl || !this.ngControl.control) {
      // Log warning in development to help developers identify misuse
      if (typeof ngDevMode !== 'undefined' && ngDevMode) {
        console.warn(
          '[ValidationHighlightDirective] No form control found. ' +
          'Ensure the element has formControlName, formControl, or ngModel directive.'
        );
      }
      return;
    }

    // Subscribe to statusChanges to react to validation state changes
    this.subscription = this.ngControl.control.statusChanges.subscribe(() => {
      this.updateValidationClasses();
    });

    // Also subscribe to value changes to handle edge cases where status
    // doesn't change but we need to re-evaluate (e.g., async validators)
    const valueSubscription = this.ngControl.control.valueChanges.subscribe(() => {
      this.updateValidationClasses();
    });

    // Combine subscriptions for unified cleanup
    this.subscription.add(valueSubscription);

    // Apply initial state in case the control already has a value/state
    this.updateValidationClasses();
  }

  /**
   * Lifecycle hook called when the directive is destroyed.
   * 
   * Cleans up the statusChanges subscription to prevent memory leaks.
   * This is critical in Angular as subscriptions to Observables must be
   * manually unsubscribed when the component/directive is destroyed.
   */
  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
      this.subscription = null;
    }
  }

  /**
   * Updates CSS classes on the host element based on the form control's
   * current validation state.
   * 
   * Logic:
   * - If control is INVALID and (dirty OR touched): Apply errorClass, remove successClass
   * - If control is VALID and (dirty OR touched): Apply successClass, remove errorClass  
   * - If control is pristine (not touched and not dirty): Remove both classes
   * - Warning class is applied/removed based on custom logic (can be extended)
   * 
   * MIGRATION NOTE: This replaces the DNN pattern of:
   * - Skin.AddModuleMessage(Me, message, ModuleMessageType.RedError)
   * - valPassword.IsValid = False with valPassword.ErrorMessage = "..."
   * (EditGroups.ascx.vb line 117, User.ascx.vb line 187-188)
   */
  private updateValidationClasses(): void {
    // Guard: Ensure control exists before accessing properties
    if (!this.ngControl || !this.ngControl.control) {
      return;
    }

    const control = this.ngControl.control;
    const nativeElement = this.elementRef.nativeElement;

    // Determine if user has interacted with the control
    const hasInteracted = control.dirty || control.touched;

    // Remove warning class by default (can be conditionally applied in extended logic)
    this.renderer.removeClass(nativeElement, this.warningClass);

    if (hasInteracted) {
      if (control.invalid) {
        // Control is invalid and user has interacted
        // Apply error styling
        this.renderer.addClass(nativeElement, this.errorClass);
        this.renderer.removeClass(nativeElement, this.successClass);
        
        // MIGRATION NOTE: This replaces the DNN pattern where
        // ModuleMessageType.RedError was used for validation feedback
      } else if (control.valid) {
        // Control is valid and user has interacted
        // Apply success styling
        this.renderer.addClass(nativeElement, this.successClass);
        this.renderer.removeClass(nativeElement, this.errorClass);
      } else if (control.pending) {
        // Async validation is in progress
        // Remove both classes to show neutral state
        this.renderer.removeClass(nativeElement, this.errorClass);
        this.renderer.removeClass(nativeElement, this.successClass);
      }
    } else {
      // Control is pristine (user hasn't interacted)
      // Remove all validation styling
      this.renderer.removeClass(nativeElement, this.errorClass);
      this.renderer.removeClass(nativeElement, this.successClass);
    }
  }
}
