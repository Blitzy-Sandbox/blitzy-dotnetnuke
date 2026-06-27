// MIGRATION: Website/admin/Security/SendPassword.ascx.vb (DotNetNuke.Modules.Admin.Security.SendPassword :
// UserModuleBase, 278 lines) -> ForgotPasswordComponent. Re-expresses the legacy password-reminder screen's
// validation + request orchestration as a password-RESET flow, an Angular 19 standalone, PUBLIC (no-guard) screen
// mounted at /auth/forgot-password. The legacy Web Forms postback/ViewState/PortalModuleBase/.resx/
// Skin.AddModuleMessage machinery is DISCARDED. The legacy flow performed a plaintext password REMINDER
// (UserController.GetPassword + Mail.SendMail PasswordReminder + EventLog PASSWORD_SENT_*); under BCrypt one-way
// hashing a plaintext reminder is impossible by design, so the migrated flow is a RESET: the server performs the
// portal-scoped user lookup and returns a single non-enumerating confirmation. This component only collects input,
// POSTs the request, and shows that confirmation.
//
// MIGRATION: BACKEND ENDPOINT -- AuthController now exposes POST /api/v1/auth/forgot-password ([AllowAnonymous],
// rate-limited under the "auth" policy). Per the migration's frontend discipline (AAP Section 0.7.3 -- a component
// talks to its FEATURE/AUTH service, never the generic ApiService directly), this component delegates to
// AuthService.requestPasswordReset(), which POSTs the frozen backend contract {portalId, usernameOrEmail} and maps
// the { data: { message } } success envelope to void. The server performs the portal-scoped user lookup and
// returns a single generic, non-enumerating confirmation. NOTE: actual e-mail dispatch is an AAP Section 0.6.2
// exclusion (Mail/Messaging subsystem), so no message is physically delivered in this phase; the endpoint
// validates input and returns the non-enumerating confirmation. Recorded in MIGRATION_NOTES.md.
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
  ElementRef,
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

import { AuthService } from '../../../core/auth/auth.service';
import type { PasswordResetRequest } from '../../../core/models/auth.model';
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
// MIGRATION: [QA F4-003] shared focus-first-invalid helper -- moves focus + scrolls to the first invalid
// control on a failed submit (this form already surfaces server errors via parseProblemDetails, so no F4-013).
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';

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
  // MIGRATION: the component talks ONLY to the auth FEATURE service (AAP Section 0.7.3), never the generic
  // ApiService/HttpClient directly. DI via inject() (Angular 19), not constructor injection.
  private readonly auth = inject(AuthService);
  private readonly fb = inject(NonNullableFormBuilder);
  // MIGRATION: [QA F4-003] host element used to locate the first invalid control on a failed submit.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

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

  /** Submit handler: validate, POST the reset request, then show a non-enumeration confirmation. */
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // MIGRATION: [QA F4-003] move focus + scroll to the first invalid control so an invalid submit gives
      // immediate, visible feedback (the shared <app-form-control> renders the per-field message, F4-002).
      focusFirstInvalidControl(this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.fieldErrors.set({});

    const raw = this.form.getRawValue();
    const verificationCode = raw.verificationCode.trim();
    const payload: PasswordResetRequest = {
      usernameOrEmail: raw.usernameOrEmail.trim(),
      // MIGRATION: portalId hard-coded to the primary portal (0), matching the login flow's primary-portal note.
      portalId: 0,
    };
    if (verificationCode.length > 0) {
      payload.verificationCode = verificationCode;
    }

    // MIGRATION: delegate to the auth feature service, which POSTs to /api/v1/auth/forgot-password. The error
    // branch enforces the non-enumeration policy + RFC 7807 handling described in the header.
    this.auth.requestPasswordReset(payload).subscribe({
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
