import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { ApiService } from '../services/api.service';
import { AuthResponse, LoginRequest } from '../models/auth.model';
import { User } from '../models/user.model';

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
 * the single sanctioned behavior change of the migration (AAP Â§0.6.2). The
 * corresponding entries in the root MIGRATION_NOTES.md Deviation Index are
 * D-001 (Forms Auth -> stateless JWT), D-002 (DES -> BCrypt, server-side), and
 * D-003 (HasNecessaryPermission -> Angular `has-permission`); see also
 * MIGRATION_NOTES.md Â§3 (Sanctioned Behavior Change: Authentication & Cryptography):
 *   - Forms Authentication (FormsAuthentication.SignOut + portal cookies,
 *     PortalSecurity.vb L77-90) -> stateless JWT Bearer tokens.
 *   - DES Encrypt/Decrypt (PortalSecurity.vb L138-211) is REMOVED from the client;
 *     password hashing is server-side BCrypt (PasswordHasher.cs). NO crypto here.
 *
 * SECURE TOKEN STORAGE (AAP Â§0.7.2 non-functional requirement; MIGRATION_NOTES.md Â§3.1):
 * Tokens are NEVER placed in `localStorage`/`sessionStorage`, so a successful XSS on the
 * SPA origin cannot exfiltrate a durable credential from web storage:
 *   - ACCESS TOKEN: lives ONLY in the in-memory `accessToken` signal below. It is the
 *     runtime source of truth for the `Authorization: Bearer` header (attached by
 *     `auth.interceptor.ts`) and dies with the tab/reload.
 *   - REFRESH TOKEN: is NOT readable by JavaScript at all. The backend issues it as an
 *     HttpOnly + Secure + SameSite=Strict cookie scoped to `/api/auth`
 *     (`AuthController.IssueRefreshCookie`, which then nulls the body `refreshToken`). The
 *     SPA neither stores nor reads it; the browser sends it automatically on the
 *     `login` / `refresh` / `logout` calls, which use `withCredentials`.
 *   - HARD RELOAD (F5): the in-memory access token is gone, so `app.config.ts`'s
 *     `provideAppInitializer` calls `refresh()` at bootstrap. That call presents the
 *     HttpOnly refresh cookie and rehydrates BOTH the access token and the current user
 *     BEFORE the router/`authGuard` run. With no valid cookie the refresh simply fails
 *     and the app boots unauthenticated.
 * This replaces the previous localStorage persistence (CP fix; the prior design kept a
 * JS-readable refresh token in `localStorage`, which the FINAL review flagged as an XSS
 * exfiltration risk).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  /**
   * Current JWT access token (null when logged out). MEMORY-ONLY runtime source of truth -
   * intentionally NOT persisted to web storage. Rehydrated on hard reload via the
   * bootstrap `refresh()` (see the class doc) using the HttpOnly refresh cookie.
   */
  readonly accessToken = signal<string | null>(null);

  /**
   * The authenticated user (null when logged out). MEMORY-ONLY (never persisted); it is
   * re-populated from each `login()` / `refresh()` / `me()` response, including the
   * bootstrap refresh that runs on a hard reload.
   */
  readonly currentUser = signal<User | null>(null);

  /** True when an access token is present. Consumed by authGuard, the interceptor, and layout chrome. */
  readonly isAuthenticated = computed<boolean>(() => this.accessToken() !== null);

  /**
   * POST /api/auth/login -> store the issued access token and authenticated user, and let
   * the browser persist the HttpOnly refresh cookie.
   *
   * `withCredentials` is set so the browser ACCEPTS and stores the `Set-Cookie` the backend
   * returns for the refresh token. The plaintext password travels over HTTPS and is verified
   * server-side against the stored BCrypt hash; no DES round-trip occurs here.
   *
   * MIGRATION (SANCTIONED): replaces the legacy Forms Authentication sign-in (PortalSecurity.vb).
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.api
      .post<AuthResponse>(this.api.authUrl('login'), credentials, undefined, true)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * POST /api/auth/refresh -> ROTATE the access token (and refresh the cached user), with the
   * refresh token carried by the HttpOnly cookie rather than the request body.
   *
   * `withCredentials` is set so the browser SENDS the existing refresh cookie and accepts the
   * rotated replacement cookie the backend returns. The request body is intentionally empty:
   * the backend reads the refresh token from the cookie first (with a body fallback that the
   * SPA no longer uses). `storeSession` updates the in-memory access token + user from the
   * response so the next interceptor retry uses the rotated token.
   *
   * SECURITY: server-side rotation is replay-resistant - the backend persists a hashed
   * refresh-token family and revokes the prior token on each rotation (see
   * `AuthService.RefreshAsync` / `IRefreshTokenStore`), so a stolen refresh cookie cannot be
   * reused after a legitimate refresh.
   */
  refresh(): Observable<AuthResponse> {
    return this.api
      .post<AuthResponse>(this.api.authUrl('refresh'), {}, undefined, true)
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Log the user out: notify the server (so it can revoke the refresh-token family and clear
   * the HttpOnly cookie) WHILE the Bearer token is still in memory, then clear all local
   * session state and redirect to the login route regardless of the server outcome.
   *
   * ORDER MATTERS (revocation contract): `POST /api/auth/logout` is `[Authorize]` server-side -
   * it extracts the user id from the Bearer JWT to revoke that user's refresh-token family and
   * deletes the refresh cookie (`withCredentials` sends the cookie). It MUST therefore be issued
   * BEFORE the access token is cleared. The local session is cleared in BOTH the success and
   * error callbacks so a failed/expired logout still ends the client session.
   *
   * ANTI-LOOP: a stale access token makes the logout 401; the interceptor performs a single
   * bounded `401 -> refresh -> retry` recovery (the refresh uses the cookie). If that refresh
   * also fails, the interceptor calls `clearSession()` (local-only, no HTTP), so no
   * `logout -> 401 -> refresh -> logout` recursion is possible (CP2 auth-chain invariant; the
   * retry happens AT MOST ONCE). When there is no token at all we skip the server call and
   * clear locally.
   *
   * MIGRATION (SANCTIONED): replaces PortalSecurity.SignOut() (PortalSecurity.vb L77-90), which
   * called FormsAuthentication.SignOut() and expired the portal cookies. See MIGRATION_NOTES.md Â§3.1.
   */
  logout(): void {
    if (this.accessToken() === null) {
      // No active session: nothing to revoke server-side; just clear locally and redirect.
      this.clearSession();
      return;
    }
    this.api.post<void>(this.api.authUrl('logout'), {}, undefined, true).subscribe({
      next: () => this.clearSession(),
      error: () => this.clearSession(),
    });
  }

  /**
   * Clear ALL client session state (the in-memory signals) and redirect to the login route
   * exactly once - WITHOUT contacting the server.
   *
   * This is the local-only logout path and the safe entry point for the
   * `auth.interceptor.ts` refresh-FAILURE branch. Because it issues NO HTTP request, it
   * cannot re-enter the interceptor and therefore cannot trigger the
   * `logout -> 401 -> refresh -> logout` recursion that calling `logout()` from the
   * interceptor would cause (CP2 auth-chain fix; see MIGRATION_NOTES.md Â§3.1). There is no
   * web-storage to purge here because tokens are never persisted (see the class doc); the
   * server-side refresh-token family is revoked by `logout()`'s server call, not here.
   *
   * RETURN-URL: when the session is cleared from a guarded deep link (e.g. the interceptor's
   * refresh-failure path while the user is on `/modules`), the current URL is appended to the
   * login redirect as a `returnUrl` query parameter so the login flow can restore the
   * destination after re-authentication. The self-referential `/auth/login` route and the bare
   * root `/` are excluded to avoid a redirect loop.
   */
  clearSession(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);

    // Capture the URL the user was on so the login flow can redirect back after
    // re-authentication (parity with `auth.guard.ts`; the `returnUrl` consumer is
    // `login.component.ts` `resolveReturnUrl()`). Skip appending `returnUrl` when
    // there is nothing meaningful to return to (the app root) or when we are
    // already on the login route, to avoid a self-referential
    // `returnUrl=/auth/login` redirect loop.
    const attemptedUrl = this.router.url;
    if (
      attemptedUrl &&
      attemptedUrl !== '/' &&
      !attemptedUrl.startsWith('/auth/login')
    ) {
      void this.router.navigate(['/auth/login'], {
        queryParams: { returnUrl: attemptedUrl },
      });
      return;
    }

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

  /**
   * Persist a freshly issued session into the in-memory signals. Only the access token and
   * user are read from the response; the refresh token is delivered out-of-band as the
   * HttpOnly cookie and is intentionally NOT read here (see the class doc).
   */
  private storeSession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.setUser(response.user);
  }

  /** Set (or clear) the current user signal. Memory-only - no persistence. */
  private setUser(user: User | null): void {
    this.currentUser.set(user);
  }
}
