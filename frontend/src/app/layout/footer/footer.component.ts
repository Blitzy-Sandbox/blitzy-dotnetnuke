import { ChangeDetectionStrategy, Component } from '@angular/core';

// MIGRATION: Legacy DNN footer/copyright (Website/Default.aspx.vb L195-200):
//   ' META copyright
//   If PortalSettings.FooterText <> "" Then
//       Copyright = PortalSettings.FooterText
//   Else
//       Copyright = "Copyright (c) " & Year(Now()) & " by " & PortalSettings.PortalName
//   End If
// The stateless SPA has no server-side PortalSettings, so the FooterText override
// branch is dropped and only the ELSE/fallback form is rendered: a dynamic current
// year (new Date().getFullYear() maps to VB Year(Now())) plus a static brand name
// (replacing PortalSettings.PortalName). Legacy ViewState / postback
// (IClientAPICallbackEventHandler, Website/Default.aspx.vb L43) is eliminated, not ported.
//
// Presentation-only app-shell footer rendered by the root shell as <app-footer />,
// pinned to the bottom of the flex-column `.app-shell` beneath the header/sidebar/main
// `.app-body` region. Dependency-free: no AuthService, no router links, no HTTP, no
// business logic (depends_on_files is empty).
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer" role="contentinfo">
      <p class="app-footer__copyright">
        Copyright (c) {{ currentYear }} by {{ brandName }}
      </p>
    </footer>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      /*
       * Full-width footer bar. Custom properties resolve to the global
       * styles.scss theme tokens (--color-muted, --color-border,
       * --color-surface, --space-*), each with a literal fallback matching the
       * theme's real value so the bar still renders correctly if the global
       * tokens are unavailable (e.g. isolated component rendering / tests).
       */
      .app-footer {
        width: 100%;
        padding: var(--space-3, 0.75rem) var(--space-4, 1rem);
        border-top: 1px solid var(--color-border, #e2e8f0);
        background: var(--color-surface, #ffffff);
        color: var(--color-muted, #64748b);
        text-align: center;
      }

      .app-footer__copyright {
        margin: 0;
        font-size: var(--font-size-sm, 0.875rem);
      }
    `,
  ],
})
export class FooterComponent {
  /**
   * Current calendar year for the copyright line.
   *
   * MIGRATION: maps to the legacy VB `Year(Now())` used to build the DNN footer
   * copyright text. Evaluated once at construction time, which keeps the value
   * stable for the component's lifetime and is safe under `OnPush` change
   * detection (no per-cycle recomputation, no change-detection churn). A Signal
   * is intentionally not used: the value is static per session.
   */
  readonly currentYear = new Date().getFullYear();

  /**
   * Brand name shown after "by" in the copyright line.
   *
   * MIGRATION: replaces the legacy `PortalSettings.PortalName`. The stateless SPA
   * has no per-request portal settings, so a static application brand name is used.
   */
  readonly brandName = 'DNN Migration';
}
