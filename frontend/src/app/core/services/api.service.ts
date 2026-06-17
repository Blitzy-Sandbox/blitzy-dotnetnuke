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

  /** GET a single resource and unwrap its `{ data }` envelope. */
  get<T>(url: string, params?: QueryParams): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(url, { params: this.toHttpParams(params) })
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
   * `withCredentials` (default false) makes the browser SEND and STORE cookies for this
   * call. It is required ONLY by the auth flow (login/refresh/logout) so the HttpOnly
   * refresh-token cookie issued by `AuthController` is set on login/refresh, sent back on
   * refresh/logout, and deleted on logout. Resource POSTs leave it false (Bearer-only),
   * matching the CORS allow-list which permits credentials for the Angular origin only.
   */
  post<T>(
    url: string,
    body?: unknown,
    params?: QueryParams,
    withCredentials = false,
  ): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(url, body ?? null, {
        params: this.toHttpParams(params),
        withCredentials,
      })
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
    // A status of 0 indicates a transport-level failure (offline, connection
    // refused, DNS failure, CORS preflight rejection, or request timeout) where
    // no HTTP response was ever received. Angular surfaces these as a raw,
    // developer-oriented message such as
    // "Http failure response for <url>: 0 Unknown Error".
    // Map every such case to a single, friendly, user-facing message so that any
    // screen rendering ProblemDetails.detail/title (login, create/edit forms,
    // list screens) shows consistent network guidance instead of leaking
    // transport internals. This is the centralized fix for the offline/network
    // error-message findings (QA Role Issue #6 / Auth Issue #2).
    if (error.status === 0) {
      const networkError: ProblemDetails = {
        title: 'Network Unavailable',
        status: 0,
        detail: 'Network unavailable. Please check your connection and try again.',
      };
      return throwError(() => networkError);
    }
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
