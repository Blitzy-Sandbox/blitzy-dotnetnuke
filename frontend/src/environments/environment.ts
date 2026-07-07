// MIGRATION: Angular environment config replaces the legacy DNN server-side
// configuration concept (Website/development.config). Only the client->API base URL
// and the production flag carry over; connection strings/secrets remain server-side
// (backend appsettings / environment variables).
export const environment = {
  production: false,
  // Absolute URL to the locally running ASP.NET Core API (Kestrel listens on :8080,
  // per docker ASPNETCORE_URLS=http://+:8080). Adjust host/port if the dev API runs elsewhere.
  apiBaseUrl: 'http://localhost:8080/api',
};
