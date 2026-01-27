/**
 * @fileoverview Forgot Password Component for Angular 19 SPA
 * @description Angular 19 standalone forgot password component for password recovery workflow.
 * 
 * MIGRATION NOTES:
 * - Replaces DNN's SendPassword.ascx functionality from Website/admin/Security/SendPassword.ascx.vb
 * - Original functionality included:
 *   - Username/email-based password retrieval
 *   - Security question verification (if configured)
 *   - CAPTCHA validation for security
 *   - Email-based password reset functionality
 * 
 * @module features/auth/forgot-password/forgot-password.component
 */

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormControl,
  FormGroup,
  Validators
} from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { finalize } from 'rxjs';

import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Typed form interface for forgot password form controls.
 */
interface ForgotPasswordFormControls {
  email: FormControl<string>;
}

/**
 * ForgotPasswordComponent - Password recovery workflow.
 * 
 * Provides form for users to request password reset by email.
 * Sends password reset link to user's registered email address.
 * 
 * Uses Angular 19 patterns:
 * - Standalone component with explicit imports
 * - inject() for dependency injection
 * - Signals for reactive state management
 * - @if control flow syntax for conditional rendering
 * 
 * @class ForgotPasswordComponent
 */
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="forgot-password-container">
      <div class="forgot-password-card">
        <h1 class="card-title">Forgot Password</h1>
        <p class="card-subtitle">
          Enter your email address and we'll send you a link to reset your password.
        </p>

        @if (loading()) {
          <div class="loading-overlay">
            <app-loading-spinner size="large" message="Sending reset link..." />
          </div>
        }

        @if (submitted()) {
          <div class="success-message" role="alert">
            <span class="success-icon">✅</span>
            <div class="success-content">
              <strong>Reset link sent!</strong>
              <p>If an account exists for {{ submittedEmail() }}, you will receive a password reset link shortly.</p>
            </div>
          </div>
          <div class="card-footer">
            <a [routerLink]="['/auth/login']" class="back-to-login-link">
              ← Back to Login
            </a>
          </div>
        } @else {
          @if (errorMessage()) {
            <div class="error-message" role="alert">
              <span class="error-icon">⚠️</span>
              {{ errorMessage() }}
            </div>
          }

          <form [formGroup]="forgotPasswordForm" (ngSubmit)="onSubmit()" class="forgot-password-form">
            <div class="form-group">
              <label for="email" class="form-label">Email Address</label>
              <input
                id="email"
                type="email"
                formControlName="email"
                class="form-input"
                [class.input-error]="emailControl.invalid && emailControl.touched"
                placeholder="Enter your email address"
                autocomplete="email"
              />
              @if (emailControl.invalid && emailControl.touched) {
                @if (emailControl.hasError('required')) {
                  <span class="field-error">Email is required</span>
                } @else if (emailControl.hasError('email')) {
                  <span class="field-error">Please enter a valid email address</span>
                }
              }
            </div>

            <button
              type="submit"
              class="submit-button"
              [disabled]="forgotPasswordForm.invalid || loading()"
            >
              {{ loading() ? 'Sending...' : 'Send Reset Link' }}
            </button>
          </form>

          <div class="card-footer">
            <a [routerLink]="['/auth/login']" class="back-to-login-link">
              ← Back to Login
            </a>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .forgot-password-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      padding: 24px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .forgot-password-card {
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

    .card-title {
      margin: 0 0 8px 0;
      font-size: 28px;
      font-weight: 700;
      color: #1a1a2e;
      text-align: center;
    }

    .card-subtitle {
      margin: 0 0 24px 0;
      font-size: 14px;
      color: #6b7280;
      text-align: center;
      line-height: 1.5;
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

    .success-message {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 16px;
      margin-bottom: 20px;
      background-color: #f0fdf4;
      border: 1px solid #86efac;
      border-radius: 8px;
      color: #166534;
    }

    .success-icon {
      flex-shrink: 0;
      font-size: 20px;
    }

    .success-content {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .success-content strong {
      font-size: 16px;
    }

    .success-content p {
      margin: 0;
      font-size: 14px;
      line-height: 1.5;
    }

    .error-icon {
      flex-shrink: 0;
    }

    .forgot-password-form {
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

    .field-error {
      font-size: 12px;
      color: #dc2626;
    }

    .submit-button {
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

    .submit-button:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }

    .submit-button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .card-footer {
      margin-top: 24px;
      text-align: center;
    }

    .back-to-login-link {
      font-size: 14px;
      color: #667eea;
      text-decoration: none;
      transition: color 0.2s;
    }

    .back-to-login-link:hover {
      color: #764ba2;
      text-decoration: underline;
    }
  `]
})
export class ForgotPasswordComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  /** Loading state signal during API call */
  readonly loading = signal(false);

  /** Error message signal for failed requests */
  readonly errorMessage = signal('');

  /** Flag indicating form was successfully submitted */
  readonly submitted = signal(false);

  /** Stores the submitted email for display in success message */
  readonly submittedEmail = signal('');

  /** Reactive forgot password form with typed controls */
  readonly forgotPasswordForm: FormGroup<ForgotPasswordFormControls> = this.fb.group({
    email: this.fb.nonNullable.control('', [Validators.required, Validators.email])
  });

  /**
   * Getter for email form control.
   */
  get emailControl(): FormControl<string> {
    return this.forgotPasswordForm.controls.email;
  }

  /**
   * Handles form submission for password reset request.
   * 
   * MIGRATION: Replaces cmdResetPassword_Click from SendPassword.ascx.vb
   */
  onSubmit(): void {
    if (this.forgotPasswordForm.invalid) {
      this.forgotPasswordForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    const email = this.forgotPasswordForm.value.email!;
    this.submittedEmail.set(email);

    // Call password reset API endpoint
    this.http.post('/api/auth/forgot-password', { email })
      .pipe(
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: () => {
          this.submitted.set(true);
        },
        error: () => {
          // For security, still show success message even on error
          // This prevents email enumeration attacks
          this.submitted.set(true);
        }
      });
  }
}
