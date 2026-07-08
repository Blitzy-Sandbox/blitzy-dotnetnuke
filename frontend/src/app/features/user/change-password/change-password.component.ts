import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { ChangePasswordRequest } from '../../../core/models';
import { FormFieldComponent } from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { UserService } from '../user.service';

/**
 * Safely reads a string control value from the untyped `AbstractControl.get()`
 * without leaking `any`. `AbstractControl.value` is typed `any` by the framework,
 * so it is captured as `unknown` and narrowed with a `typeof` guard before use.
 */
function controlValue(group: AbstractControl, name: string): string {
  const value: unknown = group.get(name)?.value;
  return typeof value === 'string' ? value : '';
}

// MIGRATION: legacy `If txtNewPassword.Text <> txtNewConfirm.Text` (Password.ascx.vb cmdUpdate_Click
// -> PasswordUpdateStatus.PasswordMismatch). Re-expressed as a FormGroup-level cross-field validator.
// Empty fields are left to each control's own `required` validator (avoids duplicate noise).
function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const newPassword = controlValue(group, 'newPassword');
  const confirmNewPassword = controlValue(group, 'confirmNewPassword');
  if (newPassword === '' || confirmNewPassword === '') {
    return null;
  }
  return newPassword === confirmNewPassword ? null : { passwordMismatch: true };
}

// MIGRATION: legacy `If Not IsAdmin And txtNewPassword.Text = txtOldPassword.Text`
// (-> PasswordUpdateStatus.PasswordNotDifferent). The legacy `Not IsAdmin` guard is now a backend
// concern (admin-initiated resets are authorized server-side); the self-service client always enforces
// new != old for functional parity.
function newPasswordDiffersValidator(group: AbstractControl): ValidationErrors | null {
  const oldPassword = controlValue(group, 'oldPassword');
  const newPassword = controlValue(group, 'newPassword');
  if (oldPassword === '' || newPassword === '') {
    return null;
  }
  return oldPassword === newPassword ? { passwordNotDifferent: true } : null;
}

// MIGRATION: Website/admin/Users/Password.ascx `pnlChange` panel. Postback/ViewState/IClientAPICallback
// eliminated (AAP §0.6.3) — stateless typed reactive form submitted over HTTP.
// MIGRATION: the secondary legacy panels `pnlReset` (reset via security answer -> cmdReset_Click) and
// `pnlQA` (change password question/answer -> cmdUpdateQA_Click) are intentionally OUT OF SCOPE here;
// only the primary { oldPassword, newPassword } change flow is implemented.
// MIGRATION: FormFieldComponent already composes `appValidationHighlight` + `[appAutofocus]` on its own
// internal <input>, so the folder-spec's ValidationHighlight/Autofocus directive requirement is satisfied
// WITHOUT importing those directives here (an unused import would trigger NG8113 and fail the 0-warnings
// Gate 3). Autofocus on the first field is delegated via FormFieldComponent's `[autofocus]="true"` input.
@Component({
  selector: 'app-change-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormFieldComponent, LoadingSpinnerComponent],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss',
})
export class ChangePasswordComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  // Target user id from the ':id' route param (route path ':id/password' in user.routes.ts).
  private readonly userId = Number(this.route.snapshot.paramMap.get('id') ?? 0);

  readonly form = this.fb.group(
    {
      // MIGRATION: legacy txtOldPassword (row trOldPassword). Legacy required it only when `Not IsAdmin`;
      // admin-initiated resets are authorized server-side now, so the self-service flow always requires
      // the current password (align to backend policy).
      oldPassword: this.fb.control('', { validators: [Validators.required] }),
      // MIGRATION: legacy txtNewPassword + UserController.ValidatePassword. minLength(7) mirrors the
      // ASP.NET Membership MinRequiredPasswordLength default; the BACKEND re-validates the full password
      // policy and is the authority (do not over-constrain the client and reject backend-valid input).
      newPassword: this.fb.control('', {
        validators: [Validators.required, Validators.minLength(7)],
      }),
      // MIGRATION: legacy txtNewConfirm.
      confirmNewPassword: this.fb.control('', { validators: [Validators.required] }),
    },
    { validators: [passwordsMatchValidator, newPasswordDiffersValidator] },
  );

  // Stable message maps (stable object references avoid re-pushing new literals into the child
  // FormFieldComponent signal inputs on every OnPush cycle). Text mirrors the legacy validator feedback.
  readonly oldPasswordErrorMessages: Record<string, string> = {
    required: 'You must enter your current password.',
  };
  readonly newPasswordErrorMessages: Record<string, string> = {
    required: 'You must enter a new password.',
    minlength: 'Your new password does not meet the minimum length requirement.',
  };
  readonly confirmNewPasswordErrorMessages: Record<string, string> = {
    required: 'You must confirm your new password.',
  };

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Bridge non-signal reactive-form state into a signal so the OnPush cross-field computeds recompute
  // when the group validity/touched state changes (same pattern as FormFieldComponent). Field
  // initializers run in an injection context, so `toSignal`/`inject` are valid here.
  private readonly formEvents = toSignal(this.form.events, { initialValue: null });

  // MIGRATION: legacy PasswordMismatch — surfaced once the confirmation field has been interacted with.
  readonly showMismatchError = computed<boolean>(() => {
    this.formEvents();
    const confirm = this.form.controls.confirmNewPassword;
    return this.form.hasError('passwordMismatch') && (confirm.touched || confirm.dirty);
  });

  // MIGRATION: legacy PasswordNotDifferent — surfaced once the new-password field has been interacted with.
  readonly showNotDifferentError = computed<boolean>(() => {
    this.formEvents();
    const newPassword = this.form.controls.newPassword;
    return this.form.hasError('passwordNotDifferent') && (newPassword.touched || newPassword.dirty);
  });

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { oldPassword, newPassword } = this.form.getRawValue();
    // SECURITY: password values live ONLY in this request body — never persisted, logged, or displayed.
    const request: ChangePasswordRequest = { oldPassword, newPassword };

    this.submitting.set(true);
    this.errorMessage.set(null);

    // MIGRATION: legacy cmdUpdate_Click -> UserController.ChangePassword(User, old, new).
    this.userService
      .changePassword(this.userId, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          // MIGRATION: legacy PasswordUpdated event -> return to the user detail screen.
          void this.router.navigate(['/users', this.userId]);
        },
        error: (problem: unknown) => {
          // MIGRATION: legacy PasswordUpdateStatus.PasswordResetFailed feedback.
          this.submitting.set(false);
          this.errorMessage.set(this.resolveErrorMessage(problem));
        },
      });
  }

  // ApiService normalizes all HTTP failures into an RFC 7807 ProblemDetails object
  // ({ type, title, status, detail?, ... }). Prefer `detail`, then `title`, else a generic message.
  private resolveErrorMessage(problem: unknown): string {
    if (problem !== null && typeof problem === 'object') {
      const details = problem as { detail?: unknown; title?: unknown };
      if (typeof details.detail === 'string' && details.detail.length > 0) {
        return details.detail;
      }
      if (typeof details.title === 'string' && details.title.length > 0) {
        return details.title;
      }
    }
    return 'Your password could not be changed. Please verify your current password and try again.';
  }
}
