import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PortalFormComponent } from './portal-form.component';
import { PortalService } from '../portal.service';
import { Portal } from '../../../core/models';

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

/**
 * Runtime regression spec for QA finding F2 (CRITICAL) — the Portal edit form's
 * "User Registration" and "Banner Advertising" <select> dropdowns rendered BLANK.
 *
 * ROOT CAUSE (backend): a global `JsonStringEnumConverter` in Program.cs serialized the
 * `UserRegistrationType` / `BannerType` enums as STRING NAMES ("PublicRegistration", "Banner").
 * The <select> options (form-field.component.ts L119: `<option [value]="option.value">`) carry
 * INTEGER codes (0/1/2/3), so Angular's SelectControlValueAccessor — which matches by string
 * comparison of the written value — found no option equal to "PublicRegistration" and fell back
 * to the empty disabled placeholder (`<option value="" disabled>`), i.e. a BLANK dropdown.
 *
 * FIX: the converter was removed so the API now emits integer enum codes. This spec is the
 * client-side runtime proof: given a portal loaded in EDIT mode with the (now integer) values
 * `userRegistration = 2` and `bannerAdvertising = 1`, the rendered <select> elements select the
 * matching options ("Public" / "Site") rather than the blank placeholder. Runs in ChromeHeadless
 * (Gate 4), so the assertion exercises the real DOM select binding, not just the model.
 */
describe('PortalFormComponent (F2 enum dropdown population)', () => {
  let spy: jasmine.SpyObj<PortalService>;

  // A portal as the FIXED backend now serializes it: enum fields are INTEGER codes.
  // userRegistration = 2 -> "Public"; bannerAdvertising = 1 -> "Site".
  const editPortal = {
    portalID: 1,
    portalName: 'Primary Site',
    logoFile: '',
    footerText: '',
    expiryDate: null,
    userRegistration: 2,
    bannerAdvertising: 1,
    currency: 'USD',
    administratorId: 1,
    hostFee: 0,
    hostSpace: 0,
    pageQuota: 0,
    userQuota: 0,
    description: '',
    keyWords: '',
    backgroundFile: '',
    siteLogHistory: 0,
    splashTabId: 0,
    homeTabId: 0,
    loginTabId: 0,
    userTabId: 0,
    defaultLanguage: 'en-US',
    timeZoneOffset: 0,
    homeDirectory: '',
    administratorRoleId: 0,
    registeredRoleId: 0,
    email: 'admin@example.com',
    adminTabId: 0,
    users: 0,
    pages: 0,
    guid: '00000000-0000-0000-0000-000000000000',
    version: '',
    aliases: [],
  } as unknown as Portal;

  beforeEach(() => {
    spy = jasmine.createSpyObj<PortalService>('PortalService', [
      'list',
      'getById',
      'create',
      'update',
      'remove',
    ]);
    // EDIT mode load returns the integer-enum portal synchronously.
    spy.getById.and.returnValue(of(editPortal));

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: spy },
        provideRouter([]),
        // Force EDIT mode: the component reads route.snapshot.paramMap.get('id').
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } },
        },
      ],
    });
  });

  it('selects the matching option (not the blank placeholder) for both enum dropdowns', () => {
    const fixture = TestBed.createComponent(PortalFormComponent);
    // First CD runs ngOnInit -> loadPortal -> getById (synchronous of) -> patchValue.
    fixture.detectChanges();
    // Second CD flushes the patched control values through the CVA to the DOM <select>.
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.isEdit).toBeTrue();
    expect(spy.getById).toHaveBeenCalledWith(1);

    // The model carries the integer codes...
    expect(component.form.controls.userRegistration.value).toBe(2);
    expect(component.form.controls.bannerAdvertising.value).toBe(1);

    // ...and the rendered DOM selects reflect them (NOT the empty placeholder).
    const selects = fixture.nativeElement.querySelectorAll(
      'select'
    ) as NodeListOf<HTMLSelectElement>;
    expect(selects.length).toBe(2);

    const userRegistrationSelect = selects[0];
    const bannerAdvertisingSelect = selects[1];

    // A blank/unmatched dropdown would report value '' (the disabled placeholder); the fix
    // makes each select resolve to its integer-valued option.
    expect(userRegistrationSelect.value).not.toBe('');
    expect(bannerAdvertisingSelect.value).not.toBe('');
    expect(userRegistrationSelect.selectedOptions[0].textContent?.trim()).toBe('Public');
    expect(bannerAdvertisingSelect.selectedOptions[0].textContent?.trim()).toBe('Site');
  });
});
