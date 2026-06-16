import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, PagedResponse, QueryParams } from '../../../core/services/api.service';
import { CreateModuleDto, Module, UpdateModuleDto } from '../models';

/**
 * Data-access service for the Module feature. Performs all Module CRUD against the
 * BFF REST API at `/api/v1/modules` by delegating to the cross-cutting core
 * {@link ApiService}; it never injects `HttpClient` directly.
 *
 * Source lineage (read-as-reference, NOT converted 1:1):
 *   Library/Components/Modules/ModuleController.vb
 *   (GetModules, GetModule, AddModule, UpdateModule, DeleteModule).
 * Authoritative REST contract: backend ModulesController (`/api/v1/modules`).
 *
 * MIGRATION: replaces the legacy in-process ModuleController.vb + ADO.NET/SqlDataProvider
 * data access (CBO reflection hydration) with stateless REST calls routed through ApiService.
 */
@Injectable({ providedIn: 'root' })
export class ModuleService {
  private readonly api = inject(ApiService);

  /** REST resource segment for the Module endpoints (`/api/v1/modules`). */
  private readonly resource = 'modules';

  /**
   * Returns the paged list of modules for a whole PORTAL (the administration grid's portal scope).
   *
   * MIGRATION: legacy ModuleController.GetModules(PortalId) -> GET /api/v1/modules?portalId=.
   * Soft-deleted modules (IsDeleted = True) are excluded SERVER-SIDE - exactly as the
   * legacy list did - so this method returns what the API sends verbatim; there is NO
   * client-side isDeleted filtering.
   *
   * Contract: the backend `GET /api/v1/modules` requires EXACTLY ONE list discriminator and
   * returns 400 ("Either portalId or tabId query parameter is required.") when neither is
   * supplied. This method always sends `portalId`, so the invalid no-filter call is
   * unrepresentable through the service API. Optional `params` carry additional query values
   * (e.g. paging) and are spread FIRST so they can never override the discriminator.
   */
  getModulesByPortal(portalId: number, params?: QueryParams): Observable<PagedResponse<Module>> {
    return this.api.getList<Module>(this.api.resourceUrl(this.resource), { ...params, portalId });
  }

  /**
   * Returns the paged list of modules placed on a single TAB (page).
   *
   * MIGRATION: legacy ModuleController.GetTabModules(TabId) -> GET /api/v1/modules?tabId=.
   * Soft-deleted modules (IsDeleted = True) are excluded SERVER-SIDE - returned verbatim with
   * NO client-side isDeleted filtering.
   *
   * Contract: as with {@link getModulesByPortal}, the backend requires exactly one discriminator
   * and 400s when neither is supplied. This method always sends `tabId` (which the API treats as
   * taking precedence over portalId), so the invalid no-filter call is unrepresentable. Optional
   * `params` carry additional query values and are spread FIRST so they cannot override `tabId`.
   */
  getModulesByTab(tabId: number, params?: QueryParams): Observable<PagedResponse<Module>> {
    return this.api.getList<Module>(this.api.resourceUrl(this.resource), { ...params, tabId });
  }

  /**
   * Loads a single module by id.
   * Legacy: ModuleController.GetModule(ModuleId, TabId, ignoreCache).
   */
  getModule(id: number): Observable<Module> {
    return this.api.get<Module>(this.api.resourceUrl(this.resource, id));
  }

  /**
   * Creates a module instance (HTTP 201).
   * Legacy: ModuleController.AddModule(objModule).
   */
  createModule(dto: CreateModuleDto): Observable<Module> {
    return this.api.post<Module>(this.api.resourceUrl(this.resource), dto);
  }

  /**
   * Updates a module instance (HTTP 200).
   * Legacy: ModuleController.UpdateModule(objModule). The caller supplies the route id
   * and a body whose moduleID matches it (the server validates the match).
   */
  updateModule(id: number, dto: UpdateModuleDto): Observable<Module> {
    return this.api.put<Module>(this.api.resourceUrl(this.resource, id), dto);
  }

  /**
   * Deletes a module (HTTP 204).
   * Legacy: ModuleController.DeleteModule(ModuleId).
   *
   * MIGRATION: the hard-vs-soft delete decision is a SERVER concern - DeleteTabModule
   * soft-deletes (IsDeleted = True, TabID = NullInteger) when the module is on no other
   * tab, whereas a module already on no tab is hard-deleted. The client issues a single
   * DELETE; the API decides hard vs. soft and excludes soft-deleted rows from lists.
   */
  deleteModule(id: number): Observable<void> {
    return this.api.delete(this.api.resourceUrl(this.resource, id));
  }
}
