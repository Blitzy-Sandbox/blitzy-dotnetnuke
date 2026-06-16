/**
 * Role / Security feature — client-side TypeScript model contracts.
 *
 * Pure type declarations (interfaces + type aliases ONLY — no runtime code) for the Angular 19 SPA.
 * `Role`, `CreateRole`, and `UpdateRole` mirror the backend `RoleDto` family wire format; `RoleGroup`
 * and `UserRole` are frontend-only view-models that have NO backend DTO equivalent.
 *
 * The field set is derived from the legacy DotNetNuke VB.NET source
 * (`Library/Components/Security/Roles/RoleInfo.vb` — 15 backing fields, exact order) and the pending
 * backend Role DTOs (wire shape). Legacy XML serialization attributes (`<XmlRoot>` / `<XmlElement>` /
 * `<XmlIgnore>`) are intentionally NOT ported.
 */

// MIGRATION: CRITICAL JSON CASING CONTRACT (TRIPLE-verified — the single most error-prone aspect).
// The backend `Program.cs` registers controllers via `AddControllers()` with the DEFAULT
// `System.Text.Json` options: `JsonNamingPolicy.CamelCase` is applied with NO override, and the Role
// DTOs carry NO `[JsonPropertyName]` attributes. `JsonNamingPolicy.CamelCase` lowercases ONLY the FIRST
// character of each PascalCase property name. Consequently the ACTUAL serialized wire field names for the
// acronym-prefixed properties are `roleID`, `portalID`, `roleGroupID`, and `rSVPCode`
// (NOT `roleId` / `portalId` / `roleGroupId` / `rsvpCode`); every other property is ordinary camelCase
// (`roleName`, `description`, `serviceFee`, `billingFrequency`, `trialPeriod`, `trialFrequency`,
// `billingPeriod`, `trialFee`, `isPublic`, `autoAssignment`, `iconFile`). The interface field names below
// MUST MATCH this wire format EXACTLY — a mismatch yields silently `undefined` fields at runtime (the
// consumer must match the producer as specced).
//
// KNOWN LATENT DIVERGENCE (do NOT fix here): `core/models/user.model.ts` declares `userId` / `portalId`
// (lowercase-d) on the assumption the C# props were `UserId` / `PortalId`, but the backend `UserDto`
// actually declares `UserID` / `PortalID`, which would serialize to `userID` / `portalID`. That
// cross-stack casing hazard lives in `core/` (outside this feature's scope) — it is noted here only and
// is NOT changed.
//
// FALLBACK: if the backend is ever confirmed/normalized to emit lowercase-d acronyms (`roleId` /
// `portalId` / `roleGroupId` / `rsvpCode`), switch the interface field names accordingly. This interface
// remains the SINGLE source of truth; `role.service.ts` is the one boundary that could remap if a
// normalization layer is introduced.

/**
 * `Role` mirrors the backend `RoleDto` (15 fields — order PRESERVED from the legacy `RoleInfo.vb`
 * backing fields). C#→TS type mapping: value types → `number` / `boolean`; `int?` → `number | null`;
 * `string?` → `string | null`; `float` (VB `Single`) → `number`.
 */
export interface Role {
  roleID: number; // RoleID (int) — PK
  portalID: number; // PortalID (int) — tenant FK
  roleGroupID: number | null; // RoleGroupID (int?) — legacy Null.NullInteger(-1) sentinel; null when ungrouped
  roleName: string | null; // RoleName (string?) — effectively required for display
  description: string | null; // Description (string?)
  serviceFee: number; // ServiceFee (float / VB Single)
  billingFrequency: string | null; // BillingFrequency (string?) — single char N/O/D/W/M/Y
  trialPeriod: number; // TrialPeriod (int)
  trialFrequency: string | null; // TrialFrequency (string?) — single char N/O/D/W/M/Y
  billingPeriod: number; // BillingPeriod (int)
  trialFee: number; // TrialFee (float / VB Single)
  isPublic: boolean; // IsPublic (bool)
  autoAssignment: boolean; // AutoAssignment (bool)
  rSVPCode: string | null; // RSVPCode (string?) — NOTE casing rSVPCode (see CASING block above)
  iconFile: string | null; // IconFile (string?)
}

// `CreateRole` mirrors the backend `CreateRoleDto` (= `RoleDto` MINUS `RoleID` → 14 fields). It RETAINS
// both `portalID` and `roleGroupID` (the legacy add path assigns a role group) and omits ONLY the
// server-assigned `roleID`.
export type CreateRole = Omit<Role, 'roleID'>;

// `UpdateRole` mirrors the backend `UpdateRoleDto` (field-identical to `RoleDto` — all 15 fields,
// including `roleID`). The `PUT /api/v1/roles/{id}` request REQUIRES `roleID` populated; the backend
// guards that the path `id` equals the body `roleID`, otherwise it returns `400 Bad Request`.
export type UpdateRole = Role;

// MIGRATION: Backend exposes NO RoleGroupDto and NO role-groups endpoint (GetRoleGroupsAsync omitted
// from IRoleService). The legacy role-group filter (-2 = AllRoles, -1 = GlobalRoles, >=0 = portal group)
// is reconstructed CLIENT-SIDE from the distinct roleGroupID values on loaded roles. Group display NAMES
// are NOT available from the API; only the sentinel labels 'AllRoles'/'GlobalRoles' are known.
//
// Sentinel values verified against legacy `Website/admin/Security/Roles.ascx.vb` (AllRoles=-2,
// GlobalRoles=-1, real groups >=0). This is intentionally the simplified 2-field view-model, NOT the full
// 4-field legacy `RoleGroupInfo` (which also carried PortalID + Description).
export interface RoleGroup {
  roleGroupID: number; // -2 AllRoles (filter sentinel), -1 GlobalRoles, >=0 portal group id
  roleGroupName: string; // sentinel labels for -2/-1; for real groups use a best-effort label (no API source)
}

// MIGRATION: Backend has NO UserRoleDto; membership endpoints (POST/DELETE /roles/{roleId}/users/{userId})
// take NO body. Legacy UserRoleInfo effectiveDate/expiryDate + notify CANNOT round-trip through the API.
// These optional fields are display-only and MUST NOT be sent to the server.
//
// NOTE: the role-assignment users grid itself binds to the core `User` model from
// `frontend/src/app/core/models`; `UserRole` is only the lightweight association view-model (keyed by
// userID + roleID) used to surface the legacy effective/expiry dates for display.
export interface UserRole {
  userID: number;
  roleID: number;
  effectiveDate?: string | null; // display-only; not persistable (parity gap)
  expiryDate?: string | null; // display-only; not persistable (parity gap)
}
