/**
 * ModuleFormComponent — Gate 4 Karma/Jasmine unit tests.
 *
 * These specs prove that the standalone Angular 19 `ModuleFormComponent` re-expresses the
 * legacy DNN `Website/admin/Modules/ModuleSettings.ascx.vb` behavior faithfully (UI functional
 * parity, AAP §0.7.1):
 *
 *   - CREATE mode initializes the DNN defaults from `Page_Load` L225-227
 *       (cboVisibility.SelectedIndex = 0 -> visibility=0 "Maximized"; chkAllTabs.Checked = False;
 *        cmdDelete hidden), plus cacheTime=0 / moduleOrder=0 / booleans false.
 *   - EDIT mode calls `getModule(id)` and patches the form (mirrors `BindData` L85-169), including
 *       the ISO -> yyyy-MM-dd date transform and the display-only friendly name.
 *   - Required-title validation blocks submit (ModuleTitle is a core non-optional field).
 *   - The cross-field date-range validator flags `start > end`.
 *   - Submit POSTs (create) / PUTs (edit) a correctly-shaped body and navigates to `/modules`
 *       (mirrors `cmdUpdate_Click` -> POST 201 / PUT 200); the update body omits the immutable
 *       placement identity (portalID/tabID/moduleDefID).
 *   - Delete confirm calls `deleteModule` and navigates (mirrors `cmdDelete_Click` -> DELETE 204).
 *
 * The component is standalone + OnPush and reads its mode from the `:id` route param during
 * `ngOnInit`, so each test configures a mock `ActivatedRoute.snapshot.paramMap` (via
 * `convertToParamMap`) BEFORE the first `fixture.detectChanges()`. All injected collaborators
 * (`ModuleService`, `Router`) are mocked as typed Jasmine spies; `ModuleService` methods return
 * `of(...)` so subscriptions complete synchronously and assertions can run immediately after
 * `onSubmit()` / `onDeleteConfirmed()` (no `fakeAsync`/`tick`). `NonNullableFormBuilder` is
 * provided automatically by Angular's `ReactiveFormsModule` platform providers via `TestBed`.
 */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of } from 'rxjs';

import { Module } from '../../../core/models';
import { ModuleService } from '../module.service';
import { ModuleFormComponent } from './module-form.component';

