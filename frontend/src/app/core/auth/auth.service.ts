/**
 * @fileoverview JWT Authentication Service for Angular 19 SPA
 * @description Core authentication service implementing JWT-based authentication,
 * replacing DNN's FormsAuthentication and MembershipProvider patterns.
 * 
 * This service provides:
 * - login/logout methods calling /api/auth endpoints
 * - Token storage/retrieval using localStorage
 * - Automatic token refresh via /api/auth/refresh
 * - Authentication state management using BehaviorSubject and Angular signals
 * - Current user info access
 * 
 * MIGRATION NOTE: Replaces DNN authentication components:
 * - UserController.UserLogin (Library/Components/Users/UserController.vb lines 991-1008)
 * - FormsAuthentication.SetAuthCookie (line 1033)
 * - PortalSecurity.SignOut (Library/Components/Security/PortalSecurity.vb lines 77-95)
 * - HttpContext.Current.Request.IsAuthenticated checks
 * - DNN's session-based MembershipProvider.UserLogin validation
 *   (UserController.vb ValidateUser lines 1110-1157) with stateless JWT validation
 * 
 * @module core/auth/auth.service
 */

import { Injectable, inject, computed, signal, Signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, tap, catchError, throwError, map, of } from 'rxjs';

import { User } from '../models/user.model';
import { LoginRequest, AuthResponse, RefreshRequest } from '../models/auth.model';

/**
 * Local storage key for JWT access token.
 * Prefixed with 'dnn_' to avoid conflicts with other applications.
 */
const ACCESS_TOKEN_KEY = 'dnn_access_token';

/**
 * Local storage key for JWT refresh token.
 * Used to obtain new access tokens without re-authentication.
 */
const REFRESH_TOKEN_KEY = 'dnn_refresh_token';

/**
 * Local storage key for cached user information.
 * Stores serialized User object for quick access on page reload.
 */
const USER_KEY = 'dnn_user';

/**
 * Base URL for authentication API endpoints.
 * All auth requests are prefixed with this path.
 */
const AUTH_API_URL = '/api/auth';

