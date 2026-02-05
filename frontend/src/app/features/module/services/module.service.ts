/**
 * Module Feature HTTP Service
 * 
 * MIGRATION: Angular 19 HTTP service replacing DNN ModuleController.vb data access patterns
 * and admin module controls (Export.ascx.vb, Import.ascx.vb) with modern Angular HTTP patterns.
 * 
 * This service provides comprehensive module management operations including:
 * - CRUD operations for module instances
 * - Module settings management (module-level and tab-module-level)
 * - Tab-module operations (add, move, copy, delete, reorder)
 * - Module portability (export/import) functionality
 * 
 * Source files referenced:
 * @source Library/Components/Modules/ModuleController.vb
 * @source Library/Components/Modules/ModuleInfo.vb
 * @source Website/admin/Modules/ModuleSettings.ascx.vb
 * @source Website/admin/Modules/Export.ascx.vb
 * @source Website/admin/Modules/Import.ascx.vb
 * 
 * @example
 * // Inject the service using Angular 19 inject function
 * private moduleService = inject(ModuleService);
 * 
 * // Get paginated modules
 * this.moduleService.getModules(0, 10, portalId).subscribe(result => {
 *   console.log(result.items);
 * });
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  Module,
  CreateModuleRequest,
  UpdateModuleRequest,
  ExportModuleRequest,
  ImportModuleRequest,
  Folder,
  ImportFile,
  ExportModuleResponse,
  MoveModuleRequest,
  CopyModuleRequest,
  AddModuleToTabRequest,
  UpdateModuleOrderRequest
} from '../models/module.model';
import { PagedResult } from '../../portal/models/portal.model';

/**
 * Angular 19 HTTP service for module API operations.
 * 
 * Implements singleton pattern with providedIn: 'root' decorator.
 * Uses inject() function for HttpClient dependency injection per Angular 19 standards.
 * 
 * MIGRATION: Replaces DNN ModuleController.vb VB.NET data access patterns with
 * modern Angular HTTP client patterns using RxJS Observables.
 * 
 * All methods return Observables for reactive API communication patterns,
 * replacing legacy DNN's synchronous DataReader patterns with reactive Observable streams.
 */
@Injectable({ providedIn: 'root' })
export class ModuleService {
  /**
   * HttpClient instance injected using Angular 19 inject() function.
   * MIGRATION: Replaces constructor injection pattern for cleaner code.
   */
  private readonly http = inject(HttpClient);

  /**
   * Base API URL for module endpoints.
   * MIGRATION: Reads from environment configuration, replacing legacy web.config connection strings.
   */
  private readonly apiUrl = `${environment.apiBaseUrl}${environment.endpoints.modules}`;

  /**
   * Base API URL for tab-related endpoints.
   */
  private readonly tabsApiUrl = `${environment.apiBaseUrl}${environment.endpoints.tabs}`;

  /**
   * Base API URL for portal-related endpoints (used for folder operations).
   */
  private readonly portalsApiUrl = `${environment.apiBaseUrl}${environment.endpoints.portals}`;

  // ============================================================================
  // MODULE CRUD METHODS
  // ============================================================================

  /**
   * Retrieves a paginated list of modules, optionally filtered by portal.
   * 
   * MIGRATION: Replaces VB.NET GetModules(ByVal PortalID As Integer) and GetAllModules()
   * from ModuleController.vb (lines 915-930, 871-873).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetModules(PortalID)
   * - FillModuleInfoCollection(DataReader)
   * 
   * @param pageIndex - Zero-based page index (default: 0)
   * @param pageSize - Number of items per page (default: 10)
   * @param portalId - Optional portal ID to filter modules
   * @returns Observable<PagedResult<Module>> - Paginated list of modules
   * 
   * @example
   * // Get first page of modules for portal 1
   * this.moduleService.getModules(0, 10, 1).subscribe(result => {
   *   console.log(`Total modules: ${result.totalCount}`);
   *   result.items.forEach(module => console.log(module.moduleTitle));
   * });
   */
  getModules(
    pageIndex: number = 0,
    pageSize: number = 10,
    portalId?: number
  ): Observable<PagedResult<Module>> {
    let params = new HttpParams()
      .set('pageIndex', pageIndex.toString())
      .set('pageSize', pageSize.toString());

    if (portalId !== undefined && portalId !== null) {
      params = params.set('portalId', portalId.toString());
    }

    return this.http.get<PagedResult<Module>>(this.apiUrl, { params });
  }

