import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ModuleListComponent } from './module-list.component';
import { ModuleService } from '../module.service';
import { Module } from '../../../core/models';
import { DEFAULT_LIST_PAGE_SIZE } from '../../../core/services/api.service';

/**
 * Unit tests for {@link ModuleListComponent} — the Angular 19 standalone screen that
 * reproduces the legacy DotNetNuke module inventory admin grid
 * (Website/admin/Modules/**) and its delete-with-confirm workflow.
 *
 * Backs AAP Validation Gate 4 (`ng test --watch=false --browsers=ChromeHeadless
 * --code-coverage`). All of the component's view-model signals and event handlers are
 * PUBLIC, so these specs drive/assert them directly. HTTP is fully mocked at the
 * ModuleService boundary (a `jasmine.SpyObj`), and routing is supplied via
 * `provideRouter([])`; consequently NO `HttpClientTestingModule`/`provideHttpClient`
 * is required. Synchronous `of(...)` stubs let `fixture.detectChanges()` complete
 * `ngOnInit -> load()` synchronously — no `fakeAsync`/`tick`.
 */

// Minimal Module factory — every field of the Module model is required, so build a complete
// baseline and allow per-test overrides. Cast through the interface (no `any`).
function makeModule(overrides: Partial<Module> = {}): Module {
  const base: Module = {
    moduleID: 1,
    tabModuleID: 1,
    portalID: 0,
    tabID: 1,
    moduleDefID: 1,
    desktopModuleID: 1,
    moduleTitle: 'Sample Module',
    friendlyName: 'HTML',
    paneName: 'ContentPane',
    moduleName: 'HTML',
    controlSrc: 'DesktopModules/HTML/HTML.ascx',
    folderName: 'HTML',
    description: '',
    version: '1.0.0',
    moduleOrder: 1,
    cacheTime: 0,
    alignment: '',
    color: '',
    border: '',
    iconFile: '',
    header: '',
    footer: '',
    containerSrc: '',
    allTabs: false,
    displayTitle: true,
    displayPrint: false,
    displaySyndicate: false,
    inheritViewPermissions: true,
    visibility: 0,
    startDate: '',
    endDate: '',
  };
  return { ...base, ...overrides };
}

