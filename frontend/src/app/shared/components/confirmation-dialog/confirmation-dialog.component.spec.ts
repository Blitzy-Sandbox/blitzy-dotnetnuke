import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { ConfirmationDialogComponent } from './confirmation-dialog.component';

/**
 * Unit tests for {@link ConfirmationDialogComponent}.
 *
 * The component is a standalone Angular 19 component, so it is provided to the
 * TestBed via `imports:` (NOT `declarations:`) and instantiated with
 * `TestBed.createComponent`. Signal inputs (`open`, `title`, `message`,
 * `confirmText`, `cancelText`) are read-only from outside the component, so they
 * are driven exclusively through `fixture.componentRef.setInput(...)` followed by
 * a change-detection pass. Output emissions (`confirm`, `cancel`) are asserted by
 * spying on the `emit` method of each `OutputEmitterRef`.
 *
 * These specs back Gate 4 (`ng test --watch=false --browsers=ChromeHeadless`).
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

  /** Opens the dialog by setting the `open` signal input and running change detection. */
  function open(): void {
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
  }

  /**
   * Resolves the rendered dialog container, or `null` when the dialog is closed.
   * `By.css` may match nothing, so the lookup is guarded to keep the return type
   * precise (`HTMLElement | null`) instead of leaking the library's `any`.
   */
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
});
