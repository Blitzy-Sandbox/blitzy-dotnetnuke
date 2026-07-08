import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, ListResult, QueryParams } from '../../core/services/api.service';
import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../../core/models';

/**
 * PortalService — thin, API-only orchestration over {@link ApiService} for the
 * `portals` REST resource. Consumed by PortalListComponent and PortalFormComponent.
 *
 * MIGRATION: replaces the data-access surface of the legacy DotNetNuke
 * PortalController (Library/Components/Portal/PortalController.vb) that backed the
 * Web Forms admin screens (Website/admin/Portal/Portals.ascx.vb,
 * Signup.ascx.vb, SiteSettings.ascx.vb). Per AAP §0.7.1 this service performs API
 * communication only (no business logic); ApiService unwraps the { data, meta }
 * envelope and surfaces RFC 7807 ProblemDetails on error.
 */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly api = inject(ApiService);

  /** REST resource segment. ApiService.baseUrl already includes `/api`. */
  private readonly resource = 'portals';

  /**
   * GET /api/portals (optional `?query=`/paging params).
   * MIGRATION: PortalController.GetPortalsByName(filter, page, size) / GetExpiredPortals().
   */
  list(params?: QueryParams): Observable<Portal[]> {
    return this.api.getList<Portal>(this.resource, params);
  }

  /**
   * GET /api/portals returning the pagination/correlation `meta` alongside data.
   * MIGRATION: server-side paging that legacy Portals.ascx.vb drove via ctlPagingControl.
   */
  listWithMeta(params?: QueryParams): Observable<ListResult<Portal>> {
    return this.api.getListWithMeta<Portal>(this.resource, params);
  }

  /** GET /api/portals/{id}. MIGRATION: PortalController.GetPortal(portalId). */
  getById(id: number): Observable<Portal> {
    return this.api.getById<Portal>(this.resource, id);
  }

  /** POST /api/portals. MIGRATION: PortalController.CreatePortal(...) from Signup.ascx.vb. */
  create(request: CreatePortalRequest): Observable<Portal> {
    return this.api.create<Portal>(this.resource, request);
  }

  /** PUT /api/portals/{id}. MIGRATION: PortalController.UpdatePortalInfo(...) from SiteSettings.ascx.vb. */
  update(id: number, request: UpdatePortalRequest): Observable<Portal> {
    return this.api.update<Portal>(this.resource, id, request);
  }

  /** DELETE /api/portals/{id}. MIGRATION: PortalController.DeletePortal(portal) grid delete command. */
  remove(id: number): Observable<void> {
    return this.api.delete(this.resource, id);
  }
}
