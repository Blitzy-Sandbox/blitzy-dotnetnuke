import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ModuleSettingsComponent } from './module-settings.component';
import { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { Module, UpdateModuleDto, VisibilityState } from '../../models';
import { ModuleService } from '../../services';

/**
 * Builds a fully-typed `Module` fixture (all 29 wire fields present, no `any`). Field names and
 * nullability mirror the REAL `../../models` `Module` interface exactly — ID/number fields are
 * `number`, the text fields are `string | null`, `visibility` is the numeric `VisibilityState`
 * enum, the placement/display flags are `boolean`, and the dates are ISO `string | null`.
 * Overrides are spread last so a single spec can tweak only the field it asserts on while every
 * required member stays present, letting the factory satisfy `Module` with no `as` cast.
 */
function createModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleID: 1,
    portalID: 0,
    tabID: 10,
    tabModuleID: 100,
    moduleDefID: 5,
    moduleOrder: 1,
    paneName: 'ContentPane',
    moduleTitle: 'Announcements',
    cacheTime: 120,
    alignment: 'left',
    color: '',
    border: '',
    iconFile: '',
    allTabs: false,
    visibility: VisibilityState.Maximized,
    displayTitle: true,
    displayPrint: false,
    displaySyndicate: false,
    header: null,
    footer: null,
    startDate: null,
    endDate: null,
    containerSrc: '~/Portals/_default/Containers/Default/Blue.ascx',
    inheritViewPermissions: true,
    desktopModuleID: 7,
    friendlyName: 'Announcements',
    description: 'Announcements module',
    version: '01.00.00',
    isDeleted: false,
    ...overrides,
  };
}

