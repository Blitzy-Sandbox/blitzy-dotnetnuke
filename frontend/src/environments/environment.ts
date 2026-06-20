/**
 * Development (base) build-time environment configuration for the Angular SPA.
 *
 * Consumed via `import { environment } from '.../environments/environment';`.
 * At a production build the Angular CLI replaces this file with
 * `environment.prod.ts` (via a `fileReplacements` entry in angular.json); both
 * files share the identical shape `{ production: boolean; apiUrl: string }`.
 *
 * `apiUrl` is the common API base ending at `/api`. The version segment is
 * never baked in here; callers append it:
 *   - versioned resources -> `${environment.apiUrl}` + `/v1/<entity>`
 *   - unversioned auth     -> `${environment.apiUrl}` + `/auth/<action>`
 *   - the server-root `/health` endpoint is NOT derived from `apiUrl`.
 *
 * Development uses the absolute backend origin (http://localhost:5000) because
 * there is intentionally no Angular dev proxy; the backend CORS policy allows
 * the http://localhost:4200 SPA origin. This file ships to the client at build
 * time and must contain NO secrets (JWT keys, connection strings, etc.).
 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api'
};
