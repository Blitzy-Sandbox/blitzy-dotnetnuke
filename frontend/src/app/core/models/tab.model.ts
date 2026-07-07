/**
 * Tab (page) data contracts.
 *
 * MIGRATION: derived from legacy TabInfo.vb (Library/Components/Tabs/TabInfo.vb);
 * names/types mirror the backend TabDto / CreateTabDto / UpdateTabDto
 * (System.Text.Json camelCase; preserved acronym tabID). The read model omits
 * legacy permission/role collections that the backend TabDto does not surface.
 */

/** Read model — mirrors backend TabDto (GET /api/tabs, GET /api/tabs/{id}). */
export interface Tab {
  tabID: number;
  tabOrder: number;
  portalID: number;
  tabName: string;
  isVisible: boolean;
  parentId: number;
  level: number;
  iconFile: string;
  disableLink: boolean;
  title: string;
  description: string;
  keyWords: string;
  url: string;
  skinSrc: string;
  containerSrc: string;
  tabPath: string;
  /** ISO 8601 date-time string. */
  startDate: string;
  /** ISO 8601 date-time string. */
  endDate: string;
  refreshInterval: number;
  pageHeadText: string;
  isSecure: boolean;
  hasChildren: boolean;
}

/** Create payload — mirrors backend CreateTabDto (POST /api/tabs). */
export interface CreateTabRequest {
  portalID: number;
  tabName: string;
  parentId: number;
  tabOrder: number;
  isVisible: boolean;
  iconFile?: string;
  disableLink: boolean;
  title?: string;
  description?: string;
  keyWords?: string;
  url?: string;
  skinSrc?: string;
  containerSrc?: string;
  refreshInterval: number;
  pageHeadText?: string;
  isSecure: boolean;
}

/**
 * Update payload — mirrors backend UpdateTabDto (PUT /api/tabs/{id}).
 * MIGRATION: tab id comes from the route; portalID is immutable (not in body).
 */
export interface UpdateTabRequest {
  tabName: string;
  parentId: number;
  tabOrder: number;
  isVisible: boolean;
  iconFile?: string;
  disableLink: boolean;
  title?: string;
  description?: string;
  keyWords?: string;
  url?: string;
  skinSrc?: string;
  containerSrc?: string;
  refreshInterval: number;
  pageHeadText?: string;
  isSecure: boolean;
}
