// MIGRATION: Accessible confirmation dialog that replaces the legacy DotNetNuke client-side
// delete-confirmation gate. DNN admin grids guarded destructive actions with a browser confirm()/
// OnClickJS prompt — verified in Website/admin/Portal/Portals.ascx.vb at L300
// (imageColumn.OnClickJS = Localization.GetString("DeleteItem")) and L437
// (confirm('" + ClientAPI.GetSafeJSString(Localization.GetString("DeleteItems.Confirm")) + "')) —
// a pattern that recurs across ~28 Website/admin/** grids. Re-expressed here as a reusable,
// presentational-only, standalone Angular component that emits confirm/cancel and is fully
// keyboard- and screen-reader-accessible (role="dialog", aria-modal, focus trap, Escape-to-cancel,
// initial focus on Confirm). No @angular/cdk / Angular Material is used (AAP 0.3.7 — no external
// design system). Net-new; the legacy VB/Web Forms markup itself is not migrated (AAP 0.6.2).
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  inject,
  input,
  output,
} from '@angular/core';
import { animate, style, transition, trigger } from '@angular/animations';

import { AutofocusDirective } from '../../directives/autofocus.directive';

// Module-level counter producing unique element ids so multiple dialog instances do not collide
// on the aria-labelledby / aria-describedby target ids.
let nextUniqueId = 0;

@Component({
  selector: 'app-confirmation-dialog',
  standalone: true,
  imports: [AutofocusDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(keydown.escape)': 'onEscapeKey($event)',
    '(keydown.tab)': 'onTabKey($event)',
    '(keydown.shift.tab)': 'onTabKey($event)',
  },
  animations: [
    trigger('cdDialogAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-8px) scale(0.98)' }),
        animate('150ms ease-out', style({ opacity: 1, transform: 'translateY(0) scale(1)' })),
      ]),
    ]),
  ],
  template: `
    <div class="cd-overlay">
      <div
        class="cd-dialog"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="titleId"
        [attr.aria-describedby]="messageId"
        @cdDialogAnim
      >
        <h2 class="cd-title" [id]="titleId">{{ title() }}</h2>
        <p class="cd-message" [id]="messageId">{{ message() }}</p>
        <div class="cd-actions">
          <button type="button" class="cd-button cd-button--cancel" (click)="onCancel()">
            {{ cancelLabel() }}
          </button>
          <button
            type="button"
            class="cd-button cd-button--confirm"
            appAutofocus
            (click)="onConfirm()"
          >
            {{ confirmLabel() }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .cd-overlay {
        position: fixed;
        inset: 0;
        z-index: 1000;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: var(--space-4, 16px);
        background: var(--color-overlay, rgba(0, 0, 0, 0.5));
      }

      .cd-dialog {
        width: 100%;
        max-width: 28rem;
        padding: var(--space-5, 24px);
        background: var(--color-surface, #ffffff);
        color: var(--color-text, #1a1a1a);
        border-radius: var(--radius-md, 8px);
        box-shadow: var(--shadow-md, 0 10px 25px rgba(0, 0, 0, 0.25));
      }

      .cd-title {
        margin: 0 0 var(--space-2, 8px);
        font-size: 1.25rem;
        font-weight: 600;
      }

      .cd-message {
        margin: 0 0 var(--space-5, 24px);
        color: var(--color-text-muted, #555555);
        line-height: var(--line-height-base, 1.5);
      }

      .cd-actions {
        display: flex;
        justify-content: flex-end;
        gap: var(--space-2, 8px);
      }

      .cd-button {
        appearance: none;
        cursor: pointer;
        font: inherit;
        font-weight: 600;
        line-height: 1;
        padding: var(--space-2, 8px) var(--space-4, 16px);
        border: 1px solid transparent;
        border-radius: var(--radius-sm, 4px);
      }

      .cd-button--cancel {
        background: var(--color-surface, #ffffff);
        border-color: var(--color-border, #d0d0d0);
        color: var(--color-text, #1a1a1a);
      }

      .cd-button--confirm {
        background: var(--color-danger, #c62828);
        color: var(--color-primary-contrast, #ffffff);
      }
    `,
  ],
})
export class ConfirmationDialogComponent implements OnInit, OnDestroy {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly instanceId = ++nextUniqueId;

  /** Id of the title element, wired to the dialog's aria-labelledby. */
  protected readonly titleId = `cd-title-${this.instanceId}`;

  /** Id of the message element, wired to the dialog's aria-describedby. */
  protected readonly messageId = `cd-message-${this.instanceId}`;

  /** Heading text of the dialog (rendered via interpolation — Angular escapes it). */
  readonly title = input.required<string>();

  /** Body/description text of the dialog (rendered via interpolation — Angular escapes it). */
  readonly message = input.required<string>();

  /** Label of the confirm (destructive) button, e.g. "Delete". */
  readonly confirmLabel = input.required<string>();

  /** Label of the cancel button. A sensible default keeps this input optional. */
  readonly cancelLabel = input<string>('Cancel');

  /** Emitted when the user confirms the action (Confirm button). */
  readonly confirm = output<void>();

  /** Emitted when the user cancels (Cancel button or the Escape key). */
  readonly cancel = output<void>();

  /** Element that held focus before the dialog opened; focus is restored to it on close. */
  private previouslyFocused: HTMLElement | null = null;

  ngOnInit(): void {
    // Capture the invoking control before autofocus moves focus into the dialog.
    this.previouslyFocused =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
  }

  ngOnDestroy(): void {
    // MIGRATION: return focus to the invoking control when the dialog closes (accessibility NFR;
    // the legacy Web Forms postback had no client-side focus management to preserve).
    this.previouslyFocused?.focus();
  }

  protected onConfirm(): void {
    this.confirm.emit();
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected onEscapeKey(event: KeyboardEvent): void {
    event.preventDefault();
    this.onCancel();
  }

  protected onTabKey(event: KeyboardEvent): void {
    // Trap Tab / Shift+Tab focus within the dialog's focusable controls.
    const focusable = this.getFocusableElements();
    const first = focusable.at(0);
    const last = focusable.at(-1);
    if (!first || !last) {
      event.preventDefault();
      return;
    }

    const active = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const activeIndex = active ? focusable.indexOf(active) : -1;

    if (event.shiftKey) {
      if (activeIndex <= 0) {
        event.preventDefault();
        last.focus();
      }
    } else if (activeIndex === -1 || activeIndex === focusable.length - 1) {
      event.preventDefault();
      first.focus();
    }
  }

  private getFocusableElements(): HTMLElement[] {
    const selector =
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(this.elementRef.nativeElement.querySelectorAll<HTMLElement>(selector));
  }
}