  /**
   * Retrieves a single module by ID and tab ID.
   * 
   * MIGRATION: Replaces VB.NET GetModule(ByVal ModuleId, ByVal TabId, ByVal ignoreCache)
   * from ModuleController.vb (lines 885-905).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetModule(ModuleId, TabId) (line 895)
   * - FillModuleInfo(dr) (line 897)
   * - DataCache.GetPersistentCacheItem() caching logic
   * 
   * @param moduleId - ID of the module to retrieve
   * @param tabId - ID of the tab where the module is located
   * @returns Observable<Module> - The requested module
   * 
   * @example
   * this.moduleService.getModule(123, 456).subscribe(module => {
   *   console.log(module.moduleTitle);
   * });
   */
  getModule(moduleId: number, tabId: number): Observable<Module> {
    const params = new HttpParams().set('tabId', tabId.toString());
    return this.http.get<Module>(`${this.apiUrl}/${moduleId}`, { params });
  }

  /**
   * Creates a new module instance.
   * 
   * MIGRATION: Replaces VB.NET AddModule(ByVal objModule As ModuleInfo)
   * from ModuleController.vb (lines 645-682).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().AddModule(...) (line 648)
   * - DataProvider.Instance().AddTabModule(...) (line 665)
   * - UpdateModuleOrder() for positioning (line 669)
   * - ModulePermissionController.AddModulePermission() for permissions (line 657)
   * - ClearCache(TabId) cache invalidation (line 678)
   * 
   * @param request - Module creation request containing module details
   * @returns Observable<Module> - The newly created module
   * 
   * @example
   * const request: CreateModuleRequest = {
   *   moduleDefId: 10,
   *   tabId: 50,
   *   paneName: 'ContentPane',
   *   moduleTitle: 'My New Module'
   * };
   * this.moduleService.createModule(request).subscribe(module => {
   *   console.log(`Created module with ID: ${module.moduleId}`);
   * });
   */
  createModule(request: CreateModuleRequest): Observable<Module> {
    return this.http.post<Module>(this.apiUrl, request);
  }

  /**
   * Updates an existing module instance.
   * 
   * MIGRATION: Replaces VB.NET UpdateModule(ByVal objModule As ModuleInfo)
   * from ModuleController.vb (lines 1095-1148).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().UpdateModule(...) (line 1097)
   * - DataProvider.Instance().UpdateTabModule(...) (line 1121)
   * - ModulePermissionController operations (lines 1100-1116)
   * - UpdateModuleOrder() for positioning (line 1124)
   * - Portal-wide module updates when AllModules=true (lines 1133-1144)
   * - ClearCache(TabId) cache invalidation (line 1147)
   * 
   * @param moduleId - ID of the module to update
   * @param request - Update request containing fields to modify
   * @returns Observable<Module> - The updated module
   * 
   * @example
   * const request: UpdateModuleRequest = {
   *   moduleTitle: 'Updated Title',
   *   visibility: VisibilityState.Maximized
   * };
   * this.moduleService.updateModule(123, request).subscribe(module => {
   *   console.log('Module updated successfully');
   * });
   */
  updateModule(moduleId: number, request: UpdateModuleRequest): Observable<Module> {
    return this.http.put<Module>(`${this.apiUrl}/${moduleId}`, request);
  }

