import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  let authSpy: jasmine.SpyObj<AuthService>;

  /**
   * Creates the component with the authentication state fixed BEFORE the
   * first change detection. This is required because the component uses
   * `OnPush`: the spy is not a signal, so its value must be set prior to
   * `createComponent` for the initial `@if (auth.isAuthenticated())` to
   * evaluate correctly.
   */
  const createComponent = (authenticated: boolean): ComponentFixture<SidebarComponent> => {
    authSpy.isAuthenticated.and.returnValue(authenticated);
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => {
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['isAuthenticated']);

    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
      ],
    });
  });

  it('should create', () => {
    const fixture = createComponent(true);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the navigation landmark when authenticated', () => {
    const fixture = createComponent(true);
    const nav = fixture.debugElement.query(By.css('nav'));
    expect(nav).not.toBeNull();
  });

  it('should render exactly the four in-scope feature links with correct hrefs and labels', () => {
    // SCOPE (QA Issue #1): only the four in-scope features route. "Tabs" is
    // present as a label but is NOT a RouterLink (see the disabled-item spec).
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));

    expect(links.length).toBe(4);

    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).toEqual(['/portals', '/modules', '/users', '/roles']);

    const labels = links.map((link) => (link.nativeElement.textContent as string).trim());
    expect(labels).toEqual(['Portals', 'Modules', 'Users', 'Roles']);
  });

  it('should NOT route to /tabs — there is no frontend Tabs feature (scope)', () => {
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));
    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).not.toContain('/tabs');
  });

  it('should render "Tabs" as a disabled, non-routing navigation item', () => {
    const fixture = createComponent(true);

    // The Tabs entry exists as a label...
    const labels = fixture.debugElement
      .queryAll(By.css('.sidebar__link'))
      .map((el) => (el.nativeElement.textContent as string).trim());
    expect(labels).toContain('Tabs');

    // ...but is rendered disabled (aria-disabled), not as a focusable anchor.
    const disabled = fixture.debugElement.query(By.css('[aria-disabled="true"]'));
    expect(disabled).not.toBeNull();
    expect((disabled.nativeElement.textContent as string).trim()).toBe('Tabs');
    expect((disabled.nativeElement as HTMLElement).tagName).not.toBe('A');
    // Not focusable: a non-anchor span carries no tabindex, so it is out of the
    // keyboard tab order.
    expect(disabled.nativeElement.getAttribute('tabindex')).toBeNull();
  });

  it('should hide the navigation when not authenticated', () => {
    const fixture = createComponent(false);
    const nav = fixture.debugElement.query(By.css('nav'));
    expect(nav).toBeNull();
  });
});
