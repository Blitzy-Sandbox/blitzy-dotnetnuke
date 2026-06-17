import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  viewChild,
} from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';
import { LayoutService } from '../layout.service';
import { IconComponent } from '../../shared/components/icon';

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
  imports: [IconComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);

  /**
   * Shared shell-UI state. The header owns the hamburger button that toggles
   * the responsive sidebar drawer on narrow viewports (QA Issue #4).
   */
  protected readonly layout = inject(LayoutService);

  /**
   * Reference to the hamburger toggle button. After the drawer is dismissed
   * with Escape, keyboard focus is returned here so it never lands on a
   * now-hidden off-canvas element (WCAG 2.1 keyboard focus management;
   * AAP §0.3.4). `viewChild` resolves once the (authenticated) header renders.
   */
  private readonly menuButton = viewChild<ElementRef<HTMLButtonElement>>('menuButton');

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

  /** Toggles the responsive sidebar drawer (visible only ≤768px). */
  protected toggleSidebar(): void {
    this.layout.toggleSidebar();
  }

  /**
   * Closes the mobile sidebar drawer when Escape is pressed while it is open,
   * then returns focus to the hamburger toggle (QA: drawer must close on
   * Escape; AAP §0.3.4 keyboard support). The guard makes this a no-op when the
   * drawer is already closed, so it never swallows Escape from other
   * Escape-driven UI (e.g. the confirmation dialog / tooltip) and only acts
   * when the off-canvas drawer is actually obscuring content on small screens.
   */
  @HostListener('document:keydown.escape')
  protected onEscapeKey(): void {
    if (!this.layout.sidebarOpen()) {
      return;
    }
    this.layout.closeSidebar();
    this.menuButton()?.nativeElement.focus();
  }
}
