/**
 * Production Environment Configuration
 * 
 * This file contains production-specific environment settings for the DNN Migration Portal
 * Angular 19 SPA. Angular CLI automatically replaces environment.ts with this file during
 * production builds (ng build --configuration production).
 * 
 * MIGRATION NOTE: This replaces the legacy release.config web.config variant with Angular
 * environment configuration pattern. Settings like ShowMissingKeys (false in prod) and
 * debug mode (false) have been mapped to appropriate feature flags.
 * 
 * @see Section 0.3.1 Target Design: environments/ folder with environment.prod.ts
 * @see Section 0.4.2 Frontend Transformation Mapping: Angular environment config
 * @see Section 0.5.4 Configuration File Transformations: Legacy web.config to Angular environment
 * @see Section 0.7.3 Angular 19 Coding Standards: Production build configuration
 */

/**
 * Environment configuration interface for type safety.
 * This interface must match the one in environment.ts for compatibility.
 */
export interface Environment {
  /** Flag indicating production mode - enables Angular production optimizations */
  readonly production: boolean;
  
  /** Base URL for API requests - relative path for same-origin or full URL for CORS */
  readonly apiBaseUrl: string;
  
  /** Application display name */
  readonly appName: string;
  
  /** Application version for build tracking and cache busting */
  readonly version: string;
  
  /** Feature flags for controlling application behavior */
  readonly features: EnvironmentFeatures;
  
  /** Authentication configuration */
  readonly auth: EnvironmentAuth;
  
  /** API endpoint path prefixes per Section 0.3.4 API design */
  readonly endpoints: EnvironmentEndpoints;
}

/**
 * Feature flags configuration for production environment.
 */
export interface EnvironmentFeatures {
  /** Enable console logging in production (should be false for prod) */
  readonly enableLogging: boolean;
  /** Enable Angular DevTools in production (should be false for prod) */
  readonly enableDevTools: boolean;
  /** Show missing localization keys - maps to legacy ShowMissingKeys setting */
  readonly showMissingKeys: boolean;
  /** Enable mock data when API is unavailable */
  readonly enableMockData: boolean;
  /** Enable detailed error messages in UI */
  readonly showDetailedErrors: boolean;
}

/**
 * Authentication configuration settings.
 */
export interface EnvironmentAuth {
  /** JWT token expiration time in minutes - matches backend JWT settings per Section 0.5.4 */
  readonly tokenExpirationMinutes: number;
  /** Refresh token expiration time in minutes */
  readonly refreshTokenExpirationMinutes: number;
  /** Whether to automatically refresh tokens before expiration */
  readonly autoRefreshToken: boolean;
  /** Time in minutes before expiration to trigger auto-refresh */
  readonly refreshThresholdMinutes: number;
}

/**
 * API endpoint path configurations.
 */
export interface EnvironmentEndpoints {
  /** Portal management API endpoints */
  readonly portals: string;
  /** Module management API endpoints */
  readonly modules: string;
  /** User management API endpoints */
  readonly users: string;
  /** Role management API endpoints */
  readonly roles: string;
  /** Authentication API endpoints */
  readonly auth: string;
  /** Tab/page management API endpoints */
  readonly tabs: string;
  /** Health check endpoint */
  readonly health: string;
}

/**
 * Production environment configuration object
 * 
 * This configuration is optimized for production deployment with:
 * - All debug features disabled
 * - Logging disabled to reduce console noise
 * - DevTools disabled for security
 * - ShowMissingKeys disabled (matches legacy release.config ShowMissingKeys="false")
 * 
 * MIGRATION: Replaces legacy release.config settings:
 * - debug="false" → production: true, features.enableLogging: false
 * - ShowMissingKeys="false" → features.showMissingKeys: false
 * - Forms authentication timeout="60" → auth.tokenExpirationMinutes: 60
 */
