/**
 * Portal (site) data contracts.
 *
 * MIGRATION: derived from legacy PortalInfo.vb
 * (Library/Components/Portal/PortalInfo.vb) and PortalAliasInfo.vb; field
 * names/types mirror the backend PortalDto / CreatePortalDto / UpdatePortalDto
 * as serialized by System.Text.Json (camelCase). NOTE the all-caps `ID`
 * suffix is preserved by the camelCase policy (portalID, not portalId).
 */

/** Read model — mirrors backend PortalDto (GET /api/portals, GET /api/portals/{id}). */
export interface Portal {
  portalID: number;
  portalName: string;
  logoFile: string;
  footerText: string;
  /** ISO 8601 date-time string. */
  expiryDate: string;
  // MIGRATION: legacy Integer UserRegistration (backend UserRegistrationType enum) → numeric code.
  userRegistration: number;
  // MIGRATION: legacy Integer BannerAdvertising (backend BannerType enum) → numeric code.
  bannerAdvertising: number;
  currency: string;
  administratorId: number;
  // MIGRATION: legacy Single (float) → number.
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  description: string;
  keyWords: string;
  backgroundFile: string;
  siteLogHistory: number;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage: string;
  timeZoneOffset: number;
  homeDirectory: string;
  administratorRoleId: number;
  registeredRoleId: number;
  email: string;
  adminTabId: number;
  users: number;
  pages: number;
  // MIGRATION: legacy GUID (System.Guid) → string.
  guid: string;
  version: string;
}

/** Create payload — mirrors backend CreatePortalDto (POST /api/portals). */
export interface CreatePortalRequest {
  portalName: string;
  firstName: string;
  lastName: string;
  username: string;
  password: string;
  email: string;
  description?: string;
  keyWords?: string;
  homeDirectory?: string;
  /** Initial HTTP alias for the new portal. */
  portalAlias: string;
}

/**
 * Update payload — mirrors backend UpdatePortalDto (PUT /api/portals/{id}).
 * MIGRATION: unlike the read model, the update payload INCLUDES payment-processor
 * fields (set by the site-settings screen); the portal id comes from the route.
 */
export interface UpdatePortalRequest {
  portalName: string;
  logoFile?: string;
  footerText?: string;
  expiryDate: string;
  userRegistration: number;
  bannerAdvertising: number;
  currency?: string;
  administratorId: number;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  paymentProcessor?: string;
  processorUserId?: string;
  processorPassword?: string;
  description?: string;
  keyWords?: string;
  backgroundFile?: string;
  siteLogHistory: number;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage?: string;
  timeZoneOffset: number;
  homeDirectory?: string;
}
