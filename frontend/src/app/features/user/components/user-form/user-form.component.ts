/**
 * User Form Component - Angular 19 Standalone User Create/Edit Form
 *
 * MIGRATION: Converted from DotNetNuke 4.x VB.NET User.ascx.vb WebForms control:
 * - Website/admin/Users/User.ascx.vb
 * - Library/Components/Users/UserInfo.vb
 * - Library/Components/Users/UserController.vb
 *
 * This component replaces the legacy WebForms control with a modern Angular 19
 * reactive form implementation using signals for state management.
 *
 * Key transformations:
 * - VB.NET UserEditor PropertyEditor → Angular Reactive Forms with FormGroup/FormControl
 * - VB.NET txtPassword/txtConfirm validation → Custom cross-field validator
 * - VB.NET cmdUpdate_Click (lines 361-384) → onSubmit() method
 * - VB.NET cmdDelete_Click (lines 342-350) → onDelete() method
 * - VB.NET Page_Load/DataBind (lines 248-297) → ngOnInit() with route params
 * - VB.NET IsValid property (lines 73-77) → Form validation with markAllAsTouched
 * - VB.NET chkAuthorize.Checked (line 223) → isAuthorized FormControl
 *
 * @fileoverview Angular 19 standalone component for user create/edit operations
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  OnInit,
  DestroyRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators,
  ValidatorFn,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { UserService } from '../../services/user.service';
import { User } from '../../models/user.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Interface for the typed reactive form structure.
 * Maps directly to user form fields for type-safe form handling.
 */
interface UserFormControls {
  username: FormControl<string>;
  email: FormControl<string>;
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  displayName: FormControl<string>;
  password: FormControl<string>;
  confirmPassword: FormControl<string>;
  isAuthorized: FormControl<boolean>;
}

/**
 * UserFormComponent
 *
 * Angular 19 standalone component implementing a reactive form for user create/edit
 * functionality. Supports both create mode (new user via '/new' route) and edit mode
 * (existing user by ':id' route parameter).
 *
 * MIGRATION: Replaces DNN's User.ascx WebForms control with modern Angular architecture:
 * - UserEditor PropertyEditor → Reactive form with FormGroup/FormControl
 * - Password validation (lines 151-154) → Custom passwordMatchValidator
 * - Email validation (Security_EmailValidation lines 408-412) → Validators.email
 * - cmdUpdate_Click/cmdDelete_Click → onSubmit()/onDelete() methods
 *
 * @example
 * ```typescript
 * // Create mode - route: /users/new
 * <app-user-form />
 *
 * // Edit mode - route: /users/:id
 * <app-user-form />
 * ```
 */
