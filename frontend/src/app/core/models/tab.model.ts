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
  // MIGRATION: the backend TabResponse (READ) OMITS the permission collection entirely -- it represents
  // tab access via the authorizedRoles / administratorRoles strings above. The legacy
  // `tabPermissions: TabPermission[]` read field drifted from the frozen backend contract (AAP Section
  // 0.3.4), so it is REMOVED and the model now mirrors TabResponse exactly (consistent with the portal
  // `processorPassword` read-model removal). A write-only request interface (permissions:
  // TabPermissionDto[]) would be introduced if/when a tab CRUD feature is added -- out of scope this
  // phase (the SPA has no tab feature; features are portal/user/role/module/auth only).
}
