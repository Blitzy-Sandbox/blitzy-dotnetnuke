/**
 * @fileoverview Angular 19 Application Bootstrap Entry Point
 * @description Application entry point that bootstraps the Angular 19 SPA using
 * the standalone component API. This file initializes the application without
 * NgModules, following Angular 19's standalone-first architecture pattern.
 *
 * MIGRATION NOTE: This file replaces the DNN (DotNetNuke) legacy initialization
 * patterns from Global.asax.vb and Default.aspx.vb:
 *
 * **Original DNN Global.asax.vb (lines 66-74) handled:**
 * - Application_Start event for global initialization
 * - Server name configuration (Config.GetSetting("ServerName"))
 * - DNN initialization via Initialize.Init() called in BeginRequest
 * - Scheduler startup via Initialize.RunSchedule() for background jobs
 *
 * **Original DNN Global.asax.vb (lines 102-110) handled:**
 * - Application_End event for graceful shutdown
 * - Scheduler stopping via Initialize.StopScheduler()
 * - Application logging via Initialize.LogEnd()
 *
 * **Original DNN Default.aspx.vb handled:**
 * - Portal context resolution and settings
 * - Skin loading and initialization
 * - Page lifecycle management
 *
 * **Angular 19 Replacement Strategy:**
 * - bootstrapApplication() replaces Application_Start initialization
 * - AppComponent and lazy-loaded routes replace Default.aspx page loading
 * - AuthInterceptor (configured in appConfig) replaces FormsAuthentication
 * - No explicit Application_End needed (browser handles cleanup)
 * - Background tasks handled by backend IHostedService, not frontend
 *
 * @see Section 0.3.1 Target Design: main.ts in frontend/src root
 * @see Section 0.4.2 Frontend Transformation Mapping: Root Configuration - main.ts
 * @see Section 0.7.3 Angular 19 Coding Standards: All components standalone
 *
 * @example
 * ```html
 * <!-- index.html references this file via the build process -->
 * <script src="main.js" type="module"></script>
 * ```
 *
 * @module main
 */

// =============================================================================
// External Imports - Angular Platform
// =============================================================================

/**
 * Import bootstrapApplication from Angular platform-browser.
 *
 * The bootstrapApplication function is the entry point for bootstrapping
 * Angular 19 standalone applications. It replaces the legacy platformBrowserDynamic()
 * and NgModule-based bootstrapModule() approach.
 *
 * Key characteristics:
 * - Accepts a standalone component as the root
 * - Accepts ApplicationConfig for provider registration
 * - Returns a Promise that resolves to ApplicationRef
 * - Enables tree-shaking of unused NgModule infrastructure
 *
 * MIGRATION NOTE: This replaces the DNN pattern of:
 * ```vb
 * ' Global.asax.vb - Application initialization
 * Private Sub Application_Start(ByVal sender As Object, ByVal e As EventArgs)
 *     If Config.GetSetting("ServerName") = "" Then
 *         ServerName = Server.MachineName
 *     Else
 *         ServerName = Config.GetSetting("ServerName")
 *     End If
 * End Sub
 * ```
 *
 * The Angular equivalent initializes the entire component tree and router
 * in a single bootstrapApplication call.
 */
import { bootstrapApplication } from '@angular/platform-browser';

// =============================================================================
// Internal Imports - Application Components and Configuration
// =============================================================================

/**
 * Import the root AppComponent.
 *
 * AppComponent is the root standalone component that defines the application
 * shell layout including:
 * - Header component for branding and user info
 * - Collapsible sidebar for navigation
 * - Main content area with router-outlet
 * - Footer component with copyright info
 *
 * MIGRATION NOTE: AppComponent replaces DNN's Default.aspx master page which:
 * - Loaded skin containers (LoadSkin method in Default.aspx.vb)
 * - Resolved portal settings (PortalSettings in Default.aspx.vb)
 * - Managed page lifecycle events (Page_Init, Page_Load)
 *
 * @see frontend/src/app/app.component.ts
 */
