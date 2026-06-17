import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models/user.model';
import { LayoutService } from '../layout.service';
import { HeaderComponent } from './header.component';

interface FakeAuthService {
  currentUser: WritableSignal<User | null>;
  isAuthenticated: WritableSignal<boolean>;
  logout: jasmine.Spy;
}

// Builds a fully-typed `User` fixture. Field names and nullability match the REAL
// core/models/user.model.ts interface exactly (`userID`/`portalID`/`affiliateID`
// casing, every member present — mirroring the sibling auth.service.spec.ts
// fixture), keeping the suite strict-TS clean with no `any`. Overrides are spread
// last so individual specs can tweak just the fields they assert on.
function buildUser(overrides: Partial<User> = {}): User {
  return {
    userID: 1,
    portalID: 0,
    affiliateID: null,
    username: 'admin',
    displayName: 'Administrator',
    email: 'admin@example.com',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: true,
    approved: true,
    updatePassword: false,
    roles: ['Administrators'],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
    ...overrides,
  };
}

describe('HeaderComponent', () => {
  let fixture: ComponentFixture<HeaderComponent>;
  let fakeAuthService: FakeAuthService;
  let layout: LayoutService;

  beforeEach(async () => {
    fakeAuthService = {
      currentUser: signal<User | null>(null),
      isAuthenticated: signal<boolean>(false),
      logout: jasmine.createSpy('logout'),
    };

    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      // provideRouter([]) supplies the Router that the REAL LayoutService now
      // injects (to auto-close the drawer on NavigationEnd). Without it, DI for
      // LayoutService — and therefore the header — would fail to construct.
      providers: [{ provide: AuthService, useValue: fakeAuthService }, provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
    layout = TestBed.inject(LayoutService);
  });

  it('should create the component', () => {
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render nothing when the user is not authenticated', () => {
    fakeAuthService.isAuthenticated.set(false);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('header'))).toBeNull();
  });

  it('should render the header landmark when the user is authenticated', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('header'))).not.toBeNull();
  });

  it('should display the authenticated user display name', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser({ displayName: 'Administrator' }));
    fixture.detectChanges();

    const userElement = fixture.debugElement.query(By.css('.app-header__user')).nativeElement as HTMLElement;
    expect(userElement.textContent?.trim()).toBe('Administrator');
  });

  it('should fall back to the username when the display name is blank', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser({ displayName: '   ', username: 'jsmith' }));
    fixture.detectChanges();

    const userElement = fixture.debugElement.query(By.css('.app-header__user')).nativeElement as HTMLElement;
    expect(userElement.textContent?.trim()).toBe('jsmith');
  });

  it('should call AuthService.logout when the logout button is clicked', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    fixture.detectChanges();

    const button = fixture.debugElement.query(By.css('.app-header__logout')).nativeElement as HTMLButtonElement;
    button.click();

    expect(fakeAuthService.logout).toHaveBeenCalledTimes(1);
  });

  it('should expose an accessible name on the logout button', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    fixture.detectChanges();

    const button = fixture.debugElement.query(By.css('.app-header__logout')).nativeElement as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Log out');
  });

  it('should toggle the drawer and reflect open state via aria-expanded on the hamburger', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    fixture.detectChanges();

    const menu = fixture.debugElement.query(By.css('.app-header__menu')).nativeElement as HTMLButtonElement;
    expect(menu.getAttribute('aria-expanded')).toBe('false');

    menu.click();
    fixture.detectChanges();
    expect(layout.sidebarOpen()).toBe(true);
    expect(menu.getAttribute('aria-expanded')).toBe('true');

    menu.click();
    fixture.detectChanges();
    expect(layout.sidebarOpen()).toBe(false);
    expect(menu.getAttribute('aria-expanded')).toBe('false');
  });

  it('should close the drawer and return focus to the hamburger when Escape is pressed', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    // Attach to the live DOM so focus() actually moves document.activeElement.
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();

    layout.openSidebar();
    fixture.detectChanges();
    expect(layout.sidebarOpen()).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    const menu = fixture.debugElement.query(By.css('.app-header__menu')).nativeElement as HTMLButtonElement;
    expect(layout.sidebarOpen()).toBe(false);
    expect(document.activeElement).toBe(menu);

    document.body.removeChild(fixture.nativeElement);
  });

  it('should ignore Escape when the drawer is already closed (no focus theft)', () => {
    fakeAuthService.isAuthenticated.set(true);
    fakeAuthService.currentUser.set(buildUser());
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();

    expect(layout.sidebarOpen()).toBe(false);

    const logout = fixture.debugElement.query(By.css('.app-header__logout')).nativeElement as HTMLButtonElement;
    logout.focus();
    expect(document.activeElement).toBe(logout);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    // Drawer stays closed and focus is NOT yanked to the hamburger.
    expect(layout.sidebarOpen()).toBe(false);
    expect(document.activeElement).toBe(logout);

    document.body.removeChild(fixture.nativeElement);
  });
});
