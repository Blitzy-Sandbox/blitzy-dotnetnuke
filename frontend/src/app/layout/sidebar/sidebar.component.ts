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
   * Primary navigation destinations. These map 1:1 to the routed feature
   * areas declared in `app.routes.ts` (portals, modules, users, roles).
   * No "Tabs" route exists, so Tabs is rendered as a disabled, non-routing
   * item in the template rather than linked here.
   */
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Modules', path: '/modules' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
  ];
}
