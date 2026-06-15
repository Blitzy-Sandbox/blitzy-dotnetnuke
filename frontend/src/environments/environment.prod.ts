/**
 * PRODUCTION build-time environment configuration for the Angular 19 SPA.
 *
 * At production build time the Angular CLI substitutes this file for its sibling
 * `environment.ts` (via the `fileReplacements` entry in the `production`
 * configuration of `angular.json`), so every consumer that imports
 * `{ environment }` transparently receives these production values.
 *
 * The exported object SHAPE is intentionally identical to `environment.ts`
 * (same keys, same value types) — only the values differ — which is the
 * contract that lets the CLI swap the files transparently.
 *
 * apiUrl is a RELATIVE base (no host, port, or scheme): in production the SPA
 * is served as static files by nginx, which reverse-proxies API requests to the
 * backend on the SAME origin, so the URL resolves against the page origin, needs
 * no CORS, and keeps the image host-agnostic. It is the shared, version-agnostic
 * base that downstream services extend (the resource service adds the version
 * segment and entity path; the auth code adds the unversioned auth path), so no
 * version segment is baked into this value.
 *
 * Contains NO secrets: the client only needs the public API base URL. JWT
 * signing keys and database connection strings live server-side only.
 */
export const environment = {
  production: true,
  apiUrl: '/api'
};
