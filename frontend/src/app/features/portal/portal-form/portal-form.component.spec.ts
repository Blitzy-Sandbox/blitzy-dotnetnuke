import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PortalFormComponent } from './portal-form.component';
import { PortalService } from '../portal.service';

/**
 * Unit spec for {@link PortalFormComponent} — focused on the migrated client-side
 * validation parity (review finding M2).
 *
 * MIGRATION: the legacy SiteSettings.ascx edit screen enforced exactly two client-side
 * data-type validators — `valExpiryDate` (CompareValidator Type=Date DataTypeCheck →
 * "Invalid expiry date!") and `valHostFee` (CompareValidator Type=Currency DataTypeCheck →
 * "Invalid fee, needs to be a currency value!"). These tests assert the reactive-form
 * equivalents attached to the `expiryDate` / `hostFee` controls behave identically: a
 * non-empty invalid value is flagged, and a valid (or empty) value passes.
 *
 * The component is exercised in CREATE mode (the default {@link provideRouter} supplies an
 * ActivatedRoute whose `paramMap.get('id')` is null, so no portal is loaded and
 * {@link PortalService} is never called at init). The two validators are attached at form
 * construction regardless of mode, so the control state can be asserted directly. Only
 * `PortalService` is mocked (`jasmine.SpyObj`); no real HTTP. Contributes to Gate 4.
 */
describe('PortalFormComponent (M2 validation parity)', () => {
  let spy: jasmine.SpyObj<PortalService>;

  beforeEach(() => {
    spy = jasmine.createSpyObj<PortalService>('PortalService', [
      'list',
      'getById',
      'create',
      'update',
      'remove',
    ]);

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: spy },
        provideRouter([]),
      ],
    });
  });

  it('should create in create mode without loading a portal', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.isEdit).toBeFalse();
    expect(spy.getById).not.toHaveBeenCalled();
  });

  // MIGRATION: valExpiryDate — "Invalid expiry date!".
  it('flags a non-empty non-date expiry value as invalidDate', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    fixture.detectChanges();
    const expiry = fixture.componentInstance.form.controls.expiryDate;

    expiry.setValue('not-a-real-date');
    expect(expiry.hasError('invalidDate')).toBeTrue();
  });

  it('accepts a valid date and an empty expiry value', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    fixture.detectChanges();
    const expiry = fixture.componentInstance.form.controls.expiryDate;

    expiry.setValue('2099-12-31');
    expect(expiry.hasError('invalidDate')).toBeFalse();

    // Empty is left to optionality (the legacy DataTypeCheck did not enforce presence).
    expiry.setValue('');
    expect(expiry.hasError('invalidDate')).toBeFalse();
  });

  // MIGRATION: valHostFee — "Invalid fee, needs to be a currency value!".
  it('flags a non-numeric host fee as invalidCurrency', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    fixture.detectChanges();
    const hostFee = fixture.componentInstance.form.controls.hostFee;

    // A NaN value models a non-currency entry; the validator must reject it.
    hostFee.setValue(NaN);
    expect(hostFee.hasError('invalidCurrency')).toBeTrue();
  });

  it('accepts a finite numeric host fee (no invented non-negative/range rule)', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    fixture.detectChanges();
    const hostFee = fixture.componentInstance.form.controls.hostFee;

    hostFee.setValue(19.99);
    expect(hostFee.hasError('invalidCurrency')).toBeFalse();

    // Parity: the legacy Currency DataTypeCheck did NOT forbid negatives, so a negative
    // amount is a valid currency value here too.
    hostFee.setValue(-5);
    expect(hostFee.hasError('invalidCurrency')).toBeFalse();
  });
});
