import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { FooterComponent } from './layout/footer/footer.component';

// MIGRATION: Root shell replaces the legacy DNN Web Forms host page
// (Website/Default.aspx.vb): the skin/SkinPlaceHolder chrome becomes <app-header>/<app-sidebar>,
// FooterText/Copyright becomes <app-footer>, and the page body becomes <router-outlet>.
// ViewState/postback/IClientAPICallbackEventHandler is eliminated (stateless SPA).
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, FooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip-link" href="#main-content">Skip to main content</a>
    <div class="app-shell">
      <app-header />
      <div class="app-body">
        <app-sidebar />
        <main id="main-content" class="app-content" role="main" tabindex="-1">
          <router-outlet />
        </main>
      </div>
      <app-footer />
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .app-shell {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }
      .app-body {
        display: flex;
        flex: 1 1 auto;
        min-height: 0;
      }
      .app-content {
        flex: 1 1 auto;
        /* QA R10 Issues 3/6/10/15: a flex item defaults to min-width:auto, so it refuses to
           shrink below its content's intrinsic width. A wide data-table therefore forced this
           column WIDER than the viewport, pushing row actions off-screen and defeating the
           table's own overflow-x:auto (its 100% width equalled the over-wide column, so nothing
           scrolled). min-width:0 lets the content column shrink to the available flex space so
           the inner .dt__table-wrap can finally scroll horizontally within the viewport. */
        min-width: 0;
        padding: var(--space-4, 1rem);
        overflow: auto;
      }
      .skip-link {
        position: absolute;
        left: -999px;
        top: 0;
        z-index: 1000;
        padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        background: var(--color-primary, #1976d2);
        color: var(--color-primary-contrast, #ffffff);
      }
      .skip-link:focus {
        left: 0;
      }
    `,
  ],
})
export class AppComponent {}
