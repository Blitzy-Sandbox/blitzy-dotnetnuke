import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';

import { ApiService } from '../services/api.service';
import {
  AuthResponse,
  CurrentUser,
  LoginRequest,
  MeResponse,
  RefreshRequest,
} from '../models';

/**
 * AuthService — the SPA's JWT authentication foundation.
 *
 * MIGRATION: replaces DotNetNuke Forms Authentication + PortalSecurity
 * (Library/Components/Security/PortalSecurity.vb). The legacy FormsAuthentication
 * cookie session + SecurityAccessLevel (Anonymous/View/Edit/Admin/Host) + DES
 * encryption are REPLACED by a stateless JWT bearer flow:
 *   PortalSecurity.SignOut()           -> logout()
 *   IsInRole / IsInRoles / IsSuperUser -> the roles() / isSuperUser() computed selectors
 *   DES / Forms auth cookies           -> an in-memory access token attached by
 *                                         core/auth/auth.interceptor.ts
 *
 * All HTTP flows through the injected ApiService (AAP §0.7.1 — Angular services
 * handle API communication only; this service NEVER calls HttpClient directly
 * and NEVER attaches tokens itself).
 *
 * MIGRATION (token storage): tokens are held MEMORY-ONLY in private signals
 * (never localStorage/sessionStorage) to prevent XSS token theft. httpOnly
 * cookies would be the most secure option, but the backend TokenResponseDto
 * returns the tokens in the JSON body, so the SPA keeps the access token in
 * memory and the interceptor attaches it as a Bearer header. See MIGRATION_NOTES.md.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiService = inject(ApiService);
  private readonly router = inject(Router);

  // --- Private, memory-only session state (backing signals) ---
  // MIGRATION: the legacy FormsAuthentication cookie / DES-encrypted session is
  // replaced by these in-memory signals. Declared BEFORE the public exposures
  // below because TypeScript class fields initialize top-to-bottom and the
  // read-only exposures/computeds reference them.
  private readonly _isAuthenticated = signal(false);
  private readonly _currentUser = signal<CurrentUser | null>(null);
  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(null);

  // --- Public reactive state (read-only) consumed by layout/ + shared/has-permission ---
  readonly isAuthenticated = this._isAuthenticated.asReadonly();
  readonly currentUser = this._currentUser.asReadonly();
  // MIGRATION: PortalSecurity.IsInRole / IsInRoles are surfaced as the reactive
  // roles() selector; consumers (shared/ has-permission directive) check
  // membership against this list instead of the legacy provider call.
  readonly roles = computed(() => this._currentUser()?.roles ?? []);
  // MIGRATION: UserInfo.IsSuperUser (host account) -> isSuperUser() selector.
  readonly isSuperUser = computed(() => this._currentUser()?.isSuperUser ?? false);

  /**
   * POST /api/auth/login — authenticate, store the tokens, then load the current user.
   * MIGRATION: replaces the Forms Authentication sign-in / PortalSecurity credential check.
   * Returns the token response; the current user is populated as a side effect so
   * callers (features/auth login component) can navigate once login resolves.
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.apiService.post<AuthResponse>('auth/login', credentials).pipe(
      tap((tokens) => this.setSession(tokens)),
      switchMap((tokens) => this.loadCurrentUser().pipe(map(() => tokens)))
    );
  }

  /**
   * POST /api/auth/refresh — silent JWT renewal (called by the interceptor on a 401).
   * MIGRATION: no DNN equivalent (Forms auth relied on server sessions); this renews
   * the bearer token in place. The refresh token is sent from memory; the backend
   * RefreshRequestDto.RefreshToken is a non-null string (defaults to string.Empty),
   * so an absent token is sent as an empty string to mirror the token-in-body contract.
   */
  refresh(): Observable<AuthResponse> {
    const body: RefreshRequest = { refreshToken: this._refreshToken() ?? '' };
    return this.apiService
      .post<AuthResponse>('auth/refresh', body)
      .pipe(tap((tokens) => this.setSession(tokens)));
  }

  /**
   * GET /api/auth/me — load the authenticated user and populate the currentUser signal.
   */
  loadCurrentUser(): Observable<CurrentUser> {
    return this.apiService.get<MeResponse>('auth/me').pipe(
      map((response) => response.user),
      tap((user) => {
        this._currentUser.set(user);
        this._isAuthenticated.set(true);
      })
    );
  }

  /**
   * POST /api/auth/logout — clear the client session and navigate to /auth.
   * MIGRATION: FormsAuthentication.SignOut() + cookie expiry -> in-memory token clear
   * + signal reset. The server call is best-effort (errors are swallowed) because the
   * client session is cleared regardless.
   */
  logout(): void {
    this.apiService
      .post<void>('auth/logout', {})
      .pipe(catchError(() => of(void 0)))
      .subscribe();
    this.clearSession();
    void this.router.navigate(['/auth']);
  }

  /**
   * Current in-memory access token, read by core/auth/auth.interceptor.ts to attach
   * the `Authorization: Bearer` header. Returns null when unauthenticated.
   */
  getAccessToken(): string | null {
    return this._accessToken();
  }

  /** Store the tokens from a login/refresh response (memory-only). */
  private setSession(tokens: AuthResponse): void {
    this._accessToken.set(tokens.accessToken);
    if (tokens.refreshToken) {
      this._refreshToken.set(tokens.refreshToken);
    }
    this._isAuthenticated.set(true);
  }

  /** Clear all in-memory session state. */
  private clearSession(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    this._currentUser.set(null);
    this._isAuthenticated.set(false);
  }
}
