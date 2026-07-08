import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

// MIGRATION: Replaces the Global.asax / HttpModules service wiring with the standalone
// Angular provider graph. Routing is supplied by app.routes.ts; HttpClient is configured
// with the functional authInterceptor (attaches the JWT bearer token and performs the
// silent refresh on 401) — the client-side counterpart of the removed Forms Authentication
// pipeline (AAP §0.4.2, §0.6.4).
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};