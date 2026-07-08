import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PortalListComponent } from './portal-list.component';
import { PortalService } from '../portal.service';
import { Portal } from '../../../core/models';

/**
 * Unit spec for {@link PortalListComponent}.
 *
 * Verifies the LIST load lifecycle and the DELETE flow of the migrated host-portals
 * administration screen against a fully mocked {@link PortalService}
 * (`jasmine.SpyObj`) — NO real HTTP, NO `HttpTestingController`. Contributes to
 * Gate 4 (`ng test --watch=false --browsers=ChromeHeadless --code-coverage`, 100%
 * pass).
 *
 * MIGRATION: the behaviors asserted here mirror the legacy DotNetNuke Web Forms
 * host-portals code-behind (Website/admin/Portal/Portals.ascx.vb) — `BindData`
 * (the load-and-populate lifecycle that fed `grdPortals`) and
 * `grdPortals_DeleteCommand` (delete-the-selected-portal-then-rebind). That VB
 * source is REFERENCE-only; no code is transliterated (AAP §0.7.1).
 *
 * Test design notes:
 * - The component is standalone, so it is imported directly (never `declarations`).
 * - It talks ONLY to `PortalService`; only `list` and `remove` are spied because
 *   those are the sole service methods the component invokes. No `AuthService`,
 *   `HttpClient`, or `provideHttpClient` is required — the row actions carry no
 *   `requiredRoles`, so the DataTable's permission-directive/`AuthService` path is
 *   never entered, and every downstream standalone component/directive resolves
 *   from the import graph plus `provideRouter([])`.
 * - A real `Router` is supplied via `provideRouter([])` to satisfy the component's
 *   injected dependency; the exercised paths (load + confirm-delete) never
 *   navigate, so no navigation spy is needed.
 * - Signals combined with synchronous `of(...)` stubs make `subscribe()` run
 *   synchronously, so component state can be asserted right after
 *   `detectChanges()` with no `fakeAsync`/`tick`.
 */
describe('PortalListComponent', () => {
  let spy: jasmine.SpyObj<PortalService>;

  /**
   * Builds a minimal {@link Portal} for tests. The real interface declares ~32
   * required fields; enumerating them all in every test adds noise without value,
   * so a partial object is cast via `as Portal` — an accepted test-only convenience
   * under strict TS (production code never casts this way). Only the fields the
   * component actually reads (`portalID`, `portalName`) are seeded by default;
   * callers override whatever they need per test.
   */
  function makePortal(overrides: Partial<Portal>): Portal {
    return { portalID: 1, portalName: 'Test Portal', ...overrides } as Portal;
  }

  beforeEach(() => {
    spy = jasmine.createSpyObj<PortalService>('PortalService', ['list', 'remove']);
    // Default stubs so ngOnInit → load()'s subscribe always has a synchronous
    // source, and onConfirmDelete()'s remove() resolves without a real round-trip.
    spy.list.and.returnValue(of([]));
    spy.remove.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      // Standalone component → provided through `imports`, not `declarations`.
      imports: [PortalListComponent],
      providers: [
        { provide: PortalService, useValue: spy },
        // Supplies the component's injected Router without asserting on navigation.
        provideRouter([]),
      ],
    });
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads portals on init from the service (legacy BindData parity)', () => {
    const rows = [makePortal({ portalID: 1 }), makePortal({ portalID: 2 })];
    spy.list.and.returnValue(of(rows));

    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // triggers ngOnInit → load()
    const component = fixture.componentInstance;

    expect(spy.list).toHaveBeenCalledTimes(1);
    // The initial free-text search term is empty on first load.
    expect(spy.list).toHaveBeenCalledWith({ query: '' });
    expect(component.portals().length).toBe(2);
    // of(rows) resolves synchronously, so `loading` has already flipped to false.
    expect(component.loading()).toBeFalse();
  });

  it('deletes the pending portal and reloads (legacy grdPortals_DeleteCommand parity)', () => {
    spy.list.and.returnValue(of([makePortal({ portalID: 5 })]));

    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // initial load
    const component = fixture.componentInstance;
    spy.list.calls.reset(); // isolate the post-delete reload from the initial load

    const target = makePortal({ portalID: 5, portalName: 'Doomed' });
    component.pendingDelete.set(target);
    component.confirmOpen.set(true);
    component.onConfirmDelete();

    expect(spy.remove).toHaveBeenCalledWith(5);
    expect(component.pendingDelete()).toBeNull();
    expect(component.confirmOpen()).toBeFalse();
    expect(spy.list).toHaveBeenCalledTimes(1); // reload after a successful delete
  });

  it('does nothing on confirm when no delete is pending', () => {
    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    spy.remove.calls.reset();

    component.pendingDelete.set(null);
    component.onConfirmDelete();

    expect(spy.remove).not.toHaveBeenCalled();
  });
});
