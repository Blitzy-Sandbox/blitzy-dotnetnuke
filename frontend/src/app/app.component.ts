import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { FooterComponent } from './layout/footer/footer.component';

/**
 * AppComponent — the ROOT standalone component of the DNN Migration Angular 19 SPA.
 *
 * Bootstrap entry point: `frontend/src/main.ts` calls
 * `bootstrapApplication(AppComponent, appConfig)` and Angular renders this
 * component into the `<app-root></app-root>` host element declared in
 * `frontend/src/index.html`. It is mandated as the application's visual root by
 * AAP §0.3.1 (frontend tree) and §0.3.4 (UI design — standalone-component SPA).
 *
 * Responsibility — application chrome / shell ONLY:
 * It composes the persistent administrative chrome (header on top, sidebar +
 * routed content in the middle row, footer at the bottom) around the Angular
 * Router's `<router-outlet>`. The routed feature areas (portals, modules, users,
 * roles, auth/login) are projected into the `<main>` content region by the
 * router; this component renders none of that domain content itself.
 *
 * Architecture:
 * - Standalone (Angular 19 default — NO NgModule). `standalone: true` is omitted
 *   because it is redundant in v19 and the project compiles under
 *   `strictStandalone` (see frontend/tsconfig.json).
 * - `ChangeDetectionStrategy.OnPush` — mandated by AAP §0.3.4/§0.7.x and the
 *   angular.json schematic default. This component holds no mutable state, so
 *   OnPush incurs zero change-detection work for the shell itself.
 * - Granular standalone `imports` — every entry is consumed by the inline
 *   template (`RouterOutlet` → `<router-outlet>`, and the three layout
 *   components via their `app-header` / `app-sidebar` / `app-footer` selectors),
 *   so there are no unused imports (Gate 3 requires zero warnings).
 * - INLINE `template` and INLINE `styles` — the AAP §0.3.1 file inventory lists
 *   ONLY `app.component.ts` at the app/ root (no sibling `.html` / `.scss`), so
 *   keeping both inline preserves the exact target structure. The inline style
 *   block is intentionally tiny to stay well within the angular.json
 *   `anyComponentStyle` budget (6 kB warning / 10 kB error).
 *
 * Accessibility (invisible — always applied):
 * The routed content region uses the semantic `<main>` landmark. The page
 * `lang` attribute lives on `<html lang="en">` in index.html, and the header /
 * footer contribute their own `banner` / `contentinfo` landmarks, giving the
 * shell a complete, screen-reader-navigable landmark structure.
 *
 * NOTE: Login-chrome handling is intentionally delegated to the layout child
 * components — it is NOT implemented here. The public `/auth/login` route should
 * not display the authenticated admin chrome; rather than branching in this
 * root component, `HeaderComponent` and `SidebarComponent` self-hide while
 * unauthenticated (they read the `isAuthenticated` signal from
 * `core/auth/auth.service.ts` and render nothing). `AppComponent` therefore
 * always renders the shell and stays "dumb" — it contains NO authentication,
 * permission, routing, or data-access logic (AAP §0.2.2 keeps such concerns out
 * of the presentation root).
 *
 * MIGRATION: Brand-new frontend application root (AAP §0.3.4). There is no 1:1
 * legacy equivalent — the DNN Web Forms shell was provided by the Skinning
 * Engine, master pages, and the control panel, all of which are explicitly out
 * of scope (AAP §0.2.2). This component replaces that server-rendered chrome
 * with a single client-rendered standalone shell.
 *
 * @see frontend/src/main.ts — bootstraps this component.
 * @see frontend/src/index.html — provides the `<app-root>` host element.
 * @see ./layout/header/header.component — `<app-header />` (top chrome).
 * @see ./layout/sidebar/sidebar.component — `<app-sidebar />` (primary nav).
 * @see ./layout/footer/footer.component — `<app-footer />` (bottom chrome).
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, FooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-header />
    <div class="app-shell">
      <app-sidebar />
      <main class="app-main">
        <router-outlet />
      </main>
    </div>
    <app-footer />
  `,
  styles: [
    `
      /* Full-viewport vertical stack: header, growing shell row, footer. */
      :host {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      /* Middle row: fixed-width sidebar beside the growing content region.
         flex: 1 1 auto lets the row absorb the space between header and footer;
         min-height: 0 allows the inner <main> to scroll instead of forcing the
         shell taller than the viewport. */
      .app-shell {
        display: flex;
        flex: 1 1 auto;
        min-height: 0;
      }

      /* Routed content region. min-width: 0 lets this flex child shrink so wide
         content (tables, grids) scrolls within <main> rather than overflowing
         the layout horizontally (defensive against unbounded content). */
      .app-main {
        flex: 1 1 auto;
        min-width: 0;
        padding: var(--space-4, 16px);
        overflow: auto;
      }
    `,
  ],
})
export class AppComponent {}
