/**
 * Portal Form Component
 *
 * Angular 19 standalone portal form component for creating and editing portals.
 * Implements typed reactive forms with validation matching legacy VB rules,
 * signals for state management, and supports both create (new) and edit
 * (existing) modes via route parameters.
 *
 * MIGRATION: Replaces VB.NET Signup.ascx.vb and SiteSettings.ascx.vb
 * - VB ViewState replaced by signals
 * - VB Response.Redirect replaced by Router.navigate()
 * - VB ProcessModuleLoadException replaced by error signal and toast/message display
 * - VB Null.NullInteger checks converted to null/undefined checks
 * - VB InStr validation converted to regex patterns
 *
 * Source files:
 * - Website/admin/Portal/Signup.ascx.vb (portal creation wizard)
 * - Website/admin/Portal/SiteSettings.ascx.vb (portal editing)
 * - Library/Components/Portal/PortalInfo.vb (entity reference)
 *
 * @fileoverview Portal form component supporting create and edit operations
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  OnInit,
  computed,
  DestroyRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators,
  AbstractControl,
  ValidationErrors,
  AsyncValidatorFn,
} from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import {
  Observable,
  of,
  map,
  catchError,
  debounceTime,
  switchMap,
  take,
  firstValueFrom,
} from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { PortalService } from '../../services/portal.service';
import {
  Portal,
  CreatePortalRequest,
  UpdatePortalRequest,
  PortalTemplate,
} from '../../models/portal.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import {
  TextInputComponent,
  SelectComponent,
  TextareaComponent,
  SelectOption,
} from '../../../../shared/components/form-controls/form-controls.component';

// ============================================================================
// INTERFACES
// ============================================================================

/**
 * Interface defining the typed structure of the portal form values.
 *
 * MIGRATION: Form fields mapped from VB.NET controls:
 * - portalName/alias (txtPortalName from Signup.ascx.vb line 183-217)
 * - firstName, lastName, username, password, email - admin user details
 * - title, description, keywords - portal metadata from Signup.ascx.vb line 274
 * - template (cboTemplate from Signup.ascx.vb line 87-96)
 * - homeDirectory (txtHomeDirectory from Signup.ascx.vb line 109-110, 244-257)
 * - portalType ('P' for Parent, 'C' for Child from Signup.ascx.vb line 101, 198-199)
 */
export interface PortalFormValue {
  /** Portal name/alias (becomes the domain or subdirectory) */
  portalName: string;

  /** HTTP alias for the portal (derived from portalName based on type) */
  alias: string;

  /** Administrator's first name */
  firstName: string;

  /** Administrator's last name */
  lastName: string;

  /** Administrator's username for login */
  username: string;

  /** Administrator's password */
  password: string;

  /** Password confirmation field */
  confirmPassword: string;

  /** Administrator's email address */
  email: string;

  /** Portal title/display name */
  title: string;

  /** Portal description for SEO and display */
  description: string;

  /** SEO keywords for the portal */
  keywords: string;

  /** Selected template file name */
  template: string;

  /** Home directory path (default: Portals/[PortalID]) */
  homeDirectory: string;

  /** Portal type - 'P' for Parent (root), 'C' for Child (subdirectory) */
  portalType: 'P' | 'C';
}

// ============================================================================
// COMPONENT
// ============================================================================

/**
 * Portal Form Component
 *
 * Angular 19 standalone component for creating and editing portals.
 * Uses typed reactive forms, signal-based state management, and
 * OnPush change detection for optimal performance.
 *
 * Features:
 * - Create mode: New portal creation with admin user setup
 * - Edit mode: Existing portal configuration modification
 * - Validation matching legacy VB rules for portal aliases
 * - Async validation for duplicate alias checking
 * - Template selection for new portal setup
 *
 * MIGRATION: Replaces the following DNN components:
 * - Signup.ascx.vb - Portal creation wizard
 * - SiteSettings.ascx.vb - Portal configuration editing
 */
