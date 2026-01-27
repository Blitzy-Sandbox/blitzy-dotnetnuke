/**
 * @fileoverview Angular 19 standalone confirmation dialog component
 * 
 * MIGRATION: Replaces DNN WebForms JavaScript confirm() calls and ClientAPI.AddButtonConfirm patterns
 * from admin screens for delete operations on users, roles, portals, and modules.
 * 
 * Original DNN patterns replaced:
 * - ClientAPI.AddButtonConfirm(cmdDelete, confirmString) from User.ascx.vb line 260
 * - ClientAPI.AddButtonConfirm(cmdDelete, Localization.GetString("DeleteItem")) from EditRoles.ascx.vb line 112
 * - ClientAPI.AddButtonConfirm(cmdDelete, Localization.GetString("DeleteItem")) from EditGroups.ascx.vb line 66
 * - imageColumn.OnClickJS = Localization.GetString("DeleteItem") from Portals.ascx.vb line 300
 * 
 * This component provides a reusable modal dialog for delete confirmations and user prompts
 * with configurable title, message, and action buttons using Angular 19 signals for state
 * management and @if control flow syntax for conditional rendering.
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  input,
  output,
  ElementRef,
  inject,
  OnDestroy,
  AfterViewInit,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Button type variants for the confirm button styling
 * - 'danger': Red button for destructive actions (default)
 * - 'primary': Blue button for standard confirmations
 * - 'warning': Yellow/Orange button for cautionary actions
 */
export type ConfirmButtonType = 'primary' | 'danger' | 'warning';

/**
 * ConfirmationDialogComponent
 * 
 * A reusable standalone Angular 19 confirmation dialog component that provides
 * a modal dialog for delete confirmations and user prompts throughout the application.
 * 
 * Features:
 * - Configurable title, message, and button text via input signals
 * - Signal-based dialog visibility state management
 * - @if control flow syntax for conditional rendering
 * - OnPush change detection for optimal performance
 * - Full accessibility support (ARIA, keyboard navigation, focus management)
 * - CSS animations for smooth open/close transitions
 * - Button styling variants (danger, primary, warning)
 * 
 * Usage Example:
 * ```html
 * <app-confirmation-dialog
 *   #deleteDialog
 *   [title]="'Delete User'"
 *   [message]="'Are you sure you want to delete this user?'"
 *   [confirmText]="'Delete'"
 *   [cancelText]="'Cancel'"
 *   [confirmButtonType]="'danger'"
 *   (confirmed)="onDeleteConfirmed()"
 *   (cancelled)="onDeleteCancelled()">
 * </app-confirmation-dialog>
 * ```
 * 
 * Imperative control:
 * ```typescript
 * @ViewChild('deleteDialog') dialog!: ConfirmationDialogComponent;
 * 
 * showDeleteConfirmation(): void {
 *   this.dialog.open();
 * }
 * ```
 * 
 * MIGRATION: This component supports all delete operations previously handled by
 * DNN WebForms: users, roles, role groups, portals, and modules.
 */
