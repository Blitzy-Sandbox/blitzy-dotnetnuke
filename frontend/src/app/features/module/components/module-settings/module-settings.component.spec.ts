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
 * Unit tests for ModuleSettingsComponent (Gate 4: ng test --watch=false --browsers=ChromeHeadless).
 *
 * MIGRATION: ModuleSettingsComponent reproduces the configuration concerns of the legacy Web Forms
 * editor Website/admin/Modules/ModuleSettings.ascx.vb (postback/ViewState/code-behind) as a stateless,
 * standalone Angular 19 reactive form. These specs pin the behavioural contract the folder requirement
 * mandates:
 *   1. cache-row hide/show (showCacheRow) — legacy hid rowCache when ModuleDefinition.DefaultCacheTime
 *      was Null.NullInteger;
 *   2. start/end-date null-guards — legacy displayed dates only when Not Null.IsNull(date);
 *   3. the inherit-view-permissions toggle (legacy chkInheritPermissions drove dgPermissions);
 *   4. delete-hidden-for-new — legacy cmdDelete.Visible = False when ModuleId = -1;
 * plus the load+patch, submit-to-UpdateModuleDto + navigation, invalid-form guard, RFC 7807 server-error
 * mapping, and delete/cancel flows.
 *
 * ngOnInit is driven from INSIDE each test via fixture.detectChanges() — AFTER each per-test
 * moduleService.getModule override — so the date-null-guard and date-formatting specs can supply
 * different payloads to exercise the patchForm/toDateInput branches. All ModuleService calls are
 * synchronous (of()/throwError()) so subscriptions resolve before assertions run (no fakeAsync/tick).
 *
 * AuthService is stubbed and provided defensively: the committed template does NOT bind
 * *appHasPermission (the client-side RBAC UI gate is a CP2 deliverable), so the stub is currently inert,
 * but providing it keeps detectChanges() resilient if the RBAC gate is re-applied to the standalone graph.
 */

/**
 * Builds a complete Module entity (all 29 wire fields, camelCase with UPPERCASE "ID") so getModule
 * returns a realistic record and the submit assertions can prove the non-edited fields are carried over
 * verbatim. Satisfies the Module interface with no cast; overrides tailor individual specs.
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

  it('shows the cache row by default (no module definition)', () => {
    fixture.detectChanges();

    expect(component.showCacheRow()).toBeTrue();
  });

  it('hides the cache row when the definition default cache time is null', () => {
    component.moduleDefinition.set({
      moduleDefID: 5,
      friendlyName: 'x',
      desktopModuleID: 7,
      tempModuleID: 0,
      defaultCacheTime: null,
    });

    expect(component.showCacheRow()).toBeFalse();
  });

  it('shows the cache row when the definition default cache time is set', () => {
    component.moduleDefinition.set({
      moduleDefID: 5,
      friendlyName: 'x',
      desktopModuleID: 7,
      tempModuleID: 0,
      defaultCacheTime: 60,
    });

    expect(component.showCacheRow()).toBeTrue();
  });

  it('converts null start/end dates into empty form inputs', () => {
    moduleService.getModule.and.returnValue(of(createModule({ startDate: null, endDate: null })));

    fixture.detectChanges();

    expect(component.form.controls.startDate.value).toBe('');
    expect(component.form.controls.endDate.value).toBe('');
  });

  it('converts ISO start/end dates into yyyy-MM-dd form inputs', () => {
    moduleService.getModule.and.returnValue(
      of(createModule({ startDate: '2024-05-01T00:00:00', endDate: '2024-06-15T00:00:00' })),
    );

    fixture.detectChanges();

    expect(component.form.controls.startDate.value).toBe('2024-05-01');
    expect(component.form.controls.endDate.value).toBe('2024-06-15');
  });

  it('allows delete for a saved module and forbids it for a new one', () => {
    fixture.detectChanges();

    expect(component.canDelete()).toBeTrue();

    component.moduleId.set(-1);
    expect(component.canDelete()).toBeFalse();
  });

  it('reflects the inherit-permissions toggle in the form', () => {
    fixture.detectChanges();

    component.form.controls.inheritViewPermissions.setValue(false);

    expect(component.form.controls.inheritViewPermissions.value).toBeFalse();
  });

  it('maps the form to an UpdateModuleDto and navigates to /modules on submit', () => {
    fixture.detectChanges();

    component.form.controls.cacheTime.setValue(240);
    component.form.controls.startDate.setValue('2024-05-01');
    component.form.controls.endDate.setValue('');
    component.form.controls.containerSrc.setValue('~/c.ascx');
    component.form.controls.inheritViewPermissions.setValue(false);
    component.form.controls.displayTitle.setValue(false);

    component.onSubmit();

    expect(moduleService.updateModule).toHaveBeenCalledTimes(1);
    const call = moduleService.updateModule.calls.mostRecent();
    expect(call.args[0]).toBe(1);
    const dto: UpdateModuleDto = call.args[1];
    expect(dto.moduleID).toBe(1);
    expect(dto.cacheTime).toBe(240);
    expect(dto.startDate).toBe('2024-05-01');
    expect(dto.endDate).toBeNull();
    expect(dto.containerSrc).toBe('~/c.ascx');
    expect(dto.inheritViewPermissions).toBeFalse();
    expect(dto.displayTitle).toBeFalse();
    expect(dto.moduleOrder).toBe(1);
    expect(dto.paneName).toBe('ContentPane');
    expect(dto.visibility).toBe(VisibilityState.Maximized);
    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
    expect(component.saving()).toBeFalse();
  });

  it('does not call the service when the form is invalid', () => {
    fixture.detectChanges();

    component.form.controls.cacheTime.setValue(-5);
    component.onSubmit();

    expect(moduleService.updateModule).not.toHaveBeenCalled();
    expect(component.form.controls.cacheTime.touched).toBeTrue();
  });

  it('maps RFC 7807 server errors into serverErrors on a failed update', () => {
    const problem: ProblemDetails = { errors: { cacheTime: ['Invalid cache time'] } };
    moduleService.updateModule.and.returnValue(throwError(() => problem));

    fixture.detectChanges();
    component.onSubmit();

    expect(component.serverErrors()).toEqual({ cacheTime: ['Invalid cache time'] });
    expect(component.saving()).toBeFalse();
  });

  it('opens the confirmation dialog and deletes the module on confirm', () => {
    fixture.detectChanges();

    component.onDeleteClick();
    expect(component.showDeleteDialog()).toBeTrue();

    component.onConfirmDelete();
    expect(component.showDeleteDialog()).toBeFalse();
    expect(moduleService.deleteModule).toHaveBeenCalledWith(1);
    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
  });

  it('closes the confirmation dialog on cancel without deleting', () => {
    fixture.detectChanges();

    component.onDeleteClick();
    component.onCancelDelete();

    expect(component.showDeleteDialog()).toBeFalse();
    expect(moduleService.deleteModule).not.toHaveBeenCalled();
  });

  it('navigates back to /modules on cancel', () => {
    component.onCancel();

    expect(router.navigate).toHaveBeenCalledWith(['/modules']);
  });
});
