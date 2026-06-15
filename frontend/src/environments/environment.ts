/**
 * Angular build-time environment configuration - DEVELOPMENT (base).
 *
 * This is the canonical/base environment module imported by the application
 * (e.g. `import { environment } from '../../../environments/environment';`).
 * The Angular CLI production build substitutes this file with the sibling
 * `environment.prod.ts` via a `fileReplacements` entry in `angular.json`,
 * so both files MUST expose an identical object shape.
 *
 * `apiUrl` is the common backend API base and intentionally ends at `/api`
 * (NOT `/api/v1`): resource endpoints are URL-path versioned and composed as
 * `${environment.apiUrl}/v1/<entity>`, while auth endpoints are unversioned
 * and composed as `${environment.apiUrl}/auth/<action>`. In development the
 * SPA (origin http://localhost:4200) calls the absolute backend base
 * http://localhost:5000/api cross-origin, which the backend CORS policy
 * permits; there is intentionally no Angular dev proxy.
 *
 * This client-side config contains NO secrets - signing keys, connection
 * strings, and similar values live server-side only.
 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api'
};
