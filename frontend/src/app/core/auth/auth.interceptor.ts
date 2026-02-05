/**
 * @fileoverview JWT Authentication HTTP Interceptor for Angular 19 SPA
 * @description Functional HTTP interceptor implementing automatic JWT token injection
 * into outgoing HTTP requests and handling 401/403 authentication errors.
 *
 * This interceptor provides:
 * - Automatic Bearer token injection in Authorization header
 * - 401 Unauthorized error handling with token refresh attempts
 * - 403 Forbidden error handling with logout and redirect
 * - Public endpoint exclusion (login, refresh endpoints)
 *
 * MIGRATION NOTE: Replaces DNN's cookie-based FormsAuthentication with stateless
 * JWT Bearer token authentication per BFF pattern:
 * - FormsAuthentication.SetAuthCookie (UserController.vb lines 1032-1054) replaced
 *   with Authorization: Bearer header injection
 * - Cookie-based session handling in PortalSecurity.SignOut (lines 77-95) replaced
 *   with localStorage token management and logout() call
 * - Cookie validation replaced with JWT token presence and validity checks
 *
 * @see Section 0.3.2 Web Search Research: BFF pattern with stateless JWT
 * @see Section 0.7.3 Angular 19 Coding Standards: Use inject() function, functional patterns
 * @see Section 0.7.7 Security Rules: JWT Bearer tokens, short-lived access tokens
 *
 * @module core/auth/auth.interceptor
 */

import { inject } from '@angular/core';
import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';
import { AuthResponse } from '../models/auth.model';

/**
 * List of URL patterns that should not have authentication tokens injected.
 * These are public endpoints that do not require authentication.
 *
 * @constant {string[]}
 */
const PUBLIC_ENDPOINTS: string[] = [
  '/api/auth/login',
  '/api/auth/refresh',
  '/api/auth/register',
];

/**
 * Checks if the given URL is a public endpoint that should not have token injection.
 *
 * @param url - The request URL to check
 * @returns true if the URL matches a public endpoint pattern
 *
 * @example
 * ```typescript
 * isPublicEndpoint('/api/auth/login'); // true
 * isPublicEndpoint('/api/users'); // false
 * ```
 */
function isPublicEndpoint(url: string): boolean {
  return PUBLIC_ENDPOINTS.some((endpoint) => url.includes(endpoint));
}

/**
 * Clones the HTTP request and adds the Authorization header with the Bearer token.
 *
 * MIGRATION NOTE: Replaces DNN's cookie-based FormsAuthentication where the
 * browser automatically sent FormsAuth cookies. In JWT-based auth, we must
 * explicitly add the Authorization header to each request.
 *
 * @param request - The original HTTP request to clone
 * @param token - The JWT access token to add
 * @returns A new HttpRequest with the Authorization header set
 *
 * @example
 * ```typescript
 * const authRequest = addTokenToRequest(originalRequest, 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...');
 * // Request now has: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
 * ```
 */
function addTokenToRequest<T>(
  request: HttpRequest<T>,
  token: string
): HttpRequest<T> {
  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });
}

/**
 * Angular 19 functional HTTP interceptor for automatic JWT token injection and
 * authentication error handling.
 *
 * This interceptor implements the HttpInterceptorFn pattern (Angular 19 functional
 * interceptor) and is registered via `provideHttpClient(withInterceptors([authInterceptor]))`.
 *
 * ## Token Injection Flow:
 * 1. Check if request URL is a public endpoint (skip token injection)
 * 2. Get access token from AuthService
 * 3. If token exists, clone request and add Authorization: Bearer header
 * 4. Pass request to next handler in chain
 *
 * ## Error Handling Flow:
 * 1. Catch HTTP errors from the response
 * 2. On 401 Unauthorized:
 *    - Check if refresh token is available
 *    - Attempt token refresh via AuthService.refreshToken()
 *    - On success: retry original request with new token
 *    - On failure: logout and redirect to login
 * 3. On 403 Forbidden: logout and redirect to login
 * 4. All other errors: propagate to caller
 *
 * MIGRATION NOTE: This interceptor replaces DNN's cookie-based authentication
 * infrastructure including:
 * - FormsAuthentication.SetAuthCookie (UserController.vb line 1033)
 * - FormsAuthenticationTicket creation (lines 1044-1050)
 * - HttpCookie handling for auth cookies
 * - PortalSecurity.SignOut cookie clearing (lines 77-95)
 *
 * @param request - The outgoing HTTP request
 * @param next - The next handler in the interceptor chain
 * @returns Observable<HttpEvent<unknown>> - The HTTP response stream
 *
 * @example
 * ```typescript
 * // In app.config.ts
 * import { provideHttpClient, withInterceptors } from '@angular/common/http';
 * import { authInterceptor } from './core/auth/auth.interceptor';
 *
 * export const appConfig: ApplicationConfig = {
 *   providers: [
 *     provideHttpClient(withInterceptors([authInterceptor])),
 *   ]
 * };
 * ```
 *
 * @example
 * ```typescript
 * // Automatic token injection - no manual configuration needed
 * // When AuthService has a valid token:
 * this.http.get('/api/users').subscribe(users => {
 *   // Request automatically includes: Authorization: Bearer <token>
 * });
 * ```
 *
 * @see {@link AuthService} for token management
 * @see {@link https://angular.dev/guide/http/interceptors} Angular HTTP Interceptors
 */
