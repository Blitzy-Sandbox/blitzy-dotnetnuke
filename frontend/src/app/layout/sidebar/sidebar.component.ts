import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { LayoutService } from '../layout.service';

/** A single primary-navigation destination rendered in the sidebar. */
interface NavItem {
  readonly label: string;
  readonly path: string;
  /**
   * When true the item is rendered as a non-routing, visually-unavailable
   * label (no `routerLink`, `aria-disabled`, out of the tab order). Used for
   * "Tabs", which appears in the navigation (AAP §0.3.4) but has no in-scope
   * frontend feature/route (QA Issue #1).
   */
  readonly disabled?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Reflects the responsive drawer's open state onto the host so the SCSS
  // off-canvas transform can react (QA Issue #4). Inert above the 768px
  // breakpoint, where the sidebar is always statically visible.
  host: {
    '[class.sidebar-host--open]': 'layout.sidebarOpen()',
  },
})
export class SidebarComponent {
  /**
   * Root-provided authentication singleton. Exposed as `protected` so the
   * template can read `auth.isAuthenticated()` under `strictTemplates`.
   */
  protected readonly auth = inject(AuthService);

  /** Shared shell-UI state (mobile sidebar drawer open/closed). */
  protected readonly layout = inject(LayoutService);

  /**
   * Primary navigation destinations. The four in-scope admin feature areas —
   * Portals, Modules, Users, Roles — each map to a routed feature declared in
   * `app.routes.ts` (`PORTAL_ROUTES`, `MODULE_ROUTES`, `USER_ROUTES`,
   * `ROLE_ROUTES`) and render as a `RouterLink` with active-route highlighting.
   *
   * "Tabs" is listed for navigational parity (AAP §0.3.4) but is marked
   * `disabled`: there is no in-scope frontend Tabs feature or `/tabs` route
   * (AAP §0.3.1/§0.4.1), so it is rendered non-routing and visually
   * unavailable rather than linking anywhere (QA Issue #1).
   */
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Modules', path: '/modules' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
    { label: 'Tabs', path: '/tabs', disabled: true },
  ];

  /**
   * Closes the mobile drawer after a navigation link is followed, so the
   * sidebar does not stay open over the newly-routed content on small screens.
   */
  protected onNavigate(): void {
    this.layout.closeSidebar();
  }
}
