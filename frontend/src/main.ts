// MIGRATION: Angular 19 standalone bootstrap entry replacing the DNN Web Forms server-side
// page lifecycle (no Global.asax / Page_Init). Boots AppComponent with ApplicationConfig providers.
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
