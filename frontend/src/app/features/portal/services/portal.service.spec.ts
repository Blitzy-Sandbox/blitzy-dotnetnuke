// MIGRATION: Karma/Jasmine unit-test suite for PortalService (Gate 4).
//
// PortalService is a thin delegation layer over the core ApiService (it never touches HttpClient
// directly). This suite therefore MOCKS ApiService with a Jasmine spy and asserts that each
// PortalService method delegates to the correct ApiService verb with the correctly composed URL and
// arguments — NO HttpTestingController and NO provideHttpClient are used (PortalService has no HTTP
// surface of its own to exercise).
//
// FIXTURE NOTE (contract alignment): the three fixture factories below are authored as fully-typed,
// direct object literals rather than rest-omit destructuring of a single Portal fixture. This is
// required because the CURRENT, committed `../models` contracts (aligned to the CP2 service contracts
// in commit ec61df9) are NOT exact Omit<>s of one another:
//   * `Portal` (read model) intentionally OMITS `processorPassword` (write-only credential, never
//     projected into GET responses) and models several numeric ids as nullable (`number | null`).
//   * `CreatePortalRequest` / `UpdatePortalRequest` (write models) INCLUDE `processorPassword` and
//     declare those same numeric ids as NON-nullable (`number`).
// Deriving a request DTO by spreading/rest-omitting a `Portal` would fail strict type-checking
// (`number | null` is not assignable to `number`; the required `processorPassword` would be missing).
// Building each fixture as an explicit typed literal keeps every fixture strictly typed (NO `any`,
// NO casts) and faithful to the wire shape each endpoint actually accepts/returns. The seven specs
// themselves — and their delegation assertions — are unchanged: they depend only on object identity,
// not on the fixtures' field values.

import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../models';
import { PortalService } from './portal.service';

/**
 * Build a fully-populated `Portal` read-model fixture (the `GET` projection — 37 fields, no
 * `processorPassword`). Callers override individual fields (e.g. `portalID`) via `overrides`.
 */
function makePortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalID: 1,
    portalName: 'Acme',
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
    guid: '00000000-0000-0000-0000-000000000000',
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
    defaultLanguage: 'en-US',
    timeZoneOffset: 0,
    homeDirectory: null,
    version: null,
    ...overrides,
  };
}

/**
 * Build a `CreatePortalRequest` body fixture (the `POST` write model — 35 fields: the create shape
 * carries `processorPassword` and uses non-nullable numeric ids; it omits the server-assigned
 * `portalID` and the server-derived metrics `users` / `pages`).
 */
function makeCreateRequest(): CreatePortalRequest {
  return {
    portalName: 'Acme',
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
    guid: '00000000-0000-0000-0000-000000000000',
    paymentProcessor: null,
    processorPassword: null,
    processorUserId: null,
    siteLogHistory: 0,
    email: null,
    adminTabId: 0,
    superTabId: 0,
    splashTabId: 0,
    homeTabId: 0,
    loginTabId: 0,
    userTabId: 0,
    defaultLanguage: 'en-US',
    timeZoneOffset: 0,
    homeDirectory: null,
    version: null,
  };
}

/**
 * Build an `UpdatePortalRequest` body fixture (the `PUT` write model — 36 fields: the create shape
 * plus the `portalID` that identifies the row being updated). All other fields are identical in type
 * to `CreatePortalRequest`, so we layer `portalID` over the create fixture.
 */
function makeUpdateRequest(id: number): UpdatePortalRequest {
  return { portalID: id, ...makeCreateRequest() };
}

describe('PortalService', () => {
  let service: PortalService;
  let api: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    const apiSpy = jasmine.createSpyObj<ApiService>('ApiService', [
      'resourceUrl',
      'get',
      'getList',
      'post',
      'put',
      'delete',
    ]);
    apiSpy.resourceUrl.and.callFake((entity: string, id?: string | number) =>
      id === undefined ? `/api/v1/${entity}` : `/api/v1/${entity}/${id}`,
    );

    TestBed.configureTestingModule({
      providers: [PortalService, { provide: ApiService, useValue: apiSpy }],
    });

    service = TestBed.inject(PortalService);
    api = TestBed.inject(ApiService) as jasmine.SpyObj<ApiService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getPortals delegates to api.getList with paging params', () => {
    const page: PagedResponse<Portal> = {
      data: [makePortal()],
      meta: { pageIndex: 0, pageSize: 10, totalCount: 1, totalPages: 1 },
    };
    api.getList.and.returnValue(of(page));

    let result: PagedResponse<Portal> | undefined;
    service.getPortals('a', 0, 10).subscribe((p) => (result = p));

    expect(api.resourceUrl).toHaveBeenCalledWith('portals');
    expect(api.getList).toHaveBeenCalledWith('/api/v1/portals', {
      query: 'a',
      pageIndex: 0,
      pageSize: 10,
    });
    expect(result).toBe(page);
  });

  it('getAllPortals delegates to api.get for the unpaged list', () => {
    const portals = [makePortal({ portalID: 1 }), makePortal({ portalID: 2 })];
    api.get.and.returnValue(of(portals));

    let result: Portal[] | undefined;
    service.getAllPortals().subscribe((p) => (result = p));

    expect(api.get).toHaveBeenCalledWith('/api/v1/portals');
    expect(result).toBe(portals);
  });

  it('getPortal delegates to api.get with the id URL', () => {
    const portal = makePortal({ portalID: 5 });
    api.get.and.returnValue(of(portal));

    let result: Portal | undefined;
    service.getPortal(5).subscribe((p) => (result = p));

    expect(api.resourceUrl).toHaveBeenCalledWith('portals', 5);
    expect(api.get).toHaveBeenCalledWith('/api/v1/portals/5');
    expect(result).toBe(portal);
  });

  it('createPortal delegates to api.post with the request body', () => {
    const request = makeCreateRequest();
    const created = makePortal({ portalID: 99 });
    api.post.and.returnValue(of(created));

    let result: Portal | undefined;
    service.createPortal(request).subscribe((p) => (result = p));

    expect(api.post).toHaveBeenCalledWith('/api/v1/portals', request);
    expect(result).toBe(created);
  });

  it('updatePortal delegates to api.put with the id URL and body', () => {
    const request = makeUpdateRequest(7);
    const updated = makePortal({ portalID: 7 });
    api.put.and.returnValue(of(updated));

    let result: Portal | undefined;
    service.updatePortal(7, request).subscribe((p) => (result = p));

    expect(api.resourceUrl).toHaveBeenCalledWith('portals', 7);
    expect(api.put).toHaveBeenCalledWith('/api/v1/portals/7', request);
    expect(result).toBe(updated);
  });

  it('deletePortal delegates to api.delete with the id URL', () => {
    api.delete.and.returnValue(of(undefined));

    let completed = false;
    service.deletePortal(3).subscribe({ complete: () => (completed = true) });

    expect(api.resourceUrl).toHaveBeenCalledWith('portals', 3);
    expect(api.delete).toHaveBeenCalledWith('/api/v1/portals/3');
    expect(completed).toBe(true);
  });
});
