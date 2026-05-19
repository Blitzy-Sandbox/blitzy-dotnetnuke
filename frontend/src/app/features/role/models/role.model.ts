/**
 * Role Management TypeScript Models
 *
 * MIGRATION: This file contains TypeScript interfaces and types for the role management feature,
 * converted from the legacy DotNetNuke VB.NET codebase.
 *
 * Source files:
 * - Library/Components/Security/Roles/RoleInfo.vb (lines 42-302)
 * - Library/Components/Security/Roles/RoleGroupInfo.vb (lines 41-106)
 * - Website/admin/Security/EditRoles.ascx.vb (lines 208-274)
 */

/**
 * Billing/Trial frequency type alias representing the available frequency codes.
 *
 * MIGRATION: Based on VB comments in RoleInfo.vb lines 140-146 and 178-185 documenting frequency values.
 *
 * Values:
 * - 'N' - None (no billing/trial)
 * - 'O' - One time fee
 * - 'D' - Daily
 * - 'W' - Weekly
 * - 'M' - Monthly
 * - 'Y' - Yearly
 * - null - Not specified
 */
export type BillingFrequency = 'N' | 'O' | 'D' | 'W' | 'M' | 'Y' | null;

/**
 * Role interface representing a security role entity.
 *
 * MIGRATION: Converted from VB.NET RoleInfo class (Library/Components/Security/Roles/RoleInfo.vb).
 * VB Private fields with Property accessors → TypeScript interface properties.
 *
 * @see RoleInfo.vb lines 42-302
 */
export interface Role {
  /**
   * The unique identifier for the role.
   * MIGRATION: VB _RoleID As Integer (line 43) → TypeScript number
   */
  roleId: number;

  /**
   * The portal/site identifier that this role belongs to.
   * MIGRATION: VB _PortalID As Integer (line 44) → TypeScript number
   */
  portalId: number;

  /**
   * The role group identifier for categorizing roles.
   * MIGRATION: VB _RoleGroupID As Integer (line 45) → TypeScript number | null
   * VB uses -1 for global roles (no group), mapped to null in TypeScript.
   */
  roleGroupId: number | null;

  /**
   * The name of the role.
   * MIGRATION: VB _RoleName As String (line 46) → TypeScript string
   */
  roleName: string;

  /**
   * The description of the role.
   * MIGRATION: VB _Description As String (line 47) → TypeScript string | null
   */
  description: string | null;

  /**
   * The service fee charged for membership in this role.
   * MIGRATION: VB _ServiceFee As Single (line 48) → TypeScript number
   * VB Single maps to TypeScript number (floating-point).
   */
  serviceFee: number;

  /**
   * The billing frequency code for role membership fees.
   * MIGRATION: VB _BillingFrequency As String (line 49) → TypeScript BillingFrequency
   * Values: 'N' (None), 'O' (One-time), 'D' (Daily), 'W' (Weekly), 'M' (Monthly), 'Y' (Yearly)
   * @see RoleInfo.vb lines 140-146
   */
  billingFrequency: BillingFrequency;

  /**
   * The number of billing periods for role membership.
   * MIGRATION: VB _BillingPeriod As Integer (line 52) → TypeScript number
   */
  billingPeriod: number;

  /**
   * The trial fee charged for trial membership in this role.
   * MIGRATION: VB _TrialFee As Single (line 53) → TypeScript number
   */
  trialFee: number;

  /**
   * The trial frequency code for trial membership.
   * MIGRATION: VB _TrialFrequency As String (line 51) → TypeScript BillingFrequency
   * Values: 'N' (None), 'O' (One-time), 'D' (Daily), 'W' (Weekly), 'M' (Monthly), 'Y' (Yearly)
   * @see RoleInfo.vb lines 178-185
   */
  trialFrequency: BillingFrequency;

  /**
   * The number of trial periods for trial membership.
   * MIGRATION: VB _TrialPeriod As Integer (line 50) → TypeScript number
   */
  trialPeriod: number;

  /**
   * Indicates whether the role is publicly visible and users can request membership.
   * MIGRATION: VB _IsPublic As Boolean (line 54) → TypeScript boolean
   */
  isPublic: boolean;

  /**
   * Indicates whether users are automatically assigned to this role.
   * MIGRATION: VB _AutoAssignment As Boolean (line 55) → TypeScript boolean
   */
  autoAssignment: boolean;

  /**
   * The RSVP code that users can use to request membership.
   * MIGRATION: VB _RSVPCode As String (line 56) → TypeScript string | null
   */
  rsvpCode: string | null;

  /**
   * The path to the icon file representing this role.
   * MIGRATION: VB _IconFile As String (line 57) → TypeScript string | null
   */
  iconFile: string | null;
}

/**
 * RoleGroup interface representing a group/category for organizing roles.
 *
 * MIGRATION: Converted from VB.NET RoleGroupInfo class (Library/Components/Security/Roles/RoleGroupInfo.vb).
 *
 * @see RoleGroupInfo.vb lines 41-106
 */
export interface RoleGroup {
  /**
   * The unique identifier for the role group.
   * MIGRATION: VB _RoleGroupID As Integer (line 42) → TypeScript number
   */
  roleGroupId: number;

