import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { HeaderComponent } from './layout/header/header.component';
import { FooterComponent } from './layout/footer/footer.component';

// MIGRATION: Root SPA shell rendered into <app-root> (src/index.html). Replaces the legacy
// DNN master-page/skin shell (Website/Default.aspx.vb): a static header and footer wrap the
// routed content region. Page content is swapped by the router-outlet as the user navigates
// the lazy-loaded feature areas declared in app.routes.ts.
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, HeaderComponent, FooterComponent],
  template: `
    <app-header />
    <main class="app-content" role="main">
      <router-outlet />
    </main>
    <app-footer />
  `,
})
export class AppComponent {}