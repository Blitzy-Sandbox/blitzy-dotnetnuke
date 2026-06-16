import { User } from './user.model';

/**
 * Authentication contracts for the Angular SPA's auth flow.
 *
 * These interfaces mirror the backend `AuthController` request/response shapes
 * exposed under `/api/auth/{login, refresh, logout, me}`. They are consumed by
 * the `core/auth` singletons — `auth.service.ts` (`login()` / `refresh()`),
 * `auth.guard.ts`, and `auth.interceptor.ts` — so the exported names
 * (`LoginRequest`, `AuthResponse`, `RefreshRequest`) form a stable contract and
 * must not be renamed.
 *
 * Property names are camelCase to match the .NET 8 API's `System.Text.Json`
 * default (`JsonNamingPolicy.CamelCase`): a C# `AccessToken` property is emitted
 * on the wire as JSON `accessToken`, so the Angular client consumes camelCase
 * names. This is a pure type-only contract — it carries no runtime code, no
 * Angular decorators, and its sole import is the relative `User` model.
 *
 * MIGRATION: There is no 1:1 legacy VB.NET equivalent for these shapes. Legacy
 * authentication used ASP.NET Forms Authentication plus 56-bit DES token
 * handling (`Library/Components/Security/PortalSecurity.vb`: `SignOut` L79, DES
 * `Encrypt`/`Decrypt` L138-L211). That model is replaced by stateless JWT Bearer
 * access tokens with refresh-token rotation (AAP §0.6.2 / §5.2.7.1) — the single
 * sanctioned behavioral change in the migration.
 */

/**
 * Credentials submitted to `POST /api/auth/login`.
 *
 * Mirrors the backend `LoginRequestDto`. `username` accepts either a login name
 * or an email address; `password` is the plaintext password sent over HTTPS and
 * verified server-side against the stored BCrypt hash (never persisted or echoed
 * back).
 */
export interface LoginRequest {
  /** Username or email address identifying the account. */
  username: string;

  /** Plaintext password (sent over HTTPS; verified against the BCrypt hash). */
  password: string;
}

/**
 * Token and user payload returned by `POST /api/auth/login` and
 * `POST /api/auth/refresh`.
 *
 * Mirrors the backend `AuthResponseDto`. Carries the freshly issued JWT Bearer
 * access token, the rotated refresh token, and the authenticated user's safe
 * read projection. It NEVER carries password material.
 */
export interface AuthResponse {
  /** Signed JWT Bearer access token (~60-minute lifetime per the JWT settings). */
  accessToken: string;

  /** Rotated refresh token; exchange it via `POST /api/auth/refresh`. */
  refreshToken: string;

  /** The authenticated user's safe client projection (no password fields). */
  user: User;

  /**
   * Access-token lifetime in seconds (OAuth2-style relative expiry). Optional:
   * the client can derive expiry from the token itself when this field is
   * absent. The AAP specifies a 60-minute access token (AAP §0.6.2 / §5.2.7.1).
   */
  expiresIn?: number;
}

/**
 * Payload submitted to `POST /api/auth/refresh` to rotate tokens.
 *
 * Mirrors the backend `RefreshTokenRequestDto`. The previously issued refresh
 * token is exchanged for a new {@link AuthResponse} (new access token plus a
 * newly rotated refresh token).
 */
export interface RefreshRequest {
  /** The rotated refresh token previously issued in {@link AuthResponse}. */
  refreshToken: string;
}
