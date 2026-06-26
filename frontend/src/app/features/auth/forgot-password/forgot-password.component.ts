// MIGRATION: Website/admin/Security/SendPassword.ascx.vb (DotNetNuke.Modules.Admin.Security.SendPassword :
// UserModuleBase, 278 lines) -> ForgotPasswordComponent. Re-expresses ONLY the password-reminder validation +
// request orchestration as an Angular 19 standalone, PUBLIC (no-guard) screen mounted at /auth/forgot-password.
// The legacy Web Forms postback/ViewState/PortalModuleBase/.resx/Skin.AddModuleMessage machinery is DISCARDED;
// all server-side work (user lookup by username or uniquely-matching email, UserController.GetPassword,
// Mail.SendMail PasswordReminder, EventLog PASSWORD_SENT_*) now lives in the BACKEND. This component only
// collects input, POSTs the request, and shows a confirmation.
//
// MIGRATION: BACKEND ENDPOINT GAP -- the pending-created AuthController
// (backend/src/DnnMigration.Api/Controllers/AuthController.cs) exposes ONLY login/refresh/logout/me, and the
// frontend AuthService exposes only login/refresh/logout/me; there is NO forgot-password (password-reminder)
// endpoint yet. This component therefore issues the request through the GENERIC ApiService (not AuthService):
// api.post('auth/forgot-password', payload). The path is RELATIVE -- ApiService roots every request at
// environment.apiUrl (already /api/v1), so this resolves to ${apiUrl}/auth/forgot-password and will work once the
// backend adds the endpoint. Flagged for the root-owned MIGRATION_NOTES.md (this folder cannot edit it).
//
// MIGRATION: NON-ENUMERATION DIVERGENCE -- the legacy code ENUMERATED accounts (distinct UsernameError/EmailError
// vs PasswordSent module messages). For security the SPA adopts a non-enumeration policy: on success AND on any
// account-related ("benign") error it shows the SAME generic confirmation and sets submitted=true regardless of
// whether the account existed. Only transport/validation/rate-limit failures (status 0/400/422/429) surface a
// real error (via parseProblemDetails) -- the response never reveals account existence. Flagged for
// MIGRATION_NOTES.md.
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { ApiService } from '../../../core/services/api.service';
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

// MIGRATION: SendPassword.GetUser() resolved a user by username, OR by a uniquely-matching email when the
// membership provider RequiresUniqueEmail. The SPA collapses that into ONE combined field and validates the value
// as an email ONLY when it looks like one (contains '@'); a plain username is accepted as-is. `required` is always
// enforced.
function usernameOrEmailFormat(
  control: AbstractControl,
): ValidationErrors | null {
  const value = typeof control.value === 'string' ? control.value.trim() : '';
  if (value.length === 0 || !value.includes('@')) {
    return null;
  }
  return Validators.email(control);
}

/** Typed reactive-form shape: a combined username/email plus an optional CAPTCHA verification code. */
interface ForgotPasswordForm {
  usernameOrEmail: FormControl<string>;
  verificationCode: FormControl<string>;
}

/** Request body POSTed to the (not-yet-implemented) backend password-reminder endpoint. */
interface ForgotPasswordRequest {
  usernameOrEmail: string;
  portalId: number;
  verificationCode?: string;
}

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    FormControlComponent,
    LoadingSpinnerComponent,
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  // MIGRATION: request issued via the generic ApiService (no AuthService.forgotPassword exists -- see endpoint gap
  // note above). DI via inject() (AAP Section 0.7.3), not constructor injection.
  private readonly api = inject(ApiService);
  private readonly fb = inject(NonNullableFormBuilder);

  // MIGRATION: signal-based view state replaces the Web Forms ViewState/postback lifecycle.
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly errorMessage = signal<string | null>(null);
  // Server-side RFC 7807 per-field errors (passed to <app-form-control [errors]>); empty until a validation 400.
  readonly fieldErrors = signal<Record<string, string[]>>({});

  readonly form: FormGroup<ForgotPasswordForm> = this.fb.group({
    usernameOrEmail: this.fb.control('', {
      validators: [Validators.required, usernameOrEmailFormat],
    }),
    // MIGRATION: optional verification code mirrors the portal-configurable Security_CaptchaLogin setting
    // (UseCaptcha) in SendPassword.ascx.vb. CAPTCHA presence was portal-configurable; the field is always present
    // here but NEVER required, and is only sent when non-empty. (The legacy RequiresQuestionAndAnswer txtAnswer
    // path is a portal-config edge case and is intentionally omitted.)
    verificationCode: this.fb.control(''),
  });

  /** Submit handler: validate, POST the reminder request, then show a non-enumeration confirmation. */
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.fieldErrors.set({});

    const raw = this.form.getRawValue();
    const verificationCode = raw.verificationCode.trim();
    const payload: ForgotPasswordRequest = {
      usernameOrEmail: raw.usernameOrEmail.trim(),
      // MIGRATION: portalId hard-coded to the primary portal (0), matching the login flow's primary-portal note.
      portalId: 0,
    };
    if (verificationCode.length > 0) {
      payload.verificationCode = verificationCode;
    }

    this.api.post<void>('auth/forgot-password', payload).subscribe({
      next: () => {
        // MIGRATION: non-enumeration -- show the generic confirmation on success.
        this.submitted.set(true);
        this.submitting.set(false);
      },
      error: (error: HttpErrorResponse) => {
        if (this.isSurfaceableError(error.status)) {
          // Transport/validation/rate-limit failure: surface a real error WITHOUT revealing account existence.
          const parsed = parseProblemDetails(error.error);
          this.fieldErrors.set(parsed.fieldErrors);
          this.errorMessage.set(
            parsed.messages.at(0) ??
              'We could not process your request. Please try again.',
          );
        } else {
          // MIGRATION: non-enumeration -- any account-related ("benign") error (e.g. 404 not found) shows the SAME
          // generic confirmation as success, so the response never reveals whether the account existed.
          this.submitted.set(true);
        }
        this.submitting.set(false);
      },
    });
  }

  /**
   * Errors that may be surfaced to the user without enabling account enumeration:
   * transport/network (0), validation (400/422), and rate-limit (429). Everything else is treated as benign and
   * collapses into the generic confirmation.
   */
  private isSurfaceableError(status: number): boolean {
    return status === 0 || status === 400 || status === 422 || status === 429;
  }
}
