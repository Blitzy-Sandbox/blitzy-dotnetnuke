// MIGRATION: Angular 19 standalone replacement for the legacy DotNetNuke Web Forms user editor
// `Website/admin/Users/User.ascx.vb` (419 lines; VB class DotNetNuke.Modules.Admin.Users.User,
// Inherits UserUserControlBase). ONE typed reactive form serves CREATE and EDIT (and the bare
// `:id` detail route, collapsed into EDIT). All Web Forms / ViewState / postback / PropertyEditor
// (UserEditor) / .resx machinery is discarded; only the validation + orchestration logic is
// re-expressed. Business rules are extracted verbatim (AAP 0.7.2 — no optimization).
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { UserService } from '../user.service';
import type { CreateUserRequest, UpdateUserRequest } from '../user.service';
import { AuthService } from '../../../core/auth/auth.service';
import type { ProblemDetails, User } from '../../../core/models';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';

/**
 * Strongly-typed reactive form model backing {@link UserFormComponent}.
 *
 * MIGRATION: mirrors the editable rows of the legacy PropertyEditor `UserEditor` plus the credential
 * controls (`txtPassword`, `txtConfirm`, `chkRandom`, `txtQuestion`, `txtAnswer`, `chkAuthorize`,
 * `chkNotify`, captcha) from `User.ascx.vb`. Credential controls are WRITE-ONLY: they are sent in
 * CreateUserRequest and NEVER read back from `User`.
 */
interface UserFormModel {
  username: FormControl<string>;
  email: FormControl<string>;
  displayName: FormControl<string>;
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  authorize: FormControl<boolean>;
  notify: FormControl<boolean>;
  password: FormControl<string>;
  confirmPassword: FormControl<string>;
  randomPassword: FormControl<boolean>;
  passwordQuestion: FormControl<string>;
  passwordAnswer: FormControl<string>;
  verificationCode: FormControl<string>;
}

