/**
 * User data contracts (identity + composed membership/profile) and user
 * write payloads.
 *
 * MIGRATION: derived from legacy UserInfo.vb, Membership/UserMembership.vb and
 * Profile/UserProfile.vb (Library/Components/Users/**). Names/types mirror the
 * backend UserDto / MembershipDto / ProfileDto / CreateUserDto / UpdateUserDto /
 * ChangePasswordDto (System.Text.Json camelCase; preserved acronyms userID,
 * affiliateID, im). Secrets (password/answer/question) are never surfaced on the
 * read boundary.
 */

/**
 * Read-only membership/account-state — mirrors backend MembershipDto.
 * MIGRATION: password/passwordAnswer/passwordQuestion are intentionally excluded.
 */
export interface Membership {
  approved: boolean;
  lockedOut: boolean;
  isOnLine: boolean;
  updatePassword: boolean;
  /** ISO 8601 date-time string. */
  createdDate: string;
  /** ISO 8601 date-time string. */
  lastLoginDate: string;
  /** ISO 8601 date-time string. */
  lastActivityDate: string;
  /** ISO 8601 date-time string. */
  lastLockoutDate: string;
  /** ISO 8601 date-time string. */
  lastPasswordChangeDate: string;
}

/** Read-only user profile (address/contact/locale) — mirrors backend ProfileDto. */
export interface Profile {
  street: string;
  unit: string;
  city: string;
  region: string;
  country: string;
  postalCode: string;
  telephone: string;
  cell: string;
  fax: string;
  website: string;
  im: string;
  // MIGRATION: legacy integer time-zone offset → number.
  timeZone: number;
  preferredLocale: string;
}

/**
 * Read model — mirrors backend UserDto (GET /api/users, GET /api/users/{id}).
 * MIGRATION: composes nested Membership/Profile; firstName/lastName surfaced at
 * the top level; contains no password/answer/question.
 */
export interface User {
  userID: number;
  portalID: number;
  username: string;
  displayName: string;
  firstName: string;
  lastName: string;
  email: string;
  isSuperUser: boolean;
  affiliateID: number;
  roles: string[];
  membership: Membership;
  profile: Profile;
}

/**
 * Create payload — mirrors backend CreateUserDto (POST /api/users).
 * Password fields are expected here (create flow only).
 */
export interface CreateUserRequest {
  username: string;
  firstName: string;
  lastName: string;
  displayName?: string;
  email: string;
  password: string;
  confirmPassword?: string;
  passwordQuestion?: string;
  passwordAnswer?: string;
  portalID: number;
  authorize: boolean;
  notify: boolean;
  randomPassword: boolean;
}

/**
 * Update payload — mirrors backend UpdateUserDto (PUT /api/users/{id}).
 * MIGRATION: identity + flat profile fields; contains NO password fields
 * (password changes use ChangePasswordRequest); user id comes from the route.
 */
export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  displayName?: string;
  email: string;
  affiliateID?: number;
  approved?: boolean;
  street?: string;
  unit?: string;
  city?: string;
  region?: string;
  country?: string;
  postalCode?: string;
  telephone?: string;
  cell?: string;
  fax?: string;
  website?: string;
  im?: string;
  preferredLocale?: string;
  timeZone: number;
}

/** Change-password payload — mirrors backend ChangePasswordDto. */
export interface ChangePasswordRequest {
  oldPassword: string;
  newPassword: string;
}
