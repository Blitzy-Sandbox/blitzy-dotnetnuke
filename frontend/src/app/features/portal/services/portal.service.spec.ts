import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../models';
import { PortalService } from './portal.service';

// Typed fixture factories that mirror the committed Portal feature wire-shapes (../models).
//
// MIGRATION/PARITY NOTE: the read `Portal` interface intentionally OMITS the write-only
// `processorPassword` credential (SECURITY: never round-tripped to the SPA), while
// `CreatePortalRequest`/`UpdatePortalRequest` OMIT the server-managed `guid` (INTEGRITY) yet
// REQUIRE the write-only `processorPassword`. The request factories therefore derive their shape
// from the full `Portal` fixture, drop the server-managed fields (`portalID`/`guid` and the
// server-derived metrics `users`/`pages`), and add `processorPassword` - keeping every fixture
// fully typed against the real interfaces with NO `any`. The rest-omit destructuring is the
// documented exception under `noUnusedLocals`, so no `void` suppressors are required.

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

function makeCreateRequest(): CreatePortalRequest {
  // CreatePortalRequest = the full Portal read shape minus the server-assigned `portalID`,
  // the server-managed `guid`, and the server-derived metrics `users`/`pages`, plus the
  // write-only `processorPassword`.
  const { portalID, users, pages, guid, ...rest } = makePortal();
  return { ...rest, processorPassword: null };
}

function makeUpdateRequest(id: number): UpdatePortalRequest {
  // UpdatePortalRequest = the full Portal read shape minus the server-managed `guid` and the
  // server-derived metrics `users`/`pages`; KEEPS `portalID` to identify the row being updated,
  // plus the write-only `processorPassword`.
  const { users, pages, guid, ...rest } = makePortal({ portalID: id });
  return { ...rest, processorPassword: null };
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
