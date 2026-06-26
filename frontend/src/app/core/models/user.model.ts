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
