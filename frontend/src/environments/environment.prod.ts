/**
 * Angular PRODUCTION build-time environment configuration.
 *
 * The Angular CLI substitutes this file in place of `environment.ts` during a
 * production build (via the `fileReplacements` entry in `angular.json`), so all
 * consumers transparently receive these production values.
 *
 * `apiUrl` is intentionally the RELATIVE base `/api`: in production the SPA is
 * served as static files by nginx, which reverse-proxies `/api/*` to the API
 * container on the same origin (no CORS needed, image stays host-agnostic). It
 * stops at the shared `/api` base: resource routes add a version segment per
 * entity while auth routes are unversioned, so each consumer appends its own
 * remaining path.
 *
 * The SHAPE of this object must stay identical to `environment.ts` (same keys
 * and value types: `production: boolean`, `apiUrl: string`) so the file swap is
 * transparent to every consumer. It contains NO secrets - JWT signing keys and
 * database connection strings live server-side only.
 */
export const environment = {
  production: true,
  apiUrl: '/api'
};
