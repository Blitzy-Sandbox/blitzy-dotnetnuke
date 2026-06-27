// MIGRATION: Spec for the net-new primary navigation sidebar (no legacy equivalent — DNN skinning/master
// navigation out of scope, AAP §0.6.2). Verifies the nav landmark and that routerLinks align with app.routes.ts.
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal, type WritableSignal } from '@angular/core';

import { SidebarComponent } from './sidebar.component';
import { AuthService } from '../../core/auth/auth.service';

describe('SidebarComponent', () => {
  // MIGRATION: [QA F4-010] sidebar now gates its nav behind AuthService.isAuthenticated. A minimal stub
  // AuthService exposes only the isAuthenticated signal the sidebar reads, so the unit test does not need
  // HttpClient (the real AuthService injects HttpClient).
  let isAuthenticated: WritableSignal<boolean>;

  beforeEach(async () => {
    // Default to authenticated (the state in which the nav is shown); the unauthenticated test flips this.
    isAuthenticated = signal(true);
    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated } },
      ],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render a primary navigation landmark with an accessible label when authenticated', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    expect(nav).toBeTruthy();
    expect(nav?.getAttribute('aria-label')?.trim()).toBeTruthy();
  });

  it('should render router links for the advertised top-level admin routes when authenticated', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    const anchors = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('a'),
    );
    const hrefs = anchors.map((anchor) => anchor.getAttribute('href'));
    expect(hrefs).toContain('/portals');
    expect(hrefs).toContain('/users');
    expect(hrefs).toContain('/roles');

    // MIGRATION: [CP4 review — Frontend Routing] /modules is intentionally NOT advertised: there is no
    // module-list landing page (AAP 0.4.2), so the contentless top-level link was removed. Assert its absence
    // (and the exact advertised link set) so the dead-end nav entry cannot be reintroduced unnoticed.
    expect(hrefs).not.toContain('/modules');
    expect(anchors.length).toBe(3);
  });

  // MIGRATION: [QA F4-010] When unauthenticated the primary nav (and its landmark) is hidden, mirroring the
  // header hiding the user-menu/logout, so an unauthenticated visitor (e.g. on /auth/login) does not see the
  // admin nav links and the shell does not disclose the admin structure pre-login.
  it('should hide the primary nav when unauthenticated', () => {
    isAuthenticated.set(false);
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('nav')).toBeNull();
    expect(compiled.querySelectorAll('a').length).toBe(0);
  });
});
