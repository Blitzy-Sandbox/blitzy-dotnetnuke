import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, of, tap } from 'rxjs';

import { ApiService } from '../services/api.service';
import { AuthResponse, LoginRequest } from '../models/auth.model';
import { User } from '../models/user.model';

/**
 * localStorage key for cross-reload persistence of the authenticated user's
 * (non-secret) client-facing projection.
 *
 * Namespaced with a `dnn.` prefix to avoid collisions with any other key in the
 * origin's storage. Kept as a module-private constant so every read/write site
 * references the exact same key string.
 *
 * MIGRATION (Finding CP-FINAL-2 / CWE-922): the access and refresh TOKENS are no
 * longer persisted to `localStorage`. The refresh token lives only in a JS-opaque
 * `HttpOnly` cookie, and the access token is held in an in-memory signal that is
 * re-minted on reload via `initializeSession()`. ONLY the non-secret user
 * projection is mirrored here, purely so the route guard and chrome can render
 * synchronously on reload while the access token is being restored.
 */
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
 * behavior change of the migration (AAP Â§0.6.2); the full narrative is recorded in
 * the root `MIGRATION_NOTES.md` (Â§3 â€” Sanctioned Behavior Change). Specifically:
 *   - Forms Authentication (FormsAuthentication.SignOut + portal cookies,
 *     PortalSecurity.vb L77-95) -> stateless JWT Bearer tokens. The server holds no
 *     session; identity travels in the access-token claims (enables horizontal scaling).
 *   - DES Encrypt/Decrypt (DESCryptoServiceProvider/CryptoStream, PortalSecurity.vb
 *     L138-211) -> REMOVED from the client entirely; password hashing is server-side
 *     BCrypt (PasswordHasher.cs). No cryptography is performed in this class.
 *
 * Token storage (MIGRATION Finding CP-FINAL-2 / CWE-922 â€” secure-storage hardening):
 *   - REFRESH token: never touches JavaScript. The API issues it as an `HttpOnly`,
 *     `Secure`, `SameSite=Strict` cookie (`dnn_refresh_token`, path `/api/auth`), so it
 *     cannot be read or exfiltrated by XSS. `refresh()` therefore sends NO token in the
 *     body â€” the browser attaches the cookie automatically (`withCredentials`).
 *   - ACCESS token: held ONLY in the in-memory `accessToken` signal (the
 *     `Authorization: Bearer` header injected by `auth.interceptor.ts` requires a
 *     JS-readable value). It is intentionally NOT persisted to `localStorage`; on a full
 *     page reload it is transparently re-minted from the refresh cookie by
 *     `initializeSession()` (wired as an Angular app initializer in `app.config.ts`).
 *   - USER projection: the non-secret `currentUser` is mirrored to `localStorage` so the
 *     guard/chrome can render synchronously on reload; it carries no credential material.
 * This is the AAP Â§0.7.2 "httpOnly preferred" secure token-storage requirement; the full
 * narrative and the accepted stateless-refresh residual risk are in `MIGRATION_NOTES.md`.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  /**
   * Current JWT access token. In-memory ONLY (never persisted) and null when logged
   * out or immediately after a reload until `initializeSession()` re-mints it from the
   * refresh cookie. Runtime source of truth for the `Authorization: Bearer` header.
   */
  readonly accessToken = signal<string | null>(null);
  /** The authenticated user (null when logged out). Mirrored to localStorage. */
  readonly currentUser = signal<User | null>(this.readUser());

  /** True when an access token is present. Consumed by authGuard and layout chrome. */
  readonly isAuthenticated = computed<boolean>(() => this.accessToken() !== null);

  /**
   * True when a (persisted) user session is believed to exist â€” i.e. a `currentUser`
   * is present even if the in-memory access token has not yet been re-minted after a
   * reload. `auth.interceptor.ts` gates its 401 -> refresh recovery on this (rather than
   * on a now-removed JS-readable refresh token) so a reload mid-session can still
   * transparently rotate via the `HttpOnly` refresh cookie.
   */
  readonly hasSession = computed<boolean>(() => this.currentUser() !== null);

  /**
   * Authenticate with username + password.
   *
   * POST /api/auth/login -> stores the issued access token and user. The refresh token
   * is delivered out-of-band as an `HttpOnly` cookie (never in the body), so nothing
   * refresh-related is stored client-side. Routed through ApiService (which unwraps the
   * `{ data }` envelope); this class never touches HttpClient directly.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    // `withCredentials: true` lets the browser store the `HttpOnly` refresh cookie the
    // API sets on a successful login (required for cross-origin dev: SPA :4200 -> API).
    return this.api
      .post<AuthResponse>(this.api.authUrl('login'), credentials, undefined, true)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Rotate the session tokens.
   *
   * POST /api/auth/refresh -> the backend reads the refresh token from the `HttpOnly`
   * `dnn_refresh_token` cookie (NOT the request body), performs refresh-token ROTATION,
   * returns a NEW access token in the body plus a NEW refresh cookie, and the refreshed
   * user. MIGRATION (Finding CP-FINAL-2): the request body is intentionally EMPTY and
   * `withCredentials: true` ensures the browser attaches the refresh cookie.
   */
  refresh(): Observable<AuthResponse> {
    return this.api
      .post<AuthResponse>(this.api.authUrl('refresh'), undefined, undefined, true)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Re-establish the in-memory session on application start (wired as an Angular app
   * initializer in `app.config.ts`).
   *
   * MIGRATION (Finding CP-FINAL-2): because the access token is in-memory only, a full
   * page reload loses it. When a `currentUser` was persisted we attempt ONE cookie-backed
   * `refresh()` to silently re-mint the access token from the still-valid `HttpOnly`
   * refresh cookie. On failure (expired/absent cookie) we clear local state WITHOUT
   * navigating â€” the app is still bootstrapping and the route guard will redirect as
   * needed. When no user was persisted we short-circuit and issue no HTTP at all.
   *
   * Returns an Observable that ALWAYS completes successfully (errors are swallowed) so a
   * failed refresh never blocks Angular's bootstrap.
   */
  initializeSession(): Observable<unknown> {
    if (this.currentUser() === null) {
      return of(void 0);
    }
    return this.refresh().pipe(
      catchError(() => {
        this.clearLocalState();
        return of(void 0);
      }),
    );
  }

  /**
   * Log out: ask the server to expire the refresh cookie, then clear client state and
   * redirect to the login route.
   *
   * MIGRATION: replaces PortalSecurity.SignOut() (PortalSecurity.vb L77-95), which
   * called FormsAuthentication.SignOut() and expired the language/authentication/
   * portalaliasid/portalroles cookies. The stateless JWT model notifies the server
   * best-effort so it can delete the `HttpOnly` `dnn_refresh_token` cookie (no
   * server-side token store exists by design â€” the accepted stateless-rotation residual
   * risk is documented in `MIGRATION_NOTES.md`), then clears the local session. The local
   * cleanup runs on BOTH success and error so a failed/offline server call never strands
   * the user in a half-logged-in state.
   */
  logout(): void {
    // `withCredentials: true` sends the `HttpOnly` refresh cookie so the server can
    // expire it (the response's Set-Cookie deletion is what clears it client-side).
    this.api.post<void>(this.api.authUrl('logout'), undefined, undefined, true).subscribe({
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
   * and trigger a further logout â€” a refresh/logout recursion loop. `clearSession()`
   * performs ONLY the local teardown (clear the in-memory access-token signal, the
   * `currentUser` signal and its localStorage mirror) and redirects to the login route,
   * breaking that cycle. A normal user-initiated logout continues to use `logout()` so
   * the server is still notified best-effort and the standard `/api/auth/logout` request
   * keeps its Bearer header (it is intentionally NOT on the interceptor skip list).
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

  /**
   * Persist a freshly issued/rotated session to runtime state.
   *
   * The access token is held in-memory ONLY; the refresh token is delivered out-of-band
   * as the `HttpOnly` cookie and is intentionally never read or stored here. Only the
   * non-secret user projection is mirrored to localStorage (by `setUser`).
   */
  private storeSession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.setUser(response.user);
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

  /**
   * Clear all LOCAL session state (the in-memory access-token signal, the currentUser
   * signal and its localStorage mirror) WITHOUT navigating. Shared by the logout paths
   * and by `initializeSession()` (which must not navigate during bootstrap).
   */
  private clearLocalState(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);
    this.remove(CURRENT_USER_KEY);
  }

  /** Clear all local session state and navigate to the login route. */
  private completeLogout(): void {
    this.clearLocalState();
    void this.router.navigate(['/auth/login']);
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
