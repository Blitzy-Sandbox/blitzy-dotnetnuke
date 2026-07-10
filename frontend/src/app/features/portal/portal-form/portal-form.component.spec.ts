import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PortalFormComponent } from './portal-form.component';
import { PortalService } from '../portal.service';
import { Portal, ProblemDetails } from '../../../core/models';

/**
 * Unit spec for {@link PortalFormComponent} -- focused on the migrated client-side
 * validation parity (review finding M2).
 *
 * MIGRATION: the legacy SiteSettings.ascx edit screen enforced exactly two client-side
 * data-type validators -- `valExpiryDate` (CompareValidator Type=Date DataTypeCheck ->
 * "Invalid expiry date!") and `valHostFee` (CompareValidator Type=Currency DataTypeCheck ->
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

  // MIGRATION: valExpiryDate -- "Invalid expiry date!".
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

  // MIGRATION: valHostFee -- "Invalid fee, needs to be a currency value!".
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
 * Runtime regression spec for QA finding F2 (CRITICAL) -- the Portal edit form's
 * "User Registration" and "Banner Advertising" <select> dropdowns rendered BLANK.
 *
 * ROOT CAUSE (backend): a global `JsonStringEnumConverter` in Program.cs serialized the
 * `UserRegistrationType` / `BannerType` enums as STRING NAMES ("PublicRegistration", "Banner").
 * The <select> options (form-field.component.ts L119: `<option [value]="option.value">`) carry
 * INTEGER codes (0/1/2/3), so Angular's SelectControlValueAccessor -- which matches by string
 * comparison of the written value -- found no option equal to "PublicRegistration" and fell back
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

/**
 * Builds a Portal fixture as the FIXED backend serializes it (integer enum codes),
 * with every field {@link PortalFormComponent.loadPortal} patches present. The cast
 * keeps the fixture concise while satisfying the read model (mirrors the F2 spec).
 */
function buildPortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalID: 1,
    portalName: 'Primary Site',
    logoFile: '',
    footerText: '',
    expiryDate: '',
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
    ...overrides,
  } as unknown as Portal;
}

/**
 * QA Report 10 Issue 3 (INFO, coverage): portal-form sat at ~55% with ~37% of its
 * functions unexercised. These specs cover the CREATE submit path (POST body shape +
 * navigation), the invalid-form early return, cancel, the null-id delete guard, the
 * portalName / administrator-credential validators, and the three RFC 7807
 * error-extraction branches -- bringing the form to depth parity with its sibling
 * forms. Only PortalService is mocked; Router.navigate is spied. Contributes to Gate 4.
 */