@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="user-form-container">
      <h2 class="form-title">{{ isEditMode() ? 'Edit User' : 'Create User' }}</h2>

      @if (loading()) {
        <div class="loading-container">
          <app-loading-spinner size="large" message="Loading user data..." />
        </div>
      } @else {
        @if (errorMessage()) {
          <div class="alert alert-error" role="alert">
            <span class="alert-icon">⚠</span>
            <span class="alert-message">{{ errorMessage() }}</span>
            <button
              type="button"
              class="alert-dismiss"
              (click)="errorMessage.set(null)"
              aria-label="Dismiss error"
            >
              ×
            </button>
          </div>
        }

        @if (successMessage()) {
          <div class="alert alert-success" role="alert">
            <span class="alert-icon">✓</span>
            <span class="alert-message">{{ successMessage() }}</span>
            <button
              type="button"
              class="alert-dismiss"
              (click)="successMessage.set(null)"
              aria-label="Dismiss message"
            >
              ×
            </button>
          </div>
        }

        <form
          [formGroup]="userForm"
          (ngSubmit)="onSubmit()"
          class="user-form"
          novalidate
        >
          <!-- Username field - readonly in edit mode per UserInfo.Username IsReadOnly attribute -->
          <div class="form-group">
            <label for="username" class="form-label">
              Username <span class="required">*</span>
            </label>
            <input
              id="username"
              type="text"
              formControlName="username"
              [readonly]="isEditMode()"
              class="form-control"
              [class.is-invalid]="isFieldInvalid('username')"
              [class.readonly]="isEditMode()"
              autocomplete="username"
              placeholder="Enter username"
            />
            @if (isFieldInvalid('username')) {
              <div class="invalid-feedback">{{ getErrorMessage('username') }}</div>
            }
            @if (isEditMode()) {
              <small class="form-hint">Username cannot be changed after creation.</small>
            }
          </div>

          <!-- Email with email validation (MIGRATION: Security_EmailValidation setting lines 408-412) -->
          <div class="form-group">
            <label for="email" class="form-label">
              Email <span class="required">*</span>
            </label>
            <input
              id="email"
              type="email"
              formControlName="email"
              class="form-control"
              [class.is-invalid]="isFieldInvalid('email')"
              autocomplete="email"
              placeholder="Enter email address"
            />
            @if (isFieldInvalid('email')) {
              <div class="invalid-feedback">{{ getErrorMessage('email') }}</div>
            }
          </div>

          <!-- FirstName (MaxLength 50 per UserInfo.vb) -->
          <div class="form-group">
            <label for="firstName" class="form-label">
              First Name <span class="required">*</span>
            </label>
            <input
              id="firstName"
              type="text"
              formControlName="firstName"
              class="form-control"
              [class.is-invalid]="isFieldInvalid('firstName')"
              autocomplete="given-name"
              placeholder="Enter first name"
            />
            @if (isFieldInvalid('firstName')) {
              <div class="invalid-feedback">{{ getErrorMessage('firstName') }}</div>
            }
          </div>

          <!-- LastName (MaxLength 50 per UserInfo.vb) -->
          <div class="form-group">
            <label for="lastName" class="form-label">
              Last Name <span class="required">*</span>
            </label>
            <input
              id="lastName"
              type="text"
              formControlName="lastName"
              class="form-control"
              [class.is-invalid]="isFieldInvalid('lastName')"
              autocomplete="family-name"
              placeholder="Enter last name"
            />
            @if (isFieldInvalid('lastName')) {
              <div class="invalid-feedback">{{ getErrorMessage('lastName') }}</div>
            }
          </div>

          <!-- DisplayName (MaxLength 128, may be auto-generated per Security_DisplayNameFormat setting) -->
          <div class="form-group">
            <label for="displayName" class="form-label">
              Display Name <span class="required">*</span>
            </label>
            <input
              id="displayName"
              type="text"
              formControlName="displayName"
              class="form-control"
              [class.is-invalid]="isFieldInvalid('displayName')"
              autocomplete="nickname"
              placeholder="Enter display name"
            />
            @if (isFieldInvalid('displayName')) {
              <div class="invalid-feedback">{{ getErrorMessage('displayName') }}</div>
            }
            <small class="form-hint">This name will be shown publicly.</small>
          </div>

          @if (!isEditMode()) {
            <!-- Password fields only shown for new users (MIGRATION: AddUser check line 276) -->
            <div class="form-group">
              <label for="password" class="form-label">
                Password <span class="required">*</span>
              </label>
              <input
                id="password"
                type="password"
                formControlName="password"
                class="form-control"
                [class.is-invalid]="isFieldInvalid('password') || hasPasswordMismatch()"
                autocomplete="new-password"
                placeholder="Enter password"
              />
              @if (isFieldInvalid('password')) {
                <div class="invalid-feedback">{{ getErrorMessage('password') }}</div>
              }
              <small class="form-hint">
                Password must be at least 8 characters with uppercase, lowercase, and numbers.
              </small>
            </div>

            <div class="form-group">
              <label for="confirmPassword" class="form-label">
                Confirm Password <span class="required">*</span>
              </label>
              <input
                id="confirmPassword"
                type="password"
                formControlName="confirmPassword"
                class="form-control"
                [class.is-invalid]="isFieldInvalid('confirmPassword') || hasPasswordMismatch()"
                autocomplete="new-password"
                placeholder="Confirm password"
              />
              @if (hasPasswordMismatch()) {
                <div class="invalid-feedback">Passwords do not match.</div>
              } @else if (isFieldInvalid('confirmPassword')) {
                <div class="invalid-feedback">{{ getErrorMessage('confirmPassword') }}</div>
              }
            </div>

            <!-- isAuthorized checkbox (MIGRATION: chkAuthorize.Checked line 223) -->
            <div class="form-group form-group-checkbox">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  formControlName="isAuthorized"
                  class="form-checkbox"
                />
                <span class="checkbox-text">Authorize user immediately</span>
              </label>
              <small class="form-hint">
                If unchecked, the user will need to be approved by an administrator before logging in.
              </small>
            </div>
          }

          <!-- Form Actions -->
          <div class="form-actions">
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="saving()"
            >
              @if (saving()) {
                <span class="btn-spinner"></span>
                <span>{{ isEditMode() ? 'Updating...' : 'Creating...' }}</span>
              } @else {
                <span>{{ isEditMode() ? 'Update User' : 'Create User' }}</span>
              }
            </button>

            @if (isEditMode()) {
              <button
                type="button"
                class="btn btn-danger"
                (click)="onDelete()"
                [disabled]="saving()"
              >
                Delete User
              </button>
            }

            <button
              type="button"
              class="btn btn-secondary"
              (click)="onCancel()"
              [disabled]="saving()"
            >
              Cancel
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [
    `
      /* Container Styles */
      .user-form-container {
        max-width: 600px;
        margin: 0 auto;
        padding: 24px;
        background-color: #fff;
        border-radius: 8px;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
      }

      .form-title {
        margin: 0 0 24px 0;
        font-size: 1.5rem;
        font-weight: 600;
        color: #1a1a1a;
        border-bottom: 2px solid #e5e7eb;
        padding-bottom: 12px;
      }

      .loading-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 300px;
      }

      /* Alert Styles */
      .alert {
        display: flex;
        align-items: flex-start;
        gap: 12px;
        padding: 12px 16px;
        margin-bottom: 20px;
        border-radius: 6px;
        font-size: 0.875rem;
      }

      .alert-error {
        background-color: #fef2f2;
        border: 1px solid #fecaca;
        color: #b91c1c;
      }

      .alert-success {
        background-color: #f0fdf4;
        border: 1px solid #bbf7d0;
        color: #15803d;
      }

      .alert-icon {
        font-size: 1rem;
        flex-shrink: 0;
      }

      .alert-message {
        flex: 1;
      }

      .alert-dismiss {
        background: none;
        border: none;
        cursor: pointer;
        font-size: 1.25rem;
        line-height: 1;
        color: inherit;
        opacity: 0.7;
        padding: 0;
        margin-left: auto;
      }

      .alert-dismiss:hover {
        opacity: 1;
      }

      /* Form Styles */
      .user-form {
        display: flex;
        flex-direction: column;
        gap: 20px;
      }

      .form-group {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }

      .form-group-checkbox {
        margin-top: 4px;
      }

      .form-label {
        font-size: 0.875rem;
        font-weight: 500;
        color: #374151;
      }

      .required {
        color: #dc2626;
      }

      .form-control {
        padding: 10px 12px;
        font-size: 0.9375rem;
        border: 1px solid #d1d5db;
        border-radius: 6px;
        transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
        outline: none;
        width: 100%;
        box-sizing: border-box;
      }

      .form-control:focus {
        border-color: #3b82f6;
        box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
      }

      .form-control.is-invalid {
        border-color: #dc2626;
      }

      .form-control.is-invalid:focus {
        box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.15);
      }

      .form-control.readonly {
        background-color: #f3f4f6;
        cursor: not-allowed;
        color: #6b7280;
      }

      .form-hint {
        font-size: 0.75rem;
        color: #6b7280;
        margin-top: 2px;
      }

      .invalid-feedback {
        font-size: 0.75rem;
        color: #dc2626;
        margin-top: 2px;
      }

      /* Checkbox Styles */
      .checkbox-label {
        display: flex;
        align-items: center;
        gap: 8px;
        cursor: pointer;
        font-size: 0.9375rem;
        color: #374151;
      }

      .form-checkbox {
        width: 18px;
        height: 18px;
        cursor: pointer;
        accent-color: #3b82f6;
      }

      .checkbox-text {
        user-select: none;
      }

      /* Form Actions */
      .form-actions {
        display: flex;
        gap: 12px;
        margin-top: 12px;
        padding-top: 20px;
        border-top: 1px solid #e5e7eb;
        flex-wrap: wrap;
      }

      /* Button Styles */
      .btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        padding: 10px 20px;
        font-size: 0.9375rem;
        font-weight: 500;
        border: none;
        border-radius: 6px;
        cursor: pointer;
        transition: background-color 0.15s ease-in-out, opacity 0.15s ease-in-out;
        text-decoration: none;
      }

      .btn:disabled {
        cursor: not-allowed;
        opacity: 0.6;
      }

      .btn-primary {
        background-color: #3b82f6;
        color: #fff;
      }

      .btn-primary:hover:not(:disabled) {
        background-color: #2563eb;
      }

      .btn-secondary {
        background-color: #6b7280;
        color: #fff;
      }

      .btn-secondary:hover:not(:disabled) {
        background-color: #4b5563;
      }

      .btn-danger {
        background-color: #dc2626;
        color: #fff;
      }

      .btn-danger:hover:not(:disabled) {
        background-color: #b91c1c;
      }

      /* Button Spinner */
      .btn-spinner {
        width: 16px;
        height: 16px;
        border: 2px solid rgba(255, 255, 255, 0.3);
        border-top-color: #fff;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
      }

      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }

      /* Responsive Design */
      @media (max-width: 640px) {
        .user-form-container {
          padding: 16px;
          margin: 12px;
          border-radius: 6px;
        }

        .form-actions {
          flex-direction: column;
        }

        .btn {
          width: 100%;
        }
      }
    `,
  ],
})
export class UserFormComponent implements OnInit {
  // ==========================================================================
  // DEPENDENCY INJECTION - Using Angular 19 inject() function
  // ==========================================================================

