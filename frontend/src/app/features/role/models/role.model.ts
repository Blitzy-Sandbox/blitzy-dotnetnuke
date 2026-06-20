// MIGRATION: CRITICAL JSON CASING — the field names on these interfaces are the ACTUAL
// serialized wire names emitted by the backend. They MUST match the producer byte-for-byte;
// a mismatch does NOT raise an error — the field silently deserializes to `undefined` at
// runtime (the consumer must match the producer-as-specced).
//
// WHY THE ACRONYM CASING LOOKS "WRONG": the backend `Program.cs` registers `AddControllers()`
// with the DEFAULT `System.Text.Json` options — i.e. `JsonNamingPolicy.CamelCase` with NO
// override — and the `RoleDto` family carries NO `[JsonPropertyName]` attributes.
// `JsonNamingPolicy.CamelCase` lowercases ONLY the FIRST character of each PascalCase property
// name; it leaves every subsequent character (including trailing acronyms) untouched. Hence:
//   RoleID       ->  roleID        (NOT roleId)
//   PortalID     ->  portalID      (NOT portalId)
//   RoleGroupID  ->  roleGroupID   (NOT roleGroupId)
//   RSVPCode     ->  rSVPCode      (NOT rsvpCode)
// while every ordinary single-word / PascalCase property becomes plain camelCase:
//   roleName, description, serviceFee, billingFrequency, trialPeriod, trialFrequency,
//   billingPeriod, trialFee, isPublic, autoAssignment, iconFile.
//
// RESOLVED CROSS-STACK CASING (CP1 remediation): `core/models/user.model.ts` previously declared
// `userId` / `portalId` / `affiliateId` (lowercase 'd'), which did NOT match the wire contract.
// The backend `UserDto` declares `UserID` / `PortalID` / `AffiliateID`, which serialize to
// `userID` / `portalID` / `affiliateID` under the same first-char-only camelCase policy described
// above. `user.model.ts` has been corrected to those exact wire names, so NO cross-stack casing
// divergence remains.
//
// FALLBACK: if the backend is ever confirmed/normalized to emit lowercase-'d' acronyms
// (`roleId` / `portalId` / `roleGroupId` / `rsvpCode`), switch these field names to match.
// This interface remains the SINGLE source of truth for the wire shape, and `role.service.ts`
// is the one boundary that could remap fields if such a normalization layer is introduced.

// `Role` mirrors the backend `RoleDto` (15 fields, in the exact wire order below, which is the
// verbatim backing-field order of the legacy `RoleInfo.vb`). C# -> TS type mapping: value types
// -> `number`/`boolean`; `int?` -> `number | null`; `string?` -> `string | null`; VB `Single`
// (C# `float`) -> `number`, and nullable `float?` -> `number | null` (physical [Roles] money/int
// columns ServiceFee/TrialFee/TrialPeriod/BillingPeriod/RoleGroupID are NULL-able, so they may be null).
export interface Role {
  roleID: number; // RoleID (int) — PK
  portalID: number; // PortalID (int) — tenant FK
  roleGroupID: number | null; // RoleGroupID (int?) — legacy Null.NullInteger(-1) sentinel; null when ungrouped
  roleName: string | null; // RoleName (string?) — effectively required for display
  description: string | null; // Description (string?)
  serviceFee: number | null; // ServiceFee (float? / VB Single, physical [Roles] NULL-able -> may be null)
  billingFrequency: string | null; // BillingFrequency (string?) — single char N/O/D/W/M/Y
  trialPeriod: number | null; // TrialPeriod (int?, physical NULL-able -> may be null)
  trialFrequency: string | null; // TrialFrequency (string?) — single char N/O/D/W/M/Y
  billingPeriod: number | null; // BillingPeriod (int?, physical NULL-able -> may be null)
  trialFee: number | null; // TrialFee (float? / VB Single, physical NULL-able -> may be null)
  isPublic: boolean; // IsPublic (bool)
  autoAssignment: boolean; // AutoAssignment (bool)
  rSVPCode: string | null; // RSVPCode (string?) — NOTE casing rSVPCode (see CASING block)
  iconFile: string | null; // IconFile (string?)
}

// `CreateRole` mirrors the backend `CreateRoleDto` (= `RoleDto` minus `RoleID`, 14 fields).
// It RETAINS `portalID` and `roleGroupID` — the legacy add path assigns the new role to a role
// group — and omits ONLY the server-assigned `roleID`. Consumed by `POST /api/v1/roles`.
export type CreateRole = Omit<Role, 'roleID'>;

// `UpdateRole` mirrors the backend `UpdateRoleDto` (field-identical to `RoleDto`, all 15 fields
// including `roleID`). The `PUT /api/v1/roles/{id}` request REQUIRES `roleID` to be populated;
// the backend guards that the path `id` equals the body `roleID` and returns `400` on mismatch.
export type UpdateRole = Role;

// MIGRATION: Backend exposes NO RoleGroupDto and NO role-groups endpoint (GetRoleGroupsAsync omitted
// from IRoleService). The legacy role-group filter (-2 = AllRoles, -1 = GlobalRoles, >=0 = portal group)
// is reconstructed CLIENT-SIDE from the distinct roleGroupID values on loaded roles. Group display NAMES
// are NOT available from the API; only the sentinel labels 'AllRoles'/'GlobalRoles' are known.
export interface RoleGroup {
  roleGroupID: number; // -2 AllRoles (filter sentinel), -1 GlobalRoles, >=0 portal group id
  roleGroupName: string; // sentinel labels for -2/-1; for real groups use a best-effort label (no API source)
}

// MIGRATION: Backend has NO UserRoleDto; membership endpoints (POST/DELETE /roles/{roleId}/users/{userId})
// take NO body. Legacy UserRoleInfo effectiveDate/expiryDate + notify CANNOT round-trip through the API.
// These optional fields are display-only and MUST NOT be sent to the server.
// NOTE: the role-assignment USERS GRID itself binds to the core `User` model from
// `frontend/src/app/core/models`; this `UserRole` is only the lightweight association view-model.
export interface UserRole {
  userID: number;
  roleID: number;
  effectiveDate?: string | null; // display-only; not persistable (parity gap)
  expiryDate?: string | null; // display-only; not persistable (parity gap)
}
