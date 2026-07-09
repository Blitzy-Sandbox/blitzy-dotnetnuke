import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiService } from './api.service';
import { ProblemDetails } from '../models';
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

      // A real 204 carries an empty (null) body — the previous post<T>() would have
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
});
