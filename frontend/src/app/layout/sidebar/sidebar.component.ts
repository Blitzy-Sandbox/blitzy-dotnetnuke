/**
 * @fileoverview Angular 19 Standalone Sidebar Component
 * @description Main navigation sidebar implementing hierarchical menu with feature module links.
 * Provides responsive collapsible navigation with role-based visibility control.
 * 
 * MIGRATION NOTE: Replaces DNN's control panel navigation patterns from:
 * - IconBar.ascx.vb: cmdSite, cmdUsers, cmdRoles, cmdFiles commands (lines 176-234)
 * - Classic.ascx.vb: admin panel navigation structure
 * 
 * Navigation mapping from legacy DNN control panel:
 * - cmdSite → /portals (Site Settings and management)
 * - cmdUsers → /users (User management)
 * - cmdRoles → /roles (Role management)
 * - cmdAddTab/cmdEditTab → /tabs (Tab/page management)
 * - Module management → /modules
 * 
 * The component uses Angular 19's standalone architecture with:
 * - OnPush change detection for optimal performance
 * - Signal-based reactive state management
 * - Control flow syntax (@if, @for) in templates
 * - inject() function for dependency injection
 * 
 * @module layout/sidebar/sidebar.component
 */

import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Interface defining the structure of a navigation menu item.
 * Supports hierarchical navigation with optional child items and role-based visibility.
 */
interface NavItem {
  /** Display label for the navigation item */
  label: string;
  /** Router link path for navigation */
  path: string;
  /** Material icon name or CSS icon class */
  icon: string;
  /** Optional child navigation items for submenus */
  children?: NavItem[];
  /** Indicates if this item requires administrator role access */
  adminOnly?: boolean;
  /** Accessibility label for the navigation item */
  ariaLabel?: string;
}

/**
 * Constant array defining the main navigation structure for the application.
 * Maps legacy DNN control panel commands to Angular routes.
 * 
 * MIGRATION NOTE: Navigation items derived from IconBar.ascx.vb (lines 176-187):
 * - Site (cmdSite) → Portals management
 * - Users (cmdUsers) → User Accounts management  
 * - Roles (cmdRoles) → Security Roles management
 * - Tab management → Pages/Tabs
 * - Module management → Modules
 */
const NAVIGATION_ITEMS: NavItem[] = [
  {
    label: 'Dashboard',
    path: '/dashboard',
    icon: 'dashboard',
    ariaLabel: 'Go to Dashboard'
  },
  {
    label: 'Portals',
    path: '/portals',
    icon: 'business',
    ariaLabel: 'Portal Management',
    adminOnly: true,
    children: [
      { label: 'List', path: '/portals', icon: 'list', ariaLabel: 'View all portals' },
      { label: 'Create', path: '/portals/create', icon: 'add', ariaLabel: 'Create new portal' },
      { label: 'Settings', path: '/portals/settings', icon: 'settings', ariaLabel: 'Portal settings' }
    ]
  },
  {
    label: 'Modules',
    path: '/modules',
    icon: 'widgets',
    ariaLabel: 'Module Management',
    adminOnly: true,
    children: [
      { label: 'List', path: '/modules', icon: 'list', ariaLabel: 'View all modules' },
      { label: 'Definitions', path: '/modules/definitions', icon: 'description', ariaLabel: 'Module definitions' }
    ]
  },
  {
    label: 'Users',
    path: '/users',
    icon: 'people',
    ariaLabel: 'User Management',
    adminOnly: true,
    children: [
      { label: 'List', path: '/users', icon: 'list', ariaLabel: 'View all users' },
      { label: 'Create', path: '/users/create', icon: 'person_add', ariaLabel: 'Create new user' },
      { label: 'Roles', path: '/users/roles', icon: 'admin_panel_settings', ariaLabel: 'User role assignments' }
    ]
  },
  {
    label: 'Roles',
    path: '/roles',
    icon: 'security',
    ariaLabel: 'Role Management',
    adminOnly: true,
    children: [
      { label: 'List', path: '/roles', icon: 'list', ariaLabel: 'View all roles' },
      { label: 'Create', path: '/roles/create', icon: 'add', ariaLabel: 'Create new role' },
      { label: 'Assignments', path: '/roles/assignments', icon: 'assignment_ind', ariaLabel: 'Role assignments' }
    ]
  },
  {
    label: 'Pages',
    path: '/tabs',
    icon: 'tab',
    ariaLabel: 'Page/Tab Management',
    children: [
      { label: 'List', path: '/tabs', icon: 'list', ariaLabel: 'View all pages' },
      { label: 'Create', path: '/tabs/create', icon: 'add', ariaLabel: 'Create new page' }
    ]
  }
];

