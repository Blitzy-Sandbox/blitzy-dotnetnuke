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
 * MIGRATION: Portal CREATE payload. Mirrors the authoritative backend `CreatePortalRequest` DTO + the
 * `CreatePortalValidator` REQUIRED set (AAP Section 0.3.4). The legacy Signup workflow
 * (Website/admin/Portal/Signup.ascx.vb) provisioned a new portal AND its first administrator account in a single
 * step, so the backend create contract REQUIRES the portal `email` PLUS the administrator bootstrap fields
 * (adminUsername / adminPassword / adminFirstName / adminLastName / adminEmail). Those admin* fields are
 * write-only provisioning inputs that are NOT part of the Portal read projection, so they are declared
 * explicitly here rather than Picked from `Portal`. NOTE: `processorPassword`, `administratorId`, the tab-id
 * fields, `siteLogHistory`, `paymentProcessor`/`processorUserId`, and `backgroundFile` are NOT accepted on
 * create (see UpdatePortalRequest for the update-only fields).
 */
export interface CreatePortalRequest {
  portalName: string;
  description?: string | null;
  keyWords?: string | null;
  logoFile?: string | null;
  footerText?: string | null;
  expiryDate?: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  currency?: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  // MIGRATION: REQUIRED by CreatePortalValidator (Must(!IsNullOrEmpty)). Omitting it previously caused the
  // backend create to fail with a 400 validation error (CP3 review finding, portal-form L230-237).
  email: string;
  defaultLanguage?: string | null;
  timeZoneOffset: number;
  homeDirectory?: string | null;
  // MIGRATION: administrator-account bootstrap group — all REQUIRED by CreatePortalValidator.
  adminUsername: string;
  adminPassword: string;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
}

/**
 * MIGRATION: Portal UPDATE payload. Mirrors the authoritative backend `UpdatePortalRequest` DTO (SiteSettings
 * cmdUpdate_Click -> PortalController.UpdatePortalInfo, L772-781). Carries the editable site-settings fields
 * plus the WRITE-ONLY `processorPassword` (the PortalDto read projection OMITS processorPassword, so it can only
 * be SET here and is never read back — see portal.model.ts). `email` is intentionally NOT part of the update
 * contract (it is a create-only provisioning input). `portalId` travels in the URL and is also echoed in the
 * body to match the backend DTO shape.
 */
export interface UpdatePortalRequest {
  portalId: number;
  portalName: string;
  logoFile?: string | null;
  footerText?: string | null;
  expiryDate?: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  currency?: string | null;
  administratorId: number;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  paymentProcessor?: string | null;
  processorUserId?: string | null;
  processorPassword?: string | null;
  description?: string | null;
  keyWords?: string | null;
  backgroundFile?: string | null;
  siteLogHistory: number;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage?: string | null;
  timeZoneOffset: number;
  homeDirectory?: string | null;
}

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
   * The legacy "All" filter maps to an omitted filter (L351-353). MIGRATION: the legacy "Expired" filter and the
   * bulk "Delete Expired" action are NOT migrated — the frozen backend portal contract (AAP Section 0.3.4) exposes
   * CRUD only, with no `portals/expired` endpoint; both are out of scope for this migration (CRUD-only contract)
   * and recorded in MIGRATION_NOTES.md.
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

  /** MIGRATION: portal creation (legacy Signup / PortalController.CreatePortal) -> POST /portals (201).
   *  Takes a CreatePortalRequest (REQUIRES email + admin* bootstrap fields per CreatePortalValidator). */
  create(portal: CreatePortalRequest): Observable<Portal> {
    return this.api.post<Portal>('portals', portal);
  }

  /** MIGRATION: SiteSettings cmdUpdate_Click -> PortalController.UpdatePortalInfo (L772-781) -> PUT /portals/{id} (200).
   *  Takes an UpdatePortalRequest (carries write-only processorPassword; NO email). */
  update(id: number | string, portal: UpdatePortalRequest): Observable<Portal> {
    return this.api.put<Portal>(`portals/${id}`, portal);
  }

  /** MIGRATION: Portals.ascx.vb grdPortals_DeleteCommand -> PortalController.DeletePortal (L388-409) -> DELETE /portals/{id} (204). */
  delete(id: number | string): Observable<void> {
    return this.api.delete(`portals/${id}`);
  }

  // MIGRATION: the legacy Portals.ascx.vb "Expired" filter (BindData L138-140 GetExpiredPortals) and the bulk
  // "Delete Expired" ModuleAction (L379-386 / L189-198, PortalController.DeleteExpiredPortals) are NOT migrated.
  // The frozen backend portal contract (AAP Section 0.3.4) exposes CRUD only and enumerates NO `portals/expired`
  // endpoint; adding one would violate the authoritative API surface. Per the AAP precedence rule (align the
  // frontend to the frozen contract rather than inventing backend endpoints), the corresponding `getExpired()` /
  // `deleteExpired()` service methods and their UI affordances are deliberately not part of this migration's
  // frozen, CRUD-only portal contract; this scope decision is recorded in MIGRATION_NOTES.md.
}
