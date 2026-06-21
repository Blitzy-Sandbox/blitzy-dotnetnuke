import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/auth/auth.interceptor';

/**
 * Application-level dependency-injection composition root for the DNN Migration
 * Angular 19 SPA.
 *
 * This is the single place where app-wide *framework* providers are wired. The
 * sibling `main.ts` bootstraps the application with it:
 *
 * ```ts
 * import { bootstrapApplication } from '@angular/platform-browser';
 * import { AppComponent } from './app/app.component';
 * import { appConfig } from './app/app.config';
 *
 * bootstrapApplication(AppComponent, appConfig);
 * ```
 *
 * The export name `appConfig` and its `ApplicationConfig` type are therefore a
 * HARD contract with `main.ts` and must not change.
 *
 * Standalone-only / no NgModules: every provider below is registered through a
 * functional `provide*` API that returns `EnvironmentProviders` (or a `Provider`
 * array). The project compiles with the Angular `strictStandalone` option, which
 * forbids the legacy NgModule-based equivalents — `RouterModule.forRoot(routes)`,
 * `HttpClientModule`, and `BrowserAnimationsModule` are deliberately NOT used.
 *
 * Scope of this file: ONLY framework providers are wired here. Application
 * singletons — `AuthService`, `ApiService`, and every feature data service —
 * declare `@Injectable({ providedIn: 'root' })` and are therefore tree-shakable
 * root singletons that need no registration in this list.
 *
 * MIGRATION: Replaces the legacy DNN application-composition surface — the
 * `Global.asax` application lifecycle plus the `web.config` `<system.web>` /
 * `<httpModules>` / Forms-Authentication wiring — with Angular's standalone
 * bootstrap. The server-side request pipeline now lives in the ASP.NET Core
 * `Program.cs` composition root; this file is its client-side counterpart.
 * Recorded in the root `MIGRATION_NOTES.md`.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    // Zone-based change detection with event coalescing. This is the Angular 19
    // CLI scaffold default and matches the configured `zone.js` polyfill in
    // `angular.json`; coalescing batches multiple events fired in the same tick
    // into a single change-detection run for better performance. The application
    // is intentionally NOT zoneless — `zone.js` remains the polyfill of record.
    provideZoneChangeDetection({ eventCoalescing: true }),

    // Wires the top-level (1st-tier) routing table from the sibling
    // `app.routes.ts`. That table attaches every feature area with
    // `loadChildren`, so feature route trees and their standalone components are
    // code-split and loaded on demand (lazy loading per AAP §0.3.4).
    provideRouter(routes),

    // Registers `HttpClient` app-wide with the FUNCTIONAL auth interceptor.
    // `authInterceptor` (an `HttpInterceptorFn`) attaches the JWT
    // `Authorization: Bearer` header to outgoing API requests and transparently
    // recovers from a `401 Unauthorized` by rotating tokens via
    // `POST /api/auth/refresh` and retrying the original request exactly once.
    // This single registration is what lets `core/services/api.service.ts` and
    // every feature data service inject `HttpClient` with automatic auth — no
    // service wires authentication itself. Functional interceptors are required
    // here (not the legacy `HTTP_INTERCEPTORS` DI-token + class style).
    provideHttpClient(withInterceptors([authInterceptor])),

    // MIGRATION (Finding CP-FINAL-2): re-mint the in-memory JWT access token on app
    // start from the `HttpOnly` refresh cookie. Because the access token is no longer
    // persisted to localStorage (secure-storage hardening), a full page reload would
    // otherwise drop the session; this initializer runs `AuthService.initializeSession()`
    // BEFORE the first route activates so the auth guard sees the restored session. The
    // initializer always completes (errors swallowed) so a missing/expired cookie never
    // blocks bootstrap. `provideAppInitializer` runs its callback in an injection context,
    // so `inject(AuthService)` is valid here (Angular 19 replacement for APP_INITIALIZER).
    provideAppInitializer(() => inject(AuthService).initializeSession()),

    // Enables the Angular animations system application-wide (e.g. for the
    // `confirmation-dialog` and other transition-driven shared components).
    provideAnimations(),
  ],
};
