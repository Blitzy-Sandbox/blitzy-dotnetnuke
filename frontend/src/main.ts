// MIGRATION: SPA bootstrap entrypoint for the Angular 19 admin application that replaces the legacy DNN
// Web Forms server-rendered shell (Default.aspx / DotNetNuke.Framework PageBase). There is NO direct
// legacy analog -- ASP.NET 2.0 had no client bootstrap; the browser requested server-rendered .aspx pages.
// This file is the configured browser entrypoint (frontend/angular.json `browser` + tsconfig.app.json
// `files`); it bootstraps the standalone root AppComponent (no NgModule) with the application-wide
// providers from app.config (router + HttpClient + interceptors), per AAP Section 0.3.6.
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

// MIGRATION: standalone bootstrap (Angular 19 default, no AppModule). A bootstrap failure is logged to the
// console sink; there is no Web Forms global error page to fall back to.
bootstrapApplication(AppComponent, appConfig).catch((err: unknown) => console.error(err));
