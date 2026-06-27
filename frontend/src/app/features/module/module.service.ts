// MIGRATION: Re-expresses the DNN Admin -> Modules user controls as a typed Angular 19 REST gateway.
//   Sources: Website/admin/Modules/ModuleSettings.ascx.vb (485 lines) -- module settings + lifecycle
//            (update / move / copy / delete-all); Website/admin/Modules/Import.ascx.vb (245 lines) --
//            content import; Website/admin/Modules/Export.ascx.vb (227 lines) -- content export.
//   ALL Web Forms postback / ViewState / PortalModuleBase / Framework.Reflection / Skin.AddModuleMessage /
//   .resx-localization machinery is DISCARDED. Only the data-access and orchestration are preserved,
//   re-expressed over ApiService (AAP Section 0.7.3: an Angular service does API communication ONLY --
//   no UI, no business decisions beyond shaping requests / holding signal state).
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { finalize, tap } from 'rxjs/operators';

import { ApiService } from '../../core/services/api.service';
import type { Module, ModulePermission, Paged } from '../../core/models';

// MIGRATION: DTO drift fix (review CP3). The authoritative backend CreateModuleRequest / UpdateModuleRequest
// expose the module permission collection as `permissions` (List<ModulePermissionDto>), NOT `modulePermissions`.
// These explicit write contracts replace the prior loosely-typed `Partial<Module>` payloads so the permission
// field name AND the tenant scoping match the backend exactly. The READ shape remains `Module` (core/models),
// whose `permissions` is the scalar legacy desktop-module string -- the permission COLLECTION is write-only and
// is never read back (ModuleResponse omits it).

/**
 * Write payload for creating a module (POST /api/modules -> 201). Mirrors the backend CreateModuleRequest.
 * `portalId` + `tabId` preserve multi-tenant + page scoping (AAP Section 0.7.1); on CREATE they travel in the
 * BODY (the create endpoint binds PortalId from the request body, not a query param). Fields the generic
 * settings form does not currently surface (moduleOrder / paneName / moduleDefId / desktopModuleId) are optional.
 */
export interface ModuleCreateRequest {
  portalId: number;
  tabId: number;
  moduleDefId?: number;
  desktopModuleId?: number;
  moduleTitle?: string | null;
  paneName?: string | null;
  moduleOrder?: number;
  allTabs?: boolean;
  visibility?: number;
  alignment?: string | null;
  color?: string | null;
  border?: string | null;
  iconFile?: string | null;
  cacheTime?: number;
  header?: string | null;
  footer?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  containerSrc?: string | null;
  displayTitle?: boolean;
  displayPrint?: boolean;
  displaySyndicate?: boolean;
  inheritViewPermissions?: boolean;
  // MIGRATION: backend write field is `permissions` (List<ModulePermissionDto>), NOT `modulePermissions`.
  permissions?: ModulePermission[];
}

/**
 * Write payload for updating a module (PUT /api/modules/{id} -> 200). Mirrors the backend UpdateModuleRequest
 * (CreateModuleRequest minus portalId / moduleDefId / desktopModuleId, PLUS the allModules / isDefaultModule
 * lifecycle flags). `portalId` is NOT in the body here -- it travels as a REQUIRED query param (EnforceTenant);
 * `moduleId` is the route segment; `isDeleted` is not part of the update contract (an update keeps the module
 * non-deleted server-side). The legacy Move / Copy / Delete-all lifecycle is orchestrated server-side from the
 * `allTabs` / `tabId` / `allModules` / `isDefaultModule` end-state (see ModuleSettings.ascx.vb cmdUpdate L398-418).
 */
export interface ModuleUpdateRequest {
  tabId: number;
  moduleTitle?: string | null;
  paneName?: string | null;
  moduleOrder?: number;
  allTabs?: boolean;
  allModules?: boolean;
  isDefaultModule?: boolean;
  visibility?: number;
  alignment?: string | null;
  color?: string | null;
  border?: string | null;
  iconFile?: string | null;
  cacheTime?: number;
  header?: string | null;
  footer?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  containerSrc?: string | null;
  displayTitle?: boolean;
  displayPrint?: boolean;
  displaySyndicate?: boolean;
  inheritViewPermissions?: boolean;
  // MIGRATION: backend write field is `permissions` (List<ModulePermissionDto>), NOT `modulePermissions`.
  permissions?: ModulePermission[];
}

/**
 * Single API gateway for the Module feature (module-form + import-export workflows).
 * Canonical sibling of PortalService: holds signal state and proxies every request through ApiService.
 */
@Injectable({ providedIn: 'root' })
export class ModuleService {
  private readonly api = inject(ApiService);

  // Relative resource root (ApiService prefixes environment.apiUrl). Backend controller is /api/modules.
  private readonly resource = 'modules';

