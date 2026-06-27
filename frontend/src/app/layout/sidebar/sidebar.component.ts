// MIGRATION: Net-new primary navigation. Mirrors the legacy DNN admin menu (Website/admin: Portals, Users,
// Security/Roles) as Angular routerLinks aligned with app.routes.ts. No code-behind ported (DNN skinning/master
// navigation out of scope, AAP §0.6.2). Navigation only — no data fetching. MIGRATION: [CP4 review] the Modules
// entry is intentionally absent — there is no module-list landing page (AAP 0.4.2); modules are reached in-context.
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

      /* MIGRATION: [QA F3 #1 responsive overflow] Below 768px the fixed 200px rail
         collapses into a full-width horizontal nav bar above the content (the shell
         switches to a single column in app.component.ts). min-width:0 + height:auto
         release the rail's intrinsic width so the content gets the full viewport, and
         the right border becomes a bottom border to read as a top bar. The links wrap
         in a row to stay compact and reachable on small admin viewports. */
      @media (max-width: 768px) {
        .sidebar {
          min-width: 0;
          height: auto;
          border-right: none;
          border-bottom: 1px solid var(--color-border, #e0e0e0);
        }
        .nav-list { flex-direction: row; flex-wrap: wrap; }
      }
    `,
  ],
})
export class SidebarComponent {
  // MIGRATION: nav items mirror the DNN admin menu; paths MUST match app.routes.ts.
  // MIGRATION: [CP4 review — Frontend Routing] The standalone "Modules" item ({ path: '/modules' }) was REMOVED:
  // per AAP 0.4.2 there is no module-list landing page, so /modules rendered no content and the link dead-ended on
  // a blank router outlet. Modules are reached in-context (always with a module :id) from the module workflows
  // (e.g. import/export -> ['/modules', id, 'settings']), not from a top-level nav entry.
  protected readonly navItems: readonly NavItem[] = [
    { label: 'Portals', path: '/portals' },
    { label: 'Users', path: '/users' },
    { label: 'Roles', path: '/roles' },
  ];
}
