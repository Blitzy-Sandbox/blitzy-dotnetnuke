import { DOCUMENT } from '@angular/common';
import {
  Directive,
  ElementRef,
  HostListener,
  inject,
  Input,
  OnDestroy,
  Renderer2,
} from '@angular/core';

/**
 * Module-scoped, monotonically increasing counter used to mint a unique DOM id
 * for every tooltip instance. A deterministic counter (rather than a random or
 * `crypto`-based value) keeps ids stable and collision-free even when many
 * tooltips coexist on the page, which is required for a valid one-to-one
 * `aria-describedby` association between a host element and its tooltip.
 */
let uniqueTooltipId = 0;

/**
 * TooltipDirective — an accessible hover/focus tooltip attribute directive.
 *
 * Usage:
 * ```html
 * <button appTooltip="Delete this portal" aria-label="Delete">
 *   <svg aria-hidden="true">…</svg>
 * </button>
 * ```
 *
 * Behaviour:
 * - Shows the tooltip on `mouseenter` (pointer users) and `focus` (keyboard
 *   users), and hides it on `mouseleave`, `blur`, and the Escape key. This dual
 *   pointer/keyboard wiring is what makes the affordance usable without a mouse.
 * - Renders the tooltip as a `role="tooltip"` element appended to
 *   `document.body`, positioned just below the host using `position: fixed`
 *   (viewport-relative, so no scroll-offset maths is needed).
 * - Wires the host to the tooltip through `aria-describedby` so screen readers
 *   announce the help text; the attribute is added on show and removed on hide.
 *
 * MIGRATION: re-expresses the legacy DotNetNuke Web Forms help/hint affordances
 * — the `<dnn:label controlname="…">` help labels / hover text rendered on the
 * admin screens under `Website/admin/**` (e.g. the portal Signup screen) — as a
 * single, lightweight, framework-idiomatic Angular directive. This is a
 * from-scratch re-expression of the *semantics* only: no VB.NET code is ported.
 *
 * Accessibility (AAP §0.3.4): the tooltip element carries `role="tooltip"` and a
 * unique id, and the host is associated via `aria-describedby`; the tooltip
 * opens on focus and closes on blur/Escape so keyboard and screen-reader users
 * get parity with pointer users.
 *
 * Security / XSS (AAP §0.7.1): the tooltip content is inserted as a DOM **text
 * node** via `Renderer2.createText` and *never* as `innerHTML`. All DOM mutation
 * flows through `Renderer2`, keeping the directive renderer-abstract and immune
 * to markup injection through the bound text.
 *
 * NOTE: this directive is standalone by default (Angular v19) — do NOT add
 * `standalone: true` and do NOT declare it in an NgModule. Consumers import
 * `TooltipDirective` directly into their component `imports: [...]` array.
 */
@Directive({ selector: '[appTooltip]' })
export class TooltipDirective implements OnDestroy {
  /**
   * Host element reference. Injected via `inject()` (Angular v19 idiom) and
   * narrowed to `ElementRef<HTMLElement>` so `nativeElement.getBoundingClientRect()`
   * and attribute mutation are strongly typed.
   */
  private readonly host = inject(ElementRef) as ElementRef<HTMLElement>;

  /**
   * Renderer used for ALL DOM manipulation. Using `Renderer2` (never the raw
   * `document`/element APIs to mutate the DOM) keeps the directive
   * platform-agnostic and XSS-safe.
   */
  private readonly renderer = inject(Renderer2);

  /**
   * The document token, injected rather than referencing the global `document`,
   * so the tooltip can be appended to `document.body` in a testable, idiomatic
   * way.
   */
  private readonly document = inject(DOCUMENT);

  /**
   * The tooltip content. Aliased to the `appTooltip` selector attribute so
   * `<button appTooltip="Save changes">` binds the text directly. Kept public
   * (Angular's `strictInputAccessModifiers` forbids `private`/`protected`
   * `@Input()` fields).
   */
  @Input('appTooltip') text = '';

  /**
   * Stable, unique id for this directive instance's tooltip element, used as the
   * value of both the tooltip's `id` and the host's `aria-describedby`.
   */
  private readonly tooltipId = `app-tooltip-${(uniqueTooltipId += 1)}`;

  /**
   * The live tooltip element while shown, or `null` when hidden. Initialised to
   * `null` to satisfy `strictPropertyInitialization` and to act as the show/hide
   * guard.
   */
  private tooltipElement: HTMLElement | null = null;

  /**
   * Create and display the tooltip. No-ops when a tooltip is already visible
   * (prevents duplicates on rapid `mouseenter`/`focus`) or when the bound text
   * is empty/whitespace (avoids rendering an empty tooltip).
   */
  @HostListener('mouseenter')
  @HostListener('focus')
  show(): void {
    if (this.tooltipElement || this.text.trim().length === 0) {
      return;
    }

    // Build the tooltip element entirely through Renderer2.
    const tooltip: HTMLElement = this.renderer.createElement('span');
    this.renderer.setAttribute(tooltip, 'role', 'tooltip');
    this.renderer.setAttribute(tooltip, 'id', this.tooltipId);
    this.renderer.addClass(tooltip, 'app-tooltip');
    // XSS-safe: content is a DOM text node — never innerHTML.
    this.renderer.appendChild(tooltip, this.renderer.createText(this.text));

    // Position just below the host. `position: fixed` is viewport-relative, so
    // the raw getBoundingClientRect() coordinates need no scroll adjustment.
    const rect = this.host.nativeElement.getBoundingClientRect();
    this.renderer.setStyle(tooltip, 'position', 'fixed');
    this.renderer.setStyle(tooltip, 'top', `${rect.bottom + 8}px`);
    this.renderer.setStyle(tooltip, 'left', `${rect.left}px`);

    this.renderer.appendChild(this.document.body, tooltip);
    // Associate the host with the tooltip for assistive technologies.
    this.renderer.setAttribute(this.host.nativeElement, 'aria-describedby', this.tooltipId);
    this.tooltipElement = tooltip;
  }

  /**
   * Hide and dispose of the tooltip. No-ops when nothing is shown. Removes the
   * tooltip node, drops the host's `aria-describedby` association, and clears the
   * reference so no node is left orphaned in `document.body`.
   */
  @HostListener('mouseleave')
  @HostListener('blur')
  @HostListener('keydown.escape')
  hide(): void {
    if (!this.tooltipElement) {
      return;
    }
    this.renderer.removeChild(this.document.body, this.tooltipElement);
    this.renderer.removeAttribute(this.host.nativeElement, 'aria-describedby');
    this.tooltipElement = null;
  }

  /**
   * Guarantee cleanup when the host is destroyed so a tooltip is never left
   * orphaned in `document.body` after navigation or view teardown.
   */
  ngOnDestroy(): void {
    this.hide();
  }
}