  /**
   * Deletes a module instance permanently.
   * 
   * MIGRATION: Replaces VB.NET DeleteModule(ByVal ModuleId As Integer)
   * from ModuleController.vb (lines 819-826).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().DeleteModule(ModuleId) (line 821)
   * - DataProvider.Instance().DeleteSearchItems(ModuleId) (line 824)
   * 
   * @param moduleId - ID of the module to delete
   * @returns Observable<void> - Completes when deletion is successful
   * 
   * @example
   * this.moduleService.deleteModule(123).subscribe(() => {
   *   console.log('Module deleted successfully');
   * });
   */
  deleteModule(moduleId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${moduleId}`);
  }

  // ============================================================================
  // MODULE SETTINGS METHODS
  // ============================================================================

  /**
   * Retrieves module-specific settings.
   * 
   * MIGRATION: Replaces VB.NET GetModuleSettings(ByVal ModuleId As Integer)
   * from ModuleController.vb (lines 1237-1271).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetModuleSettings(ModuleId) (line 1249)
   * - Hashtable population from DataReader (lines 1251-1258)
   * - DataCache caching logic (lines 1264-1265)
   * 
   * @param moduleId - ID of the module
   * @returns Observable<Record<string, string>> - Key-value pairs of module settings
   * 
   * @example
   * this.moduleService.getModuleSettings(123).subscribe(settings => {
   *   console.log(settings['MySetting']);
   * });
   */
  getModuleSettings(moduleId: number): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.apiUrl}/${moduleId}/settings`);
  }

  /**
   * Updates module-specific settings.
   * 
   * MIGRATION: Replaces VB.NET UpdateModuleSetting(ByVal ModuleId, ByVal SettingName, ByVal SettingValue)
   * from ModuleController.vb (lines 1283-1296).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetModuleSetting() existence check (line 1285)
   * - DataProvider.Instance().UpdateModuleSetting() or AddModuleSetting() (lines 1288-1290)
   * - DataCache.RemoveCache() cache invalidation (line 1294)
   * 
   * @param moduleId - ID of the module
   * @param settings - Key-value pairs of settings to update
   * @returns Observable<void> - Completes when update is successful
   * 
   * @example
   * const settings = { 'MySetting': 'MyValue', 'AnotherSetting': '123' };
   * this.moduleService.updateModuleSettings(123, settings).subscribe(() => {
   *   console.log('Settings updated');
   * });
   */
  updateModuleSettings(moduleId: number, settings: Record<string, string>): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${moduleId}/settings`, settings);
  }

  /**
   * Retrieves tab-module-specific settings.
   * 
   * MIGRATION: Replaces VB.NET GetTabModuleSettings(ByVal TabModuleId As Integer)
   * from ModuleController.vb (lines 1336-1361).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetTabModuleSettings(TabModuleId) (line 1343)
   * - Hashtable population from DataReader (lines 1345-1351)
   * - DataCache caching logic (lines 1354-1356)
   * 
   * Note: TabModuleSettings are settings specific to a module instance on a particular tab,
   * as opposed to ModuleSettings which apply to the module across all tabs.
   * 
   * @param tabModuleId - ID of the tab-module relationship
   * @returns Observable<Record<string, string>> - Key-value pairs of tab-module settings
   * 
   * @example
   * this.moduleService.getTabModuleSettings(456).subscribe(settings => {
   *   console.log(settings['TabSpecificSetting']);
   * });
   */
  getTabModuleSettings(tabModuleId: number): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.apiUrl}/tabmodule/${tabModuleId}/settings`);
  }

  /**
   * Updates tab-module-specific settings.
   * 
   * MIGRATION: Replaces VB.NET UpdateTabModuleSetting(ByVal TabModuleId, ByVal SettingName, ByVal SettingValue)
   * from ModuleController.vb (lines 1373-1385).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetTabModuleSetting() existence check (line 1375)
   * - DataProvider.Instance().UpdateTabModuleSetting() or AddTabModuleSetting() (lines 1378-1380)
   * - DataCache.RemoveCache() cache invalidation (line 1384)
   * 
   * @param tabModuleId - ID of the tab-module relationship
   * @param settings - Key-value pairs of settings to update
   * @returns Observable<void> - Completes when update is successful
   * 
   * @example
   * const settings = { 'TabSpecificSetting': 'Value' };
   * this.moduleService.updateTabModuleSettings(456, settings).subscribe(() => {
   *   console.log('Tab module settings updated');
   * });
   */
  updateTabModuleSettings(tabModuleId: number, settings: Record<string, string>): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/tabmodule/${tabModuleId}/settings`, settings);
  }

  // ============================================================================
  // TAB-MODULE OPERATIONS
  // ============================================================================

  /**
   * Retrieves all modules for a specific tab/page.
   * 
   * MIGRATION: Replaces VB.NET GetTabModules(ByVal TabId As Integer)
   * from ModuleController.vb (lines 1044-1063).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().GetTabModules(TabId) (line 1055)
   * - FillModuleInfoDictionary() (line 1055)
   * - DataCache.GetPersistentCacheItem() caching logic (line 1048)
   * - DataCache.SetCache() for result caching (line 1059)
   * 
   * @param tabId - ID of the tab/page
   * @returns Observable<Module[]> - Array of modules on the specified tab
   * 
   * @example
   * this.moduleService.getTabModules(50).subscribe(modules => {
   *   modules.forEach(m => console.log(`${m.moduleTitle} in ${m.paneName}`));
   * });
   */
  getTabModules(tabId: number): Observable<Module[]> {
    return this.http.get<Module[]>(`${this.tabsApiUrl}/${tabId}/modules`);
  }

  /**
   * Adds an existing module to a tab/page.
   * 
   * MIGRATION: Replaces the pattern of adding a module instance to a new tab,
   * derived from CopyModule pattern in ModuleController.vb (lines 700-726).
   * 
   * VB.NET patterns replaced:
   * - DataProvider.Instance().AddTabModule() (line 712)
   * - ClearCache() for both source and destination tabs (lines 723-724)
   * 
   * @param tabId - ID of the destination tab
   * @param request - Request containing module ID and target pane
   * @returns Observable<void> - Completes when addition is successful
   * 
   * @example
   * const request: AddModuleToTabRequest = {
   *   moduleId: 123,
   *   paneName: 'ContentPane'
   * };
   * this.moduleService.addModuleToTab(50, request).subscribe(() => {
   *   console.log('Module added to tab');
   * });
   */
  addModuleToTab(tabId: number, request: AddModuleToTabRequest): Observable<void> {
    return this.http.post<void>(`${this.tabsApiUrl}/${tabId}/modules`, request);
  }

  /**
   * Moves a module from one tab to another.
   * 
   * MIGRATION: Replaces VB.NET MoveModule(ByVal moduleId, ByVal fromTabId, ByVal toTabId, ByVal toPaneName)
   * from ModuleController.vb (lines 1078-1086).
   * 
   * VB.NET patterns replaced:
   * - CopyModule() call to duplicate module (line 1081)
   * - DeleteTabModule() call to remove from source (line 1084)
   * 
   * @param moduleId - ID of the module to move
   * @param request - Request containing source tab, destination tab, and target pane
   * @returns Observable<void> - Completes when move is successful
   * 
   * @example
   * const request: MoveModuleRequest = {
   *   fromTabId: 50,
   *   toTabId: 60,
   *   paneName: 'ContentPane'
   * };
   * this.moduleService.moveModule(123, request).subscribe(() => {
   *   console.log('Module moved successfully');
   * });
   */
  moveModule(moduleId: number, request: MoveModuleRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${moduleId}/move`, request);
  }

  /**
   * Copies a module from one tab to another.
   * 
   * MIGRATION: Replaces VB.NET CopyModule(ByVal moduleId, ByVal fromTabId, ByVal toTabId, ByVal toPaneName, ByVal includeSettings)
   * from ModuleController.vb (lines 700-726).
   * 
   * VB.NET patterns replaced:
   * - GetModule() to retrieve source module (line 702)
   * - DataProvider.Instance().AddTabModule() for destination (line 712)
   * - CopyTabModuleSettings() when includeSettings=true (line 717)
   * - ClearCache() for both tabs (lines 723-724)
   * 
   * @param moduleId - ID of the module to copy
   * @param request - Request containing source tab, destination tab, and settings flag
   * @returns Observable<void> - Completes when copy is successful
   * 
   * @example
   * const request: CopyModuleRequest = {
   *   fromTabId: 50,
   *   toTabId: 60,
   *   includeSettings: true
   * };
   * this.moduleService.copyModule(123, request).subscribe(() => {
   *   console.log('Module copied successfully');
   * });
   */
  copyModule(moduleId: number, request: CopyModuleRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${moduleId}/copy`, request);
  }

  /**
   * Removes a module from a specific tab (soft delete from tab).
   * 
   * MIGRATION: Replaces VB.NET DeleteTabModule(ByVal TabId, ByVal ModuleId)
   * from ModuleController.vb (lines 837-861).
   * 
   * VB.NET patterns replaced:
   * - GetModule() to retrieve module info (line 839)
   * - DataProvider.Instance().DeleteTabModule() (line 843)
   * - UpdateTabModuleOrder() for reordering (line 846)
   * - Soft delete when no other tab references (lines 849-857)
   * - ClearCache() for cache invalidation (line 860)
   * 
   * Note: If this is the last tab reference, the module is soft-deleted.
   * Otherwise, it's only removed from the specified tab.
   * 
   * @param tabId - ID of the tab to remove module from
   * @param moduleId - ID of the module to remove
   * @returns Observable<void> - Completes when removal is successful
   * 
   * @example
   * this.moduleService.deleteTabModule(50, 123).subscribe(() => {
   *   console.log('Module removed from tab');
   * });
   */
  deleteTabModule(tabId: number, moduleId: number): Observable<void> {
    return this.http.delete<void>(`${this.tabsApiUrl}/${tabId}/modules/${moduleId}`);
  }

  /**
   * Updates the display order of a module within a pane.
   * 
   * MIGRATION: Replaces VB.NET UpdateModuleOrder(ByVal TabId, ByVal ModuleId, ByVal ModuleOrder, ByVal PaneName)
   * from ModuleController.vb (lines 1160-1187).
   * 
   * VB.NET patterns replaced:
   * - GetModule() to retrieve module (line 1161)
   * - DataProvider.Instance().GetTabModuleOrder() for positioning (line 1165)
   * - DataProvider.Instance().UpdateModuleOrder() (line 1173)
   * - ClearCache() for all affected tabs when AllTabs=true (lines 1179-1183)
   * 
   * @param tabId - ID of the tab containing the module
   * @param moduleId - ID of the module to reorder
   * @param request - Request containing new order value and pane name
   * @returns Observable<void> - Completes when reorder is successful
   * 
   * @example
   * const request: UpdateModuleOrderRequest = {
   *   order: 3,
   *   paneName: 'ContentPane'
   * };
   * this.moduleService.updateModuleOrder(50, 123, request).subscribe(() => {
   *   console.log('Module order updated');
   * });
   */
  updateModuleOrder(tabId: number, moduleId: number, request: UpdateModuleOrderRequest): Observable<void> {
    return this.http.put<void>(`${this.tabsApiUrl}/${tabId}/modules/${moduleId}/order`, request);
  }

  // ============================================================================
  // MODULE PORTABILITY - EXPORT
  // ============================================================================

  /**
   * Retrieves available folders for module export.
   * 
   * MIGRATION: Replaces VB.NET FileSystemUtils.GetFoldersByUser(PortalId, False, False, "READ, WRITE")
   * from Export.ascx.vb (line 71).
   * 
   * VB.NET patterns replaced:
   * - FileSystemUtils.GetFoldersByUser() with permission filter
   * - Folder iteration and dropdown population (lines 72-81)
   * 
   * @param portalId - ID of the portal to get folders for
   * @returns Observable<Folder[]> - Array of folders with read/write permissions
   * 
   * @example
   * this.moduleService.getExportFolders(1).subscribe(folders => {
   *   folders.forEach(f => console.log(f.displayName));
   * });
   */
  getExportFolders(portalId: number): Observable<Folder[]> {
    const params = new HttpParams().set('permissions', 'READ,WRITE');
    return this.http.get<Folder[]>(`${this.portalsApiUrl}/${portalId}/folders`, { params });
  }

  /**
   * Exports module content to an XML file.
   * 
   * MIGRATION: Replaces ExportModule() function from Export.ascx.vb (lines 143-207).
   * 
   * VB.NET patterns replaced:
   * - Module portability validation (objModule.IsPortable, BusinessControllerClass) (line 150)
   * - IPortable.ExportModule(ModuleID) invocation (line 157)
   * - XML content wrapping with type/version attributes (lines 161-164)
   * - Portal space availability check (line 169)
   * - File creation with StreamWriter (lines 171-174)
   * - File registration in database (lines 177-187)
   * 
   * The backend handles all file generation logic and returns the result.
   * 
   * @param request - Export request containing module ID, folder, and filename
   * @returns Observable<ExportModuleResponse> - Export result with file path or error
   * 
   * @example
   * const request: ExportModuleRequest = {
   *   moduleId: 123,
   *   folder: 'exports/',
   *   fileName: 'my-export'
   * };
   * this.moduleService.exportModule(request).subscribe(response => {
   *   if (response.success) {
   *     console.log(`Exported to: ${response.filePath}`);
   *   } else {
   *     console.error(response.message);
   *   }
   * });
   */
  exportModule(request: ExportModuleRequest): Observable<ExportModuleResponse> {
    return this.http.post<ExportModuleResponse>(
      `${this.apiUrl}/${request.moduleId}/export`,
      request
    );
  }

  // ============================================================================
  // MODULE PORTABILITY - IMPORT
  // ============================================================================

  /**
   * Retrieves available import files for a module type from a folder.
   * 
   * MIGRATION: Replaces cboFolders_SelectedIndexChanged logic from Import.ascx.vb (lines 98-116).
   * 
   * VB.NET patterns replaced:
   * - Common.Globals.GetFileList(PortalId, "xml", False, folder) (line 104)
   * - Filename filtering by module name pattern content.{moduleName}.*.xml (lines 107-113)
   * - Display name cleanup for dropdown (line 108)
   * 
   * @param portalId - ID of the portal
   * @param folderId - ID or path of the folder to search
   * @param moduleName - Module name to filter files by
   * @returns Observable<ImportFile[]> - Array of import files matching the module
   * 
   * @example
   * this.moduleService.getImportFiles(1, 'exports', 'Text/HTML').subscribe(files => {
   *   files.forEach(f => console.log(f.displayName));
   * });
   */
  getImportFiles(
    portalId: number,
    folderId: string,
    moduleName: string
  ): Observable<ImportFile[]> {
    const params = new HttpParams().set('filter', moduleName);
    return this.http.get<ImportFile[]>(
      `${this.portalsApiUrl}/${portalId}/folders/${folderId}/files`,
      { params }
    );
  }

  /**
   * Imports module content from an XML file.
   * 
   * MIGRATION: Replaces ImportModule() function from Import.ascx.vb (lines 169-210).
   * 
   * VB.NET patterns replaced:
   * - Filename validation against module name (line 176)
   * - Module portability validation (IsPortable, BusinessControllerClass) (line 177)
   * - IPortable interface type check (line 181)
   * - File reading with StreamReader (lines 183-186)
   * - XML parsing and validation (lines 188-193)
   * - Type attribute validation against module name (line 197)
   * - IPortable.ImportModule(ModuleId, content, version, userId) invocation (line 200)
   * 
   * The backend handles all file reading and IPortable invocation logic.
   * 
   * @param request - Import request containing module ID, folder, and filename
   * @returns Observable<void> - Completes when import is successful
   * 
   * @example
   * const request: ImportModuleRequest = {
   *   moduleId: 123,
   *   folder: 'exports/',
   *   fileName: 'content.TextHTML.my-export.xml'
   * };
   * this.moduleService.importModule(request).subscribe(() => {
   *   console.log('Module content imported successfully');
   * });
   */
  importModule(request: ImportModuleRequest): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/${request.moduleId}/import`,
      request
    );
  }
}
