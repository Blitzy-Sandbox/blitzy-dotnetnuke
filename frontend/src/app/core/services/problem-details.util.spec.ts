// MIGRATION: [QA F7 #3] Spec for the net-new toProblemDetails() normaliser (no legacy VB analog -- the legacy
// DNN Web Forms page-level error handling is discarded; AAP Section 0.7.5). This finding flagged the
// core/services directory branch coverage (37.83%, attributed to "api.service") as a weak spot. With
// api.service.ts now fully covered, problem-details.util.ts was the remaining uncovered file in core/services.
// These tests exhaustively exercise every branch of the RFC 7807 normaliser: the structural ProblemDetails
// guard (object/null/non-object, title/detail/status discriminators), the HttpErrorResponse-with-body path
// (status + title backfill), the status-0 "unreachable" path, the genuine 4xx/5xx non-JSON path (string vs
// non-string body), and the non-HTTP generic envelope -- all without relying on err.message (which leaks the
// API URL, QA F4-008).
import { HttpErrorResponse } from '@angular/common/http';

import { toProblemDetails } from './problem-details.util';
import type { ProblemDetails } from '../models/problem-details.model';

/**
 * Build an HttpErrorResponse with a forced (possibly empty) statusText. The Angular constructor coerces a
 * falsy statusText to its non-empty default ('Unknown Error'), so to exercise the empty-statusText fallback
 * branch we redefine the property on the real instance after construction.
 */
function httpError(
  init: { error?: unknown; status?: number; statusText?: string },
  forceStatusText?: string,
): HttpErrorResponse {
  const err = new HttpErrorResponse({
    error: init.error,
    status: init.status ?? 0,
    statusText: init.statusText,
  });
  if (forceStatusText !== undefined) {
    Object.defineProperty(err, 'statusText', { value: forceStatusText, configurable: true });
  }
  return err;
}

describe('toProblemDetails', () => {
  describe('HttpErrorResponse carrying an RFC 7807 ProblemDetails body', () => {
    it('returns the body and preserves an explicit numeric status (status present branch)', () => {
      const body: ProblemDetails = { title: 'Conflict', detail: 'Duplicate name', status: 409 };
      const result = toProblemDetails(
        httpError({ error: body, status: 500, statusText: 'Internal Server Error' }),
      );

      expect(result.status).toBe(409);
      expect(result.title).toBe('Conflict');
      expect(result.detail).toBe('Duplicate name');
    });

    it('backfills status from the HttpErrorResponse when the body omits status (status absent branch)', () => {
      // No `status` on the body -> looksLikeProblemDetails still true via the title discriminator.
      const body = { title: 'Gone' } as unknown as ProblemDetails;
      const result = toProblemDetails(httpError({ error: body, status: 503, statusText: 'Service Unavailable' }));

      expect(result.status).toBe(503);
      expect(result.title).toBe('Gone');
    });

    it('treats a body with only `detail` as ProblemDetails and falls back to statusText for the title', () => {
      // detail-string discriminator true; title absent -> uses statusText.
      const body = { detail: 'The portal could not be created.' } as unknown as ProblemDetails;
      const result = toProblemDetails(httpError({ error: body, status: 503, statusText: 'Service Unavailable' }));

      expect(result.status).toBe(503);
      expect(result.detail).toBe('The portal could not be created.');
      expect(result.title).toBe('Service Unavailable');
    });

    it('treats a body with only numeric `status` as ProblemDetails (status-number discriminator)', () => {
      // status-number discriminator true; status present (uses body status); title absent -> statusText.
      const body = { status: 422 } as unknown as ProblemDetails;
      const result = toProblemDetails(httpError({ error: body, status: 500, statusText: 'Unprocessable Entity' }));

      expect(result.status).toBe(422);
      expect(result.title).toBe('Unprocessable Entity');
    });

    it('uses the final "Unable to load data." fallback when both body title and statusText are empty', () => {
      // title absent AND statusText forced empty -> exercises the terminal fallback of the nested ternary.
      const body = { detail: 'boom' } as unknown as ProblemDetails;
      const result = toProblemDetails(httpError({ error: body, status: 500 }, ''));

      expect(result.status).toBe(500);
      expect(result.title).toBe('Unable to load data.');
      expect(result.detail).toBe('boom');
    });
  });

  describe('HttpErrorResponse with a transport failure (status 0)', () => {
    it('returns the generic unreachable envelope and never leaks a URL (null body covers the guard null-branch)', () => {
      const result = toProblemDetails(httpError({ error: null, status: 0, statusText: 'Unknown Error' }));

      expect(result.status).toBe(0);
      expect(result.title).toBe('Unable to reach the server.');
      expect(result.detail).toBe('Please check your connection and try again.');
    });
  });

  describe('HttpErrorResponse without a ProblemDetails JSON body (genuine 4xx/5xx)', () => {
    it('uses a server-supplied string body as the detail (non-object guard branch)', () => {
      // typeof body !== 'object' -> guard false; string body -> detail.
      const result = toProblemDetails(httpError({ error: 'Resource not found', status: 404, statusText: 'Not Found' }));

      expect(result.status).toBe(404);
      expect(result.title).toBe('Not Found');
      expect(result.detail).toBe('Resource not found');
    });

    it('yields a null detail when the (non-PD) body is not a string (empty-object guard all-false branch)', () => {
      // {} is an object, not null, and has no title/detail/status -> guard false; body not a string -> detail null.
      const result = toProblemDetails(httpError({ error: {}, status: 500, statusText: 'Internal Server Error' }));

      expect(result.status).toBe(500);
      expect(result.title).toBe('Internal Server Error');
      expect(result.detail).toBeNull();
    });

    it('falls back to "Unable to load data." when statusText is empty on a non-PD error', () => {
      const result = toProblemDetails(httpError({ error: {}, status: 500 }, ''));

      expect(result.status).toBe(500);
      expect(result.title).toBe('Unable to load data.');
      expect(result.detail).toBeNull();
    });
  });

  describe('Non-HttpErrorResponse errors', () => {
    it('returns the generic unexpected-error envelope for a thrown Error', () => {
      const result = toProblemDetails(new TypeError('Cannot read properties of undefined'));

      expect(result.status).toBe(0);
      expect(result.title).toBe('An unexpected error occurred.');
      expect(result.detail).toBeNull();
    });

    it('returns the generic unexpected-error envelope for an arbitrary thrown value', () => {
      const result = toProblemDetails('a bare string was thrown');

      expect(result.status).toBe(0);
      expect(result.title).toBe('An unexpected error occurred.');
    });
  });
});
