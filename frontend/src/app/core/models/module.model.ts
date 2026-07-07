/**
 * Module data contracts.
 *
 * MIGRATION: derived from legacy ModuleInfo.vb
 * (Library/Components/Modules/ModuleInfo.vb); field names/types mirror the
 * backend ModuleDto / CreateModuleDto / UpdateModuleDto (System.Text.Json
 * camelCase — note the preserved all-caps `ID` suffixes). `visibility` keeps
 * the legacy VisibilityState as a numeric code.
 */

/** Read model — mirrors backend ModuleDto (GET /api/modules, GET /api/modules/{id}). */
export interface Module {
  portalID: number;
  tabID: number;
  tabModuleID: number;
  moduleID: number;
  moduleDefID: number;
  moduleOrder: number;
  paneName: string;
  moduleTitle: string;
  cacheTime: number;
  alignment: string;
  color: string;
  border: string;
  iconFile: string;
  allTabs: boolean;
  // MIGRATION: legacy VisibilityState enum retained as numeric code.
  visibility: number;
  header: string;
  footer: string;
  /** ISO 8601 date-time string. */
  startDate: string;
  /** ISO 8601 date-time string. */
  endDate: string;
  containerSrc: string;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  inheritViewPermissions: boolean;
  desktopModuleID: number;
  friendlyName: string;
  folderName: string;
  description: string;
  version: string;
  moduleName: string;
  controlSrc: string;
}

/** Create/placement payload — mirrors backend CreateModuleDto (POST /api/modules). */
export interface CreateModuleRequest {
  portalID: number;
  tabID: number;
  moduleDefID: number;
  moduleTitle: string;
  paneName: string;
  moduleOrder: number;
  cacheTime: number;
  alignment?: string;
  color?: string;
  border?: string;
  iconFile?: string;
  allTabs: boolean;
  visibility: number;
  header?: string;
  footer?: string;
  startDate?: string;
  endDate?: string;
  containerSrc?: string;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  inheritViewPermissions: boolean;
}

/**
 * Update payload — mirrors backend UpdateModuleDto (PUT /api/modules/{id}).
 * MIGRATION: module id + placement identity (portalID/tabID/moduleDefID) are
 * not re-assigned here; the id comes from the route.
 */
export interface UpdateModuleRequest {
  moduleTitle: string;
  paneName?: string;
  moduleOrder: number;
  cacheTime: number;
  alignment?: string;
  color?: string;
  border?: string;
  iconFile?: string;
  allTabs: boolean;
  visibility: number;
  header?: string;
  footer?: string;
  startDate?: string;
  endDate?: string;
  containerSrc?: string;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  inheritViewPermissions: boolean;
}
