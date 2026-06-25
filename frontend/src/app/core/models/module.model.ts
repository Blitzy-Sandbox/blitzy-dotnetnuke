import type { ModulePermission } from './permission.model';

// MIGRATION: ModuleInfo.vb (DotNetNuke.Entities.Modules.ModuleInfo) -> Module.
// Multi-tenant + page scoping: portalId / tabId (both nullable for shared / host modules) (AAP Section 0.7.1).
export interface Module {
  portalId?: number | null;
  tabId?: number | null;
  tabModuleId?: number | null;
  moduleId?: number | null;
  moduleDefId?: number | null;
  moduleOrder: number;
  paneName?: string | null;
  moduleTitle?: string | null;
  cacheTime: number;
  alignment?: string | null;
  color?: string | null;
  border?: string | null;
  iconFile?: string | null;
  allTabs: boolean;
  // MIGRATION: legacy VisibilityState enum serialized as int -- Maximized=0, Minimized=1, None=2 (enum not modeled; kept as number).
  visibility: number;
  isDeleted: boolean;
  header?: string | null;
  footer?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  containerSrc?: string | null;
  displayTitle: boolean;
  displayPrint: boolean;
  displaySyndicate: boolean;
  inheritViewPermissions: boolean;
  desktopModuleId: number;
  friendlyName?: string | null;
  folderName?: string | null;
  description?: string | null;
  version?: string | null;
  isPremium: boolean;
  isAdmin: boolean;
  businessControllerClass?: string | null;
  moduleName?: string | null;
  supportedFeatures: number;
  compatibleVersions?: string | null;
  dependencies?: string | null;
  permissions?: string | null;
  defaultCacheTime: number;
  moduleControlId: number;
  controlSrc?: string | null;
  // MIGRATION: SecurityAccessLevel enum (PortalSecurity.vb) serialized as int -- ControlPanel=-3, SkinObject=-2, Anonymous=-1, View=0, Edit=1, Admin=2, Host=3.
  controlType: number;
  controlTitle?: string | null;
  helpUrl?: string | null;
  supportsPartialRendering: boolean;
  authorizedEditRoles?: string | null;
  authorizedViewRoles?: string | null;
  authorizedRoles?: string | null;
  modulePermissions?: ModulePermission[];
}
