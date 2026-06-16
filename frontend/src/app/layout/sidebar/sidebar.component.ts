import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/** A single primary-navigation destination rendered in the sidebar. */
interface NavItem {
  readonly label: string;
  readonly path: string;
  /**
   * When `true`, the item is rendered as a non-interactive, disabled entry
   * (no `RouterLink`) because its feature route is not yet wired. Used for
   * Tabs, whose routing lands in a later checkpoint.
   */
  readonly disabled?: boolean;
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
   * areas: Portals, Modules, Users, Roles, and Tabs. The first four map to the
   * routed feature areas (declared in `app.routes.ts` in a later checkpoint)
   * and are rendered as `RouterLink` entries with active-route highlighting.
   * Tabs has no route yet (its routing lands in a later checkpoint), so it is
   * flagged `disabled` and rendered as an accessible, non-interactive item in
   * the template rather than as a link.
   */
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Modules', path: '/modules' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
    { label: 'Tabs', path: '/tabs', disabled: true },
  ];
}
