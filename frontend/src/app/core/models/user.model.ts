/**
 * User domain model — the client-side shape of an authenticated/admin user.
 *
 * Consumed across the Angular 19 SPA's `core/` singletons (e.g. `auth.service`),
 * the application chrome (`layout/header`), and RBAC gating
 * (`shared/has-permission`). It models the JSON payload returned by the
 * .NET 8 BFF API for the currently authenticated user / admin user records.
 *
 * MIGRATION: Derived (read as reference only) from the legacy VB.NET class
 * `DotNetNuke.Entities.Users.UserInfo` (Library/Components/Users/UserInfo.vb).
 * This is NOT a 1:1 conversion — it is the minimal, client-facing projection
 * required by the SPA.
 *
 * Property names are camelCase to match the .NET 8 API's System.Text.Json
 * default (JsonNamingPolicy.CamelCase): a C# `UserId` property is serialized
 * as JSON `userId`, which the Angular client consumes verbatim.
 */
export interface User {
  /** Unique user identifier. Legacy: UserInfo.UserID (Integer). */
  userId: number;

  /** Login name (unique; read-only after creation). Legacy: UserInfo.Username (String, Required). */
  username: string;

  /** Display name shown in UI chrome. Legacy: UserInfo.DisplayName (String, Required, MaxLength 128). */
  displayName: string;

  /** First name. Legacy: UserInfo.FirstName (String, Required, MaxLength 50; delegated to Profile.FirstName). */
  firstName: string;

  /** Last name. Legacy: UserInfo.LastName (String, Required, MaxLength 50; delegated to Profile.LastName). */
  lastName: string;

  /** Email address. Legacy: UserInfo.Email (String, Required, MaxLength 256). */
  email: string;

  /** Owning portal identifier. Legacy: UserInfo.PortalID (Integer). */
  portalId: number;

  /** Whether the user is a super (host) user. Legacy: UserInfo.IsSuperUser (Boolean). */
  isSuperUser: boolean;

  /**
   * Optional affiliate identifier. Legacy: UserInfo.AffiliateID (Integer).
   * Marked optional because it is not always present on serialized users.
   */
  affiliateId?: number;

  /**
   * Role names the user belongs to (drives RBAC / has-permission checks).
   * Legacy: UserInfo.Roles (String()).
   */
  roles: string[];

  // MIGRATION: legacy UserInfo.FullName is <Obsolete> ("deprecated in favour of
  // Display Name") and simply returns DisplayName, so it is intentionally omitted;
  // consumers use `displayName`. The Membership/Profile composite objects are also
  // omitted to keep this model minimal (no confirmed consumer requires them).
}
