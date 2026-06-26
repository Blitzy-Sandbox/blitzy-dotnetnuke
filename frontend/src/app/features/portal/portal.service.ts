// MIGRATION: Website/admin/Portal/Portals.ascx.vb (host portal list: paging/filter/delete, 447 lines) +
// SiteSettings.ascx.vb (load/create/update site settings; cmdUpdate_Click -> PortalController.UpdatePortalInfo
// L772-781, 896 lines) -> signal-based PortalService. Web Forms postback/ViewState/PortalModuleBase machinery
// is discarded; this service holds API communication + the legacy controls' orchestration only (AAP Section 0.7.3).
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { finalize, tap } from 'rxjs/operators';

import { ApiService, type ApiQueryParams } from '../../core/services/api.service';
import type { Paged, Portal } from '../../core/models';

/**
 * Editable portal field set persisted by the legacy SiteSettings cmdUpdate_Click -> PortalController.UpdatePortalInfo
 * (Website/admin/Portal/SiteSettings.ascx.vb L772-781). PortalId is supplied via the route on update and omitted here.
 * Used for BOTH create (POST -> 201) and edit (PUT -> 200); the portal-form decides the mode by route.
 */
export type PortalRequest = Pick<
  Portal,
  | 'portalName'
  | 'description'
  | 'keyWords'
  | 'footerText'
  | 'logoFile'
  | 'backgroundFile'
  | 'userRegistration'
  | 'bannerAdvertising'
  | 'currency'
  | 'administratorId'
  | 'hostFee'
  | 'hostSpace'
  | 'pageQuota'
  | 'userQuota'
  | 'siteLogHistory'
  | 'expiryDate'
  | 'paymentProcessor'
  | 'processorUserId'
  | 'processorPassword'
  | 'splashTabId'
  | 'homeTabId'
  | 'loginTabId'
  | 'userTabId'
  | 'defaultLanguage'
  | 'timeZoneOffset'
  | 'homeDirectory'
>;

@Injectable({ providedIn: 'root' })
export class PortalService {
  // MIGRATION: DataProvider.Instance() reflection singleton -> injected ApiService. Feature services talk ONLY to
  // ApiService, never HttpClient directly (AAP Section 0.7.3).
  private readonly api = inject(ApiService);

  // --- Signal state (replaces Web Forms ViewState / grid DataSource) ---
  private readonly _portals = signal<Portal[]>([]);
  private readonly _loading = signal(false);
  private readonly _totalCount = signal(0);
  private readonly _selected = signal<Portal | null>(null);

  // --- Read-only views consumed by the portal components ---
  readonly portals = this._portals.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly selected = this._selected.asReadonly();

  /**
   * MIGRATION: Portals.ascx.vb BindData L142 GetPortalsByName(Filter + "%", CurrentPage - 1, PageSize, TotalRecords).
   * pageIndex is ZERO-BASED (matches Paged.pageIndex and DataTableComponent currentPage/pageChange -> wire directly).
   * The legacy "All" filter maps to an omitted filter (L351-353); the "Expired" filter is served by getExpired().
   */
  list(pageIndex: number, pageSize: number, filter?: string): Observable<Paged<Portal>> {
    this._loading.set(true);
    const params: ApiQueryParams = filter
      ? { pageIndex, pageSize, filter }
      : { pageIndex, pageSize };
    return this.api.getPaged<Portal>('portals', params).pipe(
      tap((paged) => {
        this._portals.set(paged.items);
        this._totalCount.set(paged.totalCount);
      }),
      finalize(() => this._loading.set(false)),
    );
  }

  /** MIGRATION: SiteSettings.ascx.vb Page_Load objPortalController.GetPortal(intPortalId) (L266) -> GET /portals/{id} (200). */
  getById(id: number | string): Observable<Portal> {
    this._loading.set(true);
    return this.api.get<Portal>(`portals/${id}`).pipe(
      tap((portal) => this._selected.set(portal)),
      finalize(() => this._loading.set(false)),
    );
  }

  /** MIGRATION: portal creation (legacy Signup / PortalController.CreatePortal) -> POST /portals (201). */
  create(portal: PortalRequest): Observable<Portal> {
    return this.api.post<Portal>('portals', portal);
  }

  /** MIGRATION: SiteSettings cmdUpdate_Click -> PortalController.UpdatePortalInfo (L772-781) -> PUT /portals/{id} (200). */
  update(id: number | string, portal: PortalRequest): Observable<Portal> {
    return this.api.put<Portal>(`portals/${id}`, portal);
  }

  /** MIGRATION: Portals.ascx.vb grdPortals_DeleteCommand -> PortalController.DeletePortal (L388-409) -> DELETE /portals/{id} (204). */
  delete(id: number | string): Observable<void> {
    return this.api.delete(`portals/${id}`);
  }

  /**
   * MIGRATION: Portals.ascx.vb BindData L138-140 GetExpiredPortals() ("Expired" filter; legacy hides the pager).
   * No dedicated endpoint is enumerated in AAP Section 0.3.4 (CRUD only); mapped to the 'portals/expired' sub-resource.
   * Flagged for MIGRATION_NOTES.md / backend coordination.
   */
  getExpired(): Observable<Portal[]> {
    this._loading.set(true);
    return this.api.get<Portal[]>('portals/expired').pipe(
      tap((portals) => {
        this._portals.set(portals);
        this._totalCount.set(portals.length);
      }),
      finalize(() => this._loading.set(false)),
    );
  }

  /**
   * MIGRATION: Portals.ascx.vb ModuleAction "Delete" -> PortalController.DeleteExpiredPortals() (L379-386 / L189-198).
   * Preserved as a single bulk DELETE 'portals/expired' (NOT a client-side delete loop -> avoids new business logic).
   * Flagged for MIGRATION_NOTES.md / backend coordination.
   */
  deleteExpired(): Observable<void> {
    return this.api.delete('portals/expired');
  }
}
