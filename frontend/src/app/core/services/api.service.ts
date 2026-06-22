import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';

/**
 * Optional metadata returned alongside the payload in the backend `{ data, meta }` success envelope.
 * Populated for paged list endpoints; absent/null for single-resource responses.
 */
export interface ApiResponseMeta {
  pageIndex?: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
  correlationId?: string;
}

/** The uniform backend success envelope: `{ data, meta }`. */
export interface ApiResponse<T> {
  data: T;
  meta?: ApiResponseMeta | null;
}

/** Unwrapped paged result handed to callers: the list plus its pagination metadata. */
export interface PagedResponse<T> {
  data: T[];
  meta: ApiResponseMeta;
}

/** RFC 7807 Problem Details error contract surfaced to callers on HTTP failure. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

/**
 * Maximum length (characters) of a single-line error message surfaced in a UI alert banner.
 * Concise, actionable text fits comfortably within this bound; verbose server detail (exception
 * stack traces, internal file paths) is collapsed to fit so it never dominates the viewport.
 */
const MAX_PROBLEM_DETAIL_LENGTH = 160;

/**
 * Collapse a ProblemDetails `detail` string to a concise, single-line, length-bounded form.
 *
 * MIGRATION (QA Finding 6): in Development the backend emits full multi-line SQL exception stack
 * traces (including internal file paths) in `detail`. Rendering that verbatim inside a red alert
 * obscured the list/form screens. This helper keeps short, meaningful messages verbatim
 * (e.g. "Database unreachable.") while truncating verbose traces to their first line, capped at
 * {@link MAX_PROBLEM_DETAIL_LENGTH} with an ellipsis. The full detail remains available in the
 * server logs and the browser dev tools.
 *
 * @param detail Raw `ProblemDetails.detail` (may be `null`/`undefined`/empty).
 * @returns A concise single-line message, or `''` when no usable detail is present.
 */
export function condenseProblemDetail(detail: string | null | undefined): string {
  const trimmed = (detail ?? '').trim();
  if (trimmed.length === 0) {
    return '';
  }
  const firstLine = trimmed.split(/\r?\n/, 1)[0].trim();
  // "Verbose" == multi-line (a stack trace) OR a single line longer than the bound.
  const isVerbose = /\r?\n/.test(trimmed) || firstLine.length > MAX_PROBLEM_DETAIL_LENGTH;
  if (!isVerbose) {
    // Already concise (the common case for genuine business messages): show it verbatim.
    return firstLine;
  }
  if (firstLine.length <= MAX_PROBLEM_DETAIL_LENGTH) {
    // Multi-line but a short first line: show that line and signal truncation.
    return `${firstLine}\u2026`;
  }
  // Long first line: cap on a word boundary where possible, then append an ellipsis.
  const hardCap = firstLine.slice(0, MAX_PROBLEM_DETAIL_LENGTH);
  const wordSafe = hardCap.replace(/\s+\S*$/, '').trim();
  return `${(wordSafe.length > 0 ? wordSafe : hardCap).trim()}\u2026`;
}

/**
 * Produce a concise, user-facing message from an RFC 7807 {@link ProblemDetails} for display in an
 * error banner.
 *
 * Resolution order (backward-compatible with the previous `detail ?? title ?? fallback` behaviour
 * for short, single-line details):
 *   1. A concise `detail` (a verbose/multi-line server `detail` is condensed first).
 *   2. The short `title`.
 *   3. The caller-supplied `fallback`.
 *
 * @param problem The RFC 7807 problem (may be `null`/`undefined`).
 * @param fallback Message shown when neither a usable detail nor a title is present.
 * @returns A concise, viewport-safe message suitable for a UI alert.
 */
export function summarizeProblem(
  problem: ProblemDetails | null | undefined,
  fallback: string,
): string {
  const detail = condenseProblemDetail(problem?.detail);
  if (detail.length > 0) {
    return detail;
  }
  const title = (problem?.title ?? '').trim();
  if (title.length > 0) {
    return title;
  }
  return fallback;
}

/** A single permitted query-string value. */
export type QueryParamValue = string | number | boolean;

/** Query-string parameters accepted by the generic verbs (scalars or arrays). */
export type QueryParams = Record<
  string,
  QueryParamValue | ReadonlyArray<QueryParamValue> | null | undefined
>;

