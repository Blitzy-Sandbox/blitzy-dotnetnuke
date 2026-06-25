// MIGRATION: Angular root shell replacing the DNN Web Forms master/skin layout (Default.aspx + skin
// objects). Renders the layout shell (header/sidebar/footer) around a <router-outlet/>. No business
// logic — Web Forms postback/ViewState machinery discarded. Net-new.
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { FooterComponent } from './layout/footer/footer.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, FooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-header />
    <div class="app-shell">
      <app-sidebar />
      <main id="main-content" class="app-content" role="main" tabindex="-1">
        <router-outlet />
      </main>
    </div>
    <app-footer />
  `,
  styles: [
    `
      :host {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }
      .app-shell {
        display: flex;
        flex: 1 1 auto;
      }
      .app-content {
        flex: 1 1 auto;
        padding: var(--space-4, 16px);
      }
    `,
  ],
})
export class AppComponent {}
