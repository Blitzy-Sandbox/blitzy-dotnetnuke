/**
 * Module Form Component
 *
 * Angular 19 standalone component for creating and editing module instances.
 * Implements reactive form with typed FormGroup for module properties.
 *
 * MIGRATION: Converted from VB.NET ModuleSettings.ascx.vb WebForms code-behind.
 * This component replaces the entire ModuleSettingsPage PortalModuleBase class
 * from the DNN 4.x admin module interface.
 *
 * @source Website/admin/Modules/ModuleSettings.ascx.vb
 *
 * Key functionality converted:
 * - BindData() method (lines 85-166) → loadModule() and form initialization
 * - Page_Load handler (lines 187-246) → ngOnInit lifecycle hook
 * - cmdUpdate_Click handler (lines 326-427) → onSubmit() method
 * - cmdCancel_Click handler (lines 279-286) → onCancel() method
 *
 * @example
 * // Navigate to edit module
 * <a routerLink="/modules/123/edit">Edit Module</a>
 *
 * // Navigate to create new module
 * <a routerLink="/modules/new">New Module</a>
 */

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  OnInit,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators
} from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';

import { ModuleService } from '../../services/module.service';
import {
  Module,
  CreateModuleRequest,
  UpdateModuleRequest,
  VisibilityState
} from '../../models/module.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

/**
 * Interface defining the typed form controls for the module form.
 * MIGRATION: Form fields mapped from original ASPX form controls in ModuleSettings.ascx
 */
interface ModuleFormControls {
  /**
   * Module display title
   * MIGRATION: From txtTitle (line 126, 344)
   */
  moduleTitle: FormControl<string>;

  /**
   * Tab ID where module is placed
   * MIGRATION: From cboTab (lines 129, 404)
   */
  tabId: FormControl<number | null>;

  /**
   * Content pane name (ContentPane, LeftPane, RightPane)
   * MIGRATION: For module placement
   */
  paneName: FormControl<string>;

  /**
   * Horizontal alignment (Left, Center, Right)
   * MIGRATION: From cboAlign (line 144, 345)
   */
  alignment: FormControl<string>;

  /**
   * Module visibility state
   * MIGRATION: From cboVisibility (lines 134, 359-363)
   */
  visibility: FormControl<VisibilityState>;

  /**
   * Cache time in seconds
   * MIGRATION: From txtCacheTime (lines 141, 349-353)
   */
  cacheTime: FormControl<number>;

  /**
   * Icon file path
   * MIGRATION: From ctlIcon.Url (lines 127, 348)
   */
  iconFile: FormControl<string>;

  /**
   * Whether module appears on all tabs
   * MIGRATION: From chkAllTabs (lines 133, 358)
   */
  allTabs: FormControl<boolean>;

  /**
   * Whether to display module title
   * MIGRATION: From chkDisplayTitle (lines 163, 380)
   */
  displayTitle: FormControl<boolean>;

  /**
   * Whether to display print option
   * MIGRATION: From chkDisplayPrint (lines 164, 381)
   */
  displayPrint: FormControl<boolean>;

  /**
   * Whether to display RSS/syndicate option
   * MIGRATION: From chkDisplaySyndicate (lines 165, 382)
   */
  displaySyndicate: FormControl<boolean>;

  /**
   * HTML header content
   * MIGRATION: From txtHeader (lines 149, 365)
   */
  header: FormControl<string>;

  /**
   * HTML footer content
   * MIGRATION: From txtFooter (lines 150, 366)
   */
  footer: FormControl<string>;

  /**
   * Module start date (ISO string)
   * MIGRATION: From txtStartDate (lines 152-154, 367-370)
   */
  startDate: FormControl<string | null>;

  /**
   * Module end date (ISO string)
   * MIGRATION: From txtEndDate (lines 155-157, 371-375)
   */
  endDate: FormControl<string | null>;

  /**
   * Container skin source path
   * MIGRATION: From ctlModuleContainer (lines 161, 377)
   */
  containerSrc: FormControl<string>;

  /**
   * Whether to inherit view permissions from tab
   * MIGRATION: From chkInheritPermissions (lines 122, 379)
   */
  inheritViewPermissions: FormControl<boolean>;
}

