import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * HeaderComponent — top bar of the SPA shell, rendered by the root shell as
 * `<app-header />`.
 *
 * MIGRATION: Replaces the legacy DNN skin header / page-title brand from
 * Website/Default.aspx.vb. The legacy code (L152-161) built the page title from
 * `PortalSettings.PortalName` (`Dim strTitle As String = PortalSettings.PortalName`,
 * L153). The stateless SPA has no per-request `PortalSettings`, so the brand
 * becomes a static application constant. Legacy ViewState / postback / AJAX
 * ScriptManager machinery (IClientAPICallbackEventHandler, Website/Default.aspx.vb
 * L42-L43) is ELIMINATED, not ported (AAP §0.6.3).
 *
 * Presentation + auth-state read ONLY: no business logic, no HttpClient/HTTP
 * calls, and no routing logic. Navigation on logout happens inside
 * AuthService.logout(). Current-user state is read reactively from the
 * AuthService.currentUser() Signal under OnPush change detection.
 */
@Component({
  selector: 'app-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="app-header" role="banner">
      <div class="app-header__brand">
        <span class="app-header__title">{{ brand }}</span>
      </div>

      <!-- MIGRATION: current-user read reactively from AuthService.currentUser()
           Signal; renders nothing when unauthenticated. Replaces the legacy
           server-side PortalSettings/authenticated-user shell state. -->
      @if (authService.currentUser(); as user) {
        <div class="app-header__user">
          <span class="app-header__username">{{ user.displayName || user.username }}</span>
          <!-- MIGRATION: replaces PortalSecurity.SignOut() /
               FormsAuthentication.SignOut(); a real, keyboard-navigable button. -->
          <button
            type="button"
            class="app-header__logout"
            aria-label="Log out"
            (click)="onLogout()"
          >
            Logout
          </button>
        </div>
      }
    </header>
  `,
  styles: [
    `
      /*
       * Component-scoped SCSS. App-level CSS custom properties resolve to the
       * global styles.scss theme tokens; each is paired with a literal fallback
       * so the bar still renders correctly if the global tokens are unavailable
       * (e.g. isolated component rendering / unit tests). Same pattern as the
       * sibling layout/footer/footer.component.ts.
       */
      :host {
        display: block;
      }

      /* MIGRATION / F9 (responsive): allow the bar to wrap so the user block drops onto
         its own row on narrow viewports instead of overflowing the layout. The gap applies
         to both the row and the wrapped column axis, so wrapped rows stay spaced. */
      .app-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: var(--space-3, 0.75rem);
        padding: var(--space-3, 0.75rem) var(--space-4, 1rem);
        background-color: var(--color-primary, #1976d2);
        color: var(--color-primary-contrast, #ffffff);
      }

      /* MIGRATION / F9 (responsive): min-width:0 lets the brand flex-item shrink below its
         content width so a long title can truncate rather than push the user block off-screen. */
      .app-header__brand {
        min-width: 0;
      }

      .app-header__title {
        display: block;
        font-size: 1.125rem;
        font-weight: 600;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      /* MIGRATION / F9 (responsive): min-width:0 + flex-wrap let this block shrink and, if
         needed, wrap the username above the logout button on very small screens. */
      .app-header__user {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        min-width: 0;
        gap: var(--space-3, 0.75rem);
      }

      /* MIGRATION / F9 (responsive): truncate an over-long username with an ellipsis instead
         of forcing horizontal overflow (min-width:0 is required for a flex-item to shrink). */
      .app-header__username {
        min-width: 0;
        max-width: 100%;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-weight: 500;
      }

      .app-header__logout {
        cursor: pointer;
        padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        border: 1px solid var(--color-primary-contrast, #ffffff);
        border-radius: 4px;
        background: transparent;
        color: var(--color-primary-contrast, #ffffff);
        font: inherit;
      }

      /* Visible hover/focus affordance for accessibility (keyboard + pointer). */
      .app-header__logout:hover,
      .app-header__logout:focus-visible {
        background-color: rgba(255, 255, 255, 0.15);
      }

      /* MIGRATION / F9 (responsive): on phone-width viewports the wrapped user block takes the
         full row so the username sits at the start and the logout button at the end, and the
         bar's horizontal padding tightens to reclaim space. */
      @media (max-width: 480px) {
        .app-header {
          padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        }

        .app-header__user {
          width: 100%;
          justify-content: space-between;
        }
      }
    `,
  ],
})
export class HeaderComponent {
  // MIGRATION: constructor-less inject() DI (folder convention, matches
  // AuthService/FooterComponent). Declared `protected` (NOT `private`) so the
  // inline template can reference authService.currentUser()/logout() under
  // Angular 19 strictTemplates + the AOT production build; a `private` member
  // referenced from a template is a compile error.
  protected readonly authService = inject(AuthService);

  // MIGRATION: replaces the PortalSettings.PortalName page-title brand
  // (Website/Default.aspx.vb L153); the SPA has no PortalSettings, so a static
  // application brand string is used. `protected` because it is template-referenced.
  protected readonly brand = 'DNN Migration';

  /** Clears the in-memory session and navigates to /auth (AuthService.logout()). */
  // MIGRATION: replaces PortalSecurity.SignOut() / FormsAuthentication.SignOut().
  protected onLogout(): void {
    this.authService.logout();
  }
}
