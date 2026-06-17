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

  it('should render exactly the five feature links with correct hrefs and labels', () => {
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));

    expect(links.length).toBe(5);

    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).toEqual(['/portals', '/modules', '/users', '/roles', '/tabs']);

    const labels = links.map((link) => (link.nativeElement.textContent as string).trim());
    expect(labels).toEqual(['Portals', 'Modules', 'Users', 'Roles', 'Tabs']);
  });

  it('should link to the /tabs route as a normal navigation entry', () => {
    const fixture = createComponent(true);
    const links = fixture.debugElement.queryAll(By.directive(RouterLink));
    const hrefs = links.map((link) => link.nativeElement.getAttribute('href'));
    expect(hrefs).toContain('/tabs');
  });

  it('should not render any disabled, non-routing navigation item', () => {
    const fixture = createComponent(true);
    const disabled = fixture.debugElement.query(By.css('[aria-disabled="true"]'));
    expect(disabled).toBeNull();
  });

  it('should hide the navigation when not authenticated', () => {
    const fixture = createComponent(false);
    const nav = fixture.debugElement.query(By.css('nav'));
    expect(nav).toBeNull();
  });
});