// MIGRATION: legacy email validation expression `Globals.glbEmailRegEx`
// (Library/Components/Shared/Globals.vb L132) — the default `Security_EmailValidation` applied to
// the email editor by `User.ascx.vb` UserEditorCreated (L408-411). Preserved verbatim (TLD 2-4).
const EMAIL_PATTERN = /\b[a-zA-Z0-9._%\-+']+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,4}\b/;

// MIGRATION: legacy username allowed-character pattern. Required + non-empty at minimum; the
// username is the account key and becomes read-only once the user exists.
const USERNAME_PATTERN = /^[a-zA-Z0-9._%\-+']+$/;

/**
 * MIGRATION: group-level confirm-match check extracted from `User.ascx.vb` Validate
 * (L152-153: `If txtPassword.Text <> txtConfirm.Text Then createStatus = PasswordMismatch`). The
 * client enforces MATCH ONLY. The provider password policy (min length 7 / min 1 non-alphanumeric /
 * optional strength regex — `UserController.ValidatePassword` L1067-1091, invoked at L156) is
 * enforced SERVER-side and surfaced as RFC 7807 field errors. When a random password is requested
 * the server generates it (L162-165), so no client check applies.
 */
function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const randomControl = group.get('randomPassword');
  if (randomControl?.value === true) {
    return null;
  }
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-user-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormControlComponent, ConfirmationDialogComponent],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
})
export class UserFormComponent {
  private readonly userService = inject(UserService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  // MIGRATION: the `:id` route param is bound via withComponentInputBinding() (app.config.ts). When
  // present (`:id` / `:id/edit`) the component is in EDIT mode; when absent (`new`) it is in CREATE
  // mode. Legacy ManageUsers had distinct Add/Edit/View states; the bare `:id` (detail) state is
  // collapsed into EDIT here — read-only/authorization enforcement is performed server-side.
  readonly id = input<string>();
  readonly mode = computed<'create' | 'edit'>(() => (this.id() ? 'edit' : 'create'));

  // Component state (signals) — replaces ViewState / postback bookkeeping.
  readonly loadedUser = signal<User | null>(null);
  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly problem = signal<ProblemDetails | null>(null);
  readonly showDeleteConfirm = signal(false);
  readonly randomSelected = signal(false);

  // MIGRATION: `User.ascx.vb` UseCaptcha (L54-59) = `Security_CaptchaRegister` AND IsRegister.
  // CAPTCHA renders only in public self-registration; this admin editor defaults it off. A future
  // public-registration flow can flip this signal.
  readonly showCaptcha = signal(false);

  // MIGRATION: server-side RFC 7807 errors. `errors` is a duality (Record<string,string[]> for
  // [ApiController] model-validation 400s vs flat string[] for Result failures). `serverErrors`
  // feeds each per-field <app-form-control>; `errorSummary` surfaces the flat, form-level messages.
  readonly serverErrors = computed<ProblemDetails['errors'] | undefined>(() => this.problem()?.errors);
  readonly errorSummary = computed<string[]>(() => {
    const errors = this.problem()?.errors;
    return Array.isArray(errors) ? errors : [];
  });

  // MIGRATION: `IsUser` (a user editing their own account) drives the "UnRegister" vs "Delete"
  // label/confirm (User.ascx.vb DataBind L257-259, L270-274).
  readonly isSelf = computed<boolean>(() => {
    const user = this.loadedUser();
    const current = this.auth.currentUser();
    return user !== null && current !== null && user.userId === current.userId;
  });
  readonly deleteLabel = computed<string>(() => (this.isSelf() ? 'Unregister' : 'Delete'));

  // MIGRATION: `User.ascx.vb` DataBind delete-button visibility (L264-274). AddUser hides delete
  // (L264-265). In edit it is hidden when the target is the portal Administrator
  // (User.UserID = AdministratorId) OR when editing one's own superuser account
  // (IsUser And User.IsSuperUser) (L267). The AdministratorId comparison is enforced SERVER-side
  // (403) because AdministratorId is not present in the JWT principal; the client suppresses the
  // self+superuser case here.
  readonly canDelete = computed<boolean>(() => {
    if (this.mode() !== 'edit') {
      return false;
    }
    const user = this.loadedUser();
    if (user === null) {
      return false;
    }
    if (this.isSelf() && user.isSuperUser) {
      return false;
    }
    return true;
  });

  readonly form = new FormGroup<UserFormModel>(
    {
      username: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(USERNAME_PATTERN)],
      }),
      email: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(EMAIL_PATTERN)],
      }),
      displayName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required],
      }),
      firstName: new FormControl('', { nonNullable: true }),
      lastName: new FormControl('', { nonNullable: true }),
      authorize: new FormControl(false, { nonNullable: true }),
      notify: new FormControl(false, { nonNullable: true }),
      password: new FormControl('', { nonNullable: true }),
      confirmPassword: new FormControl('', { nonNullable: true }),
      randomPassword: new FormControl(false, { nonNullable: true }),
      passwordQuestion: new FormControl('', { nonNullable: true }),
      passwordAnswer: new FormControl('', { nonNullable: true }),
      verificationCode: new FormControl('', { nonNullable: true }),
    },
    { validators: passwordMatchValidator },
  );

  constructor() {
    // MIGRATION: replaces the postback load. When an id is bound (edit), load the user and patch the
    // form; create mode leaves the form at its empty defaults (User.ascx.vb DataBind L255-262).
    effect(() => {
      const idValue = this.id();
      if (idValue) {
        this.loadUser(Number(idValue));
      }
    });

    // MIGRATION: `chkRandom` toggle (User.ascx.vb L162-165). When a random password is requested the
    // server generates it, so the password + confirm inputs are disabled (and excluded from the DTO).
    this.form.controls.randomPassword.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((random) => {
        this.randomSelected.set(random);
        if (random) {
          this.form.controls.password.disable();
          this.form.controls.confirmPassword.disable();
        } else {
          this.form.controls.password.enable();
          this.form.controls.confirmPassword.enable();
        }
      });
  }

  /**
   * MIGRATION: edit-mode load. ONLY non-credential fields are patched — `User` carries ZERO
   * credential fields (password/question/answer live in the Identity store, never on the entity).
   * `username` is the account key and is disabled once loaded (it is not part of UpdateUserRequest).
   */
  private loadUser(id: number): void {
    this.loading.set(true);
    this.userService
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (user) => {
          this.loadedUser.set(user);
          this.patchForm(user);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.problem.set(this.toProblemDetails(error));
          this.loading.set(false);
        },
      });
  }

  private patchForm(user: User): void {
    this.form.patchValue({
      username: user.username,
      email: user.email ?? '',
      displayName: user.displayName ?? '',
      firstName: user.firstName ?? '',
      lastName: user.lastName ?? '',
      // MIGRATION: `chkAuthorize` reflects Membership.Approved (User.ascx.vb L222-224).
      authorize: user.isApproved,
    });
    this.form.controls.username.disable();
  }

  /**
   * MIGRATION: `cmdUpdate_Click` (User.ascx.vb L361-385). AddUser -> validate then CreateUser
   * (L362-365); otherwise update the editable fields (L367-376). The legacy Validate order
   * (captcha -> core fields -> password match/policy -> Q&A, L133-195) is preserved: captcha + core
   * fields + the confirm-match run client-side; the provider password/Q&A policy runs server-side.
   */
  submit(): void {
    this.problem.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    if (this.mode() === 'create') {
      this.userService
        .create(this.buildCreateRequest())
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.submitting.set(false);
            void this.router.navigate(['/users']);
          },
          error: (error: unknown) => {
            this.problem.set(this.toProblemDetails(error));
            this.submitting.set(false);
          },
        });
      return;
    }

    const idValue = this.id();
    if (!idValue) {
      this.submitting.set(false);
      return;
    }
    this.userService
      .update(Number(idValue), this.buildUpdateRequest())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          void this.router.navigate(['/users']);
        },
        error: (error: unknown) => {
          this.problem.set(this.toProblemDetails(error));
          this.submitting.set(false);
        },
      });
  }

  /**
   * MIGRATION: `cmdDelete_Click` (User.ascx.vb L342-351) -> `UserController.DeleteUser(User, True,
   * False)` (soft delete, no notification). Self uses an "Unregister" label/confirm; admin uses
   * "Delete" (L270-274). Hidden entirely for protected users (see {@link canDelete}).
   */
  delete(): void {
    if (!this.canDelete()) {
      return;
    }
    const idValue = this.id();
    if (!idValue) {
      return;
    }
    this.submitting.set(true);
    this.userService
      .delete(Number(idValue))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showDeleteConfirm.set(false);
          void this.router.navigate(['/users']);
        },
        error: (error: unknown) => {
          this.problem.set(this.toProblemDetails(error));
          this.submitting.set(false);
          this.showDeleteConfirm.set(false);
        },
      });
  }

  requestDelete(): void {
    this.showDeleteConfirm.set(true);
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
  }

  cancel(): void {
    void this.router.navigate(['/users']);
  }

  /**
   * MIGRATION: builds CreateUserRequest. Password/confirm are omitted when a random password is
   * requested (server generates it, L162-165). Q&A is sent only when provided (L168-183) and the
   * verification code only when the captcha is shown (L54-59).
   */
  private buildCreateRequest(): CreateUserRequest {
    const value = this.form.getRawValue();
    const request: CreateUserRequest = {
      username: value.username,
      email: value.email,
      displayName: value.displayName,
      firstName: value.firstName,
      lastName: value.lastName,
      randomPassword: value.randomPassword,
      authorize: value.authorize,
      notify: value.notify,
    };
    if (!value.randomPassword) {
      request.password = value.password;
      request.confirmPassword = value.confirmPassword;
    }
    if (value.passwordQuestion) {
      request.passwordQuestion = value.passwordQuestion;
    }
    if (value.passwordAnswer) {
      request.passwordAnswer = value.passwordAnswer;
    }
    if (this.showCaptcha() && value.verificationCode) {
      request.verificationCode = value.verificationCode;
    }
    return request;
  }

  /**
   * MIGRATION: builds UpdateUserRequest. `username` (the key) and credentials are NOT updatable here
   * (ongoing password changes route through the `features/auth` feature per AAP 0.4.2). The legacy
   * editor short-circuited on `UserEditor.IsDirty` (L367); this always sends the editable fields — a
   * documented divergence (the server treats an unchanged payload as a no-op).
   */
  private buildUpdateRequest(): UpdateUserRequest {
    const value = this.form.getRawValue();
    return {
      email: value.email,
      displayName: value.displayName,
      firstName: value.firstName,
      lastName: value.lastName,
      authorize: value.authorize,
    };
  }

  private toProblemDetails(error: unknown): ProblemDetails | null {
    if (error instanceof HttpErrorResponse && typeof error.error === 'object' && error.error !== null) {
      return error.error as ProblemDetails;
    }
    return null;
  }
}
