// MIGRATION: Spec for the net-new generic ApiService (no legacy equivalent; the conceptual analog the service
// replaces is the reflection-based DataProvider/SqlHelper gateway). Verifies URL building from
// environment.apiUrl, HTTP verbs, { data, meta } envelope unwrapping, Paged<T> mapping, 204 DELETE handling,
// and that non-2xx errors are NOT swallowed (they propagate for the global error interceptor).
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';
import type { ApiEnvelope, Paged } from '../models';

interface SampleResource {
  id: number;
  name: string;
}

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('get() should GET environment.apiUrl/<path> and unwrap the data envelope', () => {
    const expected: SampleResource = { id: 1, name: 'Alpha' };
    let actual: SampleResource | undefined;

    service.get<SampleResource>('portals/1').subscribe((res) => (actual = res));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/1`);
    expect(req.request.method).toBe('GET');
    const envelope: ApiEnvelope<SampleResource> = { data: expected, meta: {} };
    req.flush(envelope);

    expect(actual).toEqual(expected);
  });

  it('get() should forward query params to HttpClient', () => {
    let actual: SampleResource[] | undefined;

    service
      .get<SampleResource[]>('portals', { search: 'abc', active: true })
      .subscribe((res) => (actual = res));

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/portals`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('abc');
    expect(req.request.params.get('active')).toBe('true');
    req.flush({ data: [{ id: 1, name: 'Alpha' }], meta: { count: 1 } });

    expect(actual).toEqual([{ id: 1, name: 'Alpha' }]);
  });

  it('getPaged() should map the list envelope (data + meta) into a Paged<T>', () => {
    const items: SampleResource[] = [
      { id: 1, name: 'Alpha' },
      { id: 2, name: 'Beta' },
    ];
    let actual: Paged<SampleResource> | undefined;

    service
      .getPaged<SampleResource>('users', { pageIndex: 0, pageSize: 10 })
      .subscribe((res) => (actual = res));

    const req = httpMock.expectOne(
      (r) => r.url === `${environment.apiUrl}/users`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('pageIndex')).toBe('0');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({
      data: items,
      meta: {
        totalCount: 2,
        pageIndex: 0,
        pageSize: 10,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });

    expect(actual).toBeDefined();
    expect(actual?.items).toEqual(items);
    expect(actual?.totalCount).toBe(2);
    expect(actual?.pageIndex).toBe(0);
    expect(actual?.pageSize).toBe(10);
    expect(actual?.totalPages).toBe(1);
    expect(actual?.hasPreviousPage).toBeFalse();
    expect(actual?.hasNextPage).toBeFalse();
  });

  it('post() should POST the body and unwrap the created resource (201)', () => {
    const created: SampleResource = { id: 3, name: 'Gamma' };
    const payload = { name: 'Gamma' };
    let actual: SampleResource | undefined;

    service
      .post<SampleResource>('portals', payload)
      .subscribe((res) => (actual = res));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(
      { data: created, meta: {} },
      { status: 201, statusText: 'Created' },
    );

    expect(actual).toEqual(created);
  });

  it('put() should PUT the body and unwrap the updated resource (200)', () => {
    const updated: SampleResource = { id: 1, name: 'Alpha-Updated' };
    const payload = { id: 1, name: 'Alpha-Updated' };
    let actual: SampleResource | undefined;

    service
      .put<SampleResource>('portals/1', payload)
      .subscribe((res) => (actual = res));

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(payload);
    req.flush({ data: updated, meta: {} });

    expect(actual).toEqual(updated);
  });

  it('delete() should DELETE and handle a 204 empty body', () => {
    let completed = false;
    let result: unknown = 'sentinel';

    service.delete('portals/1').subscribe({
      next: (res) => (result = res),
      complete: () => (completed = true),
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(result).toBeNull();
    expect(completed).toBeTrue();
  });

  it('should NOT swallow non-2xx errors (propagates HttpErrorResponse)', () => {
    let errorResponse: HttpErrorResponse | undefined;

    service.get<SampleResource>('portals/999').subscribe({
      next: () => fail('expected the request to error, not succeed'),
      error: (err: HttpErrorResponse) => (errorResponse = err),
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/portals/999`);
    req.flush(
      {
        type: 'about:blank',
        title: 'Not Found',
        status: 404,
        detail: 'Portal not found',
      },
      { status: 404, statusText: 'Not Found' },
    );

    expect(errorResponse).toBeDefined();
    expect(errorResponse?.status).toBe(404);
  });
});
