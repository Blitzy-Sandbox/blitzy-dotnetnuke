import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

// MIGRATION: Standalone bootstrap replaces the legacy DNN Web Forms page lifecycle
// (Website/Default.aspx.vb / Global.asax). No NgModules; all providers come from appConfig.
bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
