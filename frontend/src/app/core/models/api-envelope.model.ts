// MIGRATION: AAP Section 0.1.2 success envelope { data, meta } + backend ApiControllerBase response wrapping.
// No single legacy VB equivalent: DNN serialized *Info objects directly (replaced by DTO + envelope, AAP Section 0.3.3).

/**
 * Universal success envelope returned by every backend API endpoint.
 * `data` carries the payload (a single DTO, or an array for list endpoints).
 * `meta` carries pagination / count metadata for list endpoints; it is an empty object for single-resource responses.
 */
export interface ApiEnvelope<T> {
  data: T;
  meta?: Record<string, unknown>;
}

/**
 * Client-side paged-list shape assembled from a list ApiEnvelope:
 *   items    = envelope.data (the current page of T)
 *   the rest = envelope.meta (totalCount, pageIndex, pageSize, totalPages, hasPreviousPage, hasNextPage)
 *
 * MIGRATION: field-name correction -- the folder-spec illustration { items; page; pageSize; totalRecords }
 * is WRONG; the real backend PagedResult<T> (Application/DTOs/Common/PagedResult.cs) uses the fields below.
 * NOTE: pageIndex is ZERO-BASED (legacy ASP.NET MembershipProvider paging convention).
 */
export interface Paged<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