export const authInterceptor: HttpInterceptorFn = (
  request: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  // Inject required services using Angular 19 functional inject() pattern
  const authService = inject(AuthService);
  const router = inject(Router);

  // Skip token injection for public endpoints (login, refresh, register)
  // These endpoints don't require authentication
  if (isPublicEndpoint(request.url)) {
    return next(request);
  }

  // Get the current access token from AuthService
  // MIGRATION NOTE: Replaces reading FormsAuthentication cookie
  const accessToken = authService.getAccessToken();

  // If we have a token, add it to the request
  let authenticatedRequest = request;
  if (accessToken) {
    authenticatedRequest = addTokenToRequest(request, accessToken);
  }

  // Handle the request and catch any errors
  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      return handleHttpError(
        error,
        request,
        next,
        authService,
        router
      );
    })
  );
};

/**
 * Handles HTTP errors from the interceptor chain, with special handling for
 * authentication-related errors (401 and 403).
 *
 * @param error - The HTTP error response
 * @param originalRequest - The original HTTP request (without token)
 * @param next - The next handler in the interceptor chain
 * @param authService - The AuthService instance for token operations
 * @param router - The Router instance for navigation
 * @returns Observable that either retries the request or throws an error
 *
 * @internal
 */
function handleHttpError(
  error: HttpErrorResponse,
  originalRequest: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  // Handle 401 Unauthorized - attempt token refresh
  if (error.status === 401) {
    return handleUnauthorizedError(
      originalRequest,
      next,
      authService,
      router
    );
  }

  // Handle 403 Forbidden - user doesn't have permission, clear auth state
  if (error.status === 403) {
    handleForbiddenError(authService, router);
    return throwError(() => error);
  }

  // For all other errors, propagate to the caller
  return throwError(() => error);
}

/**
 * Handles 401 Unauthorized errors by attempting to refresh the access token.
 *
 * Flow:
 * 1. Check if a refresh token is available
 * 2. If available, call AuthService.refreshToken()
 * 3. On success, retry the original request with the new token
 * 4. On failure, logout and redirect to login
 *
 * MIGRATION NOTE: Replaces DNN's automatic cookie renewal where FormsAuthentication
 * would issue a new ticket if the existing one was close to expiry. JWT requires
 * explicit token refresh.
 *
 * @param originalRequest - The original HTTP request to retry
 * @param next - The next handler in the interceptor chain
 * @param authService - The AuthService instance for token operations
 * @param router - The Router instance for navigation
 * @returns Observable that retries the request with new token or throws error
 *
 * @internal
 */
function handleUnauthorizedError(
  originalRequest: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  // Check if we have a refresh token available
  const refreshToken = authService.getRefreshToken();

  if (!refreshToken) {
    // No refresh token - cannot recover, must re-authenticate
    // MIGRATION NOTE: Replaces redirect to Login.aspx when FormsAuth cookie expired
    authService.logout();
    return throwError(() => new Error('Session expired. Please login again.'));
  }

  // Attempt to refresh the token
  return authService.refreshToken().pipe(
    switchMap((response: AuthResponse) => {
      // Token refresh successful - retry the original request with new token
      const retryRequest = addTokenToRequest(
        originalRequest,
        response.accessToken
      );
      return next(retryRequest);
    }),
    catchError((refreshError: HttpErrorResponse) => {
      // Token refresh failed - clear auth state and redirect to login
      // MIGRATION NOTE: Replaces PortalSecurity.SignOut behavior (lines 77-95)
      // which cleared all auth-related cookies and redirected to home page
      console.error(
        'Token refresh failed:',
        refreshError.status,
        refreshError.statusText
      );
      authService.logout();
      return throwError(
        () => new Error('Session expired. Please login again.')
      );
    })
  );
}

/**
 * Handles 403 Forbidden errors by logging out the user and redirecting to login.
 *
 * A 403 error indicates the user is authenticated but doesn't have permission
 * for the requested resource. In this case, we clear the authentication state
 * as the token may be invalid or permissions have changed.
 *
 * MIGRATION NOTE: In DNN, permission checks happened server-side and would
 * redirect to an "access denied" page. In the modern SPA approach, we clear
 * auth state and redirect to login, allowing the user to re-authenticate
 * with potentially updated permissions.
 *
 * @param authService - The AuthService instance for logout operation
 * @param router - The Router instance for navigation (unused, logout handles redirect)
 *
 * @internal
 */
function handleForbiddenError(
  authService: AuthService,
  router: Router
): void {
  // Log the forbidden access attempt for debugging
  console.warn('Access forbidden (403) - clearing authentication state');

  // Clear auth state and redirect to login
  // Note: authService.logout() handles the redirect to /auth/login
  // MIGRATION NOTE: Replaces DNN's permission denied handling which would
  // show an access denied message or redirect based on portal settings
  authService.logout();
}
