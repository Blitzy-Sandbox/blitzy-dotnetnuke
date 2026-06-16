import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Params, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ModuleFormComponent } from './module-form.component';
import { ModuleService } from '../../services';
import { CreateModuleDto, Module, UpdateModuleDto, VisibilityState } from '../../models';
import { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Builds a fully-typed `Module` fixture (all 29 wire fields present, no `any`). Field names and
 * nullability mirror the REAL `../../models` `Module` interface exactly — ID/number fields are
 * `number`, string fields are `string | null`, `visibility` is the numeric `VisibilityState`
 * enum, the display/placement flags are `boolean`, and the dates are ISO `string | null`. The
 * sibling `module.service.spec.ts` factory uses the same shape. Overrides are spread last so a
 * single spec can tweak only the fields it asserts on while every required member stays present.
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

  // Instantiates the standalone component WITHOUT running change detection. We deliberately do
  // NOT call `fixture.detectChanges()`: rendering the template would require the real
  // `FormControlsComponent`/`ValidationHighlightDirective`, whereas these specs exercise the
  // component class in isolation. `ngOnInit()` is therefore invoked MANUALLY by each spec after
  // `routeParams` has been arranged for the desired create/edit branch.
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
          // Getter-based `paramMap` so a spec can mutate `routeParams` BEFORE
          // `createComponent()`/`ngOnInit()` and have the snapshot reflect it. `convertToParamMap`
          // produces a real `ParamMap`, exactly as the router would at runtime.
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              get paramMap() {
                return convertToParamMap(routeParams);
              },
            },
          },
        },
      ],
    });
  });

  it('initializes create mode with the legacy NEW-module defaults', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(component.isEditMode()).toBeFalse();
    expect(moduleServiceSpy.getModule).not.toHaveBeenCalled();
    // MIGRATION: ModuleSettings.ascx.vb NEW-module defaults (Maximized visibility, AllTabs off)
    // plus ModuleInfo.vb ctor display-flag defaults (DisplayTitle/DisplayPrint on, Syndicate off).
    expect(component.controls.visibility.value).toBe(VisibilityState.Maximized);
    expect(component.controls.allTabs.value).toBeFalse();
    expect(component.controls.displayTitle.value).toBeTrue();
    expect(component.controls.displayPrint.value).toBeTrue();
    expect(component.controls.displaySyndicate.value).toBeFalse();
  });

  it('submits a 23-field CreateModuleDto and navigates to the module list on create', () => {
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
    // 12 edited fields + 11 non-edited structural defaults = the full create contract.
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

  it('submits an UpdateModuleDto that preserves non-edited fields on edit', () => {
    routeParams = { moduleId: '5' };
    moduleServiceSpy.getModule.and.returnValue(
      of(buildModule({ moduleID: 5, paneName: 'ContentPane', moduleOrder: 3 })),
    );
    moduleServiceSpy.updateModule.and.returnValue(of(buildModule({ moduleID: 5 })));

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue('Updated');
    component.onSubmit();

    const [idArg, requestArg]: [number, UpdateModuleDto] =
      moduleServiceSpy.updateModule.calls.mostRecent().args;
    expect(idArg).toBe(5);
    expect(requestArg.moduleID).toBe(5);
    expect(requestArg.moduleTitle).toBe('Updated');
    // `...loaded` spread carried the non-edited fields through to the update payload.
    expect(requestArg.paneName).toBe('ContentPane');
    expect(requestArg.moduleOrder).toBe(3);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('blocks the service call when the form is invalid', () => {
    routeParams = {};

    const component = createComponent();
    component.ngOnInit();
    component.controls.moduleTitle.setValue(null);
    component.onSubmit();

    expect(component.form.invalid).toBeTrue();
    expect(moduleServiceSpy.createModule).not.toHaveBeenCalled();
  });

  it('maps RFC 7807 ProblemDetails errors into serverErrors on create failure', () => {
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
