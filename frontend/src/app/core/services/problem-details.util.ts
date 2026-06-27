// MIGRATION: [QA F3 #4] net-new shared helper (no legacy VB analog). The legacy DNN list controls
// (Website/admin/Portal/Portals.ascx.vb, Users.ascx.vb, Security/Roles.ascx.vb) surfaced data-layer
// failures through Web Forms page-level error handling; that machinery is discarded. Here, a list/collection
// GET that fails must surface the backend RFC 7807 ProblemDetails envelope (AAP Section 0.7.5) to the user
// instead of silently falling back to the empty state. This helper normalises ANY caught error into a
// non-null ProblemDetails so the feature services (portal/user/role) can store it in an `_error` signal and
// the shared DataTableComponent can render a consistent danger banner. It is invoked ONLY from inside a
// catchError (an error is known to have occurred), so it always returns a renderable ProblemDetails.
import { HttpErrorResponse } from '@angular/common/http';

import type { ProblemDetails } from '../models/problem-details.model';

/**
 * Structural guard: does `body` look like an RFC 7807 ProblemDetails envelope?
 * The backend ExceptionHandlingMiddleware emits at least one of `title` / `detail` / `status`, so the presence
 * of any of those (with the expected primitive type) is sufficient to treat the body as ProblemDetails.
 */
function looksLikeProblemDetails(body: unknown): body is ProblemDetails {
  if (typeof body !== 'object' || body === null) {
    return false;
  }
  const candidate = body as Record<string, unknown>;
  return (
    typeof candidate['title'] === 'string' ||
    typeof candidate['detail'] === 'string' ||
    typeof candidate['status'] === 'number'
  );
}

/**
 * Normalise any caught error into a renderable RFC 7807 ProblemDetails.
 *
 * - When the error is an HttpErrorResponse whose `.error` body is already a ProblemDetails (the common case —
 *   the backend RFC 7807 envelope produced by ExceptionHandlingMiddleware), that body is returned, with
 *   `status` / `title` defensively backfilled from the HttpErrorResponse when missing.
 * - When the body is a plain string or absent, a ProblemDetails is synthesised from the HTTP status / statusText.
 * - For non-HTTP errors (e.g. a thrown TypeError), a generic envelope is returned so the UI still shows a banner.
 *
 * Always returns a non-null object: it is only called when an error has already occurred (inside catchError).
 */
export function toProblemDetails(err: unknown): ProblemDetails {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (looksLikeProblemDetails(body)) {
      const problem = body as ProblemDetails;
      return {
        ...problem,
        status: typeof problem.status === 'number' ? problem.status : err.status,
        title:
          typeof problem.title === 'string' && problem.title.length > 0
            ? problem.title
            : err.statusText && err.statusText.length > 0
              ? err.statusText
              : 'Unable to load data.',
      };
    }
    // MIGRATION: [QA F4-008 / F4-013] status 0 means the request never completed a round-trip
    // (backend unreachable, CORS/preflight blocked, DNS failure, or a transient network blip). In
    // that case `err.error` is a ProgressEvent (NOT a ProblemDetails) and `err.message` is the raw
    // Angular string "Http failure response for <URL>: 0 Unknown Error" -- which leaks the internal
    // API URL (information disclosure flagged by QA F4-008) and is meaningless to the user. Surface a
    // generic, friendly envelope instead. This single normalisation covers BOTH the list-fetch error
    // banner (services store it in `_error`) and the form-submit summary (forms delegate here).
    if (err.status === 0) {
      return {
        status: 0,
        title: 'Unable to reach the server.',
        detail: 'Please check your connection and try again.',
      };
    }
    // A genuine HTTP error (4xx/5xx) that did NOT carry a ProblemDetails JSON body. Prefer a
    // server-supplied string body as the detail; otherwise show only the status text. We deliberately
    // do NOT fall back to `err.message`, because Angular embeds the request URL in it (QA F4-008).
    return {
      status: err.status,
      title: err.statusText && err.statusText.length > 0 ? err.statusText : 'Unable to load data.',
      detail: typeof body === 'string' && body.length > 0 ? body : null,
    };
  }
  return { status: 0, title: 'An unexpected error occurred.', detail: null };
}
