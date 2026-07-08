import { bootstrapApplication } from '@angular/platform-browser';

import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

// MIGRATION: SPA entry point — replaces the legacy Web Forms host-page bootstrap
// (Website/Default.aspx.vb). Boots the standalone root component with the
// application-wide providers defined in app.config.ts. ViewState/postback and the
// IClientAPICallbackEventHandler machinery are eliminated, not ported (AAP §0.6.3).
bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));