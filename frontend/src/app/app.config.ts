/**
 * @fileoverview Angular 19 Application Configuration for DNN Migration SPA
 * @description Defines the application configuration for the standalone Angular 19
 * bootstrap process. This file centralizes all core service providers using the
 * provideXxx pattern, replacing the legacy DNN web.config/Global.asax initialization.
 *
 * MIGRATION NOTE: This configuration replaces DNN's web.config and Global.asax
 * Application_Start initialization with Angular's standalone application provider
 * pattern. Key replacements include:
 *
 * - web.config <authentication mode="Forms"> → JWT Bearer authentication via
 *   provideHttpClient with authInterceptor
 * - web.config <connectionStrings> → Backend API endpoints (configured in environment.ts)
 * - Global.asax Application_Start → Provider registration in this file
 * - HttpModule registration → provideHttpClient with functional interceptors
 * - RouteConfig → provideRouter with lazy-loaded routes
 *
 * Angular 19 Standalone Patterns Used:
 * - provideRouter() with withComponentInputBinding() for route parameter binding
 * - provideRouter() with withPreloading() for lazy module preloading
 * - provideHttpClient() with withInterceptors() for functional HTTP interceptors
 * - provideAnimationsAsync() for lazy-loaded animation support
 * - provideZoneChangeDetection() with eventCoalescing for performance
 *
 * @see Section 0.3.1 Target Design: app.config.ts in frontend/src/app
 * @see Section 0.4.2 Frontend Transformation Mapping: Root Configuration
 * @see Section 0.7.3 Angular 19 Coding Standards: Standalone components, no NgModules
 * @see Section 0.1.1 Core Refactoring Objective: Angular 19 SPA with standalone components
 *
 * @module app/app.config
 */

import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withPreloading,
  PreloadAllModules,
} from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

/**
 * Application configuration object for Angular 19 standalone bootstrap.
 *
 * This configuration is passed to bootstrapApplication() in main.ts to initialize
 * the Angular application with all required providers. The providers array
 * centralizes service registration without NgModules, following Angular 19's
 * standalone component architecture.
 *
 * ## Provider Configuration:
 *
 * ### Zone Change Detection
 * `provideZoneChangeDetection({ eventCoalescing: true })`
 * - Enables event coalescing to batch multiple events into single change detection cycles
 * - Improves performance by reducing unnecessary change detection runs
 * - Particularly beneficial for high-frequency events (scroll, mousemove)
 *
 * ### Router
 * `provideRouter(routes, withComponentInputBinding(), withPreloading(PreloadAllModules))`
 * - Configures Angular Router with application routes from app.routes.ts
 * - withComponentInputBinding(): Enables automatic route parameter binding to component inputs
 *   - MIGRATION NOTE: Replaces DNN's query string parameter handling
 *   - e.g., route param :id automatically binds to @Input() id in component
 * - withPreloading(PreloadAllModules): Preloads all lazy-loaded modules after initial load
 *   - Improves navigation performance by downloading modules in background
 *   - MIGRATION NOTE: Replaces DNN's synchronous page loading model
 *
 * ### HTTP Client
 * `provideHttpClient(withInterceptors([authInterceptor]))`
 * - Configures HttpClient for API communication
 * - withInterceptors([authInterceptor]): Registers functional HTTP interceptor
 *   - MIGRATION NOTE: Replaces DNN's FormsAuthentication cookie-based auth
 *   - authInterceptor automatically injects JWT Bearer token into requests
 *   - Handles 401/403 errors with token refresh and logout
 *
 * ### Animations
 * `provideAnimationsAsync()`
 * - Enables Angular animations with async loading
 * - Loads animation code lazily for better initial bundle size
 * - Supports component animations for UI transitions
 *
 * @example
 * ```typescript
 * // In main.ts:
 * import { bootstrapApplication } from '@angular/platform-browser';
 * import { AppComponent } from './app/app.component';
 * import { appConfig } from './app/app.config';
 *
 * bootstrapApplication(AppComponent, appConfig)
 *   .catch(err => console.error(err));
 * ```
 *
 * @example
 * ```typescript
 * // Using route parameter binding (enabled by withComponentInputBinding):
 * @Component({ ... })
 * export class PortalFormComponent {
 *   // Route param :id automatically bound without ActivatedRoute subscription
 *   @Input() id?: string;
 * }
 * ```
 *
 * @constant {ApplicationConfig}
 * @property {Provider[]} providers - Array of application providers
 */
export const appConfig: ApplicationConfig = {
  providers: [
    /**
     * Zone change detection with event coalescing enabled.
     * Batches multiple events triggered in the same event loop into a single
     * change detection cycle, improving performance.
     *
     * MIGRATION NOTE: DNN's WebForms used synchronous postback model.
     * Angular's zone-based change detection with event coalescing provides
     * much more efficient reactive updates.
     */
    provideZoneChangeDetection({ eventCoalescing: true }),

    /**
     * Router configuration with lazy-loaded routes.
     *
     * Features enabled:
     * - withComponentInputBinding(): Binds route params to @Input() properties
     *   automatically, reducing boilerplate for accessing route data
     * - withPreloading(PreloadAllModules): Preloads all lazy modules after
     *   initial bootstrap for faster subsequent navigation
     *
     * MIGRATION NOTE: Replaces DNN's URL-based navigation patterns:
     * - NavigateURL() → router.navigate() or routerLink
     * - EditUrl() → router.navigate() with parameters
     * - Query string params (pid=123) → Route params (/portals/123)
     */
    provideRouter(
      routes,
      withComponentInputBinding(),
      withPreloading(PreloadAllModules)
    ),

    /**
     * HTTP client with functional interceptor chain.
     *
     * The authInterceptor provides:
     * - Automatic JWT Bearer token injection for authenticated requests
     * - 401 Unauthorized handling with token refresh attempts
     * - 403 Forbidden handling with logout and redirect
     * - Public endpoint exclusion (login, refresh endpoints skip token injection)
     *
     * MIGRATION NOTE: Replaces DNN's authentication mechanisms:
     * - FormsAuthentication.SetAuthCookie → JWT token in localStorage
     * - FormsAuthenticationTicket → JWT claims
     * - Cookie-based session → Stateless JWT (BFF pattern)
     */
    provideHttpClient(withInterceptors([authInterceptor])),

    /**
     * Async animation support.
     *
     * Loads Angular animation code asynchronously to reduce initial bundle size.
     * Supports component animations for UI transitions, loading states, and
     * interactive feedback.
     *
     * MIGRATION NOTE: DNN's admin UI used limited JavaScript animations.
     * Angular animations provide:
     * - Declarative animation definitions
     * - CSS-based hardware acceleration
     * - Coordinated multi-element animations
     */
    provideAnimationsAsync(),
  ],
};