/**
 * ApiService - the SINGLE conduit for ALL HTTP traffic in the SPA.
 *
 * Responsibilities:
 *  - Centralizes URL composition. `environment.apiUrl` ends at `/api` (NOT `/api/v1`):
 *      - resources are versioned:   `${apiUrl}/v1/<entity>[/<id>]`
 *      - auth is UNVERSIONED:        `${apiUrl}/auth/<action>`
 *      - health is at SERVER ROOT:   `<origin>/health` (NOT composed from apiUrl)
 *  - Unwraps the `{ data, meta }` success envelope: verbs return `data`; `getList` also exposes `meta`.
 *  - Surfaces RFC 7807 Problem Details on error (including the `errors` field for form binding).
 *
 * It does NOT add the `Authorization: Bearer` header or perform 401->refresh retry - that is the
 * responsibility of `core/auth/auth.interceptor.ts`, registered at the HttpClient layer.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  /** Base API URL. CRITICAL: ends at `/api` (NOT `/api/v1`). */
  private readonly baseUrl = environment.apiUrl;

  // --- URL composition (centralized so callers never hardcode `/v1` or `/auth`) ---

  /** Versioned resource URL: `${apiUrl}/v1/<entity>` (and optionally `/<id>`). */
  resourceUrl(entity: string, id?: string | number): string {
    const base = `${this.baseUrl}/v1/${entity}`;
    return id === undefined ? base : `${base}/${id}`;
  }

  /** Unversioned auth URL: `${apiUrl}/auth/<action>`. */
  authUrl(action: string): string {
    return `${this.baseUrl}/auth/${action}`;
  }

  /** Server-root health URL (NOT composed from apiUrl/v1). */
  healthUrl(): string {
    return `${this.baseUrl.replace(/\/api\/?$/, '')}/health`;
  }

  // --- Generic typed verbs: unwrap `{ data, meta }`; surface ProblemDetails on error ---

  /**
   * GET a single resource and unwrap its `{ data }` envelope.
   *
   * `withCredentials` (default `false`) opts the request into sending/receiving cookies
   * (e.g. the `HttpOnly` refresh cookie) for cross-origin calls; required by the auth
   * flow (Finding CP-FINAL-2) and harmless for same-origin resource reads.
   */
  get<T>(url: string, params?: QueryParams, withCredentials = false): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(url, { params: this.toHttpParams(params), withCredentials })
      .pipe(
        map((response) => response.data),
        catchError(this.handleError),
      );
  }

  /** GET a paged list; returns both the `data` array and the paging `meta`. */
  getList<T>(url: string, params?: QueryParams): Observable<PagedResponse<T>> {
    return this.http
      .get<ApiResponse<T[]>>(url, { params: this.toHttpParams(params) })
      .pipe(
        map((response) => ({ data: response.data ?? [], meta: response.meta ?? {} })),
        catchError(this.handleError),
      );
  }

  /**
   * POST a body and unwrap the created resource from its `{ data }` envelope.
   *
   * `withCredentials` (default `false`) opts the request into sending/receiving cookies
   * (e.g. the `HttpOnly` refresh cookie) for cross-origin calls; the auth login/refresh/
   * logout calls pass `true` so the browser stores/sends the refresh cookie
   * (Finding CP-FINAL-2).
   */
  post<T>(url: string, body?: unknown, params?: QueryParams, withCredentials = false): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(url, body ?? null, { params: this.toHttpParams(params), withCredentials })
      .pipe(
        map((response) => response?.data as T),
        catchError(this.handleError),
      );
  }

  /** PUT a body and unwrap the updated resource from its `{ data }` envelope. */
  put<T>(url: string, body?: unknown, params?: QueryParams): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(url, body ?? null, { params: this.toHttpParams(params) })
      .pipe(
        map((response) => response?.data as T),
        catchError(this.handleError),
      );
  }

  /** DELETE a resource. Resolves on the 204 No Content response. */
  delete(url: string, params?: QueryParams): Observable<void> {
    return this.http
      .delete<void>(url, { params: this.toHttpParams(params) })
      .pipe(catchError(this.handleError));
  }

  /** Server health probe at the server root (`/health`); response is NOT enveloped. */
  health<T = unknown>(): Observable<T> {
    return this.http.get<T>(this.healthUrl()).pipe(catchError(this.handleError));
  }

  // --- helpers ---

  /** Convert a plain params object into HttpParams (skipping null/undefined; expanding arrays). */
  private toHttpParams(params?: QueryParams): HttpParams {
    let httpParams = new HttpParams();
    if (!params) {
      return httpParams;
    }
    for (const [key, value] of Object.entries(params)) {
      if (value === null || value === undefined) {
        continue;
      }
      if (Array.isArray(value)) {
        for (const item of value) {
          httpParams = httpParams.append(key, String(item));
        }
      } else {
        httpParams = httpParams.set(key, String(value));
      }
    }
    return httpParams;
  }

  /** Normalize an HTTP error into an RFC 7807 ProblemDetails and rethrow it. */
  private readonly handleError = (error: HttpErrorResponse): Observable<never> => {
    const body: unknown = error.error;
    if (body !== null && typeof body === 'object' && !(body instanceof ProgressEvent)) {
      return throwError(() => body as ProblemDetails);
    }
    const fallback: ProblemDetails = {
      title: error.statusText || 'Unexpected Error',
      status: error.status,
      detail: typeof body === 'string' && body.length > 0 ? body : error.message,
    };
    return throwError(() => fallback);
  };
}
