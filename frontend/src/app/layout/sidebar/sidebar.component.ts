import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/** A single primary-navigation destination rendered in the sidebar. */
interface NavItem {
  readonly label: string;
  readonly path: string;
  /**
   * When true, the item is shown but rendered as a non-routing, disabled
   * affordance (no RouterLink, aria-disabled). Used to preserve the required
   * navigation surface for a feature whose route is not yet declared.
   */
  readonly disabled?: boolean;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
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
   * Primary navigation destinations, preserving the required Portals / Modules /
   * Users / Roles / Tabs navigation surface (AAP Section 0.3.4).
   *
   * Portals, Modules, Users, and Roles map 1:1 to the routed feature areas declared
   * in `app.routes.ts`. The Tabs (DNN pages) feature arrives in a later checkpoint and
   * its route is not yet declared, so it is included here with `disabled: true` and
   * rendered by the template as an accessible, non-routing item - keeping the full
   * navigation surface visible without linking to a route that does not yet exist.
   */
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Modules', path: '/modules' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
    // MIGRATION: route deferred to the Tabs feature checkpoint; rendered disabled for now.
    { label: 'Tabs', path: '/tabs', disabled: true },
  ];
}
