/**
 * Angular 19 Standalone Tooltip Directive
 * 
 * Provides hover and focus-triggered contextual help tooltips.
 * Replaces DNN's server-side tooltip controls and Calendar.InvokePopupCal patterns
 * (SecurityRoles.ascx.vb lines 329-335) with client-side tooltip rendering.
 * 
 * MIGRATION NOTE: This directive replaces DNN's server-side popup patterns:
 * - Calendar.InvokePopupCal(txtEffectiveDate) pattern
 * - Help text tooltip controls
 * - Server-rendered tooltip markup
 * 
 * Features:
 * - Configurable placement (top, bottom, left, right)
 * - Configurable show delay for debouncing
 * - Custom CSS class support
 * - Keyboard accessibility (focus/blur triggers)
 * - ARIA attributes for screen reader support
 * - Automatic cleanup on destroy
 * 
 * Usage:
 * <button appTooltip="Click to submit" tooltipPosition="top" tooltipDelay="300">
 *   Submit
 * </button>
 * 
 * <input 
 *   appTooltip="Enter your email address" 
 *   tooltipPosition="right"
 *   tooltipClass="custom-tooltip">
 */

import {
  Directive,
  ElementRef,
  HostListener,
  inject,
  Input,
  OnDestroy,
  Renderer2
} from '@angular/core';
import { DOCUMENT } from '@angular/common';

/**
 * Type definition for tooltip positioning options
 */
type TooltipPosition = 'top' | 'bottom' | 'left' | 'right';

/**
 * Interface for calculated position coordinates
 */
interface TooltipCoordinates {
  top: number;
  left: number;
}

/**
 * Tooltip directive providing accessible, customizable tooltips
 * for Angular 19 standalone components.
 * 
 * @example
 * // Basic usage
 * <button appTooltip="Help text">Button</button>
 * 
 * // With position
 * <button appTooltip="Help text" tooltipPosition="bottom">Button</button>
 * 
 * // With delay and custom class
 * <button 
 *   appTooltip="Help text" 
 *   tooltipDelay="500" 
 *   tooltipClass="my-tooltip">
 *   Button
 * </button>
 */
@Directive({
  selector: '[appTooltip]',
  standalone: true
})
export class TooltipDirective implements OnDestroy {
  /**
   * Injected reference to the host element
   * Used to calculate tooltip positioning relative to the host
   */
  private readonly elementRef = inject(ElementRef);

  /**
   * Injected Renderer2 for safe DOM manipulation
   * Provides platform-agnostic methods for creating and styling elements
   */
  private readonly renderer = inject(Renderer2);

  /**
   * Injected document reference for appending tooltip to document body
   * Using DOCUMENT token for SSR compatibility
   */
  private readonly document = inject(DOCUMENT);

  /**
   * The tooltip content text to display
   * This is the primary input binding for the directive
   */
  @Input() appTooltip: string = '';

  /**
   * Tooltip placement relative to the host element
   * Options: 'top' | 'bottom' | 'left' | 'right'
   * Default: 'top'
   */
  @Input() tooltipPosition: TooltipPosition = 'top';

  /**
   * Delay in milliseconds before showing the tooltip
   * Provides debouncing to prevent accidental tooltip triggering
   * Default: 200ms
   */
  @Input() tooltipDelay: number = 200;

  /**
   * Optional custom CSS class to apply to the tooltip element
   * Allows for custom styling beyond the default tooltip styles
   */
  @Input() tooltipClass: string = '';

  /**
   * Reference to the created tooltip element
   * Null when tooltip is not visible
   */
  private tooltipElement: HTMLElement | null = null;

  /**
   * Timeout reference for delayed tooltip show
   * Used for debouncing and cleanup
   */
  private showTimeout: ReturnType<typeof setTimeout> | null = null;

  /**
   * Unique identifier for ARIA accessibility linking
   * Generated once per directive instance
   */
  private readonly tooltipId: string;

  /**
   * Constructor generates unique tooltip ID for accessibility
   */
  constructor() {
    // Generate unique ID for aria-describedby linking
    this.tooltipId = `tooltip-${Math.random().toString(36).substring(2, 11)}`;
  }

  /**
   * Mouse enter handler - shows tooltip after configured delay
   * Provides debouncing to prevent accidental triggering on quick hover
   */
  @HostListener('mouseenter')
  onMouseEnter(): void {
    this.scheduleShow();
  }

  /**
   * Mouse leave handler - hides tooltip and clears pending timeout
   * Ensures cleanup of both tooltip element and pending show operations
   */
  @HostListener('mouseleave')
  onMouseLeave(): void {
    this.cancelScheduledShow();
    this.hide();
  }