/**
 * Administrator role name constant for role-based access control.
 * MIGRATION NOTE: Maps to PortalSettings.AdministratorRoleName from IconBar.ascx.vb line 218
 */
const ADMINISTRATOR_ROLE = 'Administrators';

/**
 * SidebarComponent provides the main navigation menu for the Angular application.
 * 
 * This standalone component replaces DNN's IconBar.ascx.vb and Classic.ascx.vb
 * control panel navigation with a modern Angular 19 implementation featuring:
 * - Hierarchical navigation with expandable submenus
 * - Role-based visibility (admin-only items)
 * - Responsive collapsed mode for mobile/tablet views
 * - Signal-based state management for optimal reactivity
 * 
 * @example
 * ```html
 * <!-- In app layout component -->
 * <app-sidebar />
 * <main>
 *   <router-outlet />
 * </main>
 * ```
 * 
 * @class SidebarComponent
 * @implements Component with standalone:true pattern
 */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside 
      class="sidebar" 
      [class.collapsed]="isCollapsed()"
      [attr.aria-expanded]="!isCollapsed()"
      role="navigation"
      aria-label="Main navigation">
      
      <!-- Sidebar Header with Toggle -->
      <div class="sidebar-header">
        @if (!isCollapsed()) {
          <span class="sidebar-logo">DNN Admin</span>
        }
        <button 
          class="toggle-btn"
          (click)="toggleSidebar()"
          [attr.aria-label]="isCollapsed() ? 'Expand sidebar' : 'Collapse sidebar'"
          type="button">
          <span class="icon">{{ isCollapsed() ? '☰' : '✕' }}</span>
        </button>
      </div>

      <!-- Navigation Menu -->
      <nav class="sidebar-nav">
        <ul class="nav-list" role="menubar">
          @for (item of visibleNavItems(); track item.path) {
            <li class="nav-item" role="none">
              @if (item.children && item.children.length > 0) {
                <!-- Parent item with children -->
                <div class="nav-parent">
                  <a
                    class="nav-link"
                    [routerLink]="[item.path]"
                    routerLinkActive="active"
                    [routerLinkActiveOptions]="{ exact: true }"
                    [attr.aria-label]="item.ariaLabel || item.label"
                    role="menuitem">
                    <span class="nav-icon" [attr.aria-hidden]="true">{{ getIconSymbol(item.icon) }}</span>
                    @if (!isCollapsed()) {
                      <span class="nav-label">{{ item.label }}</span>
                    }
                  </a>
                  @if (!isCollapsed()) {
                    <button
                      class="submenu-toggle"
                      (click)="toggleSubmenu(item.path)"
                      [attr.aria-expanded]="isSubmenuExpanded(item.path)"
                      [attr.aria-label]="'Toggle ' + item.label + ' submenu'"
                      type="button">
                      <span class="toggle-icon">{{ isSubmenuExpanded(item.path) ? '▾' : '▸' }}</span>
                    </button>
                  }
                </div>
                
                <!-- Submenu -->
                @if (!isCollapsed() && isSubmenuExpanded(item.path)) {
                  <ul class="submenu" role="menu" [attr.aria-label]="item.label + ' submenu'">
                    @for (child of item.children; track child.path) {
                      <li class="submenu-item" role="none">
                        <a
                          class="submenu-link"
                          [routerLink]="[child.path]"
                          routerLinkActive="active"
                          [attr.aria-label]="child.ariaLabel || child.label"
                          role="menuitem">
                          <span class="nav-icon" [attr.aria-hidden]="true">{{ getIconSymbol(child.icon) }}</span>
                          <span class="nav-label">{{ child.label }}</span>
                        </a>
                      </li>
                    }
                  </ul>
                }
              } @else {
                <!-- Simple navigation item without children -->
                <a
                  class="nav-link"
                  [routerLink]="[item.path]"
                  routerLinkActive="active"
                  [attr.aria-label]="item.ariaLabel || item.label"
                  role="menuitem">
                  <span class="nav-icon" [attr.aria-hidden]="true">{{ getIconSymbol(item.icon) }}</span>
                  @if (!isCollapsed()) {
                    <span class="nav-label">{{ item.label }}</span>
                  }
                </a>
              }
            </li>
          }
        </ul>
      </nav>

      <!-- User Info Section (when authenticated) -->
      @if (authService.isAuthenticated()) {
        <div class="sidebar-footer">
          @if (!isCollapsed()) {
            <div class="user-info">
              <span class="user-icon">👤</span>
              <span class="user-name">{{ getCurrentUserDisplayName() }}</span>
            </div>
          }
        </div>
      }
    </aside>
  `,
  styles: [`
    /* 
     * Sidebar Component Styles
     * Supports light/dark theme variants through CSS custom properties
     */
    
    :host {
      display: block;
      height: 100%;
    }

    .sidebar {
      display: flex;
      flex-direction: column;
      width: 260px;
      height: 100vh;
      background-color: var(--sidebar-bg, #1e293b);
      color: var(--sidebar-text, #e2e8f0);
      transition: width 0.3s ease-in-out;
      overflow: hidden;
      position: fixed;
      left: 0;
      top: 0;
      z-index: 1000;
    }

    .sidebar.collapsed {
      width: 64px;
    }

    /* Dark theme variant */
    :host-context(.theme-dark) .sidebar {
      --sidebar-bg: #0f172a;
      --sidebar-text: #f1f5f9;
      --sidebar-hover: #334155;
      --sidebar-active: #3b82f6;
    }

    /* Light theme variant */
    :host-context(.theme-light) .sidebar {
      --sidebar-bg: #f8fafc;
      --sidebar-text: #1e293b;
      --sidebar-hover: #e2e8f0;
      --sidebar-active: #2563eb;
    }

    /* Header Section */
    .sidebar-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
      min-height: 64px;
    }

    .sidebar.collapsed .sidebar-header {
      justify-content: center;
    }

    .sidebar-logo {
      font-size: 1.25rem;
      font-weight: 700;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .toggle-btn {
      background: transparent;
      border: none;
      color: inherit;
      cursor: pointer;
      padding: 8px;
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background-color 0.2s ease;
    }

    .toggle-btn:hover {
      background-color: var(--sidebar-hover, rgba(255, 255, 255, 0.1));
    }

    .toggle-btn:focus-visible {
      outline: 2px solid var(--sidebar-active, #3b82f6);
      outline-offset: 2px;
    }

    .toggle-btn .icon {
      font-size: 1.25rem;
    }

    /* Navigation Section */
    .sidebar-nav {
      flex: 1;
      overflow-y: auto;
      overflow-x: hidden;
      padding: 8px 0;
    }

    .nav-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .nav-item {
      margin: 2px 8px;
    }

    .nav-parent {
      display: flex;
      align-items: center;
    }

    .nav-link,
    .submenu-link {
      display: flex;
      align-items: center;
      padding: 12px 16px;
      color: inherit;
      text-decoration: none;
      border-radius: 6px;
      transition: background-color 0.2s ease, color 0.2s ease;
      white-space: nowrap;
      overflow: hidden;
      flex: 1;
    }

    .nav-link:hover,
    .submenu-link:hover {
      background-color: var(--sidebar-hover, rgba(255, 255, 255, 0.1));
    }

    .nav-link:focus-visible,
    .submenu-link:focus-visible {
      outline: 2px solid var(--sidebar-active, #3b82f6);
      outline-offset: -2px;
    }

    .nav-link.active,
    .submenu-link.active {
      background-color: var(--sidebar-active, #3b82f6);
      color: white;
    }

    .sidebar.collapsed .nav-link {
      justify-content: center;
      padding: 12px;
    }

    .nav-icon {
      font-size: 1.25rem;
      min-width: 24px;
      text-align: center;
      margin-right: 12px;
    }

    .sidebar.collapsed .nav-icon {
      margin-right: 0;
    }

    .nav-label {
      font-size: 0.9375rem;
      font-weight: 500;
    }

    /* Submenu Toggle */
    .submenu-toggle {
      background: transparent;
      border: none;
      color: inherit;
      cursor: pointer;
      padding: 8px;
      margin-left: -8px;
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background-color 0.2s ease;
    }

    .submenu-toggle:hover {
      background-color: var(--sidebar-hover, rgba(255, 255, 255, 0.1));
    }

    .submenu-toggle:focus-visible {
      outline: 2px solid var(--sidebar-active, #3b82f6);
      outline-offset: 2px;
    }

    .toggle-icon {
      font-size: 0.75rem;
      opacity: 0.7;
    }

    /* Submenu */
    .submenu {
      list-style: none;
      margin: 0;
      padding: 0 0 0 24px;
    }

    .submenu-item {
      margin: 2px 0;
    }

    .submenu-link {
      padding: 10px 16px;
      font-size: 0.875rem;
    }

    .submenu-link .nav-icon {
      font-size: 1rem;
      min-width: 20px;
      margin-right: 10px;
    }

    /* Footer Section */
    .sidebar-footer {
      padding: 16px;
      border-top: 1px solid rgba(255, 255, 255, 0.1);
    }

    .user-info {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .user-icon {
      font-size: 1.5rem;
    }

    .user-name {
      font-size: 0.875rem;
      font-weight: 500;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* Scrollbar styling */
    .sidebar-nav::-webkit-scrollbar {
      width: 6px;
    }

    .sidebar-nav::-webkit-scrollbar-track {
      background: transparent;
    }

    .sidebar-nav::-webkit-scrollbar-thumb {
      background-color: rgba(255, 255, 255, 0.2);
      border-radius: 3px;
    }

    .sidebar-nav::-webkit-scrollbar-thumb:hover {
      background-color: rgba(255, 255, 255, 0.3);
    }

    /* Responsive adjustments */
    @media (max-width: 768px) {
      .sidebar {
        width: 64px;
      }

      .sidebar:not(.collapsed) {
        width: 260px;
        position: fixed;
        box-shadow: 4px 0 16px rgba(0, 0, 0, 0.2);
      }
    }
  `]
})
export class SidebarComponent {
  /**
   * Reference to AuthService for accessing authentication state.
   * Injected using Angular 19's functional inject() pattern.
   * 
   * MIGRATION NOTE: Replaces PortalSecurity.IsInRole checks from IconBar.ascx.vb
   * (lines 218-234) for determining navigation item visibility.
   */
  readonly authService = inject(AuthService);

  /**
   * Reference to Angular Router for programmatic navigation if needed.
   */
  private readonly router = inject(Router);

  /**
   * Signal controlling the collapsed/expanded state of the sidebar.
   * When collapsed, only icons are shown; when expanded, full labels are visible.
   * 
   * @public
   * @type {WritableSignal<boolean>}
   */
  readonly isCollapsed = signal<boolean>(false);

  /**
   * Signal tracking which submenus are currently expanded.
   * Stores the paths of expanded parent menu items.
   * 
   * @public
   * @type {WritableSignal<Set<string>>}
   */
  readonly expandedMenus = signal<Set<string>>(new Set<string>());

  /**
   * Computed signal that filters navigation items based on user permissions.
   * Admin-only items are only visible if the user has the Administrator role.
   * 
   * MIGRATION NOTE: Implements equivalent logic to IconBar.ascx.vb lines 218-234
   * where PortalSecurity.IsInRole(PortalSettings.AdministratorRoleName) checks
   * were used to enable/disable admin commands.
   * 
   * @public
   * @type {Signal<NavItem[]>}
   */
  readonly visibleNavItems = computed<NavItem[]>(() => {
    const user = this.authService.getCurrentUser();
    const isAdmin = this.isUserAdmin(user);
    
    // Filter navigation items based on admin status
    return NAVIGATION_ITEMS.filter(item => {
      // Show item if it's not admin-only, or if user is admin
      if (!item.adminOnly) {
        return true;
      }
      return isAdmin;
    });
  });

  /**
   * Toggles a submenu between expanded and collapsed states.
   * If the submenu is currently expanded, it will be collapsed and vice versa.
   * 
   * @param path - The path identifier of the parent menu item
   * 
   * @example
   * ```typescript
   * // In template
   * <button (click)="toggleSubmenu('/portals')">Toggle Portals</button>
   * ```
   * 
   * @public
   */
  toggleSubmenu(path: string): void {
    this.expandedMenus.update(menus => {
      const newMenus = new Set(menus);
      if (newMenus.has(path)) {
        newMenus.delete(path);
      } else {
        newMenus.add(path);
      }
      return newMenus;
    });
  }

  /**
   * Toggles the entire sidebar between collapsed and expanded states.
   * When collapsed, only icons are shown for a compact view.
   * When expanded, full navigation labels are visible.
   * 
   * Automatically collapses all submenus when sidebar is collapsed
   * to maintain a clean UI state.
   * 
   * @example
   * ```typescript
   * // In template
   * <button (click)="toggleSidebar()">Toggle Sidebar</button>
   * ```
   * 
   * @public
   */
  toggleSidebar(): void {
    this.isCollapsed.update(collapsed => {
      // If we're collapsing, also collapse all submenus
      if (!collapsed) {
        this.expandedMenus.set(new Set<string>());
      }
      return !collapsed;
    });
  }

  /**
   * Checks if a specific submenu is currently expanded.
   * Used in the template to determine submenu visibility.
   * 
   * @param path - The path identifier of the parent menu item
   * @returns true if the submenu is expanded, false otherwise
   * 
   * @public
   */
  isSubmenuExpanded(path: string): boolean {
    return this.expandedMenus().has(path);
  }

  /**
   * Maps icon names to Unicode symbols for display.
   * This provides a simple icon system without external dependencies.
   * 
   * @param iconName - The semantic name of the icon
   * @returns A Unicode character representing the icon
   * 
   * @private
   */
  getIconSymbol(iconName: string): string {
    const iconMap: Record<string, string> = {
      'dashboard': '📊',
      'business': '🏢',
      'widgets': '🧩',
      'people': '👥',
      'security': '🔐',
      'tab': '📄',
      'list': '📋',
      'add': '➕',
      'settings': '⚙️',
      'description': '📝',
      'person_add': '👤',
      'admin_panel_settings': '🛡️',
      'assignment_ind': '📎'
    };
    return iconMap[iconName] || '•';
  }

  /**
   * Gets the display name of the currently authenticated user.
   * Falls back to username if display name is not available.
   * 
   * @returns The user's display name or 'User' if not available
   * 
   * @private
   */
  getCurrentUserDisplayName(): string {
    const user = this.authService.getCurrentUser();
    if (user) {
      return user.displayName || user.username || 'User';
    }
    return 'Guest';
  }

  /**
   * Determines if the given user has administrator privileges.
   * Checks both the super user flag and the Administrators role membership.
   * 
   * MIGRATION NOTE: Implements equivalent logic to IconBar.ascx.vb line 218:
   * `If PortalSecurity.IsInRole(PortalSettings.AdministratorRoleName) = False Then`
   * 
   * @param user - The user object to check, or null if not authenticated
   * @returns true if the user has admin privileges, false otherwise
   * 
   * @private
   */
  private isUserAdmin(user: ReturnType<AuthService['getCurrentUser']>): boolean {
    if (!user) {
      return false;
    }
    
    // Super users always have admin access
    if (user.isSuperUser) {
      return true;
    }
    
    // Check if user has Administrator role
    if (user.roles && Array.isArray(user.roles)) {
      return user.roles.some(role => 
        role.toLowerCase() === ADMINISTRATOR_ROLE.toLowerCase() ||
        role.toLowerCase() === 'admin'
      );
    }
    
    return false;
  }
}
