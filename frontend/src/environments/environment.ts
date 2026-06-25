// MIGRATION: Replaces the frontend-relevant endpoint config formerly implied by Website/release.config.
// PROD apiUrl is RELATIVE because nginx serves the SPA and reverse-proxies /api to the API container (same-origin).
export const environment = {
  production: true,
  apiUrl: '/api/v1',
} as const;