  /**
   * The portal/site identifier that this role group belongs to.
   * MIGRATION: VB _PortalID As Integer (line 43) → TypeScript number
   */
  portalId: number;

  /**
   * The name of the role group.
   * MIGRATION: VB _RoleGroupName As String (line 44) → TypeScript string
   */
  roleGroupName: string;

  /**
   * The description of the role group.
   * MIGRATION: VB _Description As String (line 45) → TypeScript string | null
   */
  description: string | null;
}

/**
 * Data transfer object for creating a new role.
 *
 * MIGRATION: Mapped from EditRoles.ascx.vb cmdUpdate_Click handler (lines 208-274).
 * Form fields extracted from the role creation/edit form controls.
 */
export interface CreateRoleRequest {
  /**
   * The name of the role (required).
   * MIGRATION: From txtRoleName (line 237)
   */
  roleName: string;

  /**
   * The description of the role (optional).
   * MIGRATION: From txtDescription (line 238)
   */
  description?: string;

  /**
   * The role group identifier (optional).
   * MIGRATION: From cboRoleGroups.SelectedValue (line 236)
   * Use null or omit for global roles (VB -1 becomes null).
   */
  roleGroupId?: number;

  /**
   * Indicates whether the role is publicly visible.
   * MIGRATION: From chkIsPublic.Checked (line 245)
   */
  isPublic: boolean;

  /**
   * Indicates whether users are automatically assigned to this role.
   * MIGRATION: From chkAutoAssignment.Checked (line 246)
   */
  autoAssignment: boolean;

  /**
   * The service fee for role membership (optional).
   * MIGRATION: From txtServiceFee (line 217, 239)
   */
  serviceFee?: number;

  /**
   * The number of billing periods (optional).
   * MIGRATION: From txtBillingPeriod (line 218, 240)
   */
  billingPeriod?: number;

  /**
   * The billing frequency code (optional).
   * MIGRATION: From cboBillingFrequency (line 219, 241)
   */
  billingFrequency?: BillingFrequency;

  /**
   * The trial fee for trial membership (optional).
   * MIGRATION: From txtTrialFee (line 227, 242)
   */
  trialFee?: number;

  /**
   * The number of trial periods (optional).
   * MIGRATION: From txtTrialPeriod (line 228, 243)
   */
  trialPeriod?: number;

  /**
   * The trial frequency code (optional).
   * MIGRATION: From cboTrialFrequency (line 229, 244)
   */
  trialFrequency?: BillingFrequency;

  /**
   * The RSVP code for requesting membership (optional).
   * MIGRATION: From txtRSVPCode (line 247)
   */
  rsvpCode?: string;

  /**
   * The path to the icon file (optional).
   * MIGRATION: From ctlIcon.Url (line 248)
   */
  iconFile?: string;
}

/**
 * Data transfer object for updating an existing role.
 *
 * MIGRATION: Extends CreateRoleRequest pattern with required roleId for identifying
 * the role to update. Based on EditRoles.ascx.vb cmdUpdate_Click handler.
 */
export interface UpdateRoleRequest {
  /**
   * The unique identifier of the role to update (required).
   * MIGRATION: From RoleID property (line 235)
   */
  roleId: number;

  /**
   * The name of the role (required).
   * MIGRATION: From txtRoleName (line 237)
   */
  roleName: string;

  /**
   * The description of the role (optional).
   * MIGRATION: From txtDescription (line 238)
   */
  description?: string;

  /**
   * The role group identifier (optional).
   * MIGRATION: From cboRoleGroups.SelectedValue (line 236)
   * Use null or omit for global roles (VB -1 becomes null).
   */
  roleGroupId?: number;

  /**
   * Indicates whether the role is publicly visible.
   * MIGRATION: From chkIsPublic.Checked (line 245)
   */
  isPublic: boolean;

  /**
   * Indicates whether users are automatically assigned to this role.
   * MIGRATION: From chkAutoAssignment.Checked (line 246)
   */
  autoAssignment: boolean;

  /**
   * The service fee for role membership (optional).
   * MIGRATION: From txtServiceFee (line 217, 239)
   */
  serviceFee?: number;

  /**
   * The number of billing periods (optional).
   * MIGRATION: From txtBillingPeriod (line 218, 240)
   */
  billingPeriod?: number;

  /**
   * The billing frequency code (optional).
   * MIGRATION: From cboBillingFrequency (line 219, 241)
   */
  billingFrequency?: BillingFrequency;

  /**
   * The trial fee for trial membership (optional).
   * MIGRATION: From txtTrialFee (line 227, 242)
   */
  trialFee?: number;

  /**
   * The number of trial periods (optional).
   * MIGRATION: From txtTrialPeriod (line 228, 243)
   */
  trialPeriod?: number;

  /**
   * The trial frequency code (optional).
   * MIGRATION: From cboTrialFrequency (line 229, 244)
   */
  trialFrequency?: BillingFrequency;

  /**
   * The RSVP code for requesting membership (optional).
   * MIGRATION: From txtRSVPCode (line 247)
   */
  rsvpCode?: string;

  /**
   * The path to the icon file (optional).
   * MIGRATION: From ctlIcon.Url (line 248)
   */
  iconFile?: string;
}
