/**
 * @fileoverview Unit tests for HeaderComponent
 * @description Comprehensive test suite for Angular 19 standalone HeaderComponent
 * verifying branding display, user profile rendering, logout functionality,
 * and portal context indicator.
 *
 * MIGRATION NOTE: Tests verify the header replaces DNN's Title.ascx.vb title display
 * and Icon.ascx.vb branding with Angular reactive patterns.
 *
 * @module layout/header/header.component.spec
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { BehaviorSubject } from 'rxjs';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models/user.model';

/**
 * Creates a mock User object for testing purposes.
 * @param overrides - Optional partial User properties to override defaults
 * @returns Complete User object with test data
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
 * Mock AuthService class for testing
 */
class MockAuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  private isAuthenticatedSignal = signal(false);

  currentUser$ = this.currentUserSubject.asObservable();

  isAuthenticated = () => this.isAuthenticatedSignal();

  getCurrentUser = jasmine.createSpy('getCurrentUser').and.callFake(() => {
    return this.currentUserSubject.value;
  });

  logout = jasmine.createSpy('logout');

  // Test helper methods
  setUser(user: User | null): void {
    this.currentUserSubject.next(user);
    this.isAuthenticatedSignal.set(user !== null);
  }
}

describe('HeaderComponent', () => {
  let component: HeaderComponent;
  let fixture: ComponentFixture<HeaderComponent>;
  let mockAuthService: MockAuthService;

  beforeEach(async () => {
    mockAuthService = new MockAuthService();

    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    fixture?.destroy();
  });

  // ==================== Component Creation Tests ====================

  describe('Component Creation', () => {
    it('should create the component', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
    });

    it('should be a standalone component', () => {
      const metadata = (HeaderComponent as any).ɵcmp;
      expect(metadata.standalone).toBe(true);
    });
  });

  // ==================== Branding/Logo Tests ====================

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

    it('should have brand link element', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink).toBeTruthy();
    });

    it('should have brand logo element', () => {
      const logo = fixture.debugElement.query(By.css('.brand-logo'));
      expect(logo).toBeTruthy();
    });

    it('should have accessible aria-label on brand link', () => {
      const brandLink = fixture.debugElement.query(By.css('.brand-link'));
      expect(brandLink.nativeElement.getAttribute('aria-label')).toBe('Navigate to home');
    });
  });

  // ==================== User Profile - Authenticated ====================

  describe('User Profile Display - Authenticated', () => {
    const testUser = createMockUser({
      displayName: 'John Doe',
      username: 'johndoe'
    });

    beforeEach(() => {
      mockAuthService.setUser(testUser);
      fixture.detectChanges();
    });

    it('should display user section when authenticated', () => {
      const userSection = fixture.debugElement.query(By.css('.user-section'));
      expect(userSection).toBeTruthy();
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

    it('should display logout button', () => {
      const logoutBtn = fixture.debugElement.query(By.css('.logout-button'));
      expect(logoutBtn).toBeTruthy();
    });

    it('should not display login link when authenticated', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink).toBeFalsy();
    });
  });

  // ==================== User Profile - Not Authenticated ====================

  describe('User Profile Display - Not Authenticated', () => {
    beforeEach(() => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
    });

    it('should not display user section when not authenticated', () => {
      const userSection = fixture.debugElement.query(By.css('.user-section'));
      expect(userSection).toBeFalsy();
    });

    it('should display login link when not authenticated', () => {
      const loginLink = fixture.debugElement.query(By.css('.login-link'));
      expect(loginLink).toBeTruthy();
    });
  });

  // ==================== User Initials Tests ====================

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

    it('should return "?" when no user is logged in', () => {
      mockAuthService.setUser(null);
      fixture.detectChanges();
      expect(component.getUserInitials()).toBe('?');
    });
  });

  // ==================== Logout Functionality ====================

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
  });

  // ==================== Portal Context Indicator ====================

  describe('Portal Context Indicator', () => {
    it('should not display portal context when no user', () => {
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

    it('should display portal ID for non-super users', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ isSuperUser: false, portalId: 5 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();

      const portalBadge = fixture.debugElement.query(By.css('.portal-badge'));
      expect(portalBadge).toBeTruthy();
      expect(portalBadge.nativeElement.textContent).toContain('Portal 5');
    }));
  });

  // ==================== Signals and Computed Properties ====================

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
      mockAuthService.setUser(createMockUser());
      fixture.detectChanges();
      expect(component.user()).toBeTruthy();
      expect(component.user()?.username).toBe('testuser');
    });
  });

  // ==================== Accessibility Tests ====================

  describe('Accessibility', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should have semantic header element with class app-header', () => {
      const header = fixture.debugElement.query(By.css('header.app-header'));
      expect(header).toBeTruthy();
    });

    it('should have header-spacer for flexbox layout', () => {
      const spacer = fixture.debugElement.query(By.css('.header-spacer'));
      expect(spacer).toBeTruthy();
    });

    it('should have aria-hidden on decorative SVG icons', () => {
      const svgs = fixture.debugElement.queryAll(By.css('svg[aria-hidden="true"]'));
      expect(svgs.length).toBeGreaterThan(0);
    });
  });

  // ==================== CSS Classes Tests ====================

  describe('CSS Classes', () => {
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
  });

  // ==================== Lifecycle Tests ====================

  describe('Lifecycle Hooks', () => {
    it('should initialize portalName to empty string', () => {
      fixture.detectChanges();
      expect(component.portalName()).toBe('');
    });

    it('should update portalName when user changes', fakeAsync(() => {
      mockAuthService.setUser(createMockUser({ portalId: 5, isSuperUser: false }));
      fixture.detectChanges();
      tick();

      expect(component.portalName()).toContain('Portal');
    }));

    it('should implement OnDestroy for cleanup', () => {
      fixture.detectChanges();
      expect(typeof component.ngOnDestroy).toBe('function');
    });

    it('should clean up subscription on destroy', () => {
      fixture.detectChanges();
      // Call ngOnDestroy directly to verify it executes without error
      // and cleans up resources
      expect(() => component.ngOnDestroy()).not.toThrow();
    });
  });
});
