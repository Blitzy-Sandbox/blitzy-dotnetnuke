import type { UserRole } from './permission.model';

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
  userRoles?: UserRole[];
}
