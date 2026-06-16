// Karma/Jasmine unit suite for PortalFormComponent (./portal-form.component).
//
// Satisfies Gate 4 (`ng test --watch=false --browsers=ChromeHeadless` -> 100% pass) and Gate 3
// (Angular-19-strict type-check, NO `any`). It verifies the two-mode (create/edit) behavior, the
// validation gate, the FULL-REQUEST construction, and RFC 7807 server-error mapping that the
// component carries over from the legacy DNN editor (Website/admin/Portal/SiteSettings.ascx.vb):
//   - currency default 'USD'              (legacy cboCurrency fallback, L324-327)
//   - submit gated by a valid form        (legacy cmdUpdate_Click gated by Page.IsValid, L688)
//   - blank expiry normalized to null     (legacy txtExpiryDate "" -> Null.NullDate, L729-732)
//
// DESIGN DECISION (per the file's mandate): these specs DELIBERATELY never call
// `fixture.detectChanges()`. Rendering the template would instantiate `HasPermissionDirective`
// (which injects AuthService -> ApiService -> HttpClient) plus the child standalone components,
// dragging heavy, irrelevant dependencies into a unit test. Instead we drive the component class
// directly through its public methods (`ngOnInit()`, `onSubmit()`) and assert via its public
// signals/getters. This keeps the suite a true, isolated unit test with a minimal provider set
// (PortalService + Router + ActivatedRoute), so AuthService/ApiService/HttpClient are NOT mocked.
//
// RECONCILIATION NOTES (the real dependency files are the source of truth):
//   1. `PortalFormComponent` exposes NO `guid()` accessor; the loaded portal's GUID is retained
//      privately and re-emitted only through the PUT body. The edit-load spec therefore asserts the
//      observable public surface (portalId()/isEditMode()/controls/loading()), and the guid-retention
//      intent is verified in the edit-submit spec by asserting the loaded `guid` carries over into the
//      UpdatePortalRequest. (Mirrors the sibling module-form.component.spec.ts precedent.)
//   2. The real `Portal` interface has 37 fields: `processorPassword` is INTENTIONALLY ABSENT from
//      the read model (write-only credential, never projected by the API), so `buildPortal` omits it.
//
// Because the `of(...)` stubs are synchronous, the `subscribe` callbacks run inline during
// `ngOnInit()`/`onSubmit()`; assertions are made immediately after the call (no fakeAsync/tick).
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PortalFormComponent } from './portal-form.component';
import { PortalService } from '../../services';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../../models';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Builds a COMPLETE, fully-typed `Portal` read-model fixture (all 37 wire fields present, no `any`).
 * Field names, types, and nullability mirror the REAL `../../models` `Portal` interface exactly:
 * id/number fields are `number` (some `number | null`), string fields are `string | null`, the GUID is
 * a non-null `string`, and dates are ISO `string | null`. `processorPassword` is deliberately NOT a
 * member of `Portal` (write-only on the API) and is therefore not present here. `Partial<Portal>`
 * overrides are spread last so a single spec tweaks only the fields it asserts on while every required
 * member stays populated.
 */
function buildPortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalID: 1,
    portalName: 'Test Portal',
    logoFile: null,
    footerText: null,
    expiryDate: null,
    userRegistration: 0,
    bannerAdvertising: 0,
    administratorId: 1,
    currency: 'USD',
    hostFee: 0,
    hostSpace: 0,
    pageQuota: 0,
    userQuota: 0,
    administratorRoleId: 0,
    administratorRoleName: null,
    registeredRoleId: 0,
    registeredRoleName: null,
    description: null,
    keyWords: null,
    backgroundFile: null,
    guid: '11111111-1111-1111-1111-111111111111',
    paymentProcessor: null,
    processorUserId: null,
    siteLogHistory: 0,
    email: null,
    adminTabId: 0,
    superTabId: 0,
    users: null,
    pages: null,
    splashTabId: 0,
    homeTabId: 0,
    loginTabId: 0,
    userTabId: 0,
    defaultLanguage: null,
    timeZoneOffset: 0,
    homeDirectory: null,
    version: null,
    ...overrides,
  };
}

