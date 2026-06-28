// MIGRATION: Spec for the net-new ModuleFormComponent (re-expresses Website/admin/Modules/ModuleSettings.ascx.vb).
// Verifies creation, form pre-load from getById (BindData), the caching-row hidden-when-unsupported conditional
// (L138-139), the visibility enum-as-int binding (L359-363), the save form->DTO mapping (cmdUpdate L343-385), the
// confirm-before-delete flow (cmdDelete L300-312 / L205), and the admin access gate (Page_Load L191-193).
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal, type WritableSignal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { ModuleFormComponent } from './module-form.component';
import { ModuleService } from '../module.service';
import type { ModuleUpdateRequest } from '../module.service';
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

    // MIGRATION: getById signature is getById(id, portalId) -- portalId (tenant query) is sourced at load from
    // the authenticated user's portal (makeUser -> 1) (review CP3).
    expect(getByIdSpy).toHaveBeenCalledWith(5, 1);
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
    // MIGRATION: update signature is update(id, portalId, dto) -- portalId is the tenant query param (review CP3).
    const [id, portalId, dto] = updateSpy.calls.mostRecent().args as [
      number,
      number,
      ModuleUpdateRequest,
    ];
    expect(id).toBe(5);
    // portalId resolves to the loaded module's PortalId (makeModule -> 1).
    expect(portalId).toBe(1);
    expect(dto.moduleTitle).toBe('Renamed Module');
    expect(dto.allTabs).toBe(true);
    // MIGRATION: DTO drift fix (review CP3) -- the permission collection is sent as `permissions` (NOT
    // `modulePermissions`); the backend read omits the collection, so it starts empty. portalId / moduleId /
    // isDeleted are no longer body fields (portalId -> query, moduleId -> route, isDeleted not in contract).
    expect(dto.permissions).toEqual([]);
    expect('portalId' in dto).toBe(false);
    expect('moduleId' in dto).toBe(false);
    expect('isDeleted' in dto).toBe(false);
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
    // MIGRATION: remove signature is remove(id, portalId) -- portalId resolves to the loaded module's PortalId
    // (makeModule -> 1) (review CP3).
    expect(removeSpy).toHaveBeenCalledWith(5, 1);
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

  // MIGRATION: (QA Issue 1, secondary impact) a 500 carries an empty `errors` object (not a flat array), so
  // the form-level summary must fall back to the RFC 7807 title + detail. Previously errorSummary returned []
  // for an object-shaped `errors` and the server error rendered nowhere.
  it('surfaces the RFC 7807 title and detail in the error summary on a 500', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'An unexpected error occurred.',
      status: 500,
      detail: 'Boom.',
      errors: {},
    };
    updateSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 500 })));
    setup();

    component.submit();

    expect(component.problem()?.status).toBe(500);
    expect(component.errorSummary()).toEqual(['An unexpected error occurred.', 'Boom.']);

    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const alert = host.querySelector('.module-form__errors');
    expect(alert?.textContent).toContain('An unexpected error occurred.');
    expect(alert?.textContent).toContain('Boom.');
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #13] the border control mirrors the backend
  // Matches(@"^[0-9]$") rule: a multi-digit / non-numeric value is invalid (pattern error), a single 0-9
  // digit is valid, and an EMPTY value is valid (Angular skips empty inputs, matching the server `.When(!empty)`).
  it('applies the single-digit border pattern validator (Issue 13)', () => {
    setup();

    const border = component.form.controls.border;

    border.setValue('10');
    expect(border.invalid).toBe(true);
    expect(border.errors?.['pattern']).toBeTruthy();

    border.setValue('x');
    expect(border.invalid).toBe(true);
    expect(border.errors?.['pattern']).toBeTruthy();

    border.setValue('5');
    expect(border.valid).toBe(true);

    // empty is valid -- the rule only applies when a value is present (parity with the server `.When(!empty)`).
    border.setValue('');
    expect(border.valid).toBe(true);
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #13] the [messages] override renders the EXACT legacy/server
  // wording inline once the border is invalid AND touched, so the client message matches the backend verbatim.
  it('renders the exact legacy border message inline when the border is invalid and touched (Issue 13)', () => {
    setup();

    component.form.controls.border.setValue('10');
    component.form.controls.border.markAsTouched();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const errorTexts = Array.from(host.querySelectorAll('.form-control__error')).map(
      (el) => el.textContent ?? '',
    );
    expect(errorTexts.some((t) => t.includes('Invalid Border (must be a number between 0 and 9)'))).toBe(
      true,
    );
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #11] a failed initial GET must NOT leave a blank/default
  // interactive form (overwrite risk) and must NOT surface an uncaught HttpErrorResponse. The error is caught:
  // loadFailed gates the form OFF, a load-error banner is shown, and errorContext is 'load'.
  it('withholds the form and shows a load-error banner when the initial GET fails (Issue 11)', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'Internal Server Error',
      status: 500,
      detail: 'Database unavailable.',
      errors: {},
    };
    getByIdSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ error: problem, status: 500 })),
    );
    setup();

    // The error handler ran (loadFailed + problem set) -- i.e. the error was CAUGHT, not uncaught.
    expect(component.loadFailed()).toBe(true);
    expect(component.errorContext()).toBe('load');
    expect(component.problem()?.status).toBe(500);

    const host = fixture.nativeElement as HTMLElement;
    // No editable form is rendered, and there are no Update/Delete buttons to submit blank values.
    expect(host.querySelector('form')).toBeNull();
    expect(host.querySelector('#border')).toBeNull();
    // The load-error banner + lead is shown instead.
    const lead = host.querySelector('.module-form__load-error-lead');
    expect(lead).not.toBeNull();
    expect(lead?.textContent).toContain('could not be loaded');
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #11] a SUCCESSFUL load clears any prior load-failure state and
  // renders the editable form (regression guard for the loadFailed reset added to loadModule).
  it('renders the editable form on a successful load (Issue 11 regression guard)', () => {
    setup();

    expect(component.loadFailed()).toBe(false);
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('form')).not.toBeNull();
    expect(host.querySelector('.module-form__load-error-lead')).toBeNull();
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #12] the error banner lead is context-aware. A failed SAVE
  // reads "We could not save the module settings:".
  it('uses save-specific error wording when a save fails (Issue 12)', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'Internal Server Error',
      status: 500,
      detail: 'Boom.',
      errors: {},
    };
    updateSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 500 })));
    setup();

    component.submit();
    fixture.detectChanges();

    expect(component.errorContext()).toBe('save');
    expect(component.errorLead()).toBe('We could not save the module settings:');
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.module-form__errors')?.textContent).toContain(
      'We could not save the module settings:',
    );
  });

  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #12] a failed DELETE reads "We could not delete this module:"
  // instead of the previous static save wording.
  it('uses delete-specific error wording when a delete fails (Issue 12)', () => {
    const problem = {
      type: 'urn:dnnmigration:error:internal',
      title: 'Internal Server Error',
      status: 500,
      detail: 'Boom.',
      errors: {},
    };
    removeSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 500 })));
    setup();

    component.requestDelete();
    component.confirmDelete();
    fixture.detectChanges();

    expect(component.errorContext()).toBe('delete');
    expect(component.errorLead()).toBe('We could not delete this module:');
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.module-form__errors')?.textContent).toContain(
      'We could not delete this module:',
    );
  });
});
