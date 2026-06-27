// MIGRATION: PortalSecurity.vb (DES auth, IsInRoles) + UserMembership.vb + UserController.vb -> JWT auth contracts.
// Replaces ASP.NET 2.0 Forms auth / AspNetSqlMembershipProvider with JWT Bearer (AAP Section 0.7.6).

/**
 * POST /api/v1/auth/login request body.
 * MIGRATION: portalId is REQUIRED (multi-tenant login, AAP Section 0.7.1).
 * `password` is inbound-only -- NEVER stored client-side, NEVER logged (AAP Section 0.7.6).
 */
export interface LoginRequest {
  username: string;
  password: string;
  portalId: number;
  rememberMe: boolean;
  verificationCode?: string | null;
}

/** POST /api/v1/auth/refresh and POST /api/v1/auth/logout request body. */
export interface RefreshRequest {
  refreshToken: string;
}

/**
 * Password-reset request body for POST /api/v1/auth/forgot-password.
 * MIGRATION (CP-final review - auth workflow parity): the backend AuthController now exposes
 * `/auth/forgot-password` (AllowAnonymous, rate-limited), so this is IMPLEMENTED (no longer deferred). Re-expresses
 * SendPassword.ascx.vb, which resolved a user by username OR by a uniquely-matching email; the SPA collapses that
 * into one combined `usernameOrEmail` field. `portalId` scopes the lookup (multi-tenant, AAP Section 0.7.1). The
 * frozen backend contract is { portalId, usernameOrEmail } ONLY; `verificationCode` is an optional client-side
 * CAPTCHA affordance that AuthService.requestPasswordReset does NOT forward (reserved for forward-compatibility).
 * The endpoint returns a generic non-enumerating confirmation. See MIGRATION_NOTES.md.
 */
export interface PasswordResetRequest {
  usernameOrEmail: string;
  portalId: number;
  verificationCode?: string;
}

/**
 * Login / refresh response.
 * MIGRATION: backend C# record is named LoginResponse (Application/DTOs/Auth/LoginResponse.cs);
 * the client interface is AuthResponse (identical wire shape).
 * Supports refresh-token rotation: each refresh returns a NEW refreshToken.
 */
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string; // "Bearer"
  expiresIn: number; // seconds (3600 = 60-minute access token)
  expiresAt: string; // ISO 8601 DateTime
  user?: CurrentUser | null;
}

/**
 * GET /api/v1/auth/me response -- the precise shape for the AuthService.currentUser signal.
 * MIGRATION: self-contained projection (NOT the User CRUD model); roles <- UserMembership.vb / PortalSecurity.IsInRoles.
 * All string fields are NON-null (backend defaults to string.Empty).
 */
export interface CurrentUser {
  userId: number;
  username: string;
  email: string;
  displayName: string;
  firstName: string;
  lastName: string;
  fullName: string;
  isSuperUser: boolean;
  portalId: number;
  roles: string[];
}
