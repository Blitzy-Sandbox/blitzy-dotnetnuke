import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { ConfirmationDialogComponent } from './confirmation-dialog.component';

/**
 * Unit tests for {@link ConfirmationDialogComponent} (Gate 4).
 *
 * `ConfirmationDialogComponent` is a standalone, OnPush component, so it is registered through
 * `imports:` (NOT `declarations:`) and instantiated with `TestBed.createComponent`. No NgModule
 * test setup is required and none is used.
 *
 * The component exposes signal-based `input()` members (`open`, `title`, `message`, `confirmText`,
 * `cancelText`, `destructive`). Signal inputs are read-only from outside the component, so the suite
 * drives them through `fixture.componentRef.setInput(name, value)` followed by `detectChanges()` —
 * the supported way to feed `input()` signals in tests. The `open()` helper centralises the common
 * "set open + render" step.
 *
 * The `confirm`/`cancel` members are `output()` emitters (`OutputEmitterRef`). Their `emit` method is
 * spy-able, so emissions are asserted with `spyOn(component.<output>, 'emit')` and
 * `toHaveBeenCalledTimes(1)`. The spies deliberately do NOT call through — the suite only asserts the
 * emission occurred.
 *
 * Types are kept precise (no `any`): `DebugElement.query(...)` exposes an `any`-typed `nativeElement`,
 * so each lookup is cast to the exact element type (`HTMLElement` / `HTMLButtonElement`). The
 * `queryDialog()` helper guards the (runtime-nullable) dialog lookup and returns `HTMLElement | null`
 * so the closed-state assertion can check for a genuine absence.
 *
 * The component animates with plain CSS (no `@angular/animations`), so `provideNoopAnimations()` is
 * intentionally omitted, and the component performs no HTTP work, so no HTTP testing utilities are used.
 */
describe('ConfirmationDialogComponent', () => {
  let fixture: ComponentFixture<ConfirmationDialogComponent>;
  let component: ConfirmationDialogComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmationDialogComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfirmationDialogComponent);
    component = fixture.componentInstance;
  });

  /** Sets the `open` signal input to `true` and triggers a change-detection pass. */
  function open(): void {
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
  }

  /** Sets the `open` signal input to `false` and triggers a change-detection pass. */
  function close(): void {
    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();
  }

  /** Returns the rendered dialog container, or `null` when the modal is closed. */
  function queryDialog(): HTMLElement | null {
    const debugEl = fixture.debugElement.query(By.css('[role="dialog"]'));
    return debugEl ? (debugEl.nativeElement as HTMLElement) : null;
  }

  it('creates the component', () => {
    expect(component).toBeTruthy();
  });

  it('does not render the dialog when closed', () => {
    fixture.detectChanges();
    expect(queryDialog()).toBeNull();
  });

  it('renders the dialog with role and aria-modal when open', () => {
    open();
    const dialog = queryDialog();
    expect(dialog).not.toBeNull();
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
  });

  it('projects the title and message inputs', () => {
    fixture.componentRef.setInput('title', 'Delete Portal');
    fixture.componentRef.setInput('message', 'This action cannot be undone.');
    open();

    const title = fixture.debugElement.query(By.css('.cd-dialog__title')).nativeElement as HTMLElement;
    const message = fixture.debugElement.query(By.css('.cd-dialog__message')).nativeElement as HTMLElement;
    expect(title.textContent).toContain('Delete Portal');
    expect(message.textContent).toContain('This action cannot be undone.');
  });

  it('associates aria-labelledby and aria-describedby with the title and message', () => {
    open();
    const dialog = queryDialog();
    const title = fixture.debugElement.query(By.css('.cd-dialog__title')).nativeElement as HTMLElement;
    const message = fixture.debugElement.query(By.css('.cd-dialog__message')).nativeElement as HTMLElement;
    expect(dialog?.getAttribute('aria-labelledby')).toBe(title.id);
    expect(dialog?.getAttribute('aria-describedby')).toBe(message.id);
  });

  it('renders custom confirm and cancel labels', () => {
    fixture.componentRef.setInput('confirmText', 'Yes, delete');
    fixture.componentRef.setInput('cancelText', 'No, keep it');
    open();

    const confirmBtn = fixture.debugElement.query(By.css('.cd-btn--confirm')).nativeElement as HTMLButtonElement;
    const cancelBtn = fixture.debugElement.query(By.css('.cd-btn--cancel')).nativeElement as HTMLButtonElement;
    expect(confirmBtn.textContent).toContain('Yes, delete');
    expect(cancelBtn.textContent).toContain('No, keep it');
  });

  it('emits confirm when the confirm button is clicked', () => {
    const emitSpy = spyOn(component.confirm, 'emit');
    open();
    (fixture.debugElement.query(By.css('.cd-btn--confirm')).nativeElement as HTMLButtonElement).click();
    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel when the cancel button is clicked', () => {
    const emitSpy = spyOn(component.cancel, 'emit');
    open();
    (fixture.debugElement.query(By.css('.cd-btn--cancel')).nativeElement as HTMLButtonElement).click();
    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel when the backdrop is clicked', () => {
    const emitSpy = spyOn(component.cancel, 'emit');
    open();
    (fixture.debugElement.query(By.css('.cd-overlay')).nativeElement as HTMLElement).click();
    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel when Escape is pressed', () => {
    const emitSpy = spyOn(component.cancel, 'emit');
    open();
    queryDialog()?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('emits confirm when Enter is pressed on the dialog container', () => {
    const emitSpy = spyOn(component.confirm, 'emit');
    open();
    queryDialog()?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('emits confirm at most ONCE for repeated synchronous confirm clicks (re-entry guard)', () => {
    const emitSpy = spyOn(component.confirm, 'emit');
    open();
    const confirmBtn = fixture.debugElement.query(By.css('.cd-btn--confirm'))
      .nativeElement as HTMLButtonElement;

    // HARDENING (F4 INFO): fire the confirm affordance three times within the same cycle,
    // before any parent tears the dialog down. The re-entry guard must collapse these into a
    // single emission so only one DELETE can ever be dispatched per open cycle.
    confirmBtn.click();
    confirmBtn.click();
    confirmBtn.click();

    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('routes Enter through the same guard (repeated Enter confirms at most once)', () => {
    const emitSpy = spyOn(component.confirm, 'emit');
    open();
    const dialog = queryDialog();

    dialog?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    dialog?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));

    expect(emitSpy).toHaveBeenCalledTimes(1);
  });

  it('re-arms the confirm guard when the dialog is reopened (one emit per open cycle)', () => {
    const emitSpy = spyOn(component.confirm, 'emit');

    open();
    (fixture.debugElement.query(By.css('.cd-btn--confirm')).nativeElement as HTMLButtonElement).click();
    expect(emitSpy).toHaveBeenCalledTimes(1);

    // A fresh open cycle resets the guard so the next genuine confirm is allowed again.
    close();
    open();
    (fixture.debugElement.query(By.css('.cd-btn--confirm')).nativeElement as HTMLButtonElement).click();

    expect(emitSpy).toHaveBeenCalledTimes(2);
  });
});
