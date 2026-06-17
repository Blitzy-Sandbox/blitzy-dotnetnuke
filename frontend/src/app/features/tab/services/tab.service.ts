import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from '../../../core/services/api.service';
import type { CreateTab, Tab, UpdateTab } from '../models';

/**
 * TabService — the single Angular data service for the Tab/Page feature.
 *
 * All HTTP flows through the core `ApiService` (NEVER `HttpClient` directly); every method returns a
 * clean domain type (`Tab` / `number`), unwrapping the backend `{ data, meta }` envelope where
 * applicable.
 *
 * Mirrors backend `TabsController` (`/api/v1/tabs`) + `ITabService`. Behavior reference (read-only):
 * legacy `Library/Components/Tabs/TabController.vb` and `Website/admin/Tabs/*.ascx.vb`.
 *
 * PORTAL SCOPING (critical): in DotNetNuke a Tab's identity is portal-scoped, so the backend requires
 * a `portalId` query parameter on list / count / get-by-id / delete (it returns RFC 7807 400 without
 * it; portal 0 is the valid default portal). The caller supplies `portalId` from the authenticated
 * user (`AuthService.currentUser()?.portalID`). Create/Update carry `portalID` inside the DTO body
 * instead, so they take no separate query parameter.
 */
@Injectable({ providedIn: 'root' })
export class TabService {
  private readonly api = inject(ApiService);

  /** Resource segment for all tab endpoints (`/api/v1/tabs`). */
  private readonly resource = 'tabs';

  /**
   * GET `/api/v1/tabs?portalId={portalId}` — list all tabs for a portal.
   *
   * The list endpoint is UNPAGED: the wire shape is `{ data: Tab[], meta: null }`; `getList` tolerates
   * the null `meta`, and we map to the `data` array. `portalId` is REQUIRED (the backend returns 400
   * without it). MIGRATION: legacy `TabController.GetTabs(PortalId)`.
   */
  getTabs(portalId: number): Observable<Tab[]> {
    return this.api
      .getList<Tab>(this.api.resourceUrl(this.resource), { portalId })
      .pipe(map((response) => response.data));
  }

  /**
   * GET `/api/v1/tabs?portalId={portalId}&parentId={parentId}` — list only the child tabs of a parent.
   *
   * MIGRATION: legacy `TabController.GetTabsByParentId(ParentId, PortalId)`. Both ids are required;
   * `portalId` scopes the query and `parentId` selects the children.
   */
  getTabsByParent(parentId: number, portalId: number): Observable<Tab[]> {
    return this.api
      .getList<Tab>(this.api.resourceUrl(this.resource), { portalId, parentId })
      .pipe(map((response) => response.data));
  }

  /**
   * GET `/api/v1/tabs/count?portalId={portalId}` — count the tabs in a portal.
   *
   * The scalar count is wrapped in the `{ data }` envelope; `get<number>` unwraps it. The literal
   * `count` segment cannot collide with the `{id:int}` route because "count" is not an integer.
   * MIGRATION: legacy `TabController.GetTabCount(portalId)`.
   */
  getTabCount(portalId: number): Observable<number> {
    return this.api.get<number>(`${this.api.resourceUrl(this.resource)}/count`, { portalId });
  }

  /**
   * GET `/api/v1/tabs/{id}?portalId={portalId}` — a single portal-scoped tab.
   *
   * MIGRATION: legacy `TabController.GetTab(TabId, PortalId)` takes BOTH identifiers because a tab is
   * scoped to its portal; `portalId` is REQUIRED (the backend returns 400 without it).
   */
  getTab(id: number, portalId: number): Observable<Tab> {
    return this.api.get<Tab>(this.api.resourceUrl(this.resource, id), { portalId });
  }

  /**
   * POST `/api/v1/tabs` — create a tab (201).
   *
   * The owning portal travels INSIDE the body (`CreateTab.portalID`), so no query parameter is sent.
   * MIGRATION: legacy `TabController.AddTab(TabInfo)`.
   */
  createTab(dto: CreateTab): Observable<Tab> {
    return this.api.post<Tab>(this.api.resourceUrl(this.resource), dto);
  }

  /**
   * PUT `/api/v1/tabs/{id}` — update a tab (200).
   *
   * The backend guards that the path `id` equals the body `tabID` (else 400); the caller MUST set
   * `dto.tabID === id`. The owning portal travels inside the body. MIGRATION: legacy
   * `TabController.UpdateTab(TabInfo)`.
   */
  updateTab(id: number, dto: UpdateTab): Observable<Tab> {
    return this.api.put<Tab>(this.api.resourceUrl(this.resource, id), dto);
  }

  /**
   * DELETE `/api/v1/tabs/{id}?portalId={portalId}` — delete a tab (204).
   *
   * Tab deletion is a SOFT delete server-side (the service/repository sets the deleted flag).
   * `portalId` is REQUIRED because tab identity is portal-scoped. MIGRATION: legacy
   * `TabController.DeleteTab(TabId, PortalId)`.
   */
  deleteTab(id: number, portalId: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id), { portalId });
  }
}