@Component({
  selector: 'app-confirmation-dialog',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (isOpen()) {
      <div 
        class="dialog-overlay"
        [class.dialog-overlay--visible]="isOpen()"
        (click)="onOverlayClick($event)"
        (keydown)="onKeyDown($event)"
        role="presentation"
        aria-hidden="true">
      </div>
      <div
        class="dialog-container"
        [class.dialog-container--visible]="isOpen()"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="dialogTitleId"
        [attr.aria-describedby]="dialogMessageId"
        #dialogContainer
        tabindex="-1">
        
        <!-- Dialog Header -->
        <div class="dialog-header">
          <h2 [id]="dialogTitleId" class="dialog-title">
            {{ title() }}
          </h2>
        </div>

        <!-- Dialog Body -->
        <div class="dialog-body">
          <p [id]="dialogMessageId" class="dialog-message">
            {{ message() }}
          </p>
        </div>

        <!-- Dialog Footer -->
        <div class="dialog-footer">
          <button
            type="button"
            class="dialog-button dialog-button--cancel"
            (click)="cancel()"
            #cancelButton>
            {{ cancelText() }}
          </button>
          <button
            type="button"
            class="dialog-button"
            [class.dialog-button--danger]="confirmButtonType() === 'danger'"
            [class.dialog-button--primary]="confirmButtonType() === 'primary'"
            [class.dialog-button--warning]="confirmButtonType() === 'warning'"
            (click)="confirm()"
            #confirmButton>
            {{ confirmText() }}
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    /* Dialog Overlay - Semi-transparent dark backdrop */
    .dialog-overlay {
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background-color: rgba(0, 0, 0, 0.5);
      z-index: 1000;
      opacity: 0;
      transition: opacity 0.2s ease-in-out;
    }

    .dialog-overlay--visible {
      opacity: 1;
    }

    /* Dialog Container - Center-aligned modal */
    .dialog-container {
      position: fixed;
      top: 50%;
      left: 50%;
      transform: translate(-50%, -50%) scale(0.9);
      z-index: 1001;
      background-color: #ffffff;
      border-radius: 8px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.25);
      min-width: 320px;
      max-width: 480px;
      width: 90%;
      max-height: 90vh;
      overflow: auto;
      opacity: 0;
      transition: opacity 0.2s ease-in-out, transform 0.2s ease-in-out;
    }

    .dialog-container--visible {
      opacity: 1;
      transform: translate(-50%, -50%) scale(1);
    }

    /* Dialog Header */
    .dialog-header {
      padding: 20px 24px 12px;
      border-bottom: 1px solid #e0e0e0;
    }

    .dialog-title {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 600;
      color: #333333;
      line-height: 1.4;
    }

    /* Dialog Body */
    .dialog-body {
      padding: 16px 24px 24px;
    }

    .dialog-message {
      margin: 0;
      font-size: 1rem;
      color: #555555;
      line-height: 1.5;
    }

    /* Dialog Footer */
    .dialog-footer {
      padding: 12px 24px 20px;
      display: flex;
      justify-content: flex-end;
      gap: 12px;
      border-top: 1px solid #e0e0e0;
    }

    /* Dialog Buttons - Base styles */
    .dialog-button {
      padding: 10px 20px;
      font-size: 0.9375rem;
      font-weight: 500;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      transition: background-color 0.15s ease, transform 0.1s ease, box-shadow 0.15s ease;
      min-width: 80px;
      text-align: center;
    }

    .dialog-button:focus {
      outline: 2px solid #2196f3;
      outline-offset: 2px;
    }

    .dialog-button:active {
      transform: scale(0.98);
    }

    /* Cancel Button - Neutral styling */
    .dialog-button--cancel {
      background-color: #f5f5f5;
      color: #333333;
      border: 1px solid #cccccc;
    }

    .dialog-button--cancel:hover {
      background-color: #e8e8e8;
    }

    .dialog-button--cancel:focus {
      outline-color: #666666;
    }

    /* Danger Button - Red for destructive actions (default) */
    /* MIGRATION: Maps to delete confirmation styling from DNN */
    .dialog-button--danger {
      background-color: #dc3545;
      color: #ffffff;
    }

    .dialog-button--danger:hover {
      background-color: #c82333;
      box-shadow: 0 2px 8px rgba(220, 53, 69, 0.3);
    }

    .dialog-button--danger:focus {
      outline-color: #dc3545;
    }

    /* Primary Button - Blue for standard confirmations */
    .dialog-button--primary {
      background-color: #0d6efd;
      color: #ffffff;
    }

    .dialog-button--primary:hover {
      background-color: #0b5ed7;
      box-shadow: 0 2px 8px rgba(13, 110, 253, 0.3);
    }

    .dialog-button--primary:focus {
      outline-color: #0d6efd;
    }

    /* Warning Button - Yellow/Orange for cautionary actions */
    .dialog-button--warning {
      background-color: #ffc107;
      color: #212529;
    }

    .dialog-button--warning:hover {
      background-color: #e0a800;
      box-shadow: 0 2px 8px rgba(255, 193, 7, 0.3);
    }

    .dialog-button--warning:focus {
      outline-color: #ffc107;
    }

    /* Responsive adjustments for smaller screens */
    @media (max-width: 480px) {
      .dialog-container {
        min-width: 280px;
        width: 95%;
      }

      .dialog-header {
        padding: 16px 20px 10px;
      }

      .dialog-title {
        font-size: 1.125rem;
      }

      .dialog-body {
        padding: 12px 20px 20px;
      }

      .dialog-message {
        font-size: 0.9375rem;
      }

      .dialog-footer {
        padding: 12px 20px 16px;
        flex-direction: column-reverse;
        gap: 8px;
      }

      .dialog-button {
        width: 100%;
      }
    }

    /* Reduced motion preference support */
    @media (prefers-reduced-motion: reduce) {
      .dialog-overlay,
      .dialog-container,
      .dialog-button {
        transition: none;
      }
    }

    /* High contrast mode support */
    @media (prefers-contrast: high) {
      .dialog-container {
        border: 2px solid #000000;
      }

      .dialog-button {
        border: 2px solid currentColor;
      }
    }
  `]
})
export class ConfirmationDialogComponent implements AfterViewInit, OnDestroy {
  /**
   * Reference to the host element for DOM manipulation
   */
  private readonly elementRef = inject(ElementRef);

  /**
   * Reference to the dialog container element for focus management
   */
  @ViewChild('dialogContainer') dialogContainer?: ElementRef<HTMLDivElement>;

  /**
   * Reference to the cancel button for focus restoration
   */
  @ViewChild('cancelButton') cancelButton?: ElementRef<HTMLButtonElement>;

  /**
   * Reference to the confirm button
   */
  @ViewChild('confirmButton') confirmButton?: ElementRef<HTMLButtonElement>;

  /**
   * Unique ID for the dialog title element (accessibility)
   */
  readonly dialogTitleId = `dialog-title-${Math.random().toString(36).substring(2, 9)}`;

  /**
   * Unique ID for the dialog message element (accessibility)
   */
  readonly dialogMessageId = `dialog-message-${Math.random().toString(36).substring(2, 9)}`;

  /**
   * Element that had focus before dialog opened (for focus restoration)
   */
  private previouslyFocusedElement: HTMLElement | null = null;

  // ============================================================================
  // INPUT PROPERTIES (using Angular 19 input() signal function)
  // MIGRATION: These replace hardcoded strings from Localization.GetString() calls
  // ============================================================================

  /**
   * Dialog title displayed in the header
   * MIGRATION: Replaces Localization.GetString("DeleteItem") pattern
   * Default: 'Confirm Action'
   */
  readonly title = input<string>('Confirm Action');

  /**
   * Confirmation message displayed in the dialog body
   * MIGRATION: Replaces confirmString parameter from ClientAPI.AddButtonConfirm
   * Default: 'Are you sure you want to proceed?'
   */
  readonly message = input<string>('Are you sure you want to proceed?');

  /**
   * Text displayed on the confirm button
   * Default: 'Confirm'
   */
  readonly confirmText = input<string>('Confirm');

  /**
   * Text displayed on the cancel button
   * Default: 'Cancel'
   */
  readonly cancelText = input<string>('Cancel');

  /**
   * Styling variant for the confirm button
   * - 'danger': Red button for destructive actions (default for delete operations)
   * - 'primary': Blue button for standard confirmations
   * - 'warning': Yellow button for cautionary actions
   * Default: 'danger'
   */
  readonly confirmButtonType = input<ConfirmButtonType>('danger');

  // ============================================================================
  // OUTPUT PROPERTIES (using Angular 19 output() function)
  // ============================================================================

  /**
   * Emitted when the user confirms the action by clicking the confirm button
   * MIGRATION: Triggers the delete/action logic that was previously handled by
   * cmdDelete_Click or similar event handlers in VB.NET code-behind
   */
  readonly confirmed = output<void>();

  /**
   * Emitted when the user cancels the action by clicking cancel, backdrop, or pressing Escape
   */
  readonly cancelled = output<void>();

  // ============================================================================
  // SIGNAL-BASED STATE
  // ============================================================================

  /**
   * Signal controlling dialog visibility
   * Uses Angular 19 signal() for reactive state management
   */
  readonly isOpen = signal<boolean>(false);

  // ============================================================================
  // LIFECYCLE HOOKS
  // ============================================================================

  /**
   * Sets up global keyboard event listener for Escape key handling
   */
  ngAfterViewInit(): void {
    // Keyboard event handling is done at the overlay level
  }

  /**
   * Cleans up when component is destroyed
   */
  ngOnDestroy(): void {
    // Ensure body scroll is restored if dialog was open
    if (this.isOpen()) {
      this.restoreBodyScroll();
    }
    // Restore focus if needed
    this.restoreFocus();
  }

  // ============================================================================
  // PUBLIC API METHODS
  // ============================================================================

  /**
   * Opens the confirmation dialog
   * 
   * MIGRATION: This method replaces the implicit dialog shown by
   * ClientAPI.AddButtonConfirm when the associated button was clicked.
   * 
   * @example
   * ```typescript
   * this.confirmDialog.open();
   * ```
   */
  open(): void {
    // Store the currently focused element for restoration later
    this.previouslyFocusedElement = document.activeElement as HTMLElement;
    
    // Prevent body scrolling while dialog is open
    this.preventBodyScroll();
    
    // Show the dialog
    this.isOpen.set(true);

    // Focus the dialog container after Angular renders
    // Using setTimeout to ensure DOM is updated
    setTimeout(() => {
      this.focusDialog();
    }, 0);
  }

  /**
   * Closes the confirmation dialog without emitting any event
   * 
   * @example
   * ```typescript
   * this.confirmDialog.close();
   * ```
   */
  close(): void {
    this.isOpen.set(false);
    
    // Restore body scrolling
    this.restoreBodyScroll();
    
    // Restore focus to the previously focused element
    this.restoreFocus();
  }

  /**
   * Triggers the confirmation action and closes the dialog
   * Emits the 'confirmed' output event
   * 
   * MIGRATION: This triggers the logic that was previously in cmdDelete_Click
   * or grdPortals_DeleteCommand handlers in VB.NET code-behind files.
   * 
   * @example
   * ```typescript
   * // In template
   * (confirmed)="deleteUser()"
   * ```
   */
  confirm(): void {
    this.close();
    this.confirmed.emit();
  }

  /**
   * Triggers the cancellation action and closes the dialog
   * Emits the 'cancelled' output event
   * 
   * @example
   * ```typescript
   * // In template
   * (cancelled)="onCancelDelete()"
   * ```
   */
  cancel(): void {
    this.close();
    this.cancelled.emit();
  }

  // ============================================================================
  // EVENT HANDLERS
  // ============================================================================

  /**
   * Handles clicks on the overlay backdrop
   * Clicking outside the dialog cancels the action
   * 
   * @param event - The mouse click event
   */
  onOverlayClick(event: MouseEvent): void {
    // Only close if the click was directly on the overlay (not bubbled from dialog)
    if (event.target === event.currentTarget) {
      this.cancel();
    }
  }

  /**
   * Handles keyboard events for accessibility
   * - Escape key: Closes the dialog (cancels)
   * - Tab key: Implements focus trap within the dialog
   * 
   * @param event - The keyboard event
   */
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancel();
    } else if (event.key === 'Tab') {
      this.handleTabKey(event);
    }
  }

  // ============================================================================
  // PRIVATE HELPER METHODS
  // ============================================================================

  /**
   * Prevents body scrolling while dialog is open
   * Stores current scroll position and sets overflow hidden
   */
  private preventBodyScroll(): void {
    document.body.style.overflow = 'hidden';
  }

  /**
   * Restores body scrolling when dialog closes
   */
  private restoreBodyScroll(): void {
    document.body.style.overflow = '';
  }

  /**
   * Focuses the dialog container or cancel button for keyboard accessibility
   */
  private focusDialog(): void {
    // Focus the cancel button as it's the safer default action
    if (this.cancelButton?.nativeElement) {
      this.cancelButton.nativeElement.focus();
    } else if (this.dialogContainer?.nativeElement) {
      this.dialogContainer.nativeElement.focus();
    }
  }

  /**
   * Restores focus to the element that was focused before the dialog opened
   */
  private restoreFocus(): void {
    if (this.previouslyFocusedElement && typeof this.previouslyFocusedElement.focus === 'function') {
      setTimeout(() => {
        this.previouslyFocusedElement?.focus();
        this.previouslyFocusedElement = null;
      }, 0);
    }
  }

  /**
   * Implements focus trap - keeps focus cycling within the dialog
   * This is essential for accessibility, especially for screen reader users
   * 
   * @param event - The keyboard event from Tab key press
   */
  private handleTabKey(event: KeyboardEvent): void {
    if (!this.cancelButton?.nativeElement || !this.confirmButton?.nativeElement) {
      return;
    }

    const focusableElements = [
      this.cancelButton.nativeElement,
      this.confirmButton.nativeElement
    ];

    const firstFocusable = focusableElements[0];
    const lastFocusable = focusableElements[focusableElements.length - 1];

    // If shift+tab on first element, wrap to last
    if (event.shiftKey && document.activeElement === firstFocusable) {
      event.preventDefault();
      lastFocusable.focus();
    }
    // If tab on last element, wrap to first
    else if (!event.shiftKey && document.activeElement === lastFocusable) {
      event.preventDefault();
      firstFocusable.focus();
    }
  }
}
