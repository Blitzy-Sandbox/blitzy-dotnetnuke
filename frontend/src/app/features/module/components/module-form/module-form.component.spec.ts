import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ModuleFormComponent } from './module-form.component';
import { ModuleService } from '../../services';
import { CreateModuleDto, Module, UpdateModuleDto, VisibilityState } from '../../models';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Unit tests for ModuleFormComponent (Gate 4: ng test --watch=false --browsers=ChromeHeadless).
 *
 * MIGRATION: ModuleFormComponent replaces the legacy Web Forms editor
 * Website/admin/Modules/ModuleSettings.ascx.vb (postback/ViewState/code-behind) with a stateless,
 * standalone Angular 19 reactive form. These tests pin the class-level contract the sibling
 * module.routes.ts, the list component, and the backend ModulesController depend on:
 *   - create mode applies the legacy NEW-module defaults (cboVisibility.SelectedIndex = 0 -> Maximized,
 *     chkAllTabs.Checked = False at ModuleSettings.ascx.vb L225-226; display flags from ModuleInfo.vb ctor);
 *   - submit issues the correct REST call (createModule / updateModule) and returns to /modules;
 *   - server validation failures surface as RFC 7807 ProblemDetails field errors.
 *
 * The component class is exercised in ISOLATION: TestBed.createComponent instantiates it and ngOnInit
 * is invoked MANUALLY. fixture.detectChanges() is intentionally NOT called — rendering the template would
 * require the real FormControlsComponent / ValidationHighlightDirective the component imports, whereas
 * these specs only assert the component's observable behaviour. All ModuleService calls are synchronous
 * (of()/throwError()) so subscriptions resolve before assertions run (no fakeAsync/tick required).
 */

/**
 * Builds a complete Module entity (all 29 wire fields) so getModule returns a realistic record and the
 * edit-submit assertions can prove the `...loaded` spread carried the non-edited fields verbatim.
 */
function buildModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleID: 1,
    portalID: 0,
    tabID: 0,
    tabModuleID: 0,
    moduleDefID: 0,
    moduleOrder: 1,
    paneName: 'ContentPane',
    moduleTitle: 'Sample Module',
    cacheTime: 0,
    alignment: null,
    color: null,
    border: null,
    iconFile: null,
    allTabs: false,
    visibility: VisibilityState.Maximized,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    header: null,
    footer: null,
    startDate: null,
    endDate: null,
    containerSrc: null,
    inheritViewPermissions: false,
    desktopModuleID: 0,
    friendlyName: null,
    description: null,
    version: null,
    isDeleted: false,
    ...overrides,
  };
}

describe('ModuleFormComponent', () => {
  let moduleServiceSpy: jasmine.SpyObj<ModuleService>;
  let routerSpy: jasmine.SpyObj<Router>;
  let routeParams: Params;

  function createComponent(): ModuleFormComponent {
    return TestBed.createComponent(ModuleFormComponent).componentInstance;
  }

  beforeEach(() => {
    routeParams = {};
    moduleServiceSpy = jasmine.createSpyObj<ModuleService>('ModuleService', [
      'getModule',
      'createModule',
      'updateModule',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [ModuleFormComponent],
      providers: [
        { provide: ModuleService, useValue: moduleServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              // Getter so each spec can set `routeParams` BEFORE createComponent()/ngOnInit()
              // and have the snapshot reflect it. convertToParamMap builds a real ParamMap.
              get paramMap() {
                return convertToParamMap(routeParams);
              },
            },
          },
        },
      ],
    });
  });

  it('initializes create-mode defaults and does not load a module', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(component.isEditMode()).toBeFalse();
    expect(moduleServiceSpy.getModule).not.toHaveBeenCalled();
    expect(component.controls.visibility.value).toBe(VisibilityState.Maximized);
    expect(component.controls.allTabs.value).toBeFalse();
    expect(component.controls.displayTitle.value).toBeTrue();
    expect(component.controls.displayPrint.value).toBeTrue();
    expect(component.controls.displaySyndicate.value).toBeFalse();
  });

  it('builds a 23-field CreateModuleDto and navigates to /modules on create submit', () => {
    routeParams = {};
    moduleServiceSpy.createModule.and.returnValue(of(buildModule()));

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue('My Module');
    component.onSubmit();

    expect(moduleServiceSpy.createModule).toHaveBeenCalledTimes(1);
    const request: CreateModuleDto = moduleServiceSpy.createModule.calls.mostRecent().args[0];
    expect(request.moduleTitle).toBe('My Module');
    expect(request.visibility).toBe(VisibilityState.Maximized);
    expect(request.portalID).toBe(0);
    expect(request.inheritViewPermissions).toBeFalse();
    expect(Object.keys(request).length).toBe(23);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
    expect(component.saving()).toBeFalse();
  });

  it('loads the existing module and patches the form in edit mode', () => {
    routeParams = { moduleId: '5' };
    moduleServiceSpy.getModule.and.returnValue(
      of(buildModule({ moduleID: 5, moduleTitle: 'Existing', visibility: VisibilityState.Minimized })),
    );

    const component = createComponent();
    component.ngOnInit();

    expect(moduleServiceSpy.getModule).toHaveBeenCalledWith(5);
    expect(component.isEditMode()).toBeTrue();
    expect(component.controls.moduleTitle.value).toBe('Existing');
    expect(component.controls.visibility.value).toBe(VisibilityState.Minimized);
    expect(component.loading()).toBeFalse();
  });

  it('builds an UpdateModuleDto with the matching moduleID and preserved non-edited fields', () => {
    routeParams = { moduleId: '5' };
    moduleServiceSpy.getModule.and.returnValue(
      of(buildModule({ moduleID: 5, paneName: 'ContentPane', moduleOrder: 3 })),
    );
    moduleServiceSpy.updateModule.and.returnValue(of(buildModule({ moduleID: 5 })));

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue('Updated');
    component.onSubmit();

    const [idArg, requestArg] = moduleServiceSpy.updateModule.calls.mostRecent().args;
    expect(idArg).toBe(5);
    expect(requestArg.moduleID).toBe(5);
    expect(requestArg.moduleTitle).toBe('Updated');
    expect(requestArg.paneName).toBe('ContentPane');
    expect(requestArg.moduleOrder).toBe(3);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('does not call the service when the form is invalid', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue(null);
    component.onSubmit();

    expect(component.form.invalid).toBeTrue();
    expect(moduleServiceSpy.createModule).not.toHaveBeenCalled();
  });

  it('maps RFC 7807 ProblemDetails field errors into serverErrors on a failed create', () => {
    routeParams = {};
    const problem: ProblemDetails = { errors: { moduleTitle: ['Module title already exists.'] } };
    moduleServiceSpy.createModule.and.returnValue(throwError(() => problem));

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue('Dup');
    component.onSubmit();

    expect(component.serverErrors()).toEqual({ moduleTitle: ['Module title already exists.'] });
    expect(component.saving()).toBeFalse();
  });
});
