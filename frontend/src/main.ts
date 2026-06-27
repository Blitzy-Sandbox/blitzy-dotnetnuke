// MIGRATION: Angular 19 standalone bootstrap entry replacing the DNN Web Forms server-side
// page lifecycle (no Global.asax / Page_Init). Boots AppComponent with ApplicationConfig providers.
// This is intentionally the trivial standalone entry: the single bootstrapApplication(AppComponent,
// appConfig) call. The trailing `.catch((err) => console.error(err))` is the Angular CLI default and
// is RETAINED deliberately -- bootstrapApplication returns a Promise, and without a catch a failed
// boot (e.g. a provider/DI error) would surface only as an unhandled promise rejection that is easy
// to miss. Logging it keeps the failure visible while adding no app logic to the bootstrap.
import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
