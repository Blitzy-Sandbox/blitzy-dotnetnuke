import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { AutofocusDirective } from './autofocus.directive';

@Component({
  template: `<input type="text" [appAutofocus]="enabled" />`,
  imports: [AutofocusDirective],
})
class TestHostComponent {
  enabled = true;
}

describe('AutofocusDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
  });

  it('should create the host with the directive applied', () => {
    const directiveEl = fixture.debugElement.query(By.directive(AutofocusDirective));
    expect(directiveEl).toBeTruthy();
  });

  it('should focus the host element after render when enabled', async () => {
    const input = fixture.debugElement.query(By.directive(AutofocusDirective)).nativeElement as HTMLInputElement;
    const focusSpy = spyOn(input, 'focus');

    fixture.detectChanges();
    await fixture.whenStable();

    expect(focusSpy).toHaveBeenCalledTimes(1);
  });

  it('should not focus the host element when disabled', async () => {
    fixture.componentInstance.enabled = false;
    const input = fixture.debugElement.query(By.directive(AutofocusDirective)).nativeElement as HTMLInputElement;
    const focusSpy = spyOn(input, 'focus');

    fixture.detectChanges();
    await fixture.whenStable();

    expect(focusSpy).not.toHaveBeenCalled();
  });
});
