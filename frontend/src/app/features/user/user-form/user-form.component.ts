/**
 * UserFormComponent — dual-mode (create + edit) user-management form.
 *
 * A single net-new standalone Angular 19 component that reproduces the legacy DNN
 * admin user editor. It is lazy-loaded by the sibling `user.routes.ts` at BOTH
 * `'new'` (create) and `':id'` (edit) via
 *   import('./user-form/user-form.component').then((m) => m.UserFormComponent)
 *
 * MIGRATION lineage (REFERENCE ONLY — the VB is not transliterated):
 *  - Website/admin/Users/User.ascx(.vb)        — create/edit/delete + validators
 *                                                 (username/first/last/email required,
 *                                                 password compare/strength, random
 *                                                 password generation, authorize/notify,
 *                                                 delete-with-confirm).
 *  - Website/admin/Users/Membership.ascx(.vb)  — approve/lockout state (edit-mode
 *                                                 `approved` toggle).
 *  - Website/admin/Users/Profile.ascx(.vb)     — address/contact profile fields (edit).
 *  - Website/admin/Users/ManageUsers.ascx(.vb) — tabbed User+Membership+Profile
 *                                                 orchestration collapsed into sections
 *                                                 within ONE form (AAP §0.6.3; tabs and
 *                                                 ViewState/postback are ELIMINATED —
 *                                                 stateless HTTP + Angular Signals).
 *
 * All HTTP flows through the injected `UserService`; this component contains no
 * envelope/RFC-7807 logic and no business rules beyond form parity with the legacy UI.
 */
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { CreateUserRequest, ProblemDetails, UpdateUserRequest, User } from '../../../core/models';
import { CreateUserResult, UserService } from '../user.service';
// MIGRATION/BUILD NOTE: `FormFieldComponent` is imported from its CONCRETE file path
// because the `form-controls` folder ships no `index.ts` barrel (unlike the sibling
// `confirmation-dialog/` and `directives/` folders, which do). The concrete file is the
// exact declared dependency; importing a non-existent folder barrel would fail
// `ng build` module resolution (Gate 3).
import { FormFieldComponent } from '../../../shared/components/form-controls/form-field.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ValidationHighlightDirective } from '../../../shared/directives';

/** Minimum password length applied in CREATE mode when a manual password is entered. */
// MIGRATION: legacy User.ascx ValidatePassword() used MembershipProviderConfig.MinPasswordLength
// (no fixed value in source); a sensible 7-char default is used here. See MIGRATION_NOTES.md.
const MIN_PASSWORD_LENGTH = 7;

/** Strongly-typed control map for the single superset user form (create ∪ edit). */
interface UserFormControls {
  // Shared (both modes)
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  displayName: FormControl<string>;
  email: FormControl<string>;
  // Create-only
  username: FormControl<string>;
  randomPassword: FormControl<boolean>;
  password: FormControl<string>;
  confirmPassword: FormControl<string>;
  passwordQuestion: FormControl<string>;
  passwordAnswer: FormControl<string>;
  authorize: FormControl<boolean>;
  notify: FormControl<boolean>;
  // Edit-only
  affiliateID: FormControl<number>;
  approved: FormControl<boolean>;
  // Edit-only profile
  street: FormControl<string>;
  unit: FormControl<string>;
  city: FormControl<string>;
  region: FormControl<string>;
  country: FormControl<string>;
  postalCode: FormControl<string>;
  telephone: FormControl<string>;
  cell: FormControl<string>;
  fax: FormControl<string>;
  website: FormControl<string>;
  im: FormControl<string>;
  preferredLocale: FormControl<string>;
  timeZone: FormControl<number>;
}

type UserFormGroup = FormGroup<UserFormControls>;

/**
 * Group-level cross-field validator asserting password === confirmPassword.
 * MIGRATION: legacy User.ascx CompareValidator (UserCreateStatus.PasswordMismatch).
 * Skipped when randomPassword is true (the backend generates the password).
 */
