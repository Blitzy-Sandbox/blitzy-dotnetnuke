/**
 * Role (security role) data contracts.
 *
 * MIGRATION: derived from legacy RoleInfo.vb
 * (Library/Components/Security/Roles/RoleInfo.vb); names/types mirror the
 * backend RoleDto / CreateRoleDto / UpdateRoleDto (System.Text.Json camelCase;
 * note preserved acronyms roleID / rsvpCode). Fees are legacy Single → number.
 */

/** Read model — mirrors backend RoleDto (GET /api/roles, GET /api/roles/{id}). */
export interface Role {
  roleID: number;
  portalID: number;
  roleGroupID: number;
  roleName: string;
  description: string;
  isPublic: boolean;
  autoAssignment: boolean;
  // MIGRATION: legacy Single (float) → number.
  serviceFee: number;
  billingFrequency: string;
  billingPeriod: number;
  // MIGRATION: legacy Single (float) → number.
  trialFee: number;
  trialPeriod: number;
  trialFrequency: string;
  rsvpCode: string;
  iconFile: string;
}

/** Create payload — mirrors backend CreateRoleDto (POST /api/roles). */
export interface CreateRoleRequest {
  portalID: number;
  roleGroupID: number;
  roleName: string;
  description?: string;
  isPublic: boolean;
  autoAssignment: boolean;
  serviceFee: number;
  billingFrequency?: string;
  billingPeriod: number;
  trialFee: number;
  trialPeriod: number;
  trialFrequency?: string;
  rsvpCode?: string;
  iconFile?: string;
}

/**
 * Update payload — mirrors backend UpdateRoleDto (PUT /api/roles/{id}).
 * MIGRATION: role id comes from the route; portalID is immutable (not in body).
 */
export interface UpdateRoleRequest {
  roleName: string;
  description?: string;
  roleGroupID: number;
  isPublic: boolean;
  autoAssignment: boolean;
  serviceFee: number;
  billingFrequency?: string;
  billingPeriod: number;
  trialFee: number;
  trialPeriod: number;
  trialFrequency?: string;
  rsvpCode?: string;
  iconFile?: string;
}
