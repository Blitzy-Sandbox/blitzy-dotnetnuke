/**
 * @fileoverview Angular 19 Standalone Form Controls Components
 * 
 * MIGRATION: This file replaces DNN WebForms input controls:
 * - TextBox (txtPassword, txtQuestion, txtDescription, txtServiceFee, etc.)
 * - DropDownList (cboRoleGroups, cboBillingFrequency, cboTrialFrequency)
 * - CheckBox (chkRandom, chkIsPublic, chkAutoAssignment)
 * 
 * Source files analyzed:
 * - Website/admin/Users/User.ascx.vb
 * - Website/admin/Security/EditRoles.ascx.vb
 * - Website/admin/Portal/SiteSettings.ascx.vb
 * 
 * Features:
 * - All components use standalone: true (Angular 19 default)
 * - Uses inject() function instead of constructor injection
 * - Uses signal() and computed() for reactive state management
 * - Implements ControlValueAccessor for Angular Forms integration
 * - Uses OnPush change detection strategy
 * - ARIA attributes for accessibility
 * - @if, @for control flow syntax in templates
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  input,
  forwardRef,
  Input,
  Output,
  EventEmitter
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ControlValueAccessor,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
  FormsModule
} from '@angular/forms';

/**
 * Interface representing an option for select dropdowns
 * MIGRATION: Replaces ListItem from DNN DropDownList binding
 */
export interface SelectOption {
  /** The value to be submitted when this option is selected */
  value: string | number;
  /** The display label shown to the user */
  label: string;
  /** Whether this option should be disabled */
  disabled?: boolean;
}

// ============================================================================
// TextInputComponent
// ============================================================================

/**
 * Standalone text input component replacing DNN TextBox control
 * 
 * MIGRATION: Replaces ASP.NET TextBox controls such as:
 * - txtPassword, txtConfirm, txtQuestion, txtAnswer (User.ascx.vb)
 * - txtDescription, txtServiceFee, txtBillingPeriod, txtRSVPCode (EditRoles.ascx.vb)
 * - txtStyleSheet (SiteSettings.ascx.vb)
 * 
 * Features:
 * - Implements ControlValueAccessor for reactive forms integration
 * - Uses signals for error state management
 * - ARIA attributes for accessibility
 * - OnPush change detection for performance
 * 
 * @example
 * ```html
 * <app-text-input
 *   label="Username"
 *   placeholder="Enter username"
 *   [required]="true"
 *   [formControl]="usernameControl"
 *   [errorMessage]="getErrorMessage(usernameControl)">
 * </app-text-input>
 * ```
 */
@Component({
  selector: 'app-text-input',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextInputComponent),
      multi: true
    }
  ],
  template: `
    <div class="form-field">
      @if (label()) {
        <label 
          [attr.for]="inputId()"
          class="form-label"
          [class.required]="required()">
          {{ label() }}
          @if (required()) {
            <span class="required-indicator" aria-hidden="true">*</span>
          }
        </label>
      }
      <input
        [id]="inputId()"
        [type]="type()"
        [placeholder]="placeholder()"
        [value]="value()"
        [disabled]="disabled()"
        [attr.aria-invalid]="hasError()"
        [attr.aria-describedby]="hasError() ? errorId() : null"
        [attr.aria-required]="required()"
        class="form-input"
        [class.error]="hasError()"
        (input)="onInput($event)"
        (blur)="onTouched()" />
      @if (hasError() && errorMessage()) {
        <div 
          [id]="errorId()"
          class="form-error"
          role="alert"
          aria-live="polite">
          {{ errorMessage() }}
        </div>
      }
    </div>
  `,
  styles: [`
    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 1rem;
    }
    .form-label {
      font-weight: 500;
      color: #374151;
      font-size: 0.875rem;
    }
    .form-label.required {
      font-weight: 600;
    }
    .required-indicator {
      color: #ef4444;
      margin-left: 0.25rem;
    }
    .form-input {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
      line-height: 1.5;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }
    .form-input:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }
    .form-input:disabled {
      background-color: #f3f4f6;
      cursor: not-allowed;
    }
    .form-input.error {
      border-color: #ef4444;
    }
    .form-input.error:focus {
      box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
    }
    .form-error {
      color: #ef4444;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
  `]
})
export class TextInputComponent implements ControlValueAccessor {
  /** Label text displayed above the input */
  label = input<string>('');
  
