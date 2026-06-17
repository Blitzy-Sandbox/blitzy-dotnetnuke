import { TestBed } from '@angular/core/testing';
import { Event as RouterEvent, NavigationEnd, NavigationStart, Router } from '@angular/router';
import { Subject } from 'rxjs';

import { LayoutService } from './layout.service';

/**
 * Unit tests for {@link LayoutService}.
 *
 * The service owns the responsive sidebar-drawer open/close state and — as of
 * the QA drawer-accessibility fix — auto-closes the drawer on every completed
 * navigation. The Router is replaced with a lightweight stub whose `events`
 * stream we drive manually via a Subject, so the suite can assert the
 * NavigationEnd → closeSidebar wiring deterministically without bootstrapping a
 * real router/route table.
 */
describe('LayoutService', () => {
  let events$: Subject<RouterEvent>;
  let service: LayoutService;

  beforeEach(() => {
    events$ = new Subject<RouterEvent>();

    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        // Stub Router exposing only the `events` observable the service consumes.
        { provide: Router, useValue: { events: events$.asObservable() } },
      ],
    });

    // Instantiating within TestBed's injection context lets the service's
    // constructor `inject(Router)` + `takeUntilDestroyed()` resolve correctly.
    service = TestBed.inject(LayoutService);
  });

  it('should be created with the drawer closed by default', () => {
    expect(service).toBeTruthy();
    expect(service.sidebarOpen()).toBe(false);
  });

  it('should open, toggle, and close the drawer', () => {
    service.openSidebar();
    expect(service.sidebarOpen()).toBe(true);

    service.toggleSidebar();
    expect(service.sidebarOpen()).toBe(false);

    service.toggleSidebar();
    expect(service.sidebarOpen()).toBe(true);

    service.closeSidebar();
    expect(service.sidebarOpen()).toBe(false);
  });

  it('should close the drawer when a navigation completes (NavigationEnd)', () => {
    service.openSidebar();
    expect(service.sidebarOpen()).toBe(true);

    events$.next(new NavigationEnd(1, '/portals', '/portals'));

    expect(service.sidebarOpen()).toBe(false);
  });

  it('should keep the drawer open for non-NavigationEnd router events', () => {
    service.openSidebar();
    expect(service.sidebarOpen()).toBe(true);

    // A navigation that has only STARTED must not prematurely close the drawer.
    events$.next(new NavigationStart(1, '/portals'));

    expect(service.sidebarOpen()).toBe(true);
  });
});
