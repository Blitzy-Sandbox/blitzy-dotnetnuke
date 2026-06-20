import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse } from '../../../core/services/api.service';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../models';

/**
 * PortalService - the Portal feature's single data-access seam to the backend
 * `/api/v1/portals` REST API. A thin delegation layer over the core `ApiService`;
 * ALL HTTP is routed through `ApiService` (never `HttpClient` directly).
 *
 * Source lineage (read-as-reference, NOT converted 1:1):
 *   Library/Components/Portal/PortalController.vb
 *   (GetPortals, GetPortal, GetPortalsByName, CreatePortal, UpdatePortalInfo, DeletePortalInfo).
 * Authoritative REST contract: backend PortalsController (`/api/v1/portals`).
 */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly api = inject(ApiService);

  /** REST resource segment for the Portal endpoints (`/api/v1/portals`). */
  private readonly resource = 'portals';

  /**
   * Paged search of portals; used when a letter filter / search term is active.
   * MIGRATION: legacy PortalController.GetPortalsByName(Filter & "%", CurrentPage - 1, PageSize)
   * -> GET /api/v1/portals?query=&pageIndex=&pageSize=. `pageIndex` is 0-based.
   */
  getPortals(query?: string, pageIndex = 0, pageSize = 10): Observable<PagedResponse<Portal>> {
    return this.api.getList<Portal>(this.api.resourceUrl(this.resource), {
      query,
      pageIndex,
      pageSize,
    });
  }

  /**
   * Unfiltered list of all portals (the "All" view).
   * MIGRATION: the legacy "All" path called the paged GetPortalsByName with pageIndex = -1
   * (pageSize = Integer.MaxValue); the new API exposes an unpaged GetAll that returns the bare
   * `{ data }` envelope (no `meta`), which `ApiService.get<T>` unwraps to `Portal[]`.
   */
  getAllPortals(): Observable<Portal[]> {
    return this.api.get<Portal[]>(this.api.resourceUrl(this.resource));
  }

  /** Load a single portal by id. Legacy: PortalController.GetPortal(PortalId). */
  getPortal(id: number): Observable<Portal> {
    return this.api.get<Portal>(this.api.resourceUrl(this.resource, id));
  }

  /** Create a portal (HTTP 201). Legacy: PortalController.CreatePortal(...). */
  createPortal(request: CreatePortalRequest): Observable<Portal> {
    return this.api.post<Portal>(this.api.resourceUrl(this.resource), request);
  }

  /**
   * Update a portal (HTTP 200). Legacy: PortalController.UpdatePortalInfo(...).
   * The caller must ensure `request.portalID === id` (the server validates the match).
   */
  updatePortal(id: number, request: UpdatePortalRequest): Observable<Portal> {
    return this.api.put<Portal>(this.api.resourceUrl(this.resource, id), request);
  }

  /**
   * Delete a portal (HTTP 204). Legacy: PortalController.DeletePortalInfo(PortalId).
   * MIGRATION: the legacy hard delete + cascade (skin assignments, portal users, portal row)
   * is a SERVER concern; the client merely issues DELETE and the API performs the cascade.
   * NOTE: there is no "delete expired"/bulk endpoint and no getByAlias server-side; the
   * "Expired" filter is applied CLIENT-SIDE in the list component on `Portal.expiryDate`.
   */
  deletePortal(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }
}
