// MIGRATION: Production Angular environment. apiBaseUrl is the RELATIVE path '/api'
// (NOT an absolute host URL) because the SPA is served same-origin behind the nginx
// reverse proxy: docker/nginx.conf routes `location /api/ { proxy_pass http://api:8080/api/; }`.
// A relative base URL keeps the SPA and API same-origin inside the container; an absolute
// localhost URL would break in production. Secrets/connection strings stay server-side.
export const environment = {
  production: true,
  apiBaseUrl: '/api',
};
