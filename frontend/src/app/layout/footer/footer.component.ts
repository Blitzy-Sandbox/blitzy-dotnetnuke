/**
 * Footer Component for DNN Migration Portal
 * Angular 19 Standalone Component
 *
 * MIGRATION NOTE: This component replaces the legacy DNN footer implementation
 * which was composed of:
 * - FooterText property from PortalInfo.vb (Library/Components/Portal/PortalInfo.vb)
 * - Skin footer objects and container footer controls
 * - Various ascx-based footer UI components
 *
 * The original PortalInfo.FooterText was a simple string property:
 * ```vb
 * <XmlElement("footertext")> Public Property FooterText() As String
 *     Get
 *         Return _FooterText
 *     End Get
 *     Set(ByVal Value As String)
 *         _FooterText = Value
 *     End Set
 * End Property
 * ```
 *
 * This Angular component provides:
 * - Dynamic copyright year calculation
 * - Application version display from environment configuration
 * - Optional portal-specific footer text (replacing FooterText from PortalInfo)
 * - Configurable footer navigation links
 *
 * @see Library/Components/Portal/PortalInfo.vb - Legacy FooterText property
 * @see Website/admin/Containers/Title.ascx.vb - Legacy container pattern reference
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  type WritableSignal,
  type Signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { environment } from '../../../environments/environment';

/**
 * Interface representing a footer navigation link.
 * Supports both internal routes (using Angular Router) and external URLs.
 */
export interface FooterLink {
  /** Display text for the link */
  label: string;
  /** URL or route path for the link */
  url: string;
  /** Whether this is an external link (opens in new tab) */
  external: boolean;
  /** Optional aria-label for accessibility */
  ariaLabel?: string;
}

/**
 * FooterComponent - Application Shell Footer
 *
 * A standalone Angular 19 component that displays the application footer
 * with copyright notice, version information, and optional configurable content.
 *
 * Features:
 * - OnPush change detection for optimal performance
 * - Signals-based reactive state management
 * - Computed copyright year that updates automatically
 * - Portal-specific footer text support (maps to legacy FooterText from PortalInfo.vb)
 * - Configurable footer links array
 *
 * Usage:
 * ```html
 * <app-footer></app-footer>
 *
 * <!-- With custom footer text -->
 * <app-footer [footerText]="'Custom portal footer message'"></app-footer>
 *
 * <!-- With custom links -->
 * <app-footer [footerLinks]="customLinks"></app-footer>
 * ```
 *
 * @export
 * @class FooterComponent
 */
