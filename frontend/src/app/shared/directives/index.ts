/**
 * @fileoverview Barrel file exporting all shared Angular 19 standalone directives.
 *
 * This module provides a single-point export for all shared directives, following
 * Angular 19 best practices for feature-based organization and simplified imports.
 *
 * MIGRATION NOTE: Barrel file pattern recommended by Angular 19 best practices
 * (Section 0.3.2 Web Search Research) for feature-based organization and
 * simplified imports across feature modules.
 *
 * @usage
 * Components can import individual directives:
 * ```typescript
 * import { HasPermissionDirective } from '@shared/directives';
 * ```
 *
 * Or import the full array for component imports:
 * ```typescript
 * import { SHARED_DIRECTIVES } from '@shared/directives';
 *
 * @Component({
 *   selector: 'app-example',
 *   standalone: true,
 *   imports: [SHARED_DIRECTIVES],
 *   template: `...`
 * })
 * export class ExampleComponent {}
 * ```
 *
 * @module shared/directives
 */

// Import all directives for re-export and SHARED_DIRECTIVES array
import { AutofocusDirective } from './autofocus.directive';
import { HasPermissionDirective } from './has-permission.directive';
import { ValidationHighlightDirective } from './validation-highlight.directive';
import { TooltipDirective } from './tooltip.directive';

/**
 * AutofocusDirective - Automatic focus management for HTML elements.
 *
 * Provides declarative client-side focus control, replacing DNN WebForms
 * server-side ClientAPI focus handling patterns. Supports conditional focusing
 * and configurable delay for async rendering scenarios.
 *
 * @example
 * ```html
 * <input appAutofocus />
 * <input [appAutofocus]="shouldFocus" [focusDelay]="100" />
 * ```
 *
 * @see autofocus.directive.ts for full documentation
 */
export { AutofocusDirective };

/**
 * HasPermissionDirective - Permission-based visibility structural directive.
 *
 * Conditionally renders content based on user permissions validated against
 * JWT claims. Replaces DNN's server-side PortalSecurity.IsInRoles() checks
 * with client-side role-based DOM manipulation.
 *
 * @example
 * ```html
 * <button *appHasPermission="'Administrator'">Delete</button>
 * <div *appHasPermission="['Administrator', 'Editor']; else noAccess">
 *   Protected content
 * </div>
 * ```
 *
 * @see has-permission.directive.ts for full documentation
 */
export { HasPermissionDirective };

/**
 * ValidationHighlightDirective - Automatic form field validation highlighting.
 *
 * Applies CSS classes to form controls based on their validation state,
 * providing visual feedback to users. Replaces DNN WebForms RequiredFieldValidator
 * and CustomValidator visual feedback patterns.
 *
 * @example
 * ```html
 * <input formControlName="email" appValidationHighlight />
 * <input formControlName="password"
 *        appValidationHighlight
 *        errorClass="custom-error"
 *        successClass="custom-success" />
 * ```
 *
 * @see validation-highlight.directive.ts for full documentation
 */
export { ValidationHighlightDirective };

/**
 * TooltipDirective - Hover and focus-triggered contextual help tooltips.
 *
 * Provides accessible, customizable tooltips with configurable placement,
 * delay, and styling. Replaces DNN's server-side tooltip controls and
 * Calendar.InvokePopupCal patterns with client-side tooltip rendering.
 *
 * @example
 * ```html
 * <button appTooltip="Click to submit" tooltipPosition="top">Submit</button>
 * <input appTooltip="Enter email" tooltipPosition="right" tooltipDelay="300" />
 * ```
 *
 * @see tooltip.directive.ts for full documentation
 */
export { TooltipDirective };

/**
 * Convenience array containing all shared directive classes.
 *
 * Use this array in component imports to include all shared directives at once.
 * Optimized for tree-shaking - Angular will only bundle directives actually
 * used in templates.
 *
 * @example
 * ```typescript
 * import { SHARED_DIRECTIVES } from '@shared/directives';
 *
 * @Component({
 *   selector: 'app-my-feature',
 *   standalone: true,
 *   imports: [CommonModule, SHARED_DIRECTIVES],
 *   template: `
 *     <input appAutofocus appValidationHighlight formControlName="name" />
 *     <button *appHasPermission="['Admin']" appTooltip="Save changes">Save</button>
 *   `
 * })
 * export class MyFeatureComponent {}
 * ```
 *
 * @constant
 * @type {ReadonlyArray<typeof AutofocusDirective | typeof HasPermissionDirective | typeof ValidationHighlightDirective | typeof TooltipDirective>}
 */
export const SHARED_DIRECTIVES = [
  AutofocusDirective,
  HasPermissionDirective,
  ValidationHighlightDirective,
  TooltipDirective,
] as const;
