import type { TabPermission } from './permission.model';

// MIGRATION: TabInfo.vb (DotNetNuke.Entities.Tabs.TabInfo) -> Tab (a "page").
// Multi-tenant: portalId (nullable for host / super tabs) scopes the tab (AAP Section 0.7.1).
// NOTE: legacy computed members (SkinPath, ContainerPath, BreadCrumbs, Panes, Modules, IsSuperTab, TabType, FullUrl, IsAdminTab) are intentionally omitted.
export interface Tab {
  tabId: number;
  tabOrder: number;
  portalId?: number | null;
  tabName?: string | null;
  isVisible: boolean;
  parentId?: number | null;
  level: number;
  iconFile?: string | null;
  disableLink: boolean;
  title?: string | null;
  description?: string | null;
  keyWords?: string | null;
  isDeleted: boolean;
  url?: string | null;
  skinSrc?: string | null;
  containerSrc?: string | null;
  tabPath?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  hasChildren: boolean;
  refreshInterval?: number | null;
  pageHeadText?: string | null;
  isSecure: boolean;
  authorizedRoles?: string | null;
  administratorRoles?: string | null;
  tabPermissions?: TabPermission[];
}
