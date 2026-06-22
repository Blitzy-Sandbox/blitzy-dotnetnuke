import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import {
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { LoginRequest } from '../../../core/models/auth.model';
import { type ProblemDetails, summarizeProblem } from '../../../core/services/api.service';
import { FormControlsComponent } from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner';

/**
 * LoginComponent — public JWT login page.
 *
 * MIGRATION: This is the NEW entry point to the JWT Bearer authentication flow that
 * REPLACES the legacy ASP.NET Forms Authentication + DES model in
 * Library/Components/Security/PortalSecurity.vb (FormsAuthentication.SignOut L79;
 * DES Encrypt/Decrypt L138-211). JWT + BCrypt is the single sanctioned behavior change
 * of the migration (AAP §0.6.2); recorded in the root MIGRATION_NOTES.md. There is no
 * legacy .ascx login control — this UI is brand-new. No client-side crypto is performed:
 * credentials are POSTed to /api/auth/login via AuthService and the server hashes with BCrypt.
 */
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  imports: [ReactiveFormsModule, FormControlsComponent, LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Typed reactive login form; both fields required. */
  readonly form = this.formBuilder.group({
    username: this.formBuilder.control('', { validators: [Validators.required] }),
    password: this.formBuilder.control('', { validators: [Validators.required] }),
  });

  /** True while the login request is in flight (disables the submit button). */
  readonly submitting = signal(false);

  /** Top-level error message surfaced from the API ProblemDetails. */
  readonly errorMessage = signal<string | null>(null);

  /** Field-level server validation errors (RFC 7807 ProblemDetails.errors). */
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  /** Per-validator message overrides forwarded to the shared form-controls wrapper. */
  readonly usernameMessages: Record<string, string> = { required: 'Username is required.' };
  readonly passwordMessages: Record<string, string> = { required: 'Password is required.' };

  constructor() {
    // If a valid session already exists, skip the login page.
    if (this.authService.isAuthenticated()) {
      void this.router.navigateByUrl(this.resolveReturnUrl());
    }
  }

  /** Submit handler: validates, calls AuthService.login, then redirects or surfaces errors. */
  onSubmit(): void {
    this.errorMessage.set(null);
    this.serverErrors.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const credentials: LoginRequest = {
      username: this.form.controls.username.value.trim(),
      password: this.form.controls.password.value,
    };

    this.submitting.set(true);
    this.authService.login(credentials).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.resolveReturnUrl());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        const problem = error as ProblemDetails | null;
        this.errorMessage.set(
          summarizeProblem(problem, 'Invalid username or password.'),
        );
        this.serverErrors.set(problem?.errors ?? null);
      },
    });
  }

  /** Resolve the post-login redirect target from the returnUrl query param, defaulting to /portals. */
  private resolveReturnUrl(): string {
    return this.route.snapshot.queryParamMap.get('returnUrl') ?? '/portals';
  }
}
