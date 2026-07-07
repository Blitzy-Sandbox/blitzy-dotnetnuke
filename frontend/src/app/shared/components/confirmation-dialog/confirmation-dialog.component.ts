import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  effect,
  inject,
  input,
  model,
  output,
} from '@angular/core';

// Module-scoped counter -> unique aria ids so multiple dialog instances never collide.
// Mirrors the sibling `tooltip.directive.ts` pattern (`let uniqueTooltipId = 0;`): a
// deterministic, monotonically increasing counter keeps the `aria-labelledby` /
// `aria-describedby` associations stable and collision-free even when several
// confirmation dialogs coexist on the same page.
let uniqueConfirmationDialogId = 0;

/**
 * ConfirmationDialogComponent — accessible, presentation-only confirm/delete modal.
 *
 * Usage (feature owns the delete):
 *   <app-confirmation-dialog
 *     [(open)]="showDeleteDialog"
 *     title="Delete role"
 *     message="Are you sure you want to delete this item?"
 *     confirmLabel="Delete"
 *     [danger]="true"
 *     (confirm)="deleteRole()"
 *     (cancel)="showDeleteDialog.set(false)" />
 *
 * Features SHOULD bind two-way `[(open)]` (so the dialog can auto-close itself on
 * confirm/cancel) OR react to the `(cancel)`/`(confirm)` outputs. A plain one-way
 * `[open]` binding that also expects auto-close would fight the internal `model()`
 * write, so `[(open)]` is the recommended integration.
 *
 * PRESENTATION-ONLY: emits `confirm` / `cancel` only — NO HTTP, NO business logic.
 * The consuming feature performs the actual `DELETE /api/{entity}/{id}` in its
 * `(confirm)` handler (AAP §0.7.1: "no business logic ... Angular services handle
 * API communication only").
 *
 * MIGRATION: replaces the legacy DotNetNuke delete-confirmation workflow
 * `ClientAPI.AddButtonConfirm(cmdDelete, Localization.GetString("DeleteItem"))`
 * (Website/admin/Security/Roles.ascx.vb:L86 -> cmdDelete_Click (L290-299) ->
 * RoleController.DeleteRoleGroup(PortalId, RoleGroupId) at L294; the identical
 * pattern appears at Website/admin/Tabs/Tabs.ascx.vb:L159, and the helper lives at
 * Library/Controls/DotNetNuke.WebUtility/ClientAPI.vb:L331). The legacy code
 * attached a blocking browser `confirm()` to a delete button and executed the
 * delete on postback. Here the button click opens this ARIA modal; on `confirm` the
 * FEATURE issues `DELETE /api/{entity}/{id}` (the modern analog of
 * `RoleController.DeleteRoleGroup` / tab delete). The `confirm()`'s OK/Cancel map to
 * this component's `confirm`/`cancel` outputs; the localized "DeleteItem" text maps
 * to the `message` input. Semantics only — no VB.NET is transliterated.
 *
 * Accessibility (AAP §0.3.4): the surface is a `role="dialog"` `aria-modal="true"`
 * element wired to its title via `aria-labelledby` and its body via
 * `aria-describedby`; focus is trapped while open (Tab/Shift+Tab cycle), initial
 * focus lands on the Cancel button (the safe default for destructive actions), ESC
 * cancels, and focus is restored to the triggering element on close.
 *
 * Standalone by default (Angular v19) — DO NOT add `standalone: true` and DO NOT
 * declare in an NgModule; consumers import `ConfirmationDialogComponent` directly
 * into their component `imports: [...]` array.
 */