export const environment: Environment = {
  /**
   * Production mode flag
   * When true, Angular enables production mode which:
   * - Disables assertions and other checks
   * - Enables additional optimizations
   * - Reduces bundle size with tree-shaking
   */
  production: true,

  /**
   * API Base URL for production deployment
   * Using relative path '/api' assumes the Angular app is served from the same origin
   * as the API (typical for BFF pattern with nginx reverse proxy).
   * For separate deployments, use full URL: 'https://api.yourdomain.com'
   */
  apiBaseUrl: '/api',

  /**
   * Application name displayed in the UI
   */
  appName: 'DNN Migration Portal',

  /**
   * Application version for build tracking
   * This should be updated during CI/CD pipeline or manually for releases
   * Used for cache busting and identifying deployed versions
   */
  version: '1.0.0',

  /**
   * Feature flags configuration for production
   * All debug/development features are disabled in production
   */
  features: {
    /**
     * Console logging disabled in production
     * MIGRATION: Maps to legacy compilation debug="false" setting
     * Prevents sensitive information leakage and reduces console noise
     */
    enableLogging: false,

    /**
     * Angular DevTools disabled in production
     * Prevents external inspection of application state
     * Improves security by hiding internal implementation details
     */
    enableDevTools: false,

    /**
     * Missing localization keys display disabled
     * MIGRATION: Maps to legacy ShowMissingKeys="false" from release.config
     * When false, missing translation keys show the key itself without visual indicators
     */
    showMissingKeys: false,

    /**
     * Mock data disabled in production
     * All data must come from the real API
     */
    enableMockData: false,

    /**
     * Detailed error messages disabled in production
     * Generic error messages shown to users for security
     */
    showDetailedErrors: false
  },

  /**
   * Authentication configuration
   */
  auth: {
    /**
     * JWT token expiration time in minutes
     * MIGRATION: Maps to legacy Forms authentication timeout="60" from release.config
     * Matches backend JWT configuration per Section 0.5.4:
     * "Jwt.ExpirationMinutes": 60
     * 
     * This value is used for:
     * - Token refresh scheduling
     * - Session timeout warnings
     * - Automatic logout timing
     */
    tokenExpirationMinutes: 60,

    /**
     * Refresh token expiration time in minutes (7 days)
     */
    refreshTokenExpirationMinutes: 10080,

    /**
     * Automatically refresh tokens before expiration
     */
    autoRefreshToken: true,

    /**
     * Time in minutes before expiration to trigger auto-refresh
     */
    refreshThresholdMinutes: 5
  },

  /**
   * API endpoint path prefixes
   * These are appended to apiBaseUrl to form complete API URLs
   * Matches backend API design per Section 0.3.4
   * 
   * Example usage:
   * Full URL = `${environment.apiBaseUrl}${environment.endpoints.portals}`
   * Result: '/api/portals'
   */
  endpoints: {
    /**
     * Portal management endpoints
     * Maps to PortalsController: GET, POST, PUT, DELETE /api/portals
     */
    portals: '/portals',

    /**
     * Module management endpoints
     * Maps to ModulesController: GET, POST, PUT, DELETE /api/modules
     */
    modules: '/modules',

    /**
     * User management endpoints
     * Maps to UsersController: GET, POST, PUT, DELETE /api/users
     */
    users: '/users',

    /**
     * Role management endpoints
     * Maps to RolesController: GET, POST, PUT, DELETE /api/roles
     */
    roles: '/roles',

    /**
     * Authentication endpoints
     * Maps to AuthController: POST /api/auth/login, /api/auth/refresh, /api/auth/logout
     * GET /api/auth/me
     */
    auth: '/auth',

    /**
     * Tab/page management endpoints
     * Maps to TabsController: GET, POST, PUT, DELETE /api/tabs
     */
    tabs: '/tabs',

    /**
     * Health check endpoint
     * Maps to HealthController: GET /api/health
     */
    health: '/health'
  }
};