  /** Placeholder text shown when input is empty */
  placeholder = input<string>('');
  
  /** Input type (text, password, email, number, etc.) */
  type = input<string>('text');
  
  /** Whether the field is required */
  required = input<boolean>(false);
  
  /** Error message to display when validation fails */
  errorMessage = input<string>('');
  
  /** Internal signal for the input value */
  value = signal<string>('');
  
  /** Internal signal for disabled state */
  disabled = signal<boolean>(false);
  
  /** Counter for generating unique IDs */
  private static instanceCounter = 0;
  private instanceId = ++TextInputComponent.instanceCounter;
  
  /** Computed signal for unique input ID */
  inputId = computed(() => `text-input-${this.instanceId}`);
  
  /** Computed signal for error message ID */
  errorId = computed(() => `text-input-error-${this.instanceId}`);
  
  /** Computed signal to determine if there's an error to display */
  hasError = computed(() => !!this.errorMessage());
  
  /** Callback function for value changes - registered by forms API */
  private onChange: (value: string) => void = () => {};
  
  /** Callback function for touch events - registered by forms API */
  onTouched: () => void = () => {};
  
  /**
   * Handles input events and propagates value changes
   * @param event - The input event
   */
  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    const newValue = target.value;
    this.value.set(newValue);
    this.onChange(newValue);
  }
  
  /**
   * Writes a new value to the component from the forms API
   * @param value - The value to write
   */
  writeValue(value: string | null): void {
    this.value.set(value ?? '');
  }
  
  /**
   * Registers the onChange callback function
   * @param fn - The callback function
   */
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  
  /**
   * Registers the onTouched callback function
   * @param fn - The callback function
   */
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  
  /**
   * Sets the disabled state of the component
   * @param isDisabled - Whether the component should be disabled
   */
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}

// ============================================================================
// SelectComponent
// ============================================================================

/**
 * Standalone select dropdown component replacing DNN DropDownList control
 * 
 * MIGRATION: Replaces ASP.NET DropDownList controls such as:
 * - cboRoleGroups, cboBillingFrequency, cboTrialFrequency (EditRoles.ascx.vb)
 * - Various dropdown lists in SiteSettings.ascx.vb
 * 
 * Features:
 * - Implements ControlValueAccessor for reactive forms integration
 * - Uses signals for options and error state management
 * - ARIA attributes for accessibility
 * - OnPush change detection for performance
 * - @for control flow for option rendering
 * 
 * @example
 * ```html
 * <app-select
 *   label="Role Group"
 *   [options]="roleGroupOptions"
 *   [required]="true"
 *   [formControl]="roleGroupControl">
 * </app-select>
 * ```
 */
