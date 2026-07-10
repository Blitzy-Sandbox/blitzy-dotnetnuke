import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PortalListComponent } from './portal-list.component';
import { PortalService } from '../portal.service';
import { Portal } from '../../../core/models';
import { DEFAULT_LIST_PAGE_SIZE } from '../../../core/services/api.service';

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
    // MIGRATION (QA Issues 3 & 13): the list consumes the listWithMeta ({ data, meta })
    // variant so it can read meta.totalCount to drive the server-side pager (totalItems).
    spy = jasmine.createSpyObj<PortalService>('PortalService', ['listWithMeta', 'remove']);
    // Default stubs so ngOnInit → load()'s subscribe always has a synchronous
    // source, and onConfirmDelete()'s remove() resolves without a real round-trip.
    spy.listWithMeta.and.returnValue(of({ data: [], meta: { totalCount: 0 } }));
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

  it('loads a bounded page of portals on init from the service (legacy BindData parity)', () => {
    const rows = [makePortal({ portalID: 1 }), makePortal({ portalID: 2 })];
    spy.listWithMeta.and.returnValue(of({ data: rows, meta: { totalCount: 2 } }));

    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // triggers ngOnInit → load()
    const component = fixture.componentInstance;

    expect(spy.listWithMeta).toHaveBeenCalledTimes(1);
    // MIGRATION (QA Issues 3 & 13): the first load requests server page 1 with the default
    // per-page size and an empty free-text query. Server-side pagination replaced the old
    // first-window (MAX_LIST_PAGE_SIZE) + client-only search, so the pager can walk the whole
    // result set via meta.totalCount.
    expect(spy.listWithMeta).toHaveBeenCalledWith({ query: '', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
    expect(component.portals().length).toBe(2);
    // of(...) resolves synchronously, so `loading` has already flipped to false.
    expect(component.loading()).toBeFalse();
  });

  it('deletes the pending portal and reloads (legacy grdPortals_DeleteCommand parity)', () => {
    spy.listWithMeta.and.returnValue(of({ data: [makePortal({ portalID: 5 })], meta: { totalCount: 1 } }));

    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // initial load
    const component = fixture.componentInstance;
    spy.listWithMeta.calls.reset(); // isolate the post-delete reload from the initial load

    const target = makePortal({ portalID: 5, portalName: 'Doomed' });
    component.pendingDelete.set(target);
    component.confirmOpen.set(true);
    component.onConfirmDelete();

    expect(spy.remove).toHaveBeenCalledWith(5);
    expect(component.pendingDelete()).toBeNull();
    expect(component.confirmOpen()).toBeFalse();
    expect(spy.listWithMeta).toHaveBeenCalledTimes(1); // reload after a successful delete
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

  // MIGRATION (QA Issues 3 & 13): server-side pagination. A pageChange from the table must
  // update the page signal and re-query the server for THAT page, so records beyond the first
  // page are reachable (the core Issue-13 defect: newly created rows past the loaded window).
  it('reloads the requested server page when the table emits pageChange', () => {
    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // initial load (page 1)
    const component = fixture.componentInstance;
    spy.listWithMeta.calls.reset();

    component.onPageChange({ page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });

    expect(component.page()).toBe(2);
    expect(spy.listWithMeta).toHaveBeenCalledTimes(1);
    expect(spy.listWithMeta).toHaveBeenCalledWith({ query: '', page: 2, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // MIGRATION (QA Issues 3 & 13): a new server-side search resets to page 1 and re-queries so
  // the WHOLE dataset is searched (not just the client-side view of the loaded window).
  it('applies a server-side search and resets to page 1 on filterChange', () => {
    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges(); // initial load
    const component = fixture.componentInstance;
    // Simulate the operator having paged forward before searching.
    component.page.set(3);
    spy.listWithMeta.calls.reset();

    component.onFilter({ term: 'acme' });

    expect(component.page()).toBe(1);
    expect(spy.listWithMeta).toHaveBeenCalledWith({ query: 'acme', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // MIGRATION: legacy grdPortals "Portal Aliases" TemplateColumn (portals.ascx L37-43,
  // FormatPortalAliases(PortalID)). The restored column concatenates the backend-supplied
  // aliases via the DataTable `value` accessor.
  it('exposes a "Portal Aliases" column whose value accessor joins the alias host names', () => {
    const fixture = TestBed.createComponent(PortalListComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    const aliasColumn = component.columns.find((c) => c.field === 'aliases');
    expect(aliasColumn).toBeTruthy();
    expect(aliasColumn!.header).toBe('Portal Aliases');
    expect(aliasColumn!.value).toBeDefined();

    // Two aliases are rendered as a comma-separated list...
    const withAliases = makePortal({ aliases: ['a.example', 'www.a.example'] });
    expect(aliasColumn!.value!(withAliases)).toBe('a.example, www.a.example');

    // ...and a portal with no aliases renders an empty cell (no throw on empty/undefined).
    const noAliases = makePortal({ aliases: [] });
    expect(aliasColumn!.value!(noAliases)).toBe('');
  });
});
