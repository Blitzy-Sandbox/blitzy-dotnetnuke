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
import type { Module, Paged } from '../../core/models';

/** Payload for the module content-export workflow (<- Export.ascx.vb). */
export interface ModuleExportRequest {
  /** Destination folder (portal-relative). Legacy: cboFolders.SelectedItem.Value. */
  folder: string;
  /** Base file name (without the content.<CleanName>.<...>.xml convention applied). Legacy: txtFile.Text. */
  fileName: string;
}

/** Payload for the module content-import workflow (<- Import.ascx.vb). */
export interface ModuleImportRequest {
  /** Source folder (portal-relative). Legacy: cboFolders.SelectedItem.Value. */
  folder: string;
  /** XML file to import (must match content.<CleanName>.<...>.xml). Legacy: cboFiles.SelectedItem.Value. */
  fileName: string;
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
   * Loads a single module by id (GET /api/modules/{id} -> 200).
   * Used by both module-form (pre-load settings) and import-export (read module name / portability context).
   */
  getById(id: number): Observable<Module> {
    this._loading.set(true);
    return this.api.get<Module>(`${this.resource}/${id}`).pipe(
      tap((m) => this._selected.set(m)),
      finalize(() => this._loading.set(false)),
    );
  }

  /**
   * Creates a module (POST /api/modules -> 201). `Partial<Module>` so callers send only the fields the
   * backend CreateModuleRequest consumes; callers carry `portalId` + `tabId` to preserve portal / tab
   * scoping (AAP Section 0.7.1).
   */
  create(dto: Partial<Module>): Observable<Module> {
    return this.api.post<Module>(this.resource, dto);
  }

  /**
   * Updates a module (PUT /api/modules/{id} -> 200). This is the module-form save path (<- cmdUpdate).
   * The dto carries module settings incl. tabId / portalId / visibility / allTabs / modulePermissions /
   * scheduling / caching.
   * MIGRATION: the legacy Move / Copy / Delete-all lifecycle (ModuleSettings.ascx.vb cmdUpdate_Click ->
   * ModuleController.MoveModule / CopyModule / DeleteAllModules) is orchestrated SERVER-SIDE from the
   * updated end-state fields (`allTabs`, `tabId`). The component sends the desired end-state, NOT
   * imperative move / copy calls.
   */
  update(id: number, dto: Partial<Module>): Observable<Module> {
    return this.api
      .put<Module>(`${this.resource}/${id}`, dto)
      .pipe(tap((m) => this._selected.set(m)));
  }

  /**
   * Deletes a module (DELETE /api/modules/{id} -> 204).
   * Legacy: cmdDelete -> ModuleController.DeleteTabModule(TabId, ModuleId). Named `remove` (not `delete`)
   * to avoid the reserved word.
   */
  remove(id: number): Observable<void> {
    return this.api.delete(`${this.resource}/${id}`);
  }

  /**
   * Triggers a module content export (POST /api/modules/{id}/export).
   * MIGRATION (endpoint GAP): there is NO export endpoint on the current ModulesController / IModuleService
   * (verified: CRUD + by-portal / by-tab only). This targets the forward-looking sub-path
   * `modules/{id}/export`. The legacy server-side mechanics (<- Export.ascx.vb L120-221) -- `IsPortable`
   * + `BusinessControllerClass` reflection, `IPortable.ExportModule`, the
   * content.<CleanName(ModuleName)>.<CleanName(file)>.xml naming convention, the
   * PortalController.HasSpaceAvailable quota check, and the file write / Files-table register -- execute
   * SERVER-SIDE. This service re-expresses only validation + orchestration (folder / file selection,
   * non-empty file name). Endpoint gap documented in MIGRATION_NOTES.md.
   */
  exportContent(id: number, payload: ModuleExportRequest): Observable<void> {
    return this.api.post(`${this.resource}/${id}/export`, payload);
  }

  /**
   * Triggers a module content import (POST /api/modules/{id}/import).
   * MIGRATION (endpoint GAP): same gap as exportContent -- no import endpoint exists yet; targets the
   * forward-looking sub-path `modules/{id}/import`. The legacy server-side mechanics (<- Import.ascx.vb) --
   * clean-name file match, `IsPortable` / `BusinessControllerClass` reflection, XML root `type`-attribute
   * == clean name + `version` extraction, and `IPortable.ImportModule` -- execute SERVER-SIDE. Endpoint
   * gap documented in MIGRATION_NOTES.md.
   */
  importContent(id: number, payload: ModuleImportRequest): Observable<void> {
    return this.api.post(`${this.resource}/${id}/import`, payload);
  }
}
