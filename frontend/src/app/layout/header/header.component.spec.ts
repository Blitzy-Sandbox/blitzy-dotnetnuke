/**
 * @fileoverview Unit tests for HeaderComponent
 * @description Comprehensive test suite for Angular 19 standalone HeaderComponent
 * verifying branding display, user profile rendering, logout functionality,
 * portal context indicator, and navigation behavior.
 *
 * MIGRATION NOTE: Tests verify the header replaces DNN's Title.ascx.vb title display
 * (line 88 lblTitle.Text) and Icon.ascx.vb branding (lines 93-108 imgIcon.ImageUrl)
 * with Angular reactive patterns using signals and computed properties.
 *
 * Test Coverage:
 * - Component creation and standalone configuration
 * - Application branding/logo display
 * - User profile display based on authentication state
 * - Logout functionality with AuthService delegation
 * - Router navigation verification on logout
 * - Portal context indicator display
 * - Reactive state management (signals)
 * - Accessibility compliance
 * - Lifecycle hooks (OnInit, OnDestroy)
 *
 * @module layout/header/header.component.spec
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { BehaviorSubject, of } from 'rxjs';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models/user.model';

/**
 * Creates a mock User object for testing purposes.
 * Provides complete User interface implementation with sensible test defaults.
 *
 * MIGRATION NOTE: Mirrors the UserInfo.vb entity structure (lines 47-58)
 * including userId, username, displayName, email, portalId, and isSuperUser.
 *
 * @param overrides - Optional partial User properties to override defaults
 * @returns Complete User object with test data
 *
 * @example
 * ```typescript
 * const superUser = createMockUser({ isSuperUser: true, portalId: -1 });
 * const regularUser = createMockUser({ displayName: 'Jane Doe', portalId: 5 });
 * ```
 */
function createMockUser(overrides: Partial<User> = {}): User {
  return {
    userId: 1,
    username: 'testuser',
    displayName: 'Test User',
    firstName: 'Test',
    lastName: 'User',
    email: 'test@example.com',
    portalId: 0,
    isSuperUser: false,
    roles: ['Registered Users'],
    ...overrides
  };
}

/**
 * Mock AuthService class for testing HeaderComponent in isolation.
 *
 * Provides controllable authentication state through test helper methods
 * while exposing spies for verification of method calls (logout, getCurrentUser).
 *
 * MIGRATION NOTE: This mock replaces DNN's authentication context that was
 * previously accessed through HttpContext.Current.User and PortalSettings.
 *
 * @class MockAuthService
 */
class MockAuthService {
  /**
   * BehaviorSubject holding the current user state for testing.
   * Initialized to null (not authenticated).
   */
  private currentUserSubject = new BehaviorSubject<User | null>(null);

  /**
   * Internal signal for isAuthenticated state.
   * Synchronized with currentUserSubject for reactive testing.
   */
  private isAuthenticatedSignal = signal(false);

  /**
   * Observable stream of current user changes.
   * HeaderComponent subscribes to this in ngOnInit for portal context updates.
   */
  currentUser$ = this.currentUserSubject.asObservable();

  /**
   * Returns the current authentication state as a callable signal.
   * Used by HeaderComponent.isAuthenticated computed signal.
   */
  isAuthenticated = (): boolean => this.isAuthenticatedSignal();

  /**
   * Jasmine spy for getCurrentUser method.
   * Returns the current user from the BehaviorSubject.
   */
  getCurrentUser = jasmine.createSpy('getCurrentUser').and.callFake(() => {
    return this.currentUserSubject.value;
  });

  /**
   * Jasmine spy for logout method.
   * Tracks calls to verify HeaderComponent.onLogout() behavior.
   */
  logout = jasmine.createSpy('logout');

  /**
   * Test helper method to set the authenticated user state.
   * Synchronizes both BehaviorSubject and signal for complete state update.
   *
   * @param user - User object to set, or null for unauthenticated state
   */
  setUser(user: User | null): void {
    this.currentUserSubject.next(user);
    this.isAuthenticatedSignal.set(user !== null);
  }

  /**
   * Test helper to get the current mock user.
   * Used for verification in tests.
   */
  getCurrentMockUser(): User | null {
    return this.currentUserSubject.value;
  }
}

