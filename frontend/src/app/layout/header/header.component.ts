import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

/**
 * HeaderComponent — top chrome of the DNN Migration admin SPA.
 *
 * Renders the application brand, the current user's display name, and a logout
 * action. It is rendered once by the root {@link AppComponent} as
 * `<app-header />`. The self-hide gate (`@if (auth.isAuthenticated())`) lives in
 * the template (`header.component.html`), so this class exposes `auth` as a
 * template-accessible (`protected`) member; the header therefore hides itself on
 * the public `/auth/login` route while the shell always renders `<app-header />`.
 *
 * Standalone (Angular 19 default — NO NgModule), `OnPush` change detection, and
 * `inject()`-based DI. All session/token/navigation logic is owned by
 * {@link AuthService}; this component holds none of it (AAP §0.2.2 / §0.3.4).
 *
 * MIGRATION: Brand-new frontend chrome (AAP §0.3.4). There is no 1:1 legacy
 * source — the DNN "current user + logout" lived in the Skinning Engine /
 * control panel, which is explicitly out of scope (§0.2.2). The contextual
 * `source_files` (Website/admin/Portal/Portals.ascx.vb,
 * Website/admin/Users/ManageUsers.ascx.vb) are only the in-scope admin
 * workflows this chrome wraps; no markup is ported from them.
 */
@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);

  protected readonly displayName = computed<string>(() => {
    const user = this.auth.currentUser();
    if (user === null) {
      return '';
    }

    // MIGRATION: The shared User model (core/models/user.model.ts) types both
    // `displayName` and `username` as `string | null`, so each is null-coalesced
    // to '' before use. Intent is preserved exactly: prefer `displayName`, and
    // fall back to `username` only when `displayName` is blank/whitespace, with
    // '' as the final fallback so this computed always yields a `string`.
    const display = user.displayName ?? '';
    if (display.trim().length > 0) {
      return display;
    }

    return user.username ?? '';
  });

  protected logout(): void {
    this.auth.logout();
  }
}