describe('ModuleFormComponent', () => {
  let moduleServiceSpy: jasmine.SpyObj<ModuleService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let fixture: ComponentFixture<ModuleFormComponent>;
  let component: ModuleFormComponent;

  /**
   * A fully-populated Module used for edit-mode load assertions. Field names carry the legacy
   * all-caps `ID` suffixes exactly as emitted by the backend `System.Text.Json` camelCase policy,
   * and every required property of the `Module` contract is present.
   */
  const mockModule: Module = {
    portalID: 1,
    tabID: 7,
    tabModuleID: 22,
    moduleID: 5,
    moduleDefID: 3,
    moduleOrder: 2,
    paneName: 'ContentPane',
    moduleTitle: 'My Module',
    cacheTime: 120,
    alignment: 'left',
    color: '#fff',
    border: '0',
    iconFile: 'icon.png',
    allTabs: false,
    visibility: 1,
    header: 'H',
    footer: 'F',
    startDate: '2024-01-15T00:00:00',
    endDate: '2024-02-20T00:00:00',
    containerSrc: 'container.ascx',
    displayTitle: true,
    displayPrint: false,
    displaySyndicate: true,
    inheritViewPermissions: true,
    desktopModuleID: 9,
    friendlyName: 'My Friendly Module',
    folderName: 'MyModule',
    description: 'desc',
    version: '1.0.0',
    moduleName: 'MyModule',
    controlSrc: 'view.ascx',
  };

  /**
   * (Re)initialize the mocked collaborators before each test. `ModuleService` methods emit
   * synchronously via `of(...)`; `deleteModule` yields `Observable<void>`. The `Router.navigate`
   * spy returns a resolved promise (its return value is never awaited by the component).
   */
  function setup(): void {
    moduleServiceSpy = jasmine.createSpyObj<ModuleService>('ModuleService', [
      'getModule',
      'createModule',
      'updateModule',
      'deleteModule',
    ]);
    moduleServiceSpy.getModule.and.returnValue(of(mockModule));
    moduleServiceSpy.createModule.and.returnValue(of(mockModule));
    moduleServiceSpy.updateModule.and.returnValue(of(mockModule));
    moduleServiceSpy.deleteModule.and.returnValue(of(void 0));

    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));
  }

  /**
   * Build the component with a specific `:id` route param. Passing `null` (absent param) yields
   * CREATE mode; a numeric string yields EDIT mode. The `ActivatedRoute` mock is configured BEFORE
   * `TestBed.createComponent`, so `ngOnInit` (run by the first `detectChanges()`) reads the correct
   * mode and — in edit mode — triggers the `getModule` fetch + `patchValue`.
   */
  function createComponent(idParam: string | null): void {
    const paramMap = convertToParamMap(idParam === null ? {} : { id: idParam });
    TestBed.configureTestingModule({
      imports: [ModuleFormComponent],
      providers: [
        { provide: ModuleService, useValue: moduleServiceSpy },
        { provide: Router, useValue: routerSpy },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap } } },
      ],
    });
    fixture = TestBed.createComponent(ModuleFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers ngOnInit -> mode detection (+ edit-mode load)
  }

  beforeEach(() => {
    setup();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('creates', () => {
    createComponent(null);
    expect(component).toBeTruthy();
  });

  it('CREATE mode: initializes DNN defaults (visibility=0, allTabs=false, cacheTime=0, moduleOrder=0)', () => {
    createComponent(null);
    expect(component.isEditMode()).toBe(false);
    const raw = component.form.getRawValue();
    expect(raw.visibility).toBe(0); // Maximized (Page_Load L225)
    expect(raw.allTabs).toBe(false); // chkAllTabs.Checked = False (L226)
    expect(raw.cacheTime).toBe(0);
    expect(raw.moduleOrder).toBe(0);
    expect(moduleServiceSpy.getModule).not.toHaveBeenCalled();
  });

  it('EDIT mode: calls getModule and patches the form', () => {
    createComponent('5');
    expect(component.isEditMode()).toBe(true);
    expect(moduleServiceSpy.getModule).toHaveBeenCalledWith(5);
    const raw = component.form.getRawValue();
    expect(raw.moduleTitle).toBe('My Module');
    expect(raw.visibility).toBe(1);
    expect(raw.startDate).toBe('2024-01-15'); // ISO 'yyyy-MM-ddTHH:mm:ss' -> 'yyyy-MM-dd'
    expect(raw.endDate).toBe('2024-02-20');
    expect(component.friendlyName()).toBe('My Friendly Module');
  });

  it('EDIT mode: coerces a null alignment to the "" ("Not Specified") option', () => {
    // QA finding (unset alignment showed blank): an unset alignment arrives on the wire as null; the
    // form must map it to the '' option so the control renders "Not Specified" rather than an empty
    // selection. Assert the alignment control resolves to '' (which matches alignmentOptions[0]).
    moduleServiceSpy.getModule.and.returnValue(of({ ...mockModule, alignment: null }));
    createComponent('5');
    expect(component.isEditMode()).toBe(true);
    expect(component.form.controls.alignment.value).toBe('');
  });

  it('required title blocks submit (no create call when title empty)', () => {
    createComponent(null);
    // Valid placement identity but an empty title -> form is invalid.
    component.form.patchValue({ portalID: 1, tabID: 7, moduleDefID: 3, moduleTitle: '' });
    component.onSubmit();
    expect(component.form.controls.moduleTitle.invalid).toBe(true);
    expect(moduleServiceSpy.createModule).not.toHaveBeenCalled();
  });

  it('date-range validator flags start > end', () => {
    createComponent(null);
    component.form.patchValue({ startDate: '2024-05-10', endDate: '2024-05-01' });
    expect(component.form.errors?.['dateRange']).toBeTrue();
  });

  it('CREATE submit: calls createModule with correctly-shaped body and navigates to /modules', () => {
    createComponent(null);
    component.form.patchValue({
      portalID: 1,
      tabID: 7,
      moduleDefID: 3,
      moduleTitle: 'New Module',
      paneName: 'ContentPane',
      visibility: 0,
    });
    component.onSubmit();
    expect(moduleServiceSpy.createModule).toHaveBeenCalledTimes(1);
    const body = moduleServiceSpy.createModule.calls.mostRecent().args[0];
    expect(body.portalID).toBe(1);
    expect(body.tabID).toBe(7);
    expect(body.moduleDefID).toBe(3);
    expect(body.moduleTitle).toBe('New Module');
    expect(body.visibility).toBe(0);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
  });

  // QA R10 Issue 4 + P6-2: the placement identity controls (portalID/tabID/moduleDefID) are foreign-key
  // references whose default value 0 satisfies Validators.required (0 is "present"), so an invalid
  // reference could be submitted silently. The min floors MUST mirror the authoritative backend
  // FluentValidation contract (ModuleValidator):
  //   * PortalID is ZERO-BASED (DNN's first/default portal is PortalID 0; backend GreaterThanOrEqualTo(0))
  //     -> Validators.min(0): 0 is VALID, only a negative value is rejected. (P6-2: min(1) previously and
  //     WRONGLY rejected the valid default portal 0, blocking a legitimate create submit.)
  //   * TabID / ModuleDefID reference IDENTITY(1,1) rows (backend GreaterThan(0)) -> Validators.min(1):
  //     0 (and negatives) rejected.
  it('CREATE mode: enforces the backend-aligned identity floors (portalID>=0, tabID/moduleDefID>=1)', () => {
    createComponent(null);
    // A complete, otherwise-valid form with all identity IDs at the DNN default 0.
    component.form.patchValue({
      portalID: 0,
      tabID: 0,
      moduleDefID: 0,
      moduleTitle: 'New Module',
      paneName: 'ContentPane',
      visibility: 0,
    });
    // portalID 0 is a VALID (zero-based) reference — no min error.
    expect(component.form.controls.portalID.hasError('min')).toBe(false);
    // tabID / moduleDefID 0 are INVALID — they must be >= 1.
    expect(component.form.controls.tabID.hasError('min')).toBe(true);
    expect(component.form.controls.moduleDefID.hasError('min')).toBe(true);
    expect(component.form.invalid).toBe(true);

    // A negative portalID IS rejected (below the zero-based floor).
    component.form.patchValue({ portalID: -1 });
    expect(component.form.controls.portalID.hasError('min')).toBe(true);
    // Restore the valid default portal (0) for the remainder of the test.
    component.form.patchValue({ portalID: 0 });
    expect(component.form.controls.portalID.hasError('min')).toBe(false);

    // Submitting an invalid form (tabID/moduleDefID still 0) must NOT call the service.
    component.onSubmit();
    expect(moduleServiceSpy.createModule).not.toHaveBeenCalled();

    // P6-2 core assertion: the DEFAULT portal (portalID 0) plus valid tab/def references (>= 1) clears
    // ALL min errors and allows submit — the exact case the old min(1) wrongly blocked.
    component.form.patchValue({ portalID: 0, tabID: 7, moduleDefID: 3 });
    expect(component.form.controls.portalID.hasError('min')).toBe(false);
    expect(component.form.controls.tabID.hasError('min')).toBe(false);
    expect(component.form.controls.moduleDefID.hasError('min')).toBe(false);
    expect(component.form.valid).toBe(true);
    component.onSubmit();
    expect(moduleServiceSpy.createModule).toHaveBeenCalledTimes(1);
  });

  it('EDIT submit: calls updateModule (no placement identity in body) and navigates', () => {
    createComponent('5');
    component.form.patchValue({ moduleTitle: 'Renamed' });
    component.onSubmit();
    expect(moduleServiceSpy.updateModule).toHaveBeenCalledTimes(1);
    const [id, body] = moduleServiceSpy.updateModule.calls.mostRecent().args;
    expect(id).toBe(5);
    expect(body.moduleTitle).toBe('Renamed');
    // UpdateModuleRequest must NOT carry placement identity (portalID/tabID/moduleDefID).
    // These keys are absent from the interface, so we index a `Record<string, unknown>` view of the
    // body. The cast goes through `unknown` (not `any`) because `UpdateModuleRequest` has no index
    // signature — a direct `as Record<...>` is rejected by TS2352 under the strict config, and the
    // bracket access satisfies `noPropertyAccessFromIndexSignature`.
    const bodyRecord = body as unknown as Record<string, unknown>;
    expect(bodyRecord['portalID']).toBeUndefined();
    expect(bodyRecord['tabID']).toBeUndefined();
    expect(bodyRecord['moduleDefID']).toBeUndefined();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('delete confirm: calls deleteModule and navigates (edit mode)', () => {
    createComponent('5');
    component.onDeleteConfirmed();
    expect(moduleServiceSpy.deleteModule).toHaveBeenCalledWith(5);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
  });
});
