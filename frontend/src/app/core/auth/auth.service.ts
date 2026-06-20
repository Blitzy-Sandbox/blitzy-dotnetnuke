import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { ApiService } from '../services/api.service';
import { AuthResponse, LoginRequest, RefreshRequest } from '../models/auth.model';
import { User } from '../models/user.model';

/**
 * localStorage keys for cross-reload persistence of the JWT session.
 *
 * Namespaced with a `dnn.` prefix to avoid collisions with any other key in the
 * origin's storage. Kept as module-private constants so every read/write site
 * references the exact same key string.
 */
const ACCESS_TOKEN_KEY = 'dnn.accessToken';
const REFRESH_TOKEN_KEY = 'dnn.refreshToken';
const CURRENT_USER_KEY = 'dnn.currentUser';

/**
 * AuthService - signal-based JWT authentication singleton.
 *
 * The foundational `core/auth/` service for the Angular 19 standalone SPA. It is
 * consumed by `authGuard`, `authInterceptor`, the `layout/header` chrome, and the
 * `shared/has-permission` directive; the exported class name `AuthService` is a
 * hard contract and must not be renamed.
 *
 * MIGRATION: Replaces the legacy DotNetNuke Forms Authentication + DES model in
 * `Library/Components/Security/PortalSecurity.vb`. This is the single sanctioned
 * behavior change of the migration (AAP §0.6.2); the full narrative is recorded in
 * the root `MIGRATION_NOTES.md` (§3 — Sanctioned Behavior Change). Specifically:
 *   - Forms Authentication (FormsAuthentication.SignOut + portal cookies,
 *     PortalSecurity.vb L77-95) -> stateless JWT Bearer tokens. The server holds no
 *     session; identity travels in the access-token claims (enables horizontal scaling).
 *   - DES Encrypt/Decrypt (DESCryptoServiceProvider/CryptoStream, PortalSecurity.vb
 *     L138-211) -> REMOVED from the client entirely; password hashing is server-side
 *     BCrypt (PasswordHasher.cs). No cryptography is performed in this class.
 *
 * Token storage: in-memory Angular signals are the runtime source of truth;
 * `localStorage` adds cross-reload persistence so the route guard still passes after a
 * full page reload (F5).
 * NOTE: an httpOnly cookie is the preferred production hardening (AAP §0.7.2) and the
 * refresh token could later move server-side, but the `Authorization: Bearer` header
 * injected by `auth.interceptor.ts` requires a JS-readable access token, so a
 * JS-readable signal/localStorage pair is used here by design.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  /** Current JWT access token (null when logged out). Runtime source of truth. */
  readonly accessToken = signal<string | null>(this.readString(ACCESS_TOKEN_KEY));
  /** Current refresh token used to rotate the access token. */
  readonly refreshToken = signal<string | null>(this.readString(REFRESH_TOKEN_KEY));
  /** The authenticated user (null when logged out). */
  readonly currentUser = signal<User | null>(this.readUser());

  /** True when an access token is present. Consumed by authGuard and layout chrome. */
  readonly isAuthenticated = computed<boolean>(() => this.accessToken() !== null);

  /**
   * Authenticate with username + password.
   *
   * POST /api/auth/login -> stores the issued access token, refresh token, and user.
   * Routed through ApiService (which unwraps the `{ data }` envelope); this class
   * never touches HttpClient directly.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.api
      .post<AuthResponse>(this.api.authUrl('login'), credentials)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Rotate the session tokens.
   *
   * POST /api/auth/refresh -> the backend performs refresh-token ROTATION, returning
   * a NEW access token AND a NEW refresh token (plus the refreshed user); both are
   * stored, replacing the previous pair.
   */
  refresh(): Observable<AuthResponse> {
    const payload: RefreshRequest = { refreshToken: this.refreshToken() ?? '' };
    return this.api
      .post<AuthResponse>(this.api.authUrl('refresh'), payload)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Log out: notify the server, then clear client state and redirect to the login route.
   *
   * MIGRATION: replaces PortalSecurity.SignOut() (PortalSecurity.vb L77-95), which
   * called FormsAuthentication.SignOut() and expired the language/authentication/
   * portalaliasid/portalroles cookies. The stateless JWT model only needs to notify the
   * server best-effort (so it can invalidate the refresh token) and clear the local
   * session; the local cleanup runs on BOTH success and error so a failed/offline server
   * call never strands the user in a half-logged-in state.
   */
  logout(): void {
    this.api.post<void>(this.api.authUrl('logout'), {}).subscribe({
      next: () => this.completeLogout(),
      error: () => this.completeLogout(),
    });
  }

  /**
   * Clear the local session WITHOUT contacting the server.
   *
   * MIGRATION (M10/DEV-038): the local-only counterpart to {@link logout}. It is
   * called by `auth.interceptor.ts` when a token refresh has ALREADY failed. Using
   * the server-notifying `logout()` on that path would issue another intercepted
   * `POST /api/auth/logout` request whose own 401 could re-enter the refresh handler
   * and trigger a further logout — a refresh/logout recursion loop. `clearSession()`
   * performs ONLY the local teardown (clear the token/user signals + their
   * localStorage mirror and redirect to the login route), breaking that cycle.
   * A normal user-initiated logout continues to use `logout()` so the server is still
   * notified best-effort and the standard `/api/auth/logout` request keeps its Bearer
   * header (it is intentionally NOT on the interceptor skip list).
   */
  clearSession(): void {
    this.completeLogout();
  }

  /**
   * Fetch the current user from the access-token-authenticated session.
   *
   * GET /api/auth/me -> refreshes the cached `currentUser` signal (and its localStorage
   * mirror). Useful after a reload to re-hydrate the user from the server of record.
   */
  me(): Observable<User> {
    return this.api
      .get<User>(this.api.authUrl('me'))
      .pipe(tap((user) => this.setUser(user)));
  }

  /**
   * Client-side RBAC helper for the `has-permission` directive (UI gating only).
   *
   * MIGRATION: replaces the server-side PortalSecurity.IsInRole / IsInRoles /
   * HasNecessaryPermission checks (PortalSecurity.vb L97-136, L469-529). Superusers are
   * always granted, mirroring the legacy `objUserInfo.IsSuperUser` shortcut in
   * IsInRoles. Authoritative authorization is still enforced server-side; this method
   * only gates UI affordances and never grants real access.
   */
  hasRole(role: string): boolean {
    const user = this.currentUser();
    if (user === null) {
      return false;
    }
    if (user.isSuperUser) {
      return true;
    }
    return user.roles.includes(role);
  }

  // --- private helpers ---

  /** Persist a freshly issued/rotated session (tokens + user) to signals and storage. */
  private storeSession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.refreshToken.set(response.refreshToken);
    this.setUser(response.user);
    this.write(ACCESS_TOKEN_KEY, response.accessToken);
    this.write(REFRESH_TOKEN_KEY, response.refreshToken);
  }

  /** Set the current user signal and mirror it (or its removal) to storage. */
  private setUser(user: User | null): void {
    this.currentUser.set(user);
    if (user === null) {
      this.remove(CURRENT_USER_KEY);
    } else {
      this.write(CURRENT_USER_KEY, JSON.stringify(user));
    }
  }

  /** Clear all session state (signals + storage) and navigate to the login route. */
  private completeLogout(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.currentUser.set(null);
    this.remove(ACCESS_TOKEN_KEY);
    this.remove(REFRESH_TOKEN_KEY);
    this.remove(CURRENT_USER_KEY);
    void this.router.navigate(['/auth/login']);
  }

  /** Read a raw string from localStorage, tolerating any storage access failure. */
  private readString(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  /** Read and parse the persisted user, tolerating absent or malformed JSON. */
  private readUser(): User | null {
    try {
      const raw = localStorage.getItem(CURRENT_USER_KEY);
      return raw ? (JSON.parse(raw) as User) : null;
    } catch {
      return null;
    }
  }

  /** Write a value to localStorage, ignoring failures (private mode / quota exceeded). */
  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* ignore storage failures (private mode / quota) */
    }
  }

  /** Remove a key from localStorage, ignoring any storage access failure. */
  private remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      /* ignore storage failures */
    }
  }
}
