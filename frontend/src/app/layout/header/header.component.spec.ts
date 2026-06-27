// MIGRATION: Spec for the net-new Angular header shell (no legacy equivalent). Verifies the skip-link
// (accessibility), the banner landmark, and that logout() delegates to AuthService.logout()
// (which replaces the legacy PortalSecurity.SignOut() Forms-auth sign-out).
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';

import { HeaderComponent } from './header.component';
import { AuthService } from '../../core/auth/auth.service';

describe('HeaderComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render a skip-link targeting #main-content (accessibility)', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    const skipLink = compiled.querySelector('a.skip-link');
    expect(skipLink).toBeTruthy();
    expect(skipLink?.getAttribute('href')).toBe('#main-content');
  });

  it('should render a banner landmark', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('header[role="banner"]')).toBeTruthy();
  });

  it('should delegate logout to AuthService.logout()', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    const auth = TestBed.inject(AuthService);
    const spy = spyOn(auth, 'logout').and.returnValue(of(void 0));

    fixture.componentInstance.logout();

    expect(spy).toHaveBeenCalled();
  });

  // MIGRATION: [QA F4-011] Activating the skip-link must move focus to #main-content WITHOUT navigating.
  // A plain href="#main-content" resolves against <base href="/"> to "/#main-content" -> a cross-document
  // navigation that reloads the SPA and clears the in-memory session (logging the user out). The (click)
  // handler preventDefault()s that navigation and focuses the main landmark instead.
  it('activating the skip-link prevents default navigation and focuses #main-content', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    // #main-content is declared in app.component.ts (tabindex="-1"); recreate it for this isolated test.
    const main = document.createElement('main');
    main.id = 'main-content';
    main.tabIndex = -1;
    document.body.appendChild(main);

    try {
      const skipLink = (fixture.nativeElement as HTMLElement).querySelector(
        'a.skip-link',
      ) as HTMLAnchorElement;

      // A cancelable click (the same event keyboard Enter dispatches on an anchor).
      const event = new MouseEvent('click', { bubbles: true, cancelable: true });
      skipLink.dispatchEvent(event);

      expect(event.defaultPrevented).toBeTrue();
      expect(document.activeElement).toBe(main);
    } finally {
      main.remove();
    }
  });

  it('skipToMain still prevents default (and does not throw) when #main-content is absent', () => {
    const fixture = TestBed.createComponent(HeaderComponent);
    fixture.detectChanges();

    const event = new MouseEvent('click', { bubbles: true, cancelable: true });
    expect(() => fixture.componentInstance.skipToMain(event)).not.toThrow();
    expect(event.defaultPrevented).toBeTrue();
  });
});
