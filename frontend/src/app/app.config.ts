import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withPreloading, PreloadAllModules } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

// MIGRATION: Replaces the legacy DNN application bootstrap (Global.asax lifecycle +
// Website/Default.aspx.vb service/AJAX registration). All app-wide providers are declared
// here (standalone bootstrap, no NgModules): router with route-preloading, HttpClient with
// the JWT auth interceptor, zone-based change detection, and animations.
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withPreloading(PreloadAllModules)),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
  ],
};