  /**
   * UserService instance for CRUD operations.
   * MIGRATION: Replaces DNN UserController.vb business logic calls
   */
  private readonly userService = inject(UserService);

  /**
   * ActivatedRoute for accessing route parameters.
   * MIGRATION: Replaces DNN NavigateURL and Request.QueryString patterns
   */
  private readonly route = inject(ActivatedRoute);

  /**
   * Router for programmatic navigation.
   * MIGRATION: Replaces DNN Response.Redirect calls
   */
  private readonly router = inject(Router);

  /**
   * DestroyRef for automatic subscription cleanup.
   * Used with takeUntilDestroyed() for memory leak prevention.
   */
  private readonly destroyRef = inject(DestroyRef);

  // ==========================================================================
  // STATE MANAGEMENT - Using Angular Signals
  // ==========================================================================

  /**
   * Signal holding the current user data for edit mode.
   * MIGRATION: Replaces VB.NET User property from UserModuleBase
   */
  readonly user = signal<User | null>(null);

  /**
   * Signal indicating initial data loading state.
   * MIGRATION: Replaces implicit WebForms page loading state
   */
  readonly loading = signal<boolean>(true);

  /**
   * Signal indicating form submission in progress.
   * MIGRATION: Replaces cmdUpdate.Enabled = False pattern
   */
  readonly saving = signal<boolean>(false);

