import { Injectable, signal } from '@angular/core';

/**
 * LayoutService — root-provided singleton that owns transient application-shell
 * UI state shared between the chrome components.
 *
 * Currently it tracks whether the responsive sidebar drawer is open. On narrow
 * viewports (≤768px) the sidebar collapses into an off-canvas drawer (QA Issue
 * #4): the header's hamburger button toggles `sidebarOpen`, the sidebar slides
 * in/out accordingly, and tapping the backdrop or following a navigation link
 * closes it. On wider viewports the sidebar is always visible and this flag is
 * inert (CSS keeps the sidebar static and hides the backdrop/hamburger).
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
