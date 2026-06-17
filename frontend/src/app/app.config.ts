import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

/**
 * appConfig — the application-level dependency-injection composition root for the
 * DNN Migration Angular 19 standalone SPA.
 *
 * This is the SINGLE place where app-wide framework providers are wired. The
 * sibling bootstrap entry point `frontend/src/main.ts` imports this object
 * (`import { appConfig } from './app/app.config'`) and passes it to
 * `bootstrapApplication(AppComponent, appConfig)`. The export name `appConfig`
 * and its `ApplicationConfig` type are therefore a HARD CONTRACT with `main.ts`
 * and MUST NOT be renamed (AAP §0.3.1 "Root Files", §0.3.4 UI design).
 *
 * Standalone-application model (Angular 19):
 * This file is the standalone-bootstrap replacement for the legacy root
 * `@NgModule({ providers: [...] })`. Every provider here is a FUNCTIONAL
 * provider (`provideXxx(...)`); the project compiles under `strictStandalone`
 * (see `frontend/tsconfig.json`), so the legacy NgModule equivalents
 * (`RouterModule.forRoot`, `HttpClientModule`, `BrowserAnimationsModule`) are
 * intentionally NOT used and must never be reintroduced. There is deliberately
 * no `provideClientHydration`: server-side rendering / hydration is out of scope
 * (AAP §0.2.2 excludes SSR — all rendering is client-side).
 *
 * Provider responsibilities (registration order is intentional but not
 * semantically significant — Angular resolves these independently):
 *
 *   1. `provideZoneChangeDetection({ eventCoalescing: true })`
 *      Configures Zone.js-based change detection (the configured polyfill is
 *      `zone.js`, declared in `frontend/package.json` and wired in
 *      `angular.json`; this app is NOT zoneless). `eventCoalescing: true` batches
 *      multiple DOM events that fire in the same task into a single change-
 *      detection pass, reducing redundant work and complementing the
 *      `ChangeDetectionStrategy.OnPush` standard used by feature/layout
 *      components (AAP §0.3.4 performance: OnPush).
 *
 *   2. `provideRouter(routes)`
 *      Registers the Angular Router with the top-level route table exported by
 *      the sibling `./app.routes`. That table lazy-loads every feature area via
 *      `loadChildren` (auth, portals, modules, users, roles), keeping the initial
 *      JavaScript bundle minimal (AAP §0.3.4 lazy loading). The minimal
 *      `provideRouter(routes)` form is used deliberately; optional router
 *      features (e.g. `withComponentInputBinding()`) are not required by any
 *      in-scope feature and are therefore omitted to avoid dead configuration.
 *
 *   3. `provideHttpClient(withInterceptors([authInterceptor]))`
 *      Registers `HttpClient` and installs the FUNCTIONAL JWT auth interceptor
 *      from `./core/auth/auth.interceptor`. This single registration is the
 *      lynchpin of the application's HTTP security model: it is what lets
 *      `core/services/api.service.ts` and every feature data service inject
 *      `HttpClient`, and it transparently attaches the `Authorization: Bearer
 *      <accessToken>` header to outgoing API calls and performs the single
 *      `401 → POST /api/auth/refresh → retry` recovery. Feature services never
 *      wire authentication themselves — it is centralized here. The functional
 *      `withInterceptors([...])` API is used (NOT the legacy `HTTP_INTERCEPTORS`
 *      DI-token + class style), matching the `HttpInterceptorFn` export contract
 *      of `auth.interceptor.ts`.
 *
 *   4. `provideAnimations()`
 *      Enables the Angular animations system application-wide so animated
 *      shared primitives (e.g. the `confirmation-dialog` and other overlays
 *      under `shared/components/`) can use transition/animation triggers. The
 *      `@angular/animations` package is declared in `frontend/package.json`.
 *
 * Service registration policy:
 * Application/feature singletons (`AuthService`, `ApiService`, `PermissionService`,
 * and per-feature data services) are intentionally NOT registered here. They use
 * `@Injectable({ providedIn: 'root' })`, which makes them tree-shakable root
 * singletons — the Angular-recommended approach. This composition root wires only
 * framework-level providers (AAP §0.3.3 Dependency Injection).
 *
 * Configuration policy:
 * No environment-specific values (API base URL, JWT settings) are hardcoded here.
 * Those live in `frontend/src/environments/` and are consumed by the `core/`
 * layer, keeping this file free of deployment-specific configuration
 * (AAP §0.5.3, §0.7.2 non-functional requirements).
 *
 * MIGRATION: This file has no 1:1 legacy equivalent. It replaces the DNN Web
 * Forms server-side composition (`Global.asax` application wiring + `web.config`
 * `<httpModules>`/`<appSettings>` and ASP.NET Forms Authentication) with a
 * client-side, standalone-bootstrap provider graph. The most significant
 * behavioral change it carries is the move from implicit Forms Authentication
 * cookies to an explicit stateless JWT Bearer flow, realized through the
 * `authInterceptor` registered above (sanctioned security upgrade — see
 * MIGRATION_NOTES.md §3.1, deviation D-001). Recorded in the root
 * MIGRATION_NOTES.md.
 *
 * @see frontend/src/main.ts — calls `bootstrapApplication(AppComponent, appConfig)`.
 * @see ./app.routes — exports `routes: Routes`, consumed by `provideRouter`.
 * @see ./core/auth/auth.interceptor — exports `authInterceptor: HttpInterceptorFn`.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    // Zone.js change detection with event coalescing (Angular 19 scaffold default;
    // complements OnPush feature components — not zoneless).
    provideZoneChangeDetection({ eventCoalescing: true }),
    // Router wired to the lazy-loaded top-level route table (./app.routes).
    provideRouter(routes),
    // HttpClient + the functional JWT Bearer interceptor (attaches the Authorization
    // header and performs the 401 -> refresh -> retry recovery for every API call).
    provideHttpClient(withInterceptors([authInterceptor])),
    // Application-wide Angular animations system (used by shared overlays/dialogs).
    provideAnimations(),
  ],
};