describe('ModuleListComponent', () => {
  let serviceSpy: jasmine.SpyObj<ModuleService>;

  function setup(): ModuleListComponent {
    const fixture = TestBed.createComponent(ModuleListComponent);
    fixture.detectChanges(); // triggers ngOnInit -> load()
    return fixture.componentInstance;
  }

  beforeEach(() => {
    // MIGRATION (QA Issues 3 & 13): the list consumes the getModulesWithMeta ({ data, meta })
    // variant so it can read meta.totalCount to drive the server-side pager (totalItems).
    serviceSpy = jasmine.createSpyObj<ModuleService>('ModuleService', ['getModulesWithMeta', 'deleteModule']);
    serviceSpy.getModulesWithMeta.and.returnValue(of({ data: [], meta: { totalCount: 0 } }));
    serviceSpy.deleteModule.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [ModuleListComponent],
      providers: [
        { provide: ModuleService, useValue: serviceSpy },
        provideRouter([]),
      ],
    });
  });

  it('creates the component', () => {
    const component = setup();
    expect(component).toBeTruthy();
  });

  // QA finding P7-1 (Report 11): the inventory add control uses the standardized
  // "Add {Entity}" wording, and its aria-label matches the visible text exactly so
  // no WCAG 2.5.3 label-in-name mismatch is introduced.
  it('renders the add button with matching "Add Module" text and aria-label (P7-1)', () => {
    const fixture = TestBed.createComponent(ModuleListComponent);
    fixture.detectChanges();

    const addButton = fixture.nativeElement.querySelector('.module-list__add') as HTMLButtonElement;
    expect(addButton).toBeTruthy();
    expect(addButton.textContent?.trim()).toBe('Add Module');
    expect(addButton.getAttribute('aria-label')).toBe('Add Module');
  });

  it('loads the first server page of modules on init via getModulesWithMeta()', () => {
    const rows = [makeModule({ moduleID: 1 }), makeModule({ moduleID: 2, moduleTitle: 'Second' })];
    serviceSpy.getModulesWithMeta.and.returnValue(of({ data: rows, meta: { totalCount: 2 } }));

    const component = setup();

    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledTimes(1);
    // MIGRATION (QA Issues 3 & 13): the initial load requests server page 1 with the default
    // per-page size and an empty query. Server-side pagination/search replaced the old
    // first-window (MAX_LIST_PAGE_SIZE) + client-only filtering.
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ query: '', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
    expect(component.modules().length).toBe(2);
    expect(component.loading()).toBeFalse();
    expect(component.error()).toBeNull();
  });

  it('opens the confirmation dialog on a delete row action', () => {
    const component = setup();
    const target = makeModule({ moduleID: 7, moduleTitle: 'Deletable' });

    component.onRowAction({ action: 'delete', row: target });

    expect(component.pendingDelete()).toBe(target);
    expect(component.confirmOpen()).toBeTrue();
    expect(serviceSpy.deleteModule).not.toHaveBeenCalled();
  });

  it('deletes the pending module on confirm and reloads the list', () => {
    const target = makeModule({ moduleID: 9 });
    const component = setup();
    serviceSpy.getModulesWithMeta.calls.reset(); // ignore the ngOnInit load

    component.onRowAction({ action: 'delete', row: target });
    component.onConfirmDelete();

    expect(serviceSpy.deleteModule).toHaveBeenCalledWith(9);
    expect(component.pendingDelete()).toBeNull();
    expect(component.confirmOpen()).toBeFalse();
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledTimes(1); // reload after delete
  });

  // QA R10 Issue 5: deleting the LAST row on a page beyond page 1 must step back one page so the
  // user is not stranded on a now-empty page; the reload then re-queries the previous, populated page.
  it('steps back one page when the deleted row was the last on a page beyond page 1', () => {
    const target = makeModule({ moduleID: 41, moduleTitle: 'Only row on page 2' });
    const component = setup();
    // Simulate being on page 2 with exactly one loaded row (the last on that page).
    component.page.set(2);
    component.modules.set([target]);
    serviceSpy.getModulesWithMeta.calls.reset(); // ignore the ngOnInit load

    component.onRowAction({ action: 'delete', row: target });
    component.onConfirmDelete();

    expect(serviceSpy.deleteModule).toHaveBeenCalledWith(41);
    // Page stepped back from 2 -> 1, and the reload queried the previous, populated page.
    expect(component.page()).toBe(1);
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ query: '', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // QA R10 Issue 5 (boundary): deleting the last row while ALREADY on page 1 must NOT go to page 0;
  // it stays on page 1 and reloads.
  it('does not step below page 1 when deleting the last row on page 1', () => {
    const target = makeModule({ moduleID: 3 });
    const component = setup();
    component.page.set(1);
    component.modules.set([target]);
    serviceSpy.getModulesWithMeta.calls.reset();

    component.onRowAction({ action: 'delete', row: target });
    component.onConfirmDelete();

    expect(component.page()).toBe(1);
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ query: '', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  it('navigates to the edit form on an edit row action', () => {
    const component = setup();
    const navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const target = makeModule({ moduleID: 42 });

    component.onRowAction({ action: 'edit', row: target });

    expect(navigateSpy).toHaveBeenCalledWith(['/modules', 42]);
  });

  // MIGRATION (QA Issues 3 & 13): a new server-side search resets to page 1 and re-queries so
  // the ENTIRE module set is searched (not just the loaded page).
  it('applies a server-side search and resets to page 1 on filterChange', () => {
    const component = setup();
    component.page.set(5); // simulate the operator having paged forward first
    serviceSpy.getModulesWithMeta.calls.reset();

    component.onFilterChange({ term: 'html' });

    expect(component.page()).toBe(1);
    expect(component.query()).toBe('html');
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ query: 'html', page: 1, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });

  // MIGRATION (QA Issues 3 & 13): a pageChange updates the page signal and re-queries the
  // server for that page, so modules beyond the first page are reachable (server-side paging).
  it('reloads the requested server page when the table emits pageChange', () => {
    const component = setup();
    serviceSpy.getModulesWithMeta.calls.reset();

    component.onPageChange({ page: 3, pageSize: DEFAULT_LIST_PAGE_SIZE });

    expect(component.page()).toBe(3);
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ query: '', page: 3, pageSize: DEFAULT_LIST_PAGE_SIZE });
  });
});
