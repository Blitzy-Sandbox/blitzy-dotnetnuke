/**
 * Shared API envelope and error contracts for the DnnMigration SPA.
 *
 * MIGRATION: replaces the legacy DotNetNuke Web Forms postback/ViewState
 * response model. The ASP.NET Core BFF wraps every successful payload in a
 * { data, meta } envelope (AAP §0.7.2) and returns RFC 7807 Problem Details
 * for errors. No legacy VB type maps 1:1 to these shapes — they are defined
 * from the migration API standards.
 */

/**
 * Standard success envelope returned by all API endpoints:
 * `{ "data": {...}, "meta": {...} }`.
 * @typeParam T The payload type carried in `data` (e.g. `Portal`, `Portal[]`).
 */
export interface ApiResponse<T> {
  /** The response payload. */
  data: T;
  /**
   * Response metadata. ALWAYS present: the ASP.NET Core BFF (`ApiControllerBase`) includes a
   * `meta` object on every success envelope, substituting default metadata (e.g. the request
   * correlation id) when no pagination is computed. Marking it required keeps the client contract
   * aligned with the guaranteed `{ data, meta }` server shape (AAP §0.7.2).
   */
  meta: ApiMeta;
}

/**
 * Response metadata: pagination fields for list endpoints plus the
 * per-request correlation id echoed by the API (AAP §0.7.1).
 */
export interface ApiMeta {
  page?: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
  correlationId?: string;
}

/**
 * RFC 7807 Problem Details error body (AAP §0.7.2 error example).
 * `errors` carries per-field validation messages produced by
 * FluentValidation / ASP.NET Core ValidationProblemDetails.
 */
export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}
