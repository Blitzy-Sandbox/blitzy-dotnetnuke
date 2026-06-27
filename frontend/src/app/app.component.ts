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
        /* MIGRATION: [QA F3 #1 responsive overflow] min-width:0 lets this flex item
           shrink below the intrinsic width of its content, so wide descendants (the
           shared data-table) scroll inside their own .dt-scroll container instead of
           forcing whole-page horizontal overflow beside the fixed-width sidebar. */
        min-width: 0;
        padding: var(--space-4, 16px);
      }

      /* MIGRATION: [QA F3 #1 responsive overflow] At <=768px the shell stacks into a
         single column: the sidebar moves above the content (its own width/border
         collapse is handled in sidebar.component.ts) so narrow viewports get the full
         available width and no horizontal overflow. The 768px breakpoint matches the
         QA tablet breakpoint where wide-table screens (users/roles) overflowed. */
      @media (max-width: 768px) {
        .app-shell {
          flex-direction: column;
        }
      }
    `,
  ],
})
export class AppComponent {}
