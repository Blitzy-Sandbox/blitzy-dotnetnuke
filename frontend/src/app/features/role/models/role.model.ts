/**
 * Role / Security feature — client-side TypeScript model contracts.
 *
 * Pure type declarations (interfaces + type aliases ONLY — no runtime code) for the Angular 19 SPA.
 * `Role`, `CreateRole`, and `UpdateRole` mirror the backend `RoleDto` family wire format; `RoleGroup`
 * is a frontend-only view-model with NO backend DTO equivalent; `UserRole` and `AssignUserRole` mirror
 * the backend user-role assignment DTOs (`UserRoleAssignmentDto` / `AssignUserRoleDto`).
 *
 * The field set is derived from the legacy DotNetNuke VB.NET source
 * (`Library/Components/Security/Roles/RoleInfo.vb`) and the backend Role DTOs (wire shape). Legacy XML
 * serialization attributes (`<XmlRoot>` / `<XmlElement>` / `<XmlIgnore>`) are intentionally NOT ported.
 */

// MIGRATION: JSON CASING CONTRACT. The backend serializes with the DEFAULT System.Text.Json
// `JsonNamingPolicy.CamelCase` (no override; the DTOs carry no `[JsonPropertyName]`). That policy
// lowercases the LEADING run of an acronym up to the next word boundary, then keeps the capital that
// starts the next word. Consequently the acronym-prefixed properties serialize as:
//   RoleID      -> roleID
//   PortalID    -> portalID
//   RoleGroupID -> roleGroupID
//   RSVPCode    -> rsvpCode   (the whole "RSVP" run is lowercased, then "Code" keeps its capital C)
//   UserRoleID  -> userRoleID
// Every other property is ordinary camelCase (roleName, description, serviceFee, billingFrequency,
// trialPeriod, trialFrequency, billingPeriod, trialFee, isPublic, autoAssignment, iconFile,
// effectiveDate, expiryDate, isTrialUsed, subscribed). The interface field names below MUST MATCH this
// wire format EXACTLY — a mismatch yields silently `undefined` fields at runtime. (The sibling
// `core/models/user.model.ts` follows the same rule and uses `userID` / `portalID` / `affiliateID`.)

/**
 * `Role` mirrors the backend `RoleDto` (order preserved from the legacy `RoleInfo.vb` backing fields).
 * C#->TS type mapping: value types -> `number` / `boolean`; `int?` -> `number | null`;
 * `float?` (VB `Single?`) -> `number | null`; `string?` -> `string | null`.
 */
export interface Role {
  roleID: number; // RoleID (int) — PK
  portalID: number; // PortalID (int) — tenant FK
  roleGroupID: number | null; // RoleGroupID (int?) — null when ungrouped
  roleName: string | null; // RoleName (string?)
  description: string | null; // Description (string?)
  serviceFee: number | null; // ServiceFee (float? / VB Single?) — null when no fee configured
  billingFrequency: string | null; // BillingFrequency (string?) — single char N/O/D/W/M/Y
  trialPeriod: number | null; // TrialPeriod (int?) — null when no trial configured
  trialFrequency: string | null; // TrialFrequency (string?) — single char N/O/D/W/M/Y
  billingPeriod: number | null; // BillingPeriod (int?) — null when no billing configured
  trialFee: number | null; // TrialFee (float? / VB Single?) — null when no trial fee
  isPublic: boolean; // IsPublic (bool)
  autoAssignment: boolean; // AutoAssignment (bool)
  rsvpCode: string | null; // RSVPCode (string?) — wire name `rsvpCode` (see CASING block above)
  iconFile: string | null; // IconFile (string?)
}

// `CreateRole` mirrors the backend `CreateRoleDto` (= `RoleDto` MINUS `RoleID`). It RETAINS both
// `portalID` and `roleGroupID` (the legacy add path assigns a role group) and omits ONLY `roleID`.
export type CreateRole = Omit<Role, 'roleID'>;

// `UpdateRole` mirrors the backend `UpdateRoleDto` (field-identical to `RoleDto`, including `roleID`).
// `PUT /api/v1/roles/{id}` requires `roleID` populated; the backend guards path id == body roleID.
export type UpdateRole = Role;

// MIGRATION: Backend exposes NO RoleGroupDto and NO role-groups endpoint (GetRoleGroupsAsync omitted
// from IRoleService). The legacy role-group filter (-2 = AllRoles, -1 = GlobalRoles, >=0 = portal group)
// is reconstructed CLIENT-SIDE from the distinct roleGroupID values on loaded roles. Group display NAMES
// are NOT available from the API; only the sentinel labels 'AllRoles'/'GlobalRoles' are known.
// Sentinel values verified against legacy `Website/admin/Security/Roles.ascx.vb` (AllRoles=-2,
// GlobalRoles=-1, real groups >=0). This is the simplified 2-field view-model, NOT the full legacy
// `RoleGroupInfo` (which also carried PortalID + Description).
export interface RoleGroup {
  roleGroupID: number; // -2 AllRoles (filter sentinel), -1 GlobalRoles, >=0 portal group id
  roleGroupName: string; // sentinel labels for -2/-1; best-effort label for real groups (no API source)
}

// MIGRATION: Read model for a persisted user-role assignment. Mirrors the backend
// `DnnMigration.Application.DTOs.Role.UserRoleAssignmentDto` (ported from UserRoleInfo.vb). The full
// membership metadata (effectiveDate/expiryDate/isTrialUsed/subscribed) now ROUND-TRIPS through the API
// via the Role service contract — the prior legacy parity gap is CLOSED (see `AssignUserRole` for the
// write side). Every field is always present on the wire; nullable dates are `string | null`.
export interface UserRole {
  userRoleID: number; // server-assigned identity of the assignment
  userID: number;
  roleID: number;
  effectiveDate: string | null; // ISO-8601; null when the membership window is unbounded
  expiryDate: string | null; // ISO-8601; null when the assignment does not expire
  isTrialUsed: boolean;
  subscribed: boolean;
}

// MIGRATION: Command model for assigning a user to a role together with the full membership metadata.
// Mirrors the backend `AssignUserRoleDto`. Sent as the body of the user-role assignment endpoint; the
// server assigns `userRoleID`, so it is intentionally NOT included here. Replaces the former
// "display-only / not persistable" gap so the legacy SecurityRoles.ascx.vb assignment flow round-trips.
export interface AssignUserRole {
  userID: number;
  roleID: number;
  effectiveDate: string | null;
  expiryDate: string | null;
  isTrialUsed: boolean;
  subscribed: boolean;
}