/**
 * Option interface for dropdown selections
 */
interface SelectOption<T> {
  value: T;
  label: string;
}

/**
 * ModuleFormComponent
 *
 * Angular 19 standalone component for module create/edit operations.
 * Uses signals for reactive state management, inject() for dependency injection,
 * and OnPush change detection for optimal performance.
 *
 * MIGRATION: Replaces ModuleSettingsPage class from ModuleSettings.ascx.vb
 */
@Component({
  selector: 'app-module-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="module-form-container">
      <!-- Page Header -->
      <header class="form-header">
        <h1>{{ isEditMode() ? 'Edit Module' : 'Create Module' }}</h1>
        @if (module()?.friendlyName) {
          <span class="module-type">{{ module()?.friendlyName }}</span>
        }
      </header>

      <!-- Loading State -->
      @if (loading()) {
        <div class="loading-container">
          <app-loading-spinner 
            size="large" 
            message="Loading module settings..." />
        </div>
      }

      <!-- Error Message -->
      @if (error()) {
        <div class="error-message" role="alert">
          <span class="error-icon">⚠️</span>
          <span>{{ error() }}</span>
          <button 
            type="button" 
            class="error-dismiss" 
            (click)="clearError()"
            aria-label="Dismiss error">
            ×
          </button>
        </div>
      }

      <!-- Module Form -->
      @if (!loading()) {
        <form 
          [formGroup]="moduleForm" 
          (ngSubmit)="onSubmit()" 
          class="module-form">

          <!-- General Settings Section -->
          <section class="form-section">
            <h2 class="section-title">General Settings</h2>

            <!-- Module Title -->
            <div class="form-group">
              <label for="moduleTitle" class="form-label required">
                Module Title
              </label>
              <input
                type="text"
                id="moduleTitle"
                formControlName="moduleTitle"
                class="form-control"
                placeholder="Enter module title"
                [class.invalid]="moduleForm.get('moduleTitle')?.invalid && moduleForm.get('moduleTitle')?.touched"
              />
              @if (moduleForm.get('moduleTitle')?.invalid && moduleForm.get('moduleTitle')?.touched) {
                <span class="validation-error">Module title is required</span>
              }
            </div>

            <!-- Pane Name -->
            <div class="form-group">
              <label for="paneName" class="form-label required">
                Content Pane
              </label>
              <select
                id="paneName"
                formControlName="paneName"
                class="form-control"
                [class.invalid]="moduleForm.get('paneName')?.invalid && moduleForm.get('paneName')?.touched">
                <option value="">Select content pane</option>
                <option value="ContentPane">Content Pane</option>
                <option value="LeftPane">Left Pane</option>
                <option value="RightPane">Right Pane</option>
                <option value="TopPane">Top Pane</option>
                <option value="BottomPane">Bottom Pane</option>
              </select>
              @if (moduleForm.get('paneName')?.invalid && moduleForm.get('paneName')?.touched) {
                <span class="validation-error">Content pane is required</span>
              }
            </div>

            <!-- Tab Selection (Edit Mode Only) -->
            @if (isEditMode()) {
              <div class="form-group">
                <label for="tabId" class="form-label">
                  Move to Tab
                </label>
                <input
                  type="number"
                  id="tabId"
                  formControlName="tabId"
                  class="form-control"
                  placeholder="Tab ID (leave empty to keep current)"
                />
              </div>
            }
          </section>

          <!-- Appearance Section -->
          <section class="form-section">
            <h2 class="section-title">Appearance</h2>

            <!-- Alignment -->
            <div class="form-group">
              <label for="alignment" class="form-label">
                Alignment
              </label>
              <select
                id="alignment"
                formControlName="alignment"
                class="form-control">
                @for (option of alignmentOptions; track option.value) {
                  <option [value]="option.value">{{ option.label }}</option>
                }
              </select>
            </div>

            <!-- Visibility -->
            <div class="form-group">
              <label for="visibility" class="form-label">
                Visibility
              </label>
              <select
                id="visibility"
                formControlName="visibility"
                class="form-control">
                @for (option of visibilityOptions; track option.value) {
                  <option [value]="option.value">{{ option.label }}</option>
                }
              </select>
            </div>

            <!-- Icon File -->
            <div class="form-group">
              <label for="iconFile" class="form-label">
                Icon File
              </label>
              <input
                type="text"
                id="iconFile"
                formControlName="iconFile"
                class="form-control"
                placeholder="Path to icon file"
              />
            </div>

            <!-- Container Source -->
            <div class="form-group">
              <label for="containerSrc" class="form-label">
                Container
              </label>
              <input
                type="text"
                id="containerSrc"
                formControlName="containerSrc"
                class="form-control"
                placeholder="Container skin path"
              />
            </div>
          </section>

          <!-- Display Options Section -->
          <section class="form-section">
            <h2 class="section-title">Display Options</h2>

            <div class="checkbox-group">
              <!-- Display Title -->
              <div class="form-check">
                <input
                  type="checkbox"
                  id="displayTitle"
                  formControlName="displayTitle"
                  class="form-check-input"
                />
                <label for="displayTitle" class="form-check-label">
                  Display Module Title
                </label>
              </div>

              <!-- Display Print -->
              <div class="form-check">
                <input
                  type="checkbox"
                  id="displayPrint"
                  formControlName="displayPrint"
                  class="form-check-input"
                />
                <label for="displayPrint" class="form-check-label">
                  Display Print Link
                </label>
              </div>

              <!-- Display Syndicate -->
              <div class="form-check">
                <input
                  type="checkbox"
                  id="displaySyndicate"
                  formControlName="displaySyndicate"
                  class="form-check-input"
                />
                <label for="displaySyndicate" class="form-check-label">
                  Display RSS Link
                </label>
              </div>

              <!-- All Tabs -->
              <div class="form-check">
                <input
                  type="checkbox"
                  id="allTabs"
                  formControlName="allTabs"
                  class="form-check-input"
                />
                <label for="allTabs" class="form-check-label">
                  Display on All Tabs
                </label>
              </div>
            </div>
          </section>

          <!-- Content Section -->
          <section class="form-section">
            <h2 class="section-title">Content</h2>

            <!-- Header -->
            <div class="form-group">
              <label for="header" class="form-label">
                Header
              </label>
              <textarea
                id="header"
                formControlName="header"
                class="form-control"
                rows="3"
                placeholder="HTML content above the module"
              ></textarea>
            </div>

            <!-- Footer -->
            <div class="form-group">
              <label for="footer" class="form-label">
                Footer
              </label>
              <textarea
                id="footer"
                formControlName="footer"
                class="form-control"
                rows="3"
                placeholder="HTML content below the module"
              ></textarea>
            </div>
          </section>

          <!-- Scheduling Section -->
          <section class="form-section">
            <h2 class="section-title">Scheduling</h2>

            <div class="date-fields">
              <!-- Start Date -->
              <div class="form-group">
                <label for="startDate" class="form-label">
                  Start Date
                </label>
                <input
                  type="date"
                  id="startDate"
                  formControlName="startDate"
                  class="form-control"
                />
              </div>

              <!-- End Date -->
              <div class="form-group">
                <label for="endDate" class="form-label">
                  End Date
                </label>
                <input
                  type="date"
                  id="endDate"
                  formControlName="endDate"
                  class="form-control"
                />
                @if (hasDateError()) {
                  <span class="validation-error">End date must be after start date</span>
                }
              </div>
            </div>
          </section>

          <!-- Caching Section -->
          <section class="form-section">
            <h2 class="section-title">Caching</h2>

            <!-- Cache Time -->
            <div class="form-group">
              <label for="cacheTime" class="form-label">
                Cache Time (seconds)
              </label>
              <input
                type="number"
                id="cacheTime"
                formControlName="cacheTime"
                class="form-control"
                min="0"
                placeholder="0"
                [class.invalid]="moduleForm.get('cacheTime')?.invalid && moduleForm.get('cacheTime')?.touched"
              />
              @if (moduleForm.get('cacheTime')?.invalid && moduleForm.get('cacheTime')?.touched) {
                <span class="validation-error">Cache time must be 0 or greater</span>
              }
            </div>
          </section>

          <!-- Permissions Section -->
          <section class="form-section">
            <h2 class="section-title">Permissions</h2>

            <div class="form-check">
              <input
                type="checkbox"
                id="inheritViewPermissions"
                formControlName="inheritViewPermissions"
                class="form-check-input"
              />
              <label for="inheritViewPermissions" class="form-check-label">
                Inherit View Permissions from Tab
              </label>
            </div>
          </section>

          <!-- Form Actions -->
          <div class="form-actions">
            <button
              type="button"
              class="btn btn-secondary"
              (click)="onCancel()"
              [disabled]="saving()">
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="saving() || moduleForm.invalid">
              @if (saving()) {
                <span class="btn-spinner"></span>
                Saving...
              } @else {
                {{ isEditMode() ? 'Update Module' : 'Create Module' }}
              }
            </button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    /* Container */
    .module-form-container {
      max-width: 800px;
      margin: 0 auto;
      padding: 24px;
    }

    /* Header */
    .form-header {
      margin-bottom: 24px;
    }

    .form-header h1 {
      font-size: 24px;
      font-weight: 600;
      color: #1a1a2e;
      margin: 0 0 8px;
    }

    .module-type {
      font-size: 14px;
      color: #6c757d;
    }

    /* Loading Container */
    .loading-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 400px;
    }

    /* Error Message */
    .error-message {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      background-color: #fee2e2;
      border: 1px solid #fca5a5;
      border-radius: 8px;
      color: #991b1b;
      margin-bottom: 24px;
    }

    .error-icon {
      flex-shrink: 0;
    }

    .error-dismiss {
      margin-left: auto;
      background: none;
      border: none;
      font-size: 20px;
      color: #991b1b;
      cursor: pointer;
      padding: 0;
      line-height: 1;
    }

    .error-dismiss:hover {
      color: #7f1d1d;
    }

    /* Form */
    .module-form {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    /* Form Sections */
    .form-section {
      background-color: #ffffff;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 20px;
    }

    .section-title {
      font-size: 16px;
      font-weight: 600;
      color: #374151;
      margin: 0 0 16px;
      padding-bottom: 8px;
      border-bottom: 1px solid #e5e7eb;
    }

    /* Form Groups */
    .form-group {
      margin-bottom: 16px;
    }

    .form-group:last-child {
      margin-bottom: 0;
    }

    .form-label {
      display: block;
      font-size: 14px;
      font-weight: 500;
      color: #374151;
      margin-bottom: 6px;
    }

    .form-label.required::after {
      content: ' *';
      color: #dc2626;
    }

    .form-control {
      width: 100%;
      padding: 10px 12px;
      font-size: 14px;
      line-height: 1.5;
      color: #1f2937;
      background-color: #ffffff;
      border: 1px solid #d1d5db;
      border-radius: 6px;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }

    .form-control:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }

    .form-control.invalid {
      border-color: #dc2626;
    }

    .form-control.invalid:focus {
      box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.1);
    }

    textarea.form-control {
      resize: vertical;
    }

    select.form-control {
      appearance: none;
      background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3e%3cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3e%3c/svg%3e");
      background-position: right 10px center;
      background-repeat: no-repeat;
      background-size: 16px;
      padding-right: 36px;
    }

    /* Validation Error */
    .validation-error {
      display: block;
      font-size: 12px;
      color: #dc2626;
      margin-top: 4px;
    }

    /* Checkbox Group */
    .checkbox-group {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .form-check {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .form-check-input {
      width: 16px;
      height: 16px;
      margin: 0;
      cursor: pointer;
      accent-color: #3b82f6;
    }

    .form-check-label {
      font-size: 14px;
      color: #374151;
      cursor: pointer;
    }

    /* Date Fields */
    .date-fields {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 16px;
    }

    @media (max-width: 640px) {
      .date-fields {
        grid-template-columns: 1fr;
      }
    }

    /* Form Actions */
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 12px;
      padding-top: 24px;
      border-top: 1px solid #e5e7eb;
    }

    /* Buttons */
    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 10px 20px;
      font-size: 14px;
      font-weight: 500;
      line-height: 1.5;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background-color: #3b82f6;
      border: 1px solid #3b82f6;
      color: #ffffff;
    }

    .btn-primary:hover:not(:disabled) {
      background-color: #2563eb;
      border-color: #2563eb;
    }

    .btn-secondary {
      background-color: #ffffff;
      border: 1px solid #d1d5db;
      color: #374151;
    }

    .btn-secondary:hover:not(:disabled) {
      background-color: #f9fafb;
      border-color: #9ca3af;
    }

    /* Button Spinner */
    .btn-spinner {
      width: 14px;
      height: 14px;
      border: 2px solid transparent;
      border-top-color: currentColor;
      border-radius: 50%;
      animation: spin 0.75s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class ModuleFormComponent implements OnInit {
  // ============================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() pattern)
  // ============================================================================

  /**
   * ModuleService for API operations.
   * MIGRATION: Replaces objModules As New ModuleController from VB.NET
   */
  private readonly moduleService = inject(ModuleService);

  /**
   * ActivatedRoute for reading route parameters.
   * MIGRATION: Replaces Request.QueryString["ModuleId"] (line 448)
   */
  private readonly route = inject(ActivatedRoute);

  /**
   * Router for programmatic navigation.
   * MIGRATION: Replaces Response.Redirect(NavigateURL(), True) (lines 281, 307, 421)
   */
  private readonly router = inject(Router);

  /**
   * FormBuilder for creating reactive forms.
   */
  private readonly fb = inject(FormBuilder);

  // ============================================================================
  // STATE MANAGEMENT (Angular 19 Signals)
  // ============================================================================

  /**
   * Loading state signal - tracks data loading.
   * MIGRATION: Replaces implicit WebForms postback waiting state
   */
  readonly loading = signal<boolean>(true);

  /**
   * Module data signal - holds the current module in edit mode.
   * MIGRATION: Replaces objModule As ModuleInfo (line 341)
   */
  readonly module = signal<Module | null>(null);

  /**
   * Edit mode signal - determines create vs edit mode.
   * MIGRATION: Replaces ModuleId <> -1 check (line 222)
   */
  readonly isEditMode = signal<boolean>(false);

  /**
   * Error message signal - holds error messages for display.
   */
  readonly error = signal<string | null>(null);

  /**
   * Saving state signal - tracks save operation in progress.
   */
  readonly saving = signal<boolean>(false);

  // ============================================================================
  // FORM CONFIGURATION
  // ============================================================================

  /**
   * Reactive form group with typed controls.
   * MIGRATION: Replaces ASPX server controls (txtTitle, cboVisibility, chkAllTabs, etc.)
   */
  readonly moduleForm: FormGroup<ModuleFormControls>;

  /**
   * Visibility state dropdown options.
   * MIGRATION: From cboVisibility options (lines 359-363)
   */
  readonly visibilityOptions: SelectOption<VisibilityState>[] = [
    { value: VisibilityState.Maximized, label: 'Maximized' },
    { value: VisibilityState.Minimized, label: 'Minimized' },
    { value: VisibilityState.None, label: 'None' }
  ];

  /**
   * Alignment dropdown options.
   * MIGRATION: From cboAlign options (line 345)
   */
  readonly alignmentOptions: SelectOption<string>[] = [
    { value: 'Left', label: 'Left' },
    { value: 'Center', label: 'Center' },
    { value: 'Right', label: 'Right' }
  ];

  /**
   * Module ID from route parameters (for edit mode).
   */
  private moduleId: number | null = null;

  /**
   * Tab ID from route parameters or module data.
   */
  private currentTabId: number | null = null;

  // ============================================================================
  // CONSTRUCTOR
  // ============================================================================

  constructor() {
    // Initialize the reactive form with typed controls
    this.moduleForm = this.fb.group<ModuleFormControls>({
      moduleTitle: this.fb.control('', {
        nonNullable: true,
        validators: [Validators.required]
      }),
      tabId: this.fb.control<number | null>(null),
      paneName: this.fb.control('ContentPane', {
        nonNullable: true,
        validators: [Validators.required]
      }),
      alignment: this.fb.control('Left', { nonNullable: true }),
      visibility: this.fb.control(VisibilityState.Maximized, { nonNullable: true }),
      cacheTime: this.fb.control(0, {
        nonNullable: true,
        validators: [Validators.min(0)]
      }),
      iconFile: this.fb.control('', { nonNullable: true }),
      allTabs: this.fb.control(false, { nonNullable: true }),
      displayTitle: this.fb.control(true, { nonNullable: true }),
      displayPrint: this.fb.control(false, { nonNullable: true }),
      displaySyndicate: this.fb.control(false, { nonNullable: true }),
      header: this.fb.control('', { nonNullable: true }),
      footer: this.fb.control('', { nonNullable: true }),
      startDate: this.fb.control<string | null>(null),
      endDate: this.fb.control<string | null>(null),
      containerSrc: this.fb.control('', { nonNullable: true }),
      inheritViewPermissions: this.fb.control(true, { nonNullable: true })
    });
  }

  // ============================================================================
  // LIFECYCLE HOOKS
  // ============================================================================

  /**
   * Angular lifecycle hook - initializes the component.
   * MIGRATION: Replaces Page_Load (lines 187-246) and BindData() method logic
   */
  ngOnInit(): void {
    // Subscribe to route parameters to determine edit mode
    this.route.paramMap.subscribe(params => {
      const idParam = params.get('id');

      if (idParam && idParam !== 'new') {
        // Edit mode - load existing module
        this.moduleId = parseInt(idParam, 10);

        if (isNaN(this.moduleId)) {
          this.error.set('Invalid module ID');
          this.loading.set(false);
          return;
        }

        this.isEditMode.set(true);

        // Get tabId from query parameters
        this.route.queryParamMap.subscribe(queryParams => {
          const tabIdParam = queryParams.get('tabId');
          if (tabIdParam) {
            this.currentTabId = parseInt(tabIdParam, 10);
          }

          // Load module data
          if (this.currentTabId) {
            this.loadModule(this.moduleId!, this.currentTabId);
          } else {
            // Default to a placeholder tabId, the API should handle this
            this.loadModule(this.moduleId!, 0);
          }
        });
      } else {
        // Create mode - initialize empty form with defaults
        this.isEditMode.set(false);
        this.initializeCreateMode();
        this.loading.set(false);
      }
    });
  }

  // ============================================================================
  // PUBLIC METHODS
  // ============================================================================

  /**
   * Loads module data for editing.
   * MIGRATION: Replaces objModules.GetModule(ModuleId, TabId, False) (line 115)
   *
   * @param moduleId - ID of the module to load
   * @param tabId - ID of the tab where module is located
   */
  loadModule(moduleId: number, tabId: number): void {
    this.loading.set(true);
    this.error.set(null);

    // MIGRATION: Call moduleService.getModule() replacing VB.NET ModuleController.GetModule()
    this.moduleService.getModule(moduleId, tabId).subscribe({
      next: (module: Module) => {
        this.module.set(module);
        this.currentTabId = module.tabId;
        this.patchFormWithModule(module);
        this.loading.set(false);
      },
      error: (err: Error) => {
        console.error('Failed to load module:', err);
        this.error.set('Failed to load module. Please try again.');
        this.loading.set(false);
      }
    });
  }

  /**
   * Handles form submission for create/update operations.
   * MIGRATION: Replaces cmdUpdate_Click handler (lines 326-427)
   */
  onSubmit(): void {
    // MIGRATION: Replaces Page.IsValid check (line 328)
    if (this.moduleForm.invalid) {
      this.markFormGroupTouched();
      return;
    }

    // Validate date range
    if (!this.validateDateRange()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    if (this.isEditMode() && this.moduleId) {
      // Update existing module
      const request = this.buildUpdateRequest();

      // MIGRATION: Replaces objModules.UpdateModule(objModule) (line 385)
      this.moduleService.updateModule(this.moduleId, request).subscribe({
        next: () => {
          this.saving.set(false);
          // MIGRATION: Replaces Response.Redirect(NavigateURL(), True) (line 421)
          this.router.navigate(['/modules']);
        },
        error: (err: Error) => {
          console.error('Failed to update module:', err);
          this.error.set('Failed to update module. Please try again.');
          this.saving.set(false);
        }
      });
    } else {
      // Create new module
      const request = this.buildCreateRequest();

      this.moduleService.createModule(request).subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/modules']);
        },
        error: (err: Error) => {
          console.error('Failed to create module:', err);
          this.error.set('Failed to create module. Please try again.');
          this.saving.set(false);
        }
      });
    }
  }

  /**
   * Handles cancel action - navigates back to module list.
   * MIGRATION: Replaces cmdCancel_Click handler (lines 279-286)
   */
  onCancel(): void {
    // MIGRATION: Replaces Response.Redirect(NavigateURL(), True) (line 281)
    this.router.navigate(['/modules']);
  }

  /**
   * Clears the current error message.
   */
  clearError(): void {
    this.error.set(null);
  }

  /**
   * Computed signal to check if there's a date validation error.
   */
  hasDateError(): boolean {
    const startDate = this.moduleForm.get('startDate')?.value;
    const endDate = this.moduleForm.get('endDate')?.value;

    if (startDate && endDate) {
      return new Date(endDate) < new Date(startDate);
    }
    return false;
  }

  // ============================================================================
  // PRIVATE METHODS
  // ============================================================================

  /**
   * Initializes form defaults for create mode.
   * MIGRATION: Replaces defaults set in Page_Load when ModuleId = -1 (lines 224-228)
   */
  private initializeCreateMode(): void {
    // MIGRATION: cboVisibility.SelectedIndex = 0 (Maximized) (line 225)
    this.moduleForm.patchValue({
      visibility: VisibilityState.Maximized,
      allTabs: false,
      displayTitle: true,
      displayPrint: false,
      displaySyndicate: false,
      inheritViewPermissions: true,
      alignment: 'Left',
      cacheTime: 0,
      paneName: 'ContentPane'
    });
  }

  /**
   * Patches the form with module data for editing.
   * MIGRATION: Replaces BindData() method form population (lines 85-166)
   *
   * @param module - Module data to populate form with
   */
  private patchFormWithModule(module: Module): void {
    // MIGRATION: Form field bindings from BindData() method
    this.moduleForm.patchValue({
      // MIGRATION: txtTitle.Text = objModule.ModuleTitle (line 126)
      moduleTitle: module.moduleTitle,
      // MIGRATION: cboTab (line 129)
      tabId: module.tabId,
      // MIGRATION: paneName from module data
      paneName: module.paneName || 'ContentPane',
      // MIGRATION: cboAlign (line 144)
      alignment: module.alignment || 'Left',
      // MIGRATION: cboVisibility.SelectedIndex = objModule.Visibility (line 134)
      visibility: module.visibility,
      // MIGRATION: txtCacheTime.Text = objModule.CacheTime.ToString (line 141)
      cacheTime: module.cacheTime || 0,
      // MIGRATION: ctlIcon.Url = objModule.IconFile (line 127)
      iconFile: module.iconFile || '',
      // MIGRATION: chkAllTabs.Checked = objModule.AllTabs (line 133)
      allTabs: module.allTabs,
      // MIGRATION: chkDisplayTitle.Checked = objModule.DisplayTitle (line 163)
      displayTitle: module.displayTitle,
      // MIGRATION: chkDisplayPrint.Checked = objModule.DisplayPrint (line 164)
      displayPrint: module.displayPrint,
      // MIGRATION: chkDisplaySyndicate.Checked = objModule.DisplaySyndicate (line 165)
      displaySyndicate: module.displaySyndicate,
      // MIGRATION: txtHeader.Text = objModule.Header (line 149)
      header: module.header || '',
      // MIGRATION: txtFooter.Text = objModule.Footer (line 150)
      footer: module.footer || '',
      // MIGRATION: txtStartDate.Text = objModule.StartDate.ToShortDateString (lines 152-154)
      startDate: module.startDate ? this.formatDateForInput(module.startDate) : null,
      // MIGRATION: txtEndDate.Text = objModule.EndDate.ToShortDateString (lines 155-157)
      endDate: module.endDate ? this.formatDateForInput(module.endDate) : null,
      // MIGRATION: ctlModuleContainer.SkinSrc = objModule.ContainerSrc (line 161)
      containerSrc: module.containerSrc || '',
      // MIGRATION: chkInheritPermissions.Checked = objModule.InheritViewPermissions (line 122)
      inheritViewPermissions: module.inheritViewPermissions
    });
  }

  /**
   * Builds the update request from form values.
   * MIGRATION: Replaces objModule property assignments in cmdUpdate_Click (lines 343-384)
   *
   * @returns UpdateModuleRequest containing form values
   */
  private buildUpdateRequest(): UpdateModuleRequest {
    const formValue = this.moduleForm.getRawValue();

    // MIGRATION: Build request from form values matching VB.NET property assignments
    const request: UpdateModuleRequest = {
      // MIGRATION: objModule.ModuleTitle = txtTitle.Text (line 344)
      moduleTitle: formValue.moduleTitle,
      // MIGRATION: objModule.Alignment = cboAlign.SelectedItem.Value (line 345)
      alignment: formValue.alignment,
      // MIGRATION: objModule.IconFile = ctlIcon.Url (line 348)
      iconFile: formValue.iconFile || undefined,
      // MIGRATION: objModule.CacheTime = Int32.Parse(txtCacheTime.Text) (lines 349-353)
      cacheTime: formValue.cacheTime,
      // MIGRATION: objModule.AllTabs = chkAllTabs.Checked (line 358)
      allTabs: formValue.allTabs,
      // MIGRATION: objModule.Visibility = ... (lines 359-363)
      visibility: formValue.visibility,
      // MIGRATION: objModule.Header = txtHeader.Text (line 365)
      header: formValue.header || undefined,
      // MIGRATION: objModule.Footer = txtFooter.Text (line 366)
      footer: formValue.footer || undefined,
      // MIGRATION: objModule.StartDate = Convert.ToDateTime(txtStartDate.Text) (lines 367-370)
      startDate: formValue.startDate || undefined,
      // MIGRATION: objModule.EndDate = Convert.ToDateTime(txtEndDate.Text) (lines 371-375)
      endDate: formValue.endDate || undefined,
      // MIGRATION: objModule.ContainerSrc = ctlModuleContainer.SkinSrc (line 377)
      containerSrc: formValue.containerSrc || undefined,
      // MIGRATION: objModule.InheritViewPermissions = chkInheritPermissions.Checked (line 379)
      inheritViewPermissions: formValue.inheritViewPermissions,
      // MIGRATION: objModule.DisplayTitle = chkDisplayTitle.Checked (line 380)
      displayTitle: formValue.displayTitle,
      // MIGRATION: objModule.DisplayPrint = chkDisplayPrint.Checked (line 381)
      displayPrint: formValue.displayPrint,
      // MIGRATION: objModule.DisplaySyndicate = chkDisplaySyndicate.Checked (line 382)
      displaySyndicate: formValue.displaySyndicate
    };

    // MIGRATION: Handle tab move if changed (lines 403-408)
    if (formValue.tabId && formValue.tabId !== this.currentTabId) {
      request.tabId = formValue.tabId;
    }

    return request;
  }

  /**
   * Builds the create request from form values.
   *
   * @returns CreateModuleRequest containing form values
   */
  private buildCreateRequest(): CreateModuleRequest {
    const formValue = this.moduleForm.getRawValue();

    const request: CreateModuleRequest = {
      // Required for create - moduleDefId should come from route or be selectable
      moduleDefId: 0, // This would typically come from a module selection
      tabId: formValue.tabId || 0, // This would typically come from context
      paneName: formValue.paneName,
      moduleTitle: formValue.moduleTitle,
      alignment: formValue.alignment,
      allTabs: formValue.allTabs,
      visibility: formValue.visibility
    };

    return request;
  }

  /**
   * Marks all form controls as touched to trigger validation display.
   */
  private markFormGroupTouched(): void {
    Object.keys(this.moduleForm.controls).forEach(key => {
      const control = this.moduleForm.get(key);
      control?.markAsTouched();
    });
  }

  /**
   * Validates that end date is after start date.
   *
   * @returns true if dates are valid, false otherwise
   */
  private validateDateRange(): boolean {
    const startDate = this.moduleForm.get('startDate')?.value;
    const endDate = this.moduleForm.get('endDate')?.value;

    if (startDate && endDate) {
      if (new Date(endDate) < new Date(startDate)) {
        this.error.set('End date must be after start date.');
        return false;
      }
    }
    return true;
  }

  /**
   * Formats a date string for HTML date input.
   *
   * @param dateString - ISO date string or Date object
   * @returns Formatted date string in YYYY-MM-DD format
   */
  private formatDateForInput(dateString: string): string {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) {
      return '';
    }
    return date.toISOString().split('T')[0];
  }
}
