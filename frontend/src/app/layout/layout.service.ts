import { Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * LayoutService — root-provided singleton that owns transient application-shell
 * UI state shared between the chrome components.
 *
 * Currently it tracks whether the responsive sidebar drawer is open. On narrow
 * viewports (≤768px) the sidebar collapses into an off-canvas drawer (QA Issue
 * #4): the header's hamburger button toggles `sidebarOpen`, the sidebar slides
 * in/out accordingly, and tapping the backdrop, following a navigation link,
 * completing any route change, or pressing Escape closes it. On wider viewports
 * the sidebar is always visible and this flag is inert (CSS keeps the sidebar
 * static and hides the backdrop/hamburger).
 *
 * The state is a signal so OnPush chrome components (header, sidebar, root
 * shell) re-render automatically when it changes, with no manual change
 * detection.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly _sidebarOpen = signal<boolean>(false);

  /** Read-only view of whether the mobile sidebar drawer is currently open. */
  readonly sidebarOpen = this._sidebarOpen.asReadonly();

  constructor() {
    // Auto-close the drawer on EVERY completed navigation so it never lingers
    // over the newly-routed content on small screens (QA: "drawer persists
    // across route"). Subscribing to the router's NavigationEnd covers all
    // navigation triggers — programmatic navigation and browser back/forward —
    // not only sidebar link clicks (which additionally call closeSidebar() for
    // an immediate close, including same-URL clicks that emit no NavigationEnd).
    // takeUntilDestroyed ties the subscription to this root singleton's
    // lifetime (released on app teardown / TestBed reset).
    inject(Router)
      .events.pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this._sidebarOpen.set(false));
  }

  /** Toggle the mobile sidebar drawer open/closed (header hamburger). */
  toggleSidebar(): void {
    this._sidebarOpen.update((open) => !open);
  }

  /** Explicitly open the mobile sidebar drawer. */
  openSidebar(): void {
    this._sidebarOpen.set(true);
  }

  /** Close the mobile sidebar drawer (backdrop tap, navigation, Escape). */
  closeSidebar(): void {
    this._sidebarOpen.set(false);
  }
}
