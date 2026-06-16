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

  it('should render exactly the four feature links with correct hrefs and labels', () => {
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));

    expect(links.length).toBe(4);

    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).toEqual(['/portals', '/modules', '/users', '/roles']);

    const labels = links.map((link) => (link.nativeElement.textContent as string).trim());
    expect(labels).toEqual(['Portals', 'Modules', 'Users', 'Roles']);
  });

  it('should not link to a non-existent /tabs route', () => {
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));
    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).not.toContain('/tabs');
  });

  it('should render a disabled, non-routing Tabs item', () => {
    const fixture = createComponent(true);
    const disabled = fixture.debugElement.query(By.css('[aria-disabled="true"]'));
    expect(disabled).not.toBeNull();
    // CP3's template renders the disabled item as its label plus a "(coming soon)"
    // affordance, so assert the "Tabs" label is present rather than an exact match.
    expect((disabled.nativeElement.textContent as string).trim()).toContain('Tabs');
    expect(disabled.nativeElement.getAttribute('href')).toBeNull();
  });

  it('should hide the navigation when not authenticated', () => {
    const fixture = createComponent(false);
    const nav = fixture.debugElement.query(By.css('nav'));
    expect(nav).toBeNull();
  });
});