@Component({
  selector: 'app-portal-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent,
    TextInputComponent,
    SelectComponent,
    TextareaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Loading Overlay -->
    @if (loading() || saving()) {
      <app-loading-spinner
        [overlay]="true"
        [message]="saving() ? 'Saving portal...' : 'Loading...'" />
    }

    <!-- Error Message Display -->
    @if (error()) {
      <div class="error-banner" role="alert">
        <span class="error-icon">⚠</span>
        <span class="error-message">{{ error() }}</span>
        <button
          type="button"
          class="error-dismiss"
          (click)="clearError()"
          aria-label="Dismiss error">
          ×
        </button>
      </div>
    }

    <!-- Main Form Container -->
    <div class="portal-form-container">
      <h2 class="form-title">
        {{ isEditMode() ? 'Edit Portal' : 'Create New Portal' }}
      </h2>

      <form
        [formGroup]="portalForm"
        (ngSubmit)="onSubmit()"
        class="portal-form">

        <!-- Portal Configuration Section -->
        <fieldset class="form-section">
          <legend class="section-legend">Portal Configuration</legend>

          <!-- Portal Type Selection (Create mode only) -->
          @if (!isEditMode()) {
            <div class="form-row">
              <app-select
                label="Portal Type"
                [options]="portalTypeOptions"
                [required]="true"
                formControlName="portalType"
                [errorMessage]="getErrorMessage('portalType')" />
            </div>
          }

          <!-- Portal Name/Alias -->
          <div class="form-row">
            <app-text-input
              [label]="isEditMode() ? 'Portal Name' : 'Portal Name/Alias'"
              [placeholder]="getPortalNamePlaceholder()"
              [required]="true"
              formControlName="portalName"
              [errorMessage]="getErrorMessage('portalName')" />
          </div>

          <!-- Portal Title (Create mode) -->
          @if (!isEditMode()) {
            <div class="form-row">
              <app-text-input
                label="Portal Title"
                placeholder="Enter the display title for the portal"
                [required]="true"
                formControlName="title"
                [errorMessage]="getErrorMessage('title')" />
            </div>
          }

          <!-- Portal Description -->
          <div class="form-row">
            <app-textarea
              label="Description"
              placeholder="Enter a description for the portal"
              [rows]="4"
              formControlName="description"
              [errorMessage]="getErrorMessage('description')" />
          </div>

          <!-- Keywords -->
          <div class="form-row">
            <app-text-input
              label="Keywords"
              placeholder="Enter SEO keywords (comma-separated)"
              formControlName="keywords"
              [errorMessage]="getErrorMessage('keywords')" />
          </div>
        </fieldset>

        <!-- Administrator Account Section (Create mode only) -->
        @if (!isEditMode()) {
          <fieldset class="form-section">
            <legend class="section-legend">Administrator Account</legend>

            <div class="form-row form-row-2col">
              <app-text-input
                label="First Name"
                placeholder="Administrator's first name"
                [required]="true"
                formControlName="firstName"
                [errorMessage]="getErrorMessage('firstName')" />

              <app-text-input
                label="Last Name"
                placeholder="Administrator's last name"
                [required]="true"
                formControlName="lastName"
                [errorMessage]="getErrorMessage('lastName')" />
            </div>

            <div class="form-row">
              <app-text-input
                label="Username"
                placeholder="Administrator login username"
                [required]="true"
                formControlName="username"
                [errorMessage]="getErrorMessage('username')" />
            </div>

            <div class="form-row form-row-2col">
              <app-text-input
                label="Password"
                placeholder="Enter password"
                type="password"
                [required]="true"
                formControlName="password"
                [errorMessage]="getErrorMessage('password')" />

              <app-text-input
                label="Confirm Password"
                placeholder="Confirm password"
                type="password"
                [required]="true"
                formControlName="confirmPassword"
                [errorMessage]="getErrorMessage('confirmPassword')" />
            </div>

            <div class="form-row">
              <app-text-input
                label="Email"
                placeholder="Administrator's email address"
                type="email"
                [required]="true"
                formControlName="email"
                [errorMessage]="getErrorMessage('email')" />
            </div>
          </fieldset>
        }

        <!-- Template and Directory Section (Create mode only) -->
        @if (!isEditMode()) {
          <fieldset class="form-section">
            <legend class="section-legend">Template & Directory</legend>

            <div class="form-row">
              <app-select
                label="Portal Template"
                placeholder="Select a template"
                [options]="templateOptions()"
                formControlName="template"
                [errorMessage]="getErrorMessage('template')" />
              @if (selectedTemplateDescription()) {
                <p class="template-description">
                  {{ selectedTemplateDescription() }}
                </p>
              }
            </div>

            <div class="form-row home-directory-row">
              <app-text-input
                label="Home Directory"
                placeholder="Portals/[PortalID]"
                formControlName="homeDirectory"
                [errorMessage]="getErrorMessage('homeDirectory')" />
              <button
                type="button"
                class="btn btn-secondary btn-customize"
                (click)="onCustomizeHomeDir()">
                {{ isHomeDirectoryCustomized() ? 'Auto-Generate' : 'Customize' }}
              </button>
            </div>
          </fieldset>
        }

        <!-- Form Actions -->
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
            [disabled]="portalForm.invalid || saving()">
            {{ isEditMode() ? 'Save Changes' : 'Create Portal' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [
    `
      /* Container Styles */
      .portal-form-container {
        max-width: 800px;
        margin: 0 auto;
        padding: 24px;
      }

      .form-title {
        font-size: 1.5rem;
        font-weight: 600;
        color: #1f2937;
        margin-bottom: 24px;
      }

      /* Error Banner */
      .error-banner {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 12px 16px;
        margin-bottom: 16px;
        background-color: #fef2f2;
        border: 1px solid #fecaca;
        border-radius: 8px;
        color: #dc2626;
      }

      .error-icon {
        font-size: 1.25rem;
      }

      .error-message {
        flex: 1;
        font-size: 0.875rem;
      }

      .error-dismiss {
        background: none;
        border: none;
        font-size: 1.25rem;
        color: #dc2626;
        cursor: pointer;
        padding: 0 4px;
      }

      .error-dismiss:hover {
        color: #b91c1c;
      }

      /* Form Sections */
      .form-section {
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        padding: 20px;
        margin-bottom: 24px;
      }

      .section-legend {
        font-size: 1rem;
        font-weight: 600;
        color: #374151;
        padding: 0 8px;
      }

      /* Form Rows */
      .form-row {
        margin-bottom: 16px;
      }

      .form-row:last-child {
        margin-bottom: 0;
      }

      .form-row-2col {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
      }

      @media (max-width: 640px) {
        .form-row-2col {
          grid-template-columns: 1fr;
        }
      }

      /* Home Directory Row */
      .home-directory-row {
        display: flex;
        align-items: flex-end;
        gap: 12px;
      }

      .home-directory-row app-text-input {
        flex: 1;
      }

      .btn-customize {
        white-space: nowrap;
        margin-bottom: 1rem;
      }

      /* Template Description */
      .template-description {
        font-size: 0.875rem;
        color: #6b7280;
        margin-top: 8px;
        padding: 8px 12px;
        background-color: #f9fafb;
        border-radius: 4px;
        font-style: italic;
      }

      /* Form Actions */
      .form-actions {
        display: flex;
        justify-content: flex-end;
        gap: 12px;
        padding-top: 16px;
        border-top: 1px solid #e5e7eb;
      }

      /* Button Styles */
      .btn {
        padding: 10px 20px;
        font-size: 0.875rem;
        font-weight: 500;
        border-radius: 6px;
        cursor: pointer;
        transition: all 0.15s ease-in-out;
      }

      .btn:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }

      .btn-primary {
        background-color: #3b82f6;
        color: white;
        border: none;
      }

      .btn-primary:hover:not(:disabled) {
        background-color: #2563eb;
      }

      .btn-secondary {
        background-color: white;
        color: #374151;
        border: 1px solid #d1d5db;
      }

      .btn-secondary:hover:not(:disabled) {
        background-color: #f9fafb;
      }
    `,
  ],
})
export class PortalFormComponent implements OnInit {
  // ==========================================================================
  // DEPENDENCY INJECTION
  // ==========================================================================

  /**
   * Portal service for API operations
   * MIGRATION: Replaces VB.NET PortalController direct data access
   */
  private readonly portalService = inject(PortalService);

  /**
   * Angular Router for navigation
   * MIGRATION: Replaces VB.NET Response.Redirect()
   */
  private readonly router = inject(Router);

  /**
   * Activated route for accessing route parameters
   */
  private readonly route = inject(ActivatedRoute);

  /**
   * Form builder for creating typed reactive forms
   */
  private readonly fb = inject(FormBuilder);

  /**
   * Destroy reference for automatic subscription cleanup
   */
  private readonly destroyRef = inject(DestroyRef);

  // ==========================================================================
  // STATE SIGNALS
  // ==========================================================================

  /**
   * Loading state signal for initial data fetch
   * MIGRATION: Replaces VB.NET ViewState loading patterns
   */
  loading = signal<boolean>(false);

  /**
   * Saving state signal for form submission
   */
  saving = signal<boolean>(false);

  /**
   * Available templates for portal creation
   * MIGRATION: Replaces cboTemplate population in Signup.ascx.vb Page_Load (lines 76-96)
   */
  templates = signal<PortalTemplate[]>([]);

  /**
   * Error message signal for displaying errors
   * MIGRATION: Replaces VB.NET ProcessModuleLoadException
   */
  error = signal<string | null>(null);

  /**
   * Edit mode flag signal
   * Determined by presence of :id route parameter
   */
  isEditMode = signal<boolean>(false);

  /**
   * Current portal ID when in edit mode
   */
  private currentPortalId = signal<number | null>(null);

  /**
   * Current portal data when in edit mode
   */
  private currentPortal = signal<Portal | null>(null);

  /**
   * Domain name for child portal prefix
   */
  private domainName = signal<string>('');

  // ==========================================================================
  // FORM CONFIGURATION
  // ==========================================================================

  /**
   * Portal type selection options
   * MIGRATION: Derived from optType radio buttons in Signup.ascx.vb line 101
   */
  readonly portalTypeOptions: SelectOption[] = [
    { value: 'P', label: 'Parent Portal (Root Site)' },
    { value: 'C', label: 'Child Portal (Subdirectory)' },
  ];

  /**
   * Reactive form group with typed controls
   * MIGRATION: Form fields mapped from VB.NET controls in Signup.ascx.vb and SiteSettings.ascx.vb
   */
  portalForm!: FormGroup;

  // ==========================================================================
  // COMPUTED SIGNALS
  // ==========================================================================

  /**
   * Computed signal for template dropdown options
   */
  templateOptions = computed<SelectOption[]>(() => {
    const templateList = this.templates();
    return [
      { value: '', label: '-- Select Template --' },
      ...templateList.map((t) => ({
        value: t.fileName,
        label: t.name,
      })),
    ];
  });

  /**
   * Computed signal for selected template description
   */
  selectedTemplateDescription = computed<string>(() => {
    const selectedTemplate = this.portalForm?.get('template')?.value;
    if (!selectedTemplate) return '';

    const template = this.templates().find(
      (t) => t.fileName === selectedTemplate
    );
    return template?.description || '';
  });

  /**
   * Checks if home directory has been customized from the default auto-generated value.
   * MIGRATION: Derived from Signup.ascx.vb home directory customization logic.
   * 
   * Note: This is a method rather than a computed signal because Angular's computed()
   * only tracks other signals. FormControl values are not signals, so a computed signal
   * would cache the first result and never update when the form value changes.
   * 
   * @returns true if the home directory has a custom value, false otherwise
   */
  isHomeDirectoryCustomized(): boolean {
    const homeDir = this.portalForm?.get('homeDirectory')?.value;
    return homeDir !== '' && homeDir !== 'Portals/[PortalID]';
  }

  // ==========================================================================
  // LIFECYCLE HOOKS
  // ==========================================================================

  /**
   * Initializes the component on load
   * Checks route params to determine edit vs create mode
   * MIGRATION: Replaces Page_Load event handler logic
   */
  ngOnInit(): void {
    this.initializeForm();
    this.setupFormListeners();
    this.loadInitialData();
  }

  // ==========================================================================
  // INITIALIZATION METHODS
  // ==========================================================================

  /**
   * Initializes the reactive form with typed controls and validators
   * MIGRATION: Form controls mapped from VB.NET ASCX controls
   */
  private initializeForm(): void {
    this.portalForm = this.fb.group(
      {
        portalName: [
          '',
          [Validators.required, this.portalNameValidator()],
          [this.aliasExistsValidator()],
        ],
        alias: [''],
        firstName: ['', [Validators.required]],
        lastName: ['', [Validators.required]],
        username: ['', [Validators.required, Validators.minLength(3)]],
        password: ['', [Validators.required, Validators.minLength(6)]],
        confirmPassword: ['', [Validators.required]],
        email: ['', [Validators.required, Validators.email]],
        title: ['', [Validators.required]],
        description: [''],
        keywords: [''],
        template: [''],
        homeDirectory: [{ value: 'Portals/[PortalID]', disabled: true }],
        portalType: ['P', [Validators.required]],
      },
      {
        validators: [this.passwordMatchValidator()],
      }
    );
  }

  /**
   * Sets up form value change listeners
   * MIGRATION: Replaces VB.NET event handlers for control changes
   */
  private setupFormListeners(): void {
    // Listen for portal type changes
    // MIGRATION: Replaces optType_SelectedIndexChanged (lines 342-352)
    this.portalForm
      .get('portalType')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        this.handlePortalTypeChange(value);
      });

    // Listen for template changes to update description
    this.portalForm
      .get('template')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        // Trigger recomputation of selectedTemplateDescription
      });
  }

  /**
   * Loads initial data based on mode (create vs edit)
   */
  private async loadInitialData(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      // Determine domain name for child portal prefix
      this.domainName.set(window.location.hostname);

      // Check for portal ID in route to determine mode
      const idParam = this.route.snapshot.paramMap.get('id');
      const portalId = idParam ? parseInt(idParam, 10) : null;

      if (portalId && !isNaN(portalId)) {
        // Edit mode
        this.isEditMode.set(true);
        this.currentPortalId.set(portalId);
        await this.loadPortalForEdit(portalId);
      } else {
        // Create mode - load templates
        this.isEditMode.set(false);
        await this.loadTemplates();
      }
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to load data';
      this.error.set(errorMessage);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Loads portal data for edit mode
   * MIGRATION: Replaces SiteSettings.ascx.vb Page_Load portal loading (line 266)
   */
  private async loadPortalForEdit(portalId: number): Promise<void> {
    try {
      const portal = await firstValueFrom(this.portalService.getPortal(portalId));
      this.currentPortal.set(portal);

      // Populate form with portal data
      this.portalForm.patchValue({
        portalName: portal.portalName,
        description: portal.description || '',
        keywords: portal.keyWords || '',
      });

      // Disable admin fields in edit mode
      this.disableCreateOnlyFields();
    } catch (err) {
      throw new Error('Failed to load portal data');
    }
  }

  /**
   * Loads available templates for portal creation
   * MIGRATION: Mirrors cboTemplate population in Signup.ascx.vb Page_Load (lines 76-96)
   */
  private async loadTemplates(): Promise<void> {
    try {
      const templates = await firstValueFrom(this.portalService.getTemplates());
      this.templates.set(templates);
    } catch (err) {
      // Templates are optional, continue without them
      console.warn('Failed to load templates:', err);
      this.templates.set([]);
    }
  }

  /**
   * Disables form fields that are only relevant for create mode
   */
  private disableCreateOnlyFields(): void {
    const createOnlyFields = [
      'firstName',
      'lastName',
      'username',
      'password',
      'confirmPassword',
      'email',
      'title',
      'template',
      'homeDirectory',
      'portalType',
    ];

    createOnlyFields.forEach((field) => {
      this.portalForm.get(field)?.disable();
    });
  }

  // ==========================================================================
  // VALIDATION METHODS
  // ==========================================================================

  /**
   * Custom validator for portal name based on portal type
   *
   * MIGRATION: Implements validation from Signup.ascx.vb:
   * - Child portal (lines 191-196): only 'abcdefghijklmnopqrstuvwxyz0123456789-' allowed
   * - Parent portal (lines 207-216): 'abcdefghijklmnopqrstuvwxyz0123456789-./:' allowed
   */
  private portalNameValidator(): (
    control: AbstractControl
  ) => ValidationErrors | null {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = control.value?.toLowerCase() || '';
      const portalType = this.portalForm?.get('portalType')?.value || 'P';

      if (!value) {
        return null; // Let required validator handle this
      }

      // Remove http:// prefix if present
      // MIGRATION: From Signup.ascx.vb line 184: txtPortalName.Text = Replace(txtPortalName.Text, "http://", "")
      const cleanValue = value.replace(/^https?:\/\//i, '');

      // Define valid characters based on portal type
      // MIGRATION: From Signup.ascx.vb lines 192 and 207-209
      const childValidChars = /^[a-z0-9-]+$/;
      const parentValidChars = /^[a-z0-9.\/:_-]+$/;

      const isValid =
        portalType === 'C'
          ? childValidChars.test(cleanValue)
          : parentValidChars.test(cleanValue);

      if (!isValid) {
        const allowedChars =
          portalType === 'C'
            ? 'lowercase letters, numbers, and hyphens'
            : 'lowercase letters, numbers, periods, slashes, colons, and hyphens';
        return {
          invalidCharacters: {
            message: `Portal name can only contain ${allowedChars}`,
          },
        };
      }

      return null;
    };
  }

  /**
   * Async validator for checking duplicate portal alias
   *
   * MIGRATION: Replaces alias check from Signup.ascx.vb lines 260-265:
   * Dim PortalAlias As PortalAliasInfo = PortalSettings.GetPortalAliasLookup(strPortalAlias.ToLower)
   * If PortalAlias IsNot Nothing Then
   *     strMessage = Localization.GetString("DuplicatePortalAlias", Me.LocalResourceFile)
   * End If
   */
  private aliasExistsValidator(): AsyncValidatorFn {
    return (control: AbstractControl): Observable<ValidationErrors | null> => {
      const value = control.value?.toLowerCase() || '';

      if (!value) {
        return of(null);
      }

      // Debounce and check alias existence
      return of(value).pipe(
        debounceTime(500),
        switchMap((alias) =>
          this.portalService
            .checkAliasExists(alias, this.currentPortalId() ?? undefined)
            .pipe(
              map((exists) =>
                exists
                  ? {
                      aliasExists: {
                        message:
                          'This portal alias is already in use by another portal',
                      },
                    }
                  : null
              ),
              catchError(() => of(null))
            )
        ),
        take(1)
      );
    };
  }

  /**
   * Cross-field validator for password confirmation
   *
   * MIGRATION: From Signup.ascx.vb lines 220-222:
   * If txtPassword.Text <> txtConfirm.Text Then
   *     strMessage &= "<br>" & Localization.GetString("InvalidPassword", Me.LocalResourceFile)
   * End If
   */
  private passwordMatchValidator(): (
    group: FormGroup
  ) => ValidationErrors | null {
    return (group: FormGroup): ValidationErrors | null => {
      const password = group.get('password')?.value;
      const confirmPassword = group.get('confirmPassword')?.value;

      // Only validate in create mode
      if (this.isEditMode()) {
        return null;
      }

      if (password && confirmPassword && password !== confirmPassword) {
        // Set error on confirmPassword control for display
        group.get('confirmPassword')?.setErrors({ passwordMismatch: true });
        return { passwordMismatch: true };
      }

      return null;
    };
  }

  // ==========================================================================
  // EVENT HANDLERS
  // ==========================================================================

  /**
   * Handles portal type change (Parent vs Child)
   *
   * MIGRATION: Replaces optType_SelectedIndexChanged from Signup.ascx.vb lines 342-352:
   * If optType.SelectedValue = "C" Then
   *     txtPortalName.Text = GetDomainName(Request) & "/"
   * Else
   *     txtPortalName.Text = ""
   * End If
   */
  onPortalTypeChange(): void {
    const portalType = this.portalForm.get('portalType')?.value;
    this.handlePortalTypeChange(portalType);
  }

  /**
   * Internal handler for portal type changes
   */
  private handlePortalTypeChange(portalType: 'P' | 'C'): void {
    const portalNameControl = this.portalForm.get('portalName');

    if (portalType === 'C') {
      // Child portal - prepend domain
      const currentValue = portalNameControl?.value || '';
      if (!currentValue.includes('/')) {
        portalNameControl?.setValue(`${this.domainName()}/`);
      }
    } else {
      // Parent portal - clear prefix
      const currentValue = portalNameControl?.value || '';
      if (currentValue.startsWith(this.domainName())) {
        portalNameControl?.setValue('');
      }
    }

    // Re-validate portal name with new type
    portalNameControl?.updateValueAndValidity();
  }

  /**
   * Handles home directory customization toggle
   *
   * MIGRATION: Replaces btnCustomizeHomeDir_Click from Signup.ascx.vb lines 354-368:
   * If txtHomeDirectory.Enabled Then
   *     btnCustomizeHomeDir.Text = Localization.GetString("Customize", LocalResourceFile)
   *     txtHomeDirectory.Text = "Portals/[PortalID]"
   *     txtHomeDirectory.Enabled = False
   * Else
   *     btnCustomizeHomeDir.Text = Localization.GetString("AutoGenerate", LocalResourceFile)
   *     txtHomeDirectory.Text = ""
   *     txtHomeDirectory.Enabled = True
   * End If
   */
  onCustomizeHomeDir(): void {
    const homeDirectoryControl = this.portalForm.get('homeDirectory');

    if (homeDirectoryControl?.disabled) {
      // Enable customization
      homeDirectoryControl.enable();
      homeDirectoryControl.setValue('');
    } else {
      // Disable and reset to auto-generate
      homeDirectoryControl?.setValue('Portals/[PortalID]');
      homeDirectoryControl?.disable();
    }
  }

  /**
   * Handles form submission for create or update
   *
   * MIGRATION: Replaces cmdUpdate_Click from:
   * - Signup.ascx.vb lines 153-328 (create)
   * - SiteSettings.ascx.vb lines 687-800 (update)
   */
  async onSubmit(): Promise<void> {
    if (this.portalForm.invalid || this.saving()) {
      // Mark all controls as touched to show validation errors
      Object.keys(this.portalForm.controls).forEach((key) => {
        this.portalForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      if (this.isEditMode()) {
        await this.updatePortal();
      } else {
        await this.createPortal();
      }

      // Navigate to portal list on success
      // MIGRATION: Replaces Response.Redirect
      this.router.navigate(['/portals']);
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'An error occurred while saving';
      this.error.set(errorMessage);
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Creates a new portal
   *
   * MIGRATION: Derived from Signup.ascx.vb cmdUpdate_Click (line 274):
   * intPortalId = objPortalController.CreatePortal(txtTitle.Text, txtFirstName.Text,
   *     txtLastName.Text, txtUsername.Text, txtPassword.Text, txtEmail.Text,
   *     txtDescription.Text, txtKeyWords.Text, ..., strTemplateFile, HomeDir,
   *     strPortalAlias, strServerPath, strChildPath, blnChild)
   */
  private async createPortal(): Promise<void> {
    const formValue = this.portalForm.getRawValue() as PortalFormValue;

    // Prepare portal alias
    // MIGRATION: From Signup.ascx.vb lines 197-238
    let portalAlias = formValue.portalName.toLowerCase();
    portalAlias = portalAlias.replace(/^https?:\/\//i, '');

    // Get home directory
    // MIGRATION: From Signup.ascx.vb lines 244-249
    const homeDirectory =
      formValue.homeDirectory !== 'Portals/[PortalID]'
        ? formValue.homeDirectory
        : undefined;

    const request: CreatePortalRequest = {
      portalAlias,
      title: formValue.title,
      description: formValue.description || undefined,
      keyWords: formValue.keywords || undefined,
      firstName: formValue.firstName,
      lastName: formValue.lastName,
      username: formValue.username,
      password: formValue.password,
      email: formValue.email,
      template: formValue.template || undefined,
      homeDirectory,
      isChildPortal: formValue.portalType === 'C',
    };

    await firstValueFrom(this.portalService.createPortal(request));
  }

  /**
   * Updates an existing portal
   *
   * MIGRATION: Derived from SiteSettings.ascx.vb cmdUpdate_Click (lines 772-781):
   * objPortalController.UpdatePortalInfo(intPortalId, txtPortalName.Text, strLogo, ...)
   */
  private async updatePortal(): Promise<void> {
    const portalId = this.currentPortalId();
    if (!portalId) {
      throw new Error('Portal ID not found');
    }

    const formValue = this.portalForm.getRawValue();
    const currentPortal = this.currentPortal();

    if (!currentPortal) {
      throw new Error('Portal data not loaded');
    }

    const request: UpdatePortalRequest = {
      portalName: formValue.portalName,
      description: formValue.description || undefined,
      keyWords: formValue.keywords || undefined,
      // Preserve existing values for fields not editable in this form
      logoFile: currentPortal.logoFile,
      footerText: currentPortal.footerText,
      expiryDate: currentPortal.expiryDate,
      userRegistration: currentPortal.userRegistration,
      bannerAdvertising: currentPortal.bannerAdvertising,
      currency: currentPortal.currency,
      administratorId: currentPortal.administratorId,
      hostFee: currentPortal.hostFee,
      hostSpace: currentPortal.hostSpace,
      pageQuota: currentPortal.pageQuota,
      userQuota: currentPortal.userQuota,
      siteLogHistory: currentPortal.siteLogHistory,
      splashTabId: currentPortal.splashTabId,
      homeTabId: currentPortal.homeTabId,
      loginTabId: currentPortal.loginTabId,
      userTabId: currentPortal.userTabId,
      defaultLanguage: currentPortal.defaultLanguage,
      timeZoneOffset: currentPortal.timeZoneOffset,
      homeDirectory: currentPortal.homeDirectory,
    };

    await firstValueFrom(this.portalService.updatePortal(portalId, request));
  }

  /**
   * Handles cancel button click
   *
   * MIGRATION: Replaces cmdCancel_Click from Signup.ascx.vb lines 130-139:
   * If IsHostMenu Then
   *     Response.Redirect(NavigateURL(), True)
   * Else
   *     Response.Redirect(GetPortalDomainName(PortalAlias.HTTPAlias, Request), True)
   * End If
   */
  onCancel(): void {
    this.router.navigate(['/portals']);
  }

  /**
   * Clears the current error message
   */
  clearError(): void {
    this.error.set(null);
  }

  // ==========================================================================
  // HELPER METHODS
  // ==========================================================================

  /**
   * Gets the placeholder text for portal name based on portal type
   */
  getPortalNamePlaceholder(): string {
    if (this.isEditMode()) {
      return 'Enter portal name';
    }

    const portalType = this.portalForm?.get('portalType')?.value || 'P';
    return portalType === 'C'
      ? `${this.domainName()}/subdirectory`
      : 'www.example.com';
  }

  /**
   * Gets the error message for a specific form control
   *
   * @param controlName - Name of the form control
   * @returns Error message string or empty string
   */
  getErrorMessage(controlName: string): string {
    const control = this.portalForm.get(controlName);

    if (!control || !control.touched || !control.errors) {
      return '';
    }

    const errors = control.errors;

    // Check for specific error types
    if (errors['required']) {
      return this.getFieldLabel(controlName) + ' is required';
    }

    if (errors['email']) {
      return 'Please enter a valid email address';
    }

    if (errors['minlength']) {
      const minLength = errors['minlength'].requiredLength;
      return `${this.getFieldLabel(controlName)} must be at least ${minLength} characters`;
    }

    if (errors['invalidCharacters']) {
      return errors['invalidCharacters'].message;
    }

    if (errors['aliasExists']) {
      return errors['aliasExists'].message;
    }

    if (errors['passwordMismatch']) {
      return 'Passwords do not match';
    }

    return 'Invalid value';
  }

  /**
   * Gets a human-readable label for a form field
   *
   * @param controlName - Name of the form control
   * @returns Human-readable field label
   */
  private getFieldLabel(controlName: string): string {
    const labels: Record<string, string> = {
      portalName: 'Portal name',
      alias: 'Portal alias',
      firstName: 'First name',
      lastName: 'Last name',
      username: 'Username',
      password: 'Password',
      confirmPassword: 'Password confirmation',
      email: 'Email',
      title: 'Title',
      description: 'Description',
      keywords: 'Keywords',
      template: 'Template',
      homeDirectory: 'Home directory',
      portalType: 'Portal type',
    };

    return labels[controlName] || controlName;
  }
}
