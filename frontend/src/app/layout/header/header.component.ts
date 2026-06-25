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
    <a class="skip-link" href="#main-content">Skip to main content</a>
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
}
