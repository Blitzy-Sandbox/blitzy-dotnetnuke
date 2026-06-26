// MIGRATION: Library/Components/Providers/Data/DataProvider.vb (DotNetNuke.Data.DataProvider) + SqlHelper
// (Microsoft.ApplicationBlocks.Data) -> ApiService.
// The legacy MustInherit DataProvider was reflection-instantiated (Framework.Reflection.CreateObject) into a
// singleton (DataProvider.Instance()) and was the ONE gateway every *Controller.vb used to reach SQL Server
// via stored procedures (ExecuteReader/ExecuteNonQuery/ExecuteScalar/ExecuteDataSet). This typed REST client
// is the frontend analog: the ONLY place the SPA talks to HttpClient for resource CRUD. Every features/* and
// core/auth service builds on it and MUST NOT call HttpClient directly. API communication only -- no business
// logic, no UI, no state.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import type { ApiEnvelope, Paged } from '../models';

/**
 * Query-string parameters accepted by the GET helpers. Forwarded directly to Angular's HttpClient
 * `params` option, which serializes them into the query string. Strictly typed -- never `any`.
 */
export type ApiQueryParams = Record<
  string,
  string | number | boolean | ReadonlyArray<string | number | boolean>
>;

// MIGRATION: /v1 routing discrepancy -- the frontend roots ALL requests at environment.apiUrl (/api/v1), but
// the backend controllers route at /api/... (no /v1 segment; backend chose /api/... for Gate 5 + resource-table
// parity over the /api/v1 NFR). nginx reconciles /api in production (docker/nginx.conf); dev points directly at
// Kestrel (http://localhost:8080). Flagged for MIGRATION_NOTES.md / integration testing. Do NOT hardcode /api
// to bypass environment.apiUrl.
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /** GET a single resource (or unpaged list); unwraps the { data, meta } envelope and returns `data` as T. */
  get<T>(path: string, params?: ApiQueryParams): Observable<T> {
    return this.http
      .get<ApiEnvelope<T>>(this.buildUrl(path), { params })
      .pipe(map((envelope) => envelope.data));
  }

  /** GET a paged list; combines envelope `data` (-> items) and envelope `meta` (paging fields) into Paged<T>. */
  getPaged<T>(path: string, params?: ApiQueryParams): Observable<Paged<T>> {
    return this.http
      .get<ApiEnvelope<T[]>>(this.buildUrl(path), { params })
      .pipe(map((envelope) => this.toPaged(envelope)));
  }

  /** POST (expects 201 Created); unwraps the envelope and returns the created resource `data` as T. */
  post<T>(path: string, body: unknown): Observable<T> {
    return this.http
      .post<ApiEnvelope<T>>(this.buildUrl(path), body)
      .pipe(map((envelope) => envelope.data));
  }

  /**
   * PUT (expects 200 OK); unwraps the envelope and returns the updated resource `data` as T.
   * MIGRATION: the optional `params` forwards tenant-scoping query parameters -- specifically the REQUIRED
   * `portalId` on the tenant-scoped Users / Roles / Modules update endpoints ([BindRequired] + EnforceTenant,
   * AAP Section 0.7.1) -- through HttpClient's `params` option (correctly URL-encoded). It is omitted by
   * host-level resources (e.g. Portals), so existing callers are unaffected (`params` stays undefined ==
   * no query string).
   */
  put<T>(path: string, body: unknown, params?: ApiQueryParams): Observable<T> {
    return this.http
      .put<ApiEnvelope<T>>(this.buildUrl(path), body, { params })
      .pipe(map((envelope) => envelope.data));
  }

  /**
   * DELETE (expects 204 No Content, empty body -- no envelope to unwrap).
   * MIGRATION: the optional `params` forwards the same tenant-scoping query parameters (the REQUIRED
   * `portalId` on the tenant-scoped Users / Roles / Modules delete endpoints) via HttpClient's `params`
   * option; omitted by host-level resources, preserving prior behavior.
   */
  delete<T = void>(path: string, params?: ApiQueryParams): Observable<T> {
    return this.http.delete<T>(this.buildUrl(path), { params });
  }

  /** Joins the configured API root and a RELATIVE resource path (tolerating a leading slash on `path`). */
  private buildUrl(path: string): string {
    const relativePath = path.startsWith('/') ? path.slice(1) : path;
    return `${this.baseUrl}/${relativePath}`;
  }

  /** Assembles a Paged<T> from a list envelope: items <- envelope.data, paging fields <- envelope.meta. */
  private toPaged<T>(envelope: ApiEnvelope<T[]>): Paged<T> {
    const meta: Record<string, unknown> = envelope.meta ?? {};
    return {
      items: envelope.data,
      totalCount: this.toNumber(meta['totalCount']),
      pageIndex: this.toNumber(meta['pageIndex']),
      pageSize: this.toNumber(meta['pageSize']),
      totalPages: this.toNumber(meta['totalPages']),
      hasPreviousPage: this.toBoolean(meta['hasPreviousPage']),
      hasNextPage: this.toBoolean(meta['hasNextPage']),
    };
  }

  /** Coerces an unknown `meta` value to a number (strict-typed bridge from Record<string, unknown>). */
  private toNumber(value: unknown): number {
    return typeof value === 'number' ? value : Number(value ?? 0);
  }

  /** Coerces an unknown `meta` value to a boolean. */
  private toBoolean(value: unknown): boolean {
    return typeof value === 'boolean' ? value : value === 'true';
  }
}
