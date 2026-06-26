// MIGRATION: Spec for the net-new primary navigation sidebar (no legacy equivalent — DNN skinning/master
// navigation out of scope, AAP §0.6.2). Verifies the nav landmark and that routerLinks align with app.routes.ts.
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render a primary navigation landmark with an accessible label', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    expect(nav).toBeTruthy();
    expect(nav?.getAttribute('aria-label')?.trim()).toBeTruthy();
  });

  it('should render router links for the advertised top-level admin routes', () => {
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
});
