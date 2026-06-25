// MIGRATION: Dev points directly at Kestrel; backend CORS must allow http://localhost:4200 (the ng serve origin).
export const environment = {
  production: false,
  apiUrl: 'http://localhost:8080/api/v1',
} as const;
