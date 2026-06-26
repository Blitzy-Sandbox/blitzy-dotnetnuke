// MIGRATION: Replaces DNN web.config <appSettings>/provider wiring with Angular ApplicationConfig
// providers — zone change detection, router (preloading + route-param input binding), HttpClient with
// the token & error functional interceptors, and animations. Consumed by frontend/src/main.ts. Net-new.
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import {
  provideRouter,
  withComponentInputBinding,
  withPreloading,
  PreloadAllModules,
} from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { tokenInterceptor } from './core/interceptors/token.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding(), withPreloading(PreloadAllModules)),
    provideHttpClient(withInterceptors([tokenInterceptor, errorInterceptor])),
    provideAnimations(),
  ],
};
