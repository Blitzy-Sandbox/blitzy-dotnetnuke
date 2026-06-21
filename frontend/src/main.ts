import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

/**
 * Application entry point for the DNN Migration Angular 19 SPA.
 *
 * THE frontend compilation entry point: the already-created build configuration
 * targets this file directly — `angular.json` declares `browser: "src/main.ts"`
 * and `tsconfig.app.json` declares `files: ["src/main.ts"]`. It is the bridge
 * between that build configuration and the `app/` subtree: the bundler starts
 * here, follows the two imports below, and pulls the entire standalone component
 * graph and its composition root into the build.
 *
 * It performs the Angular 19 standalone bootstrap, wiring the root `AppComponent`
 * (selector `app-root`, rendered into `<app-root>` in `index.html`) together with
 * the `appConfig` composition root (router, HttpClient + auth interceptor, change
 * detection, animations). Both are imported by name via RELATIVE paths — no
 * tsconfig path aliases are configured (AAP §0.5.2) — so these two export names
 * are a hard integration contract with `./app/app.component` and
 * `./app/app.config`. The trailing `.catch` surfaces any error thrown during the
 * asynchronous bootstrap to the console, so a failed start is observable rather
 * than silently swallowed.
 *
 * Intentionally minimal — bootstrap only: app-wide framework wiring lives in
 * `appConfig` (the composition root), and the browser polyfill is supplied by
 * `angular.json` (`polyfills: ["zone.js"]`), keeping this file single-purpose.
 *
 * MIGRATION: Replaces the legacy DotNetNuke server-rendered client startup surface
 * (Web Forms page lifecycle and `Default.aspx` skin host) with a client-side
 * standalone SPA bootstrap. Recorded in the root `MIGRATION_NOTES.md`.
 */
bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