@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer">
      <div class="footer-content">
        <!-- Main footer section with copyright and version -->
        <div class="footer-main">
          <div class="footer-copyright">
            &copy; {{ currentYear() }} {{ appName() }}. All rights reserved.
          </div>
          <div class="footer-version">
            Version {{ version() }}
          </div>
        </div>

        <!-- Optional portal-specific footer text -->
        <!-- MIGRATION: This replaces the FooterText property from PortalInfo.vb -->
        @if (footerText()) {
          <div class="footer-text">
            {{ footerText() }}
          </div>
        }

        <!-- Optional footer navigation links -->
        @if (footerLinks().length > 0) {
          <nav class="footer-nav" aria-label="Footer navigation">
            <ul class="footer-links">
              @for (link of footerLinks(); track link.url) {
                <li class="footer-link-item">
                  @if (link.external) {
                    <a
                      [href]="link.url"
                      target="_blank"
                      rel="noopener noreferrer"
                      [attr.aria-label]="link.ariaLabel || link.label + ' (opens in new tab)'"
                      class="footer-link footer-link-external"
                    >
                      {{ link.label }}
                      <span class="external-link-icon" aria-hidden="true">↗</span>
                    </a>
                  } @else {
                    <a
                      [routerLink]="link.url"
                      [attr.aria-label]="link.ariaLabel || link.label"
                      class="footer-link"
                    >
                      {{ link.label }}
                    </a>
                  }
                </li>
              }
            </ul>
          </nav>
        }
      </div>
    </footer>
  `,
  styles: [`
    .app-footer {
      width: 100%;
      background-color: #f8f9fa;
      border-top: 1px solid #dee2e6;
      padding: 1rem 0;
      margin-top: auto;
    }

    .footer-content {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
    }

    .footer-main {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      align-items: center;
      gap: 1rem;
      width: 100%;
    }

    .footer-copyright {
      color: #6c757d;
      font-size: 0.875rem;
      text-align: center;
    }

    .footer-version {
      color: #adb5bd;
      font-size: 0.75rem;
      padding: 0.25rem 0.5rem;
      background-color: #e9ecef;
      border-radius: 0.25rem;
    }

    .footer-text {
      color: #495057;
      font-size: 0.875rem;
      text-align: center;
      max-width: 800px;
      line-height: 1.5;
    }

    .footer-nav {
      width: 100%;
    }

    .footer-links {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      align-items: center;
      gap: 1rem;
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .footer-link-item {
      margin: 0;
      padding: 0;
    }

    .footer-link {
      color: #0d6efd;
      text-decoration: none;
      font-size: 0.875rem;
      transition: color 0.2s ease-in-out;
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
    }

    .footer-link:hover,
    .footer-link:focus {
      color: #0a58ca;
      text-decoration: underline;
    }

    .footer-link:focus {
      outline: 2px solid #0d6efd;
      outline-offset: 2px;
      border-radius: 2px;
    }

    .external-link-icon {
      font-size: 0.75rem;
      opacity: 0.7;
    }

    /* Dark mode support */
    @media (prefers-color-scheme: dark) {
      .app-footer {
        background-color: #212529;
        border-top-color: #495057;
      }

      .footer-copyright {
        color: #adb5bd;
      }

      .footer-version {
        color: #6c757d;
        background-color: #343a40;
      }

      .footer-text {
        color: #dee2e6;
      }

      .footer-link {
        color: #6ea8fe;
      }

      .footer-link:hover,
      .footer-link:focus {
        color: #9ec5fe;
      }
    }

    /* Responsive design */
    @media (max-width: 768px) {
      .footer-main {
        flex-direction: column;
        gap: 0.5rem;
      }

      .footer-links {
        flex-direction: column;
        gap: 0.5rem;
      }
    }
  `]
})
export class FooterComponent {
  /**
   * Computed signal that returns the current year.
   * Used in the copyright notice to always display the current year.
   *
   * This is a computed signal rather than a simple value to support
   * potential future scenarios where the year might need to be recalculated
   * (e.g., if the app is kept open across year boundaries).
   *
   * @readonly
   * @type {Signal<number>}
   */
  readonly currentYear: Signal<number> = computed(() => new Date().getFullYear());

  /**
   * Signal holding the application version string.
   * Sourced from the environment configuration file.
   *
   * MIGRATION: This replaces the need for version information that was
   * previously scattered across various DNN components and the _Version
   * private member in PortalInfo.vb.
   *
   * @readonly
   * @type {Signal<string>}
   */
  readonly version: Signal<string> = computed(() => environment.version);

  /**
   * Signal holding the application name.
   * Sourced from the environment configuration file.
   *
   * Used in the copyright notice to display the application/portal name.
   * MIGRATION: This centralizes the application name that was previously
   * derived from PortalName property in PortalInfo.vb.
   *
   * @readonly
   * @type {Signal<string>}
   */
  readonly appName: Signal<string> = computed(() => environment.appName);

  /**
   * Writable signal for optional portal-specific footer text.
   *
   * MIGRATION: This directly maps to the FooterText property from
   * PortalInfo.vb in the legacy DNN application. The FooterText was
   * a simple string property that allowed administrators to configure
   * custom text to appear in the portal's footer area.
   *
   * The signal can be updated programmatically to display portal-specific
   * content, or left null/empty for no additional footer text.
   *
   * Example usage:
   * ```typescript
   * // Set custom footer text
   * this.footer.footerText.set('Welcome to our portal!');
   *
   * // Clear footer text
   * this.footer.footerText.set(null);
   * ```
   *
   * @type {WritableSignal<string | null>}
   */
  readonly footerText: WritableSignal<string | null> = signal<string | null>(null);

  /**
   * Writable signal for configurable footer navigation links.
   *
   * Provides an array of footer links that can be configured per portal.
   * Supports both internal application routes and external URLs.
   *
   * Default links can be set during initialization, and the array can
   * be updated dynamically based on portal configuration.
   *
   * Example usage:
   * ```typescript
   * // Set footer links
   * this.footer.footerLinks.set([
   *   { label: 'Privacy Policy', url: '/privacy', external: false },
   *   { label: 'Terms of Service', url: '/terms', external: false },
   *   { label: 'Help', url: 'https://help.example.com', external: true }
   * ]);
   * ```
   *
   * @type {WritableSignal<FooterLink[]>}
   */
  readonly footerLinks: WritableSignal<FooterLink[]> = signal<FooterLink[]>([]);

  /**
   * Creates an instance of FooterComponent.
   *
   * Initializes the component with default values from environment configuration.
   * The component uses signals for all state management, following Angular 19
   * best practices for reactive state.
   *
   * No constructor injection is used as all dependencies (environment config)
   * are imported directly at the module level. This follows the Angular 19
   * pattern of preferring inject() function for services while using direct
   * imports for configuration constants.
   */
  constructor() {
    // Component initialization is handled by signal default values.
    // No additional setup required as version and appName are computed
    // from the environment configuration.
    //
    // Portal-specific footer text and links can be set by parent components
    // or services after the component is instantiated:
    //
    // Example in parent component:
    // @ViewChild(FooterComponent) footer!: FooterComponent;
    // ngAfterViewInit() {
    //   this.footer.footerText.set('Custom portal footer');
    //   this.footer.footerLinks.set([...links]);
    // }
  }

  /**
   * Sets the footer text to a new value.
   * Convenience method for updating the footerText signal.
   *
   * @param text - The new footer text, or null to clear
   */
  setFooterText(text: string | null): void {
    this.footerText.set(text);
  }

  /**
   * Sets the footer links to a new array.
   * Convenience method for updating the footerLinks signal.
   *
   * @param links - The new array of footer links
   */
  setFooterLinks(links: FooterLink[]): void {
    this.footerLinks.set(links);
  }

  /**
   * Adds a single link to the footer links array.
   * Convenience method for appending to the existing links.
   *
   * @param link - The footer link to add
   */
  addFooterLink(link: FooterLink): void {
    this.footerLinks.update(currentLinks => [...currentLinks, link]);
  }

  /**
   * Removes a link from the footer links array by URL.
   * Convenience method for filtering out a specific link.
   *
   * @param url - The URL of the link to remove
   */
  removeFooterLink(url: string): void {
    this.footerLinks.update(currentLinks =>
      currentLinks.filter(link => link.url !== url)
    );
  }

  /**
   * Clears all footer links.
   * Convenience method for resetting the links array.
   */
  clearFooterLinks(): void {
    this.footerLinks.set([]);
  }
}