  private readonly _modules = signal<Module[]>([]);
  private readonly _selected = signal<Module | null>(null);
  private readonly _loading = signal<boolean>(false);

  /** All modules loaded for the current portal context. */
  readonly modules = this._modules.asReadonly();
  /** The module currently being edited / acted upon. */
  readonly selected = this._selected.asReadonly();
  /** True while a request is in flight. */
  readonly loading = this._loading.asReadonly();

  /**
   * Loads a page of modules scoped to a portal (GET /api/modules?portalId=&pageIndex=&pageSize=).
   * MIGRATION: preserves multi-tenant PortalId scoping (AAP Section 0.7.1) -- `portalId` is REQUIRED and
   * forwarded as a query param; the backend binds it with [BindRequired], so omitting it yields HTTP 400.
   */
  getByPortal(portalId: number, pageIndex = 0, pageSize = 20): Observable<Paged<Module>> {
    this._loading.set(true);
    return this.api.getPaged<Module>(this.resource, { portalId, pageIndex, pageSize }).pipe(
      tap((page) => this._modules.set(page.items)),
      finalize(() => this._loading.set(false)),
    );
  }

  /**
   * Loads a single module by id (GET /api/modules/{id}?portalId= -> 200).
   * Used by both module-form (pre-load settings) and import-export (read module name context).
   * MIGRATION: multi-tenant scoping fix (review CP3). The backend GET /api/modules/{id} REQUIRES `portalId`
   * ([BindRequired] + EnforceTenant, AAP Section 0.7.1); omitting it yields HTTP 400. It is forwarded as a
   * query param.
   */
  getById(id: number, portalId: number): Observable<Module> {
    this._loading.set(true);
    return this.api.get<Module>(`${this.resource}/${id}`, { portalId }).pipe(
      tap((m) => this._selected.set(m)),
      finalize(() => this._loading.set(false)),
    );
  }

  /**
   * Creates a module (POST /api/modules -> 201). The typed ModuleCreateRequest carries `portalId` + `tabId`
   * (in the body) to preserve portal / tab scoping (AAP Section 0.7.1) and the write `permissions` collection.
   */
  create(dto: ModuleCreateRequest): Observable<Module> {
    return this.api.post<Module>(this.resource, dto);
  }

  /**
   * Updates a module (PUT /api/modules/{id}?portalId= -> 200). This is the module-form save path (<- cmdUpdate).
   * The dto carries module settings incl. tabId / visibility / allTabs / `permissions` (write collection) /
   * scheduling / caching.
   * MIGRATION: multi-tenant scoping fix (review CP3) -- `portalId` is REQUIRED on the backend update endpoint
   * (EnforceTenant, AAP Section 0.7.1) and is forwarded as a query param (NOT in the body). The legacy
   * Move / Copy / Delete-all lifecycle (ModuleSettings.ascx.vb cmdUpdate_Click -> ModuleController.MoveModule /
   * CopyModule / DeleteAllModules) is orchestrated SERVER-SIDE from the updated end-state fields (`allTabs`,
   * `tabId`, `allModules`, `isDefaultModule`). The component sends the desired end-state, NOT imperative
   * move / copy calls.
   */
  update(id: number, portalId: number, dto: ModuleUpdateRequest): Observable<Module> {
    return this.api
      .put<Module>(`${this.resource}/${id}`, dto, { portalId })
      .pipe(tap((m) => this._selected.set(m)));
  }

  /**
   * Deletes a module (DELETE /api/modules/{id}?portalId= -> 204).
   * Legacy: cmdDelete -> ModuleController.DeleteTabModule(TabId, ModuleId). Named `remove` (not `delete`)
   * to avoid the reserved word.
   * MIGRATION: multi-tenant scoping fix (review CP3) -- `portalId` is REQUIRED on the backend delete endpoint
   * (EnforceTenant, AAP Section 0.7.1) and is forwarded as a query param.
   */
  remove(id: number, portalId: number): Observable<void> {
    return this.api.delete(`${this.resource}/${id}`, { portalId });
  }

  // SCOPE BOUNDARY (AAP Section 0.6.2 -- Explicitly Out of Scope): the legacy module content export / import
  // workflows (Export.ascx.vb / Import.ascx.vb -> IPortable.ExportModule / ImportModule) depend on the
  // reflection-based BusinessControllerClass module-loader, which AAP Section 0.6.2 places explicitly OUT OF
  // SCOPE. The authoritative, frozen backend (AAP Section 0.3.4) therefore exposes CRUD + by-portal / by-tab ONLY
  // -- there is deliberately NO POST /api/modules/{id}/export or /api/modules/{id}/import endpoint. Accordingly,
  // `exportContent` / `importContent` methods and `ModuleExportRequest` / `ModuleImportRequest` payload types are
  // intentionally NOT part of this service; the import-export component renders a documented scope-boundary
  // notice (NOT a deferral). Recorded in MIGRATION_NOTES.md.
}