describe('PortalFormComponent (create-mode submit, cancel, validation & error handling)', () => {
  let spy: jasmine.SpyObj<PortalService>;
  let component: PortalFormComponent;
  let navigateSpy: jasmine.Spy;

  /** Populates the create-mode required fields (portalName + administrator account) with valid values. */
  function fillValidCreateForm(): void {
    component.form.patchValue({
      portalName: 'Test Portal',
      portalAlias: 'testalias',
      firstName: 'Test',
      lastName: 'Admin',
      username: 'testadmin',
      password: 'Passw0rd!',
      email: 'admin@example.com',
    });
  }

  beforeEach(() => {
    spy = jasmine.createSpyObj<PortalService>('PortalService', [
      'list',
      'getById',
      'create',
      'update',
      'remove',
    ]);
    // Default happy-path return so a valid submit never hits an undefined observable.
    spy.create.and.returnValue(of(buildPortal()));

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [{ provide: PortalService, useValue: spy }, provideRouter([])],
    });

    const fixture = TestBed.createComponent(PortalFormComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    // ngOnInit (create mode) attaches the Signup.ascx credential validators.
    fixture.detectChanges();
  });

  it('submits a POST body and navigates to the list on success', () => {
    const created = buildPortal();
    spy.create.and.returnValue(of(created));
    fillValidCreateForm();

    component.save();

    expect(spy.update).not.toHaveBeenCalled();
    expect(spy.create).toHaveBeenCalledTimes(1);
    const body = spy.create.calls.mostRecent().args[0];
    expect(body).toEqual({
      portalName: 'Test Portal',
      firstName: 'Test',
      lastName: 'Admin',
      username: 'testadmin',
      password: 'Passw0rd!',
      email: 'admin@example.com',
      description: '',
      keyWords: '',
      homeDirectory: '',
      portalAlias: 'testalias',
    });
    expect(navigateSpy).toHaveBeenCalledWith(['/portals']);
  });

  it('on create error clears saving and surfaces the message', () => {
    spy.create.and.returnValue(
      throwError(
        () =>
          ({
            type: 'about:blank',
            title: 'Bad Request',
            status: 400,
            detail: 'Portal name taken.',
          }) as ProblemDetails
      )
    );
    fillValidCreateForm();

    component.save();

    expect(component.saving()).toBeFalse();
    expect(component.error()).toBe('Portal name taken.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('does not call the service when the form is invalid (early return + markAllAsTouched)', () => {
    // portalName + the create credentials are blank -> the form is invalid.
    component.save();

    expect(spy.create).not.toHaveBeenCalled();
    expect(spy.update).not.toHaveBeenCalled();
    expect(component.form.touched).toBeTrue();
  });

  it('cancel() navigates back to the list without calling the service', () => {
    component.cancel();

    expect(navigateSpy).toHaveBeenCalledWith(['/portals']);
    expect(spy.remove).not.toHaveBeenCalled();
  });

  it('confirmDelete() is a no-op in create mode (null id guard)', () => {
    component.confirmDelete();

    expect(spy.remove).not.toHaveBeenCalled();
  });

  it('validates portalName: required and maxLength(128)', () => {
    const c = component.form.controls.portalName;

    c.setValue('');
    expect(c.hasError('required')).toBeTrue();

    c.setValue('a'.repeat(129));
    expect(c.hasError('maxlength')).toBeTrue();

    c.setValue('Valid Portal Name');
    expect(c.valid).toBeTrue();
  });

  it('activates the administrator credential validators (required + email) in create mode', () => {
    const email = component.form.controls.email;
    email.setValue('');
    expect(email.hasError('required')).toBeTrue();
    email.setValue('not-an-email');
    expect(email.hasError('email')).toBeTrue();
    email.setValue('admin@example.com');
    expect(email.valid).toBeTrue();

    const username = component.form.controls.username;
    username.setValue('');
    expect(username.hasError('required')).toBeTrue();
    username.setValue('admin');
    expect(username.valid).toBeTrue();
  });

  it('extractError: joins field-level errors from a ProblemDetails.errors map', () => {
    spy.create.and.returnValue(
      throwError(
        () =>
          ({
            type: 'about:blank',
            title: 'Validation',
            status: 400,
            errors: { portalName: ['Required.'], email: ['Invalid.'] },
          }) as ProblemDetails
      )
    );
    fillValidCreateForm();

    component.save();

    expect(component.error()).toBe('Required. Invalid.');
  });

  it('extractError: falls back to detail when there are no field errors', () => {
    spy.create.and.returnValue(
      throwError(
        () =>
          ({ type: 'about:blank', title: 'Server Error', status: 500, detail: 'Boom' }) as ProblemDetails
      )
    );
    fillValidCreateForm();

    component.save();

    expect(component.error()).toBe('Boom');
  });

  it('extractError: falls back to title when there is neither errors nor detail', () => {
    spy.create.and.returnValue(
      throwError(() => ({ type: 'about:blank', title: 'Conflict', status: 409 }) as ProblemDetails)
    );
    fillValidCreateForm();

    component.save();

    expect(component.error()).toBe('Conflict');
  });

  it('extractError: uses the generic fallback when errors, detail and title are all absent', () => {
    // A degenerate server error carrying neither field errors, a detail, nor a title.
    // The user must still receive a message, so the component falls back to the generic string.
    spy.create.and.returnValue(throwError(() => ({ status: 500 }) as unknown as ProblemDetails));
    fillValidCreateForm();

    component.save();

    expect(component.error()).toBe('An unexpected error occurred.');
  });

  it('hostFee accepts an empty (cleared) value -- currency validator optionality parity', () => {
    // MIGRATION parity: legacy valHostFee only rejected non-currency INPUT; a cleared
    // numeric input (null/'' at runtime) must pass, matching the legacy DataTypeCheck.
    const hostFee = component.form.controls.hostFee;

    hostFee.setValue('' as unknown as number);
    expect(hostFee.hasError('invalidCurrency')).toBeFalse();
    expect(hostFee.valid).toBeTrue();

    // A non-currency value is still rejected (the other side of the same branch).
    hostFee.setValue('abc' as unknown as number);
    expect(hostFee.hasError('invalidCurrency')).toBeTrue();
  });
});