@Component({
  selector: 'app-select',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SelectComponent),
      multi: true
    }
  ],
  template: `
    <div class="form-field">
      @if (label()) {
        <label 
          [attr.for]="selectId()"
          class="form-label"
          [class.required]="required()">
          {{ label() }}
          @if (required()) {
            <span class="required-indicator" aria-hidden="true">*</span>
          }
        </label>
      }
      <select
        [id]="selectId()"
        [disabled]="disabled()"
        [attr.aria-invalid]="hasError()"
        [attr.aria-describedby]="hasError() ? errorId() : null"
        [attr.aria-required]="required()"
        class="form-select"
        [class.error]="hasError()"
        (change)="onSelectChange($event)"
        (blur)="onTouched()">
        @if (placeholder()) {
          <option value="" disabled [selected]="!value()">
            {{ placeholder() }}
          </option>
        }
        @for (option of options(); track option.value) {
          <option 
            [value]="option.value"
            [disabled]="option.disabled ?? false"
            [selected]="option.value === value()">
            {{ option.label }}
          </option>
        }
      </select>
      @if (hasError() && errorMessage()) {
        <div 
          [id]="errorId()"
          class="form-error"
          role="alert"
          aria-live="polite">
          {{ errorMessage() }}
        </div>
      }
    </div>
  `,
  styles: [`
    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 1rem;
    }
    .form-label {
      font-weight: 500;
      color: #374151;
      font-size: 0.875rem;
    }
    .form-label.required {
      font-weight: 600;
    }
    .required-indicator {
      color: #ef4444;
      margin-left: 0.25rem;
    }
    .form-select {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
      line-height: 1.5;
      background-color: white;
      cursor: pointer;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }
    .form-select:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }
    .form-select:disabled {
      background-color: #f3f4f6;
      cursor: not-allowed;
    }
    .form-select.error {
      border-color: #ef4444;
    }
    .form-select.error:focus {
      box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
    }
    .form-error {
      color: #ef4444;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
  `]
})
export class SelectComponent implements ControlValueAccessor {
  /** Label text displayed above the select */
  label = input<string>('');
  
  /** Placeholder text for the default empty option */
  placeholder = input<string>('');
  
  /** Array of options to display in the dropdown */
  options = input<SelectOption[]>([]);
  
  /** Whether the field is required */
  required = input<boolean>(false);
  
  /** Error message to display when validation fails */
  errorMessage = input<string>('');
  
  /** Internal signal for the selected value */
  value = signal<string | number | null>(null);
  
  /** Internal signal for disabled state */
  disabled = signal<boolean>(false);
  
  /** Counter for generating unique IDs */
  private static instanceCounter = 0;
  private instanceId = ++SelectComponent.instanceCounter;
  
  /** Computed signal for unique select ID */
  selectId = computed(() => `select-${this.instanceId}`);
  
  /** Computed signal for error message ID */
  errorId = computed(() => `select-error-${this.instanceId}`);
  
  /** Computed signal to determine if there's an error to display */
  hasError = computed(() => !!this.errorMessage());
  
  /** Callback function for value changes - registered by forms API */
  private onChange: (value: string | number | null) => void = () => {};
  
  /** Callback function for touch events - registered by forms API */
  onTouched: () => void = () => {};
  
  /**
   * Handles select change events and propagates value changes
   * @param event - The change event
   */
  onSelectChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    const rawValue = target.value;
    
    // Try to parse as number if applicable
    const numericValue = Number(rawValue);
    const newValue = !isNaN(numericValue) && rawValue !== '' ? numericValue : rawValue;
    
    this.value.set(newValue);
    this.onChange(newValue);
  }
  
  /**
   * Writes a new value to the component from the forms API
   * @param value - The value to write
   */
  writeValue(value: string | number | null): void {
    this.value.set(value);
  }
  
  /**
   * Registers the onChange callback function
   * @param fn - The callback function
   */
  registerOnChange(fn: (value: string | number | null) => void): void {
    this.onChange = fn;
  }
  
  /**
   * Registers the onTouched callback function
   * @param fn - The callback function
   */
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  
  /**
   * Sets the disabled state of the component
   * @param isDisabled - Whether the component should be disabled
   */
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}

// ============================================================================
// CheckboxComponent
// ============================================================================

/**
 * Standalone checkbox component replacing DNN CheckBox control
 * 
 * MIGRATION: Replaces ASP.NET CheckBox controls such as:
 * - chkRandom (User.ascx.vb) - Random password generation toggle
 * - chkIsPublic, chkAutoAssignment (EditRoles.ascx.vb) - Role settings
 * 
 * Features:
 * - Implements ControlValueAccessor for reactive forms integration
 * - Uses signals for checked state management
 * - ARIA attributes for accessibility
 * - OnPush change detection for performance
 * 
 * @example
 * ```html
 * <app-checkbox
 *   label="Generate random password"
 *   [formControl]="randomPasswordControl">
 * </app-checkbox>
 * ```
 */
