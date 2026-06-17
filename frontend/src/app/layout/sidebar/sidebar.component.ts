import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/** A single primary-navigation destination rendered in the sidebar. */
interface NavItem {
  readonly label: string;
  readonly path: string;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SidebarComponent {
  /**
   * Root-provided authentication singleton. Exposed as `protected` so the
   * template can read `auth.isAuthenticated()` under `strictTemplates`.
   */
  protected readonly auth = inject(AuthService);

  /**
   * Primary navigation destinations spanning the five in-scope admin feature
   * areas: Portals, Modules, Users, Roles, and Tabs. Each maps to a routed
   * feature area declared in `app.routes.ts` (`PORTAL_ROUTES`, `MODULE_ROUTES`,
   * `USER_ROUTES`, `ROLE_ROUTES`, `TAB_ROUTES`) and is rendered as a
   * `RouterLink` entry with active-route highlighting.
   */
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Modules', path: '/modules' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
    { label: 'Tabs', path: '/tabs' },
  ];
}
