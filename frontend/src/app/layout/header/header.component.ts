/**
 * @fileoverview Angular 19 Header Component for DNN Migration Application Shell
 * @description Implements the top navigation bar with application branding/logo,
 * user profile display, logout functionality, and portal context indicator.
 *
 * MIGRATION NOTE: This component replaces multiple DNN skin/container objects:
 * - Title.ascx.vb: lblTitle rendering (line 88) and permission checks (lines 51-58)
 * - Icon.ascx.vb: imgIcon branding display (lines 93-108)
 * - IconBar.ascx.vb: User context awareness and portal context (lines 46-93, line 59)
 *
 * Key Angular 19 features used:
 * - standalone: true (default in Angular 19)
 * - inject() function for dependency injection
 * - signal() and computed() for reactive state management
 * - @if control flow syntax for conditional rendering
 * - ChangeDetectionStrategy.OnPush for optimal performance
 *
 * @module layout/header/header.component
 */

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  OnInit,
  OnDestroy,
  WritableSignal,
  Signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models/user.model';

/**
 * Header component providing the application's top navigation bar.
 *
 * This standalone component implements:
 * - Application branding with logo and title
 * - Current user profile display (displayName, username)
 * - Logout button with navigation to login page
 * - Portal context indicator showing current portal name
 *
 * MIGRATION NOTE: Replaces DNN container skin objects:
 * - Title.ascx.vb lblTitle.Text rendering (line 88) with reactive user display
 * - Icon.ascx.vb imgIcon.ImageUrl branding (lines 93-108) with static logo
 * - IconBar.ascx.vb PortalSettings.PortalId context (line 59) with portalName signal
 * - CanEditModule() permission checks (Title.ascx.vb lines 51-58) with isAuthenticated signal
 *
 * @example
 * ```html
 * <!-- In app.component.ts template -->
 * <app-header />
 * <router-outlet />
 * <app-footer />
 * ```
 *
 * @class HeaderComponent
 * @implements {OnInit}
 * @implements {OnDestroy}
 */
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="app-header">
      <!-- Branding Section: Logo and Application Title -->
      <!-- MIGRATION: Replaces Icon.ascx.vb imgIcon branding (lines 93-108) -->
      <div class="header-brand">
        <a routerLink="/" class="brand-link" aria-label="Navigate to home">
          <svg
            class="brand-logo"
            width="36"
            height="36"
            viewBox="0 0 36 36"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            aria-hidden="true"
          >
            <rect width="36" height="36" rx="8" fill="currentColor" class="logo-bg" />
            <path
              d="M10 18C10 13.5817 13.5817 10 18 10C22.4183 10 26 13.5817 26 18C26 22.4183 22.4183 26 18 26"
              stroke="white"
              stroke-width="2.5"
              stroke-linecap="round"
            />
            <path
              d="M18 14V18L21 21"
              stroke="white"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
          <span class="brand-title">DNN Migration</span>
        </a>
      </div>

      <!-- Portal Context Indicator -->
      <!-- MIGRATION: Replaces IconBar.ascx.vb PortalSettings.PortalId context (lines 59, 93) -->
      @if (portalName()) {
        <div class="portal-context">
          <span class="portal-badge">
            <svg
              class="portal-icon"
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              aria-hidden="true"
            >
              <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
              <polyline points="9 22 9 12 15 12 15 22" />
            </svg>
            {{ portalName() }}
          </span>
        </div>
      }

      <!-- Spacer for flexbox layout -->
      <div class="header-spacer"></div>

      <!-- User Profile Section -->
      <!-- MIGRATION: Replaces Title.ascx.vb CanEditModule() checks (lines 51-58) -->
      <!-- and lblTitle rendering of ModuleConfiguration.ModuleTitle (lines 79-88) -->
      @if (isAuthenticated()) {
        <div class="user-section">
          <!-- User Profile Display -->
          @if (user()) {
            <div class="user-profile">
              <div class="user-avatar" [attr.aria-label]="'User: ' + user()!.displayName">
                {{ getUserInitials() }}
              </div>
              <div class="user-info">
                <span class="user-display-name">{{ user()!.displayName }}</span>
                <span class="user-username">&#64;{{ user()!.username }}</span>
              </div>
            </div>
          }

          <!-- Logout Button -->
          <!-- MIGRATION: Replaces DNN's FormsAuthentication.SignOut patterns -->
          <button
            type="button"
            class="logout-button"
            (click)="onLogout()"
            aria-label="Sign out of your account"
          >
            <svg
              class="logout-icon"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
              <polyline points="16 17 21 12 16 7" />
              <line x1="21" y1="12" x2="9" y2="12" />
            </svg>
            <span class="logout-text">Sign Out</span>
          </button>
        </div>
      } @else {
        <!-- Login Link for Non-Authenticated Users -->
        <div class="auth-section">
          <a routerLink="/auth/login" class="login-link">
            <svg
              class="login-icon"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
              <polyline points="10 17 15 12 10 7" />
              <line x1="15" y1="12" x2="3" y2="12" />
            </svg>
            <span>Sign In</span>
          </a>
        </div>
      }
    </header>
  `,
  styles: [`
    /* Header Container */
    .app-header {
      display: flex;
      align-items: center;
      height: 64px;
      padding: 0 24px;
      background: linear-gradient(135deg, #1e3a5f 0%, #2d5a87 100%);
      color: #ffffff;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
      position: sticky;
      top: 0;
      z-index: 1000;
    }

    /* Branding Section */
    .header-brand {
      display: flex;
      align-items: center;
    }

    .brand-link {
      display: flex;
      align-items: center;
      gap: 12px;
      text-decoration: none;
      color: inherit;
      transition: opacity 0.2s ease;
    }

    .brand-link:hover {
      opacity: 0.9;
    }

    .brand-link:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: 4px;
      border-radius: 4px;
    }

    .brand-logo {
      flex-shrink: 0;
      color: #4a9eff;
    }

    .logo-bg {
      opacity: 0.9;
    }

    .brand-title {
      font-size: 1.25rem;
      font-weight: 600;
      letter-spacing: -0.02em;
      white-space: nowrap;
    }

    /* Portal Context Badge */
    .portal-context {
      margin-left: 24px;
      padding-left: 24px;
      border-left: 1px solid rgba(255, 255, 255, 0.2);
    }

    .portal-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 6px 12px;
      background: rgba(255, 255, 255, 0.1);
      border-radius: 16px;
      font-size: 0.875rem;
      font-weight: 500;
    }

    .portal-icon {
      flex-shrink: 0;
      opacity: 0.8;
    }

    /* Spacer for Flexbox */
    .header-spacer {
      flex: 1;
    }

    /* User Section */
    .user-section {
      display: flex;
      align-items: center;
      gap: 16px;
    }

    /* User Profile Display */
    .user-profile {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 6px 12px;
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.05);
    }

    .user-avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: linear-gradient(135deg, #4a9eff 0%, #2d7dd2 100%);
      font-size: 0.875rem;
      font-weight: 600;
      text-transform: uppercase;
      flex-shrink: 0;
    }

    .user-info {
      display: flex;
      flex-direction: column;
      line-height: 1.3;
    }

    .user-display-name {
      font-size: 0.9375rem;
      font-weight: 500;
      white-space: nowrap;
    }

    .user-username {
      font-size: 0.75rem;
      opacity: 0.7;
      white-space: nowrap;
    }

    /* Logout Button */
    .logout-button {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 16px;
      background: rgba(255, 255, 255, 0.1);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 6px;
      color: #ffffff;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .logout-button:hover {
      background: rgba(255, 255, 255, 0.2);
      border-color: rgba(255, 255, 255, 0.3);
    }

    .logout-button:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: 2px;
    }

    .logout-button:active {
      transform: translateY(1px);
    }

    .logout-icon {
      flex-shrink: 0;
    }

    /* Auth Section (Non-Authenticated) */
    .auth-section {
      display: flex;
      align-items: center;
    }

    .login-link {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 8px 20px;
      background: #4a9eff;
      border-radius: 6px;
      color: #ffffff;
      font-size: 0.875rem;
      font-weight: 500;
      text-decoration: none;
      transition: all 0.2s ease;
    }

    .login-link:hover {
      background: #3d8ae6;
    }

    .login-link:focus-visible {
      outline: 2px solid #ffffff;
      outline-offset: 2px;
    }

    .login-icon {
      flex-shrink: 0;
    }

    /* Responsive Adjustments */
    @media (max-width: 768px) {
      .app-header {
        padding: 0 16px;
        height: 56px;
      }

      .brand-title {
        display: none;
      }

      .portal-context {
        display: none;
      }

      .user-info {
        display: none;
      }

      .logout-text {
        display: none;
      }

      .logout-button {
        padding: 8px;
      }
    }

    /* Reduced Motion Preference */
    @media (prefers-reduced-motion: reduce) {
      .brand-link,
      .logout-button,
      .login-link {
        transition: none;
      }

      .logout-button:active {
        transform: none;
      }
    }

    /* High Contrast Mode Support */
    @media (forced-colors: active) {
      .app-header {
        background: Canvas;
        border-bottom: 1px solid CanvasText;
      }

      .logout-button,
      .login-link {
        border: 1px solid CanvasText;
      }
    }
  `]
})
export class HeaderComponent implements OnInit, OnDestroy {
  /**
   * AuthService instance injected using Angular 19's inject() function.
   * Provides authentication state and methods.
   *
   * MIGRATION NOTE: Replaces DNN's Title.ascx.vb PortalSettings and
   * permission checks (CanEditModule method, lines 51-58).
   *
   * @private
   * @readonly
   */
  private readonly authService = inject(AuthService);

  /**
   * Router instance injected using Angular 19's inject() function.
   * Used for programmatic navigation after logout.
   *
   * MIGRATION NOTE: Replaces DNN's Response.Redirect patterns
   * used after FormsAuthentication.SignOut.
   *
   * @private
   * @readonly
   */
  private readonly router = inject(Router);

  /**
   * Subscription container for cleanup on component destruction.
   * @private
   */
  private userSubscription: Subscription | null = null;

  /**
   * Writable signal holding the current portal name for display.
   * Initialized as empty string and can be updated based on context.
   *
   * MIGRATION NOTE: Derived from IconBar.ascx.vb PortalSettings.PortalId
   * (line 59) and portal name resolution (line 93). In the legacy system,
   * portal context was determined by the current request URL and PortalSettings.
   *
   * @public
   * @type {WritableSignal<string>}
   *
   * @example
   * ```typescript
   * // Setting portal name
   * this.portalName.set('Default Portal');
   *
   * // In template
   * @if (portalName()) {
   *   <span>{{ portalName() }}</span>
   * }
   * ```
   */
  readonly portalName: WritableSignal<string> = signal<string>('');

  /**
   * Computed signal providing the current authenticated user.
   * Returns null when no user is authenticated.
   *
   * MIGRATION NOTE: Replaces Title.ascx.vb's objPortalModule.ModuleConfiguration.ModuleTitle
   * access pattern (lines 79-88) with reactive user information from AuthService.
   * The legacy pattern retrieved module context from the container hierarchy;
   * the new pattern uses centralized authentication state.
   *
   * @public
   * @type {Signal<User | null>}
   *
   * @example
   * ```typescript
   * // In template with signal syntax
   * @if (user()) {
   *   <span>{{ user()!.displayName }}</span>
   * }
   * ```
   */
  readonly user: Signal<User | null> = computed(() => this.authService.getCurrentUser());

  /**
   * Computed signal indicating whether a user is currently authenticated.
   * Used for conditional rendering of user profile section.
   *
   * MIGRATION NOTE: Replaces Title.ascx.vb CanEditModule() permission checks
   * (lines 51-58) which verified PortalSettings.UserMode, IsInRoles for
   * AdministratorRoleName, and ActiveTab.AdministratorRoles. The new pattern
   * uses a simple authentication check; role-based authorization is handled
   * at the route level using auth.guard.ts.
   *
   * @public
   * @type {Signal<boolean>}
   *
   * @example
   * ```typescript
   * // In template with @if control flow
   * @if (isAuthenticated()) {
   *   <app-user-menu />
   * } @else {
   *   <app-login-button />
   * }
   * ```
   */
  readonly isAuthenticated: Signal<boolean> = computed(() => this.authService.isAuthenticated());

  /**
   * Lifecycle hook called after component initialization.
   * Sets up subscriptions and initializes portal context.
   */
  ngOnInit(): void {
    // Subscribe to user changes to update portal context
    this.userSubscription = this.authService.currentUser$.subscribe((user) => {
      if (user) {
        // Derive portal name from user's portal association
        // In a full implementation, this would fetch portal details from a service
        this.portalName.set(this.derivePortalName(user));
      } else {
        this.portalName.set('');
      }
    });
  }

  /**
   * Lifecycle hook called before component destruction.
   * Cleans up subscriptions to prevent memory leaks.
   */
  ngOnDestroy(): void {
    if (this.userSubscription) {
      this.userSubscription.unsubscribe();
      this.userSubscription = null;
    }
  }

  /**
   * Handles user logout action.
   * Calls AuthService.logout() which clears tokens and navigates to login page.
   *
   * MIGRATION NOTE: Replaces DNN's FormsAuthentication.SignOut patterns
   * from PortalSecurity.vb (lines 77-95) which:
   * - Called FormsAuthentication.SignOut()
   * - Cleared language, authentication type, and portal cookies
   * - Redirected to the home page or specified URL
   *
   * The new implementation delegates to AuthService which handles:
   * - Calling POST /api/auth/logout to invalidate server token
   * - Clearing localStorage tokens
   * - Resetting authentication state
   * - Navigating to /auth/login
   *
   * @public
   * @returns {void}
   *
   * @example
   * ```html
   * <button (click)="onLogout()">Sign Out</button>
   * ```
   */
  onLogout(): void {
    this.authService.logout();
  }

  /**
   * Generates user initials from the current user's display name.
   * Used for the avatar display when no profile image is available.
   *
   * @returns {string} Up to two uppercase initials from the user's display name
   *
   * @example
   * ```typescript
   * // For user with displayName "John Doe" → "JD"
   * // For user with displayName "Admin" → "AD"
   * ```
   */
  getUserInitials(): string {
    const currentUser = this.user();
    if (!currentUser || !currentUser.displayName) {
      return '?';
    }

    const names = currentUser.displayName.trim().split(/\s+/);

    if (names.length === 0 || names[0].length === 0) {
      return '?';
    }

    if (names.length === 1) {
      // Single name: take first two characters
      return names[0].substring(0, 2).toUpperCase();
    }

    // Multiple names: take first letter of first and last name
    const firstInitial = names[0].charAt(0);
    const lastInitial = names[names.length - 1].charAt(0);

    return (firstInitial + lastInitial).toUpperCase();
  }

  /**
   * Derives the portal name from the user's portal association.
   * In a complete implementation, this would fetch portal details
   * from a dedicated PortalService.
   *
   * MIGRATION NOTE: In DNN, portal name was retrieved from PortalSettings
   * which was populated based on the current request's portal alias.
   * The IconBar.ascx.vb accessed this via PortalSettings.PortalId (line 59)
   * and other portal properties.
   *
   * @private
   * @param {User} user - The current authenticated user
   * @returns {string} The portal name or a default value
   */
  private derivePortalName(user: User): string {
    // Super users (host users) don't belong to a specific portal
    if (user.isSuperUser) {
      return 'Host Portal';
    }

    // Normal users have a portal association
    // In a full implementation, we would call PortalService.getPortal(user.portalId)
    // For now, we use a descriptive default based on portal ID
    if (user.portalId >= 0) {
      return `Portal ${user.portalId}`;
    }

    return '';
  }
}