@Component({
  selector: 'app-checkbox',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CheckboxComponent),
      multi: true
    }
  ],
  template: `
    <div class="form-checkbox">
      <input
        [id]="checkboxId()"
        type="checkbox"
        [checked]="checked()"
        [disabled]="disabled()"
        [attr.aria-checked]="checked()"
        class="checkbox-input"
        (change)="onCheckboxChange($event)"
        (blur)="onTouched()" />
      @if (label()) {
        <label 
          [attr.for]="checkboxId()"
          class="checkbox-label">
          {{ label() }}
        </label>
      }
    </div>
  `,
  styles: [`
    .form-checkbox {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
    .checkbox-input {
      width: 1rem;
      height: 1rem;
      border: 1px solid #d1d5db;
      border-radius: 0.25rem;
      cursor: pointer;
      accent-color: #3b82f6;
    }
    .checkbox-input:focus {
      outline: none;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }
    .checkbox-input:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }
    .checkbox-label {
      font-weight: 400;
      color: #374151;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .checkbox-input:disabled + .checkbox-label {
      cursor: not-allowed;
      opacity: 0.5;
    }
  `]
})
export class CheckboxComponent implements ControlValueAccessor {
  /** Label text displayed next to the checkbox */
  label = input<string>('');
  
  /** Internal signal for the checked state */
  checked = signal<boolean>(false);
  
  /** Internal signal for the value (same as checked for checkboxes) */
  value = signal<boolean>(false);
  
  /** Internal signal for disabled state */
  disabled = signal<boolean>(false);
  
  /** Counter for generating unique IDs */
  private static instanceCounter = 0;
  private instanceId = ++CheckboxComponent.instanceCounter;
  
  /** Computed signal for unique checkbox ID */
  checkboxId = computed(() => `checkbox-${this.instanceId}`);
  
  /** Callback function for value changes - registered by forms API */
  private onChange: (value: boolean) => void = () => {};
  
  /** Callback function for touch events - registered by forms API */
  onTouched: () => void = () => {};
  
  /**
   * Handles checkbox change events and propagates value changes
   * @param event - The change event
   */
  onCheckboxChange(event: Event): void {
    const target = event.target as HTMLInputElement;
    const newValue = target.checked;
    this.checked.set(newValue);
    this.value.set(newValue);
    this.onChange(newValue);
  }
  
  /**
   * Writes a new value to the component from the forms API
   * @param value - The value to write
   */
  writeValue(value: boolean | null): void {
    const boolValue = !!value;
    this.checked.set(boolValue);
    this.value.set(boolValue);
  }
  
  /**
   * Registers the onChange callback function
   * @param fn - The callback function
   */
  registerOnChange(fn: (value: boolean) => void): void {
    this.onChange = fn;
  }
  
  /**
   * Registers the onTouched callback function
   * @param fn - The callback function
   */
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  
  /**
   * Sets the disabled state of the component
   * @param isDisabled - Whether the component should be disabled
   */
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}

// ============================================================================
// TextareaComponent
// ============================================================================

/**
 * Standalone textarea component for multi-line text input
 * 
 * MIGRATION: Replaces ASP.NET TextBox controls with TextMode="MultiLine":
 * - txtStyleSheet (SiteSettings.ascx.vb) - CSS stylesheet editor
 * - txtDescription with multiple lines (EditRoles.ascx.vb)
 * 
 * Features:
 * - Implements ControlValueAccessor for reactive forms integration
 * - Uses signals for error state management
 * - ARIA attributes for accessibility
 * - OnPush change detection for performance
 * - Configurable row count
 * 
 * @example
 * ```html
 * <app-textarea
 *   label="Description"
 *   placeholder="Enter description"
 *   [rows]="5"
 *   [formControl]="descriptionControl"
 *   [errorMessage]="getErrorMessage(descriptionControl)">
 * </app-textarea>
 * ```
 */
