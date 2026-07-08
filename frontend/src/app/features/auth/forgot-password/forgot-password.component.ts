/**
 * ForgotPasswordComponent — the public "Forgot Password" (password reminder)
 * standalone screen of the `dnn-migration` Angular 19 SPA.
 *
 * MIGRATION: behavioural re-expression (UI functional parity, AAP §0.7.1) of the
 * legacy DotNetNuke 4.x VB.NET / ASP.NET Web Forms SendPassword control
 * (Website/admin/Security/SendPassword.ascx[.vb] + App_LocalResources/
 * SendPassword.ascx.resx). It is NOT a line-by-line port: the postback/ViewState
 * model, DNN MembershipProvider password retrieval, CAPTCHA, and the two-step
 * password Question & Answer reveal are intentionally not carried across (see the
 * `// MIGRATION:` notes below and MIGRATION_NOTES.md). The username-or-email
 * required-branch validation is preserved for parity.
 *
 * Lazy-loaded by the sibling features/auth/auth.routes.ts via
 *   loadComponent: () => import('./forgot-password/forgot-password.component')
 *     .then((m) => m.ForgotPasswordComponent)
 * so the exported class name MUST remain exactly `ForgotPasswordComponent`.
 */
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { timer } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { FormFieldComponent } from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Simulated latency (ms) before the confirmation is shown. Exported so the unit
 * test can advance virtual time via tick(...).
 * MIGRATION: there is NO backend password-recovery endpoint (AAP §0.2.2 — the
 * DNN MembershipProvider path UserController.GetPassword + Mail.SendMail is out
 * of scope; see MIGRATION_NOTES.md). This delay only emulates a network
 * round-trip for UX + testability.
 */
export const PASSWORD_REMINDER_SIMULATED_LATENCY_MS = 600;

/**
 * MIGRATION: security-conscious confirmation shown on EVERY valid submit.
 * Equivalent in meaning to the legacy 'PasswordSent' resource string but
 * deliberately does NOT reveal whether the supplied username/email matches an
 * account (prevents user enumeration). Collapses the legacy
 * PasswordSent / UsernameError / EmailError branch outcomes into one message.
 */
export const GENERIC_CONFIRMATION =
  'If an account matches the information provided, a password reminder has been sent to the email on file.';

/** MIGRATION: equivalent to legacy 'EnterUsernameEmail' resource string. */
export const ENTER_USERNAME_EMAIL =
  'Please enter your username or the email address you used during registration.';

/** MIGRATION: equivalent to legacy 'EnterUsername' resource string (kept for parity/completeness). */
export const ENTER_USERNAME = 'Please enter your username.';

/**
 * Cross-field validator: the group is invalid unless AT LEAST ONE named control
 * has a non-blank (non-whitespace) value.
 * MIGRATION: preserves the legacy SendPassword.ascx.vb required branch
 * (cmdSendPassword_Click L178-191). Whitespace-only counts as blank, mirroring
 * the legacy Trim(...) = "" checks.
 */