import { AppComponent } from './app/app.component';

/**
 * Import the application configuration.
 *
 * appConfig contains all application-level providers configured using
 * Angular 19's functional provider API:
 * - provideZoneChangeDetection() for optimized change detection
 * - provideRouter() for lazy-loaded routing
 * - provideHttpClient() with auth interceptor for API calls
 * - provideAnimationsAsync() for UI animations
 *
 * MIGRATION NOTE: appConfig replaces DNN's web.config and Global.asax
 * configuration patterns:
 * - <connectionStrings> → API endpoints in environment.ts
 * - <authentication mode="Forms"> → JWT via authInterceptor
 * - HttpModule registration → provideHttpClient()
 * - RouteConfig → provideRouter()
 *
 * @see frontend/src/app/app.config.ts
 */
import { appConfig } from './app/app.config';

// =============================================================================
// Application Bootstrap
// =============================================================================

/**
 * Bootstrap the Angular 19 application.
 *
 * This call initializes the Angular platform and bootstraps the component tree
 * starting from AppComponent. The bootstrapApplication function:
 *
 * 1. Creates the Angular platform (zone.js, DI container)
 * 2. Registers all providers from appConfig
 * 3. Compiles the AppComponent and its dependencies
 * 4. Renders the application to the DOM (targeting <app-root>)
 * 5. Initializes the router and triggers navigation
 *
 * MIGRATION NOTE: This single call replaces the entire DNN initialization
 * sequence from Global.asax.vb:
 *
 * Original DNN flow (Global.asax.vb lines 76-88):
 * ```vb
 * Private Sub Global_BeginRequest(ByVal sender As Object, ByVal e As EventArgs) Handles Me.BeginRequest
 *     Dim app As HttpApplication = CType(sender, HttpApplication)
 *     Dim Request As HttpRequest = app.Request
 *
 *     ' Initialize DNN framework on first request
 *     Initialize.Init(app)
 *
 *     ' Start scheduler for background jobs
 *     Initialize.RunSchedule(Request)
 * End Sub
 * ```
 *
 * Angular 19 advantages:
 * - Single-page application eliminates per-request initialization
 * - Lazy loading defers feature module compilation until needed
 * - Standalone components enable fine-grained tree-shaking
 * - Zone.js handles change detection automatically
 *
 * Error Handling:
 * The .catch() handler captures and logs any bootstrap failures.
 * Common failure scenarios include:
 * - Missing required providers in appConfig
 * - Template compilation errors in root component
 * - Router configuration errors
 * - Dependency injection failures
 *
 * In production, consider implementing more robust error reporting
 * (e.g., error tracking service integration) beyond console.error.
 *
 * @returns {Promise<ApplicationRef>} Promise resolving to the application reference
 * @throws {Error} Bootstrap errors are caught and logged to console
 *
 * @example
 * ```typescript
 * // Successful bootstrap logs nothing
 * // Failed bootstrap logs error details to console:
 * // Error: NullInjectorError: No provider for SomeService!
 * ```
 */
bootstrapApplication(AppComponent, appConfig)
  .catch((error: unknown) => {
    /**
     * Error handler for bootstrap failures.
     *
     * Logs the error to the browser console for debugging.
     * This minimal error handling ensures failures are visible
     * during development and can be captured by browser error
     * monitoring in production.
     *
     * MIGRATION NOTE: This replaces DNN's Global.asax.vb
     * Application_Error handler pattern:
     * ```vb
     * Private Sub Application_Error(ByVal sender As Object, ByVal e As EventArgs)
     *     ' Log error to DNN event log
     *     Initialize.LogError(Server.GetLastError())
     * End Sub
     * ```
     *
     * In Angular, most runtime errors are handled by:
     * - ErrorHandler service (configurable in appConfig)
     * - HTTP interceptor error handling
     * - Component-level try/catch blocks
     *
     * This catch block specifically handles bootstrap-time errors
     * that occur before the application is fully initialized.
     */
    console.error('DNN Migration Angular application failed to bootstrap:', error);
  });
