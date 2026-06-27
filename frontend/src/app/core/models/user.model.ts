// MIGRATION: UserInfo.vb + UserMembership.vb (non-credential account-status fields only) -> User.
// CREDENTIALS ARE NEVER PRESENT (AAP Section 0.7.6): no password / passwordQuestion / passwordAnswer / membership / profile.
// Multi-tenant: portalId scopes the user to its portal (AAP Section 0.7.1).
export interface User {
  userId: number;
  username: string;
  displayName?: string | null;
  email?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  fullName?: string | null;
  isSuperUser: boolean;
  affiliateId?: number | null;
  portalId: number;
  isApproved: boolean;
  createdDate?: string | null;
  lastLoginDate?: string | null;
  lastActivityDate?: string | null;
  lastLockoutDate?: string | null;
  lockedOut: boolean;
  // MIGRATION: aligns with backend UserResponse.Roles (System.Text.Json camelCase -> `roles`), the flattened
  // role-name list the service/mapping layer projects from the Domain User.UserRoles join navigation (mirrors the
  // legacy UserInfo.Roles String()). The previous `userRoles?: UserRole[]` join-entity field was contract drift:
  // the backend read model exposes role NAMES (string[]), never the raw UserRole join rows. Always present
  // (backend defaults to []), enabling role-based UI gating to read it directly.
  roles: string[];
}

// MIGRATION (CP-final review - profile workflow parity): frontend projection of the backend UserProfileDto
// (DnnMigration.Application.DTOs.User.UserProfileDto), promoted from the legacy DotNetNuke.Entities.Users.UserProfile
// (Library/Components/Users/Profile/UserProfile.vb). The DNN profile is the EXISTING EAV schema
// ([ProfilePropertyDefinition] + [UserProfile]); the backend service flattens it to/from this shape over
// GET/PUT /api/users/{id}/profile. Keys are the System.Text.Json Web (camelCase) serialization of the C# record --
// note `im` (the legacy "IM" instant-messenger handle: camelCase lowercases the WHOLE leading uppercase run, so
// "IM" -> "im", verified empirically) and `timeZone` (an integer; -1 == unset, the legacy Null.NullInteger).
// `fullName` is read-only/server-composed (FirstName + " " + LastName) and is never sent on update.
export interface UserProfile {
  firstName?: string | null;
  lastName?: string | null;
  fullName?: string | null;
  cell?: string | null;
  telephone?: string | null;
  fax?: string | null;
  im?: string | null;
  street?: string | null;
  unit?: string | null;
  city?: string | null;
  region?: string | null;
  country?: string | null;
  postalCode?: string | null;
  preferredLocale?: string | null;
  timeZone: number;
  website?: string | null;
}
