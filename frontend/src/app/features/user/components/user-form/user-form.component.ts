// MIGRATION: UserFormComponent replaces the legacy DNN Web Forms "Admin > Users" create/edit/delete
// user control (Website/admin/Users/User.ascx.vb + User.ascx) — a server-rendered .ascx driven by
// postback/ViewState — with a stateless, client-rendered Angular 19 standalone reactive-form screen.
// This SINGLE component serves BOTH the 'new' (create) and ':id' (edit) routes in ../../user.routes.ts.
// It is a BEHAVIOR-reference migration: the legacy validation rules and semantics are reproduced
// exactly (see the per-member // MIGRATION: notes), but expressed idiomatically in Angular 19.
// Behavioral parity map:
//   - Validate() (User.ascx.vb L133-195)        -> reactive Validators + passwordMatch group validator
//   - CreateUser() (User.ascx.vb L209-238)      -> createUser() + toCreateRequest()
//   - cmdUpdate_Click (User.ascx.vb L361-385)   -> updateUser() (valid AND dirty gate, L367)
//   - cmdDelete_Click (User.ascx.vb L342-351)   -> confirmDelete() (gated by canDelete + *appHasPermission)
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
import {
  UserCreateStatus,
  type CreateUserDto,
  type UpdateUserDto,
  type CreateUserRequest,
  type UpdateUserRequest,
} from '../../models';
import type { User } from '../../../../core/models/user.model';
import type { ProblemDetails } from '../../../../core/services/api.service';

