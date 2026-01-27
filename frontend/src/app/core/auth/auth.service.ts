/**
 * @fileoverview JWT Authentication Service for Angular 19 SPA
 * @description Core authentication service implementing JWT-based authentication,
 * replacing DNN's FormsAuthentication and MembershipProvider patterns.
 * 
 * MIGRATION NOTE: Replaces:
 * - UserController.UserLogin (lines 991-1008)
 * - FormsAuthentication.SetAuthCookie (line 1033)
 * - PortalSecurity.SignOut (lines 77-95)
 * 
 * @module core/auth/auth.service
 */

import { Injectable, inject, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, tap, catchError, throwError } from 'rxjs';

import { User } from '../models/user.model';
import { LoginRequest, AuthResponse, RefreshRequest } from '../models/auth.model';

/** Local storage key for access token */
const ACCESS_TOKEN_KEY = 'dnn_access_token';

/** Local storage key for refresh token */
const REFRESH_TOKEN_KEY = 'dnn_refresh_token';

/** Local storage key for cached user info */
const USER_KEY = 'dnn_user';

/** API base URL for authentication endpoints */
const AUTH_API_URL = '/api/auth';

/**
 * Core authentication service providing JWT-based authentication.
 * 
 * Provides:
 * - login/logout methods calling /api/auth endpoints
 * - Token storage/retrieval using localStorage
 * - Automatic token refresh via /api/auth/refresh
 * - Authentication state management using BehaviorSubject/signal
 * 
 * MIGRATION NOTE: Replaces DNN's session-based MembershipProvider.UserLogin
 * validation (UserController.vb ValidateUser lines 1110-1157) with stateless
 * JWT validation.
 * 
 * @class AuthService
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** BehaviorSubject for reactive current user state */
  private readonly currentUserSubject = new BehaviorSubject<User | null>(this.loadUserFromStorage());

  /** Observable stream of current user */
  readonly currentUser$ = this.currentUserSubject.asObservable();

  /** Signal-based computed property for authentication state */
  readonly isAuthenticated = computed(() => !!this.currentUserSubject.value);

  /**
   * Constructor initializes state from stored tokens on app startup.
   */
  constructor() {
    // Check if stored tokens are still valid on service initialization
    const token = this.getAccessToken();
    if (!token) {
      this.clearTokens();
    }
  }

  /**
   * Authenticates user with credentials.
   * 
   * MIGRATION: Replaces UserController.UserLogin (lines 991-1008)
   * and FormsAuthentication.SetAuthCookie (line 1033).
   * 
   * @param credentials Login credentials (username, password, rememberMe)
   * @returns Observable of AuthResponse with tokens and user info
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${AUTH_API_URL}/login`, credentials).pipe(
      tap(response => {
        this.storeTokens(response);
        this.currentUserSubject.next(response.user);
      }),
      catchError(error => {
        console.error('Login failed:', error);
        return throwError(() => error);
      })
    );
  }

  /**
   * Logs out the current user.
   * 
   * MIGRATION: Replaces PortalSecurity.SignOut (lines 77-95).
   */
  logout(): void {
    // Optionally call logout endpoint to invalidate server-side tokens
    this.http.post(`${AUTH_API_URL}/logout`, {}).subscribe({
      error: () => {
        // Ignore errors - clear local state regardless
      }
    });

    this.clearTokens();
    this.currentUserSubject.next(null);
    this.router.navigate(['/auth/login']);
  }

  /**
   * Refreshes the access token using the refresh token.
   * 
   * @returns Observable of AuthResponse with new tokens
   */
  refreshToken(): Observable<AuthResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token available'));
    }

    const request: RefreshRequest = { refreshToken };
    return this.http.post<AuthResponse>(`${AUTH_API_URL}/refresh`, request).pipe(
      tap(response => {
        this.storeTokens(response);
        this.currentUserSubject.next(response.user);
      }),
      catchError(error => {
        console.error('Token refresh failed:', error);
        this.clearTokens();
        this.currentUserSubject.next(null);
        return throwError(() => error);
      })
    );
  }

  /**
   * Gets the currently authenticated user.
   * 
   * @returns Current User or null if not authenticated
   */
  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  /**
   * Gets the stored access token.
   * 
   * @returns Access token string or null
   */
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  /**
   * Gets the stored refresh token.
   * 
   * @returns Refresh token string or null
   */
  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  /**
   * Checks if user is currently authenticated.
   * 
   * MIGRATION: Replaces HttpContext.Current.Request.IsAuthenticated check.
   * 
   * @returns true if user has valid access token
   */
  checkAuthenticated(): boolean {
    return !!this.getAccessToken();
  }

  /**
   * Stores authentication tokens in localStorage.
   * 
   * @param response AuthResponse containing tokens and user
   */
  private storeTokens(response: AuthResponse): void {
    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }

  /**
   * Clears all stored tokens from localStorage.
   */
  private clearTokens(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  /**
   * Loads user from localStorage on service initialization.
   * 
   * @returns Stored User or null
   */
  private loadUserFromStorage(): User | null {
    const userJson = localStorage.getItem(USER_KEY);
    if (userJson) {
      try {
        return JSON.parse(userJson) as User;
      } catch {
        return null;
      }
    }
    return null;
  }
}
