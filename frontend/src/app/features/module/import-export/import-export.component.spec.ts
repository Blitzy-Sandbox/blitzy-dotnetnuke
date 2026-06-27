// MIGRATION: Spec for ImportExportComponent (the Angular 19 replacement for DNN Export.ascx.vb + Import.ascx.vb).
// SCOPE BOUNDARY (AAP Section 0.6.2): module content import/export depends on the legacy module-loader
// (IPortable), which is explicitly OUT OF SCOPE, so the screen presents a documented scope-boundary notice
// rather than an export/import form. This spec verifies the component pre-loads the target module (tenant-scoped
// getById with the REQUIRED portalId), renders the scope-boundary notice (no export/import form), and navigates
// back to module settings. Gate 4: ng test --watch=false --browsers=ChromeHeadless --code-coverage (100% pass).
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
    // MIGRATION: multi-tenant scoping (AAP Section 0.7.1) -- getById(id, portalId), portalId from the user's portal (1).
    expect(getByIdSpy).toHaveBeenCalledWith(5, 1);
  });

  it('renders the scope-boundary notice and the target module title, not an export/import form', () => {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.import-export__notice')).not.toBeNull();
    expect(host.querySelector('.import-export__module')?.textContent).toContain('Test Module');
    // MIGRATION: the legacy export/import forms are gone -- module content portability (IPortable via the
    // legacy module-loader) is out of scope per AAP Section 0.6.2.
    expect(host.querySelector('form')).toBeNull();
  });

  it('navigates back to module settings', () => {
    component.back();
    expect(navigateSpy).toHaveBeenCalledWith(['/modules', '5', 'settings']);
  });

  // MIGRATION: [QA F7 #3] fail-closed tenant scoping. When there is no authenticated user, the portalId
  // resolves to -1 (the `auth.currentUser()?.portalId ?? -1` fallback) so the tenant-scoped getById can
  // never silently fetch across portals. Covers the `?? -1` branch.
  it('falls back to portalId -1 for the tenant-scoped load when there is no authenticated user', () => {
    currentUser.set(null);
    getByIdSpy.calls.reset();

    const fx2 = TestBed.createComponent(ImportExportComponent);
    fx2.componentRef.setInput('id', '7');
    fx2.detectChanges();

    expect(getByIdSpy).toHaveBeenCalledWith(7, -1);
  });
});
