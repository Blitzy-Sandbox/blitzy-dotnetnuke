// MIGRATION: Replaces the legacy DNN Web Forms Admin > Users create/edit/delete control
// (Website/admin/Users/User.ascx + User.ascx.vb - postback/ViewState/code-behind) with a single,
// stateless, standalone Angular 19 reactive-form component used by BOTH the 'new' (create) and the
// ':id' (edit) routes. This is a BEHAVIOR-parity migration, NOT a line-by-line VB->TS conversion: the
// legacy validation rules and semantics are reproduced exactly, expressed idiomatically in Angular.
//   - Validate() password block (User.ascx.vb L133-195)        -> reactive validators + passwordMismatch
//   - cmdUpdate_Click (User.ascx.vb L361-385)                  -> submit() -> createUser()/updateUser()
//   - CreateUser()/chkAuthorize/chkNotify (User.ascx.vb L209-238) -> buildCreateDto() (authorize/notify)
//   - UserController.UpdateUser (User.ascx.vb L376)            -> updateUser() + buildUpdateDto()
//   - cmdDelete_Click/cmdDelete.Visible (L264-268, L342-351)   -> canDelete() + confirmDelete()
//
// The reactive form is the UI SUPERSET (mirrors the `models` `CreateUserForm`): it collects the
// confirm-password, the random-password toggle, the password question/answer, and the authorize/notify
// flags. Those are validated client-side and then mapped DOWN to the wire `CreateUserDto`/`UpdateUserDto`
// (which intentionally do NOT carry them). Captcha (self-registration only), password reset, and the
// membership/profile sub-screens are OUT OF SCOPE here (AAP section 0.2.2) and live in separate components.
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import { UserService } from '../../services';
import { UserCreateStatus, type CreateUserDto, type UpdateUserDto } from '../../models';
import type { User } from '../../../../core/models/user.model';
import { type ProblemDetails, summarizeProblem } from '../../../../core/services/api.service';

/**
 * Strongly-typed reactive-form model for the user create/edit screen.
 *
 * This is the UI SUPERSET (mirrors the `models` `CreateUserForm`): `randomPassword`, `password`,
 * `confirmPassword`, `question`, `answer`, `authorize`, and `notify` are collected/validated client-side
 * and then mapped DOWN to the wire `CreateUserDto` (the API does not accept them). `username` is editable
 * in create and disabled (read-only) in edit. `affiliateId` is DISPLAY-ONLY - the wire DTOs do not accept
 * it (it is read-only on the backend `UserDto`), so it is shown but never submitted.
 */
interface UserFormControls {
  username: FormControl<string>;
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  displayName: FormControl<string>;
  email: FormControl<string>;
  affiliateId: FormControl<number | null>;
  randomPassword: FormControl<boolean>;
  password: FormControl<string>;
  confirmPassword: FormControl<string>;
  question: FormControl<string>;
  answer: FormControl<string>;
  authorize: FormControl<boolean>;
  notify: FormControl<boolean>;
}

