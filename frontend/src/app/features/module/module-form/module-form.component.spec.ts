// MIGRATION: Spec for the net-new ModuleFormComponent (re-expresses Website/admin/Modules/ModuleSettings.ascx.vb).
// Verifies creation, form pre-load from getById (BindData), the caching-row hidden-when-unsupported conditional
// (L138-139), the visibility enum-as-int binding (L359-363), the save form->DTO mapping (cmdUpdate L343-385), the
// confirm-before-delete flow (cmdDelete L300-312 / L205), and the admin access gate (Page_Load L191-193).
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal, type WritableSignal } from '@angular/core';
import { of } from 'rxjs';

import { ModuleFormComponent } from './module-form.component';
import { ModuleService } from '../module.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { CurrentUser, Module } from '../../../core/models';

function makeModule(overrides: Partial<Module> = {}): Module {
  return {
    moduleId: 5,
    portalId: 1,
    tabId: 10,
    tabModuleId: 7,
    moduleDefId: 3,
    moduleOrder: 1,
    moduleTitle: 'Test Module',
    cacheTime: 0,
    allTabs: false,
    visibility: 0,
    isDeleted: false,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    inheritViewPermissions: true,
    desktopModuleId: 2,
    isPremium: false,
    isAdmin: false,
    supportedFeatures: 0,
    defaultCacheTime: 0,
    moduleControlId: 0,
    controlType: 0,
    supportsPartialRendering: false,
    ...overrides,
  } as Module;
}

function makeUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    userId: 1,
    username: 'admin',
    email: 'admin@example.com',
    displayName: 'Administrator',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    isSuperUser: true,
    portalId: 1,
    roles: ['Administrators'],
    ...overrides,
  };
}

describe('ModuleFormComponent', () => {
  let fixture: ComponentFixture<ModuleFormComponent>;
  let component: ModuleFormComponent;
  let getByIdSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let removeSpy: jasmine.Spy;
  let loadingSignal: WritableSignal<boolean>;
  let selectedSignal: WritableSignal<Module | null>;
  let currentUserSignal: WritableSignal<CurrentUser | null>;

  function createComponent(): void {
    fixture = TestBed.createComponent(ModuleFormComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', '5');
  }

  // The module loads in an effect() (runs after the first render); a second change-detection
  // pass flushes the effect-driven signal updates (loadedModule -> supportsCaching, patched form)
  // into the rendered DOM. getById returns synchronously (of(...)), so no async waiting is needed.
  function setup(): void {
    createComponent();
    fixture.detectChanges();
    fixture.detectChanges();
  }

  beforeEach(() => {
    loadingSignal = signal(false);
    selectedSignal = signal<Module | null>(null);
    currentUserSignal = signal<CurrentUser | null>(makeUser());
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeModule()));
    updateSpy = jasmine.createSpy('update').and.returnValue(of(makeModule()));
    removeSpy = jasmine.createSpy('remove').and.returnValue(of(undefined));

    const moduleServiceStub = {
      selected: selectedSignal,
      loading: loadingSignal,
      modules: signal<Module[]>([]),
      getById: getByIdSpy,
      update: updateSpy,
      remove: removeSpy,
    };
    const authServiceStub = {
      currentUser: currentUserSignal,
    };

    TestBed.configureTestingModule({
      imports: [ModuleFormComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ModuleService, useValue: moduleServiceStub },
        { provide: AuthService, useValue: authServiceStub },
      ],
    });
  });

  it('creates the component', () => {
    setup();
    expect(component).toBeTruthy();
  });

  it('pre-loads the module from getById and patches the form', () => {
    setup();

    expect(getByIdSpy).toHaveBeenCalledWith(5);
    expect(component.form.controls.moduleTitle.value).toBe('Test Module');
  });

  it('hides the cache control when the module does not support caching', () => {
    getByIdSpy.and.returnValue(of(makeModule({ defaultCacheTime: -1 })));
    setup();

    expect(component.supportsCaching()).toBe(false);
    expect((fixture.nativeElement as HTMLElement).querySelector('#cacheTime')).toBeNull();
  });

  it('shows the cache control when the module supports caching', () => {
    getByIdSpy.and.returnValue(of(makeModule({ defaultCacheTime: 120 })));
    setup();

    expect(component.supportsCaching()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).querySelector('#cacheTime')).not.toBeNull();
  });

  it('binds the visibility enum as a number', () => {
    getByIdSpy.and.returnValue(of(makeModule({ visibility: 1 })));
    setup();

    expect(component.form.controls.visibility.value).toBe(1);
  });

  it('maps the form to a DTO and calls update on save', () => {
    setup();

    component.form.controls.moduleTitle.setValue('Renamed Module');
    component.form.controls.allTabs.setValue(true);
    component.submit();

    expect(updateSpy).toHaveBeenCalledTimes(1);
    const [id, dto] = updateSpy.calls.mostRecent().args as [number, Partial<Module>];
    expect(id).toBe(5);
    expect(dto.moduleTitle).toBe('Renamed Module');
    expect(dto.allTabs).toBe(true);
    expect(dto.portalId).toBe(1);
    expect(dto.isDeleted).toBe(false);
    expect(component.saved()).toBe(true);
  });

  it('requires confirmation before deleting, then calls remove and navigates', () => {
    setup();

    component.requestDelete();
    expect(component.showConfirm()).toBe(true);
    expect(removeSpy).not.toHaveBeenCalled();

    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    component.confirmDelete();
    expect(removeSpy).toHaveBeenCalledWith(5);
    expect(navigateSpy).toHaveBeenCalledWith(['/portals']);
    expect(component.showConfirm()).toBe(false);
  });

  it('renders an access-denied state for non-administrators', () => {
    currentUserSignal.set(makeUser({ isSuperUser: false, roles: ['RegisteredUsers'] }));
    setup();

    expect(component.isPortalAdmin()).toBe(false);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.module-form__denied')).not.toBeNull();
    expect(host.querySelector('form')).toBeNull();
  });
});
