/**
 * @fileoverview Angular 19 Standalone User Profile Component
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET WebForms controls:
 * - Website/admin/Users/Membership.ascx.vb (lines 43-273) - Membership status management
 * - Website/admin/Users/Profile.ascx.vb (lines 45-238) - Profile editing
 *
 * This component provides profile editing and membership state management for users,
 * implementing administrative actions (authorize, unauthorize, unlock, force password change)
 * derived from the original VB.NET event handlers.
 *
 * Key transformations:
 * - VB.NET Partial Class Membership Inherits UserModuleBase → Angular standalone component
 * - VB.NET ViewState["UserId"] → ActivatedRoute params with signals
 * - VB.NET Property Membership() As UserMembership → signal<UserMembership | null>
 * - VB.NET cmdAuthorize_Click handler → onAuthorize() method
 * - VB.NET cmdUnAuthorize_Click handler → onUnauthorize() method
 * - VB.NET cmdUnLock_Click handler → onUnlock() method
 * - VB.NET cmdPassword_Click handler → onForcePasswordChange() method
 * - VB.NET ProfileProperties.DataSource binding → reactive form with typed controls
 * - VB.NET MembershipEditor.DataBind() → computed signals for membership display
 *
 * @module features/user/components/user-profile
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  inject,
  OnInit,
  DestroyRef,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { tap, catchError, switchMap, filter } from 'rxjs/operators';
import { of } from 'rxjs';

import { User, UserProfile, UserMembership } from '../../../../core/models/user.model';
import { UserService, UpdateUserRequest } from '../../services/user.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TextInputComponent, SelectOption } from '../../../../shared/components/form-controls/form-controls.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

/**
 * Interface for typed profile form controls
 * MIGRATION: Derived from ProfileProperties binding in Profile.ascx.vb (lines 162-172)
 */
interface ProfileFormControls {
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  email: FormControl<string>;
  street: FormControl<string>;
  city: FormControl<string>;
  region: FormControl<string>;
  country: FormControl<string>;
  postalCode: FormControl<string>;
  telephone: FormControl<string>;
  cell: FormControl<string>;
  website: FormControl<string>;
  timeZone: FormControl<number | null>;
  preferredLocale: FormControl<string>;
}

/**
 * UserProfileComponent
 *
 * Angular 19 standalone component for user profile and membership management.
 * Implements profile editing for user properties and membership state management
 * with administrative actions.
 *
 * MIGRATION: This component replaces the combined functionality of:
 * - Membership.ascx.vb (lines 43-44): Partial Class Membership Inherits UserModuleBase
 * - Profile.ascx.vb (lines 45-46): Partial Class Profile Inherits ProfileUserControlBase
 *
 * Features:
 * - Signal-based reactive state management
 * - inject() for dependency injection (Angular 19 pattern)
 * - OnPush change detection strategy for performance
 * - Reactive forms with typed FormGroup
 * - @if/@for control flow syntax in templates
 * - Integration with shared form-controls and confirmation-dialog components
 */
