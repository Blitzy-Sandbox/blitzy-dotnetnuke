/**
 * Development Environment Configuration for DNN Migration Portal
 * Angular 19 SPA - Development Build Configuration
 *
 * MIGRATION NOTE: This file replaces the legacy development.config web.config variant
 * from the DotNetNuke 4.x application. Settings have been mapped as follows:
 *
 * Legacy Setting                     | Angular Environment Property
 * ----------------------------------|------------------------------------
 * compilation debug="true"          | production: false (enables dev mode)
 * ShowMissingKeys="false"           | features.showMissingKeys: true (inverted for dev)
 * forms timeout="60"                | auth.tokenExpirationMinutes: 60
 * AutoUpgrade="true"                | features.enableDevTools: true
 *
 * This configuration is used during development builds:
 * - ng serve (development server)
 * - ng build (default development build)
 *
 * For production builds, see environment.prod.ts
 *
 * @see https://angular.dev/tools/cli/environments
 */

/**
 * Environment configuration interface for type safety across environment files.
 * Both environment.ts and environment.prod.ts should conform to this structure.
 */
export interface Environment {
  /** Indicates if this is a production build */
  readonly production: boolean;
  /** Base URL for API requests to the ASP.NET Core backend */
  readonly apiBaseUrl: string;
  /** Application display name */
  readonly appName: string;
  /** Application version string */
  readonly version: string;
  /** Feature flags for enabling/disabling functionality */
  readonly features: EnvironmentFeatures;
  /** Authentication configuration */
  readonly auth: EnvironmentAuth;
  /** API endpoint path configurations */
  readonly endpoints: EnvironmentEndpoints;
}

/**
 * Feature flags configuration for development environment.
 * These flags control debugging and development-specific functionality.
 */
export interface EnvironmentFeatures {
  /** Enable detailed logging to console for debugging */
  readonly enableLogging: boolean;
  /** Enable Angular DevTools and performance profiling */
  readonly enableDevTools: boolean;
  /**
   * Show missing localization keys in the UI instead of silent fallback.
   * MIGRATION: Derived from legacy DNN ShowMissingKeys appSetting.
   * Inverted for development (legacy was false, we set true for debugging).
   */
  readonly showMissingKeys: boolean;
  /** Enable mock data when API is unavailable (development only) */
  readonly enableMockData: boolean;
  /** Enable detailed error messages in UI (not for production) */
  readonly showDetailedErrors: boolean;
}

/**
 * Authentication configuration settings.
 * MIGRATION: Derived from legacy Forms authentication settings in web.config.
 */
export interface EnvironmentAuth {
  /**
   * JWT token expiration time in minutes.
   * MIGRATION: Mapped from legacy forms timeout="60" in web.config.
   */
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
 * These are appended to apiBaseUrl to form complete API URLs.
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
 * Development environment configuration.
 *
 * This configuration is automatically used by Angular CLI during:
 * - `ng serve` - Development server
 * - `ng build` - Default development build
 * - `ng test` - Unit test execution
 *
 * The Angular CLI file replacement feature swaps this file with
 * environment.prod.ts during production builds (`ng build --configuration production`).
 */
export const environment: Environment = {
  /**
   * Production flag - set to false for development mode.
   * When false, Angular enables development mode with:
   * - Additional runtime assertions and checks
   * - Detailed error messages
   * - Zone.js change detection profiling
   * - NgDevMode enabled for framework debugging
   */
  production: false,

  /**
   * API base URL for the ASP.NET Core backend.
   * Points to local development server running on default port.
   *
   * Development setup:
   * - Backend: dotnet run (runs on https://localhost:5001 or http://localhost:5000)
   * - Frontend: ng serve (runs on http://localhost:4200)
   *
   * CORS is configured on the backend to accept requests from localhost:4200.
   */
  apiBaseUrl: 'http://localhost:5000/api',

  /**
   * Application display name used in page titles, headers, and metadata.
   */
  appName: 'DNN Migration Portal',

  /**
   * Application version for development builds.
   * Production builds will use the actual version from package.json.
   */
  version: 'dev',

  /**
   * Feature flags for development environment.
   * These enable debugging and development-specific functionality.
   */
  features: {
    /**
     * Enable detailed console logging for debugging.
     * Logs API requests, state changes, and component lifecycle events.
     */
    enableLogging: true,

    /**
     * Enable Angular DevTools integration and performance profiling.
     * Allows inspection of component tree, change detection, and injector hierarchy.
     */
    enableDevTools: true,

    /**
     * Show missing localization keys in the UI.
     * MIGRATION: This is the inverse of legacy DNN ShowMissingKeys="false".
     * In development, we want to see missing keys to catch localization issues early.
     * Format: [MISSING: key.name] displayed in place of missing translations.
     */
    showMissingKeys: true,

    /**
     * Enable mock data fallback when API is unavailable.
     * Useful for frontend development when backend is not running.
     */
    enableMockData: false,

    /**
     * Show detailed error messages and stack traces in the UI.
     * Helps developers quickly identify and debug issues.
     * NEVER enable this in production.
     */
    showDetailedErrors: true,
  },

  /**
   * Authentication configuration for JWT-based auth.
   * MIGRATION: Replaces legacy Forms authentication from web.config.
   */
  auth: {
    /**
     * JWT access token expiration time in minutes.
     * MIGRATION: Mapped from legacy forms timeout="60" in web.config.
     * Shorter duration for development to test token refresh scenarios.
     */
    tokenExpirationMinutes: 60,

    /**
     * Refresh token expiration time in minutes.
     * Allows users to stay logged in longer without re-authenticating.
     */
    refreshTokenExpirationMinutes: 10080, // 7 days

    /**
     * Automatically refresh tokens before they expire.
     * Provides seamless user experience without session interruption.
     */
    autoRefreshToken: true,

    /**
     * Time in minutes before token expiration to trigger auto-refresh.
     * Token will be refreshed when remaining validity falls below this threshold.
     */
    refreshThresholdMinutes: 5,
  },

  /**
   * API endpoint path configurations.
   * These paths are appended to apiBaseUrl to construct full API URLs.
   *
   * Example usage:
   * const portalsUrl = `${environment.apiBaseUrl}${environment.endpoints.portals}`;
   * // Results in: http://localhost:5000/api/portals
   */
  endpoints: {
    /** Portal management API - /api/portals */
    portals: '/portals',

    /** Module management API - /api/modules */
    modules: '/modules',

    /** User management API - /api/users */
    users: '/users',

    /** Role management API - /api/roles */
    roles: '/roles',

    /** Authentication API - /api/auth */
    auth: '/auth',

    /** Tab/page management API - /api/tabs */
    tabs: '/tabs',

    /** Health check endpoint - /api/health */
    health: '/health',
  },
};
