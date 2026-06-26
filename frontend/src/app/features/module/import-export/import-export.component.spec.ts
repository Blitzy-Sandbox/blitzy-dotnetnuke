// MIGRATION: Spec for ImportExportComponent (the Angular 19 replacement for DNN Export.ascx.vb + Import.ascx.vb).
// CONTRACT ALIGNMENT (review CP3, AAP 0.3.4): the frozen, authoritative backend exposes NO module export/import
// endpoint (ModulesController is CRUD + by-portal/by-tab only), so the workflow is DEFERRED to an in-page
// notice. This spec verifies the component pre-loads the target module (tenant-scoped getById with the REQUIRED
// portalId), renders the deferral notice (no export/import form), and navigates back to module settings.
// Gate 4: ng test --watch=false --browsers=ChromeHeadless --code-coverage (100% pass, non-interactive).
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { ImportExportComponent } from './import-export.component';
import { ModuleService } from '../module.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { CurrentUser, Module } from '../../../core/models';

function makeModule(overrides: Partial<Module> = {}): Module {
  const base = {
    moduleId: 5,
    portalId: 1,
    tabId: 10,
    moduleOrder: 1,
    moduleTitle: 'Test Module',
    moduleName: 'Test Module',
    cacheTime: 0,
    allTabs: false,
    visibility: 0,
    isDeleted: false,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    inheritViewPermissions: true,
    defaultCacheTime: 0,
    controlType: 0,
  };
  return { ...base, ...overrides } as Module;
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

describe('ImportExportComponent', () => {
  let fixture: ComponentFixture<ImportExportComponent>;
  let component: ImportExportComponent;
  let selected: WritableSignal<Module | null>;
  let loading: WritableSignal<boolean>;
  let currentUser: WritableSignal<CurrentUser | null>;
  let getByIdSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    selected = signal<Module | null>(makeModule());
    loading = signal(false);
    currentUser = signal<CurrentUser | null>(makeUser());
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeModule()));

    const moduleServiceStub = {
      selected,
      loading,
      getById: getByIdSpy,
    };
    const authServiceStub = {
      currentUser,
    };

    TestBed.configureTestingModule({
      imports: [ImportExportComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ModuleService, useValue: moduleServiceStub },
        { provide: AuthService, useValue: authServiceStub },
      ],
    });

    fixture = TestBed.createComponent(ImportExportComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
  });

  it('creates the component and pre-loads the module with the tenant portalId', () => {
    expect(component).toBeTruthy();
    // MIGRATION: multi-tenant scoping (review CP3) -- getById(id, portalId), portalId from the user's portal (1).
    expect(getByIdSpy).toHaveBeenCalledWith(5, 1);
  });

  it('renders the deferral notice and the target module title, not an export/import form', () => {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.import-export__notice')).not.toBeNull();
    expect(host.querySelector('.import-export__module')?.textContent).toContain('Test Module');
    // MIGRATION: the legacy export/import forms are gone -- the workflow is deferred (no backend endpoint).
    expect(host.querySelector('form')).toBeNull();
  });

  it('navigates back to module settings', () => {
    component.back();
    expect(navigateSpy).toHaveBeenCalledWith(['/modules', '5', 'settings']);
  });
});
