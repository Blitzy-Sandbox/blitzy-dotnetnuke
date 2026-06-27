// MIGRATION: Re-expresses the legacy DotNetNuke Forms-auth login screen
// (Website/admin/Authentication/Login.ascx.vb; sign-in orchestration formerly in
// Library/Components/Security/PortalSecurity.vb and Library/Components/Users/Membership/UserMembership.vb,
// now BACKEND-owned by AuthService/JwtService) as a standalone Angular 19 component. The Web Forms
// postback/ViewState lifecycle, the dynamic per-provider login-control loading (BindLogin/
// DisplayLoginControl), and the server-side UserAuthenticated -> ValidateUser pipeline are discarded;
// this component renders a typed reactive login form and delegates authentication to the in-memory,
// signal-based AuthService.
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type FormControl,
  type FormGroup,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';
import type { HttpErrorResponse } from '@angular/common/http';
import type { LoginRequest, ProblemDetails } from '../../../core/models';

/** Strongly-typed reactive form model for the sign-in screen. */
interface LoginForm {
  username: FormControl<string>;
  password: FormControl<string>;
  rememberMe: FormControl<boolean>;
}

/** Fallback banner text when the API failure carries no usable ProblemDetails message. */
const GENERIC_LOGIN_ERROR =
  'Invalid login attempt. Please verify your username and password and try again.';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    FormControlComponent,
    LoadingSpinnerComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  // MIGRATION: legacy RedirectURL was computed server-side from the "returnurl"/"appctx" query string.
  // withComponentInputBinding() (app.config.ts) binds the ?returnUrl query param to this input; the
  // authGuard supplies it via createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } }).
  readonly returnUrl = input<string>();

  /** True while a login request is in flight (drives the spinner + disabled submit). */
  readonly submitting = signal(false);

  /** Top-level (form-level) error banner text, or null when there is no error. */
  readonly errorMessage = signal<string | null>(null);

  /** Per-field RFC 7807 validation errors fed to each <app-form-control [errors]>. */
  readonly fieldErrors = signal<ProblemDetails['errors'] | undefined>(undefined);

  // MIGRATION: legacy fields txtUsername -> username, txtPassword -> password,
  // chkCookie "Remember Login" (CreatePersistentCookie) -> rememberMe. The legacy optional CAPTCHA
  // (UseCaptcha = GetSetting(PortalId, "Security_CaptchaLogin")) was portal-configurable and is
  // intentionally omitted until a portal-configuration/CAPTCHA mechanism exists in the SPA.
  readonly form: FormGroup<LoginForm> = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
    rememberMe: [false],
  });

  constructor() {
    // Accessibility: focus the username field once the view has rendered (browser-only).
    afterNextRender(() => {
      this.host.nativeElement
        .querySelector<HTMLInputElement>('#username')
        ?.focus();
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusFirstInvalidField();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.fieldErrors.set(undefined);

    const { username, password, rememberMe } = this.form.getRawValue();

    // MIGRATION: PortalId is REQUIRED by the multi-tenant LoginRequest contract. The legacy app derived
    // it from PortalSettings/host mapping; the SPA targets the primary portal (0) until a portal
    // selector exists.
    const request: LoginRequest = {
      username,
      password,
      portalId: 0,
      rememberMe,
    };

    this.authService
      .login(request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => this.onLoginSuccess(),
        error: (error: HttpErrorResponse) => this.handleLoginError(error),
      });
  }

  private onLoginSuccess(): void {
    void this.router.navigateByUrl(this.resolveReturnUrl());
  }

  private handleLoginError(error: HttpErrorResponse): void {
    // MIGRATION: login failures are HTTP 400 (RFC 7807) and rate-limited responses are 429 — neither is
    // the 401 that errorInterceptor handles, so they reach this callback for display.
    const body: unknown = error.error;
    const parsed = parseProblemDetails(body);
    this.fieldErrors.set(parsed.fieldErrors);
    this.errorMessage.set(
      parsed.messages.length > 0 ? parsed.messages.join(' ') : GENERIC_LOGIN_ERROR,
    );
  }

  /** Resolve a safe post-login redirect target, guarding against open redirects. */
  private resolveReturnUrl(): string {
    const candidate =
      this.returnUrl() ?? this.route.snapshot.queryParamMap.get('returnUrl');
    if (candidate !== null && this.isSafeInternalUrl(candidate)) {
      return candidate;
    }
    return '/portals';
  }

  // MIGRATION: legacy Login.ascx.vb rejected returnurl values containing "://" (open-redirect/CSRF guard).
  // Stricter here: accept only app-internal absolute paths that start with a single '/'.
  private isSafeInternalUrl(url: string): boolean {
    return /^\/(?![/\\])/.test(url);
  }

  // MIGRATION: [QA F4-003] focus AND scroll the first invalid control into view via the shared helper.
  // Previously this only called .focus() (no scrollIntoView); on a scrolled viewport the focused control
  // could remain off-screen, giving no visible feedback. The shared helper is reused by every form.
  private focusFirstInvalidField(): void {
    focusFirstInvalidControl(this.host.nativeElement);
  }
}