  /**
   * Focus handler - shows tooltip for keyboard navigation accessibility
   * Allows keyboard-only users to access tooltip content
   */
  @HostListener('focus')
  onFocus(): void {
    this.scheduleShow();
  }

  /**
   * Blur handler - hides tooltip when focus leaves element
   * Ensures tooltip doesn't persist when navigating away
   */
  @HostListener('blur')
  onBlur(): void {
    this.cancelScheduledShow();
    this.hide();
  }

  /**
   * Cleanup on directive destruction
   * Removes tooltip element from DOM and clears any pending timeouts
   * Prevents memory leaks from orphaned DOM elements or timers
   */
  ngOnDestroy(): void {
    this.cancelScheduledShow();
    this.hide();
  }

  /**
   * Schedules tooltip display after configured delay
   * Uses setTimeout for debouncing rapid hover events
   */
  private scheduleShow(): void {
    // Don't show if no content
    if (!this.appTooltip || this.appTooltip.trim() === '') {
      return;
    }

    // Clear any existing timeout to prevent duplicate tooltips
    this.cancelScheduledShow();

    // Schedule show after delay
    this.showTimeout = setTimeout(() => {
      this.show();
    }, this.tooltipDelay);
  }

  /**
   * Cancels any pending tooltip show operation
   * Called on mouse leave, blur, and destroy
   */
  private cancelScheduledShow(): void {
    if (this.showTimeout !== null) {
      clearTimeout(this.showTimeout);
      this.showTimeout = null;
    }
  }

  /**
   * Creates and displays the tooltip element
   * Positions relative to host element and adds to document body
   * Includes ARIA attributes for accessibility
   */
  private show(): void {
    // Prevent duplicate tooltips
    if (this.tooltipElement) {
      return;
    }

    // Don't show if no content
    if (!this.appTooltip || this.appTooltip.trim() === '') {
      return;
    }

    // Create tooltip element
    this.tooltipElement = this.document.createElement('div');

    // Set content
    this.renderer.setProperty(this.tooltipElement, 'textContent', this.appTooltip);

    // Set unique ID for accessibility
    this.renderer.setAttribute(this.tooltipElement, 'id', this.tooltipId);

    // Add ARIA role for screen readers
    this.renderer.setAttribute(this.tooltipElement, 'role', 'tooltip');

    // Link host element to tooltip via aria-describedby
    this.renderer.setAttribute(
      this.elementRef.nativeElement,
      'aria-describedby',
      this.tooltipId
    );

    // Add base tooltip CSS class
    this.renderer.addClass(this.tooltipElement, 'tooltip');

    // Add position-specific CSS class
    this.renderer.addClass(this.tooltipElement, `tooltip-${this.tooltipPosition}`);

    // Add custom CSS class if provided
    if (this.tooltipClass && this.tooltipClass.trim() !== '') {
      // Support multiple space-separated classes
      const customClasses = this.tooltipClass.trim().split(/\s+/);
      customClasses.forEach(className => {
        if (className) {
          this.renderer.addClass(this.tooltipElement!, className);
        }
      });
    }

    // Apply base styles for positioning
    this.applyBaseStyles();

    // Append to document body (outside normal DOM flow)
    this.renderer.appendChild(this.document.body, this.tooltipElement);

    // Calculate and apply position after element is in DOM
    // Use requestAnimationFrame to ensure layout is complete
    requestAnimationFrame(() => {
      if (this.tooltipElement) {
        this.positionTooltip();
        // Add visible class for CSS transition
        this.renderer.addClass(this.tooltipElement, 'tooltip-visible');
      }
    });
  }

  /**
   * Removes the tooltip element from DOM
   * Cleans up ARIA attributes from host element
   */
  private hide(): void {
    if (this.tooltipElement) {
      // Remove aria-describedby from host element
      this.renderer.removeAttribute(this.elementRef.nativeElement, 'aria-describedby');

      // Remove tooltip from DOM
      this.renderer.removeChild(this.document.body, this.tooltipElement);

      // Clear reference
      this.tooltipElement = null;
    }
  }

