import { User } from './user.model';

/**
 * Authentication Data Transfer Object (DTO) contracts for the Angular 19 SPA.
 *
 * These interfaces mirror the request/response shapes of the .NET 8 BFF API's
 * `AuthController`, which exposes the JWT authentication surface at
 * `/api/auth/{login, refresh, logout, me}`. They are consumed by
 * `core/auth/auth.service.ts` (`login()` / `refresh()`) and the auth
 * interceptor/guard; the exported names below MUST stay stable.
 *
 * MIGRATION: legacy Forms Authentication + DES symmetric encryption
 * (`Library/Components/Security/PortalSecurity.vb`) is replaced by stateless
 * JWT Bearer tokens with refresh-token rotation (AAP §0.6.2 / §5.2.7.1). The
 * server holds no session; identity travels in the access-token claims, which
 * enables horizontal scaling.
 *
 * Property names are camelCase to match the .NET 8 API's System.Text.Json
 * default (JsonNamingPolicy.CamelCase): a C# `AccessToken` property is
 * serialized as JSON `accessToken`, consumed verbatim by the client.
 */

/**
 * Credentials submitted to `POST /api/auth/login` to obtain an access/refresh
 * token pair.
 */
export interface LoginRequest {
  /** Login name of the account attempting to authenticate. */
  username: string;

  /** Plaintext password (sent over HTTPS; verified server-side via BCrypt). */
  password: string;
}

/**
 * Token + user payload returned by `POST /api/auth/login` and
 * `POST /api/auth/refresh` (JWT Bearer issuance with refresh-token rotation).
 */
export interface AuthResponse {
  /** Short-lived JWT Bearer access token attached to subsequent API requests. */
  accessToken: string;

  /** Opaque refresh token used to rotate the access token once it expires. */
  refreshToken: string;

  /** The authenticated user's client-facing projection. */
  user: User;

  /**
   * Access-token lifetime in seconds. Optional because the client can derive
   * expiry from the token itself; the AAP specifies a 60-minute access token
   * (AAP §0.6.2 / §5.2.7.1).
   */
  expiresIn?: number;
}

/**
 * Payload submitted to `POST /api/auth/refresh` to rotate an expiring access
 * token using a previously issued refresh token.
 */
export interface RefreshRequest {
  /** The refresh token previously issued by login or a prior refresh. */
  refreshToken: string;
}
