/**
 * @fileoverview Login Component for Angular 19 SPA
 * @description Angular 19 standalone login component implementing JWT-based authentication UI.
 * 
 * MIGRATION NOTES:
 * - Replaces DNN's server-side UserController.UserLogin (lines 991-1008)
 * - Replaces FormsAuthentication.SetAuthCookie (line 1033) with localStorage JWT token storage
 * - Replaces ViewState-backed form state with Angular reactive forms
 * - UserLoginStatus enum values (lines 23-31) used for error message mapping
 * 
 * @module features/auth/login/login.component
 */

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { LoginRequest, UserLoginStatus } from '../../../core/models/auth.model';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Typed form interface for login form controls.
 */
interface LoginFormControls {
  username: FormControl<string>;
  password: FormControl<string>;
  rememberMe: FormControl<boolean>;
}

/**
 * LoginComponent - Primary authentication entry point.
 * 
 * Implements login form with username/password fields using reactive forms,
 * form validation, error messaging for authentication failures, and loading
 * states during API calls.
 * 
 * Uses Angular 19 patterns:
 * - Standalone component with explicit imports
 * - inject() for dependency injection
 * - Signals for reactive state management
 * - @if control flow syntax for conditional rendering
 * 
 * @class LoginComponent
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-container">
      <div class="login-card">
        <h1 class="login-title">Sign In</h1>
        <p class="login-subtitle">Enter your credentials to access the portal</p>

        @if (loading()) {
          <div class="loading-overlay">
            <app-loading-spinner size="large" message="Signing in..." />
          </div>
        }

        @if (errorMessage()) {
          <div class="error-message" role="alert">
            <span class="error-icon">⚠️</span>
            {{ errorMessage() }}
          </div>
        }

        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="login-form">
          <div class="form-group">
            <label for="username" class="form-label">Username</label>
            <input
              id="username"
              type="text"
              formControlName="username"
              class="form-input"
              [class.input-error]="usernameControl.invalid && usernameControl.touched"
              placeholder="Enter your username"
              autocomplete="username"
            />
            @if (usernameControl.invalid && usernameControl.touched) {
              <span class="field-error">Username is required</span>
            }
          </div>

          <div class="form-group">
            <label for="password" class="form-label">Password</label>
            <div class="password-wrapper">
              <input
                id="password"
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                class="form-input"
                [class.input-error]="passwordControl.invalid && passwordControl.touched"
                placeholder="Enter your password"
                autocomplete="current-password"
              />
              <button
                type="button"
                class="password-toggle"
                (click)="togglePasswordVisibility()"
                [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
              >
                {{ showPassword() ? '👁️‍🗨️' : '👁️' }}
              </button>
            </div>
            @if (passwordControl.invalid && passwordControl.touched) {
              <span class="field-error">Password is required</span>
            }
          </div>

          <div class="form-group checkbox-group">
            <label class="checkbox-label">
              <input
                type="checkbox"
                formControlName="rememberMe"
                class="checkbox-input"
              />
              <span class="checkbox-text">Remember me</span>
            </label>
          </div>

          <button
            type="submit"
            class="login-button"
            [disabled]="loginForm.invalid || loading()"
          >
            {{ loading() ? 'Signing in...' : 'Sign In' }}
          </button>
        </form>

        <div class="login-footer">
          <a [routerLink]="['/auth/forgot-password']" class="forgot-password-link">
            Forgot your password?
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      padding: 24px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .login-card {
      position: relative;
      width: 100%;
      max-width: 400px;
      padding: 40px;
      background-color: #ffffff;
      border-radius: 12px;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
    }

    .loading-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background-color: rgba(255, 255, 255, 0.9);
      border-radius: 12px;
      z-index: 10;
    }

    .login-title {
      margin: 0 0 8px 0;
      font-size: 28px;
      font-weight: 700;
      color: #1a1a2e;
      text-align: center;
    }

    .login-subtitle {
      margin: 0 0 24px 0;
      font-size: 14px;
      color: #6b7280;
      text-align: center;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      margin-bottom: 20px;
      background-color: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: 8px;
      color: #dc2626;
      font-size: 14px;
    }

    .error-icon {
      flex-shrink: 0;
    }

    .login-form {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .form-label {
      font-size: 14px;
      font-weight: 500;
      color: #374151;
    }

    .form-input {
      width: 100%;
      padding: 12px 16px;
      font-size: 16px;
      border: 1px solid #d1d5db;
      border-radius: 8px;
      outline: none;
      transition: border-color 0.2s, box-shadow 0.2s;
      box-sizing: border-box;
    }

    .form-input:focus {
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.2);
    }

    .form-input.input-error {
      border-color: #dc2626;
    }

    .password-wrapper {
      position: relative;
      display: flex;
      align-items: center;
    }

    .password-wrapper .form-input {
      padding-right: 48px;
    }

    .password-toggle {
      position: absolute;
      right: 12px;
      background: none;
      border: none;
      cursor: pointer;
      font-size: 18px;
      padding: 4px;
      opacity: 0.6;
      transition: opacity 0.2s;
    }

    .password-toggle:hover {
      opacity: 1;
    }

    .field-error {
      font-size: 12px;
      color: #dc2626;
    }

    .checkbox-group {
      flex-direction: row;
    }

    .checkbox-label {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
    }

    .checkbox-input {
      width: 18px;
      height: 18px;
      cursor: pointer;
    }

    .checkbox-text {
      font-size: 14px;
      color: #4b5563;
    }

    .login-button {
      width: 100%;
      padding: 14px 24px;
      font-size: 16px;
      font-weight: 600;
      color: #ffffff;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border: none;
      border-radius: 8px;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .login-button:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }

    .login-button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .login-footer {
      margin-top: 24px;
      text-align: center;
    }

    .forgot-password-link {
      font-size: 14px;
      color: #667eea;
      text-decoration: none;
      transition: color 0.2s;
    }

    .forgot-password-link:hover {
      color: #764ba2;
      text-decoration: underline;
    }
  `]
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  /** Loading state signal during API call */
  readonly loading = signal(false);

  /** Error message signal for authentication failures */
  readonly errorMessage = signal('');

  /** Toggle for password visibility */
  readonly showPassword = signal(false);

  /** Return URL after successful login */
  private returnUrl = '/';

  /** Reactive login form with typed controls */
  readonly loginForm: FormGroup<LoginFormControls> = this.fb.group({
    username: this.fb.nonNullable.control('', [Validators.required]),
    password: this.fb.nonNullable.control('', [Validators.required]),
    rememberMe: this.fb.nonNullable.control(false)
  });

  /**
   * Getter for username form control.
   */
  get usernameControl(): FormControl<string> {
    return this.loginForm.controls.username;
  }

  /**
   * Getter for password form control.
   */
  get passwordControl(): FormControl<string> {
    return this.loginForm.controls.password;
  }

  /**
   * Initializes component, captures return URL from query params.
   */
  ngOnInit(): void {
    // Get return URL from route parameters or default to '/'
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/';

    // Redirect if already authenticated
    if (this.authService.checkAuthenticated()) {
      this.router.navigate([this.returnUrl]);
    }
  }

  /**
   * Handles form submission for login.
   * 
   * MIGRATION: Replaces cmdLogin_Click from Login.ascx.vb
   */
  onSubmit(): void {
    if (this.loginForm.invalid) {
      // Mark all fields as touched to show validation errors
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    const credentials: LoginRequest = {
      username: this.loginForm.value.username!,
      password: this.loginForm.value.password!,
      rememberMe: this.loginForm.value.rememberMe
    };

    this.authService.login(credentials)
      .pipe(
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: () => {
          // Navigate to return URL after successful login
          this.router.navigate([this.returnUrl]);
        },
        error: (error) => {
          // Map error status to user-friendly message
          const status = error.error?.status as UserLoginStatus | undefined;
          this.errorMessage.set(this.getErrorMessage(status));
        }
      });
  }

  /**
   * Toggles password visibility.
   */
  togglePasswordVisibility(): void {
    this.showPassword.update(current => !current);
  }

  /**
   * Maps UserLoginStatus enum to user-friendly error messages.
   * 
   * MIGRATION: UserLoginStatus values from Library/Components/Users/Membership/UserLoginStatus.vb
   * 
   * @param status UserLoginStatus code from API response
   * @returns User-friendly error message
   */
  private getErrorMessage(status?: UserLoginStatus): string {
    switch (status) {
      case UserLoginStatus.LOGIN_USERLOCKEDOUT:
        return 'Your account has been locked. Please contact the administrator.';
      case UserLoginStatus.LOGIN_USERNOTAPPROVED:
        return 'Your account has not been approved yet.';
      case UserLoginStatus.LOGIN_INSECUREADMINPASSWORD:
      case UserLoginStatus.LOGIN_INSECUREHOSTPASSWORD:
        return 'Your password is insecure and must be changed.';
      case UserLoginStatus.LOGIN_FAILURE:
      default:
        return 'Invalid username or password. Please try again.';
    }
  }
}
