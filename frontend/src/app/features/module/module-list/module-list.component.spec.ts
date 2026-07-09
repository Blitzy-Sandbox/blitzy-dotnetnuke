import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { ModuleListComponent } from './module-list.component';
import { ModuleService } from '../module.service';
import { Module } from '../../../core/models';
import { MAX_LIST_PAGE_SIZE } from '../../../core/services/api.service';

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
    // QA finding (Report 6, Issue 1): the list now consumes the bounded getModulesWithMeta
    // ({ data, meta }) variant so it can read meta.totalCount for the truncation hint.
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

  it('loads a bounded page of modules on init via getModulesWithMeta()', () => {
    const rows = [makeModule({ moduleID: 1 }), makeModule({ moduleID: 2, moduleTitle: 'Second' })];
    serviceSpy.getModulesWithMeta.and.returnValue(of({ data: rows, meta: { totalCount: 2 } }));

    const component = setup();

    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledTimes(1);
    // QA finding (Report 6, Issue 1): the initial load requests one bounded page (the backend cap).
    expect(serviceSpy.getModulesWithMeta).toHaveBeenCalledWith({ pageSize: MAX_LIST_PAGE_SIZE });
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

  it('navigates to the edit form on an edit row action', () => {
    const component = setup();
    const navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const target = makeModule({ moduleID: 42 });

    component.onRowAction({ action: 'edit', row: target });

    expect(navigateSpy).toHaveBeenCalledWith(['/modules', 42]);
  });
});