/**
 * Core authentication service providing JWT-based authentication for the Angular SPA.
 * 
 * This service is provided in root and serves as a singleton, acting as the foundation
 * for auth.guard.ts route protection and auth.interceptor.ts token injection.
 * 
 * Key features:
 * - Reactive authentication state using Angular signals and RxJS
 * - Secure token storage in localStorage
 * - Automatic token refresh capability
 * - Seamless integration with Angular 19 patterns
 * 
 * @example
 * ```typescript
 * // In a component
 * private authService = inject(AuthService);
 * 
 * // Check authentication state reactively
 * isLoggedIn = this.authService.isAuthenticated;
 * 
 * // Subscribe to user changes
 * this.authService.currentUser$.subscribe(user => {
 *   if (user) {
 *     console.log('Logged in as:', user.username);
 *   }
 * });
 * 
 * // Login
 * this.authService.login({ username: 'admin', password: 'password' })
 *   .subscribe(response => console.log('Logged in!'));
 * ```
 * 
 * MIGRATION NOTE: This service replaces DNN's cookie-based authentication with
 * stateless JWT tokens per BFF pattern best practices. JWT Bearer tokens have
 * a default expiry of 60 minutes (per Section 0.7.7 Security Rules).
 * 
 * @class AuthService
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  /**
   * Angular HttpClient for making API requests.
   * Injected using Angular 19's functional inject() pattern.
   */
  private readonly http = inject(HttpClient);

  /**
   * Angular Router for programmatic navigation.
   * Used to redirect to login page after logout.
   */
  private readonly router = inject(Router);

  /**
   * BehaviorSubject holding the current authenticated user.
   * Initialized from localStorage on service creation to persist auth state.
   * 
   * MIGRATION NOTE: Replaces DNN's HttpContext.Current.User principal.
   */
  private readonly currentUserSubject = new BehaviorSubject<User | null>(
    this.loadUserFromStorage()
  );

  /**
   * Internal signal mirroring the BehaviorSubject for computed signal support.
   * This enables reactive computed properties using Angular's signal API.
   */
  private readonly currentUserSignal = signal<User | null>(
    this.loadUserFromStorage()
  );

  /**
   * Observable stream of the current authenticated user.
   * Components can subscribe to this to react to authentication changes.
   * 
   * @public
   * @readonly
   * @type {Observable<User | null>}
   * 
   * @example
   * ```typescript
   * authService.currentUser$.subscribe(user => {
   *   this.userName = user?.displayName ?? 'Guest';
   * });
   * ```
   */
  readonly currentUser$: Observable<User | null> = this.currentUserSubject.asObservable();

  /**
   * Computed signal indicating whether a user is currently authenticated.
   * This is a reactive property that updates automatically when auth state changes.
   * 
   * Uses Angular 19's signal-based reactivity for optimal change detection.
   * 
   * MIGRATION NOTE: Replaces HttpContext.Current.Request.IsAuthenticated checks
   * from DNN codebase.
   * 
   * @public
   * @readonly
   * @type {Signal<boolean>}
   * 
   * @example
   * ```typescript
   * // In template with signal syntax
   * @if (authService.isAuthenticated()) {
   *   <app-user-menu />
   * }
   * 
   * // In component
   * if (this.authService.isAuthenticated()) {
   *   this.loadUserData();
   * }
   * ```
   */
  readonly isAuthenticated: Signal<boolean> = computed(() => {
    const user = this.currentUserSignal();
    return user !== null && this.getAccessToken() !== null;
  });

  /**
   * Constructor initializes authentication state from stored tokens.
   * 
   * On service creation:
   * 1. Loads user from localStorage if available
   * 2. Validates that stored tokens exist
   * 3. Clears invalid state if tokens are missing
   * 
   * MIGRATION NOTE: This initialization replaces DNN's session state restoration
   * from FormsAuthentication cookies.
   */
  constructor() {
    // Synchronize initial state between BehaviorSubject and signal
    const storedUser = this.loadUserFromStorage();
    const token = this.getAccessToken();

    if (!token || !storedUser) {
      // Clear any partial state
      this.clearTokens();
    } else {
      // Ensure both state holders are synchronized
      this.currentUserSubject.next(storedUser);
      this.currentUserSignal.set(storedUser);
    }
  }

  /**
   * Authenticates a user with the provided credentials.
   * 
   * Makes a POST request to /api/auth/login with username and password.
   * On success, stores JWT tokens and updates authentication state.
   * 
   * MIGRATION NOTE: Replaces DNN's UserController.UserLogin method (lines 991-1008)
   * which called ValidateUser and FormsAuthentication.SetAuthCookie.
   * 
   * @param credentials - Login credentials containing username, password, and optional rememberMe flag
   * @returns Observable<AuthResponse> containing access token, refresh token, and user info
   * 
   * @example
   * ```typescript
   * this.authService.login({
   *   username: 'admin',
   *   password: 'password123',
   *   rememberMe: true
   * }).subscribe({
   *   next: (response) => {
   *     console.log('Welcome,', response.user.displayName);
   *     this.router.navigate(['/dashboard']);
   *   },
   *   error: (error) => {
   *     this.errorMessage = 'Invalid credentials';
   *   }
   * });
   * ```
   * 
   * @throws HttpErrorResponse on authentication failure (401, 403, etc.)
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${AUTH_API_URL}/login`, credentials).pipe(
      tap((response: AuthResponse) => {
        // Store tokens and user in localStorage
        this.storeTokens(response);
        
        // Update reactive state holders
        this.currentUserSubject.next(response.user);
        this.currentUserSignal.set(response.user);
      }),
      catchError((error: HttpErrorResponse) => {
        // Log error for debugging but don't expose sensitive details
        console.error('Authentication failed:', error.status, error.statusText);
        
        // Clear any partial state
        this.clearTokens();
        this.currentUserSubject.next(null);
        this.currentUserSignal.set(null);
        
        return throwError(() => error);
      })
    );
  }

  /**
   * Logs out the current user and clears authentication state.
   * 
   * Performs the following actions:
   * 1. Calls POST /api/auth/logout to invalidate server-side token (fire-and-forget)
   * 2. Clears all tokens from localStorage
   * 3. Resets authentication state to null
   * 4. Navigates to the login page
   * 
   * MIGRATION NOTE: Replaces DNN's PortalSecurity.SignOut method (lines 77-95)
   * which cleared FormsAuthentication cookies, language cookies, authentication
   * type cookies, and portal-related cookies.
   * 
   * @example
   * ```typescript
   * // In a logout button handler
   * onLogout(): void {
   *   this.authService.logout();
   *   // User will be redirected to /auth/login
   * }
   * ```
   */
  logout(): void {
    // Attempt to invalidate token on server (fire-and-forget pattern)
    // We don't wait for this to complete as local state should be cleared regardless
    this.http.post(`${AUTH_API_URL}/logout`, {}).pipe(
      catchError(() => {
        // Silently ignore server errors during logout
        // Token invalidation failure shouldn't block local logout
        return of(null);
      })
    ).subscribe();

    // Clear all stored tokens and user data
    this.clearTokens();

    // Reset authentication state
    this.currentUserSubject.next(null);
    this.currentUserSignal.set(null);

    // Navigate to login page
    // MIGRATION NOTE: Replaces DNN's Response.Redirect behavior after SignOut
    this.router.navigate(['/auth/login']);
  }

  /**
   * Refreshes the access token using the stored refresh token.
   * 
   * Makes a POST request to /api/auth/refresh with the refresh token.
   * On success, updates stored tokens and user state.
   * On failure, clears authentication state (user must re-login).
   * 
   * This method is typically called by auth.interceptor.ts when a 401
   * response is received and a refresh token is available.
   * 
   * @returns Observable<AuthResponse> with new tokens and updated user info
   * 
   * @example
   * ```typescript
   * // In an interceptor
   * return this.authService.refreshToken().pipe(
   *   switchMap((response) => {
   *     // Retry original request with new token
   *     return next.handle(this.addToken(request, response.accessToken));
   *   }),
   *   catchError((error) => {
   *     // Refresh failed, redirect to login
   *     this.authService.logout();
   *     return throwError(() => error);
   *   })
   * );
   * ```
   * 
   * @throws Error if no refresh token is available
   * @throws HttpErrorResponse if refresh request fails
   */
  refreshToken(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    
    if (!refreshToken) {
      // No refresh token available - cannot refresh
      this.clearTokens();
      this.currentUserSubject.next(null);
      this.currentUserSignal.set(null);
      return throwError(() => new Error('No refresh token available'));
    }

    const request: RefreshRequest = { refreshToken };
    
    return this.http.post<AuthResponse>(`${AUTH_API_URL}/refresh`, request).pipe(
      tap((response: AuthResponse) => {
        // Update stored tokens
        this.storeTokens(response);
        
        // Update reactive state with potentially updated user info
        this.currentUserSubject.next(response.user);
        this.currentUserSignal.set(response.user);
      }),
      catchError((error: HttpErrorResponse) => {
        console.error('Token refresh failed:', error.status, error.statusText);
        
        // Refresh failed - clear all auth state
        // User will need to re-authenticate
        this.clearTokens();
        this.currentUserSubject.next(null);
        this.currentUserSignal.set(null);
        
        return throwError(() => error);
      })
    );
  }

  /**
   * Fetches the current user information from the server.
   * 
   * Makes a GET request to /api/auth/me to retrieve the authenticated
   * user's current information. Updates local state with server response.
   * 
   * Useful for refreshing user data after profile updates or to verify
   * the current session is still valid.
   * 
   * @returns Observable<User> with current user information
   * 
   * @example
   * ```typescript
   * this.authService.fetchCurrentUser().subscribe({
   *   next: (user) => {
   *     console.log('User roles:', user.roles);
   *   },
   *   error: (error) => {
   *     // Session may have expired
   *     this.authService.logout();
   *   }
   * });
   * ```
   */
  fetchCurrentUser(): Observable<User> {
    return this.http.get<User>(`${AUTH_API_URL}/me`).pipe(
      tap((user: User) => {
        // Update local state with fresh user data
        localStorage.setItem(USER_KEY, JSON.stringify(user));
        this.currentUserSubject.next(user);
        this.currentUserSignal.set(user);
      }),
      catchError((error: HttpErrorResponse) => {
        console.error('Failed to fetch current user:', error.status);
        return throwError(() => error);
      })
    );
  }

  /**
   * Gets the currently authenticated user from local state.
   * 
   * Returns the cached user object without making an API call.
   * For fresh data, use fetchCurrentUser() instead.
   * 
   * @returns The current User object or null if not authenticated
   * 
   * @example
   * ```typescript
   * const user = this.authService.getCurrentUser();
   * if (user) {
   *   console.log(`Hello, ${user.displayName}!`);
   *   console.log(`Portal ID: ${user.portalId}`);
   *   console.log(`Is Super User: ${user.isSuperUser}`);
   * }
   * ```
   */
  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  /**
   * Gets the stored JWT access token.
   * 
   * Retrieves the access token from localStorage. This token is used
   * in the Authorization header for API requests.
   * 
   * @returns The access token string or null if not stored
   * 
   * @example
   * ```typescript
   * // In an interceptor
   * const token = this.authService.getAccessToken();
   * if (token) {
   *   request = request.clone({
   *     setHeaders: { Authorization: `Bearer ${token}` }
   *   });
   * }
   * ```
   */
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  /**
   * Gets the stored refresh token.
   * 
   * Retrieves the refresh token from localStorage. This token is used
   * to obtain new access tokens without re-authentication.
   * 
   * @returns The refresh token string or null if not stored
   * 
   * @example
   * ```typescript
   * const refreshToken = this.authService.getRefreshToken();
   * if (refreshToken) {
   *   // Can attempt token refresh
   *   this.authService.refreshToken().subscribe();
   * } else {
   *   // Must re-authenticate
   *   this.router.navigate(['/auth/login']);
   * }
   * ```
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  /**
   * Checks if the user is currently authenticated.
   * 
   * This is a synchronous method that checks for the presence of an
   * access token in localStorage. For reactive state, use the
   * isAuthenticated signal instead.
   * 
   * MIGRATION NOTE: Replaces HttpContext.Current.Request.IsAuthenticated
   * property checks in DNN codebase.
   * 
   * @returns true if user has a stored access token, false otherwise
   * 
   * @example
   * ```typescript
   * if (this.authService.checkAuthenticated()) {
   *   // Proceed with authenticated operation
   * } else {
   *   this.router.navigate(['/auth/login']);
   * }
   * ```
   */
  checkAuthenticated(): boolean {
    return this.getAccessToken() !== null;
  }

  /**
   * Stores authentication tokens and user info in localStorage.
   * 
   * @param response - AuthResponse containing tokens and user
   * @private
   */
  private storeTokens(response: AuthResponse): void {
    if (!response.accessToken || !response.refreshToken) {
      console.error('Invalid auth response: missing tokens');
      return;
    }

    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    
    if (response.user) {
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    }
  }

  /**
   * Clears all authentication tokens and user info from localStorage.
   * 
   * MIGRATION NOTE: Replaces DNN's cookie clearing logic in PortalSecurity.SignOut
   * which expired multiple cookies (portalaliasid, portalroles, language, authentication).
   * 
   * @private
   */
  private clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  /**
   * Loads cached user from localStorage.
   * 
   * Called during service initialization to restore authentication state
   * after page refresh.
   * 
   * @returns Stored User object or null if not available or invalid
   * @private
   */
  private loadUserFromStorage(): User | null {
    try {
      const userJson = localStorage.getItem(USER_KEY);
      if (!userJson) {
        return null;
      }

      const user = JSON.parse(userJson) as User;
      
      // Basic validation to ensure parsed object has expected shape
      if (user && typeof user.userId === 'number' && typeof user.username === 'string') {
        return user;
      }
      
      // Invalid user object - clear it
      localStorage.removeItem(USER_KEY);
      return null;
    } catch (error) {
      // JSON parse failed - clear corrupted data
      console.error('Failed to parse stored user:', error);
      localStorage.removeItem(USER_KEY);
      return null;
    }
  }

  /**
   * Decodes a JWT token to extract its payload.
   * 
   * Parses the JWT without validation (validation is done server-side).
   * Useful for reading token claims like expiration time.
   * 
   * @param token - JWT token string to decode
   * @returns Decoded payload object or null if decoding fails
   * @private
   * 
   * @example
   * ```typescript
   * const payload = this.decodeToken(accessToken);
   * if (payload && payload.exp) {
   *   const expiresAt = new Date(payload.exp * 1000);
   *   console.log('Token expires at:', expiresAt);
   * }
   * ```
   */
  private decodeToken(token: string): Record<string, unknown> | null {
    try {
      if (!token || token.split('.').length !== 3) {
        return null;
      }

      // JWT structure: header.payload.signature
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      
      // Decode base64
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );

      return JSON.parse(jsonPayload);
    } catch (error) {
      console.error('Failed to decode token:', error);
      return null;
    }
  }

  /**
   * Checks if the access token is expired or about to expire.
   * 
   * @param bufferSeconds - Number of seconds before actual expiry to consider as expired (default: 60)
   * @returns true if token is expired or will expire within buffer period
   */
  isTokenExpired(bufferSeconds: number = 60): boolean {
    const token = this.getAccessToken();
    if (!token) {
      return true;
    }

    const payload = this.decodeToken(token);
    if (!payload || typeof payload['exp'] !== 'number') {
      return true;
    }

    const expirationTime = (payload['exp'] as number) * 1000; // Convert to milliseconds
    const currentTime = Date.now();
    const bufferTime = bufferSeconds * 1000;

    return currentTime >= (expirationTime - bufferTime);
  }
}
