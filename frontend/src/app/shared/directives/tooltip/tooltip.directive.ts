import { DOCUMENT } from '@angular/common';
import {
  DestroyRef,
  Directive,
  ElementRef,
  HostListener,
  Renderer2,
  inject,
  input,
} from '@angular/core';

/**
 * TooltipDirective — lightweight, accessible attribute tooltip.
 *
 * Usage: `<button type="button" appTooltip="Delete portal">…</button>`
 *
 * NEW migration infrastructure (no 1:1 legacy source). It modernizes the legacy
 * DotNetNuke admin UX where action icons carried localized Edit/Delete labels
 * (Website/admin/Portal/Portals.ascx.vb ~L296-315) and forms exposed inline help
 * text (lblUserHelp in Website/admin/Users/ManageUsers.ascx.vb ~L318-324),
 * reproducing that "help on hover" affordance as an accessible, keyboard-navigable
 * tooltip. The legacy UI had no ARIA; the aria-describedby/role="tooltip" wiring
 * here is a deliberate accessibility upgrade (AAP §0.3.4 / §0.7.2).
 *
 * Accessibility (WAI-ARIA tooltip pattern): the tooltip element gets role="tooltip"
 * and the host's aria-describedby is set to the tooltip id while visible (removed on
 * hide). Shows on hover AND keyboard focus; hides on mouseleave/blur/Escape.
 */
@Directive({
  selector: '[appTooltip]',
})
export class TooltipDirective {
  /** Tooltip text. Empty/whitespace text shows no tooltip. */
  readonly appTooltip = input<string>('');

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  private static nextId = 0;

  private tooltipElement: HTMLElement | null = null;

  constructor() {
    // MIGRATION: clean up the detached tooltip node + aria wiring if the host is
    // destroyed while the tooltip is visible (no equivalent in the legacy UI).
    this.destroyRef.onDestroy(() => this.hide());
  }

  @HostListener('mouseenter')
  @HostListener('focus')
  show(): void {
    const text = this.appTooltip().trim();
    if (text.length === 0 || this.tooltipElement !== null) {
      return;
    }

    const id = `app-tooltip-${TooltipDirective.nextId++}`;
    const tooltip: HTMLElement = this.renderer.createElement('span');
    this.renderer.setAttribute(tooltip, 'id', id);
    this.renderer.setAttribute(tooltip, 'role', 'tooltip');
    this.renderer.addClass(tooltip, 'app-tooltip');
    this.renderer.appendChild(tooltip, this.renderer.createText(text));

    // F38: all VISUAL styling (surface, colours, padding, radius, font, elevation,
    // and the constant position:fixed / z-index / pointer-events) lives in the
    // global `.app-tooltip` class in styles.scss — added via addClass() above —
    // which consumes the application's CSS custom properties for theme consistency
    // and high-contrast text. Only the DYNAMIC viewport coordinates are set inline.
    this.renderer.appendChild(this.document.body, tooltip);

    const rect = this.host.nativeElement.getBoundingClientRect();
    this.renderer.setStyle(tooltip, 'top', `${rect.bottom + 8}px`);
    this.renderer.setStyle(tooltip, 'left', `${rect.left}px`);

    this.renderer.setAttribute(this.host.nativeElement, 'aria-describedby', id);
    this.tooltipElement = tooltip;
  }

  @HostListener('mouseleave')
  @HostListener('blur')
  @HostListener('keydown.escape')
  hide(): void {
    if (this.tooltipElement !== null) {
      this.renderer.removeChild(this.document.body, this.tooltipElement);
      this.tooltipElement = null;
    }
    this.renderer.removeAttribute(this.host.nativeElement, 'aria-describedby');
  }
}