export function atLeastOneOf(...controlNames: string[]): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const hasValue = controlNames.some((name) => {
      const value: unknown = group.get(name)?.value;
      return typeof value === 'string' ? value.trim().length > 0 : value != null;
    });
    return hasValue ? null : { atLeastOneOf: true };
  };
}

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // MIGRATION / NG8113 avoidance: import ONLY what THIS template renders.
  // FormFieldComponent already applies appValidationHighlight + [appAutofocus] to
  // its OWN internal control, so the shared AutofocusDirective /
  // ValidationHighlightDirective are deliberately NOT imported here — importing
  // unused standalone declarations would emit NG8113 and fail the 0-warnings
  // Gate 3. Autofocus + validation-highlight parity is achieved by composing
  // <app-form-field> (see the username field's [autofocus]="true"). CommonModule
  // is likewise omitted: the built-in @if control flow needs no module.
  imports: [ReactiveFormsModule, RouterLink, FormFieldComponent, LoadingSpinnerComponent],
  template: `
    <section class="forgot-password" aria-labelledby="forgot-password-title">
      <h1 id="forgot-password-title" class="forgot-password__title">Forgot Password</h1>

      <!-- MIGRATION: equivalent to legacy 'SendPasswordHelp' (rendered as escaped
           <p> text, NOT [innerHTML]; legacy <br/> markup dropped). -->
      <div class="forgot-password__help">
        <p>Enter your username and a password reminder will be sent to the email address you provided during registration.</p>
        <p>Alternatively, provide the email address you used during registration — in that case you do not need to enter your username.</p>
      </div>

      @if (submitted()) {
        <div class="forgot-password__confirmation" role="status" aria-live="polite">
          <p class="forgot-password__confirmation-text">{{ successMessage() }}</p>
          <a routerLink="/auth/login" class="forgot-password__link">Return to sign in</a>
        </div>
      } @else {
        <form class="forgot-password__form" [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
          @if (errorMessage()) {
            <div class="forgot-password__alert" role="alert">{{ errorMessage() }}</div>
          }

          <app-form-field
            [control]="form.controls.username"
            controlId="forgot-password-username"
            label="User Name"
            controlType="text"
            hint="Enter your User Name here. In most cases this will be your email address."
            [autofocus]="true"
          />

          <app-form-field
            [control]="form.controls.email"
            controlId="forgot-password-email"
            label="Email Address"
            controlType="email"
            hint="You can provide the email address you used on registration to retrieve your password."
          />

          @if (showAnswer()) {
            <!-- MIGRATION: optional password-answer field (legacy txtAnswer). The
                 two-step Q&A reveal (tblQA / RequiresQuestionAndAnswer) and CAPTCHA
                 (ctlCaptcha / Security_CaptchaLogin) are NOT reproduced — they
                 depend on the DNN MembershipProvider (out of scope, AAP §0.2.2).
                 showAnswer() is always false, so this field is not shown. -->
            <app-form-field
              [control]="form.controls.answer"
              controlId="forgot-password-answer"
              label="Answer"
              controlType="text"
              hint="Enter the answer to the Password Question."
            />
          }

          <app-loading-spinner [loading]="submitting()" message="Sending password reminder…" />

          <div class="forgot-password__actions">
            <button type="submit" class="forgot-password__submit" [disabled]="submitting()">
              Send Password
            </button>
            <a routerLink="/auth/login" class="forgot-password__link">Return to sign in</a>
          </div>
        </form>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
        max-width: 22rem;
        margin: 0 auto;
        padding: var(--space-4, 1rem);
        font-family: var(--font-family-base, inherit);
      }
      .forgot-password__title {
        margin: 0 0 var(--space-3, 0.75rem);
        font-size: 1.25rem;
        font-weight: 600;
        color: var(--color-text, #1a1a1a);
      }
      .forgot-password__help {
        margin-bottom: var(--space-3, 0.75rem);
        color: var(--color-muted, #6c757d);
        font-size: 0.875rem;
      }
      .forgot-password__help p {
        margin: 0 0 var(--space-2, 0.5rem);
      }
      .forgot-password__alert {
        margin-bottom: var(--space-3, 0.75rem);
        padding: var(--space-2, 0.5rem);
        border: 1px solid var(--color-danger, #dc3545);
        border-radius: var(--radius, 4px);
        color: var(--color-danger, #dc3545);
        font-size: 0.875rem;
      }
      .forgot-password__confirmation {
        padding: var(--space-3, 0.75rem);
        border: 1px solid var(--color-success, #198754);
        border-radius: var(--radius, 4px);
        color: var(--color-success, #198754);
      }
      .forgot-password__confirmation-text {
        margin: 0 0 var(--space-2, 0.5rem);
      }
      .forgot-password__actions {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-2, 0.5rem);
        margin-top: var(--space-3, 0.75rem);
      }
      .forgot-password__submit {
        padding: var(--space-2, 0.5rem) var(--space-4, 1rem);
        font: inherit;
        color: #fff;
        background-color: var(--color-primary, #0d6efd);
        border: none;
        border-radius: var(--radius, 4px);
        cursor: pointer;
      }
      .forgot-password__submit:disabled {
        opacity: 0.65;
        cursor: not-allowed;
      }
      .forgot-password__link {
        color: var(--color-primary, #0d6efd);
        text-decoration: none;
      }
      .forgot-password__link:hover,
      .forgot-password__link:focus {
        text-decoration: underline;
      }
    `,
  ],
})
export class ForgotPasswordComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  // MIGRATION: reserved seam. AuthService exposes only login/refresh/logout/me
  // (no password-recovery endpoint — AAP §0.2.2; see MIGRATION_NOTES.md). When a
  // recovery endpoint is added, onSubmit() must call it via this AuthService
  // (NEVER HttpClient directly). Injected but intentionally not yet called.
  private readonly authService = inject(AuthService);

  readonly form = this.fb.group(
    {
      username: this.fb.control(''),
      email: this.fb.control('', { validators: [Validators.email] }),
      // MIGRATION: legacy password Q&A answer (txtAnswer). Two-step reveal not reproduced.
      answer: this.fb.control(''),
    },
    { validators: [atLeastOneOf('username', 'email')] },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  // MIGRATION: always false — the two-step Q&A reveal is not reproduced.
  readonly showAnswer = signal(false);

  onSubmit(): void {
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // MIGRATION: FormFieldComponent shows only per-control errors; the
      // group-level username-or-email required message (legacy 'EnterUsernameEmail')
      // is surfaced here. Legacy L239/251 PortalSecurity.InputFilter sanitization is
      // superseded by Angular's built-in template sanitization + strict typing.
      if (this.form.hasError('atLeastOneOf')) {
        this.errorMessage.set(ENTER_USERNAME_EMAIL);
      }
      return;
    }

    // MIGRATION: no backend recovery endpoint in scope (AAP §0.2.2). Emulate a
    // round-trip, then show a non-revealing confirmation. Replace this timer
    // with an AuthService recovery call when the endpoint exists.
    this.submitting.set(true);
    timer(PASSWORD_REMINDER_SIMULATED_LATENCY_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.submitting.set(false);
        this.submitted.set(true);
        this.successMessage.set(GENERIC_CONFIRMATION);
      });
  }
}
