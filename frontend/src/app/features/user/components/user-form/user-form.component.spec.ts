// MIGRATION: Unit spec for UserFormComponent — the Angular 19 replacement for the legacy DNN
// Web Forms Admin > Users create/edit/delete control (Website/admin/Users/User.ascx + User.ascx.vb).
// It validates the BEHAVIOR-parity contract the component ports verbatim from the legacy code-behind:
//   - Validate() password block (User.ascx.vb L148-195)      -> reactive validators + passwordMismatch()
//   - cmdUpdate_Click create branch (User.ascx.vb L362-365)  -> submit() -> createUser()
//   - cmdUpdate_Click edit branch  (User.ascx.vb L367)       -> update saved ONLY when valid AND dirty
//   - cmdDelete_Click (User.ascx.vb L342-351)                -> confirmDelete() -> UserService.deleteUser
//   - cmdDelete.Visible (User.ascx.vb L264-268)              -> canDelete() (hidden in create / for superusers)
//
// Testing strategy — true UNIT test in ISOLATION. The component imports four real shared standalone
// building blocks (FormControlsComponent, ConfirmationDialogComponent, LoadingSpinnerComponent,
// HasPermissionDirective) which inject their own collaborators (e.g. AuthService). To keep this spec
// free of those transitive dependencies, TestBed.overrideComponent swaps the real imports for lightweight
// standalone STUB doubles that share the same selectors and declare a SUPERSET of every input/output the
// real components expose — so the (separately authored) component template compiles against them. All
// HTTP collaboration is replaced by a typed UserService spy; Router is spied; ActivatedRoute is stubbed
// to drive the create (no :id) vs edit (:id) code paths. Every collaborator observable is synchronous
// (of/throwError), so assertions run immediately after the triggering call with no fakeAsync/tick.
import { Component, Directive, TemplateRef, ViewContainerRef, inject, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { UserFormComponent } from './user-form.component';
import { UserService } from '../../services';
import type { User } from '../../../../core/models/user.model';
import type { CreateUserDto, UpdateUserDto } from '../../models';
import type { ProblemDetails } from '../../../../core/services/api.service';

// --- Standalone stub doubles (Angular 19 standalone-by-default) -------------------------------------
// Each stub mirrors the real shared component's selector and declares EVERY input/output the component
// template can bind (a superset of the real contract). The templates are intentionally empty: this spec
// asserts component-class behavior (signals, form state, service calls), not rendered shared-component DOM.

/** Stub for the real app-form-controls (label + input + inline-validation wrapper). */
@Component({ selector: 'app-form-controls', template: '' })
class StubFormControlsComponent {
  readonly control = input<FormControl>();
  readonly controlId = input('');
  readonly label = input('');
  readonly type = input('text');
  readonly placeholder = input('');
  readonly autofocus = input(false);
  readonly serverErrors = input<Record<string, string[]> | null>(null);
  readonly messages = input<Record<string, string>>({});
}

/** Stub for the real app-confirmation-dialog (accessible delete-confirmation modal). */
@Component({ selector: 'app-confirmation-dialog', template: '' })
class StubConfirmationDialogComponent {
  readonly open = input(false);
  readonly title = input('Confirm');
  readonly message = input('');
  readonly confirmText = input('Delete');
  readonly cancelText = input('Cancel');
  readonly destructive = input(true);
  readonly confirm = output<void>();
  readonly cancel = output<void>();
}

/** Stub for the real app-loading-spinner. */
@Component({ selector: 'app-loading-spinner', template: '' })
class StubLoadingSpinnerComponent {
  readonly loading = input(true);
  readonly message = input('Loading...');
  readonly diameter = input(40);
}

/**
 * Stub for the real [appHasPermission] structural directive. Unlike the real directive (which consults
 * AuthService), this double ALWAYS renders its host template so any permission-gated affordance in the
 * component template is present during the test; RBAC enforcement is verified in the directive's own spec.
 */
@Directive({ selector: '[appHasPermission]' })
class StubHasPermissionDirective {
  private readonly tpl: TemplateRef<unknown> = inject(TemplateRef);
  private readonly vcr = inject(ViewContainerRef);
  readonly appHasPermission = input<string>('');
  ngOnInit(): void {
    this.vcr.createEmbeddedView(this.tpl);
  }
}

describe('UserFormComponent', () => {
  let userServiceSpy: jasmine.SpyObj<UserService>;
  let routerSpy: jasmine.SpyObj<Router>;

  // NOTE: property casing matches the real core User model exactly — userID / portalID (trailing
  // acronym preserved by System.Text.Json camelCase) and affiliateID is OPTIONAL (omitted here).
  const mockUser: User = {
    userID: 5,
    username: 'jdoe',
    firstName: 'John',
    lastName: 'Doe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    portalID: 0,
    isSuperUser: false,
    roles: [],
  };

  beforeEach(() => {
    userServiceSpy = jasmine.createSpyObj<UserService>('UserService', [
      'getUser',
      'createUser',
      'updateUser',
      'deleteUser',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    // Sensible synchronous defaults; individual tests override as needed.
    userServiceSpy.getUser.and.returnValue(of(mockUser));
    userServiceSpy.createUser.and.returnValue(of(mockUser));
    userServiceSpy.updateUser.and.returnValue(of(mockUser));
    userServiceSpy.deleteUser.and.returnValue(of(void 0));
  });

  /**
   * Configures TestBed per test (the environment resets TestBed between specs), swaps the real shared
   * imports for stubs, and creates the component. `idParam` drives the route: null -> create route
   * (no :id), a numeric string -> edit route (:id). The initial detectChanges() runs ngOnInit.
   */
  function setup(idParam: string | null): ComponentFixture<UserFormComponent> {
    TestBed.configureTestingModule({
      imports: [UserFormComponent],
      providers: [
        { provide: UserService, useValue: userServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(idParam ? { id: idParam } : {}) } },
        },
      ],
    });
    TestBed.overrideComponent(UserFormComponent, {
      set: {
        imports: [
          ReactiveFormsModule,
          StubFormControlsComponent,
          StubConfirmationDialogComponent,
          StubLoadingSpinnerComponent,
          StubHasPermissionDirective,
        ],
      },
    });
    const fixture = TestBed.createComponent(UserFormComponent);
    fixture.detectChanges(); // runs ngOnInit
    return fixture;
  }

  it('creates the component', () => {
    expect(setup(null).componentInstance).toBeTruthy();
  });

  it('defaults to create mode with an enabled username', () => {
    const component = setup(null).componentInstance;

    expect(component.mode()).toBe('create');
    expect(component.isEditMode()).toBeFalse();
    expect(component.form.controls.username.enabled).toBeTrue();
  });

  it('loads the user in edit mode and renders the username read-only', () => {
    const component = setup('5').componentInstance;

    expect(userServiceSpy.getUser).toHaveBeenCalledWith(5);
    expect(component.mode()).toBe('edit');
    expect(component.form.controls.username.disabled).toBeTrue();
    expect(component.form.controls.email.value).toBe('jdoe@example.com');
  });

  it('flags a password / confirm-password mismatch in create mode', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance;

    component.form.controls.randomPassword.setValue(false);
    component.form.controls.password.setValue('Secret123!');
    component.form.controls.confirmPassword.setValue('Different1!');
    fixture.detectChanges();

    expect(component.form.errors?.['passwordMismatch']).toBeTruthy();
    expect(component.passwordMismatch()).toBeTrue();
  });

  it('clears the password validators when random-password is enabled', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance;

    component.form.controls.randomPassword.setValue(true);
    fixture.detectChanges();
    component.form.controls.password.setValue('');

    expect(component.form.controls.password.valid).toBeTrue();
  });

  it('creates a user and navigates back to the list on submit', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance;

    component.form.patchValue({
      username: 'newuser',
      firstName: 'New',
      lastName: 'User',
      displayName: 'New User',
      email: 'newuser@example.com',
      randomPassword: true,
    });
    fixture.detectChanges();
    component.submit();

    expect(userServiceSpy.createUser).toHaveBeenCalled();
    const dto = userServiceSpy.createUser.calls.mostRecent().args[0] as CreateUserDto;
    expect(dto.username).toBe('newuser');
    // random-password ON -> the server generates the password, so it is omitted from the wire DTO.
    expect(dto.password).toBeUndefined();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('does NOT update when the edit form is pristine (legacy requires IsDirty)', () => {
    const component = setup('5').componentInstance;

    component.submit();

    expect(userServiceSpy.updateUser).not.toHaveBeenCalled();
  });

  it('updates the user when the edit form is dirty and valid', () => {
    const fixture = setup('5');
    const component = fixture.componentInstance;

    component.form.controls.firstName.setValue('Jonathan');
    component.form.controls.firstName.markAsDirty();
    fixture.detectChanges();
    component.submit();

    expect(userServiceSpy.updateUser).toHaveBeenCalled();
    const [id, dto] = userServiceSpy.updateUser.calls.mostRecent().args as [number, UpdateUserDto];
    expect(id).toBe(5);
    expect(dto.firstName).toBe('Jonathan');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('opens the delete dialog and deletes the user on confirmation', () => {
    const component = setup('5').componentInstance;

    component.openDeleteDialog();
    expect(component.deleteDialogOpen()).toBeTrue();

    component.confirmDelete();
    expect(userServiceSpy.deleteUser).toHaveBeenCalledWith(5);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('maps RFC 7807 problem details to server errors and a summary message', () => {
    const problem: ProblemDetails = {
      status: 400,
      title: 'Validation failed',
      errors: { email: ['Email already exists.'] },
    };
    userServiceSpy.createUser.and.returnValue(throwError(() => problem));

    const fixture = setup(null);
    const component = fixture.componentInstance;
    component.form.patchValue({
      username: 'newuser',
      firstName: 'New',
      lastName: 'User',
      displayName: 'New User',
      email: 'newuser@example.com',
      randomPassword: true,
    });
    fixture.detectChanges();
    component.submit();

    expect(component.serverErrors()).toEqual({ email: ['Email already exists.'] });
    expect(component.submitError()).toBe('Validation failed');
  });

  it('disallows delete for a superuser', () => {
    userServiceSpy.getUser.and.returnValue(of({ ...mockUser, isSuperUser: true }));

    const component = setup('5').componentInstance;

    expect(component.canDelete()).toBeFalse();
  });
});