/**
 * Strongly-typed reactive-form model for the user create/edit screen.
 *
 * MIGRATION: mirrors the editable surface of User.ascx (the UserEditor property grid plus the
 * AddUser-panel checkboxes and password block). String fields are non-nullable form controls;
 * `affiliateId` is the only nullable numeric control. The password/confirm/question/answer and
 * the authorize/notify/randomPassword affordances exist only in CREATE mode.
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
  // 1) Injected dependencies (declared first so the group validator and form can reference them safely).
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly userService = inject(UserService);
  private readonly destroyRef = inject(DestroyRef);

  // 2) State signals (BEFORE the form: the group validator reads this.mode() at FormGroup construction).
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
  // MIGRATION: the legacy captcha (ctlCaptcha / trCaptcha, User.ascx.vb L54-59, L137-140) applies ONLY to
  // self-registration (UseCaptcha = Security_CaptchaRegister AND IsRegister). Admin user creation is not
  // registration, so the captcha is intentionally omitted from this form (no captcha control or signal).

  // 3) Group validator (reads this.mode() + group controls; assigned before the form that consumes it).
  // MIGRATION: legacy `If txtPassword.Text <> txtConfirm.Text Then createStatus = UserCreateStatus.PasswordMismatch`
  // (User.ascx.vb L152-154). Only enforced in CREATE mode, when the password block is shown and random-password is OFF.
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

  // 4) The reactive form (consumes the group validator declared above).
  readonly form = new FormGroup<UserFormControls>(
    {
      username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      firstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: UserInfo FirstName <Required(True)>
      lastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: UserInfo LastName <Required(True)>
      displayName: new FormControl('', { nonNullable: true, validators: [Validators.required] }), // MIGRATION: server enforces Security_DisplayNameFormat; the SPA cannot read the portal setting.
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

  // 5) Derived signals (AFTER the form).
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly randomPasswordValue = computed(() => this.formValue().randomPassword ?? false);
  readonly isEditMode = computed(() => this.mode() === 'edit');
  // MIGRATION: the password block is create-mode-only (legacy `AddUser And ShowPassword`, User.ascx.vb L148).
  readonly showPassword = computed(() => this.mode() === 'create');
  readonly title = computed(() => (this.isEditMode() ? 'Edit User' : 'Add New User'));
  // MIGRATION: display mirror of the PasswordMismatch group error; show only once the user has typed a confirmation.
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

  // 6) Message maps (genuinely USE the UserCreateStatus enum via statusMessage so the import is a real value).
  // MIGRATION: maps the legacy UserController.GetUserCreateStatus messages (User.ascx.vb L187) to client copy.
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
        return 'A valid email address is required.';
      default:
        return 'Unable to save the user.';
    }
  }

  // Defined as component fields (NOT inline template literals) so OnPush does not re-create them each cycle.
  protected readonly passwordMismatchMessage = this.statusMessage(UserCreateStatus.PasswordMismatch);
  protected readonly usernameMessages: Record<string, string> = { required: 'Username is required.' };
  protected readonly firstNameMessages: Record<string, string> = { required: 'First name is required.' };
  protected readonly lastNameMessages: Record<string, string> = { required: 'Last name is required.' };
  protected readonly displayNameMessages: Record<string, string> = { required: 'Display name is required.' };
  protected readonly emailMessages: Record<string, string> = {
    required: 'Email is required.',
    email: this.statusMessage(UserCreateStatus.InvalidEmail),
  };
  // MIGRATION: minlength mirrors UserController.ValidatePassword min length (ASP.NET default 7); server is authoritative for InvalidPassword.
  protected readonly passwordMessages: Record<string, string> = {
    required: 'Password is required.',
    minlength: this.statusMessage(UserCreateStatus.InvalidPassword),
  };
  protected readonly confirmMessages: Record<string, string> = { required: 'Confirm password is required.' };
  protected readonly questionMessages: Record<string, string> = {
    required: this.statusMessage(UserCreateStatus.InvalidQuestion),
  };
  protected readonly answerMessages: Record<string, string> = {
    required: this.statusMessage(UserCreateStatus.InvalidAnswer),
  };

  constructor() {
    // MIGRATION: toggling the random-password checkbox re-applies the password/Q&A validators (User.ascx.vb L150-183).
    this.form.controls.randomPassword.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyPasswordValidators());
  }

  /**
   * Resolves create vs. edit mode from the `:id` route param.
   *
   * DECISION: the composition root registers `provideRouter(APP_ROUTES)` WITHOUT
   * `withComponentInputBinding()`, so route params are read from the snapshot rather than bound to an
   * `input()`. The 'new' route has no `:id` param (get('id') -> null -> create mode); the ':id' route
   * yields the numeric id -> edit mode.
   */
  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam !== null && idParam !== 'new') {
      const id = Number(idParam);
      if (!Number.isNaN(id)) {
        this.mode.set('edit');
        this.userId.set(id);
        this.form.controls.username.disable(); // MIGRATION: UserInfo.Username IsReadOnly after creation
        this.applyPasswordValidators(); // edit -> clears password/Q&A validators
        this.form.updateValueAndValidity();
        this.loadUser(id);
        return;
      }
    }
    this.mode.set('create');
    this.applyPasswordValidators(); // create -> password required + minlength
  }

  /**
   * Re-applies the conditional password/Q&A validators based on mode + the random-password toggle.
   *
   * MIGRATION: in edit mode there is no password block, and a random password defers generation to the
   * server (User.ascx.vb L148/L164), so the password/Q&A validators are cleared. In create mode with a
   * user-supplied password, the password policy applies and the Q&A pair is required only when the
   * membership provider RequiresQuestionAndAnswer (User.ascx.vb L156-181).
   */
  private applyPasswordValidators(): void {
    const controls = this.form.controls;
    // Read the LIVE control value (not the formValue()-derived signal): this method is invoked from the
    // randomPassword control's own valueChanges, which fires BEFORE the group's valueChanges updates the
    // toSignal, so randomPasswordValue() would be stale here. The control's .value is always current.
    if (this.mode() === 'edit' || controls.randomPassword.value) {
      controls.password.clearValidators();
      controls.confirmPassword.clearValidators();
      controls.question.clearValidators();
      controls.answer.clearValidators();
    } else {
      // MIGRATION: ValidatePassword min length (ASP.NET default 7); the server remains authoritative for InvalidPassword.
      controls.password.setValidators([Validators.required, Validators.minLength(7)]);
      controls.confirmPassword.setValidators([Validators.required]);
      if (this.requiresQuestionAndAnswer()) {
        // MIGRATION: InvalidQuestion / InvalidAnswer (User.ascx.vb L168-181).
        controls.question.setValidators([Validators.required]);
        controls.answer.setValidators([Validators.required]);
      } else {
        controls.question.clearValidators();
        controls.answer.clearValidators();
      }
    }
    controls.password.updateValueAndValidity();
    controls.confirmPassword.updateValueAndValidity();
    controls.question.updateValueAndValidity();
    controls.answer.updateValueAndValidity();
  }

  /**
   * Loads the existing user (edit mode) and patches the form.
   *
   * MIGRATION: nullable wire strings (UserDto string?) coalesce to '' for the non-nullable form controls;
   * the affiliate field is `affiliateID` on the wire (legacy UserInfo.AffiliateID).
   */
  private loadUser(id: number): void {
    this.loading.set(true);
    this.userService
      .getUser(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (user: User) => {
          this.currentUser.set(user);
          this.form.patchValue({
            username: user.username ?? '',
            firstName: user.firstName ?? '',
            lastName: user.lastName ?? '',
            displayName: user.displayName ?? '',
            email: user.email ?? '',
            affiliateId: user.affiliateID,
          });
        },
        error: (err: ProblemDetails) => {
          this.serverErrors.set(err.errors ?? null);
          this.submitError.set(err.title ?? err.detail ?? 'Failed to load user.');
        },
      });
  }

  /**
   * MIGRATION: cmdUpdate_Click (User.ascx.vb L361-385). Clears prior server feedback, then branches on mode
   * to the create or update path.
   */
  submit(): void {
    this.serverErrors.set(null);
    this.submitError.set(null);
    if (this.mode() === 'edit') {
      this.updateUser();
    } else {
      this.createUser();
    }
  }

  /** MIGRATION: AddUser path of cmdUpdate_Click -> if IsValid Then CreateUser() (User.ascx.vb L362-365). */
  private createUser(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    const request = this.toCreateRequest(this.buildCreateDto());
    this.submitting.set(true);
    this.userService
      .createUser(request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/users']);
        },
        error: (err: ProblemDetails) => this.handleServerError(err),
      });
  }

  /**
   * MIGRATION: non-AddUser path of cmdUpdate_Click — the legacy control updates ONLY when the editor is
   * valid AND dirty AND a user is loaded (User.ascx.vb L367). The reactive form's `dirty` flag mirrors
   * `UserEditor.IsDirty`.
   */
  private updateUser(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || !this.form.dirty) {
      return;
    }
    const id = this.userId();
    if (id === null) {
      return;
    }
    const request = this.toUpdateRequest(this.buildUpdateDto(), id);
    this.submitting.set(true);
    this.userService
      .updateUser(id, request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/users']);
        },
        error: (err: ProblemDetails) => this.handleServerError(err),
      });
  }

  /** Builds the create FORM model from the raw (incl. disabled) form value. */
  private buildCreateDto(): CreateUserDto {
    const v = this.form.getRawValue();
    return {
      username: v.username,
      firstName: v.firstName,
      lastName: v.lastName,
      displayName: v.displayName,
      email: v.email,
      randomPassword: v.randomPassword,
      password: v.randomPassword ? undefined : v.password, // MIGRATION: random ON -> server generates (User.ascx.vb L162-165)
      confirmPassword: v.randomPassword ? undefined : v.confirmPassword,
      question: this.requiresQuestionAndAnswer() ? v.question : undefined,
      answer: this.requiresQuestionAndAnswer() ? v.answer : undefined,
      authorize: v.authorize,
      notify: v.notify,
    };
  }

  /**
   * Builds the update FORM model. No `username` — UserInfo.Username is read-only post-creation, so it is
   * absent from UpdateUserDto.
   */
  private buildUpdateDto(): UpdateUserDto {
    const v = this.form.getRawValue();
    return {
      firstName: v.firstName,
      lastName: v.lastName,
      displayName: v.displayName,
      email: v.email,
      affiliateId: v.affiliateId ?? undefined,
    };
  }

  /**
   * Adapts the create FORM model into the WIRE request consumed by UserService.createUser.
   *
   * MIGRATION: the create endpoint consumes CreateUserRequest, NOT the form-only CreateUserDto (the user
   * models contract is explicit: "Do NOT send this shape to the API directly"). `authorize` maps to
   * `approved` (legacy chkAuthorize -> Membership.Approved, User.ascx.vb L223). `portalID` defaults to the
   * primary/admin portal (0) and `isSuperUser` is false — the admin create flow provisions a portal user
   * and the form has no superuser toggle. The password is omitted when random-password is on (server generates).
   */
  private toCreateRequest(dto: CreateUserDto): CreateUserRequest {
    return {
      portalID: 0,
      username: dto.username,
      password: dto.password,
      displayName: dto.displayName,
      email: dto.email,
      firstName: dto.firstName,
      lastName: dto.lastName,
      isSuperUser: false,
      approved: dto.authorize ?? true,
    };
  }

  /**
   * Adapts the update FORM model into the WIRE request consumed by UserService.updateUser.
   *
   * MIGRATION: the update endpoint consumes UpdateUserRequest; the route id is stamped onto `userID` (the
   * server rejects a mismatch with HTTP 400). The legacy non-AddUser update path (User.ascx.vb L361-385)
   * does NOT change membership Approved/IsSuperUser, so both are preserved from the loaded user rather than
   * mutated by this form. `affiliateId` has no place on the wire contract and is dropped.
   */
  private toUpdateRequest(dto: UpdateUserDto, id: number): UpdateUserRequest {
    const loaded = this.currentUser();
    return {
      userID: id,
      displayName: dto.displayName,
      email: dto.email,
      firstName: dto.firstName,
      lastName: dto.lastName,
      isSuperUser: loaded?.isSuperUser ?? false,
      approved: loaded?.approved ?? false,
    };
  }

  /**
   * MIGRATION: RFC 7807 ProblemDetails -> field-level errors (serverErrors, forwarded to each
   * <app-form-controls>) plus a summary message (submitError). Replaces the legacy postback/ViewState
   * validation round-trip.
   */
  private handleServerError(err: ProblemDetails): void {
    this.serverErrors.set(err.errors ?? null);
    this.submitError.set(err.title ?? err.detail ?? 'An error occurred while saving the user.');
  }

  /** Opens the delete confirmation dialog. MIGRATION: replaces ClientAPI.AddButtonConfirm (User.ascx.vb L260). */
  openDeleteDialog(): void {
    this.deleteDialogOpen.set(true);
  }

  /** Dismisses the delete confirmation dialog without deleting. */
  cancelDelete(): void {
    this.deleteDialogOpen.set(false);
  }

  /** MIGRATION: cmdDelete_Click -> UserController.DeleteUser (User.ascx.vb L342-351). */
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
        error: (err: ProblemDetails) => this.handleServerError(err),
      });
  }

  /** Returns to the user list without saving. */
  cancel(): void {
    void this.router.navigate(['/users']);
  }
}