  /**
   * Applies base CSS styles required for positioning
   * These styles ensure proper tooltip behavior
   */
  private applyBaseStyles(): void {
    if (!this.tooltipElement) {
      return;
    }

    // Position fixed to stay relative to viewport
    this.renderer.setStyle(this.tooltipElement, 'position', 'fixed');

    // Z-index to appear above other content
    this.renderer.setStyle(this.tooltipElement, 'z-index', '10000');

    // Prevent text selection and pointer events on tooltip itself
    this.renderer.setStyle(this.tooltipElement, 'pointer-events', 'none');

    // Initial opacity for transition
    this.renderer.setStyle(this.tooltipElement, 'opacity', '0');

    // Smooth opacity transition
    this.renderer.setStyle(this.tooltipElement, 'transition', 'opacity 0.2s ease-in-out');

    // Basic tooltip styling (can be overridden with tooltipClass)
    this.renderer.setStyle(this.tooltipElement, 'background-color', 'rgba(0, 0, 0, 0.85)');
    this.renderer.setStyle(this.tooltipElement, 'color', '#ffffff');
    this.renderer.setStyle(this.tooltipElement, 'padding', '6px 10px');
    this.renderer.setStyle(this.tooltipElement, 'border-radius', '4px');
    this.renderer.setStyle(this.tooltipElement, 'font-size', '13px');
    this.renderer.setStyle(this.tooltipElement, 'line-height', '1.4');
    this.renderer.setStyle(this.tooltipElement, 'max-width', '250px');
    this.renderer.setStyle(this.tooltipElement, 'word-wrap', 'break-word');
    this.renderer.setStyle(this.tooltipElement, 'white-space', 'pre-wrap');
  }

  /**
   * Calculates and applies tooltip position relative to host element
   * Uses getBoundingClientRect for accurate positioning
   */
  private positionTooltip(): void {
    if (!this.tooltipElement) {
      return;
    }

    const coordinates = this.calculatePosition();

    // Apply calculated position
    this.renderer.setStyle(this.tooltipElement, 'top', `${coordinates.top}px`);
    this.renderer.setStyle(this.tooltipElement, 'left', `${coordinates.left}px`);

    // Show tooltip (opacity transition)
    this.renderer.setStyle(this.tooltipElement, 'opacity', '1');
  }

  /**
   * Calculates tooltip coordinates based on position setting and host element bounds
   * Accounts for tooltip dimensions and viewport boundaries
   * 
   * @returns Calculated top and left coordinates
   */
  private calculatePosition(): TooltipCoordinates {
    // Get host element bounding rectangle
    const hostRect = this.elementRef.nativeElement.getBoundingClientRect();

    // Get tooltip dimensions (element must be in DOM)
    const tooltipRect = this.tooltipElement!.getBoundingClientRect();

    // Gap between tooltip and host element
    const offset = 8;

    let top: number;
    let left: number;

    switch (this.tooltipPosition) {
      case 'top':
        // Position above host, centered horizontally
        top = hostRect.top - tooltipRect.height - offset;
        left = hostRect.left + (hostRect.width - tooltipRect.width) / 2;
        break;

      case 'bottom':
        // Position below host, centered horizontally
        top = hostRect.bottom + offset;
        left = hostRect.left + (hostRect.width - tooltipRect.width) / 2;
        break;

      case 'left':
        // Position to the left of host, centered vertically
        top = hostRect.top + (hostRect.height - tooltipRect.height) / 2;
        left = hostRect.left - tooltipRect.width - offset;
        break;

      case 'right':
        // Position to the right of host, centered vertically
        top = hostRect.top + (hostRect.height - tooltipRect.height) / 2;
        left = hostRect.right + offset;
        break;

      default:
        // Default to top position
        top = hostRect.top - tooltipRect.height - offset;
        left = hostRect.left + (hostRect.width - tooltipRect.width) / 2;
    }

    // Constrain to viewport boundaries
    const constrainedCoordinates = this.constrainToViewport(
      { top, left },
      tooltipRect.width,
      tooltipRect.height
    );

    return constrainedCoordinates;
  }

  /**
   * Constrains tooltip position to stay within viewport boundaries
   * Prevents tooltip from being cut off at screen edges
   * 
   * @param coordinates Initial calculated coordinates
   * @param width Tooltip width
   * @param height Tooltip height
   * @returns Constrained coordinates
   */
  private constrainToViewport(
    coordinates: TooltipCoordinates,
    width: number,
    height: number
  ): TooltipCoordinates {
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const padding = 8; // Minimum distance from viewport edge

    let { top, left } = coordinates;

    // Constrain horizontal position
    if (left < padding) {
      left = padding;
    } else if (left + width > viewportWidth - padding) {
      left = viewportWidth - width - padding;
    }

    // Constrain vertical position
    if (top < padding) {
      top = padding;
    } else if (top + height > viewportHeight - padding) {
      top = viewportHeight - height - padding;
    }

    return { top, left };
  }
}
