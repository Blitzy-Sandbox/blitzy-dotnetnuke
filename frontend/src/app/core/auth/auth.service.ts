// MIGRATION: PortalSecurity.vb (UserLogin / SignOut / DES Encrypt-Decrypt) + UserMembership.vb (membership
// state) -> signal-based AuthService that consumes the backend JWT/BFF contract. The legacy ASP.NET 2.0 Forms
// authentication / AspNetSqlMembershipProvider and DES credential encryption are replaced by JWT Bearer + BCrypt
// ON THE BACKEND; this Angular service only orchestrates login/refresh/logout/me against /api/v1/auth and holds
// authentication state in memory signals.
import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, finalize, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  AuthResponse,
  CurrentUser,
  LoginRequest,
  RefreshRequest,
} from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // MIGRATION: inject() DI (Angular 19) replaces VB shared/static access to PortalSecurity / UserController.
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // environment.apiUrl already includes the '/api/v1' version segment (prod: '/api/v1',
  // dev: 'http://localhost:8080/api/v1'). AAP Section 0.3.4 occasionally writes '/api/auth/...' without the
  // version, but the versioned apiUrl is authoritative, so we only append the relative 'auth' resource path.
  private readonly authUrl = `${environment.apiUrl}/auth`;

  // MIGRATION: secure token storage = MEMORY-ONLY signals (AAP Section 0.7.6). The access token is held in
  // memory so core/interceptors/token.interceptor can attach `Authorization: Bearer <accessToken>`;
  // localStorage / sessionStorage are intentionally AVOIDED for these high-value tokens (XSS exfiltration risk).
  // Trade-off: auth state is cleared on a full page reload and the user re-authenticates. See MIGRATION_NOTES.md.
  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(null);
  private readonly _expiresAt = signal<number | null>(null);
  private readonly _currentUser = signal<CurrentUser | null>(null);

  /** Read-only access token signal consumed by core/interceptors/token.interceptor. */
  readonly accessToken = this._accessToken.asReadonly();
  /** Read-only current-user signal consumed by the layout/feature components. */
  readonly currentUser = this._currentUser.asReadonly();
  /** Read-only access-token expiry (epoch milliseconds) signal. */
  readonly expiresAt = this._expiresAt.asReadonly();

  // MIGRATION: replaces PortalSecurity / HttpContext.Request.IsAuthenticated -- the user is authenticated when an
  // access token is present in memory. Consumed by authGuard and the error interceptor.
  readonly isAuthenticated = computed(() => this._accessToken() !== null);

  /**
   * POST /api/v1/auth/login.
   * MIGRATION: PortalSecurity.UserLogin(Username, Password, PortalID, ...) -- multi-tenant login;
   * credentials.portalId is REQUIRED and credentials.rememberMe maps to the legacy CreatePersistentCookie flag.
   * Login FAILURES return HTTP 400 (RFC 7807), NOT 401, and are rate-limited (429); the failure is propagated to
   * the caller through the returned Observable.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.authUrl}/login`, credentials)
      .pipe(tap((response) => this.setSession(response)));
  }

  /**
   * POST /api/v1/auth/refresh using the stored refresh token.
   * MIGRATION: refresh-token ROTATION -- each successful response carries a NEW refreshToken that replaces the
   * stored one. Invoked by core/interceptors/error.interceptor on a 401. A failed refresh returns HTTP 400
   * (not 401), so it cannot trigger a refresh loop.
   */
  refresh(): Observable<AuthResponse> {
    const request: RefreshRequest = { refreshToken: this._refreshToken() ?? '' };
    return this.http
      .post<AuthResponse>(`${this.authUrl}/refresh`, request)
      .pipe(tap((response) => this.setSession(response)));
  }

  /**
   * POST /api/v1/auth/logout, then clear all in-memory auth state and navigate to the login page.
   * MIGRATION: PortalSecurity.SignOut() -- formerly FormsAuthentication.SignOut() plus cookie expiry; now clears
   * the memory signals. State is cleared on BOTH success and error so the client always ends up signed out.
   */
  logout(): Observable<void> {
    const request: RefreshRequest = { refreshToken: this._refreshToken() ?? '' };
    return this.http
      .post<void>(`${this.authUrl}/logout`, request)
      .pipe(finalize(() => this.clearSessionAndRedirect()));
  }

  /**
   * GET /api/v1/auth/me -- hydrate the currentUser signal.
   * MIGRATION: UserMembership.vb membership state -> CurrentUser (non-sensitive projection; the legacy
   * Password / PasswordQuestion / PasswordAnswer credentials are NEVER exposed to the client).
   */
  me(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>(`${this.authUrl}/me`)
      .pipe(tap((user) => this._currentUser.set(user)));
  }

  /** Persist token state from a login/refresh response (including refresh-token rotation). */
  private setSession(response: AuthResponse): void {
    this._accessToken.set(response.accessToken);
    this._refreshToken.set(response.refreshToken);
    // expiresIn is a seconds value (3600 = a 60-minute access token, AAP Section 0.7.6).
    this._expiresAt.set(Date.now() + response.expiresIn * 1000);
    if (response.user) {
      this._currentUser.set(response.user);
    }
  }

  /** Clear every in-memory auth signal and redirect to the login route. */
  private clearSessionAndRedirect(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    this._expiresAt.set(null);
    this._currentUser.set(null);
    void this.router.navigate(['/auth/login']);
  }
}
