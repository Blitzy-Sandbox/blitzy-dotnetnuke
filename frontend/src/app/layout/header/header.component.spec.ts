import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models/user.model';
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

  beforeEach(async () => {
    fakeAuthService = {
      currentUser: signal<User | null>(null),
      isAuthenticated: signal<boolean>(false),
      logout: jasmine.createSpy('logout'),
    };

    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [{ provide: AuthService, useValue: fakeAuthService }],
    }).compileComponents();

    fixture = TestBed.createComponent(HeaderComponent);
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
});
