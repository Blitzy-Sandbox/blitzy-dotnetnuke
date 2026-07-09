import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { ApiMeta, ApiResponse, ProblemDetails } from '../models';

/**
 * Query-string parameters accepted by list/read endpoints. Values are coerced
 * to strings; array values produce repeated query keys (e.g. `?id=1&id=2`).
 */
export type QueryParams = Record<
  string,
  string | number | boolean | ReadonlyArray<string | number | boolean>
>;

/**
 * A list payload together with its response metadata (pagination / correlationId).
 */
export interface ListResult<T> {
  data: T[];
  meta?: ApiMeta;
}

/**
 * ApiService — the SPA's single HTTP gateway to the ASP.NET Core BFF API.
 *
 * MIGRATION: replaces the legacy DotNetNuke data-access facade. In the legacy
 * stack every call funnelled through
 * `SqlDataProvider.Instance().Execute*(...)` -> `SqlHelper.Execute*` stored
 * procedures (Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb)
 * against the abstract `DataProvider` contract
 * (Library/Components/Providers/Data/DataProvider.vb). The ADO.NET / DataSet /
 * DES machinery is NOT ported; instead every network call in the SPA flows
 * through this thin, typed HttpClient wrapper. The legacy generic
 * `ExecuteScalar(Of T)` becomes the generic getById<T>/create<T>/update<T>
 * helpers below.
 *
 * Responsibilities (transport / orchestration ONLY — never business logic):
 *   - resolve the API base URL from the environment (never hardcoded);
 *   - unwrap the success envelope `{ data, meta }` (AAP §0.7.2) so callers get T;
 *   - normalize error bodies to RFC 7807 ProblemDetails and rethrow.
 *
 * Auth-agnostic: the JWT bearer token is attached centrally by
 * core/auth/auth.interceptor.ts, NOT here.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  /**
   * API base URL from the environment. Dev: `http://localhost:8080/api`;
   * prod (after angular.json fileReplacement): `/api`. Already includes `/api`.
   */
  private readonly baseUrl = environment.apiBaseUrl;

  // ---- Generic CRUD helpers (consumed by every features/* domain service) ----

  /**
   * GET {baseUrl}/{resource} — list endpoint. Supports search / filter / paging
   * query params (`GET /api/{entity}?query=...`). Unwraps the envelope to `T[]`.
   */
  getList<T>(resource: string, params?: QueryParams): Observable<T[]> {
    return this.get<T[]>(resource, params);
  }

  /**
   * GET {baseUrl}/{resource} — list endpoint that also exposes the `meta`
   * envelope (pagination / correlationId) for list screens that need it.
   */
  getListWithMeta<T>(resource: string, params?: QueryParams): Observable<ListResult<T>> {
    return this.http
      .get<ApiResponse<T[]>>(this.buildUrl(resource), { params: this.toHttpParams(params) })
      .pipe(
        map((res) => ({ data: res.data, meta: res.meta })),
        catchError(this.handleError)
      );
  }

  /** GET {baseUrl}/{resource}/{id} — single resource by id. */
  getById<T>(resource: string, id: number | string): Observable<T> {
    return this.get<T>(this.join(resource, id));
  }

  /** POST {baseUrl}/{resource} — create a resource (expects HTTP 201). */
  create<T>(resource: string, body: unknown): Observable<T> {
    return this.post<T>(resource, body);
  }

  /** PUT {baseUrl}/{resource}/{id} — full update of a resource (expects HTTP 200). */
  update<T>(resource: string, id: number | string, body: unknown): Observable<T> {
    return this.put<T>(this.join(resource, id), body);
  }

  /** PATCH {baseUrl}/{resource}/{id} — partial update of a resource. */
  patchById<T>(resource: string, id: number | string, body: unknown): Observable<T> {
    return this.patch<T>(this.join(resource, id), body);
  }

  /** DELETE {baseUrl}/{resource}/{id} — remove a resource (expects HTTP 204, no body). */
  delete(resource: string, id: number | string): Observable<void> {
    // buildUrl() must wrap join() so the DELETE hits the absolute API URL
    // ({baseUrl}/{resource}/{id}, AAP §0.7.2), consistent with getById/update/
    // patchById; without it the request would resolve relative to the SPA origin.
    return this.http
      .delete<void>(this.buildUrl(this.join(resource, id)))
      .pipe(
        map(() => undefined),
        catchError(this.handleError)
      );
  }

  // ---- Low-level, envelope-aware verb primitives the helpers build upon.
  //      Used directly for non-CRUD routes, e.g. core/auth (auth/login, auth/me). ----

  /** GET a path relative to the API base, unwrapping the `{ data }` envelope. */
  get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(this.buildUrl(path), { params: this.toHttpParams(params) })
      .pipe(
        map((res) => res.data),
        catchError(this.handleError)
      );
  }

  /** POST to a path relative to the API base, unwrapping the `{ data }` envelope. */
  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(this.buildUrl(path), body)
      .pipe(
        map((res) => res.data),
        catchError(this.handleError)
      );
  }

  /**
   * POST to a path relative to the API base for endpoints that return HTTP 204
   * No Content, resolving to `void`.
   *
   * MIGRATION (Checkpoint-8 API-contract finding): backend command endpoints such
   * as `POST auth/logout` and `POST users/{id}/change-password` return 204 with an
   * EMPTY body — there is no `{ data, meta }` envelope to unwrap. The generic
   * `post<T>()` above maps `res.data`, which throws on a 204's `null` body and
   * makes a SUCCESSFUL command surface as a client-side error. This helper never
   * dereferences the response body (mirroring `delete()`, which already handles
   * 204 correctly), so no-content commands resolve cleanly to `void`. Errors are
   * still normalized to RFC 7807 ProblemDetails by the shared handler and rethrown.
   */
  postNoContent(path: string, body: unknown): Observable<void> {
    return this.http
      .post<null>(this.buildUrl(path), body)
      .pipe(
        map(() => undefined),
        catchError(this.handleError)
      );
  }

  /** PUT to a path relative to the API base, unwrapping the `{ data }` envelope. */
  put<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(this.buildUrl(path), body)
      .pipe(
        map((res) => res.data),
        catchError(this.handleError)
      );
  }

  /** PATCH a path relative to the API base, unwrapping the `{ data }` envelope. */
  patch<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .patch<ApiResponse<T>>(this.buildUrl(path), body)
      .pipe(
        map((res) => res.data),
        catchError(this.handleError)
      );
  }

  // ---- Internal helpers ----

  /** Join the base URL and a resource/path, trimming stray slashes. */
  private buildUrl(pathOrResource: string): string {
    const trimmed = pathOrResource.replace(/^\/+|\/+$/g, '');
    return `${this.baseUrl}/${trimmed}`;
  }

  /** Append an id segment to a resource, url-encoding the id. */
  private join(resource: string, id: number | string): string {
    return `${resource.replace(/\/+$/g, '')}/${encodeURIComponent(String(id))}`;
  }

  /** Convert a plain params object into HttpParams (arrays -> repeated keys). */
  private toHttpParams(params?: QueryParams): HttpParams | undefined {
    if (!params) {
      return undefined;
    }
    let httpParams = new HttpParams();
    for (const [key, value] of Object.entries(params)) {
      if (value === undefined || value === null) {
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

  /**
   * MIGRATION: legacy server-side failures (ASPX error pages / postback
   * exceptions) are replaced by RFC 7807 Problem Details. Normalize any
   * HttpErrorResponse into a ProblemDetails and rethrow so the auth interceptor
   * and feature components can react. Errors are never swallowed here.
   */
  private readonly handleError = (error: HttpErrorResponse): Observable<never> => {
    const body = error.error as Partial<ProblemDetails> | string | null | undefined;
    let problem: ProblemDetails;

    if (body && typeof body === 'object' && typeof body.status === 'number') {
      // Backend already returned RFC 7807 — pass it through, filling any gaps.
      problem = {
        type: body.type ?? 'about:blank',
        title: body.title ?? error.statusText ?? 'Error',
        status: body.status,
        detail: body.detail,
        instance: body.instance,
        errors: body.errors,
      };
    } else {
      // Network failure or non-Problem body — synthesize a ProblemDetails.
      problem = {
        type: 'about:blank',
        title: error.statusText || 'Http Error',
        status: error.status,
        detail: typeof body === 'string' && body.length > 0 ? body : error.message,
      };
    }

    return throwError(() => problem);
  };
}