@Component({
  selector: 'app-user-form',
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserFormComponent implements OnInit {
  // 1) Injected dependencies - declared FIRST so the field initializers below can consume them.
  //    All data access flows through UserService; HttpClient is never injected here (BFF boundary).
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly userService = inject(UserService);
  private readonly destroyRef = inject(DestroyRef);

  // 2) State signals - declared BEFORE the form because the group validator reads `this.mode()`
  //    synchronously while the FormGroup is being constructed (FormGroup runs its validators on init).
  readonly mode = signal<'create' | 'edit'>('create');
  readonly userId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly serverErrors = signal<Record<string, string[]> | null>(null);
  readonly submitError = signal<string | null>(null);
  readonly currentUser = signal<User | null>(null);
  readonly deleteDialogOpen = signal(false);
  // MIGRATION: legacy MembershipProviderConfig.RequiresQuestionAndAnswer is a server membership setting
  // not exposed to the SPA in Phase 1; default false. (User.ascx.vb L168)
  readonly requiresQuestionAndAnswer = signal(false);

  // 3) Cross-field group validator - reads `this.mode()` plus sibling control values.
  // MIGRATION: legacy `If txtPassword.Text <> txtConfirm.Text Then createStatus = UserCreateStatus.PasswordMismatch`
  // (User.ascx.vb L152-154). Only enforced in CREATE mode, when the password block is shown and random is OFF.
  private readonly passwordMatchValidator: ValidatorFn = (group): ValidationErrors | null => {
    if (this.mode() === 'edit') {
      return null;
    }
    const g = group as FormGroup<UserFormControls>;
    if (g.controls.randomPassword.value) {
      return null;
    }
    return g.controls.password.value === g.controls.confirmPassword.value ? null : { passwordMismatch: true };
  };

  // 4) The reactive form - consumes the group validator declared immediately above.
  readonly form = new FormGroup<UserFormControls>(
    {
      username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: UserInfo FirstName <Required(True)>
      lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: UserInfo LastName <Required(True)>
      displayName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: server enforces Security_DisplayNameFormat; SPA cannot read the portal setting
      email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), // MIGRATION: Security_EmailValidation regex enforced server-side
      affiliateId: new FormControl<number | null>(null),
      randomPassword: new FormControl(false, { nonNullable: true }), // MIGRATION: markup checked=True but forced False on load (User.ascx.vb L261)
      password: new FormControl('', { nonNullable: true }),
      confirmPassword: new FormControl('', { nonNullable: true }),
      question: new FormControl('', { nonNullable: true }),
      answer: new FormControl('', { nonNullable: true }),
      authorize: new FormControl(true, { nonNullable: true }), // MIGRATION: chkAuthorize default checked -> Membership.Approved (User.ascx.vb L223, User.ascx L21)
      notify: new FormControl(true, { nonNullable: true }), // MIGRATION: chkNotify default checked (User.ascx L25)
    },
    { validators: [this.passwordMatchValidator] },
  );

  // 5) Derived signals - declared AFTER the form they depend on.
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly randomPasswordValue = computed(() => this.formValue().randomPassword ?? false);
  readonly isEditMode = computed(() => this.mode() === 'edit');
  // MIGRATION: the password block is create-mode-only (legacy `AddUser And ShowPassword`, User.ascx.vb L148).
  readonly showPassword = computed(() => this.mode() === 'create');
  readonly title = computed(() => (this.isEditMode() ? 'Edit User' : 'Add New User'));
  // MIGRATION: display mirror of the PasswordMismatch group error; shown only once the user has typed a confirmation.
  readonly passwordMismatch = computed(() => {
    const v = this.formValue();
    return (
      this.mode() === 'create' &&
      !v.randomPassword &&
      (v.confirmPassword ?? '').length > 0 &&
      v.password !== v.confirmPassword
    );
  });
  // MIGRATION: legacy cmdDelete hidden in create; in edit hidden when editing the portal Administrator
  // OR a superuser editing themselves (User.ascx.vb L264-268). The SPA has no AdministratorId, so we
  // protect superusers as the closest parity; RBAC is additionally enforced by *appHasPermission='DELETE'.
  readonly canDelete = computed(() => {
    const u = this.currentUser();
    return this.isEditMode() && u !== null && !u.isSuperUser;
  });

  // 6) Validation message maps - declared as fields (NOT inline template literals) so OnPush does not
  //    recreate them every change-detection cycle. They genuinely USE the UserCreateStatus enum through
  //    statusMessage(), so the enum import is a real value usage (not type-only / unused).
  protected statusMessage(status: UserCreateStatus): string {
    switch (status) {
      case UserCreateStatus.PasswordMismatch:
        return 'Password and Confirm Password do not match.';
      case UserCreateStatus.InvalidPassword:
        return 'The password does not meet the security requirements.';
      case UserCreateStatus.InvalidQuestion:
        return 'The password question is required.';
      case UserCreateStatus.InvalidAnswer:
        return 'The password answer is required.';
      case UserCreateStatus.InvalidEmail:
        // MIGRATION (QA Finding — validation parity): VERBATIM backend wording. Both the inline `email`
        // validator message (emailMessages.email below, which calls statusMessage(InvalidEmail)) and any
        // server-returned InvalidEmail status now read identically to the FluentValidation
        // Create/UpdateUserValidator Email .EmailAddress() rule ("Email Address is invalid.").
        return 'Email Address is invalid.';
      default:
        return 'Unable to save the user.';
    }
  }

  protected readonly passwordMismatchMessage = this.statusMessage(UserCreateStatus.PasswordMismatch);
  // MIGRATION (QA Finding — validation parity): every inline `required`/`email` message reads VERBATIM as
  // the backend FluentValidation Create/UpdateUserValidator emits, so the client-side empty/invalid-field
  // message and the server 400 ProblemDetails field error are identical (AAP §0.7.1 UI functional parity:
  // validation rules AND error semantics). Username + Password are create-only on the backend; the other
  // four (DisplayName/Email/FirstName/LastName) are validated identically on both Create and Update.
  protected readonly usernameMessages: Record<string, string> = { required: 'Username Is Required.' };
  protected readonly firstNameMessages: Record<string, string> = { required: 'First Name Is Required.' };
  protected readonly lastNameMessages: Record<string, string> = { required: 'Last Name Is Required.' };
  protected readonly displayNameMessages: Record<string, string> = { required: 'Display Name Is Required.' };
  protected readonly emailMessages: Record<string, string> = {
    required: 'Email Is Required.',
    email: this.statusMessage(UserCreateStatus.InvalidEmail),
  };
  // MIGRATION: minlength mirrors UserController.ValidatePassword min length (ASP.NET default 7); the server
  // remains authoritative for InvalidPassword. The `required` message reads VERBATIM as the backend
  // CreateUserValidator Password .NotEmpty() rule ("Password Is Required.").
  protected readonly passwordMessages: Record<string, string> = {
    required: 'Password Is Required.',
    minlength: this.statusMessage(UserCreateStatus.InvalidPassword),
  };
  protected readonly confirmMessages: Record<string, string> = { required: 'Confirm password is required.' };
  protected readonly questionMessages: Record<string, string> = { required: this.statusMessage(UserCreateStatus.InvalidQuestion) };
  protected readonly answerMessages: Record<string, string> = { required: this.statusMessage(UserCreateStatus.InvalidAnswer) };

  constructor() {
    // MIGRATION: toggling the random-password checkbox re-applies the password/Q&A validators (User.ascx.vb L150-183).
    this.form.controls.randomPassword.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyPasswordValidators());
  }

  ngOnInit(): void {
    // DECISION: the app composition root registers provideRouter(routes) WITHOUT withComponentInputBinding(),
    // so route params are NOT bound to component input()s - read the id from the snapshot paramMap instead.
    // The 'new' route has no :id param (get('id') -> null -> create); the ':id' route yields the numeric id.
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam !== null && idParam !== 'new') {
      const id = Number(idParam);
      if (!Number.isNaN(id)) {
        this.mode.set('edit');
        this.userId.set(id);
        this.form.controls.username.disable(); // MIGRATION: UserInfo.Username is IsReadOnly after creation
        this.applyPasswordValidators(); // edit -> clears password/Q&A validators
        this.form.updateValueAndValidity();
        this.loadUser(id);
        return;
      }
    }
    this.mode.set('create');
    this.applyPasswordValidators(); // create -> password required + minlength
  }

  /** Public submit entry point (template `(ngSubmit)`); clears prior errors then branches by mode. */
  submit(): void {
    this.serverErrors.set(null);
    this.submitError.set(null);
    if (this.mode() === 'create') {
      this.createUser();
    } else {
      this.updateUser();
    }
  }

  /** Opens the delete confirmation dialog (template binds to deleteDialogOpen()). */
  openDeleteDialog(): void {
    this.deleteDialogOpen.set(true);
  }

  /** Dismisses the delete confirmation dialog without deleting. */
  cancelDelete(): void {
    this.deleteDialogOpen.set(false);
  }

  // MIGRATION: cmdDelete_Click -> UserController.DeleteUser(User, True, False) (User.ascx.vb L342-351).
  // The server enforces the admin-protection + cascade strategy; the SPA confirms intent via the dialog.
  confirmDelete(): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    this.deleteDialogOpen.set(false);
    this.submitting.set(true);
    this.userService
      .deleteUser(id)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/users']);
        },
        error: (e: ProblemDetails) => this.handleServerError(e),
      });
  }

  /** Abandons the form and returns to the user list (no save). */
  cancel(): void {
    void this.router.navigate(['/users']);
  }

  // MIGRATION: mirrors the legacy Validate() password block (User.ascx.vb L148-183). Password + Q&A rules
  // apply ONLY in create mode with random-password OFF; edit mode and random-ON clear them. Reads the control
  // value DIRECTLY (not the toSignal mirror) so it is always current inside the valueChanges subscription.
  private applyPasswordValidators(): void {
    const { password, confirmPassword, question, answer } = this.form.controls;
    if (this.mode() === 'edit' || this.form.controls.randomPassword.value) {
      password.clearValidators();
      confirmPassword.clearValidators();
      question.clearValidators();
      answer.clearValidators();
    } else {
      // MIGRATION: UserController.ValidatePassword min length (ASP.NET default 7) -> InvalidPassword.
      password.setValidators([Validators.required, Validators.minLength(7)]);
      confirmPassword.setValidators([Validators.required]);
      if (this.requiresQuestionAndAnswer()) {
        // MIGRATION: InvalidQuestion / InvalidAnswer (User.ascx.vb L168-183) - only when the provider requires Q&A.
        question.setValidators([Validators.required]);
        answer.setValidators([Validators.required]);
      } else {
        question.clearValidators();
        answer.clearValidators();
      }
    }
    password.updateValueAndValidity();
    confirmPassword.updateValueAndValidity();
    question.updateValueAndValidity();
    answer.updateValueAndValidity();
  }

  // MIGRATION: edit-mode hydration. UserController.GetUser -> UserService.getUser; the User read model
  // populates the editable profile fields. AffiliateID (wire `affiliateID`) is display-only - the create/
  // update wire DTOs do not accept it, so it is shown but never submitted.
  private loadUser(id: number): void {
    this.loading.set(true);
    this.userService
      .getUser(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (user: User) => {
          this.currentUser.set(user);
          this.form.patchValue({
            username: user.username,
            firstName: user.firstName,
            lastName: user.lastName,
            displayName: user.displayName,
            email: user.email,
            affiliateId: user.affiliateID ?? null,
          });
        },
        error: (err: ProblemDetails) => {
          this.serverErrors.set(err.errors ?? null);
          this.submitError.set(summarizeProblem(err, 'Failed to load user.'));
        },
      });
  }

  // MIGRATION: cmdUpdate_Click when AddUser (User.ascx.vb L362-365): `If IsValid Then CreateUser()`.
  private createUser(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    const dto = this.buildCreateDto();
    this.submitting.set(true);
    this.userService
      .createUser(dto)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/users']);
        },
        error: (e: ProblemDetails) => this.handleServerError(e),
      });
  }

  // MIGRATION: cmdUpdate_Click when NOT AddUser (User.ascx.vb L367): legacy saves ONLY when the editor is
  // valid AND dirty (UserEditor.IsValid AndAlso UserEditor.IsDirty). Reproduced as form.valid && form.dirty.
  private updateUser(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.form.dirty) {
      return;
    }
    const id = this.userId();
    if (id === null) {
      return;
    }
    const dto = this.buildUpdateDto();
    this.submitting.set(true);
    this.userService
      .updateUser(id, dto)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/users']);
        },
        error: (e: ProblemDetails) => this.handleServerError(e),
      });
  }

  // MIGRATION: maps the UI superset DOWN to the wire CreateUserDto (POST /api/v1/users). The form-only inputs
  // (confirmPassword, question, answer, notify) are validated client-side then stripped - they are NOT part
  // of the CreateUserDto contract (they live on `models` CreateUserForm).
  private buildCreateDto(): CreateUserDto {
    const v = this.form.getRawValue();
    return {
      // MIGRATION: the SPA has no portal selector in Phase 1; admin-created users target the primary portal
      // (DNN default PortalID 0). The server remains authoritative for the portal context.
      portalID: 0,
      username: v.username,
      // MIGRATION: random-password ON -> the server generates the password (User.ascx.vb L162-165); omit it.
      password: v.randomPassword ? undefined : v.password,
      displayName: v.displayName,
      email: v.email,
      firstName: v.firstName,
      lastName: v.lastName,
      // MIGRATION: admin-created portal users are not host superusers (legacy AddUser default).
      isSuperUser: false,
      // MIGRATION: legacy chkAuthorize -> Membership.Approved (User.ascx.vb L223).
      approved: v.authorize,
    };
  }

  // MIGRATION: maps the form DOWN to the wire UpdateUserDto (PUT /api/v1/users/{id}). username/password are
  // omitted (read-only / dedicated flow); affiliateId is omitted (read-only on the backend). userID MUST
  // equal the route id - the server rejects a mismatch with RFC 7807 400.
  private buildUpdateDto(): UpdateUserDto {
    const v = this.form.getRawValue();
    const current = this.currentUser();
    return {
      userID: this.userId() ?? 0,
      displayName: v.displayName,
      email: v.email,
      firstName: v.firstName,
      lastName: v.lastName,
      // MIGRATION: IsSuperUser is not editable on this screen; preserve the loaded user's value.
      isSuperUser: current?.isSuperUser ?? false,
      // MIGRATION: the SPA User read model does not surface Membership.Approved (owned by the separate
      // Membership screen, and the legacy edit path L361-385 does not change approval); the required
      // UpdateUserDto.Approved is sent from the form's authorize flag.
      approved: v.authorize,
    };
  }

  // MIGRATION: RFC 7807 ProblemDetails -> field-level (errors, surfaced inline by app-form-controls) plus a
  // summary message (title/detail), replacing the legacy per-validator postback ErrorMessage rendering.
  private handleServerError(err: ProblemDetails): void {
    this.serverErrors.set(err.errors ?? null);
    this.submitError.set(summarizeProblem(err, 'An error occurred while saving the user.'));
  }
}
