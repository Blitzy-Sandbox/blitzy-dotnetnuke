import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { ApiResponse, ApiService, PagedResponse, ProblemDetails } from './api.service';

interface SamplePortal {
  portalId: number;
  portalName: string;
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

  describe('URL composition', () => {
    it('builds a versioned resource URL (apiUrl + /v1/<entity>)', () => {
      expect(service.resourceUrl('portals')).toBe(`${environment.apiUrl}/v1/portals`);
    });

    it('builds a versioned resource URL with an id', () => {
      expect(service.resourceUrl('portals', 5)).toBe(`${environment.apiUrl}/v1/portals/5`);
    });

    it('builds an UNVERSIONED auth URL (apiUrl + /auth/<action>)', () => {
      expect(service.authUrl('login')).toBe(`${environment.apiUrl}/auth/login`);
    });

    it('builds a health URL at the SERVER ROOT (no /api, no /v1)', () => {
      expect(service.healthUrl()).toBe('http://localhost:5000/health');
    });
  });

  describe('get<T>', () => {
    it('unwraps the { data } envelope', () => {
      const url = service.resourceUrl('portals', 1);
      let result: SamplePortal | undefined;
      service.get<SamplePortal>(url).subscribe((data) => (result = data));

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('GET');
      const envelope: ApiResponse<SamplePortal> = { data: { portalId: 1, portalName: 'Acme' } };
      req.flush(envelope);

      expect(result).toEqual({ portalId: 1, portalName: 'Acme' });
    });
  });

  describe('getList<T>', () => {
    it('returns data + meta and appends query params', () => {
      const url = service.resourceUrl('portals');
      let result: PagedResponse<SamplePortal> | undefined;
      service
        .getList<SamplePortal>(url, { pageIndex: 0, pageSize: 10 })
        .subscribe((page) => (result = page));

      const req = httpMock.expectOne((r) => r.url === url && r.method === 'GET');
      expect(req.request.params.get('pageIndex')).toBe('0');
      expect(req.request.params.get('pageSize')).toBe('10');
      req.flush({
        data: [
          { portalId: 1, portalName: 'A' },
          { portalId: 2, portalName: 'B' },
        ],
        meta: { pageIndex: 0, pageSize: 10, totalCount: 2, totalPages: 1 },
      });

      expect(result?.data.length).toBe(2);
      expect(result?.meta.totalCount).toBe(2);
      expect(result?.meta.pageIndex).toBe(0);
    });
  });

  describe('post<T>', () => {
    it('sends the body and unwraps the created resource', () => {
      const url = service.resourceUrl('portals');
      let result: SamplePortal | undefined;
      service.post<SamplePortal>(url, { portalName: 'New' }).subscribe((data) => (result = data));

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ portalName: 'New' });
      req.flush({ data: { portalId: 99, portalName: 'New' } }, { status: 201, statusText: 'Created' });

      expect(result).toEqual({ portalId: 99, portalName: 'New' });
    });
  });

  describe('put<T>', () => {
    it('sends the body and unwraps the updated resource', () => {
      const url = service.resourceUrl('portals', 7);
      let result: SamplePortal | undefined;
      service
        .put<SamplePortal>(url, { portalName: 'Edited' })
        .subscribe((data) => (result = data));

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('PUT');
      req.flush({ data: { portalId: 7, portalName: 'Edited' } });

      expect(result).toEqual({ portalId: 7, portalName: 'Edited' });
    });
  });

  describe('delete', () => {
    it('completes on a 204 No Content response', () => {
      const url = service.resourceUrl('portals', 3);
      let completed = false;
      service.delete(url).subscribe({ complete: () => (completed = true) });

      const req = httpMock.expectOne(url);
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });

      expect(completed).toBe(true);
    });
  });

  describe('error handling (RFC 7807)', () => {
    it('surfaces ProblemDetails including field-level errors', () => {
      const url = service.resourceUrl('portals');
      let problem: ProblemDetails | undefined;
      service.post<SamplePortal>(url, {}).subscribe({
        next: () => fail('expected the request to error'),
        error: (err: ProblemDetails) => (problem = err),
      });

      const req = httpMock.expectOne(url);
      req.flush(
        {
          type: 'https://dnnmigration.com/errors/validation',
          title: 'Validation Error',
          status: 400,
          detail: 'One or more validation errors occurred.',
          errors: { portalName: ['Portal name is required.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(problem?.status).toBe(400);
      expect(problem?.title).toBe('Validation Error');
      expect(problem?.errors?.['portalName']).toEqual(['Portal name is required.']);
    });
  });
});
