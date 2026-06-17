/**
 * main.ts — Angular 19 standalone bootstrap ENTRY POINT for the DNN Migration SPA.
 *
 * This is THE compilation entry point of the frontend application. It is wired as
 * the build `browser` target in `frontend/angular.json`
 * (`"browser": "src/main.ts"`) and as the sole `files` entry in
 * `frontend/tsconfig.app.json` (`"files": ["src/main.ts"]`). Without this file the
 * production build (Gate 3 — `ng build --configuration production`) cannot resolve
 * an entry point and fails. Mandated by AAP §0.3.1 (frontend tree lists
 * `src/main.ts`) and §0.3.4 (standalone-component SPA).
 *
 * Responsibility — bootstrap ONLY:
 * It performs the single Angular 19 NgModule-less bootstrap via
 * `bootstrapApplication(AppComponent, appConfig)`. It registers NO providers and
 * contains NO application logic — provider wiring is the exclusive concern of the
 * composition root `./app/app.config` (`appConfig`), and the visual root is the
 * standalone `./app/app.component` (`AppComponent`, selector `app-root`). This
 * separation keeps the entry point minimal and side-effect-only.
 *
 * Standalone model (Angular 19):
 * `bootstrapApplication` is the standalone replacement for the legacy
 * `platformBrowserDynamic().bootstrapModule(AppModule)` flow. There are NO
 * NgModules in this project; `strictStandalone: true` in `frontend/tsconfig.json`
 * enforces that at compile time, so an NgModule bootstrap path would not compile.
 * `enableProdMode()` is intentionally NOT called — the `@angular-devkit/build-angular:application`
 * builder applies production optimizations, making it unnecessary in v19. The
 * `zone.js` polyfill is NOT imported here either; it is supplied by
 * `angular.json` (`"polyfills": ["zone.js"]`).
 *
 * Integration contract (export names are a HARD contract — see AAP §0.5.2):
 * - `AppComponent` is imported from `./app/app.component` (relative path; no
 *   tsconfig path aliases are configured).
 * - `appConfig` is imported from `./app/app.config` (relative path).
 * Both names mirror the standard Angular CLI scaffold and MUST match the exports
 * authored by the `app/` files.
 *
 * Error surfacing:
 * `.catch((err) => console.error(err))` reports any asynchronous bootstrap
 * failure (e.g. a provider initialization error) to the browser console rather
 * than letting the rejected promise go unhandled.
 *
 * MIGRATION: Brand-new application entry point with no 1:1 legacy equivalent. It
 * replaces the DNN Web Forms server-side application start (`Global.asax` +
 * `web.config`), which is explicitly out of scope (AAP §0.2.2); all rendering is
 * now client-side. Recorded in the root MIGRATION_NOTES.md.
 *
 * @see ./app/app.component — exports the standalone root `AppComponent`.
 * @see ./app/app.config — exports `appConfig: ApplicationConfig` (DI composition root).
 * @see frontend/src/index.html — provides the `<app-root>` host element.
 */
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
