import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { AuthResponse, LoginRequest } from '../../../core/models';
import { FormFieldComponent } from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * LoginComponent — the SPA sign-in screen (PUBLIC route /auth/login).
 *
 * MIGRATION: replaces the legacy DotNetNuke Web Forms login entry shell
 * (Website/Default.aspx.vb, which implemented IClientAPICallbackEventHandler) and
 * the admin login control (Website/admin/Authentication/Login.ascx.vb whose
 * UserController.ValidateUser(PortalId, txtUsername, txtPassword, ...) call is this
 * form's lineage). ViewState / postback / IClientAPICallbackEventHandler are
 * ELIMINATED (AAP §0.6.3): a stateless typed reactive form drives
 * AuthService.login(); Page_Load -> ngOnInit; the postback login button ->
 * (ngSubmit). All API access is delegated to AuthService (AAP §0.7.1 — this
 * component holds NO business logic and NEVER calls HttpClient directly).
 * NOTE: AutofocusDirective/ValidationHighlightDirective are NOT imported here —
 * FormFieldComponent applies them internally; importing them unused would trip
 * NG8113 and fail the 0-warnings Gate 3.
 */
@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, FormFieldComponent, LoadingSpinnerComponent],
  template: `
    <div class="login">
      <section class="login__card" aria-labelledby="login-title">
        <h1 id="login-title" class="login__title">Sign In</h1>

        <!-- MIGRATION: DNN AddModuleMessage login-failure feedback -> inline alert. -->
        @if (errorMessage()) {
          <div class="login__error" role="alert" aria-live="assertive">
            {{ errorMessage() }}
          </div>
        }

        <form class="login__form" [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
          <app-form-field
            [control]="form.controls.username"
            label="User Name"
            controlId="login-username"
            [required]="true"
            [autofocus]="true"
            [errorMessages]="usernameErrorMessages"
          />

          <app-form-field
            [control]="form.controls.password"
            label="Password"
            controlId="login-password"
            controlType="password"
            [required]="true"
            [errorMessages]="passwordErrorMessages"
          />

          <div class="login__actions">
            <button type="submit" class="login__submit" [disabled]="submitting()">
              Sign In
            </button>
          </div>

          <div class="login__links">
            <!-- MIGRATION: legacy SendPassword.ascx "Forgot Password" reminder link. -->
            <a class="login__forgot" routerLink="/auth/forgot-password">
              Forgot your password?
            </a>
          </div>
        </form>

        <app-loading-spinner
          [loading]="submitting()"
          [overlay]="true"
          message="Signing in…"
        />
      </section>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .login {
        display: flex;
        justify-content: center;
        align-items: flex-start;
        padding: var(--space-6, 2rem) var(--space-3, 0.75rem);
      }

      .login__card {
        position: relative; /* anchor for the overlay spinner */
        width: 100%;
        max-width: 24rem;
        padding: var(--space-5, 1.5rem);
        background: var(--color-surface, #ffffff);
        border: 1px solid var(--color-border, #e2e8f0);
        border-radius: var(--radius, 8px);
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      }

      .login__title {
        margin: 0 0 var(--space-4, 1rem);
        font-size: 1.5rem;
        color: var(--color-text, #1a1a1a);
      }

      .login__error {
        margin-bottom: var(--space-3, 0.75rem);
        padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        color: var(--color-danger-contrast, #842029);
        background: var(--color-danger-bg, #f8d7da);
        border: 1px solid var(--color-danger, #dc3545);
        border-radius: var(--radius, 4px);
        font-size: 0.875rem;
      }

      .login__actions {
        margin-top: var(--space-3, 0.75rem);
      }

      .login__submit {
        width: 100%;
        padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        font: inherit;
        font-weight: 600;
        color: var(--color-primary-contrast, #ffffff);
        background: var(--color-primary, #0d6efd);
        border: 1px solid var(--color-primary, #0d6efd);
        border-radius: var(--radius, 4px);
        cursor: pointer;
      }

      .login__submit:disabled {
        opacity: 0.65;
        cursor: not-allowed;
      }

      .login__links {
        margin-top: var(--space-3, 0.75rem);
        text-align: center;
      }

      .login__forgot {
        color: var(--color-primary, #0d6efd);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  // MIGRATION: legacy RequiredFieldValidator on txtUsername/txtPassword ->
  // Validators.required (UI functional parity, AAP §0.7.1). portalId mirrors the
  // legacy PortalId login context (optional / nullable).
  readonly form = this.fb.group({
    username: this.fb.control('', { validators: [Validators.required] }),
    password: this.fb.control('', { validators: [Validators.required] }),
    portalId: this.fb.control<number | null>(null),
  });

  // Stable references so the OnPush signal-input diff on <app-form-field> does not
  // receive a new object literal every change-detection cycle.
  readonly usernameErrorMessages: Record<string, string> = {
    required: 'User Name is required.',
  };
  readonly passwordErrorMessages: Record<string, string> = {
    required: 'Password is required.',
  };

  // View state via Signals (AAP §0.3.4) — no manual change detection.
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Default post-login destination; overridden by the authGuard's returnUrl.
  private returnUrl = '/portals';

  ngOnInit(): void {
    // MIGRATION: DNN postback redirect (Redirect_AfterLogin / returnurl query) ->
    // SPA route navigation. authGuard attaches ?returnUrl=<attempted-url> on a 401.
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/portals';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request: LoginRequest = {
      username: raw.username,
      password: raw.password,
    };
    // The model marks portalId optional; omit it when not supplied.
    if (raw.portalId !== null) {
      request.portalId = raw.portalId;
    }

    // NOTE: AuthService.login() already loads the current user (GET /api/auth/me)
    // internally — do NOT call loadCurrentUser here.
    this.authService
      .login(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (_response: AuthResponse) => {
          void this.router.navigateByUrl(this.returnUrl);
        },
        error: () => {
          this.submitting.set(false);
          this.errorMessage.set(
            'Sign in failed. Please check your user name and password and try again.',
          );
        },
      });
  }
}
