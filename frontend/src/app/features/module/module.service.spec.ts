// MIGRATION: Validates the migrated Module data-access gateway
// (frontend/src/app/features/module/module.service.ts), which re-expresses the legacy DNN Admin ->
// Modules user controls -- Website/admin/Modules/ModuleSettings.ascx.vb (module settings + lifecycle),
// Import.ascx.vb (content import) and Export.ascx.vb (content export) -- over the modern REST API. The
// Web Forms postback / ViewState / IPortable-reflection machinery is discarded; this spec asserts the
// surviving CONTRACT: every method issues the correct HTTP verb + RELATIVE url + params/body through the
// REAL ApiService (exercised via the HTTP testing backend) and mutates the service signals
// (AAP Sections 0.7.3 / 0.7.4; Gate 4: ChromeHeadless, non-interactive, 100% pass, code-coverage).
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import {
  ModuleService,
  type ModuleCreateRequest,
  type ModuleUpdateRequest,
} from './module.service';
import type { Module, Paged } from '../../core/models';

/**
 * Builds a fully-typed `Module` with every REQUIRED field populated, allowing per-test overrides.
 * Cross-checked against core/models `Module`: all non-optional number/boolean fields are set explicitly
 * (moduleOrder, cacheTime, allTabs, visibility, isDeleted, displayTitle, displayPrint, displaySyndicate,
 * inheritViewPermissions, desktopModuleId, isPremium, isAdmin, supportedFeatures, defaultCacheTime,
 * moduleControlId, controlType, supportsPartialRendering). The trailing `as Module` cast -- which is NOT
 * `any` -- only tolerates fields beyond this enumeration.
 */
function makeModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleId: 5,
    portalId: 1,
    tabId: 10,
    tabModuleId: 7,
    moduleDefId: 3,
    moduleOrder: 1,
    moduleTitle: 'Test Module',
    cacheTime: 0,
    allTabs: false,
    visibility: 0,
    isDeleted: false,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    inheritViewPermissions: true,
    desktopModuleId: 2,
    isPremium: false,
    isAdmin: false,
    supportedFeatures: 0,
    defaultCacheTime: 0,
    moduleControlId: 4,
    controlType: 0,
    supportsPartialRendering: false,
    ...overrides,
  } as Module;
}

describe('ModuleService', () => {
  let service: ModuleService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ModuleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify(); // asserts no outstanding/unexpected requests
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getById issues GET /modules/{id}?portalId= and sets the selected signal', () => {
    const mock = makeModule();
    let emitted: Module | undefined;

    service.getById(5, 1).subscribe((m) => (emitted = m));

    const req = httpMock.expectOne(
      (r) => r.method === 'GET' && r.url.endsWith('/modules/5'),
    );
    // MIGRATION: multi-tenant scoping (review CP3) -- portalId is forwarded as a REQUIRED query param
    // (EnforceTenant) and excluded from request.url by the HttpClient `params` option.
    expect(req.request.params.get('portalId')).toBe('1');
    req.flush({ data: mock, meta: {} });

    expect(emitted).toEqual(mock);
    expect(service.selected()).toEqual(mock);
  });

  it('getById toggles the loading signal', () => {
    // `loading` is set true synchronously inside getById(); `finalize` resets it on flush.
    service.getById(5, 1).subscribe();

    expect(service.loading()).toBe(true);

    const req = httpMock.expectOne(
      (r) => r.method === 'GET' && r.url.endsWith('/modules/5'),
    );
    req.flush({ data: makeModule(), meta: {} });

    expect(service.loading()).toBe(false);
  });

  it('getByPortal issues GET /modules with portalId param and sets the modules signal', () => {
    let result: Paged<Module> | undefined;

    service.getByPortal(1).subscribe((r) => (result = r));

    const req = httpMock.expectOne(
      (r) => r.method === 'GET' && r.url.endsWith('/modules'),
    );
    // ApiService forwards query values via the HttpClient `params` option (excluded from request.url).
    expect(req.request.params.get('portalId')).toBe('1');
    expect(req.request.params.get('pageIndex')).toBe('0');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({
      data: [makeModule()],
      meta: {
        totalCount: 1,
        pageIndex: 0,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });

    expect(result).toBeDefined();
    expect(result?.items.length).toBe(1);
    expect(service.modules().length).toBe(1);
  });

  it('create issues POST /modules with the dto body', () => {
    const dto: ModuleCreateRequest = { portalId: 1, tabId: 10, moduleTitle: 'New' };
    const created = makeModule({ moduleTitle: 'New' });
    let emitted: Module | undefined;

    service.create(dto).subscribe((m) => (emitted = m));

    const req = httpMock.expectOne(
      (r) => r.method === 'POST' && r.url.endsWith('/modules'),
    );
    expect(req.request.body).toEqual(dto);
    req.flush({ data: created, meta: {} });

    expect(emitted).toEqual(created);
  });

  it('update issues PUT /modules/{id}?portalId= with the dto body and updates selected', () => {
    const dto: ModuleUpdateRequest = {
      moduleTitle: 'Renamed',
      allTabs: true,
      tabId: 10,
    };
    const updated = makeModule({ moduleTitle: 'Renamed', allTabs: true });
    let emitted: Module | undefined;

    service.update(5, 1, dto).subscribe((m) => (emitted = m));

    const req = httpMock.expectOne(
      (r) => r.method === 'PUT' && r.url.endsWith('/modules/5'),
    );
    // MIGRATION: multi-tenant scoping (review CP3) -- portalId travels as a REQUIRED query param
    // (EnforceTenant), NOT in the body.
    expect(req.request.params.get('portalId')).toBe('1');
    expect(req.request.body).toEqual(dto);
    req.flush({ data: updated, meta: {} });

    expect(emitted).toEqual(updated);
    expect(service.selected()).toEqual(updated);
  });

  it('remove issues DELETE /modules/{id}?portalId=', () => {
    let completed = false;

    service.remove(5, 1).subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne(
      (r) => r.method === 'DELETE' && r.url.endsWith('/modules/5'),
    );
    // MIGRATION: multi-tenant scoping (review CP3) -- portalId travels as a REQUIRED query param (EnforceTenant).
    expect(req.request.params.get('portalId')).toBe('1');
    // ApiService.delete does NOT unwrap an envelope, so a bare 204/no-content body is correct here.
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBe(true);
  });

  // MIGRATION: there are no `exportContent` / `importContent` service tests. Module content export/import depends
  // on the legacy module-loader (IPortable), which AAP Section 0.6.2 places explicitly OUT OF SCOPE, so those
  // service methods (and the ModuleExportRequest / ModuleImportRequest payload types) are intentionally not part
  // of this service; the import-export component renders a documented scope-boundary notice. Recorded in
  // MIGRATION_NOTES.md.
});