describe('PortalFormComponent', () => {
  let portalServiceSpy: jasmine.SpyObj<PortalService>;
  let routerSpy: jasmine.SpyObj<Router>;
  // Mutable holder backing the ActivatedRoute stub's `paramMap` getter. A spec arranges create
  // (`{}`) or edit (`{ id: '5' }`) mode by reassigning this BEFORE `createComponent()` + `ngOnInit()`.
  let routeParams: Params;

  // Instantiates the standalone component WITHOUT change detection (see the file header rationale):
  // `ngOnInit()` is invoked MANUALLY by each spec once `routeParams` reflects the desired branch.
  function createComponent(): PortalFormComponent {
    return TestBed.createComponent(PortalFormComponent).componentInstance;
  }

  beforeEach(() => {
    routeParams = {};
    portalServiceSpy = jasmine.createSpyObj<PortalService>('PortalService', [
      'getPortal',
      'createPortal',
      'updatePortal',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      // Standalone component -> add to `imports`, NOT `declarations`.
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: portalServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          // Getter-based `paramMap` so reassigning `routeParams` before `ngOnInit()` switches modes.
          // `convertToParamMap` builds a real `ParamMap`, exactly as the router would at runtime.
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get paramMap() {
                return convertToParamMap(routeParams);
              },
            },
          },
        },
      ],
    });
  });

  it('initializes create mode with the legacy "USD" currency default', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(component.isEditMode()).toBeFalse();
    // Create mode must not fetch an existing portal.
    expect(portalServiceSpy.getPortal).not.toHaveBeenCalled();
    // MIGRATION: legacy cboCurrency "USD" fallback (SiteSettings.ascx.vb L324-327).
    expect(component.controls.currency.value).toBe('USD');
  });

  it('submits a fully-populated 35-field CreatePortalRequest and navigates on create', () => {
    routeParams = {};
    portalServiceSpy.createPortal.and.returnValue(of(buildPortal()));

    const component = createComponent();
    component.ngOnInit();
    component.controls.portalName.setValue('My Portal');
    component.onSubmit();

    expect(portalServiceSpy.createPortal).toHaveBeenCalledTimes(1);
    const request: CreatePortalRequest = portalServiceSpy.createPortal.calls.mostRecent().args[0];
    expect(request.portalName).toBe('My Portal');
    expect(request.currency).toBe('USD');
    // MIGRATION: create sends the placeholder GUID; the server assigns the real one.
    expect(request.guid).toBe('00000000-0000-0000-0000-000000000000');
    // A non-edited field defaulted by buildCreateRequest (the server assigns its real value).
    expect(request.hostFee).toBe(0);
    // The full create contract: 15 edited fields + 20 explicit defaults = every required key present.
    expect(Object.keys(request).length).toBe(35);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    expect(component.saving()).toBeFalse();
  });

  it('loads the existing portal and patches the form in edit mode', () => {
    routeParams = { id: '5' };
    portalServiceSpy.getPortal.and.returnValue(
      of(buildPortal({ portalID: 5, portalName: 'Existing', guid: 'abc' })),
    );

    const component = createComponent();
    component.ngOnInit();

    expect(portalServiceSpy.getPortal).toHaveBeenCalledWith(5);
    expect(component.isEditMode()).toBeTrue();
    expect(component.portalId()).toBe(5);
    expect(component.controls.portalName.value).toBe('Existing');
    // Synchronous `of(...)` load resolves inline, so the loading flag is already cleared.
    expect(component.loading()).toBeFalse();
  });

  it('submits an UpdatePortalRequest with the route id that preserves non-edited loaded fields', () => {
    routeParams = { id: '5' };
    // Load with a distinctive guid + the default hostFee (0) so both carry-overs are observable.
    portalServiceSpy.getPortal.and.returnValue(
      of(buildPortal({ portalID: 5, guid: 'abc' })),
    );
    portalServiceSpy.updatePortal.and.returnValue(of(buildPortal({ portalID: 5 })));

    const component = createComponent();
    component.ngOnInit();
    component.controls.portalName.setValue('Updated');
    component.onSubmit();

    const [idArg, requestArg]: [number, UpdatePortalRequest] =
      portalServiceSpy.updatePortal.calls.mostRecent().args;
    expect(idArg).toBe(5);
    // MIGRATION: the PUT body's portalID is forced to the route :id (UpdatePortalValidator: PortalID > 0).
    expect(requestArg.portalID).toBe(5);
    expect(requestArg.portalName).toBe('Updated');
    // `...loaded` spread carried the non-edited fields through to the update payload, including the
    // loaded GUID (which the component never exposes via a public accessor) and hostFee.
    expect(requestArg.hostFee).toBe(0);
    expect(requestArg.guid).toBe('abc');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
  });

  it('blocks the service call when the form is invalid', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();
    // portalName carries the only client validator (required), matching the legacy
    // RequiredFieldValidator on txtPortalName and the backend Create/UpdatePortalValidator.
    component.controls.portalName.setValue(null);
    component.onSubmit();

    expect(component.form.invalid).toBeTrue();
    expect(portalServiceSpy.createPortal).not.toHaveBeenCalled();
  });

  it('maps RFC 7807 ProblemDetails errors into serverErrors on create failure', () => {
    routeParams = {};
    const problem: ProblemDetails = { errors: { portalName: ['Portal name already exists.'] } };
    portalServiceSpy.createPortal.and.returnValue(throwError(() => problem));

    const component = createComponent();
    component.ngOnInit();
    component.controls.portalName.setValue('My Portal');
    component.onSubmit();

    expect(component.serverErrors()).toEqual({ portalName: ['Portal name already exists.'] });
    expect(component.saving()).toBeFalse();
  });

  it('normalizes a blank expiry date to null in the create request', () => {
    routeParams = {};
    portalServiceSpy.createPortal.and.returnValue(of(buildPortal()));

    const component = createComponent();
    component.ngOnInit();
    component.controls.portalName.setValue('My Portal');
    // MIGRATION: blank expiry -> null (legacy txtExpiryDate "" -> Null.NullDate, L729-732).
    component.controls.expiryDate.setValue('');
    component.onSubmit();

    const request: CreatePortalRequest = portalServiceSpy.createPortal.calls.mostRecent().args[0];
    expect(request.expiryDate).toBeNull();
  });
});
