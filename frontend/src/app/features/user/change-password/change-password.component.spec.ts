/**
 * Unit tests for {@link ChangePasswordComponent} (Validation Gate 4).
 *
 * MIGRATION: proves UI functional parity with the legacy DNN
 * `Website/admin/Users/Password.ascx.vb` `cmdUpdate_Click` handler (AAP §0.7.1):
 *   - new <> confirm             -> PasswordUpdateStatus.PasswordMismatch      (group error `passwordMismatch`)
 *   - new  = old (self-service)  -> PasswordUpdateStatus.PasswordNotDifferent  (group error `passwordNotDifferent`)
 *   - UserController.ChangePassword success -> PasswordUpdated -> navigate back to the user
 *   - ChangePassword failure (RFC 7807 ProblemDetails) -> PasswordResetFailed -> surface message, stay on page
 *
 * The subject is a standalone Angular 19 component (OnPush) that owns a typed
 * `NonNullableFormBuilder` group with two cross-field group validators, so it is
 * registered via TestBed `imports` (never `declarations`); its own imports
 * (`ReactiveFormsModule` + the child standalone components) come along
 * automatically. Injected collaborators (`UserService`, `Router`,
 * `ActivatedRoute`) are mocked with typed spies / a lightweight stub; the
 * framework-provided `NonNullableFormBuilder`/`DestroyRef` are left intact.
 *
 * Determinism: `of<void>(undefined)` and `throwError(() => problem)` emit
 * synchronously, so the `next`/`error` callbacks run inside `onSubmit()` with no
 * `fakeAsync`/`tick`. Only PUBLIC members are asserted — private state (userId,
 * injected deps) is verified through its observable effects (spy call args).
 */
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ChangePasswordComponent } from './change-password.component';
import { UserService } from '../user.service';

describe('ChangePasswordComponent', () => {
  let fixture: ComponentFixture<ChangePasswordComponent>;
  let component: ChangePasswordComponent;
  let userService: jasmine.SpyObj<UserService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    userService = jasmine.createSpyObj<UserService>('UserService', ['changePassword']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      // Standalone component: register via `imports` (never `declarations`). Its own
      // imports (ReactiveFormsModule + child standalone components) are pulled in.
      imports: [ChangePasswordComponent],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: Router, useValue: router },
        // ActivatedRoute stub: the component reads only `snapshot.paramMap.get('id')`.
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '42' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChangePasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  /** Fills the group with a valid change (old != new, confirm == new). */
  function fillValidChange(): void {
    component.form.setValue({
      oldPassword: 'Original1',
      newPassword: 'BrandNew1',
      confirmNewPassword: 'BrandNew1',
    });
  }

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  it('is invalid while empty and does not submit', () => {
    // All three controls are `required`, so the empty group is invalid up-front.
    expect(component.form.invalid).toBe(true);

    component.onSubmit();

    // Legacy parity: an invalid form short-circuits before UserController.ChangePassword.
    expect(userService.changePassword).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('flags a password mismatch (new <> confirm) as invalid (legacy PasswordMismatch)', () => {
    component.form.setValue({
      oldPassword: 'Original1',
      newPassword: 'BrandNew1',
      confirmNewPassword: 'Different1',
    });

    expect(component.form.hasError('passwordMismatch')).toBe(true);
    expect(component.form.invalid).toBe(true);
  });

  it('flags a new password equal to the old password as invalid (legacy PasswordNotDifferent)', () => {
    component.form.setValue({
      oldPassword: 'SamePass1',
      newPassword: 'SamePass1',
      confirmNewPassword: 'SamePass1',
    });

    expect(component.form.hasError('passwordNotDifferent')).toBe(true);
    expect(component.form.invalid).toBe(true);
  });

  it('surfaces the mismatch flag only after the confirmation field is touched', () => {
    // `setValue` mutates the value without marking the control touched/dirty, so the
    // gated computed stays quiet until the user has interacted with the confirm field.
    component.form.controls.newPassword.setValue('BrandNew1');
    component.form.controls.confirmNewPassword.setValue('Different1');
    expect(component.showMismatchError()).toBe(false);

    component.form.controls.confirmNewPassword.markAsTouched();
    expect(component.showMismatchError()).toBe(true);
  });

  it('submits a valid change and navigates back to the user', () => {
    userService.changePassword.and.returnValue(of<void>(undefined));
    fillValidChange();

    component.onSubmit();

    // id resolves from the ':id' route param stub ('42') -> Number(...) === 42.
    expect(userService.changePassword).toHaveBeenCalledWith(42, {
      oldPassword: 'Original1',
      newPassword: 'BrandNew1',
    });
    expect(router.navigate).toHaveBeenCalledWith(['/users', 42]);
    expect(component.errorMessage()).toBeNull();
    // Success path navigates away and never resets the busy flag.
    expect(component.submitting()).toBe(true);
  });

  it('surfaces the API error and clears the busy flag without navigating (legacy PasswordResetFailed)', () => {
    userService.changePassword.and.returnValue(
      throwError(() => ({
        type: 'about:blank',
        title: 'Change failed',
        status: 400,
        detail: 'The current password is incorrect.',
      })),
    );
    fillValidChange();

    component.onSubmit();

    expect(component.submitting()).toBe(false);
    // The component prefers the RFC 7807 `detail` for user-facing feedback.
    expect(component.errorMessage()).toBe('The current password is incorrect.');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
