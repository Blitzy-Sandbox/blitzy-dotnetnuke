import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

/** Monotonic counter giving each dialog instance unique, stable element ids for ARIA wiring. */
let uniqueDialogId = 0;

/**
 * ConfirmationDialogComponent — reusable, accessible delete-confirmation modal.
 *
 * Replaces the legacy DotNetNuke admin-grid browser `confirm()` (Website/admin/Portal/Portals.ascx.vb
 * ~L300/L437; Website/admin/Users/User.ascx.vb L255-260) with an in-app modal that carries identical
 * confirm/cancel semantics. Presentation-only: it emits `confirm`/`cancel` and performs NO domain or
 * HTTP work — the parent feature deletes in its `(confirm)` handler.
 */
@Component({
  selector: 'app-confirmation-dialog',
  templateUrl: './confirmation-dialog.component.html',
  styleUrl: './confirmation-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmationDialogComponent {
  /** Whether the modal is visible. Bare attribute (`open`) coerces to `true` via booleanAttribute. */
  readonly open = input(false, { transform: booleanAttribute });
  /** Dialog heading text. */
  readonly title = input('Confirm');
  /** Dialog body question. Default mirrors the legacy "Are you sure you want to delete this item?". */
  readonly message = input('Are you sure you want to delete this item?');
  /** Confirm (primary) button label. */
  readonly confirmText = input('Delete');
  /** Cancel (secondary) button label. */
  readonly cancelText = input('Cancel');
  /** When true, styles the confirm button as destructive (danger). Defaults to true (delete flow). */
  readonly destructive = input(true, { transform: booleanAttribute });

  /** Emitted when the user confirms. The PARENT performs the actual delete. */
  readonly confirm = output<void>();
  /** Emitted when the user cancels (cancel button, backdrop click, or ESC). */
  readonly cancel = output<void>();

  /** The dialog container element (present only while `open()` is true). */
  private readonly dialogRef = viewChild<ElementRef<HTMLElement>>('dialog');

  private readonly uid = uniqueDialogId++;
  protected readonly titleId = `cd-title-${this.uid}`;
  protected readonly messageId = `cd-message-${this.uid}`;

  /** The element focused before the dialog opened, restored on close. */
  private previousActiveElement: HTMLElement | null = null;
  private hasFocused = false;

  /**
   * Re-entry guard: ensures `confirm` is emitted AT MOST ONCE per open cycle, even if the confirm
   * affordance is activated several times before the parent tears the dialog down. It is reset
   * whenever the dialog (re)opens (in the focus `effect` below), so each fresh open can confirm once.
   *
   * HARDENING (F4 INFO, defense-in-depth): under normal interaction the parent detaches the dialog
   * synchronously on the first confirm, so a second activation already lands on a removed node; this
   * guard additionally closes the same-tick programmatic repeated-confirm bypass the QA noted. A
   * normal single confirm is unchanged. It is a plain field (not a signal) deliberately — mirroring
   * `hasFocused` — so it can be reset inside the effect without writing a signal from an effect.
   */
  private confirmed = false;

  constructor() {
    // Safety net: if the component is destroyed while open, restore focus to the trigger.
    inject(DestroyRef).onDestroy(() => this.restoreFocus());

    // Focus management driven by the `open` input + the conditionally-rendered dialog element.
    effect(() => {
      const isOpen = this.open();
      const dialog = this.dialogRef();

      if (isOpen && dialog && !this.hasFocused) {
        // Opening: remember the trigger, then move focus into the dialog container (safe, non-destructive).
        this.previousActiveElement =
          document.activeElement instanceof HTMLElement ? document.activeElement : null;
        dialog.nativeElement.focus();
        this.hasFocused = true;
        // Reset the at-most-once confirm guard for this fresh open cycle.
        this.confirmed = false;
      } else if (!isOpen && this.hasFocused) {
        // Closing: return focus to the trigger.
        this.restoreFocus();
        this.hasFocused = false;
      }
    });
  }

  protected onConfirm(): void {
    // Re-entry guard: emit `confirm` at most once per open cycle (see `confirmed`).
    if (this.confirmed) {
      return;
    }
    this.confirmed = true;
    this.confirm.emit();
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected onBackdropClick(): void {
    this.cancel.emit();
  }

  protected onKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        this.cancel.emit();
        break;
      case 'Enter':
        // Let a focused <button> activate natively; otherwise Enter confirms (keyboard shortcut).
        // Route through onConfirm() so the keyboard path shares the at-most-once re-entry guard.
        if (!(event.target instanceof HTMLButtonElement)) {
          event.preventDefault();
          this.onConfirm();
        }
        break;
      case 'Tab':
        this.trapFocus(event);
        break;
      default:
        break;
    }
  }

  /** Keep Tab focus cycling within the dialog (focus trap). */
  private trapFocus(event: KeyboardEvent): void {
    const dialog = this.dialogRef()?.nativeElement;
    if (!dialog) {
      return;
    }

    const focusable = Array.from(
      dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ),
    );
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private restoreFocus(): void {
    this.previousActiveElement?.focus();
    this.previousActiveElement = null;
  }
}
