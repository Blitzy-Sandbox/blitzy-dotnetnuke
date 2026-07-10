/**
 * Unit tests for {@link ConfirmationDialogComponent}.
 *
 * Backs Validation Gate 4 (`ng test --watch=false --browsers=ChromeHeadless
 * --code-coverage`, 100% pass). The suite verifies the accessible-modal contract
 * of the presentation-only confirm/delete dialog:
 *   1. `confirm` emits when the confirm button is clicked.
 *   2. `cancel` emits when the cancel button is clicked.
 *   3. ESC emits `cancel` and closes the dialog.
 *   4. Focus trap — Tab wraps last -> first, Shift+Tab wraps first -> last.
 *   5. Initial focus lands inside the dialog on open (deferred via setTimeout(0)).
 *   6. `role="dialog"` + `aria-modal="true"` present, with resolvable
 *      `aria-labelledby` / `aria-describedby`.
 *
 * Testing techniques (see the component under test for the behaviours asserted):
 * - The component is STANDALONE (Angular v19), so it goes in `imports`, never
 *   `declarations`.
 * - The fixture host is appended to `document.body` in `beforeEach` (and removed in
 *   `afterEach`) because `.focus()` / `document.activeElement` only work on elements
 *   attached to the live document — omitting this makes focus assertions silently
 *   fail.
 * - Inputs / the two-way `open` model are set with `fixture.componentRef.setInput`
 *   and read back through the `open()` signal.
 * - Outputs are `OutputEmitterRef`s, subscribed via `.subscribe(spy)`.
 * - The component defers its initial focus with `setTimeout(0)`, so every focus
 *   assertion runs inside `fakeAsync` and flushes the timer with `tick()`.
 * - Synthetic `keydown` events don't move focus natively, so trap tests position
 *   focus manually and assert the component's `preventDefault()` + manual-focus
 *   behaviour. Events are dispatched with `bubbles: true` so they reach the host's
 *   `@HostListener('keydown')`.
 */
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { ConfirmationDialogComponent } from './confirmation-dialog.component';

describe('ConfirmationDialogComponent', () => {
  let fixture: ComponentFixture<ConfirmationDialogComponent>;
  let component: ConfirmationDialogComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmationDialogComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmationDialogComponent);
    component = fixture.componentInstance;
    // Attach to the live DOM so focus()/document.activeElement work.
    document.body.appendChild(fixture.nativeElement);
  });

  afterEach(() => {
    fixture.nativeElement.remove();
  });

  function openDialog(): void {
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
  }

  function queryDialog(): HTMLElement | null {
    return fixture.nativeElement.querySelector('[role="dialog"]');
  }

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('does not render the dialog while closed', () => {
    fixture.detectChanges();
    expect(queryDialog()).toBeNull();
  });

  it('renders an accessible dialog (role, aria-modal, labelled/described) when open', () => {
    fixture.componentRef.setInput('title', 'Delete role');
    fixture.componentRef.setInput('message', 'Are you sure you want to delete this item?');
    openDialog();

    const dialog = queryDialog();
    expect(dialog).not.toBeNull();
    expect(dialog?.getAttribute('role')).toBe('dialog');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');

    const labelledBy = dialog?.getAttribute('aria-labelledby');
    const describedBy = dialog?.getAttribute('aria-describedby');
    expect(labelledBy).toBeTruthy();
    expect(describedBy).toBeTruthy();

    const titleEl = labelledBy ? fixture.nativeElement.querySelector(`#${labelledBy}`) : null;
    const messageEl = describedBy ? fixture.nativeElement.querySelector(`#${describedBy}`) : null;
    expect(titleEl?.textContent).toContain('Delete role');
    expect(messageEl?.textContent).toContain('Are you sure you want to delete this item?');
  });

  it('emits confirm when the confirm button is clicked', () => {
    const spy = jasmine.createSpy('confirm');
    component.confirm.subscribe(spy);
    openDialog();

    const confirmBtn = fixture.nativeElement.querySelector('.cdlg-btn--confirm') as HTMLButtonElement;
    confirmBtn.click();

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel when the cancel button is clicked', () => {
    const spy = jasmine.createSpy('cancel');
    component.cancel.subscribe(spy);
    openDialog();

    const cancelBtn = fixture.nativeElement.querySelector('.cdlg-btn--cancel') as HTMLButtonElement;
    cancelBtn.click();

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel and closes on Escape', () => {
    const spy = jasmine.createSpy('cancel');
    component.cancel.subscribe(spy);
    openDialog();

    const dialog = queryDialog() as HTMLElement;
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(spy).toHaveBeenCalledTimes(1);
    expect(component.open()).toBeFalse();
  });

  it('emits cancel when the backdrop is clicked', () => {
    const spy = jasmine.createSpy('cancel');
    component.cancel.subscribe(spy);
    openDialog();

    const overlay = fixture.nativeElement.querySelector('.cdlg-overlay') as HTMLElement;
    overlay.click();

    expect(spy).toHaveBeenCalledTimes(1);
  });

  it('moves initial focus into the dialog when opened', fakeAsync(() => {
    openDialog();
    tick(); // flush the deferred setTimeout(0) initial focus

    const dialog = queryDialog() as HTMLElement;
    expect(dialog.contains(document.activeElement)).toBeTrue();
  }));

  it('traps focus: Tab from the last element wraps to the first', fakeAsync(() => {
    openDialog();
    tick();

    const dialog = queryDialog() as HTMLElement;
    const buttons = dialog.querySelectorAll<HTMLButtonElement>('button');
    const first = buttons[0];
    const last = buttons[buttons.length - 1];

    last.focus();
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));

    expect(document.activeElement).toBe(first);
  }));

  it('traps focus: Shift+Tab from the first element wraps to the last', fakeAsync(() => {
    openDialog();
    tick();

    const dialog = queryDialog() as HTMLElement;
    const buttons = dialog.querySelectorAll<HTMLButtonElement>('button');
    const first = buttons[0];
    const last = buttons[buttons.length - 1];

    first.focus();
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }));

    expect(document.activeElement).toBe(last);
  }));

  it('restores focus to the trigger when closed', fakeAsync(() => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();
    expect(document.activeElement).toBe(trigger);

    openDialog();
    tick(); // initial focus moves into the dialog
    expect(document.activeElement).not.toBe(trigger);

    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();

    expect(document.activeElement).toBe(trigger);
    trigger.remove();
  }));
});
