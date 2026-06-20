import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { FooterComponent } from './layout/footer/footer.component';

/**
 * AppComponent — the root standalone component of the DNN Migration Angular 19 SPA.
 *
 * This is the visual root of the application. It is bootstrapped by `src/main.ts`
 * via `bootstrapApplication(AppComponent, appConfig)` and rendered into the
 * `<app-root></app-root>` host element declared in `src/index.html`. Its sole
 * responsibility is to lay out the persistent application chrome — header on top,
 * sidebar plus the routed content region in the middle, footer at the bottom —
 * around the Angular `<router-outlet>` where feature screens are rendered.
 *
 * Architecture:
 *   - Standalone component (Angular 19 makes components standalone by default; the
 *     redundant `standalone: true` is intentionally omitted, which is the v19 idiom).
 *     It declares its template dependencies directly via the `imports` array — there
 *     are no NgModules anywhere in this SPA (AAP §0.3.4).
 *   - `ChangeDetectionStrategy.OnPush` is mandatory for every component in this
 *     codebase (AAP §0.3.4 / §0.7.2). The shell holds no mutable input state, so
 *     OnPush incurs effectively zero change-detection cost here.
 *   - Template and styles are kept INLINE on purpose: the target frontend tree
 *     (AAP §0.3.1) lists only `app.component.ts` at the `app/` root, so no sibling
 *     `app.component.html` / `app.component.scss` files are created. The inline
 *     styles stay well within the `anyComponentStyle` 6 kb budget (angular.json).
 *
 * MIGRATION: This shell is brand-new frontend infrastructure with no 1:1 legacy
 * equivalent. The DotNetNuke Skinning Engine, Container System and master-page
 * chrome are explicitly out of scope (AAP §0.2.2); the SPA layout is reconstructed
 * from first principles per the target design (AAP §0.3.1 / §0.3.4).
 *
 * NOTE: The root shell ALWAYS renders the header, sidebar and footer. Hiding the
 * admin chrome on the public `/auth/login` route is deliberately NOT done here —
 * that responsibility is delegated to the `layout/` components themselves:
 * `HeaderComponent` and `SidebarComponent` self-hide when unauthenticated (they read
 * the `isAuthenticated` signal from `core/auth/auth.service.ts` and render nothing),
 * keeping this root component free of authentication/permission logic.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, FooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!--
      Skip-navigation link: the first focusable element on the page. It lets
      keyboard and screen-reader users bypass the header/sidebar chrome and jump
      straight to the routed content (AAP §0.7.2 — keyboard/screen-reader a11y).
      It is visually hidden until focused, so it has no impact for pointer users.
    -->
    <a class="app-skip-link" href="#main-content">Skip to main content</a>

    <app-header />

    <div class="app-shell">
      <app-sidebar />

      <!-- Semantic landmark for the routed content; programmatically focusable
           (tabindex="-1") so the skip link above can move focus into it. -->
      <main id="main-content" class="app-main" tabindex="-1">
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

      /* Header (top) and footer (bottom) keep their intrinsic height; the shell
         row between them grows to fill the remaining vertical space. */
      .app-shell {
        display: flex;
        flex: 1 1 auto;
        /* Allow the routed <main> to own its own scroll instead of stretching
           the shell beyond the viewport. */
        min-height: 0;
      }

      .app-main {
        flex: 1 1 auto;
        padding: var(--space-4, 16px);
        overflow: auto;
      }

      /* Accessible skip link: removed from view (translated off-screen) until it
         receives keyboard focus, then revealed at the top-inline-start corner.
         Reveal is instant — no transition — so there is nothing to gate behind
         prefers-reduced-motion. */
      .app-skip-link {
        position: absolute;
        inset-block-start: var(--space-2, 8px);
        inset-inline-start: var(--space-2, 8px);
        z-index: 1000;
        padding: var(--space-2, 8px) var(--space-3, 12px);
        border-radius: var(--radius-sm, 4px);
        background: var(--color-primary, #1565c0);
        color: var(--color-primary-contrast, #ffffff);
        text-decoration: none;
        transform: translateY(-200%);
      }

      .app-skip-link:focus-visible {
        transform: translateY(0);
      }
    `,
  ],
})
export class AppComponent {}
