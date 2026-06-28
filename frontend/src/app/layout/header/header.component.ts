// MIGRATION: Net-new Angular header shell. No 1:1 legacy source — the DNN admin skin/master chrome
// (Website/admin skinning) is out of scope (AAP §0.6.2); only the brand + user-menu/logout concept is
// reproduced. Auth state + logout are delegated to core/auth/AuthService (replacing PortalSecurity/UserMembership
// Forms auth). Renders the accessibility skip-link whose target #main-content lives in app.component.ts.
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- MIGRATION: skip-link MUST be the first focusable element; target #main-content is in app.component.ts. -->
    <!-- MIGRATION: [QA F4-011] A plain href="#main-content" resolves against <base href="/"> to "/#main-content",
         which is a DIFFERENT document path than the current route -> the browser performs a CROSS-DOCUMENT
         navigation + full SPA reload. That reload clears the memory-only JWT (AuthService signals), logging the
         user out and redirecting to /auth/login -- making the documented WCAG 2.4.1 bypass link actively harmful.
         The (click) handler intercepts activation (mouse click AND keyboard Enter, which dispatches a click on an
         anchor), prevents the default navigation, and moves focus to #main-content (tabindex="-1" in
         app.component.ts) so focus reaches main WITHOUT any navigation or reload. The href is retained for
         visible affordance and as the no-JS fallback. -->
    <a class="skip-link" href="#main-content" (click)="skipToMain($event)">Skip to main content</a>
    <header class="app-header" role="banner">
      <a class="brand" routerLink="/portals">DnnMigration Admin</a>
      @if (isAuthenticated()) {
        <div class="user-menu">
          <span class="user-name">{{ currentUser()?.displayName || currentUser()?.username }}</span>
          <button type="button" class="logout-btn" (click)="logout()">Log out</button>
        </div>
      }
    </header>
  `,
  styles: [
    `
      :host { display: block; }
      .skip-link {
        position: absolute; left: -999px; top: 0; z-index: 1000;
        padding: var(--space-2, 8px) var(--space-3, 12px);
        background: var(--color-primary, #1976d2); color: var(--color-primary-contrast, #fff);
      }
      .skip-link:focus { left: var(--space-2, 8px); }
      .app-header {
        display: flex; align-items: center; justify-content: space-between;
        padding: var(--space-3, 12px) var(--space-4, 16px);
        background: var(--color-surface, #fff);
        border-bottom: 1px solid var(--color-border, #e0e0e0);
      }
      .brand { font-weight: 600; color: var(--color-text, #1a1a1a); text-decoration: none; }
      .user-menu { display: flex; align-items: center; gap: var(--space-3, 12px); }
      /* MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #16] the Log out control was an unstyled native
         button (~30px tall). Give it the shared >=44px touch target plus a token-based affordance
         consistent with the .btn system. */
      .logout-btn {
        display: inline-flex; align-items: center; justify-content: center;
        min-height: var(--touch-target-min, 44px);
        padding: var(--space-2, 8px) var(--space-3, 12px);
        font: inherit; color: var(--color-text, #1a1a1a); cursor: pointer;
        background: var(--color-surface, #fff); border: 1px solid var(--color-border, #e0e0e0);
        border-radius: var(--radius-sm, 4px);
      }
      .logout-btn:hover { background: var(--color-surface-muted, #f0f2f5); }
    `,
  ],
})
export class HeaderComponent {
  // MIGRATION: inject() DI (Angular 19) — no constructor injection.
  private readonly auth = inject(AuthService);

  // Expose AuthService signals to the template (read-only).
  protected readonly currentUser = this.auth.currentUser;
  protected readonly isAuthenticated = this.auth.isAuthenticated;

  // MIGRATION: PortalSecurity.SignOut() -> AuthService.logout(); the service clears the in-memory session
  // and redirects to /auth/login via its finalize() operator, so no explicit navigation is needed here.
  // Declared PUBLIC so the spec can invoke it directly (it is also bound from the template).
  logout(): void {
    this.auth.logout().subscribe();
  }

  // MIGRATION: [QA F4-011] Activate the "skip to main content" bypass WITHOUT navigating. Calling
  // preventDefault() stops the default fragment-href navigation (which, under <base href="/">, would reload the
  // SPA and clear the in-memory session); focus is then moved to the #main-content landmark (declared with
  // tabindex="-1" in app.component.ts so it is programmatically focusable), satisfying WCAG 2.4.1 Bypass Blocks.
  skipToMain(event: Event): void {
    event.preventDefault();
    const main = document.getElementById('main-content');
    if (main) {
      main.focus();
      main.scrollIntoView();
    }
  }
}
