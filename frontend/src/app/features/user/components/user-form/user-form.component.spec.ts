// MIGRATION: Unit spec for UserFormComponent — the Angular 19 standalone reactive-form screen that
// replaces the legacy DNN Web Forms "Admin > Users" create/edit/delete user control
// (Website/admin/Users/User.ascx.vb). These tests pin the migrated behavior so Gate 4
// (`ng test --watch=false --browsers=ChromeHeadless`) stays green:
//   - create vs. edit mode resolved from the :id route param (User.ascx.vb AddUser branch)
//   - password/confirm mismatch + random-password validator toggling (Validate(), L150-189)
//   - the create submit -> CreateUser() path (L209-238) and the valid+dirty update gate (L361-385)
//   - RFC 7807 ProblemDetails -> field/summary error mapping, and the delete flow (cmdDelete_Click, L342-351)
//
// Testing strategy — ISOLATE. The component imports the real shared standalone building blocks
// (FormControlsComponent, ConfirmationDialogComponent, LoadingSpinnerComponent, HasPermissionDirective),
// which can inject their own services. To keep this a true UNIT test we swap them — via
// TestBed.overrideComponent — for lightweight standalone stub doubles that share the same selectors and
// declare every input/output the template binds (required under strictTemplates). UserService and Router
// are jasmine spies; ActivatedRoute is a snapshot stub that selects the create vs. edit code path.
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

// --- Standalone stub doubles (Angular 19: standalone by default). Each mirrors the real shared
// component/directive selector and declares EVERY input/output the user-form template binds, so the
// template still type-checks under strictTemplates once the component's imports are overridden. ---

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

@Component({ selector: 'app-loading-spinner', template: '' })
class StubLoadingSpinnerComponent {
  readonly loading = input(true);
  readonly message = input('Loading...');
  readonly diameter = input(40);
}

@Directive({ selector: '[appHasPermission]' })
class StubHasPermissionDirective {
  private readonly tpl: TemplateRef<unknown> = inject(TemplateRef);
  private readonly vcr = inject(ViewContainerRef);
  readonly appHasPermission = input<string>('');

  ngOnInit(): void {
    // Always render the gated content so the host template (e.g. the delete button) materializes.
    this.vcr.createEmbeddedView(this.tpl);
  }
}

describe('UserFormComponent', () => {
  let userServiceSpy: jasmine.SpyObj<UserService>;
  let routerSpy: jasmine.SpyObj<Router>;

  // Corrected to the REAL `User` contract (core/models/user.model.ts): wire field names are
  // userID/portalID/affiliateID (System.Text.Json camelCase of the C# DTO), and every member is
  // required (no optional members), so a partial literal would not type-check.
  const mockUser: User = {
    userID: 5,
    portalID: 0,
    affiliateID: null,
    username: 'jdoe',
    displayName: 'John Doe',
    email: 'jdoe@example.com',
    firstName: 'John',
    lastName: 'Doe',
    fullName: 'John Doe',
    isSuperUser: false,
    approved: true,
    roles: [],
    createdDate: null,
    lastLoginDate: null,
    lastPasswordChangeDate: null,
    lastActivityDate: null,
  };

  beforeEach(() => {
    userServiceSpy = jasmine.createSpyObj<UserService>('UserService', [
      'getUser',
      'createUser',
      'updateUser',
      'deleteUser',
    ]);
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);

    // Sensible defaults; individual tests override as needed. All observables are synchronous, so
    // assertions can run immediately after submit()/confirmDelete() with no fakeAsync/tick.
    userServiceSpy.getUser.and.returnValue(of(mockUser));
    userServiceSpy.createUser.and.returnValue(of(mockUser));
    userServiceSpy.updateUser.and.returnValue(of(mockUser));
    userServiceSpy.deleteUser.and.returnValue(of(void 0));
    routerSpy.navigate.and.resolveTo(true);
  });

  // Configure TestBed PER TEST (not in a shared beforeEach): the ActivatedRoute snapshot differs
  // between the create (no :id) and edit (:id) cases, and the test environment resets TestBed
  // between specs. overrideComponent swaps only the imports (the real template is preserved).
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
    fixture.detectChanges(); // first CD runs ngOnInit
    return fixture;
  }

  it('creates the component', () => {
    expect(setup(null).componentInstance).toBeTruthy();
  });

  it('defaults to create mode with an enabled username control', () => {
    const component = setup(null).componentInstance;

    expect(component.mode()).toBe('create');
    expect(component.isEditMode()).toBeFalse();
    expect(component.form.controls.username.enabled).toBeTrue();
  });

  it('loads the user in edit mode and makes the username read-only', () => {
    const component = setup('5').componentInstance;

    expect(userServiceSpy.getUser).toHaveBeenCalledWith(5);
    expect(component.mode()).toBe('edit');
    expect(component.form.controls.username.disabled).toBeTrue();
    expect(component.form.controls.email.value).toBe('jdoe@example.com');
  });

  it('flags a password/confirm mismatch in create mode', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance;

    component.form.patchValue({
      randomPassword: false,
      password: 'Secret123!',
      confirmPassword: 'Different1!',
    });
    fixture.detectChanges();

    expect(component.form.errors?.['passwordMismatch']).toBeTruthy();
    expect(component.passwordMismatch()).toBeTrue();
  });

  it('skips the password validators when a random password is requested', () => {
    const fixture = setup(null);
    const component = fixture.componentInstance;

    component.form.controls.randomPassword.setValue(true);
    fixture.detectChanges();
    component.form.controls.password.setValue('');

    // No required/minlength applied while random-password is on (asserted via control validity).
    expect(component.form.controls.password.valid).toBeTrue();
  });

  it('submits a create request and navigates to the user list', () => {
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
    const created = userServiceSpy.createUser.calls.mostRecent().args[0] as CreateUserDto;
    expect(created.username).toBe('newuser');
    expect(created.password).toBeUndefined(); // random-password on -> server generates
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('does not update when the edit form is pristine (MIGRATION: legacy requires IsDirty)', () => {
    const component = setup('5').componentInstance;

    component.submit();

    expect(userServiceSpy.updateUser).not.toHaveBeenCalled();
  });

  it('updates when the edit form is valid and dirty', () => {
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

  it('confirms a delete and navigates to the user list', () => {
    const component = setup('5').componentInstance;

    component.openDeleteDialog();
    expect(component.deleteDialogOpen()).toBeTrue();

    component.confirmDelete();
    expect(userServiceSpy.deleteUser).toHaveBeenCalledWith(5);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/users']);
  });

  it('maps RFC 7807 problem details to field-level and summary errors', () => {
    userServiceSpy.createUser.and.returnValue(
      throwError(
        () =>
          ({
            status: 400,
            title: 'Validation failed',
            errors: { email: ['Email already exists.'] },
          }) as ProblemDetails,
      ),
    );

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

  it('disallows deleting a super user', () => {
    userServiceSpy.getUser.and.returnValue(of({ ...mockUser, isSuperUser: true }));

    const component = setup('5').componentInstance;

    expect(component.canDelete()).toBeFalse();
  });
});
