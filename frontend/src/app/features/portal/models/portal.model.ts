// MIGRATION: TypeScript wire-shape interfaces for the Portal feature. These mirror the backend
// MIGRATION: DnnMigration.Application.DTOs.Portal DTOs (PortalDto / CreatePortalDto / UpdatePortalDto),
// MIGRATION: which are projected from the legacy Library/Components/Portal/PortalInfo.vb value object.
// MIGRATION: Field names follow the ASP.NET Core default System.Text.Json camelCase policy (AddControllers, no custom
// MIGRATION: naming override): PascalCase C# -> camelCase JSON, where an all-uppercase acronym collapses fully
// MIGRATION: (GUID -> guid; HTTPAlias -> httpAlias) while a trailing acronym after a lowercase break is preserved
// MIGRATION: (PortalID -> portalID).
// MIGRATION: Type conversions: VB Single (HostFee) -> number; VB Date (ExpiryDate) -> ISO-8601 string | null;
// MIGRATION: C# Guid (GUID, non-nullable) -> string (always present on read, never null over the wire);
// MIGRATION: Null.NullInteger lazy server-derived metrics (Users, Pages) -> number | null.
// MIGRATION: Nullable physical [Portals] FK/tab-id columns (administratorId, administratorRoleId,
// MIGRATION: registeredRoleId, siteLogHistory, adminTabId, splashTabId, homeTabId, loginTabId, userTabId) are
// MIGRATION: number | null, mirroring the backend int? DTO members so a DB null survives as JSON null (not 0).
// MIGRATION (SECURITY): processorPassword is REMOVED from the read `Portal` interface so the SPA never
// MIGRATION: receives/stores the payment-processor credential; it remains write-only on the create/update
// MIGRATION: requests (mirrors backend PortalDto omission + Create/UpdatePortalDto input-only field).
// MIGRATION (INTEGRITY): guid is REMOVED from the create/update request interfaces because [Portals].GUID is
// MIGRATION: server-managed (newid() default, retained server-side); it remains present on the read `Portal`.
// MIGRATION: HomeDirectoryMapPath (legacy <XmlIgnore> runtime helper) is NOT persisted and is intentionally excluded.
// MIGRATION: Nullable properties are modeled as `T | null` (NOT optional `?`) because the API emits null values
// MIGRATION: (no JsonIgnoreCondition configured), so every property is always present on the wire.

/**
 * Read projection of a Portal, as serialized by `GET /api/v1/portals` and `GET /api/v1/portals/{id}`
 * inside the standard `{ data, meta }` envelope. Mirrors the backend `PortalDto` verbatim
 * (camelCase wire names). Source lineage: legacy `Library/Components/Portal/PortalInfo.vb`.
 * The payment-processor credential (`processorPassword`) is intentionally absent from read responses.
 */
export interface Portal {
  portalID: number;
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number | null;
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number | null;
  administratorRoleName: string | null;
  registeredRoleId: number | null;
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  guid: string;
  paymentProcessor: string | null;
  processorUserId: string | null;
  siteLogHistory: number | null;
  email: string | null;
  adminTabId: number | null;
  superTabId: number;
  users: number | null;
  pages: number | null;
  splashTabId: number | null;
  homeTabId: number | null;
  loginTabId: number | null;
  userTabId: number | null;
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}

/**
 * Request body for `POST /api/v1/portals`. Mirrors the backend `CreatePortalDto`:
 * the `Portal` shape minus the server-assigned `portalID`, the server-managed `guid`, and the
 * server-derived metrics `users` / `pages`. `processorPassword` is write-only (accepted here,
 * never echoed back on read).
 */
export interface CreatePortalRequest {
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number | null;
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number | null;
  administratorRoleName: string | null;
  registeredRoleId: number | null;
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  paymentProcessor: string | null;
  processorPassword: string | null;
  processorUserId: string | null;
  siteLogHistory: number | null;
  email: string | null;
  adminTabId: number | null;
  superTabId: number;
  splashTabId: number | null;
  homeTabId: number | null;
  loginTabId: number | null;
  userTabId: number | null;
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}

/**
 * Request body for `PUT /api/v1/portals/{id}`. Mirrors the backend `UpdatePortalDto`:
 * the `Portal` shape minus the server-managed `guid` and the server-derived metrics `users` / `pages`,
 * but KEEPS `portalID` to identify the row being updated. `processorPassword` is write-only.
 */
export interface UpdatePortalRequest {
  portalID: number;
  portalName: string | null;
  logoFile: string | null;
  footerText: string | null;
  expiryDate: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number | null;
  currency: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number | null;
  administratorRoleName: string | null;
  registeredRoleId: number | null;
  registeredRoleName: string | null;
  description: string | null;
  keyWords: string | null;
  backgroundFile: string | null;
  paymentProcessor: string | null;
  processorPassword: string | null;
  processorUserId: string | null;
  siteLogHistory: number | null;
  email: string | null;
  adminTabId: number | null;
  superTabId: number;
  splashTabId: number | null;
  homeTabId: number | null;
  loginTabId: number | null;
  userTabId: number | null;
  defaultLanguage: string | null;
  timeZoneOffset: number;
  homeDirectory: string | null;
  version: string | null;
}
