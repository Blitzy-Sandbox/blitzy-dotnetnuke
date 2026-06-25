// MIGRATION: Net-new primary navigation. Mirrors the legacy DNN admin menu (Website/admin: Portals, Users,
// Security/Roles, Modules) as Angular routerLinks aligned with app.routes.ts. No code-behind ported
// (DNN skinning/master navigation out of scope, AAP §0.6.2). Navigation only — no data fetching.
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  readonly label: string;
  readonly path: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="sidebar" aria-label="Primary">
      <ul class="nav-list">
        @for (item of navItems; track item.path) {
          <li class="nav-item">
            <a
              class="nav-link"
              [routerLink]="item.path"
              routerLinkActive="active"
              ariaCurrentWhenActive="page"
            >{{ item.label }}</a>
          </li>
        }
      </ul>
    </nav>
  `,
  styles: [
    `
      :host { display: block; }
      .sidebar {
        padding: var(--space-3, 12px);
        background: var(--color-surface, #fff);
        border-right: 1px solid var(--color-border, #e0e0e0);
        min-width: 200px; height: 100%;
      }
      .nav-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-1, 4px); }
      .nav-link {
        display: block; padding: var(--space-2, 8px) var(--space-3, 12px);
        color: var(--color-text, #1a1a1a); text-decoration: none; border-radius: var(--radius-sm, 4px);
      }
      .nav-link.active { background: var(--color-primary, #1976d2); color: var(--color-primary-contrast, #fff); }
    `,
  ],
})
export class SidebarComponent {
  // MIGRATION: nav items mirror the DNN admin menu; paths MUST match app.routes.ts.
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
    { label: 'Modules', path: '/modules' },
  ];
}