@Component({
  selector: 'app-textarea',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextareaComponent),
      multi: true
    }
  ],
  template: `
    <div class="form-field">
      @if (label()) {
        <label 
          [attr.for]="textareaId()"
          class="form-label"
          [class.required]="required()">
          {{ label() }}
          @if (required()) {
            <span class="required-indicator" aria-hidden="true">*</span>
          }
        </label>
      }
      <textarea
        [id]="textareaId()"
        [placeholder]="placeholder()"
        [rows]="rows()"
        [disabled]="disabled()"
        [attr.aria-invalid]="hasError()"
        [attr.aria-describedby]="hasError() ? errorId() : null"
        [attr.aria-required]="required()"
        class="form-textarea"
        [class.error]="hasError()"
        [value]="value()"
        (input)="onInput($event)"
        (blur)="onTouched()"></textarea>
      @if (hasError() && errorMessage()) {
        <div 
          [id]="errorId()"
          class="form-error"
          role="alert"
          aria-live="polite">
          {{ errorMessage() }}
        </div>
      }
    </div>
  `,
  styles: [`
    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-bottom: 1rem;
    }
    .form-label {
      font-weight: 500;
      color: #374151;
      font-size: 0.875rem;
    }
    .form-label.required {
      font-weight: 600;
    }
    .required-indicator {
      color: #ef4444;
      margin-left: 0.25rem;
    }
    .form-textarea {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
      font-family: inherit;
      line-height: 1.5;
      resize: vertical;
      min-height: 6rem;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }
    .form-textarea:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }
    .form-textarea:disabled {
      background-color: #f3f4f6;
      cursor: not-allowed;
    }
    .form-textarea.error {
      border-color: #ef4444;
    }
    .form-textarea.error:focus {
      box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
    }
    .form-error {
      color: #ef4444;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
  `]
})
export class TextareaComponent implements ControlValueAccessor {
  /** Label text displayed above the textarea */
  label = input<string>('');
  
  /** Placeholder text shown when textarea is empty */
  placeholder = input<string>('');
  
  /** Number of visible text rows */
  rows = input<number>(4);
  
  /** Whether the field is required */
  required = input<boolean>(false);
  
  /** Error message to display when validation fails */
  errorMessage = input<string>('');
  
  /** Internal signal for the textarea value */
  value = signal<string>('');
  
  /** Internal signal for disabled state */
  disabled = signal<boolean>(false);
  
  /** Counter for generating unique IDs */
  private static instanceCounter = 0;
  private instanceId = ++TextareaComponent.instanceCounter;
  
  /** Computed signal for unique textarea ID */
  textareaId = computed(() => `textarea-${this.instanceId}`);
  
  /** Computed signal for error message ID */
  errorId = computed(() => `textarea-error-${this.instanceId}`);
  
  /** Computed signal to determine if there's an error to display */
  hasError = computed(() => !!this.errorMessage());
  
  /** Callback function for value changes - registered by forms API */
  private onChange: (value: string) => void = () => {};
  
  /** Callback function for touch events - registered by forms API */
  onTouched: () => void = () => {};
  
  /**
   * Handles input events and propagates value changes
   * @param event - The input event
   */
  onInput(event: Event): void {
    const target = event.target as HTMLTextAreaElement;
    const newValue = target.value;
    this.value.set(newValue);
    this.onChange(newValue);
  }
  
  /**
   * Writes a new value to the component from the forms API
   * @param value - The value to write
   */
  writeValue(value: string | null): void {
    this.value.set(value ?? '');
  }
  
  /**
   * Registers the onChange callback function
   * @param fn - The callback function
   */
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  
  /**
   * Registers the onTouched callback function
   * @param fn - The callback function
   */
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  
  /**
   * Sets the disabled state of the component
   * @param isDisabled - Whether the component should be disabled
   */
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
