// MIGRATION: PortalSecurity.vb (UserLogin / SignOut / DES Encrypt-Decrypt) + UserMembership.vb (membership
// state) -> signal-based AuthService that consumes the backend JWT/BFF contract. The legacy ASP.NET 2.0 Forms
// authentication / AspNetSqlMembershipProvider and DES credential encryption are replaced by JWT Bearer + BCrypt
// ON THE BACKEND; this Angular service only orchestrates login/refresh/logout/me against /api/v1/auth and holds
// authentication state in memory signals.
import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, finalize, map, of, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import type { ApiEnvelope } from '../models/api-envelope.model';
import type {
  AuthResponse,
  CurrentUser,
  LoginRequest,
  PasswordResetRequest,
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
    // MIGRATION: the backend wraps EVERY response in the { data, meta } success envelope (AAP Section 0.1.2).
    // AuthService is the documented exception that calls HttpClient directly (avoiding an ApiService cycle), so it
    // must unwrap `data` ITSELF -- exactly as ApiService.post does -- before the access/refresh tokens are read.
    return this.http
      .post<ApiEnvelope<AuthResponse>>(`${this.authUrl}/login`, credentials)
      .pipe(
        map((envelope) => envelope.data),
        tap((response) => this.setSession(response)),
      );
  }

  /**
   * POST /api/v1/auth/refresh using the stored refresh token.
   * MIGRATION: refresh-token ROTATION -- each successful response carries a NEW refreshToken that replaces the
   * stored one. Invoked by core/interceptors/error.interceptor on a 401. A failed refresh returns HTTP 400
   * (not 401), so it cannot trigger a refresh loop.
   */
  refresh(): Observable<AuthResponse> {
    const request: RefreshRequest = { refreshToken: this._refreshToken() ?? '' };
    // MIGRATION: unwrap the { data, meta } envelope before reading the ROTATED refresh token, otherwise the
    // rotated token would be read as undefined and the next refresh would fail (breaking the retry flow).
    return this.http
      .post<ApiEnvelope<AuthResponse>>(`${this.authUrl}/refresh`, request)
      .pipe(
        map((envelope) => envelope.data),
        tap((response) => this.setSession(response)),
      );
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
    // MIGRATION: unwrap the { data, meta } envelope so the currentUser signal is hydrated with the actual
    // CurrentUserDto payload (the envelope root has no userId/roles fields -- reading it directly leaves the
    // role-based UI gating empty/incorrect).
    return this.http
      .get<ApiEnvelope<CurrentUser>>(`${this.authUrl}/me`)
      .pipe(
        map((envelope) => envelope.data),
        tap((user) => this._currentUser.set(user)),
      );
  }

  /**
   * Request a password-reminder/reset email.
   *
   * MIGRATION: DEFERRED. Re-expresses Website/admin/Security/SendPassword.ascx.vb cmdSendPassword_Click
   * (UserController.GetUserByUserName / uniquely-matching email -> Mail.SendMail PasswordReminder + EventLog).
   * The frozen backend AuthController (AAP Section 0.3.4) exposes ONLY login/refresh/logout/me -- there is NO
   * `/auth/forgot-password` endpoint in this phase. Rather than issue a request that would 404, this is a
   * client-side NO-OP that completes successfully. That also PRESERVES the non-enumeration policy: the UI always
   * shows the same generic confirmation and never reveals whether the account exists. This keeps the
   * forgot-password feature wired to the feature service (NOT the generic ApiService) per AAP Section 0.7.3; it
   * must be connected to a real backend endpoint before it functions at runtime. Tracked in MIGRATION_NOTES.md.
   */
  requestPasswordReset(_request: PasswordResetRequest): Observable<void> {
    return of(void 0);
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
