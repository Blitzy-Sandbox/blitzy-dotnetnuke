import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';

/**
 * NavItem — a single primary-navigation entry in the sidebar rail.
 *
 * `roles` is typed `string[]` (NOT `readonly string[]`) so it is assignable to the
 * HasPermissionDirective input (`string | string[]`) under strictTemplates.
 */
interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly roles: string[];
}

/**
 * SidebarComponent — the SPA shell's primary navigation rail.
 *
 * MIGRATION: replaces the legacy DotNetNuke tab/breadcrumb navigation
 * (PortalSettings.ActiveTab.BreadCrumbs, Website/Default.aspx.vb L152-161) and the
 * admin menu under Website/admin/{Portal,Modules,Users,Security}. Legacy
 * postback/ViewState/IClientAPICallbackEventHandler navigation is ELIMINATED in
 * favor of stateless client-side routing via routerLink (AAP §0.6.3); the legacy
 * "current tab/breadcrumb" highlight becomes routerLinkActive.
 */
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="sidebar" role="navigation" aria-label="Primary">
      <ul class="nav-list">
        @for (item of navItems; track item.path) {
          <li class="nav-item" *appHasPermission="item.roles">
            <a
              class="nav-link"
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: false }"
              ariaCurrentWhenActive="page"
            >
              {{ item.label }}
            </a>
          </li>
        }
      </ul>
    </nav>
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
      }
      .sidebar {
        height: 100%;
        padding: var(--space-3) 0;
        background: var(--color-surface);
        border-right: 1px solid var(--color-border);
      }
      .nav-list {
        margin: 0;
        padding: 0;
        list-style: none;
        display: flex;
        flex-direction: column;
        gap: var(--space-1);
      }
      .nav-link {
        display: block;
        padding: var(--space-2) var(--space-4);
        color: var(--color-text);
        text-decoration: none;
        border-left: 3px solid transparent;
        transition:
          background-color 0.15s ease,
          border-color 0.15s ease,
          color 0.15s ease;
      }
      .nav-link:hover {
        background: var(--color-surface-hover);
      }
      .nav-link:focus-visible {
        outline: 2px solid var(--color-primary);
        outline-offset: -2px;
      }
      .nav-link.active {
        color: var(--color-primary);
        background: var(--color-primary-subtle);
        border-left-color: var(--color-primary);
        font-weight: 600;
      }
    `,
  ],
})
export class SidebarComponent {
  // MIGRATION: the four links map to the legacy admin areas
  // (Website/admin/{Portal,Modules,Users,Security}). Role-gating with
  // ['Administrators'] mirrors the legacy SecurityAccessLevel.Admin /
  // PortalSecurity.IsInRoles(PortalSettings.AdministratorRoleName) restriction
  // (verified in Website/admin/Security/SecurityRoles.ascx.vb L322); the shared
  // *appHasPermission directive adds the super-user override.
  readonly navItems: NavItem[] = [
    { path: '/portals', label: 'Portals', roles: ['Administrators'] },
    { path: '/modules', label: 'Modules', roles: ['Administrators'] },
    { path: '/users', label: 'Users', roles: ['Administrators'] },
    { path: '/roles', label: 'Roles', roles: ['Administrators'] },
  ];
}