  /**
   * Signal indicating whether the form is in edit mode.
   * MIGRATION: Replaces VB.NET AddUser property check (line 276)
   */
  readonly isEditMode = signal<boolean>(false);

  /**
   * Signal holding error message for display.
   * MIGRATION: Replaces DNN valPassword.ErrorMessage and error label patterns
   */
  readonly errorMessage = signal<string | null>(null);

  /**
   * Signal holding success message for display.
   * MIGRATION: Replaces DNN skin message patterns
   */
  readonly successMessage = signal<string | null>(null);

  // ==========================================================================
  // REACTIVE FORM DEFINITION
  // ==========================================================================

  /**
   * Typed reactive form for user input.
   *
   * MIGRATION: Replaces DNN UserEditor PropertyEditor control:
   * - Username: SortOrder(0), IsReadOnly(True), Required(True) per UserInfo.vb line 301
   * - Email: SortOrder(4), MaxLength(256), Required(True) per UserInfo.vb line 121-122
   * - FirstName: SortOrder(1), MaxLength(50), Required(True) per UserInfo.vb line 144
   * - LastName: SortOrder(2), MaxLength(50), Required(True) per UserInfo.vb line 178
   * - DisplayName: SortOrder(3), Required(True), MaxLength(128) per UserInfo.vb line 104
   * - Password/Confirm: From User.ascx.vb txtPassword/txtConfirm controls
   * - isAuthorized: From User.ascx.vb chkAuthorize checkbox (line 223)
   */
  readonly userForm = new FormGroup<UserFormControls>(
    {
      username: new FormControl<string>('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.minLength(4),
          Validators.maxLength(128),
          // MIGRATION: Username pattern validation - alphanumeric with underscore/hyphen
          Validators.pattern(/^[a-zA-Z][a-zA-Z0-9_-]*$/),
        ],
      }),
      email: new FormControl<string>('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.email,
          Validators.maxLength(256),
        ],
      }),
      firstName: new FormControl<string>('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.maxLength(50),
        ],
      }),
      lastName: new FormControl<string>('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.maxLength(50),
        ],
      }),
      displayName: new FormControl<string>('', {
        nonNullable: true,
        validators: [
          Validators.required,
          Validators.maxLength(128),
        ],
      }),
      password: new FormControl<string>('', {
        nonNullable: true,
        // Validators set dynamically based on mode
      }),
      confirmPassword: new FormControl<string>('', {
        nonNullable: true,
        // Validators set dynamically based on mode
      }),
      isAuthorized: new FormControl<boolean>(false, {
        nonNullable: true,
      }),
    },
    {
      // MIGRATION: VB txtPassword.Text <> txtConfirm.Text check (lines 151-154)
      validators: [this.passwordMatchValidator()],
    }
  );

  // ==========================================================================
  // LIFECYCLE HOOKS
  // ==========================================================================

  /**
   * Component initialization.
   *
   * MIGRATION: Replaces VB.NET Page_Load and Page_Init handlers (lines 314-332).
   * Determines create/edit mode from route parameters and loads user data if editing.
   */
  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const idParam = params.get('id');

        if (idParam && idParam !== 'new') {
          // Edit mode - parse user ID and load user data
          const userId = parseInt(idParam, 10);
          if (!isNaN(userId)) {
            this.isEditMode.set(true);
            this.setupEditModeValidation();
            this.loadUser(userId);
          } else {
            // Invalid ID parameter - show error
            this.errorMessage.set('Invalid user ID provided.');
            this.loading.set(false);
          }
        } else {
          // Create mode - new user
          this.isEditMode.set(false);
          this.setupCreateModeValidation();
          this.loading.set(false);
        }
      });
  }

  // ==========================================================================
  // PUBLIC METHODS - Form Actions
  // ==========================================================================

  /**
   * Handle form submission for create or update operations.
   *
   * MIGRATION: Replaces VB.NET cmdUpdate_Click handler (lines 361-384):
   * ```vb
   * Private Sub cmdUpdate_Click(...) Handles cmdUpdate.Click
   *     If AddUser Then
   *         If IsValid Then CreateUser()
   *     Else
   *         If UserEditor.IsValid AndAlso UserEditor.IsDirty...
   *             UserController.UpdateUser(UserPortalID, User)
   *     End If
   * End Sub
   * ```
   */
  onSubmit(): void {
    // Clear previous messages
    this.errorMessage.set(null);
    this.successMessage.set(null);

    // Validate form
    if (this.userForm.invalid) {
      // Mark all fields as touched to show validation errors
      this.userForm.markAllAsTouched();
      this.errorMessage.set('Please correct the validation errors before submitting.');
      return;
    }

    // Check password match for create mode
    if (!this.isEditMode() && this.hasPasswordMismatch()) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    // Set saving state
    this.saving.set(true);

    // Get form values
    const formData = this.userForm.getRawValue();

    if (this.isEditMode()) {
      // MIGRATION: VB.NET UserController.UpdateUser(UserPortalID, User) (line 376)
      const currentUser = this.user();
      if (!currentUser) {
        this.errorMessage.set('No user data available for update.');
        this.saving.set(false);
        return;
      }

      // Build update request - only include changed fields
      const updateRequest = {
        displayName: formData.displayName,
        email: formData.email,
        firstName: formData.firstName,
        lastName: formData.lastName,
      };

      this.userService
        .updateUser(currentUser.userId, updateRequest)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (updatedUser) => {
            this.user.set(updatedUser);
            this.successMessage.set('User updated successfully.');
            this.saving.set(false);
            // Navigate to user list after short delay
            setTimeout(() => {
              this.router.navigate(['/users']);
            }, 1500);
          },
          error: (error) => {
            this.errorMessage.set(
              error?.error?.message || error?.message || 'Failed to update user. Please try again.'
            );
            this.saving.set(false);
          },
        });
    } else {
      // MIGRATION: VB.NET CreateUser method (lines 209-238)
      const createRequest = {
        username: formData.username,
        email: formData.email,
        firstName: formData.firstName,
        lastName: formData.lastName,
        displayName: formData.displayName,
        password: formData.password,
        approved: formData.isAuthorized,
      };

      this.userService
        .createUser(createRequest)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.successMessage.set('User created successfully.');
            this.saving.set(false);
            // Navigate to user list after short delay
            setTimeout(() => {
              this.router.navigate(['/users']);
            }, 1500);
          },
          error: (error) => {
            // MIGRATION: VB.NET UserController.GetUserCreateStatus error messages
            const errorMsg = this.parseCreateError(error);
            this.errorMessage.set(errorMsg);
            this.saving.set(false);
          },
        });
    }
  }

  /**
   * Handle user deletion.
   *
   * MIGRATION: Replaces VB.NET cmdDelete_Click handler (lines 342-350):
   * ```vb
   * Private Sub cmdDelete_Click(...) Handles cmdDelete.Click
   *     If UserController.DeleteUser(User, True, False) Then
   *         OnUserDeleted(New UserDeletedEventArgs(id, name))
   *     Else
   *         OnUserDeleteError(...)
   *     End If
   * End Sub
   * ```
   */
  onDelete(): void {
    const currentUser = this.user();
    if (!currentUser) {
      this.errorMessage.set('No user data available for deletion.');
      return;
    }

    // MIGRATION: VB.NET ClientAPI.AddButtonConfirm(cmdDelete, confirmString) (line 260)
    const confirmMessage = `Are you sure you want to delete the user "${currentUser.displayName}"? This action cannot be undone.`;
    if (!window.confirm(confirmMessage)) {
      return;
    }

    // Clear previous messages
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.saving.set(true);

    this.userService
      .deleteUser(currentUser.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          // MIGRATION: VB.NET OnUserDeleted event - navigate to list with success message
          this.successMessage.set('User deleted successfully.');
          this.saving.set(false);
          // Navigate to user list
          setTimeout(() => {
            this.router.navigate(['/users']);
          }, 1000);
        },
        error: (error) => {
          // MIGRATION: VB.NET OnUserDeleteError event
          this.errorMessage.set(
            error?.error?.message || error?.message || 'Failed to delete user. Please try again.'
          );
          this.saving.set(false);
        },
      });
  }

  /**
   * Handle cancel button click - navigate back to user list.
   *
   * MIGRATION: Replaces DNN NavigateURL patterns for returning to list view.
   */
  onCancel(): void {
    this.router.navigate(['/users']);
  }

  /**
   * Get validation error message for a specific form control.
   *
   * MIGRATION: Replaces DNN Localization.GetString patterns for error messages
   * and the ErrorMessage properties on validation controls.
   *
   * @param controlName - Name of the form control to get error message for
   * @returns Human-readable error message string
   */
  getErrorMessage(controlName: string): string {
    const control = this.userForm.get(controlName);
    if (!control || !control.errors) {
      return '';
    }

    const errors = control.errors;

    // Handle different validation error types
    if (errors['required']) {
      return this.getFieldLabel(controlName) + ' is required.';
    }
    if (errors['email']) {
      return 'Please enter a valid email address.';
    }
    if (errors['minlength']) {
      const minLength = errors['minlength'].requiredLength;
      return `${this.getFieldLabel(controlName)} must be at least ${minLength} characters.`;
    }
    if (errors['maxlength']) {
      const maxLength = errors['maxlength'].requiredLength;
      return `${this.getFieldLabel(controlName)} cannot exceed ${maxLength} characters.`;
    }
    if (errors['pattern']) {
      if (controlName === 'username') {
        return 'Username must start with a letter and contain only letters, numbers, underscores, or hyphens.';
      }
      if (controlName === 'password') {
        return 'Password must contain at least one uppercase letter, one lowercase letter, and one number.';
      }
      return `${this.getFieldLabel(controlName)} format is invalid.`;
    }

    return 'Invalid value.';
  }

  // ==========================================================================
  // TEMPLATE HELPER METHODS
  // ==========================================================================

  /**
   * Check if a form field is invalid and has been touched or dirty.
   *
   * @param controlName - Name of the form control to check
   * @returns true if the field should display validation errors
   */
  isFieldInvalid(controlName: string): boolean {
    const control = this.userForm.get(controlName);
    return control ? control.invalid && (control.dirty || control.touched) : false;
  }

  /**
   * Check if password and confirm password fields don't match.
   *
   * MIGRATION: Replaces VB.NET password comparison logic (lines 151-154):
   * ```vb
   * If txtPassword.Text <> txtConfirm.Text Then
   *     createStatus = UserCreateStatus.PasswordMismatch
   * End If
   * ```
   *
   * @returns true if passwords don't match and both fields have values
   */
  hasPasswordMismatch(): boolean {
    if (this.isEditMode()) {
      return false;
    }

    const password = this.userForm.get('password')?.value;
    const confirmPassword = this.userForm.get('confirmPassword')?.value;

    // Only show mismatch error if both fields have values
    if (!password || !confirmPassword) {
      return false;
    }

    return password !== confirmPassword;
  }

  // ==========================================================================
  // PRIVATE METHODS
  // ==========================================================================

  /**
   * Load user data for edit mode.
   *
   * MIGRATION: Replaces VB.NET Page_Load data binding (lines 248-297):
   * ```vb
   * Public Overrides Sub DataBind()
   *     UserEditor.DataSource = User
   *     UserEditor.DataBind()
   * End Sub
   * ```
   *
   * @param id - User ID to load
   */
  private loadUser(id: number): void {
    this.userService
      .getUserById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (user) => {
          this.user.set(user);
          this.patchFormWithUserData(user);
          this.loading.set(false);
        },
        error: (error) => {
          this.errorMessage.set(
            error?.error?.message || error?.message || 'Failed to load user data.'
          );
          this.loading.set(false);
        },
      });
  }

  /**
   * Patch form controls with loaded user data.
   *
   * @param user - User data to populate form with
   */
  private patchFormWithUserData(user: User): void {
    this.userForm.patchValue({
      username: user.username,
      email: user.email,
      firstName: user.firstName,
      lastName: user.lastName,
      displayName: user.displayName,
    });
  }

  /**
   * Setup form validation for create mode.
   * Adds required validators to password fields.
   */
  private setupCreateModeValidation(): void {
    const passwordControl = this.userForm.get('password');
    const confirmPasswordControl = this.userForm.get('confirmPassword');

    if (passwordControl) {
      passwordControl.setValidators([
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(128),
        // MIGRATION: VB.NET UserController.ValidatePassword (line 156)
        // Password policy: at least one uppercase, one lowercase, one number
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/),
      ]);
      passwordControl.updateValueAndValidity();
    }

    if (confirmPasswordControl) {
      confirmPasswordControl.setValidators([
        Validators.required,
      ]);
      confirmPasswordControl.updateValueAndValidity();
    }
  }

  /**
   * Setup form validation for edit mode.
   * Removes validators from password fields as they're not shown.
   */
  private setupEditModeValidation(): void {
    const passwordControl = this.userForm.get('password');
    const confirmPasswordControl = this.userForm.get('confirmPassword');

    if (passwordControl) {
      passwordControl.clearValidators();
      passwordControl.updateValueAndValidity();
    }

    if (confirmPasswordControl) {
      confirmPasswordControl.clearValidators();
      confirmPasswordControl.updateValueAndValidity();
    }
  }

  /**
   * Custom validator for password match validation.
   *
   * MIGRATION: Replaces VB.NET password comparison (lines 151-154):
   * ```vb
   * '1. Check Password and Confirm are the same
   * If txtPassword.Text <> txtConfirm.Text Then
   *     createStatus = UserCreateStatus.PasswordMismatch
   * End If
   * ```
   *
   * @returns ValidatorFn for cross-field validation
   */
  private passwordMatchValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const formGroup = control as FormGroup;
      const password = formGroup.get('password');
      const confirmPassword = formGroup.get('confirmPassword');

      // Skip validation if in edit mode or if passwords are empty
      if (!password?.value || !confirmPassword?.value) {
        return null;
      }

      if (password.value !== confirmPassword.value) {
        return { passwordMismatch: true };
      }

      return null;
    };
  }

  /**
   * Get human-readable label for a form field.
   *
   * @param controlName - Name of the form control
   * @returns Human-readable field label
   */
  private getFieldLabel(controlName: string): string {
    const labels: Record<string, string> = {
      username: 'Username',
      email: 'Email',
      firstName: 'First Name',
      lastName: 'Last Name',
      displayName: 'Display Name',
      password: 'Password',
      confirmPassword: 'Confirm Password',
    };
    return labels[controlName] || controlName;
  }

  /**
   * Parse error response from user creation API.
   *
   * MIGRATION: Maps backend error codes to user-friendly messages,
   * similar to VB.NET UserController.GetUserCreateStatus() method.
   *
   * @param error - Error response from API
   * @returns User-friendly error message
   */
  private parseCreateError(error: unknown): string {
    // Default error message
    const defaultMessage = 'Failed to create user. Please try again.';

    if (!error || typeof error !== 'object') {
      return defaultMessage;
    }

    const err = error as Record<string, unknown>;

    // Check for structured error response
    if (err['error'] && typeof err['error'] === 'object') {
      const errorBody = err['error'] as Record<string, unknown>;

      // Check for specific error codes (MIGRATION: UserCreateStatus enum values)
      if (errorBody['code']) {
        switch (errorBody['code']) {
          case 'DUPLICATE_USERNAME':
            return 'A user with this username already exists.';
          case 'DUPLICATE_EMAIL':
            return 'A user with this email address already exists.';
          case 'INVALID_PASSWORD':
            return 'The password does not meet the security requirements.';
          case 'PASSWORD_MISMATCH':
            return 'The passwords do not match.';
          case 'INVALID_EMAIL':
            return 'The email address format is invalid.';
          default:
            break;
        }
      }

      // Check for message property
      if (typeof errorBody['message'] === 'string') {
        return errorBody['message'];
      }
    }

    // Check for direct message property
    if (typeof err['message'] === 'string') {
      return err['message'];
    }

    return defaultMessage;
  }
}