describe('ModuleSettingsComponent', () => {
  let fixture: ComponentFixture<ModuleSettingsComponent>;
  let component: ModuleSettingsComponent;
  let moduleService: jasmine.SpyObj<ModuleService>;
  let router: jasmine.SpyObj<Router>;

  // MIGRATION: the OnPush template gates its body behind `*appHasPermission`, whose directive
  // injects AuthService and, inside an effect, reads `currentUser()` and calls `hasRole()`. The
  // stub supplies both so `fixture.detectChanges()` renders the gated content instead of throwing
  // a NullInjectorError. `hasRole` returns true so every permission-gated affordance renders.
  const authServiceStub = {
    currentUser: signal<{ userId: number; username: string; roles: string[] } | null>({
      userId: 1,
      username: 'admin',
      roles: ['Administrators'],
    }),
    hasRole: jasmine.createSpy('hasRole').and.returnValue(true),
  };

  beforeEach(async () => {
    moduleService = jasmine.createSpyObj<ModuleService>('ModuleService', [
      'getModule',
      'updateModule',
      'deleteModule',
    ]);
    moduleService.getModule.and.returnValue(of(createModule()));
    moduleService.updateModule.and.returnValue(of(createModule()));
    moduleService.deleteModule.and.returnValue(of(void 0));

    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [ModuleSettingsComponent],
      providers: [
        { provide: ModuleService, useValue: moduleService },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ moduleId: '1' }) } },
        },
        { provide: AuthService, useValue: authServiceStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ModuleSettingsComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
  });

  it('loads the module on init and patches the form', () => {
    fixture.detectChanges();

    expect(moduleService.getModule).toHaveBeenCalledWith(1);
    expect(component.form.controls.cacheTime.value).toBe(120);
    expect(component.form.controls.containerSrc.value).toBe(
      '~/Portals/_default/Containers/Default/Blue.ascx',
    );
    expect(component.form.controls.inheritViewPermissions.value).toBeTrue();
    expect(component.form.controls.displayTitle.value).toBeTrue();
  });

  it('shows the cache row by default', () => {
    fixture.detectChanges();

    // No ModuleDefinition is loaded (the 5-method ModuleService exposes no definition endpoint),
    // so the legacy `rowCache` stays visible.
    expect(component.showCacheRow()).toBeTrue();
  });

  it('hides the cache row when defaultCacheTime is null', () => {
    // MIGRATION: legacy `If objModuleDef.DefaultCacheTime = Null.NullInteger Then rowCache.Visible = False`.
    component.moduleDefinition.set({
      moduleDefID: 5,
      friendlyName: 'x',
      desktopModuleID: 7,
      tempModuleID: 0,
      defaultCacheTime: null,
    });

    expect(component.showCacheRow()).toBeFalse();
  });

  it('shows the cache row when defaultCacheTime is set', () => {
    component.moduleDefinition.set({
      moduleDefID: 5,
      friendlyName: 'x',
      desktopModuleID: 7,
      tempModuleID: 0,
      defaultCacheTime: 60,
    });

    expect(component.showCacheRow()).toBeTrue();
  });

  it('converts null dates to empty inputs', () => {
    moduleService.getModule.and.returnValue(of(createModule({ startDate: null, endDate: null })));

    fixture.detectChanges();

    expect(component.form.controls.startDate.value).toBe('');
    expect(component.form.controls.endDate.value).toBe('');
  });

  it('converts ISO dates to yyyy-MM-dd', () => {
    moduleService.getModule.and.returnValue(
      of(createModule({ startDate: '2024-05-01T00:00:00', endDate: '2024-06-15T00:00:00' })),
    );

    fixture.detectChanges();

    expect(component.form.controls.startDate.value).toBe('2024-05-01');
    expect(component.form.controls.endDate.value).toBe('2024-06-15');
  });

  it('exposes canDelete only for a saved module', () => {
    fixture.detectChanges();

    expect(component.canDelete()).toBeTrue();

    // MIGRATION: legacy `cmdDelete.Visible = False` when ModuleId = -1 (a new/unsaved module).
    component.moduleId.set(-1);

    expect(component.canDelete()).toBeFalse();
  });

  it('reflects the inherit-permissions toggle', () => {
    fixture.detectChanges();

    component.form.controls.inheritViewPermissions.setValue(false);

    expect(component.form.controls.inheritViewPermissions.value).toBeFalse();
  });

  it('maps the form to an UpdateModuleDto and navigates on submit', () => {
    fixture.detectChanges();

    component.form.controls.cacheTime.setValue(240);
    component.form.controls.startDate.setValue('2024-05-01');
    component.form.controls.endDate.setValue('');
    component.form.controls.containerSrc.setValue('~/c.ascx');
    component.form.controls.inheritViewPermissions.setValue(false);
    component.form.controls.displayTitle.setValue(false);

    component.onSubmit();

    expect(moduleService.updateModule).toHaveBeenCalledTimes(1);
    const [idArg, dto]: [number, UpdateModuleDto] =
      moduleService.updateModule.calls.mostRecent().args;
    expect(idArg).toBe(1);
    expect(dto.moduleID).toBe(1);
    expect(dto.cacheTime).toBe(240);
    expect(dto.startDate).toBe('2024-05-01');
    // MIGRATION: legacy empty date -> Null.NullDate; an empty input maps to null here.
    expect(dto.endDate).toBeNull();
    expect(dto.containerSrc).toBe('~/c.ascx');
    expect(dto.inheritViewPermissions).toBeFalse();
    expect(dto.displayTitle).toBeFalse();
    // Non-edited fields are carried over verbatim from the loaded module.
    expect(dto.moduleOrder).toBe(1);
    expect(dto.paneName).toBe('ContentPane');
    expect(dto.visibility).toBe(VisibilityState.Maximized);
    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('does not call the service when the form is invalid', () => {
    fixture.detectChanges();

    // Violates `Validators.min(0)` on cacheTime.
    component.form.controls.cacheTime.setValue(-5);
    component.onSubmit();

    expect(moduleService.updateModule).not.toHaveBeenCalled();
    expect(component.form.controls.cacheTime.touched).toBeTrue();
  });

  it('maps RFC 7807 server errors into serverErrors on submit failure', () => {
    const problem: ProblemDetails = { errors: { cacheTime: ['Invalid cache time'] } };
    moduleService.updateModule.and.returnValue(throwError(() => problem));

    fixture.detectChanges();
    component.onSubmit();

    expect(component.serverErrors()).toEqual({ cacheTime: ['Invalid cache time'] });
    expect(component.saving()).toBeFalse();
  });

  it('runs the delete flow through the confirmation dialog', () => {
    fixture.detectChanges();

    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();

    component.onConfirmDelete();
    expect(moduleService.deleteModule).toHaveBeenCalledWith(1);
    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('closes the delete dialog without deleting on cancel', () => {
    component.onDeleteClick();
    component.onCancelDelete();

    expect(component.showDeleteDialog()).toBeFalse();
    expect(moduleService.deleteModule).not.toHaveBeenCalled();
  });

  it('navigates back to the module list on cancel', () => {
    component.onCancel();

    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
  });
});