/**
 * QA Report 10 Issue 3 -- EDIT-mode branches: load+patch (incl. ISO expiryDate ->
 * date-input conversion), the PUT submit path, delete (open dialog + confirm), and
 * the update/delete error handlers. ActivatedRoute is stubbed with a numeric id so
 * the component enters edit mode; getById returns synchronously.
 */
describe('PortalFormComponent (edit-mode actions)', () => {
  let spy: jasmine.SpyObj<PortalService>;
  let component: PortalFormComponent;
  let navigateSpy: jasmine.Spy;
  const editPortal = buildPortal({
    portalID: 1,
    portalName: 'Primary Site',
    expiryDate: '2099-12-31T00:00:00',
  });

  beforeEach(() => {
    spy = jasmine.createSpyObj<PortalService>('PortalService', [
      'list',
      'getById',
      'create',
      'update',
      'remove',
    ]);
    spy.getById.and.returnValue(of(editPortal));
    spy.update.and.returnValue(of(editPortal));
    spy.remove.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: spy },
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } },
        },
      ],
    });

    const fixture = TestBed.createComponent(PortalFormComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.returnValue(Promise.resolve(true));
    // ngOnInit -> loadPortal (synchronous of) -> patchValue.
    fixture.detectChanges();
  });

  it('loads the portal and converts the ISO expiryDate to a date-input value', () => {
    expect(component.isEdit).toBeTrue();
    expect(spy.getById).toHaveBeenCalledWith(1);
    expect(component.loading()).toBeFalse();
    expect(component.form.controls.expiryDate.value).toBe('2099-12-31');
    expect(component.form.controls.portalName.value).toBe('Primary Site');
  });

  it('submits a PUT with the route id and navigates on success', () => {
    component.save();

    expect(spy.create).not.toHaveBeenCalled();
    expect(spy.update).toHaveBeenCalledTimes(1);
    const [id, body] = spy.update.calls.mostRecent().args;
    expect(id).toBe(1);
    expect(body.portalName).toBe('Primary Site');
    expect(navigateSpy).toHaveBeenCalledWith(['/portals']);
  });

  it('on update error clears saving and surfaces the message', () => {
    spy.update.and.returnValue(
      throwError(() => ({ type: 'about:blank', title: 'Bad', status: 400, detail: 'Nope' }) as ProblemDetails)
    );

    component.save();

    expect(component.saving()).toBeFalse();
    expect(component.error()).toBe('Nope');
  });

  it('requestDelete() opens the confirmation dialog', () => {
    expect(component.showDeleteDialog()).toBeFalse();

    component.requestDelete();

    expect(component.showDeleteDialog()).toBeTrue();
  });

  it('confirmDelete() deletes the portal and navigates on success', () => {
    component.confirmDelete();

    expect(spy.remove).toHaveBeenCalledWith(1);
    expect(navigateSpy).toHaveBeenCalledWith(['/portals']);
  });

  it('confirmDelete() on error clears saving and surfaces the message', () => {
    spy.remove.and.returnValue(
      throwError(
        () => ({ type: 'about:blank', title: 'Server Error', status: 500, detail: 'Fail' }) as ProblemDetails
      )
    );

    component.confirmDelete();

    expect(component.saving()).toBeFalse();
    expect(component.error()).toBe('Fail');
  });
});

/**
 * QA Report 10 Issue 3 -- the edit-mode LOAD FAILURE branch: when getById errors,
 * loadPortal must surface the message and stop the loading spinner.
 */
describe('PortalFormComponent (edit-mode load failure)', () => {
  it('surfaces an error and stops loading when the portal fails to load', () => {
    const spy = jasmine.createSpyObj<PortalService>('PortalService', [
      'list',
      'getById',
      'create',
      'update',
      'remove',
    ]);
    spy.getById.and.returnValue(
      throwError(
        () =>
          ({
            type: 'about:blank',
            title: 'Not Found',
            status: 404,
            detail: 'Portal not found.',
          }) as ProblemDetails
      )
    );

    TestBed.configureTestingModule({
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: spy },
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } },
        },
      ],
    });

    const fixture = TestBed.createComponent(PortalFormComponent);
    // ngOnInit -> loadPortal -> getById throwError.
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(spy.getById).toHaveBeenCalledWith(7);
    expect(component.loading()).toBeFalse();
    expect(component.error()).toBe('Portal not found.');
  });
});