@Component({
  selector: 'app-confirmation-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="cdlg-overlay" (click)="onBackdropClick()">
        <div
          class="cdlg-dialog"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="messageId"
          tabindex="-1"
          (click)="$event.stopPropagation()"
        >
          <h2 class="cdlg-dialog__title" [id]="titleId">{{ title() }}</h2>
          <p class="cdlg-dialog__message" [id]="messageId">{{ message() }}</p>
          <div class="cdlg-dialog__actions">
            <button type="button" class="cdlg-btn cdlg-btn--cancel" (click)="onCancel()">
              {{ cancelLabel() }}
            </button>
            <button
              type="button"
              class="cdlg-btn cdlg-btn--confirm"
              [class.cdlg-btn--danger]="danger()"
              (click)="onConfirm()"
            >
              {{ confirmLabel() }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      /* Custom properties reference the GLOBAL design-system tokens defined in
         src/styles.scss (:root), each with a hardcoded fallback so the dialog still
         renders correctly before the global stylesheet loads. MIGRATION: the earlier
         component-local --app-* names were normalized to the shared
         --color-*, --space-*, --radius*, --shadow-* and --z-* token vocabulary
         to eliminate token-name divergence across shared components. */
      .cdlg-overlay {
        position: fixed;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: var(--space-4, 1rem);
        background: var(--color-overlay, rgba(0, 0, 0, 0.5));
        z-index: var(--z-overlay, 1000);
      }

      .cdlg-dialog {
        width: 100%;
        max-width: 26rem;
        padding: var(--space-5, 1.5rem);
        border-radius: var(--radius-lg, 0.5rem);
        background: var(--color-surface, #ffffff);
        color: var(--color-text, #1f2933);
        box-shadow: var(--shadow-lg, 0 10px 25px rgba(0, 0, 0, 0.2));
        outline: none;
      }

      .cdlg-dialog__title {
        margin: 0 0 var(--space-2, 0.5rem);
        font-size: 1.125rem;
        font-weight: 600;
      }

      .cdlg-dialog__message {
        margin: 0 0 var(--space-5, 1.5rem);
        line-height: 1.5;
      }

      .cdlg-dialog__actions {
        display: flex;
        justify-content: flex-end;
        gap: var(--space-2, 0.5rem);
      }

      .cdlg-btn {
        padding: var(--space-2, 0.5rem) var(--space-4, 1rem);
        font: inherit;
        border: 1px solid transparent;
        border-radius: var(--radius, 0.375rem);
        cursor: pointer;
      }

      .cdlg-btn--cancel {
        background: var(--color-secondary, #e4e7eb);
        color: var(--color-text, #1f2933);
      }

      .cdlg-btn--confirm {
        background: var(--color-primary, #2563eb);
        color: var(--color-primary-contrast, #ffffff);
      }

      .cdlg-btn--danger {
        background: var(--color-danger, #dc2626);
        color: var(--color-danger-contrast, #ffffff);
      }
    `,
  ],
})
export class ConfirmationDialogComponent {
  /**
   * Host element reference. Injected via `inject()` (Angular v19 idiom) and narrowed
   * to `ElementRef<HTMLElement>` so the focus-trap DOM queries
   * (`querySelector`/`querySelectorAll`) are strongly typed. This is the ONLY
   * dependency the component injects — it performs no data access.
   */
  private readonly host = inject(ElementRef) as ElementRef<HTMLElement>;

  /**
   * The document token, injected via `inject(DOCUMENT)` rather than referencing the
   * global `document` object directly. Mirrors the sibling `tooltip.directive.ts`
   * pattern and keeps focus-management DOM reads (`activeElement`) decoupled from the
   * ambient global, so the component is testable/SSR-safe and does not depend on a
   * platform global. MIGRATION: replaces direct `document.activeElement` access.
   */
  private readonly document = inject(DOCUMENT);

  /**
   * Open state. Prefer two-way `[(open)]` so the dialog can auto-close itself on
   * confirm/cancel (a presentation concern, faithful to the legacy `confirm()` that
   * dismissed on both OK and Cancel).
   */
  readonly open = model<boolean>(false);

  /** Dialog heading text. Rendered via interpolation only (XSS-safe). */
  readonly title = input<string>('Confirm');
  /** Dialog body text. MIGRATION: maps to the localized "DeleteItem" string. */
  readonly message = input<string>('Are you sure?');
  /** Label for the confirm button (e.g. "Delete" for destructive flows). */
  readonly confirmLabel = input<string>('Confirm');
  /** Label for the cancel button. */
  readonly cancelLabel = input<string>('Cancel');
  /** Destructive styling flag — features pass `[danger]="true"` for delete flows. */
  readonly danger = input<boolean>(false);

  /** Emitted when the user confirms. MIGRATION: the FEATURE performs the DELETE. */
  readonly confirm = output<void>();
  /** Emitted on cancel/dismiss (Cancel button, ESC, or backdrop click). */
  readonly cancel = output<void>();

  /** Per-instance id seed so concurrent dialogs never share aria ids. */
  private readonly instanceId = (uniqueConfirmationDialogId += 1);
  /** Stable id for the title element, referenced by `aria-labelledby`. */
  protected readonly titleId = `app-confirmation-dialog-title-${this.instanceId}`;
  /** Stable id for the message element, referenced by `aria-describedby`. */
  protected readonly messageId = `app-confirmation-dialog-message-${this.instanceId}`;

  /**
   * The element that had focus when the dialog opened (typically the trigger
   * button). Captured on open and refocused on close for correct focus restoration.
   * Initialised to `null` to satisfy `strictPropertyInitialization`.
   */
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    // React to open-state transitions: on open, capture the trigger and move focus
    // into the dialog; on close, restore focus to the trigger. Running this inside an
    // `effect()` (created within the constructor's injection context) keeps focus
    // management in lock-step with the `open` signal regardless of who toggles it
    // (the Cancel/Confirm buttons, ESC, the backdrop, or a two-way `[(open)]` parent).
    effect(() => {
      if (this.open()) {
        this.onOpened();
      } else {
        this.onClosed();
      }
    });
  }

  /**
   * Global-per-host keyboard handler for the modal. Bound to the host (not the
   * overlay) so it keeps working even though the overlay is rendered inline within
   * the host — `keydown` events bubble up to this listener. No-ops while closed.
   *
   * - `Escape` -> cancel (matches the browser `confirm()`'s Cancel button).
   * - `Tab` / `Shift+Tab` -> delegate to the focus trap so focus stays inside.
   */
  @HostListener('keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    if (!this.open()) {
      return;
    }
    if (event.key === 'Escape') {
      // MIGRATION: ESC == the browser confirm()'s Cancel — dismiss without deleting.
      event.preventDefault();
      this.onCancel();
      return;
    }
    if (event.key === 'Tab') {
      this.trapFocus(event);
    }
  }

  /**
   * Confirm handler. Closes the dialog (presentation) then emits `confirm` so the
   * feature can perform the actual DELETE. Order matters: closing first restores
   * focus to the trigger before the feature reacts.
   */
  protected onConfirm(): void {
    this.open.set(false);
    this.confirm.emit();
  }

  /**
   * Cancel handler shared by the Cancel button, ESC, and backdrop dismissal. Closes
   * the dialog then emits `cancel`.
   */
  protected onCancel(): void {
    this.open.set(false);
    this.cancel.emit();
  }

  /**
   * Backdrop (overlay) click handler. Treated as a cancel. Clicks that originate on
   * the dialog surface are stopped in the template (`$event.stopPropagation()`) and
   * therefore never reach this handler.
   */
  protected onBackdropClick(): void {
    this.onCancel();
  }

  /**
   * Runs when the dialog transitions to open. Captures the currently focused element
   * (the trigger) for later restoration, then defers moving focus into the dialog
   * until the `@if` block has rendered the surface into the host DOM.
   */
  private onOpened(): void {
    const active = this.document.activeElement;
    // Narrow with `instanceof` (no `as`/`any`) — activeElement may be null or a
    // non-HTMLElement (e.g. an SVGElement), neither of which we can `.focus()` safely.
    this.previouslyFocused = active instanceof HTMLElement ? active : null;
    // Defer until the @if block has rendered the dialog into the host DOM. The guard
    // inside focusInitialElement() makes this race-safe against a rapid open->close.
    setTimeout(() => this.focusInitialElement(), 0);
  }

  /**
   * Runs when the dialog transitions to closed. Restores focus to the element that
   * was focused when the dialog opened (the trigger), then clears the reference.
   */
  private onClosed(): void {
    const trigger = this.previouslyFocused;
    this.previouslyFocused = null;
    trigger?.focus();
  }

  /**
   * Moves focus to the first focusable element inside the dialog (the Cancel button,
   * a safe default for destructive actions so an accidental Enter cannot trigger the
   * confirm), falling back to the dialog surface itself. Guarded so a dialog that was
   * closed again before this deferred callback ran does nothing.
   */
  private focusInitialElement(): void {
    if (!this.open()) {
      return;
    }
    const dialog = this.getDialogElement();
    if (!dialog) {
      return;
    }
    const focusable = this.getFocusableElements(dialog);
    (focusable[0] ?? dialog).focus();
  }

  /**
   * Focus trap: keeps Tab / Shift+Tab cycling within the dialog's focusable elements,
   * wrapping last -> first (Tab) and first -> last (Shift+Tab). Also re-enters the
   * dialog if focus has somehow escaped it.
   */
  private trapFocus(event: KeyboardEvent): void {
    const dialog = this.getDialogElement();
    if (!dialog) {
      return;
    }
    const focusable = this.getFocusableElements(dialog);
    if (focusable.length === 0) {
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = this.document.activeElement;
    if (event.shiftKey) {
      if (active === first || !dialog.contains(active)) {
        event.preventDefault();
        last.focus();
      }
    } else if (active === last || !dialog.contains(active)) {
      event.preventDefault();
      first.focus();
    }
  }

  /**
   * Resolves the live dialog surface element (the `role="dialog"` node) from the
   * host, or `null` when the dialog is not currently rendered.
   */
  private getDialogElement(): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>('[role="dialog"]');
  }

  /**
   * Returns the tab-focusable descendants of the given container, in DOM order.
   * The selector intentionally excludes disabled controls and `tabindex="-1"`
   * elements so the trap only cycles genuinely reachable stops.
   */
  private getFocusableElements(container: HTMLElement): HTMLElement[] {
    const selector = [
      'a[href]',
      'button:not([disabled])',
      'textarea:not([disabled])',
      'input:not([disabled])',
      'select:not([disabled])',
      '[tabindex]:not([tabindex="-1"])',
    ].join(',');
    return Array.from(container.querySelectorAll<HTMLElement>(selector));
  }
}
