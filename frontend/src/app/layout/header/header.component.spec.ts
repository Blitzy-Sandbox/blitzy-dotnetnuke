import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../core/auth/auth.service';
import { CurrentUser } from '../../core/models';

/**
 * Karma/Jasmine unit spec for {@link HeaderComponent} (AAP Validation Gate 4:
 * `ng test --watch=false --browsers=ChromeHeadless --code-coverage`).
 *
 * QA finding P10-1 (Report 11 — WCAG 2.5.3 "Label in Name"): the logout
 * control's accessible name (aria-label) must contain its visible label. The
 * button's visible text is "Logout" but the aria-label was "Log out" (a
 * one-space divergence that breaks speech-input activation and fails
 * label-in-name). This spec locks the corrected, matching accessible name in the
 * REAL DOM so the fix cannot silently regress.
 *
 * HeaderComponent is presentation-only (AAP §0.6.3): it reads
 * `AuthService.currentUser()` (a Signal) and calls `AuthService.logout()` on
 * click — no HttpClient, no Router (logout navigation lives inside
 * AuthService.logout()). Only AuthService is injected, so only AuthService is
 * stubbed. `CurrentUser` aliases the full `User` model; the template reads just
 * `displayName`/`username`, so a partial object is cast (test-only convenience,
 * mirroring the sibling feature-list specs; production code never casts this way).
 */
describe('HeaderComponent', () => {
  // Writable signal standing in for AuthService.currentUser (a read-only Signal at
  // runtime); WritableSignal is assignable to Signal and both are callable, which
  // is all the OnPush template needs.
  const currentUser = signal<CurrentUser | null>(null);
  let logoutSpy: jasmine.Spy;

  beforeEach(() => {
    // Default: an authenticated user so the logout button is rendered.
    currentUser.set({ username: 'jdoe', displayName: 'Jane Doe' } as CurrentUser);
    logoutSpy = jasmine.createSpy('logout');

    const authStub = {
      currentUser,
      logout: logoutSpy,
    } as unknown as AuthService;

    TestBed.configureTestingModule({
      // Standalone component → provided via `imports`, never `declarations`.
      imports: [HeaderComponent],
      providers: [{ provide: AuthService, useValue: authStub }],
    });
  });

  it('creates the component', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the logout button with an aria-label that matches its visible text (P10-1 / WCAG 2.5.3)', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    const logout = fixture.nativeElement.querySelector('.app-header__logout') as HTMLButtonElement;
    expect(logout).toBeTruthy();

    const visibleText = logout.textContent?.trim();
    const ariaLabel = logout.getAttribute('aria-label');

    // Both the visible label and the accessible name are the single token "Logout".
    expect(visibleText).toBe('Logout');
    expect(ariaLabel).toBe('Logout');
    // WCAG 2.5.3: the accessible name must contain the visible label string.
    expect(ariaLabel?.toLowerCase()).toContain(visibleText!.toLowerCase());
  });

  it('invokes AuthService.logout() when the logout button is clicked', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    const logout = fixture.nativeElement.querySelector('.app-header__logout') as HTMLButtonElement;
    logout.click();

    expect(logoutSpy).toHaveBeenCalledTimes(1);
  });

  it('renders no user region (and no logout button) when unauthenticated', () => {
    currentUser.set(null);

    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-header__logout')).toBeNull();
    expect(fixture.nativeElement.querySelector('.app-header__user')).toBeNull();
  });
});
