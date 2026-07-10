import { User } from './user.model';

/**
 * Authentication contracts for the JWT / Backend-for-Frontend flow.
 *
 * MIGRATION: legacy DES / FormsAuthentication / PortalSecurity
 * (Library/Components/Security/PortalSecurity.vb) are REPLACED by JWT bearer
 * tokens. Names/types mirror the backend AuthDtos.cs (LoginRequestDto,
 * RefreshRequestDto, TokenResponseDto, CurrentUserDto) as serialized by
 * System.Text.Json (camelCase).
 */

/** Credentials for POST /api/auth/login — mirrors backend LoginRequestDto. */
export interface LoginRequest {
  username: string;
  password: string;
  /** Optional portal context (backend PortalId is int?). */
  portalId?: number;
}

/**
 * Payload for POST /api/auth/refresh — mirrors backend RefreshRequestDto.
 * MIGRATION: backend RefreshRequestDto.RefreshToken is a non-null string
 * (defaults to string.Empty), so the client always sends the refresh token in
 * the request body (token-in-body contract); the field is therefore required
 * to mirror the backend contract exactly.
 */
export interface RefreshRequest {
  refreshToken: string;
}

/**
 * Payload for POST /api/auth/logout — mirrors backend LogoutRequestDto.
 * MIGRATION (Checkpoint-8 API-contract finding): logout revokes server-side
 * refresh-token state and is keyed off the refresh token carried in the request
 * body (token-in-body contract), NOT the bearer principal — the auth interceptor
 * never attaches a bearer to the auth-flow routes and the access token may be
 * expired at logout. The field mirrors the backend (non-null string) so the
 * client always sends the token it holds; a blank value is an idempotent no-op.
 */
export interface LogoutRequest {
  refreshToken: string;
}

/**
 * Token pair returned by POST /api/auth/login and POST /api/auth/refresh —
 * mirrors backend TokenResponseDto.
 * MIGRATION: login/refresh return TOKENS ONLY; the current user is fetched
 * separately via GET /api/auth/me (see MeResponse). `expiresAt` is the absolute
 * UTC expiry (ISO 8601). Backend TokenResponseDto emits `refreshToken` and
 * `tokenType` as non-null strings (`tokenType` defaults to "Bearer"), so both
 * are required here to mirror the token-in-body contract exactly.
 */
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  /** ISO 8601 UTC date-time string. */
  expiresAt: string;
  /** Backend defaults this to "Bearer". */
  tokenType: string;
}

/**
 * The authenticated user.
 * MIGRATION: the backend CurrentUserDto composes the full UserDto, so the
 * current user IS the full User read projection. Aliased to `User` to keep a
 * single source of truth; drives AuthService.currentUser signal and the
 * shared/ has-permission directive (via `roles` / `isSuperUser`).
 */
export type CurrentUser = User;

/**
 * Response body of GET /api/auth/me — mirrors backend CurrentUserDto `{ user }`.
 * Delivered inside the standard { data, meta } envelope (i.e. data: MeResponse).
 */
export interface MeResponse {
  user: CurrentUser;
}
