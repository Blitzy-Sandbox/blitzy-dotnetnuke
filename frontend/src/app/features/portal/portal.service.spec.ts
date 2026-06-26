// MIGRATION: Spec for the signal-based PortalService (behaviors from Website/admin/Portal/Portals.ascx.vb +
// SiteSettings.ascx.vb). Verifies CRUD URLs/verbs against environment.apiUrl, { data, meta } envelope handling,
// zero-based paged mapping + signal updates, the Expired sub-resource, and bulk delete. Gate 4 (100% pass).
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import { PortalService, type PortalRequest } from './portal.service';
import { environment } from '../../../environments/environment';
import type { Portal } from '../../core/models';

function makePortal(overrides: Partial<Portal> = {}): Portal {
  return {
    portalId: 1,
    portalName: 'Test Portal',
    userRegistration: 0,
    bannerAdvertising: 0,
    administratorId: 2,
    hostFee: 0,
    hostSpace: 0,
    pageQuota: 0,
    userQuota: 0,
    administratorRoleId: 0,
    registeredRoleId: 1,
    guid: '00000000-0000-0000-0000-000000000000',
    siteLogHistory: -1,
    adminTabId: 0,
    superTabId: 0,
    splashTabId: -1,
    homeTabId: -1,
    loginTabId: -1,
    userTabId: -1,
    timeZoneOffset: 0,
    ...overrides,
  };
}

const portalRequest: PortalRequest = {
  portalName: 'New Portal',
  userRegistration: 0,
  bannerAdvertising: 0,
  administratorId: 2,
  hostFee: 0,
  hostSpace: 0,
  pageQuota: 0,
  userQuota: 0,
  siteLogHistory: -1,
  splashTabId: -1,
  homeTabId: -1,
  loginTabId: -1,
  userTabId: -1,
  timeZoneOffset: 0,
};

describe('PortalService', () => {
  let service: PortalService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PortalService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('list() should GET portals (zero-based paging) and update the portals/totalCount signals', () => {
    const items = [
      makePortal({ portalId: 1 }),
      makePortal({ portalId: 2, portalName: 'Second' }),
    ];

    service.list(0, 20).subscribe();
    expect(service.loading()).toBeTrue();

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/portals`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('pageIndex')).toBe('0');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.has('filter')).toBeFalse();
    req.flush({
      data: items,
      meta: {
        totalCount: 2,
        pageIndex: 0,
        pageSize: 20,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });

    expect(service.portals()).toEqual(items);
    expect(service.totalCount()).toBe(2);
    expect(service.loading()).toBeFalse();
  });

  it('list() should forward a letter filter as a query param', () => {
    service.list(0, 20, 'A').subscribe();

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/portals`,
    );
    expect(req.request.params.get('filter')).toBe('A');
    req.flush({
      data: [],
      meta: {
        totalCount: 0,
        pageIndex: 0,
        pageSize: 20,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });

    expect(service.portals()).toEqual([]);
    expect(service.totalCount()).toBe(0);
  });

  it('getById() should GET portals/{id} and update the selected signal', () => {
    const portal = makePortal({ portalId: 5, portalName: 'Five' });
    let actual: Portal | undefined;

    service.getById(5).subscribe((p) => (actual = p));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/5`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: portal, meta: {} });

    expect(actual).toEqual(portal);
    expect(service.selected()).toEqual(portal);
    expect(service.loading()).toBeFalse();
  });

  it('create() should POST portals and return the created portal (201)', () => {
    const created = makePortal({ portalId: 9, portalName: 'New Portal' });
    let actual: Portal | undefined;

    service.create(portalRequest).subscribe((p) => (actual = p));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(portalRequest);
    req.flush(
      { data: created, meta: {} },
      { status: 201, statusText: 'Created' },
    );

    expect(actual).toEqual(created);
  });

  it('update() should PUT portals/{id} and return the updated portal (200)', () => {
    const updated = makePortal({ portalId: 5, portalName: 'Updated' });
    let actual: Portal | undefined;

    service.update(5, portalRequest).subscribe((p) => (actual = p));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/5`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(portalRequest);
    req.flush({ data: updated, meta: {} });

    expect(actual).toEqual(updated);
  });

  it('delete() should DELETE portals/{id} and handle a 204', () => {
    let completed = false;

    service.delete(5).subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/5`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
  });

  it('getExpired() should GET portals/expired and populate the portals signal', () => {
    const expired = [makePortal({ portalId: 7, portalName: 'Expired One' })];

    service.getExpired().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/expired`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: expired, meta: {} });

    expect(service.portals()).toEqual(expired);
    expect(service.totalCount()).toBe(1);
    expect(service.loading()).toBeFalse();
  });

  it('deleteExpired() should DELETE portals/expired (204)', () => {
    let completed = false;

    service.deleteExpired().subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/expired`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
  });
});
