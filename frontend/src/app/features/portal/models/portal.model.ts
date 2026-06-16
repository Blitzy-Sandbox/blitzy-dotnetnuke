// MIGRATION: TypeScript wire-shape interfaces for the Portal feature. These mirror the backend
// MIGRATION: DnnMigration.Application.DTOs.Portal DTOs (PortalDto / CreatePortalDto / UpdatePortalDto),
// MIGRATION: which are projected from the legacy Library/Components/Portal/PortalInfo.vb value object (38 persisted fields).
// MIGRATION: Field names follow the ASP.NET Core default System.Text.Json camelCase policy (AddControllers, no custom
// MIGRATION: naming override): PascalCase C# -> camelCase JSON, where an all-uppercase acronym collapses fully
// MIGRATION: (GUID -> guid; HTTPAlias -> httpAlias) while a trailing acronym after a lowercase break is preserved
// MIGRATION: (PortalID -> portalID, *TabId -> *TabId already camel).
// MIGRATION: Type conversions: VB Single (HostFee) -> number; VB Date (ExpiryDate) -> ISO-8601 string | null;
// MIGRATION: C# Guid (GUID, non-nullable) -> string (always present, never null over the wire);
// MIGRATION: Null.NullInteger lazy server-derived metrics (Users, Pages) -> number | null.
// MIGRATION: HomeDirectoryMapPath (legacy <XmlIgnore> runtime helper) is NOT persisted and is intentionally excluded.
// MIGRATION: Nullable properties are modeled as `T | null` (NOT optional `?`) because the API emits null values
// MIGRATION: (no JsonIgnoreCondition configured), so every property is always present on the wire.

/**
 * Read projection of a Portal, as serialized by `GET /api/v1/portals` and `GET /api/v1/portals/{id}`
 * inside the standard `{ data, meta }` envelope. Mirrors the backend `PortalDto` (38 fields) verbatim
 * (camelCase wire names). Source lineage: legacy `Library/Components/Portal/PortalInfo.vb`.
 */
export interface Portal {
  portalID: number;
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null; // ExpiryDate (DateTime?) -> ISO-8601 string | null
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number | null; // AdministratorId (int?) — null when no administrator assigned
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number | null; // AdministratorRoleId (int?)
  administratorRoleName: string | null;
  registeredRoleId: number | null; // RegisteredRoleId (int?)
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  guid: string;
  paymentProcessor: string | null;
  // MIGRATION/SECURITY: processorPassword is INTENTIONALLY ABSENT from this read model — the backend
  // PortalDto never projects the payment-processor credential into GET responses (sensitive-data
  // exposure). It is accepted write-only via CreatePortalRequest/UpdatePortalRequest (below) when the
  // UI must update the credential, and is never returned by the API.
  processorUserId: string | null;
  siteLogHistory: number | null; // SiteLogHistory (int?)
  email: string | null;
  adminTabId: number | null; // AdminTabId (int?)
  superTabId: number; // SuperTabId (int) — non-nullable in the read projection
  users: number | null;
  pages: number | null;
  splashTabId: number | null; // SplashTabId (int?)
  homeTabId: number | null; // HomeTabId (int?)
  loginTabId: number | null; // LoginTabId (int?)
  userTabId: number | null; // UserTabId (int?)
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}

/**
 * Request body for `POST /api/v1/portals`. Mirrors the backend `CreatePortalDto` (35 fields):
 * the `Portal` shape minus the server-assigned `portalID` and the server-derived metrics `users` / `pages`.
 */
export interface CreatePortalRequest {
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number;
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number;
  administratorRoleName: string | null;
  registeredRoleId: number;
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  guid: string;
  paymentProcessor: string | null;
  processorPassword: string | null;
  processorUserId: string | null;
  siteLogHistory: number;
  email: string | null;
  adminTabId: number;
  superTabId: number;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}

/**
 * Request body for `PUT /api/v1/portals/{id}`. Mirrors the backend `UpdatePortalDto` (36 fields):
 * the `Portal` shape minus the server-derived metrics `users` / `pages`, but KEEPS `portalID`
 * to identify the row being updated.
 */
export interface UpdatePortalRequest {
  portalID: number;
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number;
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number;
  administratorRoleName: string | null;
  registeredRoleId: number;
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  guid: string;
  paymentProcessor: string | null;
  processorPassword: string | null;
  processorUserId: string | null;
  siteLogHistory: number;
  email: string | null;
  adminTabId: number;
  superTabId: number;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}
