/**
 * Loading Spinner Component
 * 
 * Angular 19 standalone loading spinner component providing configurable visual
 * loading indicator for async operations throughout the application.
 * 
 * MIGRATION NOTES:
 * - This replaces implicit WebForms postback waiting states from DNN admin pages
 * - WebForms relied on browser's native loading indicator during postbacks
 * - Angular SPA requires explicit loading feedback for better UX
 * - Component is most foundational shared component with no internal dependencies
 * 
 * @example
 * // Inline spinner (default)
 * <app-loading-spinner />
 * 
 * // Large spinner with message
 * <app-loading-spinner size="large" message="Loading data..." />
 * 
 * // Full-screen overlay spinner
 * <app-loading-spinner [overlay]="true" message="Please wait..." />
 */

import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Type alias defining available spinner size options.
 * - 'small': 16px diameter - suitable for inline/button contexts
 * - 'medium': 32px diameter - default size for general use
 * - 'large': 48px diameter - suitable for page-level loading states
 */
export type LoadingSpinnerSize = 'small' | 'medium' | 'large';

/**
 * LoadingSpinnerComponent
 * 
 * A configurable loading spinner component that provides visual feedback
 * during asynchronous operations. Supports multiple sizes and an overlay
 * mode for blocking user interaction during critical operations.
 * 
 * Uses Angular 19 standalone architecture with signal-based inputs and
 * OnPush change detection for optimal performance.
 */
@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (overlay()) {
      <div class="spinner-overlay" role="alert" aria-busy="true" aria-label="Loading">
        <div class="spinner-overlay-content">
          <div [class]="spinnerClasses()" aria-hidden="true"></div>
          @if (message()) {
            <p class="spinner-message">{{ message() }}</p>
          }
        </div>
      </div>
    } @else {
      <div class="spinner-container" role="alert" aria-busy="true" aria-label="Loading">
        <div [class]="spinnerClasses()" aria-hidden="true"></div>
        @if (message()) {
          <p class="spinner-message">{{ message() }}</p>
        }
      </div>
    }
  `,
  styles: [`
    /* Base spinner container for inline mode */
    .spinner-container {
      display: inline-flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 8px;
    }

    /* Full-screen overlay mode */
    .spinner-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background-color: rgba(0, 0, 0, 0.5);
      backdrop-filter: blur(2px);
      z-index: 9999;
    }

    .spinner-overlay-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 24px;
      background-color: rgba(255, 255, 255, 0.95);
      border-radius: 8px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
    }

    /* Base spinner element */
    .spinner {
      border-radius: 50%;
      border-style: solid;
      border-color: #e0e0e0;
      border-top-color: #3f51b5;
      animation: spinner-rotate 0.8s linear infinite;
    }

    /* Size variants */
    .spinner-small {
      width: 16px;
      height: 16px;
      border-width: 2px;
    }

    .spinner-medium {
      width: 32px;
      height: 32px;
      border-width: 3px;
    }

    .spinner-large {
      width: 48px;
      height: 48px;
      border-width: 4px;
    }

    /* Loading message styling */
    .spinner-message {
      margin: 0;
      font-size: 14px;
      color: #666;
      text-align: center;
      max-width: 200px;
      line-height: 1.4;
    }

    .spinner-overlay .spinner-message {
      color: #333;
      font-weight: 500;
    }

    /* Keyframe animation for spinning effect */
    @keyframes spinner-rotate {
      0% {
        transform: rotate(0deg);
      }
      100% {
        transform: rotate(360deg);
      }
    }

    /* Reduced motion support for accessibility */
    @media (prefers-reduced-motion: reduce) {
      .spinner {
        animation-duration: 1.5s;
      }
    }
  `]
})
export class LoadingSpinnerComponent {
  /**
   * Size of the spinner.
   * Controls the diameter and border width of the spinner element.
   * - 'small': 16px - for inline/compact contexts
   * - 'medium': 32px - default general purpose size
   * - 'large': 48px - for page-level loading states
   * 
   * @default 'medium'
   */
  readonly size = input<LoadingSpinnerSize>('medium');

  /**
   * Whether to display the spinner in full-screen overlay mode.
   * When true, renders a semi-transparent backdrop that blocks user
   * interaction with the underlying content.
   * 
   * @default false
   */
  readonly overlay = input<boolean>(false);

  /**
   * Optional loading message displayed below the spinner.
   * Provides context to users about what operation is in progress.
   * 
   * @default ''
   */
  readonly message = input<string>('');

  /**
   * Computed CSS classes for the spinner element based on size input.
   * Combines the base 'spinner' class with the appropriate size modifier.
   * 
   * @returns Combined CSS class string (e.g., 'spinner spinner-medium')
   */
  readonly spinnerClasses = computed(() => {
    const sizeValue = this.size();
    return `spinner spinner-${sizeValue}`;
  });
}
