// MIGRATION: RoleInfo.vb (DotNetNuke.Security.Roles.RoleInfo) -> Role
// Pure TypeScript projection of the backend DnnMigration.Domain.Entities.Role POCO.
// Multi-tenant: portalId scopes every role to its portal (AAP Section 0.7.1).
export interface Role {
  roleId: number;
  portalId: number;
  roleGroupId?: number | null;
  roleName: string;
  description?: string | null;
  serviceFee: number;
  billingFrequency?: string | null;
  trialPeriod: number;
  trialFrequency?: string | null;
  billingPeriod: number;
  trialFee: number;
  isPublic: boolean;
  autoAssignment: boolean;
  rsvpCode?: string | null;
  iconFile?: string | null;
}