/**
 * HeaderComponent Test Suite
 *
 * Comprehensive tests for the application header component verifying:
 * - Component instantiation and standalone configuration
 * - Application branding/logo display (replaces Icon.ascx.vb)
 * - User profile rendering based on authentication state (replaces Title.ascx.vb)
 * - Logout button functionality and AuthService delegation
 * - Router navigation on logout
 * - Portal context indicator (replaces IconBar.ascx.vb PortalSettings.PortalId)
 * - Signal-based reactive state management
 * - Accessibility attributes and semantic HTML
 * - Lifecycle hook implementation (OnInit, OnDestroy)
 */
describe('HeaderComponent', () => {
  let component: HeaderComponent;
  let fixture: ComponentFixture<HeaderComponent>;
  let mockAuthService: MockAuthService;
  let router: Router;

  /**
   * Test module configuration before each test.
   * Configures TestBed with standalone HeaderComponent and mocked providers.
   */
  beforeEach(async () => {
    mockAuthService = new MockAuthService();

    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        provideRouter([
          { path: 'auth/login', component: HeaderComponent } // Dummy route for navigation testing
        ])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);

    // Spy on router.navigate for navigation verification
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
  });

  /**
   * Cleanup after each test to prevent memory leaks.
   */
  afterEach(() => {
    fixture?.destroy();
  });

  // ============================================================================
  // Component Creation Tests
  // ============================================================================

  describe('Component Creation', () => {
    it('should create the component', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
    });

    it('should be a standalone component', () => {
      // Access component metadata to verify standalone configuration
      const componentDef = (HeaderComponent as any).ɵcmp;
      expect(componentDef.standalone).toBe(true);
    });

    it('should implement OnInit lifecycle hook', () => {
      expect(typeof component.ngOnInit).toBe('function');
    });

    it('should implement OnDestroy lifecycle hook', () => {
      expect(typeof component.ngOnDestroy).toBe('function');
    });
  });

  // ============================================================================
  // Application Branding Tests
  // MIGRATION NOTE: Replaces Icon.ascx.vb imgIcon.ImageUrl branding (lines 93-108)
  // ============================================================================

  describe('Application Branding', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should display application branding section', () => {
      const brand = fixture.debugElement.query(By.css('.header-brand'));
      expect(brand).toBeTruthy();
    });

    it('should display application title containing "DNN Migration"', () => {
      const title = fixture.debugElement.query(By.css('.brand-title'));
      expect(title).toBeTruthy();
      expect(title.nativeElement.textContent).toContain('DNN Migration');
    });

    it('should have brand link element with routerLink to home', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink).toBeTruthy();
      expect(brandLink.nativeElement.getAttribute('href')).toBe('/');
    });

    it('should have brand logo SVG element', () => {
      const logo = fixture.debugElement.query(By.css('.brand-logo'));
      expect(logo).toBeTruthy();
      expect(logo.nativeElement.tagName.toLowerCase()).toBe('svg');
    });

    it('should have accessible aria-label on brand link', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink.nativeElement.getAttribute('aria-label')).toBe('Navigate to home');
    });

    it('should have aria-hidden on brand logo SVG', () => {
      const logo = fixture.debugElement.query(By.css('.brand-logo'));
      expect(logo.nativeElement.getAttribute('aria-hidden')).toBe('true');
    });
  });

  // ============================================================================
  // User Profile Display - Authenticated State Tests
  // MIGRATION NOTE: Replaces Title.ascx.vb lblTitle.Text rendering (line 88)
  // and CanEditModule() permission checks (lines 51-58)
  // ============================================================================

  describe('User Profile Display - Authenticated', () => {
    const testUser = createMockUser({
      userId: 42,
      displayName: 'John Doe',
      username: 'johndoe',
      email: 'john.doe@example.com',
      portalId: 1
    });

    beforeEach(() => {
      mockAuthService.setUser(testUser);
      fixture.detectChanges();
    });

    it('should display user section when authenticated', () => {
      const userSection = fixture.debugElement.query(By.css('.user-section'));
      expect(userSection).toBeTruthy();
    });

    it('should display user profile container', () => {
      const userProfile = fixture.debugElement.query(By.css('.user-profile'));
      expect(userProfile).toBeTruthy();
    });

    it('should display user display name "John Doe"', () => {
      const displayName = fixture.debugElement.query(By.css('.user-display-name'));
      expect(displayName).toBeTruthy();
      expect(displayName.nativeElement.textContent).toContain('John Doe');
    });

    it('should display username with @ prefix "@johndoe"', () => {
      const username = fixture.debugElement.query(By.css('.user-username'));
      expect(username).toBeTruthy();
      expect(username.nativeElement.textContent).toContain('@johndoe');
    });

    it('should display user avatar with initials "JD"', () => {
      const avatar = fixture.debugElement.query(By.css('.user-avatar'));
      expect(avatar).toBeTruthy();
      expect(avatar.nativeElement.textContent.trim()).toBe('JD');
    });

    it('should display logout button when authenticated', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn).toBeTruthy();
    });

    it('should not display login link when authenticated', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink).toBeFalsy();
    });

    it('should have correct aria-label on user avatar', () => {
      const avatar = fixture.debugElement.query(By.css('.user-avatar'));
      expect(avatar.nativeElement.getAttribute('aria-label')).toBe('User: John Doe');
    });
  });

  // ============================================================================
  // User Profile Display - Not Authenticated State Tests
  // ============================================================================

  describe('User Profile Display - Not Authenticated', () => {
    beforeEach(() => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
    });

    it('should not display user section when not authenticated', () => {
      const userSection = fixture.debugElement.query(By.css('.user-section'));
      expect(userSection).toBeFalsy();
    });

    it('should not display user profile when not authenticated', () => {
      const userProfile = fixture.debugElement.query(By.css('.user-profile'));
      expect(userProfile).toBeFalsy();
    });

    it('should not display logout button when not authenticated', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn).toBeFalsy();
    });

    it('should display auth section with login link', () => {
      const authSection = fixture.debugElement.query(By.css('.auth-section'));
      expect(authSection).toBeTruthy();
    });

    it('should display login link when not authenticated', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink).toBeTruthy();
    });

    it('should have login link pointing to /auth/login', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink.nativeElement.getAttribute('href')).toBe('/auth/login');
    });

    it('should display "Sign In" text in login link', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink.nativeElement.textContent).toContain('Sign In');
    });
  });

  // ============================================================================
  // User Initials Generation Tests
  // ============================================================================

  describe('User Initials Generation', () => {
    it('should generate initials "JD" from two-word name "John Doe"', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'John Doe' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('JD');
    });

    it('should generate initials "AD" from single name "Admin"', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'Admin' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('AD');
    });

    it('should generate initials "JD" from multi-word name "John Michael Doe"', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'John Michael Doe' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('JD');
    });

    it('should generate initials "AB" from name with extra spaces "  Alice   Brown  "', () => {
      mockAuthService.setUser(createMockUser({ displayName: '  Alice   Brown  ' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('AB');
    });

    it('should return "?" when no user is logged in', () => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('?');
    });

    it('should return "?" when user has empty display name', () => {
      mockAuthService.setUser(createMockUser({ displayName: '' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('?');
    });

    it('should return "?" when user has whitespace-only display name', () => {
      mockAuthService.setUser(createMockUser({ displayName: '   ' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('?');
    });

    it('should handle single character name "X"', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'X' }));
      fixture.detectChanges();
      // Single char should return first two chars, but only one available
      expect(component.getUserInitials()).toBe('X');
    });

    it('should convert initials to uppercase', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'jane doe' }));
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('JD');
    });
  });

  // ============================================================================
  // Logout Functionality Tests
  // MIGRATION NOTE: Replaces DNN's FormsAuthentication.SignOut patterns
  // ============================================================================

  describe('Logout Functionality', () => {
    const testUser = createMockUser();

    beforeEach(() => {
      mockAuthService.setUser(testUser);
      fixture.detectChanges();
    });

    it('should call AuthService.logout when logout button is clicked', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn).toBeTruthy();

      logoutBtn.triggerEventHandler('click', null);

      expect(mockAuthService.logout).toHaveBeenCalled();
    });

    it('should call AuthService.logout exactly once per click', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));

      logoutBtn.triggerEventHandler('click', null);

      expect(mockAuthService.logout).toHaveBeenCalledTimes(1);
    });

    it('should call onLogout method which delegates to AuthService', () => {
      component.onLogout();
      expect(mockAuthService.logout).toHaveBeenCalled();
    });

    it('should have logout button with type="button"', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn.nativeElement.getAttribute('type')).toBe('button');
    });

    it('should have accessible aria-label on logout button', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn.nativeElement.getAttribute('aria-label')).toBe('Sign out of your account');
    });

    it('should display "Sign Out" text in logout button', () => {
      const logoutText = fixture.debugElement.query(By.css('.logout-text'));
      expect(logoutText).toBeTruthy();
      expect(logoutText.nativeElement.textContent.trim()).toBe('Sign Out');
    });

    it('should have logout icon SVG in button', () => {
      const logoutIcon = fixture.debugElement.query(By.css('.logout-icon'));
      expect(logoutIcon).toBeTruthy();
      expect(logoutIcon.nativeElement.tagName.toLowerCase()).toBe('svg');
    });
  });

  // ============================================================================
  // Router Navigation on Logout Tests
  // MIGRATION NOTE: Verifies navigation to login page after logout
  // Replaces DNN's Response.Redirect behavior after FormsAuthentication.SignOut
  // ============================================================================

  describe('Router Navigation on Logout', () => {
    const testUser = createMockUser();

    beforeEach(() => {
      mockAuthService.setUser(testUser);
      fixture.detectChanges();
    });

    it('should call AuthService.logout which handles navigation', fakeAsync(() => {
      // Configure mock to simulate AuthService's internal navigation behavior
      mockAuthService.logout.and.callFake(() => {
        router.navigate(['/auth/login']);
      });

      component.onLogout();
      tick();

      expect(mockAuthService.logout).toHaveBeenCalled();
      expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
    }));

    it('should navigate to /auth/login path on logout button click', fakeAsync(() => {
      mockAuthService.logout.and.callFake(() => {
        router.navigate(['/auth/login']);
      });

      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      logoutBtn.triggerEventHandler('click', null);
      tick();

      expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
    }));
  });

  // ============================================================================
  // Portal Context Indicator Tests
  // MIGRATION NOTE: Replaces IconBar.ascx.vb PortalSettings.PortalId context (lines 59, 93)
  // ============================================================================

  describe('Portal Context Indicator', () => {
    it('should not display portal context when no user is authenticated', () => {
      mockAuthService.setUser(null);
      fixture.detectChanges();

      const portalContext = fixture.debugElement.query(By.css('.portal-context'));
      expect(portalContext).toBeFalsy();
    });

    it('should display portal context when authenticated user has portal', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ portalId: 1, isSuperUser: false }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalContext = fixture.debugElement.query(By.css('.portal-context'));
      expect(portalContext).toBeTruthy();
    }));

    it('should display "Host Portal" badge for super users', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ isSuperUser: true, portalId: -1 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge).toBeTruthy();
      expect(portalBadge.nativeElement.textContent).toContain('Host Portal');
    }));

    it('should display portal ID in badge for non-super users', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ isSuperUser: false, portalId: 5 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge).toBeTruthy();
      expect(portalBadge.nativeElement.textContent).toContain('Portal 5');
    }));

    it('should display portal context for portal ID 0', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ isSuperUser: false, portalId: 0 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge).toBeTruthy();
      expect(portalBadge.nativeElement.textContent).toContain('Portal 0');
    }));

    it('should have portal icon SVG in badge', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ isSuperUser: false, portalId: 1 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalIcon = fixture.debugElement.query(By.css('.portal-icon'));
      expect(portalIcon).toBeTruthy();
      expect(portalIcon.nativeElement.tagName.toLowerCase()).toBe('svg');
    }));

    it('should update portal context when user changes', fakeAsync(() => {
      // Start with one user
      mockAuthService.setUser(createMockUser({ portalId: 1, isSuperUser: false }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      let portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge.nativeElement.textContent).toContain('Portal 1');

      // Change to different user
      mockAuthService.setUser(createMockUser({ portalId: 2, isSuperUser: false }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge.nativeElement.textContent).toContain('Portal 2');
    }));
  });

  // ============================================================================
  // Signals and Computed Properties Tests
  // MIGRATION NOTE: Verifies Angular 19 reactive patterns using signals
  // ============================================================================

  describe('Reactive State (Signals)', () => {
    it('should have isAuthenticated as a callable signal', () => {
      fixture.detectChanges();
      expect(component.isAuthenticated).toBeDefined();
      expect(typeof component.isAuthenticated).toBe('function');
    });

    it('should have user as a callable signal', () => {
      fixture.detectChanges();
      expect(component.user).toBeDefined();
      expect(typeof component.user).toBe('function');
    });

    it('should have portalName as a writable signal', () => {
      fixture.detectChanges();
      expect(component.portalName).toBeDefined();
      expect(typeof component.portalName).toBe('function');
    });

    it('should return false for isAuthenticated when not logged in', () => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
      expect(component.isAuthenticated()).toBe(false);
    });

    it('should return true for isAuthenticated when logged in', () => {
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();
      expect(component.isAuthenticated()).toBe(true);
    });

    it('should return null for user() when not logged in', () => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
      expect(component.user()).toBeNull();
    });

    it('should return user object for user() when logged in', () => {
      const testUser = createMockUser({ username: 'signaltest' });
      mockAuthService.setUser(testUser);
      fixture.detectChanges();
      expect(component.user()).toBeTruthy();
      expect(component.user()?.username).toBe('signaltest');
    });

    it('should call getCurrentUser spy when accessing user signal', () => {
      fixture.detectChanges();
      component.user();
      expect(mockAuthService.getCurrentUser).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should have semantic header element with class app-header', () => {
      const header = fixture.debugElement.query(By.css('header.app-header'));
      expect(header).toBeTruthy();
    });

    it('should use semantic header element as root', () => {
      const rootElement = fixture.debugElement.query(By.css('.app-header'));
      expect(rootElement.nativeElement.tagName.toLowerCase()).toBe('header');
    });

    it('should have header-spacer for flexbox layout', () => {
      const spacer = fixture.debugElement.query(By.css('.header-spacer'));
      expect(spacer).toBeTruthy();
    });

    it('should have aria-hidden on decorative SVG icons', () => {
      const svgs = fixture.debugElement.queryAll(By.css('svg[aria-hidden="true"]'));
      expect(svgs.length).toBeGreaterThan(0);
    });

    it('should have all SVG elements with aria-hidden attribute', () => {
      const allSvgs = fixture.debugElement.queryAll(By.css('svg'));
      allSvgs.forEach(svg => {
        expect(svg.nativeElement.getAttribute('aria-hidden')).toBe('true');
      });
    });

    it('should have focusable brand link', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink.nativeElement.getAttribute('tabindex')).not.toBe('-1');
    });

    it('should have logout button focusable when authenticated', () => {
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn.nativeElement.getAttribute('tabindex')).not.toBe('-1');
    });
  });

  // ============================================================================
  // CSS Classes and Structure Tests
  // ============================================================================

  describe('CSS Classes and Structure', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should have app-header class on root element', () => {
      const header = fixture.debugElement.query(By.css('.app-header'));
      expect(header).toBeTruthy();
    });

    it('should have header-brand class for branding section', () => {
      const brand = fixture.debugElement.query(By.css('.header-brand'));
      expect(brand).toBeTruthy();
    });

    it('should have brand-link class on home navigation link', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink).toBeTruthy();
    });

    it('should have brand-logo class on logo SVG', () => {
      const logo = fixture.debugElement.query(By.css('.brand-logo'));
      expect(logo).toBeTruthy();
    });

    it('should have brand-title class on title text', () => {
      const title = fixture.debugElement.query(By.css('.brand-title'));
      expect(title).toBeTruthy();
    });

    it('should have user-avatar class when authenticated', () => {
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();
      const avatar = fixture.debugElement.query(By.css('.user-avatar'));
      expect(avatar).toBeTruthy();
    });

    it('should have user-info class when authenticated', () => {
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();
      const userInfo = fixture.debugElement.query(By.css('.user-info'));
      expect(userInfo).toBeTruthy();
    });
  });

  // ============================================================================
  // Lifecycle Hooks Tests
  // ============================================================================

  describe('Lifecycle Hooks', () => {
    it('should initialize portalName to empty string before user subscription fires', () => {
      // Before detectChanges, component is not initialized
      fixture.detectChanges();
      // With no user, portalName should remain empty
      mockAuthService.setUser(null);
      fixture.detectChanges();
      expect(component.portalName()).toBe('');
    });

    it('should update portalName when user changes via subscription', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ portalId: 5, isSuperUser: false }));
      fixture.detectChanges();
      tick();

      expect(component.portalName()).toContain('Portal');
    }));

    it('should clear portalName when user logs out', fakeAsync(() => {
      // Start authenticated
      mockAuthService.setUser(createMockUser({ portalId: 5 }));
      fixture.detectChanges();
      tick();
      expect(component.portalName()).toContain('Portal');

      // Logout
      mockAuthService.setUser(null);
      fixture.detectChanges();
      tick();
      expect(component.portalName()).toBe('');
    }));

    it('should implement OnDestroy for cleanup', () => {
      fixture.detectChanges();
      expect(typeof component.ngOnDestroy).toBe('function');
    });

    it('should clean up subscription on destroy without throwing', () => {
      fixture.detectChanges();
      expect(() => component.ngOnDestroy()).not.toThrow();
    });

    it('should handle multiple ngOnDestroy calls gracefully', () => {
      fixture.detectChanges();
      component.ngOnDestroy();
      expect(() => component.ngOnDestroy()).not.toThrow();
    });
  });

  // ============================================================================
  // Edge Cases and Error Handling Tests
  // ============================================================================

  describe('Edge Cases', () => {
    it('should handle user with negative portalId', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ portalId: -1, isSuperUser: false }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      // Negative portalId (but not super user) should still show context
      // Based on implementation: portalId >= 0 shows portal, otherwise empty
      const portalContext = fixture.debugElement.query(By.css('.portal-context'));
      // -1 is not >= 0, so no portal context for non-super users
      expect(portalContext).toBeFalsy();
    }));

    it('should handle rapid user changes', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ displayName: 'User One', portalId: 1 }));
      fixture.detectChanges();
      tick();

      mockAuthService.setUser(createMockUser({ displayName: 'User Two', portalId: 2 }));
      fixture.detectChanges();
      tick();

      mockAuthService.setUser(createMockUser({ displayName: 'User Three', portalId: 3 }));
      fixture.detectChanges();
      tick();

      expect(component.portalName()).toContain('Portal 3');
      expect(component.user()?.displayName).toBe('User Three');
    }));

    it('should handle special characters in display name', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'José García' }));
      fixture.detectChanges();

      const displayName = fixture.debugElement.query(By.css('.user-display-name'));
      expect(displayName.nativeElement.textContent).toContain('José García');
    });

    it('should handle emoji in display name', () => {
      mockAuthService.setUser(createMockUser({ displayName: 'Test 🎉 User' }));
      fixture.detectChanges();

      const displayName = fixture.debugElement.query(By.css('.user-display-name'));
      expect(displayName.nativeElement.textContent).toContain('Test 🎉 User');
    });
  });

  // ============================================================================
  // Integration with AuthService Tests
  // ============================================================================

  describe('AuthService Integration', () => {
    it('should use AuthService.getCurrentUser for user signal', () => {
      const testUser = createMockUser({ userId: 999 });
      mockAuthService.setUser(testUser);
      fixture.detectChanges();

      // Access user signal which calls getCurrentUser
      const user = component.user();
      
      expect(mockAuthService.getCurrentUser).toHaveBeenCalled();
      expect(user?.userId).toBe(999);
    });

    it('should use AuthService.isAuthenticated for authentication check', () => {
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();

      const isAuth = component.isAuthenticated();
      
      expect(isAuth).toBe(true);
    });

    it('should subscribe to AuthService.currentUser$ for portal context', fakeAsync(() => {
      fixture.detectChanges();
      
      // Initial state - no user
      expect(component.portalName()).toBe('');

      // User subscription should update portal name
      mockAuthService.setUser(createMockUser({ portalId: 7 }));
      tick();
      fixture.detectChanges();

      expect(component.portalName()).toContain('Portal 7');
    }));
  });
});
