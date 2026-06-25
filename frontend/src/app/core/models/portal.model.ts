// MIGRATION: PortalInfo.vb (DotNetNuke.Entities.Portals.PortalInfo) -> Portal
// Pure TypeScript projection of the backend DnnMigration.Domain.Entities.Portal POCO.
// Multi-tenant: portalId is the primary key / tenant discriminator (AAP Section 0.7.1).
// NOTE: the legacy computed HomeDirectoryMapPath is intentionally omitted (not a persisted field).
export interface Portal {
  portalId: number;
  portalName: string;
  logoFile?: string | null;
  footerText?: string | null;
  expiryDate?: string | null;
  userRegistration: number;
  bannerAdvertising: number;
  administratorId: number;
  currency?: string | null;
  hostFee: number;
  hostSpace: number;
  pageQuota: number;
  userQuota: number;
  administratorRoleId: number;
  administratorRoleName?: string | null;
  registeredRoleId: number;
  registeredRoleName?: string | null;
  description?: string | null;
  keyWords?: string | null;
  backgroundFile?: string | null;
  guid: string;
  paymentProcessor?: string | null;
  processorPassword?: string | null;
  processorUserId?: string | null;
  siteLogHistory: number;
  email?: string | null;
  adminTabId: number;
  superTabId: number;
  users?: number | null;
  pages?: number | null;
  splashTabId: number;
  homeTabId: number;
  loginTabId: number;
  userTabId: number;
  defaultLanguage?: string | null;
  timeZoneOffset: number;
  homeDirectory?: string | null;
  version?: string | null;
}
