// MIGRATION: Spec for the net-new accessible ConfirmationDialogComponent. There is no legacy
// equivalent — the component replaces DotNetNuke's client-side confirm()/OnClickJS delete gate
// (Website/admin/Portal/Portals.ascx.vb L300/L437). Verifies rendering, confirm/cancel emission
// (button clicks + Escape), dialog ARIA semantics, focus-on-open, focus trap, and focus restoration.
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { ConfirmationDialogComponent } from './confirmation-dialog.component';

describe('ConfirmationDialogComponent', () => {
  const fixtures: ComponentFixture<ConfirmationDialogComponent>[] = [];
  const detached: HTMLElement[] = [];

  function createDialog(): ComponentFixture<ConfirmationDialogComponent> {
    const fixture = TestBed.createComponent(ConfirmationDialogComponent);
    fixture.componentRef.setInput('title', 'Delete Portal');
    fixture.componentRef.setInput('message', 'Are you sure you want to delete this item?');
    fixture.componentRef.setInput('confirmLabel', 'Delete');
    // Attach to the live DOM so HTMLElement.focus() updates document.activeElement.
    const host = fixture.nativeElement as HTMLElement;
    document.body.appendChild(host);
    fixtures.push(fixture);
    detached.push(host);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ConfirmationDialogComponent],
      providers: [provideNoopAnimations()],
    });
  });

  afterEach(() => {
    while (fixtures.length > 0) {
      fixtures.pop()?.destroy();
    }
    while (detached.length > 0) {
      detached.pop()?.remove();
    }
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur();
    }
  });

  it('should create', () => {
    const fixture = createDialog();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the title, message and confirm label', () => {
    const host = createDialog().nativeElement as HTMLElement;
    expect(host.querySelector('.cd-title')?.textContent).toContain('Delete Portal');
    expect(host.querySelector('.cd-message')?.textContent).toContain(
      'Are you sure you want to delete this item?',
    );
    expect(host.querySelector('.cd-button--confirm')?.textContent).toContain('Delete');
  });

  it('uses the default cancel label when none is provided', () => {
    const host = createDialog().nativeElement as HTMLElement;
    expect(host.querySelector('.cd-button--cancel')?.textContent).toContain('Cancel');
  });

  it('exposes role="dialog" and aria-modal with wired aria-labelledby/aria-describedby', () => {
    const host = createDialog().nativeElement as HTMLElement;
    const dialog = host.querySelector('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(dialog?.getAttribute('aria-modal')).toBe('true');

    const labelledBy = dialog?.getAttribute('aria-labelledby');
    const describedBy = dialog?.getAttribute('aria-describedby');
    expect(labelledBy).toBeTruthy();
    expect(describedBy).toBeTruthy();
    expect(host.querySelector('.cd-title')?.id).toBe(labelledBy ?? '');
    expect(host.querySelector('.cd-message')?.id).toBe(describedBy ?? '');
  });

  it('focuses the confirm button on open', () => {
    const host = createDialog().nativeElement as HTMLElement;
    const confirmButton = host.querySelector('.cd-button--confirm');
    expect(document.activeElement).toBe(confirmButton);
  });

  it('emits confirm when the confirm button is clicked', () => {
    const fixture = createDialog();
    let confirmed = false;
    fixture.componentInstance.confirm.subscribe(() => (confirmed = true));
    const confirmButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--confirm',
    ) as HTMLButtonElement;
    confirmButton.click();
    expect(confirmed).toBeTrue();
  });

  it('emits cancel when the cancel button is clicked', () => {
    const fixture = createDialog();
    let cancelled = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));
    const cancelButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--cancel',
    ) as HTMLButtonElement;
    cancelButton.click();
    expect(cancelled).toBeTrue();
  });

  it('emits cancel when the Escape key is pressed', () => {
    const fixture = createDialog();
    let cancelled = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));
    const host = fixture.nativeElement as HTMLElement;
    host.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(cancelled).toBeTrue();
  });

  it('traps focus: Tab on the last control wraps to the first', () => {
    const host = createDialog().nativeElement as HTMLElement;
    const cancelButton = host.querySelector('.cd-button--cancel') as HTMLButtonElement;
    const confirmButton = host.querySelector('.cd-button--confirm') as HTMLButtonElement;
    confirmButton.focus();
    expect(document.activeElement).toBe(confirmButton);
    confirmButton.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    expect(document.activeElement).toBe(cancelButton);
  });

  it('traps focus: Shift+Tab on the first control wraps to the last', () => {
    const host = createDialog().nativeElement as HTMLElement;
    const cancelButton = host.querySelector('.cd-button--cancel') as HTMLButtonElement;
    const confirmButton = host.querySelector('.cd-button--confirm') as HTMLButtonElement;
    cancelButton.focus();
    expect(document.activeElement).toBe(cancelButton);
    cancelButton.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }),
    );
    expect(document.activeElement).toBe(confirmButton);
  });

  it('returns focus to the invoking element when closed', () => {
    const invoker = document.createElement('button');
    invoker.textContent = 'Open dialog';
    document.body.appendChild(invoker);
    detached.push(invoker);
    invoker.focus();
    expect(document.activeElement).toBe(invoker);

    const fixture = createDialog();
    // On open, focus moves into the dialog (Confirm button), away from the invoker.
    expect(document.activeElement).not.toBe(invoker);

    // Closing (destroying) the dialog restores focus to the invoker.
    const index = fixtures.indexOf(fixture);
    if (index !== -1) {
      fixtures.splice(index, 1);
    }
    fixture.destroy();
    expect(document.activeElement).toBe(invoker);
  });

  // MIGRATION: [QA F4-014] re-entrancy guard. While the host's confirmed operation (e.g. a DELETE) is in
  // flight the host sets [busy]="true": the Confirm button is disabled AND onConfirm() short-circuits, so
  // rapid repeated clicks on the still-mounted dialog cannot emit (confirm) more than once. Cancel stays
  // enabled so the user can always dismiss. Default [busy]=false keeps existing call sites unchanged.
  it('disables the confirm button while [busy] is true', () => {
    const fixture = createDialog();
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();

    const confirmButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--confirm',
    ) as HTMLButtonElement;
    expect(confirmButton.disabled).toBeTrue();
  });

  it('does NOT emit confirm even when a click reaches the handler while [busy] is true', () => {
    const fixture = createDialog();
    let confirmCount = 0;
    fixture.componentInstance.confirm.subscribe(() => (confirmCount += 1));

    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();

    const confirmButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--confirm',
    ) as HTMLButtonElement;
    // dispatchEvent bypasses the native disabled-click suppression so the (click)->onConfirm() handler
    // actually runs; the busy() short-circuit inside onConfirm must still prevent every (confirm) emission.
    confirmButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    confirmButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    confirmButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(confirmCount).toBe(0);
  });

  it('keeps the cancel button enabled while [busy] is true so the dialog can always be dismissed', () => {
    const fixture = createDialog();
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();

    const cancelButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--cancel',
    ) as HTMLButtonElement;
    expect(cancelButton.disabled).toBeFalse();

    let cancelled = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));
    cancelButton.click();
    expect(cancelled).toBeTrue();
  });

  it('defaults [busy] to false so the confirm button is enabled and emits', () => {
    const fixture = createDialog();
    expect(fixture.componentInstance.busy()).toBeFalse();

    let confirmed = false;
    fixture.componentInstance.confirm.subscribe(() => (confirmed = true));
    const confirmButton = (fixture.nativeElement as HTMLElement).querySelector(
      '.cd-button--confirm',
    ) as HTMLButtonElement;
    expect(confirmButton.disabled).toBeFalse();
    confirmButton.click();
    expect(confirmed).toBeTrue();
  });
});
