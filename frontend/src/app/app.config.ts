// MIGRATION: application-wide provider configuration for the standalone Angular 19 SPA. Replaces the
// legacy DNN server-side composition (Website/web.config <httpModules>/<httpHandlers> + Global.asax
// Application_Start provider wiring) with explicit, tree-shakable standalone providers (AAP Section 0.3.6).
// There is no NgModule; providers are assembled here and consumed by bootstrapApplication in main.ts.
import { type ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { tokenInterceptor } from './core/interceptors/token.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

/**
 * Root application configuration consumed by `bootstrapApplication` in `main.ts`.
 *
 * MIGRATION: the provider set mirrors the responsibilities the legacy DNN runtime split across
 * web.config and Global.asax -- routing (replaces the Web Forms page/URL pipeline), the HTTP client
 * (replaces server-side data providers for the SPA's API calls), and the auth/error HTTP interceptors
 * (replace the Forms-authentication cookie module + Web Forms global error handling).
 */
export const appConfig: ApplicationConfig = {
  providers: [
    // MIGRATION: zone change detection with event coalescing (Angular 19 default-recommended). Components
    // additionally use OnPush + signals (AAP Section 0.7.3), so coalescing reduces redundant CD passes.
    provideZoneChangeDetection({ eventCoalescing: true }),

    // MIGRATION: lazy, feature-organized routing (AAP Section 0.3.6). withComponentInputBinding() binds
    // route params to component `input()`s -- REQUIRED by the edit/detail forms (portal-form, user-form,
    // role-form, profile, import-export, role-assignment) which read the `:id` route param via input().
    provideRouter(routes, withComponentInputBinding()),

    // MIGRATION: the SPA's single HttpClient. Functional interceptors run in array order on the OUTBOUND
    // request: tokenInterceptor attaches the `Authorization: Bearer <accessToken>` header FIRST, then
    // errorInterceptor wraps the response to parse RFC 7807 ProblemDetails and drive the 401 -> refresh ->
    // retry-once -> logout flow. This order is REQUIRED -- error.interceptor re-clones the retried request
    // with the rotated token itself rather than re-entering tokenInterceptor (see error.interceptor.ts).
    provideHttpClient(withInterceptors([tokenInterceptor, errorInterceptor])),
  ],
};