@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent,
    TextInputComponent,
    ConfirmationDialogComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Loading Spinner -->
    @if (loading()) {
      <div class="loading-container">
        <app-loading-spinner
          size="large"
          message="Loading user profile..." />
      </div>
    } @else if (error()) {
      <!-- Error Display -->
      <div class="error-container" role="alert">
        <div class="error-icon">
          <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
        </div>
        <h2 class="error-title">Error Loading Profile</h2>
        <p class="error-message">{{ error() }}</p>
        <button type="button" class="btn btn-secondary" (click)="onCancel()">
          Go Back
        </button>
      </div>
    } @else if (user()) {
      <div class="profile-container">
        <!-- Page Header -->
        <div class="profile-header">
          <h1 class="profile-title">
            @if (isCurrentUser()) {
              My Profile
            } @else {
              User Profile: {{ user()!.displayName }}
            }
          </h1>
          <div class="profile-meta">
            <span class="user-id">User ID: {{ user()!.userId }}</span>
            <span class="username">Username: {{ user()!.username }}</span>
          </div>
        </div>

        <!-- Membership Status Section -->
        <!-- MIGRATION: Derived from MembershipEditor.DataSource binding (Membership.ascx.vb line 147-148) -->
        @if (membership()) {
          <section class="card membership-section">
            <h2 class="card-title">
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              </svg>
              Membership Status
            </h2>
            <div class="card-body">
              <div class="status-grid">
                <!-- Approved Status -->
                <!-- MIGRATION: Replaces Membership.Approved display (Membership.ascx.vb line 143) -->
                <div class="status-item">
                  <span class="status-label">Approved</span>
                  <span [class]="membership()!.approved ? 'status-badge status-badge--success' : 'status-badge status-badge--warning'">
                    {{ membership()!.approved ? 'Yes' : 'No' }}
                  </span>
                </div>

                <!-- Locked Out Status -->
                <!-- MIGRATION: Replaces Membership.LockedOut display (Membership.ascx.vb line 141) -->
                <div class="status-item">
                  <span class="status-label">Locked Out</span>
                  <span [class]="membership()!.lockedOut ? 'status-badge status-badge--danger' : 'status-badge status-badge--success'">
                    {{ membership()!.lockedOut ? 'Yes' : 'No' }}
                  </span>
                </div>

                <!-- Online Status -->
                <div class="status-item">
                  <span class="status-label">Online</span>
                  <span [class]="membership()!.isOnline ? 'status-badge status-badge--success' : 'status-badge status-badge--neutral'">
                    {{ membership()!.isOnline ? 'Yes' : 'No' }}
                  </span>
                </div>

                <!-- Update Password Required -->
                <!-- MIGRATION: Replaces Membership.UpdatePassword display (Membership.ascx.vb line 144) -->
                <div class="status-item">
                  <span class="status-label">Password Update Required</span>
                  <span [class]="membership()!.updatePassword ? 'status-badge status-badge--warning' : 'status-badge status-badge--neutral'">
                    {{ membership()!.updatePassword ? 'Yes' : 'No' }}
                  </span>
                </div>
              </div>

              <!-- Date Information -->
              <div class="dates-grid">
                <div class="date-item">
                  <span class="date-label">Created Date</span>
                  <span class="date-value">{{ formatDate(membership()!.createdDate) }}</span>
                </div>
                <div class="date-item">
                  <span class="date-label">Last Login</span>
                  <span class="date-value">{{ formatDate(membership()!.lastLoginDate) }}</span>
                </div>
                <div class="date-item">
                  <span class="date-label">Last Activity</span>
                  <span class="date-value">{{ formatDate(membership()!.lastActivityDate) }}</span>
                </div>
                <div class="date-item">
                  <span class="date-label">Last Password Change</span>
                  <span class="date-value">{{ formatDate(membership()!.lastPasswordChangeDate) }}</span>
                </div>
                @if (membership()!.lockedOut) {
                  <div class="date-item">
                    <span class="date-label">Last Lockout</span>
                    <span class="date-value date-value--warning">{{ formatDate(membership()!.lastLockoutDate) }}</span>
                  </div>
                }
              </div>

              <!-- Administrative Action Buttons -->
              <!-- MIGRATION: Replaces cmdAuthorize, cmdUnAuthorize, cmdUnLock, cmdPassword buttons
                   Visibility logic from Membership.ascx.vb DataBind() (lines 135-145) -->
              @if (!isCurrentUser()) {
                <div class="admin-actions">
                  @if (canAuthorize()) {
                    <!-- MIGRATION: cmdAuthorize.Visible = Not Membership.Approved (line 143) -->
                    <button
                      type="button"
                      class="btn btn-success"
                      (click)="onAuthorize()"
                      [disabled]="actionInProgress()">
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="20 6 9 17 4 12"></polyline>
                      </svg>
                      Authorize User
                    </button>
                  }
                  @if (canUnauthorize()) {
                    <!-- MIGRATION: cmdUnAuthorize.Visible = Membership.Approved (line 142) -->
                    <button
                      type="button"
                      class="btn btn-warning"
                      (click)="showUnauthorizeConfirmation()">
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line>
                      </svg>
                      Unauthorize User
                    </button>
                  }
                  @if (canUnlock()) {
                    <!-- MIGRATION: cmdUnLock.Visible = Membership.LockedOut (line 141) -->
                    <button
                      type="button"
                      class="btn btn-primary"
                      (click)="onUnlock()"
                      [disabled]="actionInProgress()">
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                        <path d="M7 11V7a5 5 0 0 1 9.9-1"></path>
                      </svg>
                      Unlock Account
                    </button>
                  }
                  @if (canForcePasswordChange()) {
                    <!-- MIGRATION: cmdPassword.Visible = Not Membership.UpdatePassword (line 144) -->
                    <button
                      type="button"
                      class="btn btn-secondary"
                      (click)="onForcePasswordChange()"
                      [disabled]="actionInProgress()">
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                        <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                      </svg>
                      Force Password Change
                    </button>
                  }
                </div>
              }
            </div>
          </section>
        }

        <!-- Profile Edit Form Section -->
        <!-- MIGRATION: Derived from ProfileProperties.DataSource binding in Profile.ascx.vb (line 171-172) -->
        <section class="card profile-form-section">
          <h2 class="card-title">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
              <circle cx="12" cy="7" r="4"></circle>
            </svg>
            Profile Information
          </h2>
          <div class="card-body">
            <form [formGroup]="profileForm" (ngSubmit)="onSaveProfile()">
              <!-- Name Section -->
              <div class="form-section">
                <h3 class="form-section-title">Personal Information</h3>
                <div class="form-row">
                  <app-text-input
                    label="First Name"
                    formControlName="firstName"
                    [required]="true"
                    [errorMessage]="getFieldError('firstName')" />
                  <app-text-input
                    label="Last Name"
                    formControlName="lastName"
                    [required]="true"
                    [errorMessage]="getFieldError('lastName')" />
                </div>
                <div class="form-row">
                  <app-text-input
                    label="Email"
                    type="email"
                    formControlName="email"
                    [required]="true"
                    [errorMessage]="getFieldError('email')" />
                </div>
              </div>

              <!-- Address Section -->
              <!-- MIGRATION: Derived from UserProfile.vb cStreet, cCity, cRegion, cCountry, cPostalCode constants (lines 52-58) -->
              <div class="form-section">
                <h3 class="form-section-title">Address</h3>
                <div class="form-row">
                  <app-text-input
                    label="Street Address"
                    formControlName="street"
                    placeholder="Enter street address" />
                </div>
                <div class="form-row">
                  <app-text-input
                    label="City"
                    formControlName="city"
                    placeholder="Enter city" />
                  <app-text-input
                    label="Region/State"
                    formControlName="region"
                    placeholder="Enter region or state" />
                </div>
                <div class="form-row">
                  <div class="form-field">
                    <label for="country" class="form-label">Country</label>
                    <select
                      id="country"
                      formControlName="country"
                      class="form-select"
                      (change)="onCountryChange()">
                      <option value="">Select a country</option>
                      @for (option of countryOptions; track option.value) {
                        <option [value]="option.value" [disabled]="option.disabled">
                          {{ option.label }}
                        </option>
                      }
                    </select>
                  </div>
                  <app-text-input
                    label="Postal Code"
                    formControlName="postalCode"
                    placeholder="Enter postal code" />
                </div>
              </div>

              <!-- Contact Section -->
              <!-- MIGRATION: Derived from UserProfile.vb cTelephone, cCell, cWebsite constants (lines 61-66) -->
              <div class="form-section">
                <h3 class="form-section-title">Contact Information</h3>
                <div class="form-row">
                  <app-text-input
                    label="Telephone"
                    type="tel"
                    formControlName="telephone"
                    placeholder="Enter telephone number" />
                  <app-text-input
                    label="Cell/Mobile"
                    type="tel"
                    formControlName="cell"
                    placeholder="Enter cell number" />
                </div>
                <div class="form-row">
                  <app-text-input
                    label="Website"
                    type="url"
                    formControlName="website"
                    placeholder="https://example.com" />
                </div>
              </div>

              <!-- Preferences Section -->
              <!-- MIGRATION: Derived from UserProfile.vb cTimeZone, cPreferredLocale constants (lines 70-71) -->
              <div class="form-section">
                <h3 class="form-section-title">Preferences</h3>
                <div class="form-row">
                  <div class="form-field">
                    <label for="timeZone" class="form-label">Time Zone</label>
                    <select id="timeZone" formControlName="timeZone" class="form-select">
                      <option [ngValue]="null">Select time zone</option>
                      @for (tz of timeZoneOptions; track tz.value) {
                        <option [ngValue]="tz.value">{{ tz.label }}</option>
                      }
                    </select>
                  </div>
                  <div class="form-field">
                    <label for="preferredLocale" class="form-label">Preferred Locale</label>
                    <select id="preferredLocale" formControlName="preferredLocale" class="form-select">
                      <option value="">Select locale</option>
                      @for (locale of localeOptions; track locale.value) {
                        <option [value]="locale.value">{{ locale.label }}</option>
                      }
                    </select>
                  </div>
                </div>
              </div>

              <!-- Form Actions -->
              <!-- MIGRATION: Replaces cmdUpdate button from Profile.ascx.vb (lines 114-121) -->
              <div class="form-actions">
                <button
                  type="button"
                  class="btn btn-secondary"
                  (click)="onCancel()">
                  Cancel
                </button>
                <button
                  type="submit"
                  class="btn btn-primary"
                  [disabled]="!profileForm.valid || savingProfile()">
                  @if (savingProfile()) {
                    <app-loading-spinner size="small" />
                    Saving...
                  } @else {
                    Save Profile
                  }
                </button>
              </div>
            </form>
          </div>
        </section>
      </div>
    }

    <!-- Confirmation Dialog for Unauthorize Action -->
    <!-- MIGRATION: Replaces DNN ClientAPI.AddButtonConfirm pattern for destructive actions -->
    <app-confirmation-dialog
      #unauthorizeDialog
      title="Unauthorize User"
      message="Are you sure you want to unauthorize this user? They will no longer be able to log in."
      confirmText="Unauthorize"
      cancelText="Cancel"
      confirmButtonType="warning"
      (confirmed)="onUnauthorize()"
      (cancelled)="onUnauthorizeCancelled()" />
  `,
  styles: [`
    /* Container Styles */
    .loading-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 400px;
      padding: 2rem;
    }

    .error-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 400px;
      padding: 2rem;
      text-align: center;
    }

    .error-icon {
      color: #ef4444;
      margin-bottom: 1rem;
    }

    .error-title {
      font-size: 1.5rem;
      font-weight: 600;
      color: #1f2937;
      margin: 0 0 0.5rem;
    }

    .error-message {
      color: #6b7280;
      margin: 0 0 1.5rem;
      max-width: 400px;
    }

    .profile-container {
      max-width: 900px;
      margin: 0 auto;
      padding: 1.5rem;
    }

    /* Header Styles */
    .profile-header {
      margin-bottom: 2rem;
    }

    .profile-title {
      font-size: 1.75rem;
      font-weight: 700;
      color: #1f2937;
      margin: 0 0 0.5rem;
    }

    .profile-meta {
      display: flex;
      gap: 1rem;
      color: #6b7280;
      font-size: 0.875rem;
    }

    .user-id,
    .username {
      background: #f3f4f6;
      padding: 0.25rem 0.75rem;
      border-radius: 0.375rem;
    }

    /* Card Styles */
    .card {
      background: #ffffff;
      border: 1px solid #e5e7eb;
      border-radius: 0.75rem;
      margin-bottom: 1.5rem;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
    }

    .card-title {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 1.125rem;
      font-weight: 600;
      color: #1f2937;
      padding: 1rem 1.5rem;
      margin: 0;
      border-bottom: 1px solid #e5e7eb;
      background: #f9fafb;
      border-radius: 0.75rem 0.75rem 0 0;
    }

    .card-body {
      padding: 1.5rem;
    }

    /* Status Grid */
    .status-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 1rem;
      margin-bottom: 1.5rem;
    }

    .status-item {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .status-label {
      font-size: 0.75rem;
      font-weight: 500;
      color: #6b7280;
      text-transform: uppercase;
      letter-spacing: 0.025em;
    }

    .status-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.25rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.875rem;
      font-weight: 500;
      width: fit-content;
    }

    .status-badge--success {
      background: #d1fae5;
      color: #065f46;
    }

    .status-badge--warning {
      background: #fef3c7;
      color: #92400e;
    }

    .status-badge--danger {
      background: #fee2e2;
      color: #991b1b;
    }

    .status-badge--neutral {
      background: #f3f4f6;
      color: #374151;
    }

    /* Dates Grid */
    .dates-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
      padding: 1rem;
      background: #f9fafb;
      border-radius: 0.5rem;
      margin-bottom: 1.5rem;
    }

    .date-item {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .date-label {
      font-size: 0.75rem;
      font-weight: 500;
      color: #6b7280;
    }

    .date-value {
      font-size: 0.875rem;
      color: #1f2937;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    }

    .date-value--warning {
      color: #dc2626;
    }

    /* Admin Actions */
    .admin-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      padding-top: 1rem;
      border-top: 1px solid #e5e7eb;
    }

    /* Button Styles */
    .btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.625rem 1.25rem;
      font-size: 0.875rem;
      font-weight: 500;
      border-radius: 0.5rem;
      border: none;
      cursor: pointer;
      transition: all 0.15s ease;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background: #3b82f6;
      color: #ffffff;
    }

    .btn-primary:hover:not(:disabled) {
      background: #2563eb;
    }

    .btn-secondary {
      background: #f3f4f6;
      color: #374151;
      border: 1px solid #d1d5db;
    }

    .btn-secondary:hover:not(:disabled) {
      background: #e5e7eb;
    }

    .btn-success {
      background: #10b981;
      color: #ffffff;
    }

    .btn-success:hover:not(:disabled) {
      background: #059669;
    }

    .btn-warning {
      background: #f59e0b;
      color: #ffffff;
    }

    .btn-warning:hover:not(:disabled) {
      background: #d97706;
    }

    .btn-danger {
      background: #ef4444;
      color: #ffffff;
    }

    .btn-danger:hover:not(:disabled) {
      background: #dc2626;
    }

    /* Form Section Styles */
    .form-section {
      margin-bottom: 2rem;
    }

    .form-section:last-of-type {
      margin-bottom: 1.5rem;
    }

    .form-section-title {
      font-size: 0.9375rem;
      font-weight: 600;
      color: #374151;
      margin: 0 0 1rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #e5e7eb;
    }

    .form-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .form-row:last-child {
      margin-bottom: 0;
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .form-label {
      font-size: 0.875rem;
      font-weight: 500;
      color: #374151;
    }

    .form-select {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
      line-height: 1.5;
      background: #ffffff;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .form-select:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }

    /* Form Actions */
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      padding-top: 1.5rem;
      border-top: 1px solid #e5e7eb;
    }

    /* Responsive Adjustments */
    @media (max-width: 640px) {
      .profile-container {
        padding: 1rem;
      }

      .profile-title {
        font-size: 1.5rem;
      }

      .profile-meta {
        flex-direction: column;
        gap: 0.5rem;
      }

      .card-body {
        padding: 1rem;
      }

      .form-row {
        grid-template-columns: 1fr;
      }

      .form-actions {
        flex-direction: column-reverse;
      }

      .form-actions .btn {
        width: 100%;
        justify-content: center;
      }

      .admin-actions {
        flex-direction: column;
      }

      .admin-actions .btn {
        width: 100%;
        justify-content: center;
      }
    }
  `]
})
export class UserProfileComponent implements OnInit {
  // ===========================================================================
  // DEPENDENCY INJECTION
  // MIGRATION: Replaces DNN module lifecycle and ByRef parameters
  // ===========================================================================

  /**
   * User service for API operations
   * MIGRATION: Replaces UserController reference from VB.NET
   */
  private readonly userService = inject(UserService);

  /**
   * Activated route for accessing route parameters
   * MIGRATION: Replaces ViewState-backed UserId from UserModuleBase
   */
  private readonly route = inject(ActivatedRoute);

  /**
   * Router for navigation
   * MIGRATION: Replaces Response.Redirect patterns
   */
  private readonly router = inject(Router);

  /**
   * DestroyRef for managing subscription cleanup
   */
  private readonly destroyRef = inject(DestroyRef);

  // ===========================================================================
  // VIEW CHILDREN
  // ===========================================================================

  /**
   * Reference to the unauthorize confirmation dialog
   */
  @ViewChild('unauthorizeDialog') unauthorizeDialog!: ConfirmationDialogComponent;

  // ===========================================================================
  // REACTIVE STATE SIGNALS
  // MIGRATION: Replaces VB.NET ViewState and data binding
  // ===========================================================================

  /**
   * Current user being viewed/edited
   * MIGRATION: Replaces User property from UserModuleBase (Membership.ascx.vb line 64-72)
   */
  readonly user = signal<User | null>(null);

  /**
   * User profile data
   * MIGRATION: Replaces UserProfile property (Profile.ascx.vb line 131-138)
   */
  readonly profile = signal<UserProfile | null>(null);

  /**
   * User membership data
   * MIGRATION: Replaces Membership property (Membership.ascx.vb line 64-72)
   */
  readonly membership = signal<UserMembership | null>(null);

  /**
   * Loading state signal
   */
  readonly loading = signal<boolean>(true);

  /**
   * Error message signal
   */
  readonly error = signal<string | null>(null);

  /**
   * Signal indicating if an administrative action is in progress
   */
  readonly actionInProgress = signal<boolean>(false);

  /**
   * Signal indicating if profile is being saved
   */
  readonly savingProfile = signal<boolean>(false);

  /**
   * Current logged-in user ID (would typically come from auth service)
   * For now, we'll use a placeholder; in real implementation, inject AuthService
   */
  private readonly currentUserId = signal<number | null>(null);

  // ===========================================================================
  // COMPUTED SIGNALS
  // MIGRATION: Replaces VB.NET visibility logic from DataBind() method
  // ===========================================================================

  /**
   * Computed signal to check if viewing own profile
   * MIGRATION: Replaces UserInfo.UserID = User.UserID check (Membership.ascx.vb line 135)
   */
  readonly isCurrentUser = computed(() => {
    const currentId = this.currentUserId();
    const userId = this.user()?.userId;
    return currentId !== null && userId !== undefined && currentId === userId;
  });

  /**
   * Computed signal for authorize button visibility
   * MIGRATION: Replaces cmdAuthorize.Visible = Not Membership.Approved (line 143)
   */
  readonly canAuthorize = computed(() => {
    const m = this.membership();
    return m !== null && !m.approved && !this.isCurrentUser();
  });

  /**
   * Computed signal for unauthorize button visibility
   * MIGRATION: Replaces cmdUnAuthorize.Visible = Membership.Approved (line 142)
   */
  readonly canUnauthorize = computed(() => {
    const m = this.membership();
    return m !== null && m.approved && !this.isCurrentUser();
  });

  /**
   * Computed signal for unlock button visibility
   * MIGRATION: Replaces cmdUnLock.Visible = Membership.LockedOut (line 141)
   */
  readonly canUnlock = computed(() => {
    const m = this.membership();
    return m !== null && m.lockedOut && !this.isCurrentUser();
  });

  /**
   * Computed signal for force password change button visibility
   * MIGRATION: Replaces cmdPassword.Visible = Not Membership.UpdatePassword (line 144)
   */
  readonly canForcePasswordChange = computed(() => {
    const m = this.membership();
    return m !== null && !m.updatePassword && !this.isCurrentUser();
  });

  // ===========================================================================
  // PROFILE FORM
  // MIGRATION: Derived from ProfileProperties binding (Profile.ascx.vb lines 162-172)
  // ===========================================================================

  /**
   * Typed reactive form for profile editing
   */
  readonly profileForm = new FormGroup<ProfileFormControls>({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(50)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(256)] }),
    street: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    city: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    region: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    country: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] }),
    postalCode: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(20)] }),
    telephone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
    cell: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(50)] }),
    website: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    timeZone: new FormControl<number | null>(null),
    preferredLocale: new FormControl('', { nonNullable: true })
  });

  // ===========================================================================
  // SELECT OPTIONS
  // ===========================================================================

  /**
   * Country options for the country dropdown
   * MIGRATION: In original DNN, countries were loaded from a list
   */
  readonly countryOptions: SelectOption[] = [
    { value: 'US', label: 'United States' },
    { value: 'CA', label: 'Canada' },
    { value: 'GB', label: 'United Kingdom' },
    { value: 'DE', label: 'Germany' },
    { value: 'FR', label: 'France' },
    { value: 'AU', label: 'Australia' },
    { value: 'JP', label: 'Japan' },
    { value: 'CN', label: 'China' },
    { value: 'IN', label: 'India' },
    { value: 'BR', label: 'Brazil' },
    { value: 'MX', label: 'Mexico' },
    { value: 'ES', label: 'Spain' },
    { value: 'IT', label: 'Italy' },
    { value: 'NL', label: 'Netherlands' },
    { value: 'SE', label: 'Sweden' }
  ];

  /**
   * Region options - dynamically populated based on selected country
   */
  regionOptions: SelectOption[] = [];

  /**
   * Time zone options
   */
  readonly timeZoneOptions: SelectOption[] = [
    { value: -720, label: '(UTC-12:00) International Date Line West' },
    { value: -660, label: '(UTC-11:00) Midway Island, Samoa' },
    { value: -600, label: '(UTC-10:00) Hawaii' },
    { value: -540, label: '(UTC-09:00) Alaska' },
    { value: -480, label: '(UTC-08:00) Pacific Time (US & Canada)' },
    { value: -420, label: '(UTC-07:00) Mountain Time (US & Canada)' },
    { value: -360, label: '(UTC-06:00) Central Time (US & Canada)' },
    { value: -300, label: '(UTC-05:00) Eastern Time (US & Canada)' },
    { value: -240, label: '(UTC-04:00) Atlantic Time (Canada)' },
    { value: -180, label: '(UTC-03:00) Brasilia' },
    { value: 0, label: '(UTC) London, Dublin, Edinburgh' },
    { value: 60, label: '(UTC+01:00) Berlin, Paris, Rome' },
    { value: 120, label: '(UTC+02:00) Athens, Cairo' },
    { value: 180, label: '(UTC+03:00) Moscow, Baghdad' },
    { value: 240, label: '(UTC+04:00) Dubai' },
    { value: 300, label: '(UTC+05:00) Karachi' },
    { value: 330, label: '(UTC+05:30) Mumbai, New Delhi' },
    { value: 360, label: '(UTC+06:00) Dhaka' },
    { value: 420, label: '(UTC+07:00) Bangkok, Jakarta' },
    { value: 480, label: '(UTC+08:00) Beijing, Singapore' },
    { value: 540, label: '(UTC+09:00) Tokyo, Seoul' },
    { value: 600, label: '(UTC+10:00) Sydney, Melbourne' },
    { value: 720, label: '(UTC+12:00) Auckland, Wellington' }
  ];

  /**
   * Locale options
   */
  readonly localeOptions: SelectOption[] = [
    { value: 'en-US', label: 'English (United States)' },
    { value: 'en-GB', label: 'English (United Kingdom)' },
    { value: 'de-DE', label: 'German (Germany)' },
    { value: 'fr-FR', label: 'French (France)' },
    { value: 'es-ES', label: 'Spanish (Spain)' },
    { value: 'it-IT', label: 'Italian (Italy)' },
    { value: 'pt-BR', label: 'Portuguese (Brazil)' },
    { value: 'ja-JP', label: 'Japanese (Japan)' },
    { value: 'zh-CN', label: 'Chinese (Simplified)' },
    { value: 'zh-TW', label: 'Chinese (Traditional)' },
    { value: 'ko-KR', label: 'Korean (Korea)' }
  ];

  // ===========================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: Replaces Page_Load and DataBind calls
  // ===========================================================================

  /**
   * Component initialization
   * MIGRATION: Replaces Page_Load event handler (Membership.ascx.vb line 180-184)
   */
  ngOnInit(): void {
    // Subscribe to route params to get userId
    this.route.params
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter(params => !!params['id']),
        switchMap(params => {
          const userId = parseInt(params['id'], 10);
          if (isNaN(userId)) {
            this.error.set('Invalid user ID');
            this.loading.set(false);
            return of(null);
          }
          return this.loadUser(userId);
        })
      )
      .subscribe();
  }

  // ===========================================================================
  // DATA LOADING
  // MIGRATION: Replaces Page_Load and MembershipEditor.DataSource patterns
  // ===========================================================================

  /**
   * Loads user data from the API
   * MIGRATION: Replaces MembershipEditor.DataSource = Membership pattern (line 147-148)
   *
   * @param id - User ID to load
   */
  private loadUser(id: number) {
    this.loading.set(true);
    this.error.set(null);

    return this.userService.getUserById(id).pipe(
      tap(user => {
        this.user.set(user);
        this.profile.set(user.profile ?? null);
        this.membership.set(user.membership ?? null);
        this.initializeForm(user);
        this.loading.set(false);
      }),
      catchError(err => {
        console.error('Error loading user:', err);
        this.error.set('Failed to load user profile. The user may not exist or you may not have permission to view it.');
        this.loading.set(false);
        return of(null);
      })
    );
  }

  /**
   * Initializes the profile form with user data
   *
   * @param user - User data to populate form with
   */
  private initializeForm(user: User): void {
    const profile = user.profile;

    this.profileForm.patchValue({
      firstName: user.firstName ?? profile?.firstName ?? '',
      lastName: user.lastName ?? profile?.lastName ?? '',
      email: user.email ?? '',
      street: profile?.street ?? '',
      city: profile?.city ?? '',
      region: profile?.region ?? '',
      country: profile?.country ?? '',
      postalCode: profile?.postalCode ?? '',
      telephone: profile?.telephone ?? '',
      cell: profile?.cell ?? '',
      website: profile?.website ?? '',
      timeZone: profile?.timeZone ?? null,
      preferredLocale: profile?.preferredLocale ?? ''
    });

    // Update region options based on selected country
    if (profile?.country) {
      this.updateRegionOptions(profile.country);
    }
  }

  // ===========================================================================
  // ADMINISTRATIVE ACTIONS
  // MIGRATION: Derived from Membership.ascx.vb event handlers
  // ===========================================================================

  /**
   * Authorizes a user account
   * MIGRATION: Replaces cmdAuthorize_Click (Membership.ascx.vb lines 194-206)
   *
   * Original VB.NET:
   * ```vb
   * User.Membership = CType(MembershipEditor.DataSource, UserMembership)
   * User.Membership.Approved = True
   * UserController.UpdateUser(PortalId, User)
   * OnMembershipAuthorized(EventArgs.Empty)
   * ```
   */
  onAuthorize(): void {
    const currentUser = this.user();
    const currentMembership = this.membership();

    if (!currentUser || !currentMembership) {
      return;
    }

    this.actionInProgress.set(true);

    const updateRequest: UpdateUserRequest = {
      membership: {
        ...currentMembership,
        approved: true
      }
    };

    this.userService.updateUser(currentUser.userId, updateRequest)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(updatedUser => {
          // Update local state
          this.user.set(updatedUser);
          this.membership.set(updatedUser.membership ?? null);
          this.actionInProgress.set(false);
          // MIGRATION: Equivalent to OnMembershipAuthorized event
          console.log('User authorized successfully');
        }),
        catchError(err => {
          console.error('Error authorizing user:', err);
          this.actionInProgress.set(false);
          return of(null);
        })
      )
      .subscribe();
  }

  /**
   * Shows confirmation dialog before unauthorizing
   */
  showUnauthorizeConfirmation(): void {
    this.unauthorizeDialog.open();
  }

  /**
   * Unauthorizes a user account
   * MIGRATION: Replaces cmdUnAuthorize_Click (Membership.ascx.vb lines 238-250)
   *
   * Original VB.NET:
   * ```vb
   * User.Membership = CType(MembershipEditor.DataSource, UserMembership)
   * User.Membership.Approved = False
   * UserController.UpdateUser(PortalId, User)
   * OnMembershipUnAuthorized(EventArgs.Empty)
   * ```
   */
  onUnauthorize(): void {
    const currentUser = this.user();
    const currentMembership = this.membership();

    if (!currentUser || !currentMembership) {
      return;
    }

    this.actionInProgress.set(true);

    const updateRequest: UpdateUserRequest = {
      membership: {
        ...currentMembership,
        approved: false
      }
    };

    this.userService.updateUser(currentUser.userId, updateRequest)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(updatedUser => {
          // Update local state
          this.user.set(updatedUser);
          this.membership.set(updatedUser.membership ?? null);
          this.actionInProgress.set(false);
          // MIGRATION: Equivalent to OnMembershipUnAuthorized event
          console.log('User unauthorized successfully');
        }),
        catchError(err => {
          console.error('Error unauthorizing user:', err);
          this.actionInProgress.set(false);
          return of(null);
        })
      )
      .subscribe();
  }

  /**
   * Called when unauthorize action is cancelled
   */
  onUnauthorizeCancelled(): void {
    // No action needed, dialog already closed
  }

  /**
   * Unlocks a locked user account
   * MIGRATION: Replaces cmdUnLock_Click (Membership.ascx.vb lines 260-268)
   *
   * Original VB.NET:
   * ```vb
   * Dim isUnLocked As Boolean = UserController.UnLockUser(User)
   * If isUnLocked Then
   *     User.Membership.LockedOut = False
   *     OnMembershipUnLocked(EventArgs.Empty)
   * End If
   * ```
   */
  onUnlock(): void {
    const currentUser = this.user();

    if (!currentUser) {
      return;
    }

    this.actionInProgress.set(true);

    this.userService.unlockUser(currentUser.userId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(updatedUser => {
          // Update local state
          this.user.set(updatedUser);
          this.membership.set(updatedUser.membership ?? null);
          this.actionInProgress.set(false);
          // MIGRATION: Equivalent to OnMembershipUnLocked event
          console.log('User account unlocked successfully');
        }),
        catchError(err => {
          console.error('Error unlocking user:', err);
          this.actionInProgress.set(false);
          return of(null);
        })
      )
      .subscribe();
  }

  /**
   * Forces user to change password on next login
   * MIGRATION: Replaces cmdPassword_Click (Membership.ascx.vb lines 216-228)
   *
   * Original VB.NET:
   * ```vb
   * User.Membership = CType(MembershipEditor.DataSource, UserMembership)
   * User.Membership.UpdatePassword = True
   * UserController.UpdateUser(PortalId, User)
   * DataBind()
   * ```
   */
  onForcePasswordChange(): void {
    const currentUser = this.user();
    const currentMembership = this.membership();

    if (!currentUser || !currentMembership) {
      return;
    }

    this.actionInProgress.set(true);

    const updateRequest: UpdateUserRequest = {
      membership: {
        ...currentMembership,
        updatePassword: true
      }
    };

    this.userService.updateUser(currentUser.userId, updateRequest)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(updatedUser => {
          // Update local state
          this.user.set(updatedUser);
          this.membership.set(updatedUser.membership ?? null);
          this.actionInProgress.set(false);
          console.log('Password change requirement set successfully');
        }),
        catchError(err => {
          console.error('Error setting password change requirement:', err);
          this.actionInProgress.set(false);
          return of(null);
        })
      )
      .subscribe();
  }

  // ===========================================================================
  // PROFILE UPDATE
  // MIGRATION: Derived from Profile.ascx.vb cmdUpdate_Click (lines 220-232)
  // ===========================================================================

  /**
   * Saves profile changes
   * MIGRATION: Replaces cmdUpdate_Click (Profile.ascx.vb lines 220-232)
   *
   * Original VB.NET:
   * ```vb
   * If IsValid Then
   *     Dim properties As ProfilePropertyDefinitionCollection = CType(ProfileProperties.DataSource, ProfilePropertyDefinitionCollection)
   *     User = ProfileController.UpdateUserProfile(User, properties)
   *     OnProfileUpdated(EventArgs.Empty)
   *     OnProfileUpdateCompleted(EventArgs.Empty)
   * End If
   * ```
   */
  onSaveProfile(): void {
    // MIGRATION: Replaces IsValid check (line 222)
    if (!this.profileForm.valid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const currentUser = this.user();
    if (!currentUser) {
      return;
    }

    this.savingProfile.set(true);

    const formValue = this.profileForm.getRawValue();

    // Build profile update object
    const profileUpdate: Partial<UserProfile> = {
      firstName: formValue.firstName,
      lastName: formValue.lastName,
      street: formValue.street || undefined,
      city: formValue.city || undefined,
      region: formValue.region || undefined,
      country: formValue.country || undefined,
      postalCode: formValue.postalCode || undefined,
      telephone: formValue.telephone || undefined,
      cell: formValue.cell || undefined,
      website: formValue.website || undefined,
      timeZone: formValue.timeZone ?? undefined,
      preferredLocale: formValue.preferredLocale || undefined
    };

    this.userService.updateProfile(currentUser.userId, profileUpdate)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(updatedUser => {
          // Update local state
          this.user.set(updatedUser);
          this.profile.set(updatedUser.profile ?? null);
          this.savingProfile.set(false);
          // MIGRATION: Equivalent to OnProfileUpdated and OnProfileUpdateCompleted events
          console.log('Profile saved successfully');
        }),
        catchError(err => {
          console.error('Error saving profile:', err);
          this.savingProfile.set(false);
          return of(null);
        })
      )
      .subscribe();
  }

  /**
   * Cancels profile editing and navigates back
   */
  onCancel(): void {
    this.router.navigate(['/users']);
  }

  // ===========================================================================
  // FORM HELPERS
  // ===========================================================================

  /**
   * Gets the error message for a form field
   *
   * @param fieldName - Name of the form field
   * @returns Error message or empty string
   */
  getFieldError(fieldName: keyof ProfileFormControls): string {
    const control = this.profileForm.get(fieldName);
    if (!control || !control.errors || !control.touched) {
      return '';
    }

    if (control.errors['required']) {
      return `${this.getFieldLabel(fieldName)} is required`;
    }
    if (control.errors['email']) {
      return 'Please enter a valid email address';
    }
    if (control.errors['maxlength']) {
      const maxLength = control.errors['maxlength'].requiredLength;
      return `Maximum length is ${maxLength} characters`;
    }

    return 'Invalid value';
  }

  /**
   * Gets a human-readable label for a form field
   *
   * @param fieldName - Name of the form field
   * @returns Human-readable label
   */
  private getFieldLabel(fieldName: keyof ProfileFormControls): string {
    const labels: Record<keyof ProfileFormControls, string> = {
      firstName: 'First Name',
      lastName: 'Last Name',
      email: 'Email',
      street: 'Street Address',
      city: 'City',
      region: 'Region',
      country: 'Country',
      postalCode: 'Postal Code',
      telephone: 'Telephone',
      cell: 'Cell/Mobile',
      website: 'Website',
      timeZone: 'Time Zone',
      preferredLocale: 'Preferred Locale'
    };
    return labels[fieldName] || fieldName;
  }

  /**
   * Handles country selection change to update region options
   */
  onCountryChange(): void {
    const country = this.profileForm.get('country')?.value;
    if (country) {
      this.updateRegionOptions(country);
      // Clear region when country changes
      this.profileForm.patchValue({ region: '' });
    }
  }

  /**
   * Updates region options based on selected country
   *
   * @param countryCode - ISO country code
   */
  private updateRegionOptions(countryCode: string): void {
    // Sample region data - in production, this would come from an API
    const regionsByCountry: Record<string, SelectOption[]> = {
      US: [
        { value: 'AL', label: 'Alabama' },
        { value: 'AK', label: 'Alaska' },
        { value: 'AZ', label: 'Arizona' },
        { value: 'CA', label: 'California' },
        { value: 'CO', label: 'Colorado' },
        { value: 'FL', label: 'Florida' },
        { value: 'GA', label: 'Georgia' },
        { value: 'NY', label: 'New York' },
        { value: 'TX', label: 'Texas' },
        { value: 'WA', label: 'Washington' }
      ],
      CA: [
        { value: 'AB', label: 'Alberta' },
        { value: 'BC', label: 'British Columbia' },
        { value: 'ON', label: 'Ontario' },
        { value: 'QC', label: 'Quebec' }
      ],
      GB: [
        { value: 'ENG', label: 'England' },
        { value: 'SCT', label: 'Scotland' },
        { value: 'WLS', label: 'Wales' },
        { value: 'NIR', label: 'Northern Ireland' }
      ],
      DE: [
        { value: 'BY', label: 'Bavaria' },
        { value: 'BE', label: 'Berlin' },
        { value: 'HH', label: 'Hamburg' },
        { value: 'NW', label: 'North Rhine-Westphalia' }
      ],
      AU: [
        { value: 'NSW', label: 'New South Wales' },
        { value: 'VIC', label: 'Victoria' },
        { value: 'QLD', label: 'Queensland' },
        { value: 'WA', label: 'Western Australia' }
      ]
    };

    this.regionOptions = regionsByCountry[countryCode] || [];
  }

  // ===========================================================================
  // DATE FORMATTING
  // ===========================================================================

  /**
   * Formats a date value for display
   *
   * @param date - Date value to format
   * @returns Formatted date string
   */
  formatDate(date: Date | string | null | undefined): string {
    if (!date) {
      return 'N/A';
    }

    try {
      const dateObj = typeof date === 'string' ? new Date(date) : date;
      if (isNaN(dateObj.getTime())) {
        return 'N/A';
      }

      // Format: "Jan 15, 2024 3:45 PM"
      return dateObj.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true
      });
    } catch {
      return 'N/A';
    }
  }
}
