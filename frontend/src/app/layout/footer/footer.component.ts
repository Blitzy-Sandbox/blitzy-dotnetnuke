import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Application footer — bottom chrome of the DNN Migration admin SPA.
 *
 * Rendered once by the root {@link AppComponent} as `<app-footer />` beneath the
 * shell and `<router-outlet>`. It is intentionally a simple, static, presentational
 * component: it displays the application name and a dynamic copyright year and holds
 * NO business/domain logic, data fetching, or routing.
 *
 * Unlike the header and sidebar, the footer does NOT self-hide when unauthenticated;
 * it stays minimal and unobtrusive so it is acceptable on the public `/auth/login`
 * page as well as the authenticated administrative screens.
 *
 * MIGRATION: Brand-new frontend chrome (AAP §0.3.4). There is no 1:1 legacy
 * equivalent — the DNN Skinning/Container footer engine is out of scope (AAP §0.2.2),
 * and per-portal `PortalInfo.FooterText` / per-module `ModuleInfo.Footer` are domain
 * fields owned by the Portal/Module features, not this layout chrome.
 */
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer" role="contentinfo">
      <p class="app-footer__text">
        &copy; {{ currentYear }} DNN Migration. All rights reserved.
      </p>
    </footer>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .app-footer {
        display: flex;
        align-items: center;
        justify-content: center;
        padding: var(--space-3, 12px) var(--space-4, 16px);
        border-top: 1px solid var(--color-border, #e2e8f0);
        background: var(--color-surface, #f8fafc);
        color: var(--color-text-muted, #64748b);
      }

      .app-footer__text {
        margin: 0;
        font-size: 0.8125rem;
        line-height: 1.5;
        text-align: center;
      }
    `,
  ],
})
export class FooterComponent {
  /** Current calendar year shown in the copyright notice. */
  protected readonly currentYear: number = new Date().getFullYear();
}
