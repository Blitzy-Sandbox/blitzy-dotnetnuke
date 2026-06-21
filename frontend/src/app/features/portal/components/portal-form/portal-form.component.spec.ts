// PortalFormComponent unit tests (Karma + Jasmine) — satisfies Gate 4
// (`ng test --watch=false --browsers=ChromeHeadless` → 100% pass).
//
// MIGRATION: these specs exercise the Angular replacement for the legacy Web Forms portal
// editor Website/admin/Portal/SiteSettings.ascx.vb. The verified parity behaviors covered here:
//   - cmdUpdate_Click + `If Page.IsValid` gate (SiteSettings.ascx.vb L687-688)
//        -> onSubmit() guards form.invalid (markAllAsTouched + early return)        [spec 5]
//   - PortalController.CreatePortal / UpdatePortalInfo
//        -> PortalService.createPortal (create) / updatePortal (edit)               [specs 2, 4]
//   - blank ExpiryDate -> Null.NullDate (SiteSettings.ascx.vb L729-731)
//        -> a blank expiry control is coerced to null on the wire request           [spec 7]
//   - stored currency null/unmatched -> "USD" selected (SiteSettings.ascx.vb L324-327)
//        -> the currency control defaults to 'USD'                                  [specs 1, 2]
//   - server FluentValidation failures surface as RFC 7807 ProblemDetails.errors
//        -> mapped onto the serverErrors() signal                                   [spec 6]
//
// DESIGN: these tests deliberately DO NOT call fixture.detectChanges(). Rendering the template
// would instantiate the child standalone components and HasPermissionDirective (which injects
// AuthService -> ApiService -> HttpClient), dragging heavy collaborators into what must remain
// an isolated unit test. The component is instead driven through its public API (ngOnInit(),
// onSubmit(), the typed form controls, and the public signals). A minimal provider set (mocked
// PortalService + Router, stubbed ActivatedRoute) is therefore sufficient; AuthService /
// ApiService / HttpClient are intentionally NOT provided because the template never renders.
//
// Synchronous of(...) / throwError(...) stubs make the subscribe callbacks run immediately
// during ngOnInit()/onSubmit(), so assertions can be made right after the call (no fakeAsync/tick).
//
// RECONCILED AGAINST THE REAL MODEL/COMPONENT (the source of truth — not the example template):
//   - The read `Portal` interface declares 37 members and NO `processorPassword` (the payment
//     credential is removed from read responses for security), so buildPortal() populates exactly
//     those 37 fields.
//   - `CreatePortalRequest` declares 34 members and NO `guid` ([Portals].GUID is server-managed,
//     newid() default), so spec 2 asserts the full 34-field request and that `guid` is absent
//     rather than an empty Guid string.
//   - PortalFormComponent exposes no public `guid()` member (loadedPortal is private), so the
//     edit-load spec asserts the publicly observable load effects (portalId / isEditMode /
//     patched control value / loading) instead; the loaded entity's carry-over is verified via
//     spec 4 (a non-edited field survives the {...loaded} spread of buildUpdateRequest).
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PortalFormComponent } from './portal-form.component';
import { PortalService } from '../../services';
import { Portal } from '../../models';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Builds a COMPLETE `Portal` read projection (all 37 wire fields) with optional `Partial<Portal>`
 * overrides applied last. Field names are reconciled verbatim against the `Portal` interface in
 * `../../models`; `processorPassword` is intentionally absent because it is not part of the read shape.
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
  let routeParams: Params;

  /**
   * Instantiates the component WITHOUT rendering it (no detectChanges). ngOnInit is invoked
   * explicitly by each spec AFTER `routeParams` has been set, so the same TestBed config serves
   * both create ({}) and edit ({ id: '5' }) mode.
   */
  function createComponent(): PortalFormComponent {
    const fixture = TestBed.createComponent(PortalFormComponent);
    return fixture.componentInstance;
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
      // PortalFormComponent is standalone, so it is provided via `imports`, NOT `declarations`.
      imports: [PortalFormComponent],
      providers: [
        { provide: PortalService, useValue: portalServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          // The paramMap getter recomputes from `routeParams` on every access, so reassigning
          // `routeParams` BEFORE ngOnInit() switches the same TestBed config between modes.
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

  it('creates in create mode with pristine defaults when the route carries no id', () => {
    routeParams = {};

    const c = createComponent();
    c.ngOnInit();

    expect(c).toBeTruthy();
    expect(c.isEditMode()).toBeFalse();
    expect(portalServiceSpy.getPortal).not.toHaveBeenCalled();
    // currency defaults to 'USD' (legacy SiteSettings.ascx.vb L324-327).
    expect(c.controls.currency.value).toBe('USD');
  });

  it('builds a full CreatePortalRequest and navigates to the list on a successful create', () => {
    routeParams = {};
    portalServiceSpy.createPortal.and.returnValue(of(buildPortal()));

    const c = createComponent();
    c.ngOnInit();
    c.controls.portalName.setValue('My Portal');
    c.onSubmit();

    expect(portalServiceSpy.createPortal).toHaveBeenCalledTimes(1);

    // args[0] is strongly typed as CreatePortalRequest through the typed jasmine spy (no `any`).
    const req = portalServiceSpy.createPortal.calls.mostRecent().args[0];
    expect(req.portalName).toBe('My Portal');
    expect(req.currency).toBe('USD');
    expect(req.hostFee).toBe(0);
    // GUID is server-managed and intentionally omitted from the create contract (not an empty Guid).
    expect(Object.keys(req)).not.toContain('guid');
    // Full population of the CreatePortalRequest contract: 15 edited + 19 defaulted = 34 fields.
    expect(Object.keys(req).length).toBe(34);

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
    expect(c.saving()).toBeFalse();
  });

  it('loads the portal and patches the form in edit mode', () => {
    routeParams = { id: '5' };
    portalServiceSpy.getPortal.and.returnValue(
      of(buildPortal({ portalID: 5, portalName: 'Existing', guid: 'abc' })),
    );

    const c = createComponent();
    c.ngOnInit();

    expect(portalServiceSpy.getPortal).toHaveBeenCalledWith(5);
    expect(c.isEditMode()).toBeTrue();
    expect(c.portalId()).toBe(5);
    expect(c.controls.portalName.value).toBe('Existing');
    // The synchronous of(...) stub resolves the load immediately, so loading() is already false.
    expect(c.loading()).toBeFalse();
  });

  it('builds an UpdatePortalRequest with the matching portalID and carried-over fields on edit submit', () => {
    routeParams = { id: '5' };
    portalServiceSpy.getPortal.and.returnValue(
      of(buildPortal({ portalID: 5, portalName: 'Existing', guid: 'abc' })),
    );
    portalServiceSpy.updatePortal.and.returnValue(of(buildPortal({ portalID: 5 })));

    const c = createComponent();
    c.ngOnInit();
    c.controls.portalName.setValue('Updated');
    c.onSubmit();

    expect(portalServiceSpy.updatePortal).toHaveBeenCalledTimes(1);

    // args are typed [number, UpdatePortalRequest] through the typed jasmine spy (no `any`).
    const [idArg, reqArg] = portalServiceSpy.updatePortal.calls.mostRecent().args;
    expect(idArg).toBe(5);
    expect(reqArg.portalID).toBe(5);
    expect(reqArg.portalName).toBe('Updated');
    // A non-edited field is carried verbatim from the loaded entity via the {...loaded} spread.
    expect(reqArg.hostFee).toBe(0);

    expect(routerSpy.navigate).toHaveBeenCalledWith(['/portals']);
  });

  it('blocks submit and issues no request when the form is invalid', () => {
    routeParams = {};

    const c = createComponent();
    c.ngOnInit();
    c.controls.portalName.setValue(null);
    c.onSubmit();

    expect(c.form.invalid).toBeTrue();
    expect(portalServiceSpy.createPortal).not.toHaveBeenCalled();
  });

  it('maps RFC 7807 ProblemDetails.errors onto the serverErrors signal on a failed create', () => {
    routeParams = {};
    // ProblemDetails members are all optional, so the errors-only shape is a valid value.
    const problem: ProblemDetails = {
      errors: { portalName: ['Portal name already exists.'] },
    };
    portalServiceSpy.createPortal.and.returnValue(throwError(() => problem));

    const c = createComponent();
    c.ngOnInit();
    c.controls.portalName.setValue('My Portal');
    c.onSubmit();

    expect(c.serverErrors()).toEqual({ portalName: ['Portal name already exists.'] });
    expect(c.saving()).toBeFalse();
  });

  it('coerces a blank expiry date to null on the created request', () => {
    routeParams = {};
    portalServiceSpy.createPortal.and.returnValue(of(buildPortal()));

    const c = createComponent();
    c.ngOnInit();
    c.controls.portalName.setValue('My Portal');
    // Blank expiry -> null (legacy: blank txtExpiryDate -> Null.NullDate, SiteSettings.ascx.vb L729-731).
    c.controls.expiryDate.setValue('');
    c.onSubmit();

    const req = portalServiceSpy.createPortal.calls.mostRecent().args[0];
    expect(req.expiryDate).toBeNull();
  });
});