function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const g = group as UserFormGroup;
  if (g.controls.randomPassword.value) {
    return null;
  }
  const password = g.controls.password.value;
  const confirm = g.controls.confirmPassword.value;
  return confirm.length > 0 && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-user-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormFieldComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    ValidationHighlightDirective,
  ],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.scss',
})
export class UserFormComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** Raw ':id' route param captured once from the snapshot (null in create mode). */
  private readonly routeId = this.route.snapshot.paramMap.get('id');
  /** Convenience boolean for construction-time branching (validators, group validator). */
  private readonly editing = this.routeId !== null;

  // --- Reactive UI state (Signals) ---
  readonly idParam = signal<string | null>(this.routeId);
  readonly isEditMode = computed(() => this.idParam() !== null);
  readonly userId = computed<number | null>(() => {
    const value = this.idParam();
    return value !== null ? Number(value) : null;
  });
  readonly loading = signal(false); // true while fetching the user (edit)
  readonly saving = signal(false); // true while POST/PUT/DELETE in flight
  readonly busy = computed(() => this.loading() || this.saving());
  readonly loadingMessage = computed(() => (this.saving() ? 'Saving…' : 'Loading…'));
  readonly showPasswordFields = signal(!this.editing); // create + manual password → visible
  readonly showDeleteDialog = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly loadedUser = signal<User | null>(null);

  // --- R10 Issue 7: field-level surfacing of a duplicate-user 409 Conflict ---
  // The backend rejects a duplicate (PortalID, Username) create with a 409 whose RFC 7807
  // detail/title carries the SPECIFIC business message (UserService: "A user with the username
  // '...' already exists in portal N."). Previously the component showed only the generic
  // "Unable to save the user..." banner, which was not actionable. These signals hold that
  // server message so it can be rendered INLINE on the offending control (User Name, or Email
  // when the message concerns the email), keyed under a custom `duplicate` validation error.
  readonly usernameServerError = signal<string | null>(null);
  readonly emailServerError = signal<string | null>(null);
  /** errorMessages override injecting the server 409 message under the custom `duplicate` key. */
  readonly usernameErrorMessages = computed<Record<string, string>>(() => {
    const message = this.usernameServerError();
    const messages: Record<string, string> = {};
    if (message) {
      // Bracket access is required: noPropertyAccessFromIndexSignature forbids dot access on
      // a Record index signature.
      messages['duplicate'] = message;
    }
    return messages;
  });
  readonly emailErrorMessages = computed<Record<string, string>>(() => {
    const message = this.emailServerError();
    const messages: Record<string, string> = {};
    if (message) {
      messages['duplicate'] = message;
    }
    return messages;
  });

  // --- QA finding F3: one-time generated-password hand-off (create mode) ---
  /**
   * The server-generated temporary password to reveal after a successful random-password
   * create, or null when there is nothing to reveal (manual password, or already dismissed).
   * MIGRATION: legacy User.ascx surfaced the generated password inline after CreateUser;
   * here it is shown in an acknowledgement dialog before navigating back to the list.
   */
  readonly generatedPassword = signal<string | null>(null);
  /** Controls the generated-password acknowledgement dialog (create mode only). */
  readonly showPasswordDialog = signal(false);

  /**
   * Delete allowed only in edit mode for a loaded, non-superuser account that is
   * not the current user.
   * MIGRATION: legacy User.ascx hid cmdDelete when AddUser, and for the portal
   * Administrator / a superuser edited by a normal user. AdministratorId is not
   * available client-side; superuser + self checks are the closest parity.
   */
  readonly canDelete = computed<boolean>(() => {
    const user = this.loadedUser();
    if (!this.isEditMode() || user === null || user.isSuperUser) {
      return false;
    }
    const current = this.authService.currentUser();
    return current === null || current.userID !== user.userID;
  });

  /** The single superset form (create ∪ edit); fields/validators applied per mode. */
  readonly form: UserFormGroup = this.buildForm();

  private buildForm(): UserFormGroup {
    // username is required ONLY in create mode; in edit it stays empty & validator-free
    // so it never blocks the form's validity.
    const usernameValidators = this.editing ? [] : [Validators.required];
    return this.fb.group<UserFormControls>(
      {
        firstName: this.fb.control('', { validators: [Validators.required] }),
        lastName: this.fb.control('', { validators: [Validators.required] }),
        displayName: this.fb.control(''),
        // MIGRATION: legacy validators required a well-formed e-mail → Validators.email.
        email: this.fb.control('', { validators: [Validators.required, Validators.email] }),
        username: this.fb.control('', { validators: usernameValidators }),
        randomPassword: this.fb.control(true),
        password: this.fb.control(''),
        confirmPassword: this.fb.control(''),
        passwordQuestion: this.fb.control(''),
        passwordAnswer: this.fb.control(''),
        authorize: this.fb.control(true),
        notify: this.fb.control(true),
        affiliateID: this.fb.control(0),
        approved: this.fb.control(false),
        street: this.fb.control(''),
        unit: this.fb.control(''),
        city: this.fb.control(''),
        region: this.fb.control(''),
        country: this.fb.control(''),
        postalCode: this.fb.control(''),
        telephone: this.fb.control(''),
        cell: this.fb.control(''),
        fax: this.fb.control(''),
        website: this.fb.control(''),
        im: this.fb.control(''),
        preferredLocale: this.fb.control(''),
        timeZone: this.fb.control(0),
      },
      // MIGRATION: CompareValidator → group-level cross-field validator (create only).
      { validators: this.editing ? [] : [passwordMatchValidator] },
    );
  }

  ngOnInit(): void {
    if (this.isEditMode()) {
      this.loadUser();
      return;
    }
    // CREATE mode: initialise password visibility/validators from the default
    // randomPassword=true, then react to the checkbox.
    this.applyPasswordMode(this.form.controls.randomPassword.value);
    this.form.controls.randomPassword.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((isRandom) => this.applyPasswordMode(isRandom));
  }

  /**
   * MIGRATION: legacy chkRandom — when checked, DNN GeneratePassword() creates the
   * password server-side, so the password/confirm inputs are hidden, disabled and
   * validator-free; when unchecked they become required (+ min length).
   * NOTE: randomPassword defaults to TRUE here (folder-spec mandate); the legacy
   * User.ascx loaded with chkRandom.Checked=False — documented in MIGRATION_NOTES.md.
   */
  private applyPasswordMode(isRandom: boolean): void {
    const password = this.form.controls.password;
    const confirmPassword = this.form.controls.confirmPassword;
    this.showPasswordFields.set(!isRandom);
    if (isRandom) {
      password.clearValidators();
      confirmPassword.clearValidators();
      password.disable({ emitEvent: false });
      confirmPassword.disable({ emitEvent: false });
    } else {
      password.setValidators([Validators.required, Validators.minLength(MIN_PASSWORD_LENGTH)]);
      confirmPassword.setValidators([Validators.required]);
      password.enable({ emitEvent: false });
      confirmPassword.enable({ emitEvent: false });
    }
    // R10 Issue 11: reset value + interaction state on every mode switch so stale
    // touched/dirty flags and stale values from a previous manual-entry attempt (or a
    // prior failed-submit markAllAsTouched) cannot surface premature "required"/"mismatch"
    // errors the instant the checkbox is toggled. reset('') both clears the value and marks
    // the control pristine + untouched, and it re-runs the validators just configured above:
    // a fresh manual mode is therefore INVALID (empty) yet UNTOUCHED, and FormFieldComponent
    // only renders a message once the control is touched OR dirty. reset() preserves the
    // disabled state set for random mode. This also clears the group-level passwordMismatch
    // error because confirmPassword returns to length 0.
    password.reset('', { emitEvent: false });
    confirmPassword.reset('', { emitEvent: false });
  }

  private loadUser(): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    this.loading.set(true);
    this.errorMessage.set(null);
    this.userService
      .getUser(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (user) => {
          this.patchUser(user);
          this.loadedUser.set(user);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.errorMessage.set('Unable to load the requested user.');
        },
      });
  }

  private patchUser(user: User): void {
    // SECURITY / MIGRATION: never patch password/question/answer — the read User
    // model carries no secrets (they are write-only, create-mode fields).
    this.form.patchValue({
      username: user.username,
      firstName: user.firstName,
      lastName: user.lastName,
      // MIGRATION: legacy DisplayName auto-formats from Security_DisplayNameFormat;
      // here it is an optional editable field seeded from the loaded value.
      displayName: user.displayName,
      email: user.email,
      affiliateID: user.affiliateID,
      // MIGRATION: Membership.ascx approve state → single `approved` toggle.
      approved: user.membership.approved,
      street: user.profile.street,
      unit: user.profile.unit,
      city: user.profile.city,
      region: user.profile.region,
      country: user.profile.country,
      postalCode: user.profile.postalCode,
      telephone: user.profile.telephone,
      cell: user.profile.cell,
      fax: user.profile.fax,
      website: user.profile.website,
      im: user.profile.im,
      preferredLocale: user.profile.preferredLocale,
      timeZone: user.profile.timeZone,
    });
    // MIGRATION: Membership.ascx cmdUnLock (unlock account) and cmdPassword (force
    // password change) have no UpdateUserRequest field and are intentionally not
    // surfaced as form controls here — future dedicated actions/endpoints.
  }

  onSubmit(): void {
    // MIGRATION: legacy postback save (cmdUpdate_Click) → stateless POST (create) / PUT (edit).
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (this.isEditMode()) {
      this.submitUpdate();
    } else {
      this.submitCreate();
    }
  }

  private submitCreate(): void {
    // MIGRATION: legacy User.ascx ctlCaptcha is a registration-only control; the admin
    // create flow never presented a captcha, so it is intentionally omitted here.
    const raw = this.form.getRawValue();
    const useRandom = raw.randomPassword;
    const request: CreateUserRequest = {
      username: raw.username,
      firstName: raw.firstName,
      lastName: raw.lastName,
      displayName: raw.displayName ? raw.displayName : undefined,
      email: raw.email,
      // MIGRATION: legacy GeneratePassword() — when randomPassword is true the backend
      // generates the password, so an empty string is sent (password is a required field).
      password: useRandom ? '' : raw.password,
      confirmPassword: useRandom ? undefined : raw.confirmPassword,
      passwordQuestion: raw.passwordQuestion ? raw.passwordQuestion : undefined,
      passwordAnswer: raw.passwordAnswer ? raw.passwordAnswer : undefined,
      // portalID comes from the authenticated admin's context (never prompted).
      portalID: this.authService.currentUser()?.portalID ?? 0,
      authorize: raw.authorize, // MIGRATION: chkAuthorize → Membership.Approved
      notify: raw.notify, // MIGRATION: chkNotify
      randomPassword: useRandom,
    };
    this.saving.set(true);
    this.errorMessage.set(null);
    // R10 Issue 7: clear any prior duplicate-conflict message before the new attempt.
    this.usernameServerError.set(null);
    this.emailServerError.set(null);
    this.userService
      .createUser(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.onCreateSuccess(result),
        error: (err: unknown) => this.onSaveError(err),
      });
  }

  private submitUpdate(): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    const raw = this.form.getRawValue();
    const request: UpdateUserRequest = {
      firstName: raw.firstName,
      lastName: raw.lastName,
      displayName: raw.displayName ? raw.displayName : undefined,
      email: raw.email,
      affiliateID: raw.affiliateID,
      approved: raw.approved, // MIGRATION: Membership.ascx cmdAuthorize/cmdUnAuthorize
      street: raw.street ? raw.street : undefined,
      unit: raw.unit ? raw.unit : undefined,
      city: raw.city ? raw.city : undefined,
      region: raw.region ? raw.region : undefined,
      country: raw.country ? raw.country : undefined,
      postalCode: raw.postalCode ? raw.postalCode : undefined,
      telephone: raw.telephone ? raw.telephone : undefined,
      cell: raw.cell ? raw.cell : undefined,
      fax: raw.fax ? raw.fax : undefined,
      website: raw.website ? raw.website : undefined,
      im: raw.im ? raw.im : undefined,
      preferredLocale: raw.preferredLocale ? raw.preferredLocale : undefined,
      timeZone: raw.timeZone, // required number — always supplied
    };
    this.saving.set(true);
    this.errorMessage.set(null);
    // R10 Issue 7: clear any prior duplicate-conflict message before the new attempt.
    this.usernameServerError.set(null);
    this.emailServerError.set(null);
    this.userService
      .updateUser(id, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.onSaveSuccess(),
        error: (err: unknown) => this.onSaveError(err),
      });
  }

  /** MIGRATION: legacy cmdDelete — open the shared confirmation dialog first. */
  requestDelete(): void {
    this.showDeleteDialog.set(true);
  }

  confirmDelete(): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    this.saving.set(true);
    this.errorMessage.set(null);
    this.userService
      .deleteUser(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.navigateToList();
        },
        error: (err: unknown) => this.onSaveError(err),
      });
  }

  cancel(): void {
    this.navigateToList();
  }

  /**
   * QA finding F4: navigate to the change-password screen for the user being edited. The route
   * (/users/:id/password) and ChangePasswordComponent already existed but had NO UI entry point,
   * so the screen was unreachable. In-app router navigation is used deliberately: the JWT session
   * is memory-only (core/auth/auth.service.ts), so a full-page reload would clear it. MIGRATION:
   * legacy ManageUsers.ascx "Manage Password" tab (Password.ascx).
   */
  changePassword(): void {
    const id = this.userId();
    if (id === null) {
      return;
    }
    void this.router.navigate(['/users', id, 'password']);
  }

  /**
   * QA finding F3: handle a successful CREATE. When the account was created with a random
   * password the backend returns a one-time, non-retrievable generated password
   * (result.generatedPassword); reveal it in an acknowledgement dialog and DEFER navigation
   * until the admin dismisses it, so the password is not lost. With a manually supplied password
   * there is nothing to reveal, so this behaves exactly like a normal save.
   */
  private onCreateSuccess(result: CreateUserResult): void {
    this.saving.set(false);
    if (result.generatedPassword) {
      this.generatedPassword.set(result.generatedPassword);
      this.showPasswordDialog.set(true);
      return;
    }
    this.navigateToList();
  }

  /**
   * QA finding F3: the admin has acknowledged (copied) the generated password. Close the dialog,
   * clear the in-memory password, and continue to the users list.
   */
  acknowledgeGeneratedPassword(): void {
    this.showPasswordDialog.set(false);
    this.generatedPassword.set(null);
    this.navigateToList();
  }

  private onSaveSuccess(): void {
    this.saving.set(false);
    this.navigateToList();
  }

  /**
   * R10 Issue 7: a duplicate-user create/update is rejected by the backend with a 409 Conflict
   * whose RFC 7807 detail/title carries the SPECIFIC, non-sensitive business message
   * (UserService: "A user with the username '...' already exists in portal N."). Surface that
   * message INLINE on the offending control (User Name, or Email when the message concerns the
   * email) so it is actionable, instead of the generic save banner. Any other failure (or a 409
   * that does not map to a visible field, e.g. refusing to delete a portal administrator) keeps
   * the banner behaviour.
   */
  private onSaveError(error?: unknown): void {
    this.saving.set(false);
    const problem = this.asProblemDetails(error);

    if (problem?.status === 409) {
      const message = problem.detail ?? problem.title ?? 'That value is already in use.';
      // The User Name field is rendered ONLY in create mode; Email is rendered in both. Route the
      // message to Email when it concerns the email, to User Name for the primary create-duplicate
      // case, and otherwise fall back to the banner (no matching visible field, e.g. a delete
      // conflict for a portal administrator).
      if (/e-?mail/i.test(message)) {
        this.applyFieldConflict('email', message);
        return;
      }
      if (!this.isEditMode()) {
        this.applyFieldConflict('username', message);
        return;
      }
      this.errorMessage.set(message);
      return;
    }

    this.errorMessage.set('Unable to save the user. Please review the form and try again.');
  }

  /**
   * Narrows an unknown thrown value to the RFC 7807 {@link ProblemDetails} rethrown by
   * ApiService.handleError (which normalises every HttpErrorResponse to a ProblemDetails with a
   * numeric `status`). Returns null for anything else so callers fall back to generic handling.
   */
  private asProblemDetails(error: unknown): ProblemDetails | null {
    if (
      error !== null &&
      typeof error === 'object' &&
      'status' in error &&
      typeof (error as { status: unknown }).status === 'number'
    ) {
      return error as ProblemDetails;
    }
    return null;
  }

  /**
   * R10 Issue 7: attach the server's duplicate-conflict message to a specific control so
   * FormFieldComponent renders it inline (via the matching `[errorMessages]` override keyed on
   * `duplicate`). setErrors marks the form invalid until the admin changes the value; editing that
   * control re-runs its validators, which REPLACE control.errors and thus clear this custom error
   * automatically. markAsTouched guarantees the message is shown immediately.
   */
  private applyFieldConflict(controlName: 'username' | 'email', message: string): void {
    const control =
      controlName === 'username' ? this.form.controls.username : this.form.controls.email;
    if (controlName === 'username') {
      this.usernameServerError.set(message);
    } else {
      this.emailServerError.set(message);
    }
    control.setErrors({ ...(control.errors ?? {}), duplicate: true });
    control.markAsTouched();
    control.markAsDirty();
  }

  private navigateToList(): void {
    // router.navigate returns a Promise; the result is intentionally ignored.
    void this.router.navigate(['/users']);
  }
}
