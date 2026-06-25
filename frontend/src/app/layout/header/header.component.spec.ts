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
});
