// MIGRATION: TypeScript type contracts for the Module administration feature.
// Source-of-truth legacy files: Library/Components/Modules/{ModuleInfo,DesktopModuleInfo,ModuleDefinitionInfo}.vb
// These types are the anti-corruption boundary between the REST API JSON envelopes and the Angular layer
// (no Angular/HTTP/DI imports here). Field names + nullability mirror the authoritative backend DTOs
// (DnnMigration.Application/DTOs/Module/*) so the .NET 8 System.Text.Json camelCase wire shape deserializes 1:1.

/**
 * Module display visibility state.
 * MIGRATION: enum values preserved VERBATIM from legacy ModuleInfo.vb (`Public Enum VisibilityState`:
 * Maximized, Minimized, None — implicit 0, 1, 2) so any persisted/compared integer remains valid.
 * Authoritative numeric order is Maximized=0, Minimized=1, None=2 (NOT the descriptive
 * "None/Minimized/Maximized" prose ordering). Do NOT reorder.
 */
export enum VisibilityState {
  Maximized = 0,
  Minimized = 1,
  None = 2,
}

/**
 * A module instance placed on a page (tab) — read projection.
 * MIGRATION: mirrors backend ModuleDto (the authoritative wire contract). ID fields keep uppercase "ID"
 * because .NET 8 JsonNamingPolicy.CamelCase lowercases only the leading run (ModuleID -> moduleID).
 * MIGRATION: legacy Null.NullInteger/Null.NullDate sentinels -> nullable unions (never magic numbers).
 * CacheTime is a non-nullable int on the DTO -> `number`. StartDate/EndDate are DateTime? -> ISO `string | null`.
 * MIGRATION: catalog/control fields present on legacy ModuleInfo.vb (folderName, moduleName, defaultCacheTime,
 * moduleControlId, controlSrc, controlTitle, helpUrl) are NOT on the ModuleDto wire; they live on
 * DesktopModule / ModuleDefinition instead and are intentionally omitted here.
 */
export interface Module {
  moduleID: number;
  portalID: number;
  tabID: number;
  tabModuleID: number;
  moduleDefID: number;
  moduleOrder: number;
  paneName: string | null;
  moduleTitle: string | null;
  cacheTime: number;
  alignment: string | null;
  color: string | null;
  border: string | null;
  iconFile: string | null;
  allTabs: boolean;
  visibility: VisibilityState;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  header: string | null;
  footer: string | null;
  startDate: string | null;
  endDate: string | null;
  containerSrc: string | null;
  inheritViewPermissions: boolean;
  desktopModuleID: number;
  friendlyName: string | null;
  description: string | null;
  version: string | null;
  isDeleted: boolean;
}

/**
 * Payload to create a module instance. Mirrors backend CreateModuleDto.
 * MIGRATION: server-assigned moduleID/tabModuleID and internal isDeleted are excluded; immutable catalog
 * fields (friendlyName/description/version) are not part of creation. Display flags carry legacy ctor
 * defaults server-side (DisplayTitle=true, DisplayPrint=true, DisplaySyndicate=false); the typed reactive
 * form supplies explicit values for them.
 */
export interface CreateModuleDto {
  portalID: number;
  tabID: number;
  moduleDefID: number;
  desktopModuleID: number;
  moduleOrder: number;
  paneName: string | null;
  moduleTitle: string | null;
  cacheTime: number;
  alignment: string | null;
  color: string | null;
  border: string | null;
  iconFile: string | null;
  allTabs: boolean;
  visibility: VisibilityState;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  header: string | null;
  footer: string | null;
  startDate: string | null;
  endDate: string | null;
  containerSrc: string | null;
  inheritViewPermissions: boolean;
}

/**
 * Payload to update a module instance. Mirrors backend UpdateModuleDto.
 * MIGRATION: moduleID identifies the target; immutable relationship keys
 * (portalID/tabID/moduleDefID/desktopModuleID/tabModuleID) and internal isDeleted are NOT editable here.
 */
export interface UpdateModuleDto {
  moduleID: number;
  moduleOrder: number;
  paneName: string | null;
  moduleTitle: string | null;
  cacheTime: number;
  alignment: string | null;
  color: string | null;
  border: string | null;
  iconFile: string | null;
  allTabs: boolean;
  visibility: VisibilityState;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  header: string | null;
  footer: string | null;
  startDate: string | null;
  endDate: string | null;
  containerSrc: string | null;
  inheritViewPermissions: boolean;
}

/**
 * Desktop module catalog entry (the installed module "type").
 * MIGRATION: from legacy DesktopModuleInfo.vb. isUpgradeable/isPortable/isSearchable are exposed as plain
 * booleans — legacy derives them from the SupportedFeatures bitmask (IsPortable=1, IsSearchable=2,
 * IsUpgradeable=4) via GetFeature/UpdateFeature; that bit logic is server-side only. No backend DTO exists
 * for this type yet; names follow the same camelCase convention for forward-compatibility.
 */
export interface DesktopModule {
  desktopModuleID: number;
  moduleName: string;
  friendlyName: string;
  description: string | null;
  folderName: string;
  version: string;
  isPremium: boolean;
  isAdmin: boolean;
  businessControllerClass: string | null;
  supportedFeatures: number;
  isUpgradeable: boolean;
  isPortable: boolean;
  isSearchable: boolean;
  compatibleVersions: string | null;
  dependencies: string | null;
  permissions: string | null;
}

/**
 * Module definition (a named control set within a desktop module).
 * MIGRATION: from legacy ModuleDefinitionInfo.vb (ctor defaults DefaultCacheTime=0 -> nullable `number | null`).
 */
export interface ModuleDefinition {
  moduleDefID: number;
  friendlyName: string;
  desktopModuleID: number;
  tempModuleID: number;
  defaultCacheTime: number | null;
}
