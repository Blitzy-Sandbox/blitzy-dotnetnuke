import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { ApiService } from '../services/api.service';
import { AuthResponse, LoginRequest, RefreshRequest } from '../models/auth.model';
import { User } from '../models/user.model';

/**
 * localStorage keys for cross-reload persistence of the JWT session.
 *
 * Namespaced under the `dnn.` prefix to avoid collisions with any other origin
 * state. The in-memory signals below are the runtime source of truth; these keys
 * only mirror that state so a hard reload (F5) can rehydrate it.
 */
const ACCESS_TOKEN_KEY = 'dnn.accessToken';
const REFRESH_TOKEN_KEY = 'dnn.refreshToken';
const CURRENT_USER_KEY = 'dnn.currentUser';

/**
 * AuthService - signal-based JWT authentication singleton for the Angular 19 SPA.
 *
 * This is the FOUNDATIONAL `core/auth` service: `auth.guard.ts`,
 * `auth.interceptor.ts`, `layout/header`, and the `shared/has-permission`
 * directive all consume the public surface declared here (the exported name
 * `AuthService` is a hard contract and must not be renamed).
 *
 * MIGRATION (SANCTIONED): Replaces the legacy DotNetNuke Forms Authentication +
 * DES security model in `Library/Components/Security/PortalSecurity.vb`. This is
 * the single sanctioned behavior change of the migration (AAP §0.6.2). The
 * corresponding entries in the root MIGRATION_NOTES.md Deviation Index are
 * D-001 (Forms Auth -> stateless JWT), D-002 (DES -> BCrypt, server-side), and
 * D-003 (HasNecessaryPermission -> Angular `has-permission`); see also
 * MIGRATION_NOTES.md §3 (Sanctioned Behavior Change: Authentication & Cryptography):
 *   - Forms Authentication (FormsAuthentication.SignOut + portal cookies,
 *     PortalSecurity.vb L77-90) -> stateless JWT Bearer tokens.
 *   - DES Encrypt/Decrypt (PortalSecurity.vb L138-211) is REMOVED from the client;
 *     password hashing is server-side BCrypt (PasswordHasher.cs). NO crypto here.
 *
 * Token storage: in-memory Angular signals are the runtime source of truth, while
 * localStorage adds cross-reload persistence (so the route guard still passes after
 * a hard refresh).
 * NOTE: httpOnly cookies are the preferred production hardening (AAP §0.7.2) and the
 * refresh token could later move server-side, but the `auth.interceptor.ts`
 * Bearer-header design requires a JS-readable access token, so signals + localStorage
 * are used here. All localStorage access is wrapped in try/catch to stay defensive
 * against private-mode / quota failures.
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
   * POST /api/auth/login -> store the issued tokens and authenticated user.
   *
   * MIGRATION (SANCTIONED): replaces the legacy Forms Authentication sign-in
   * (PortalSecurity.vb). The plaintext password travels over HTTPS and is verified
   * server-side against the stored BCrypt hash; no DES round-trip occurs here.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.api
      .post<AuthResponse>(this.api.authUrl('login'), credentials)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * POST /api/auth/refresh -> ROTATE the access AND refresh tokens (and refresh
   * the cached user). The backend issues a brand-new refresh token on every call;
   * `storeSession` persists both so the next refresh uses the rotated value.
   */
  refresh(): Observable<AuthResponse> {
    const payload: RefreshRequest = { refreshToken: this.refreshToken() ?? '' };
    return this.api
      .post<AuthResponse>(this.api.authUrl('refresh'), payload)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Log the user out: clear ALL client session state immediately, redirect to the
   * login route, then fire a best-effort (fire-and-forget) server notification.
   *
   * ORDER MATTERS (CP2 auth-chain fix): the local session is cleared FIRST - via
   * `clearSession()` - BEFORE the `POST /api/auth/logout` courtesy notification.
   * Clearing first means the stale access / refresh tokens can never be replayed by
   * the interceptor: by the time the courtesy request resolves (e.g. a 401 on the
   * now-tokenless call), `refreshToken()` is already null, so the interceptor's
   * `401 -> refresh` recovery is skipped and the
   * `logout -> 401 -> refresh -> logout` recursion is impossible. The server logout
   * is a stateless no-op in Phase 1 (no server-side refresh-token store; see
   * IAuthService.LogoutAsync), so issuing it after clearing local state is purely a
   * forward-looking courtesy and its outcome is intentionally ignored.
   *
   * MIGRATION (SANCTIONED): replaces PortalSecurity.SignOut() (PortalSecurity.vb
   * L77-90), which called FormsAuthentication.SignOut() and expired the language /
   * authentication / portalaliasid / portalroles cookies. See MIGRATION_NOTES.md §3.1.
   */
  logout(): void {
    // Snapshot whether a session existed BEFORE clearing, so we only bother the
    // server with a courtesy notification when there was actually a session to end.
    const hadSession = this.accessToken() !== null;
    this.clearSession();
    if (hadSession) {
      this.api.post<void>(this.api.authUrl('logout'), {}).subscribe({
        next: () => undefined,
        error: () => undefined,
      });
    }
  }

  /**
   * Clear ALL client session state (signals + localStorage) and redirect to the
   * login route exactly once - WITHOUT contacting the server.
   *
   * This is the local-only logout path and the safe entry point for the
   * `auth.interceptor.ts` refresh-FAILURE branch. Because it issues NO HTTP request,
   * it cannot re-enter the interceptor and therefore cannot trigger the
   * `logout -> 401 -> refresh -> logout` recursion that calling `logout()` from the
   * interceptor would cause (CP2 auth-chain fix; see MIGRATION_NOTES.md §3.1).
   *
   * It is also reused by `logout()` (which adds the best-effort server notification)
   * so the local-clear-and-redirect behavior lives in exactly one place.
   */
  clearSession(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.currentUser.set(null);
    this.remove(ACCESS_TOKEN_KEY);
    this.remove(REFRESH_TOKEN_KEY);
    this.remove(CURRENT_USER_KEY);
    void this.router.navigate(['/auth/login']);
  }

  /** GET /api/auth/me -> refresh the cached current user from the server. */
  me(): Observable<User> {
    return this.api
      .get<User>(this.api.authUrl('me'))
      .pipe(tap((user) => this.setUser(user)));
  }

  /**
   * Client-side RBAC helper for the `shared/has-permission` directive.
   *
   * MIGRATION (SANCTIONED): replaces the server-side PortalSecurity.IsInRole /
   * IsInRoles / HasNecessaryPermission checks (PortalSecurity.vb L103-136). The
   * legacy superuser shortcut (`objUserInfo.IsSuperUser`, L123) is preserved:
   * super users are always granted. This ONLY gates UI affordances - authoritative
   * authorization remains enforced server-side via ASP.NET Core policies
   * (MIGRATION_NOTES.md D-003).
   */
  hasRole(role: string): boolean {
    const user = this.currentUser();
    if (user === null) {
      return false;
    }
    if (user.isSuperUser) {
      return true;
    }
    // MIGRATION: `User.roles` is `string[] | null` (the backend projection may omit
    // role names). A null/absent role set means "no roles", so guard before the
    // membership test rather than assuming a populated array.
    return user.roles?.includes(role) ?? false;
  }

  // --- private helpers ---

  /** Persist a freshly issued session: update signals first, then mirror to storage. */
  private storeSession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.refreshToken.set(response.refreshToken);
    this.setUser(response.user);
    this.write(ACCESS_TOKEN_KEY, response.accessToken);
    this.write(REFRESH_TOKEN_KEY, response.refreshToken);
  }

  /** Set (or clear) the current user across both the signal and localStorage. */
  private setUser(user: User | null): void {
    this.currentUser.set(user);
    if (user === null) {
      this.remove(CURRENT_USER_KEY);
    } else {
      this.write(CURRENT_USER_KEY, JSON.stringify(user));
    }
  }

  /** Read a raw string from localStorage, tolerating storage being unavailable. */
  private readString(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  /** Read and parse the persisted current user, tolerating storage/parse failures. */
  private readUser(): User | null {
    try {
      const raw = localStorage.getItem(CURRENT_USER_KEY);
      return raw ? (JSON.parse(raw) as User) : null;
    } catch {
      return null;
    }
  }

  /** Write a value to localStorage, ignoring private-mode / quota failures. */
  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* ignore storage failures (private mode / quota) */
    }
  }

  /** Remove a key from localStorage, ignoring storage failures. */
  private remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      /* ignore storage failures */
    }
  }
}
