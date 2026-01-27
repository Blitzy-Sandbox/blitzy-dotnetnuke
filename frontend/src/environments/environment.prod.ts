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
 * Environment configuration interface for type safety
 */
export interface Environment {
  /** Flag indicating production mode - enables Angular production optimizations */
  production: boolean;
  
  /** Base URL for API requests - relative path for same-origin or full URL for CORS */
  apiBaseUrl: string;
  
  /** Application display name */
  appName: string;
  
  /** Application version for build tracking and cache busting */
  version: string;
  
  /** Feature flags for controlling application behavior */
  features: {
    /** Enable console logging in production (should be false for prod) */
    enableLogging: boolean;
    /** Enable Angular DevTools in production (should be false for prod) */
    enableDevTools: boolean;
    /** Show missing localization keys - maps to legacy ShowMissingKeys setting */
    showMissingKeys: boolean;
  };
  
  /** Authentication configuration */
  auth: {
    /** JWT token expiration time in minutes - matches backend JWT settings per Section 0.5.4 */
    tokenExpirationMinutes: number;
  };
  
  /** API endpoint path prefixes per Section 0.3.4 API design */
  endpoints: {
    /** Portal management API endpoints */
    portals: string;
    /** Module management API endpoints */
    modules: string;
    /** User management API endpoints */
    users: string;
    /** Role management API endpoints */
    roles: string;
    /** Authentication API endpoints */
    auth: string;
  };
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
    showMissingKeys: false
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
    tokenExpirationMinutes: 60
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
    auth: '/auth'
  }
};
