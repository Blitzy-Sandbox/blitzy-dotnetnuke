import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiService, QueryParams } from './api.service';
import { ApiMeta, ProblemDetails } from '../models';
import { environment } from '../../../environments/environment';

/**
 * Unit spec for {@link ApiService}, exercising the REAL service against
 * {@link HttpTestingController} (not a mock) so the wire-level response contract is
 * verified end-to-end.
 *
 * MIGRATION (Checkpoint-8 API-contract finding): this spec locks the fix for the
 * 204-No-Content contract. The generic `post<T>()` unwraps the `{ data, meta }`
 * envelope by reading `res.data`, which throws on a 204's empty body and mis-reports a
 * successful command as an error. `postNoContent()` was added for the command
 * endpoints (`auth/logout`, `users/{id}/change-password`) that return 204; these tests
 * prove it resolves cleanly to `void` on a 204 while `post<T>()` still unwraps the
 * envelope, and that both still normalize errors to RFC 7807 ProblemDetails.
 * Contributes to Gate 4 (ng test --code-coverage, 100% pass).
 */
describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;
  const base = environment.apiBaseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('postNoContent()', () => {
    it('resolves to void on a 204 No Content response without reading res.data', () => {
      let completed = false;
      let errored = false;
      let emitted: unknown = 'unset';

      service.postNoContent('auth/logout', { refreshToken: 'rt' }).subscribe({
        next: (value) => (emitted = value),
        error: () => (errored = true),
        complete: () => (completed = true),
      });

      const req = httpMock.expectOne(`${base}/auth/logout`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ refreshToken: 'rt' });

      // A real 204 carries an empty (null) body -- the previous post<T>() would have
      // thrown mapping res.data on this; postNoContent must complete cleanly.
      req.flush(null, { status: 204, statusText: 'No Content' });

      expect(errored).toBeFalse();
      expect(completed).toBeTrue();
      expect(emitted).toBeUndefined();
    });

    it('normalizes an error body to RFC 7807 ProblemDetails and rethrows', () => {
      let problem: ProblemDetails | undefined;

      service.postNoContent('auth/logout', {}).subscribe({
        error: (err: ProblemDetails) => (problem = err),
      });

      const req = httpMock.expectOne(`${base}/auth/logout`);
      req.flush(
        { type: 'about:blank', title: 'Bad Request', status: 400, detail: 'nope' },
        { status: 400, statusText: 'Bad Request' }
      );

      expect(problem).toBeTruthy();
      expect(problem?.status).toBe(400);
      expect(problem?.title).toBe('Bad Request');
    });
  });

  describe('post<T>() envelope contract (unchanged)', () => {
    it('unwraps the { data } success envelope', () => {
      let result: { id: number } | undefined;

      service.post<{ id: number }>('things', { n: 1 }).subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/things`);
      expect(req.request.method).toBe('POST');
      req.flush({ data: { id: 42 }, meta: {} });

      expect(result).toEqual({ id: 42 });
    });
  });

  // ---------------------------------------------------------------------------
  // QA Report 10 Issue 1 (MINOR, coverage): the single SPA HTTP gateway
  // (api.service.ts) sat at 36% coverage -- its CRUD helpers, URL construction
  // (join/buildUrl) and QueryParams serialization were never directly exercised
  // (feature services mock ApiService, so the real methods never ran). The specs
  // below drive the REAL service against HttpTestingController and lock the exact
  // wire contract (method, absolute URL, id encoding, query serialization,
  // { data, meta } envelope unwrap, and RFC 7807 error normalization) for every
  // public helper. Contributes to Gate 4 (ng test --code-coverage, 100% pass).
  // ---------------------------------------------------------------------------

  describe('getList()', () => {
    it('GETs the resource URL and unwraps the { data } array', () => {
      let result: Array<{ id: number }> | undefined;
      service.getList<{ id: number }>('portals').subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/portals`);
      expect(req.request.method).toBe('GET');
      req.flush({ data: [{ id: 1 }, { id: 2 }], meta: { totalCount: 2 } });

      expect(result).toEqual([{ id: 1 }, { id: 2 }]);
    });

    it('serializes QueryParams: scalars via set, arrays as repeated keys, null/undefined skipped', () => {
      // `null`/`undefined` values are outside the QueryParams type but the runtime
      // toHttpParams() skips them; cast exercises that defensive branch.
      const params = {
        query: 'abc',
        page: 1,
        active: true,
        id: [1, 2],
        skip: null,
        omit: undefined,
      } as unknown as QueryParams;

      service.getList('portals', params).subscribe();

      const req = httpMock.expectOne((r) => r.url === `${base}/portals`);
      expect(req.request.method).toBe('GET');
      const p = req.request.params;
      expect(p.get('query')).toBe('abc');
      expect(p.get('page')).toBe('1');
      expect(p.get('active')).toBe('true');
      expect(p.getAll('id')).toEqual(['1', '2']);
      expect(p.has('skip')).toBeFalse();
      expect(p.has('omit')).toBeFalse();
      req.flush({ data: [], meta: {} });
    });
  });

  describe('getListWithMeta()', () => {
    it('returns BOTH the data array and the meta envelope, and serializes params', () => {
      let out: { data: Array<{ id: number }>; meta?: ApiMeta } | undefined;
      service.getListWithMeta<{ id: number }>('portals', { page: 2 }).subscribe((r) => (out = r));

      const req = httpMock.expectOne((r) => r.url === `${base}/portals`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('page')).toBe('2');
      req.flush({ data: [{ id: 1 }], meta: { totalCount: 10, page: 2, correlationId: 'cid-1' } });

      expect(out?.data).toEqual([{ id: 1 }]);
      expect(out?.meta?.totalCount).toBe(10);
      expect(out?.meta?.correlationId).toBe('cid-1');
    });
  });

  describe('getById()', () => {
    it('GETs {resource}/{id} and unwraps { data }', () => {
      let result: { id: number } | undefined;
      service.getById<{ id: number }>('portals', 5).subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/portals/5`);
      expect(req.request.method).toBe('GET');
      req.flush({ data: { id: 5 }, meta: {} });

      expect(result).toEqual({ id: 5 });
    });

    it('URL-encodes a string id segment (join)', () => {
      service.getById('users', 'a b').subscribe();

      const req = httpMock.expectOne(`${base}/users/a%20b`);
      expect(req.request.method).toBe('GET');
      req.flush({ data: {}, meta: {} });
    });
  });

  describe('create() / createWithMeta()', () => {
    it('create() POSTs the body and unwraps { data }', () => {
      let result: { id: number } | undefined;
      service.create<{ id: number }>('roles', { roleName: 'Admins' }).subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/roles`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ roleName: 'Admins' });
      req.flush({ data: { id: 9 }, meta: {} });

      expect(result).toEqual({ id: 9 });
    });

    it('createWithMeta() exposes the created entity AND meta (e.g. generatedPassword, QA F3)', () => {
      let out: { data: { id: number }; meta: ApiMeta } | undefined;
      service
        .createWithMeta<{ id: number }>('users', { username: 'u' })
        .subscribe((r) => (out = r));

      const req = httpMock.expectOne(`${base}/users`);
      expect(req.request.method).toBe('POST');
      req.flush({ data: { id: 4 }, meta: { generatedPassword: 'Temp!234' } });

      expect(out?.data).toEqual({ id: 4 });
      expect(out?.meta.generatedPassword).toBe('Temp!234');
    });
  });

  describe('update() (PUT) / patchById() (PATCH)', () => {
    it('update() PUTs {resource}/{id} with the body and unwraps { data }', () => {
      let result: { id: number } | undefined;
      service
        .update<{ id: number }>('portals', 3, { portalName: 'x' })
        .subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/portals/3`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ portalName: 'x' });
      req.flush({ data: { id: 3 }, meta: {} });

      expect(result).toEqual({ id: 3 });
    });

    it('patchById() PATCHes {resource}/{id} with the body and unwraps { data }', () => {
      let result: { id: number } | undefined;
      service
        .patchById<{ id: number }>('users', 7, { email: 'a@b.com' })
        .subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/users/7`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual({ email: 'a@b.com' });
      req.flush({ data: { id: 7 }, meta: {} });

      expect(result).toEqual({ id: 7 });
    });
  });

  describe('delete()', () => {
    it('DELETEs {resource}/{id} against the ABSOLUTE API url and resolves to void on 204', () => {
      let completed = false;
      let errored = false;
      let emitted: unknown = 'unset';

      service.delete('portals', 9).subscribe({
        next: (v) => (emitted = v),
        error: () => (errored = true),
        complete: () => (completed = true),
      });

      const req = httpMock.expectOne(`${base}/portals/9`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });

      expect(errored).toBeFalse();
      expect(completed).toBeTrue();
      expect(emitted).toBeUndefined();
    });
  });

  describe('low-level verb primitives', () => {
    it('get<T>() GETs a path relative to the API base and unwraps { data }', () => {
      let result: { ok: boolean } | undefined;
      service.get<{ ok: boolean }>('auth/me').subscribe((r) => (result = r));

      const req = httpMock.expectOne(`${base}/auth/me`);
      expect(req.request.method).toBe('GET');
      req.flush({ data: { ok: true }, meta: {} });

      expect(result).toEqual({ ok: true });
    });

    it('buildUrl() trims stray leading/trailing slashes off the path', () => {
      service.get('/auth/me/').subscribe();

      // The trimmed absolute URL has no doubled or trailing slash.
      const req = httpMock.expectOne(`${base}/auth/me`);
      expect(req.request.method).toBe('GET');
      req.flush({ data: null, meta: {} });
    });
  });

  describe('handleError() -- RFC 7807 normalization', () => {
    it('passes a well-formed Problem body through, preserving status + field errors', () => {
      let problem: ProblemDetails | undefined;
      service.getList('portals').subscribe({ error: (e: ProblemDetails) => (problem = e) });

      const req = httpMock.expectOne(`${base}/portals`);
      req.flush(
        { type: 'https://errors/val', title: 'Bad', status: 400, detail: 'd', errors: { name: ['required'] } },
        { status: 400, statusText: 'Bad Request' }
      );

      expect(problem?.status).toBe(400);
      expect(problem?.type).toBe('https://errors/val');
      expect(problem?.title).toBe('Bad');
      expect(problem?.errors).toEqual({ name: ['required'] });
    });

    it('fills type/title defaults when the Problem body omits them', () => {
      let problem: ProblemDetails | undefined;
      service.getList('portals').subscribe({ error: (e: ProblemDetails) => (problem = e) });

      const req = httpMock.expectOne(`${base}/portals`);
      // Numeric status present (Problem branch) but no type/title -> defaults apply.
      req.flush({ status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });

      expect(problem?.type).toBe('about:blank');
      expect(problem?.title).toBe('Unprocessable Entity');
      expect(problem?.status).toBe(422);
    });

    it('synthesizes a Problem from a non-Problem error body (no numeric status)', () => {
      let problem: ProblemDetails | undefined;
      service.getList('portals').subscribe({ error: (e: ProblemDetails) => (problem = e) });

      const req = httpMock.expectOne(`${base}/portals`);
      // Object without a numeric `status` field -> synthesize branch.
      req.flush({ message: 'oops' }, { status: 500, statusText: 'Server Error' });

      expect(problem?.status).toBe(500);
      expect(problem?.title).toBe('Server Error');
    });

    it('synthesizes a Problem with the string body as detail', () => {
      let problem: ProblemDetails | undefined;
      service.getList('portals').subscribe({ error: (e: ProblemDetails) => (problem = e) });

      const req = httpMock.expectOne(`${base}/portals`);
      // A plain-text error body -> detail is the string.
      req.flush('plain text failure', { status: 503, statusText: 'Service Unavailable' });

      expect(problem?.status).toBe(503);
      expect(problem?.detail).toBe('plain text failure');
    });
  });
});
