/**
 * User domain model — the client-side shape of an authenticated/admin user.
 *
 * Consumed across the Angular SPA's `core/` singletons (e.g.
 * `core/auth/auth.service.ts`) and feature/layout components (e.g.
 * `layout/header`, the `shared/has-permission` directive). This is a pure
 * type-only contract: it carries no runtime code, no Angular decorators and
 * no imports — it is the foundational model with zero dependencies.
 *
 * MIGRATION: Mirrors the backend `DnnMigration.Application.DTOs.User.UserDto`
 * wire shape (read as reference from the legacy VB.NET class
 * `DotNetNuke.Entities.Users.UserInfo`, Library/Components/Users/UserInfo.vb).
 * Field names are the EXACT `System.Text.Json` camelCase serialization of the
 * C# DTO properties (`JsonNamingPolicy.CamelCase`, no override, no
 * `[JsonPropertyName]`). That policy lowercases only the LEADING run of an
 * acronym up to the next word boundary, so the C# properties `UserID`,
 * `PortalID` and `AffiliateID` are emitted on the wire as `userID`, `portalID`
 * and `affiliateID` (NOT `userId` / `portalId` / `affiliateId`). Nullability
 * matches the DTO exactly: `string?` -> `string | null`, `int?` -> `number | null`,
 * `string[]?` -> `string[] | null`, `DateTime?` -> ISO-8601 `string | null`.
 * Every property is always present on the wire (no `JsonIgnoreCondition`), so
 * nullable members are modeled as `T | null` rather than optional `?`.
 */
export interface User {
  /**
   * Unique user identifier.
   * Backend: `UserDto.UserID` (int) -> wire `userID`.
   * Legacy: `UserInfo.UserID` (Integer) [UserInfo.vb:L284-L291].
   */
  userID: number;

  /**
   * Owning portal identifier.
   * Backend: `UserDto.PortalID` (int) -> wire `portalID`.
   * Legacy: `UserInfo.PortalID` (Integer) [UserInfo.vb:L219-L226].
   */
  portalID: number;

  /**
   * Optional affiliate identifier; `null` when the user has no affiliate.
   * Backend: `UserDto.AffiliateID` (int?) -> wire `affiliateID`.
   * Legacy: `UserInfo.AffiliateID` (Integer) [UserInfo.vb:L87-L94].
   */
  affiliateID: number | null;

  /**
   * Login name — unique and read-only after creation.
   * Backend: `UserDto.Username` (string?) -> wire `username`.
   * Legacy: `UserInfo.Username` (String, Required, IsReadOnly) [UserInfo.vb:L301-L312].
   */
  username: string | null;

  /**
   * Display name shown in UI chrome (header, grids, profile).
   * Backend: `UserDto.DisplayName` (string?) -> wire `displayName`.
   * Legacy: `UserInfo.DisplayName` (String, Required, MaxLength 128) [UserInfo.vb:L104-L111].
   */
  displayName: string | null;

  /**
   * Email address.
   * Backend: `UserDto.Email` (string?) -> wire `email`.
   * Legacy: `UserInfo.Email` (String, Required, MaxLength 256) [UserInfo.vb:L123-L134].
   */
  email: string | null;

  /**
   * First name.
   * Backend: `UserDto.FirstName` (string?) -> wire `firstName`.
   * Legacy: `UserInfo.FirstName` (String; getter/setter delegates to `Profile.FirstName`) [UserInfo.vb:L144-L151].
   */
  firstName: string | null;

  /**
   * Last name.
   * Backend: `UserDto.LastName` (string?) -> wire `lastName`.
   * Legacy: `UserInfo.LastName` (String; getter/setter delegates to `Profile.LastName`) [UserInfo.vb:L178-L185].
   */
  lastName: string | null;

  /**
   * Computed full name (`firstName + " " + lastName`); always present on the wire.
   * Backend: `UserDto.FullName` (computed, read-only) -> wire `fullName`.
   * MIGRATION: legacy `UserInfo.FullName` [UserInfo.vb:L374-L386] was <Obsolete>; the backend DTO
   * re-exposes it as a computed read field, so it is surfaced here for display parity.
   */
  fullName: string;

  /**
   * Whether the user is a super (host) user with cross-portal privileges.
   * Backend: `UserDto.IsSuperUser` (bool) -> wire `isSuperUser`.
   * Legacy: `UserInfo.IsSuperUser` (Boolean) [UserInfo.vb:L161-L168].
   */
  isSuperUser: boolean;

  /**
   * Whether the user's membership is approved.
   * Backend: `UserDto.Approved` (bool, from `UserMembership.Approved`, legacy default True) -> wire `approved`.
   */
  approved: boolean;

  /**
   * Role names the user belongs to; `null` when the projection did not populate them.
   * Drives RBAC checks (e.g. the `shared/has-permission` directive) and route/UI gating.
   * Backend: `UserDto.Roles` (string[]?) -> wire `roles`.
   * Legacy: `UserInfo.Roles` (`String()` array) [UserInfo.vb:L261-L274].
   */
  roles: string[] | null;

  /**
   * Membership audit timestamps (ISO-8601 strings; `null` when unset). Backend `DateTime?` fields
   * `CreatedDate` / `LastLoginDate` / `LastPasswordChangeDate` / `LastActivityDate`.
   */
  createdDate: string | null;
  lastLoginDate: string | null;
  lastPasswordChangeDate: string | null;
  lastActivityDate: string | null;

  // MIGRATION: The legacy `Membership` and `Profile` composite objects
  // [UserInfo.vb:L195-L209, L236-L250] are flattened into the scalar fields above (the backend UserDto
  // performs the same flattening); the nested composites are intentionally not modeled here.
  // Backend enums (UserLoginStatus, UserCreateStatus, VisibilityState) intentionally do NOT live here —
  // they belong to the backend DnnMigration.Domain/Enums (AAP §0.4.1).
}
