import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService, ListResult, QueryParams } from '../../core/services/api.service';
import { CreateModuleRequest, Module, UpdateModuleRequest } from '../../core/models';

/**
 * ModuleService — thin orchestration over ApiService for the `modules` REST resource.
 *
 * MIGRATION: replaces the in-page ModuleController data calls of the legacy DNN
 * admin screen Website/admin/Modules/ModuleSettings.ascx.vb —
 *   ModuleController.GetModule(...)         -> getModule(id)     [GET    /api/modules/{id}]
 *   ModuleController.UpdateModule(...)       -> updateModule(...) [PUT    /api/modules/{id}]
 *   ModuleController.DeleteTabModule(...)    -> deleteModule(id)  [DELETE /api/modules/{id}]
 *   (module inventory / list)                -> getModules(...)   [GET    /api/modules]
 * The legacy calls were made directly against ADO.NET-backed controllers inside the
 * Web Forms code-behind (BindData L115, cmdUpdate_Click L385, cmdDelete_Click L305);
 * they now flow as injected, async HTTP calls routed through the shared ApiService.
 *
 * AAP §0.7.1 (code organization): "Angular services handle API communication ONLY
 * (not business logic)." This service is a pure pass-through to ApiService for the
 * 'modules' resource — it holds NO state/Signals, performs NO validation/transformation,
 * and never touches HttpClient or tokens directly. Component-level state (Signals)
 * lives in the feature components (module-list, module-form); the JWT bearer token is
 * attached centrally by core/auth/auth.interceptor.ts. Returning the ApiService
 * Observable unchanged preserves the { data, meta } envelope-unwrapping and the
 * RFC 7807 ProblemDetails error normalization implemented centrally in ApiService.
 */
@Injectable({ providedIn: 'root' })
export class ModuleService {
  private readonly api = inject(ApiService);

  /** REST resource segment (ApiService prepends the /api baseUrl). */
  private static readonly RESOURCE = 'modules';

  /** GET /api/modules[?query=...] — list, with optional filter/sort/paging params. */
  getModules(params?: QueryParams): Observable<Module[]> {
    return this.api.getList<Module>(ModuleService.RESOURCE, params);
  }

  /** GET /api/modules[?...] — list WITH the { data, meta } envelope (for server paging/total counts). */
  getModulesWithMeta(params?: QueryParams): Observable<ListResult<Module>> {
    return this.api.getListWithMeta<Module>(ModuleService.RESOURCE, params);
  }

  /** GET /api/modules/{id} — single module by id. */
  getModule(id: number | string): Observable<Module> {
    return this.api.getById<Module>(ModuleService.RESOURCE, id);
  }

  /** POST /api/modules — create (returns 201 + created Module). */
  createModule(body: CreateModuleRequest): Observable<Module> {
    return this.api.create<Module>(ModuleService.RESOURCE, body);
  }

  /** PUT /api/modules/{id} — update existing module. */
  updateModule(id: number | string, body: UpdateModuleRequest): Observable<Module> {
    return this.api.update<Module>(ModuleService.RESOURCE, id, body);
  }

  /** DELETE /api/modules/{id} — delete (returns 204). */
  deleteModule(id: number | string): Observable<void> {
    return this.api.delete(ModuleService.RESOURCE, id);
  }
}
