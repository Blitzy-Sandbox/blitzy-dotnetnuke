/**
 * @fileoverview Angular 19 Root Application Component
 * @description Root standalone component that bootstraps the DNN Migration application.
 * Defines the application shell layout including header, collapsible sidebar,
 * main content area with router-outlet, and footer.
 *
 * MIGRATION NOTE: This component replaces the legacy DNN Default.aspx WebForms
 * master layout structure. The original Default.aspx.vb handled:
 * - Portal settings management and context resolution (lines 90-215)
 * - Skin/container loading (LoadSkin method, lines 217-243)
 * - Page title, description, keywords meta management (lines 152-207)
 * - WebForms lifecycle initialization
 *
 * This Angular SPA shell provides:
 * - Clean component-based layout architecture
 * - Reactive sidebar collapse/expand state via signals
 * - Router-outlet for lazy-loaded feature modules
 * - OnPush change detection for optimal performance
 *
 * Key Angular 19 features used:
 * - standalone: true (default in Angular 19)
 * - signal() for reactive state management (isSidebarCollapsed)
 * - @if control flow syntax for conditional rendering
 * - ChangeDetectionStrategy.OnPush for performance
 *
 * @module app.component
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

// Layout components from depends_on_files
import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { FooterComponent } from './layout/footer/footer.component';

/**
 * Root application component serving as the primary entry point for the component tree.
 *
 * This standalone component implements the main application shell layout:
 * - Header component at the top for branding, user info, and logout
 * - Collapsible sidebar on the left for navigation
 * - Main content area with router-outlet for feature components
 * - Footer at the bottom with copyright and version info
 *
 * MIGRATION NOTE: Replaces DNN Default.aspx page structure that:
 * - Loaded skins via LoadSkin method (Default.aspx.vb lines 217-243)
 * - Resolved portal context via PortalSettings (lines 99, 153, 165)
 * - Managed WebForms lifecycle (Page_Init, Page_Load events)
 * - Handled skin/container module placement
 *
 * The layout uses CSS Grid/Flexbox for responsive behavior:
 * ```
 * ┌──────────────────────────────────────────────┐
 * │                   Header                      │
 * ├─────────────┬────────────────────────────────┤
 * │             │                                 │
 * │   Sidebar   │        Main Content            │
 * │  (nav menu) │       (router-outlet)          │
 * │             │                                 │
 * ├─────────────┴────────────────────────────────┤
 * │                   Footer                      │
 * └──────────────────────────────────────────────┘
 * ```
 *
 * @example
 * ```typescript
 * // In main.ts
 * bootstrapApplication(AppComponent, appConfig);
 * ```
 *
 * @export
 * @class AppComponent
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    HeaderComponent,
    SidebarComponent,
    FooterComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="app-container" [class.sidebar-collapsed]="isSidebarCollapsed()">
      <!-- Application Header -->
      <!-- MIGRATION: Replaces DNN skin header objects and branding from Default.aspx -->
      <app-header />

      <!-- Main Layout Wrapper -->
      <div class="app-layout">
        <!-- Collapsible Sidebar Navigation -->
        <!-- MIGRATION: Replaces DNN control panel and navigation skins -->
        <aside 
          class="app-sidebar"
          [class.collapsed]="isSidebarCollapsed()"
          role="navigation"
          aria-label="Main navigation"
        >
          <!-- Sidebar Toggle Button -->
          <button
            type="button"
            class="sidebar-toggle"
            (click)="onSidebarToggle()"
            [attr.aria-expanded]="!isSidebarCollapsed()"
            aria-controls="sidebar-navigation"
            aria-label="Toggle sidebar navigation"
          >
            @if (isSidebarCollapsed()) {
              <!-- Expand Icon (Menu) -->
              <svg 
                class="toggle-icon"
                width="24" 
                height="24" 
                viewBox="0 0 24 24" 
                fill="none" 
                stroke="currentColor" 
                stroke-width="2" 
                stroke-linecap="round" 
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <line x1="3" y1="12" x2="21" y2="12" />
                <line x1="3" y1="6" x2="21" y2="6" />
                <line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            } @else {
              <!-- Collapse Icon (Chevron Left) -->
              <svg 
                class="toggle-icon"
                width="24" 
                height="24" 
                viewBox="0 0 24 24" 
                fill="none" 
                stroke="currentColor" 
                stroke-width="2" 
                stroke-linecap="round" 
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <polyline points="15 18 9 12 15 6" />
              </svg>
            }
          </button>

          <!-- Sidebar Content -->
          <div id="sidebar-navigation" class="sidebar-content">
            <app-sidebar />
          </div>
        </aside>

        <!-- Main Content Area -->
        <!-- MIGRATION: Replaces DNN's ContentPane placeholder where modules were injected -->
        <main class="app-main" role="main">
          <div class="main-content">
            <!-- Router Outlet for Feature Components -->
            <!-- Feature modules are lazy-loaded here based on route configuration -->
            <router-outlet />
          </div>
        </main>
      </div>

      <!-- Application Footer -->
      <!-- MIGRATION: Replaces DNN skin footer objects and PortalInfo.FooterText -->
      <app-footer />
    </div>
  `,
  styles: [`
    /**
     * Root Application Styles
     * 
     * Implements a responsive layout with:
     * - Fixed header at top
     * - Collapsible sidebar on left
     * - Flexible main content area
     * - Fixed footer at bottom
     * 
     * CSS Custom Properties for theming:
     * --header-height: Height of the header component
     * --footer-height: Height of the footer component
     * --sidebar-width: Width of expanded sidebar
     * --sidebar-collapsed-width: Width of collapsed sidebar
     */
    
    :host {
      display: block;
      min-height: 100vh;
      
      /* CSS Custom Properties for layout dimensions */
      --header-height: 64px;
      --footer-height: 56px;
      --sidebar-width: 260px;
      --sidebar-collapsed-width: 64px;
      --transition-speed: 0.3s;
      
      /* Color scheme */
      --bg-primary: #f5f7fa;
      --bg-sidebar: #1e3a5f;
      --bg-content: #ffffff;
      --border-color: #e1e5eb;
      --shadow-color: rgba(0, 0, 0, 0.08);
    }

    /* Main Application Container */
    .app-container {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
      background-color: var(--bg-primary);
    }

    /* Layout Wrapper (Sidebar + Main Content) */
    .app-layout {
      display: flex;
      flex: 1;
      min-height: calc(100vh - var(--header-height) - var(--footer-height));
    }

    /* Sidebar Styles */
    .app-sidebar {
      position: relative;
      width: var(--sidebar-width);
      min-height: 100%;
      background: var(--bg-sidebar);
      color: #ffffff;
      transition: width var(--transition-speed) ease-in-out;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      box-shadow: 2px 0 8px var(--shadow-color);
    }

    .app-sidebar.collapsed {
      width: var(--sidebar-collapsed-width);
    }

    /* Sidebar Toggle Button */
    .sidebar-toggle {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      margin: 12px auto;
      padding: 0;
      background: rgba(255, 255, 255, 0.1);
      border: none;
      border-radius: 8px;
      color: #ffffff;
      cursor: pointer;
      transition: background-color 0.2s ease, transform 0.2s ease;
    }

    .sidebar-toggle:hover {
      background: rgba(255, 255, 255, 0.2);
    }

    .sidebar-toggle:focus-visible {
      outline: 2px solid #4a9eff;
      outline-offset: 2px;
    }

    .sidebar-toggle:active {
      transform: scale(0.95);
    }

    .toggle-icon {
      flex-shrink: 0;
    }

    /* Sidebar Content */
    .sidebar-content {
      flex: 1;
      overflow-y: auto;
      overflow-x: hidden;
    }

    /* Collapsed sidebar hides content but keeps icons visible */
    .app-sidebar.collapsed .sidebar-content {
      /* Let the SidebarComponent handle its own collapsed state */
    }

    /* Main Content Area */
    .app-main {
      flex: 1;
      min-width: 0; /* Prevents flex item from overflowing */
      display: flex;
      flex-direction: column;
      background-color: var(--bg-primary);
    }

    .main-content {
      flex: 1;
      padding: 24px;
      max-width: 100%;
      overflow-x: auto;
    }

    /* Content card styling for nested components */
    .main-content :host ::ng-deep > * {
      background: var(--bg-content);
      border-radius: 8px;
      box-shadow: 0 1px 4px var(--shadow-color);
    }

    /* Responsive Adjustments */
    @media (max-width: 992px) {
      :host {
        --sidebar-width: 220px;
      }
    }

    @media (max-width: 768px) {
      :host {
        --sidebar-width: 100%;
        --sidebar-collapsed-width: 0;
      }

      .app-layout {
        flex-direction: column;
      }

      .app-sidebar {
        width: 100%;
        min-height: auto;
        order: -1;
      }

      .app-sidebar.collapsed {
        width: 0;
        min-height: 0;
        overflow: hidden;
      }

      .sidebar-toggle {
        position: fixed;
        top: 72px;
        left: 8px;
        z-index: 100;
        background: var(--bg-sidebar);
        box-shadow: 0 2px 8px var(--shadow-color);
      }

      .app-sidebar.collapsed + .app-main .sidebar-toggle {
        left: 8px;
      }

      .main-content {
        padding: 16px;
      }
    }

    @media (max-width: 480px) {
      .main-content {
        padding: 12px;
      }
    }

    /* Scrollbar Styling for Sidebar */
    .sidebar-content::-webkit-scrollbar {
      width: 6px;
    }

    .sidebar-content::-webkit-scrollbar-track {
      background: transparent;
    }

    .sidebar-content::-webkit-scrollbar-thumb {
      background: rgba(255, 255, 255, 0.3);
      border-radius: 3px;
    }

    .sidebar-content::-webkit-scrollbar-thumb:hover {
      background: rgba(255, 255, 255, 0.5);
    }

    /* Print Styles */
    @media print {
      .app-sidebar,
      .sidebar-toggle,
      app-header,
      app-footer {
        display: none !important;
      }

      .app-main {
        width: 100% !important;
      }

      .main-content {
        padding: 0 !important;
      }
    }

    /* Reduced Motion Preference */
    @media (prefers-reduced-motion: reduce) {
      :host {
        --transition-speed: 0s;
      }

      .sidebar-toggle {
        transition: none;
      }
    }

    /* High Contrast Mode Support */
    @media (prefers-contrast: high) {
      .app-sidebar {
        border-right: 2px solid #ffffff;
      }

      .sidebar-toggle {
        border: 2px solid #ffffff;
      }
    }
  `]
})
export class AppComponent {
  /**
   * Application title used for display and metadata.
   * MIGRATION: Derived from PortalSettings.PortalName in Default.aspx.vb (line 153)
   */
  readonly title = 'DNN Migration';

  /**
   * Signal controlling the sidebar collapsed/expanded state.
   * 
   * Uses Angular 19 signals for reactive state management:
   * - false = sidebar expanded (default)
   * - true = sidebar collapsed
   * 
   * MIGRATION: Replaces DNN's JavaScript-based panel collapse
   * functionality from the control panel skins.
   */
  readonly isSidebarCollapsed = signal<boolean>(false);

  /**
   * Toggles the sidebar collapsed state.
   * 
   * This method is called by the sidebar toggle button click handler.
   * It inverts the current collapsed state using the signal's update method.
   * 
   * MIGRATION: Replaces DNN control panel JavaScript toggle functions
   * that managed the visibility of the admin sidebar.
   * 
   * @public
   * @returns {void}
   * 
   * @example
   * ```html
   * <button (click)="onSidebarToggle()">Toggle Sidebar</button>
   * ```
   */
  onSidebarToggle(): void {
    this.isSidebarCollapsed.update(collapsed => !collapsed);
  }
}
