/**
 * User domain model — the client-side shape of an authenticated/admin user.
 *
 * Consumed across the Angular SPA's `core/` singletons (e.g.
 * `core/auth/auth.service.ts`) and feature/layout components (e.g.
 * `layout/header`, the `shared/has-permission` directive). This is a pure
 * type-only contract: it carries no runtime code, no Angular decorators and
 * no imports — it is the foundational model with zero dependencies.
 *
 * MIGRATION: Derived (read as reference only — NOT a 1:1 conversion) from the
 * legacy VB.NET class `DotNetNuke.Entities.Users.UserInfo`
 * (Library/Components/Users/UserInfo.vb). Property names are camelCase to
 * match the .NET 8 API's `System.Text.Json` default
 * (`JsonNamingPolicy.CamelCase`): a C# `UserId` property is emitted on the
 * wire as JSON `userId`, so the Angular client consumes camelCase names.
 */
export interface User {
  /**
   * Unique user identifier.
   * Legacy: `UserInfo.UserID` (Integer) [UserInfo.vb:L284-L291].
   */
  userId: number;

  /**
   * Login name — unique and read-only after creation.
   * Legacy: `UserInfo.Username` (String, Required, IsReadOnly) [UserInfo.vb:L301-L312].
   */
  username: string;

  /**
   * Display name shown in UI chrome (header, grids, profile).
   * Legacy: `UserInfo.DisplayName` (String, Required, MaxLength 128) [UserInfo.vb:L104-L111].
   */
  displayName: string;

  /**
   * First name.
   * Legacy: `UserInfo.FirstName` (String, Required, MaxLength 50; the legacy
   * getter/setter delegates to `Profile.FirstName`) [UserInfo.vb:L144-L151].
   */
  firstName: string;

  /**
   * Last name.
   * Legacy: `UserInfo.LastName` (String, Required, MaxLength 50; the legacy
   * getter/setter delegates to `Profile.LastName`) [UserInfo.vb:L178-L185].
   */
  lastName: string;

  /**
   * Email address.
   * Legacy: `UserInfo.Email` (String, Required, MaxLength 256) [UserInfo.vb:L123-L134].
   */
  email: string;

  /**
   * Owning portal identifier.
   * Legacy: `UserInfo.PortalID` (Integer) [UserInfo.vb:L219-L226].
   */
  portalId: number;

  /**
   * Whether the user is a super (host) user with cross-portal privileges.
   * Legacy: `UserInfo.IsSuperUser` (Boolean) [UserInfo.vb:L161-L168].
   */
  isSuperUser: boolean;

  /**
   * Optional affiliate identifier. Marked optional (`?`) because it is not
   * always present on serialized users.
   * Legacy: `UserInfo.AffiliateID` (Integer) [UserInfo.vb:L87-L94].
   */
  affiliateId?: number;

  /**
   * Role names the user belongs to. Drives RBAC checks in the
   * `shared/has-permission` directive and route/UI gating.
   * Legacy: `UserInfo.Roles` (`String()` array) [UserInfo.vb:L261-L274].
   */
  roles: string[];

  // MIGRATION: The legacy `UserInfo.FullName` property [UserInfo.vb:L374-L386] is
  // explicitly <Obsolete> ("deprecated in favour of Display Name") and merely
  // returns DisplayName; it is intentionally omitted — consumers use `displayName`.
  // MIGRATION: The legacy `Membership` and `Profile` composite objects
  // [UserInfo.vb:L195-L209, L236-L250] are also omitted to keep this client model
  // minimal; no confirmed consumer needs them. A separate optional composite
  // interface can be introduced later if a future consumer requires them.
  // Backend enums (UserLoginStatus, UserCreateStatus, VisibilityState) intentionally
  // do NOT live here — they belong to the backend DnnMigration.Domain/Enums (AAP §0.4.1).
}
