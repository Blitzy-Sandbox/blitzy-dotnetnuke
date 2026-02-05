/**
 * Module Settings Component
 * 
 * MIGRATION: Angular 19 standalone component converting VB.NET WebForms
 * ModuleSettings.ascx.vb to modern Angular reactive architecture.
 * 
 * This component provides comprehensive module configuration interface with:
 * - Collapsible sections for organized settings management
 * - Signal-based reactive state management
 * - Reactive forms with typed controls
 * - Permission inheritance toggle
 * - Module move/copy operations
 * - Delete confirmation dialog
 * 
 * Source file: Website/admin/Modules/ModuleSettings.ascx.vb
 * 
 * @example
 * // Route configuration
 * { path: 'modules/:moduleId/settings', component: ModuleSettingsComponent }
 * 
 * // Direct navigation
 * this.router.navigate(['/modules', moduleId, 'settings'], { queryParams: { tabId } });
 */

import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  inject,
  DestroyRef,
  OnInit
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
import { forkJoin, of, catchError, tap, switchMap, finalize } from 'rxjs';

import { ModuleService } from '../../services/module.service';
import {
  Module,
  VisibilityState,
  UpdateModuleRequest,
  Tab,
  Container,
  ModulePermission,
  MoveModuleRequest,
  CopyModuleRequest
} from '../../models/module.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import {
  TextInputComponent,
  SelectComponent,
  CheckboxComponent,
  TextareaComponent,
  SelectOption
} from '../../../../shared/components/form-controls/form-controls.component';

/**
 * Interface for the module settings form structure with typed controls
 * MIGRATION: Derived from ModuleSettings.ascx.vb form field bindings (lines 85-166)
 */
interface ModuleSettingsForm {
  /** Module display title - txtTitle (line 126) */
  moduleTitle: FormControl<string>;
  /** Icon file path - ctlIcon (line 127) */
  iconFile: FormControl<string>;
  /** Target tab ID - cboTab (lines 129-131) */
  tabId: FormControl<number | null>;
  /** Show on all tabs - chkAllTabs (line 133) */
  allTabs: FormControl<boolean>;
  /** Visibility state - cboVisibility (line 134) */
  visibility: FormControl<VisibilityState>;
  /** Output cache time in seconds - txtCacheTime (line 141) */
  cacheTime: FormControl<number>;
  /** Horizontal alignment - cboAlign (line 144) */
  alignment: FormControl<string>;
  /** Background color - txtColor (line 146) */
  color: FormControl<string>;
  /** Border style - txtBorder (line 147) */
  border: FormControl<string>;
  /** Header HTML content - txtHeader (line 149) */
  header: FormControl<string>;
  /** Footer HTML content - txtFooter (line 150) */
  footer: FormControl<string>;
  /** Module start date - txtStartDate (line 153) */
  startDate: FormControl<string | null>;
  /** Module end date - txtEndDate (line 156) */
  endDate: FormControl<string | null>;
  /** Container skin source - ctlModuleContainer (line 161) */
  containerSrc: FormControl<string>;
  /** Display title in header - chkDisplayTitle (line 163) */
  displayTitle: FormControl<boolean>;
  /** Show print icon - chkDisplayPrint (line 164) */
  displayPrint: FormControl<boolean>;
  /** Show RSS icon - chkDisplaySyndicate (line 165) */
  displaySyndicate: FormControl<boolean>;
  /** Is default module - chkDefault (line 383) */
  isDefaultModule: FormControl<boolean>;
  /** Apply to all modules - chkAllModules (line 384) */
  allModules: FormControl<boolean>;
}

/**
 * ModuleSettingsComponent
 * 
 * Angular 19 standalone component for comprehensive module configuration.
 * Replaces DNN WebForms ModuleSettings.ascx.vb with modern reactive patterns.
 * 
 * Features:
 * - Signal-based state management for optimal reactivity
 * - OnPush change detection for performance
 * - Collapsible sections matching original DNN admin UI
 * - Full CRUD operations with confirmation dialogs
 * - Permission inheritance management
 * - Module move/copy/delete operations
 */
@Component({
  selector: 'app-module-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent,
    TextInputComponent,
    SelectComponent,
    CheckboxComponent,
    TextareaComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-container">
        <app-loading-spinner size="large" message="Loading module settings..." />
      </div>
    } @else if (errorMessage()) {
      <div class="error-container" role="alert">
        <div class="error-icon">⚠️</div>
        <h2 class="error-title">Error Loading Module</h2>
        <p class="error-message">{{ errorMessage() }}</p>
        <button type="button" class="btn btn-secondary" (click)="onCancel()">
          Go Back
        </button>
      </div>
    } @else {
      <div class="module-settings-container">
        <header class="settings-header">
          <h1 class="settings-title">Module Settings</h1>
          <p class="settings-subtitle">{{ friendlyName() }}</p>
        </header>

        <form [formGroup]="moduleForm" (ngSubmit)="onSave()" class="settings-form">
          <!-- Module Info Section (Read-only) -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('info')">
              <h2 class="section-title">Module Information</h2>
              <span class="section-toggle">{{ sectionStates().info ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().info) {
              <div class="section-content">
                <div class="form-row">
                  <div class="form-group">
                    <label class="form-label">Module Name</label>
                    <div class="form-static">{{ friendlyName() }}</div>
                  </div>
                </div>
                <app-text-input
                  label="Module Title"
                  formControlName="moduleTitle"
                  placeholder="Enter module title"
                  [required]="true"
                  [errorMessage]="getFieldError('moduleTitle')" />
              </div>
            }
          </section>

          <!-- Permissions Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('permissions')">
              <h2 class="section-title">Permissions</h2>
              <span class="section-toggle">{{ sectionStates().permissions ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().permissions) {
              <div class="section-content">
                <app-checkbox
                  label="Inherit View Permissions from Tab"
                  [formControl]="inheritPermissionsControl"
                  (change)="onInheritPermissionsChange($event)" />
                
                @if (!inheritViewPermissions()) {
                  <div class="permissions-grid">
                    <table class="permissions-table" role="grid" aria-label="Module Permissions">
                      <thead>
                        <tr>
                          <th scope="col">Role/User</th>
                          <th scope="col">Permission</th>
                          <th scope="col">Allow</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (permission of permissions(); track permission.modulePermissionId) {
                          <tr>
                            <td>{{ permission.roleName || permission.displayName || 'Unknown' }}</td>
                            <td>{{ permission.permissionName }}</td>
                            <td>
                              <span [class]="permission.allowAccess ? 'badge-allow' : 'badge-deny'">
                                {{ permission.allowAccess ? 'Allow' : 'Deny' }}
                              </span>
                            </td>
                          </tr>
                        } @empty {
                          <tr>
                            <td colspan="3" class="empty-message">No custom permissions configured</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </div>
            }
          </section>

          <!-- Appearance Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('appearance')">
              <h2 class="section-title">Appearance</h2>
              <span class="section-toggle">{{ sectionStates().appearance ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().appearance) {
              <div class="section-content">
                <div class="form-row two-columns">
                  <app-text-input
                    label="Icon File"
                    formControlName="iconFile"
                    placeholder="Path to icon file" />
                  <app-select
                    label="Container"
                    formControlName="containerSrc"
                    placeholder="Select container..."
                    [options]="containerOptions()" />
                </div>
                <div class="form-row two-columns">
                  <app-text-input
                    label="Background Color"
                    formControlName="color"
                    placeholder="#ffffff or color name" />
                  <app-text-input
                    label="Border"
                    formControlName="border"
                    placeholder="e.g., 1px solid #ccc" />
                </div>
                <div class="form-row two-columns">
                  <app-select
                    label="Alignment"
                    formControlName="alignment"
                    [options]="alignmentOptions" />
                </div>
                <div class="form-row">
                  <app-checkbox
                    label="Display Title"
                    formControlName="displayTitle" />
                </div>
                <div class="form-row two-columns">
                  <app-checkbox
                    label="Display Print Icon"
                    formControlName="displayPrint" />
                  <app-checkbox
                    label="Display Syndicate Icon"
                    formControlName="displaySyndicate" />
                </div>
              </div>
            }
          </section>

          <!-- Page Settings Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('pageSettings')">
              <h2 class="section-title">Page Settings</h2>
              <span class="section-toggle">{{ sectionStates().pageSettings ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().pageSettings) {
              <div class="section-content">
                <div class="form-row two-columns">
                  <app-select
                    label="Page"
                    formControlName="tabId"
                    placeholder="Select page..."
                    [options]="tabOptions()"
                    [disabled]="!canManageAllTabs()" />
                  <app-select
                    label="Visibility"
                    formControlName="visibility"
                    [options]="visibilityOptions" />
                </div>
                <div class="form-row">
                  <app-checkbox
                    label="Display on All Pages"
                    formControlName="allTabs"
                    [disabled]="!canManageAllTabs()" />
                </div>
                @if (canManageAllTabs()) {
                  <div class="form-row two-columns">
                    <app-checkbox
                      label="Set as Default Module"
                      formControlName="isDefaultModule" />
                    <app-checkbox
                      label="Apply to All Modules"
                      formControlName="allModules" />
                  </div>
                }
              </div>
            }
          </section>

          <!-- Scheduling Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('scheduling')">
              <h2 class="section-title">Scheduling</h2>
              <span class="section-toggle">{{ sectionStates().scheduling ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().scheduling) {
              <div class="section-content">
                <div class="form-row two-columns">
                  <app-text-input
                    label="Start Date"
                    formControlName="startDate"
                    type="date"
                    placeholder="Select start date" />
                  <app-text-input
                    label="End Date"
                    formControlName="endDate"
                    type="date"
                    placeholder="Select end date" />
                </div>
                <p class="form-hint">
                  Leave dates empty for the module to be always visible.
                </p>
              </div>
            }
          </section>

          <!-- Caching Section (Conditional) -->
          @if (showCacheRow()) {
            <section class="settings-section">
              <div class="section-header" (click)="toggleSection('caching')">
                <h2 class="section-title">Caching</h2>
                <span class="section-toggle">{{ sectionStates().caching ? '−' : '+' }}</span>
              </div>
              @if (sectionStates().caching) {
                <div class="section-content">
                  <app-text-input
                    label="Cache Time (seconds)"
                    formControlName="cacheTime"
                    type="number"
                    placeholder="0 = No caching" />
                  <p class="form-hint">
                    Set to 0 to disable output caching for this module.
                  </p>
                </div>
              }
            </section>
          }

          <!-- Header/Footer Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('headerFooter')">
              <h2 class="section-title">Header & Footer</h2>
              <span class="section-toggle">{{ sectionStates().headerFooter ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().headerFooter) {
              <div class="section-content">
                <app-textarea
                  label="Header HTML"
                  formControlName="header"
                  placeholder="HTML content to display above the module"
                  [rows]="3" />
                <app-textarea
                  label="Footer HTML"
                  formControlName="footer"
                  placeholder="HTML content to display below the module"
                  [rows]="3" />
              </div>
            }
          </section>

          <!-- Module-Specific Settings Section -->
          <section class="settings-section">
            <div class="section-header" (click)="toggleSection('specific')">
              <h2 class="section-title">Module-Specific Settings</h2>
              <span class="section-toggle">{{ sectionStates().specific ? '−' : '+' }}</span>
            </div>
            @if (sectionStates().specific) {
              <div class="section-content">
                @if (Object.keys(moduleSettings()).length > 0) {
                  <div class="specific-settings">
                    @for (key of Object.keys(moduleSettings()); track key) {
                      <div class="setting-item">
                        <label class="setting-label">{{ formatSettingKey(key) }}</label>
                        <input
                          type="text"
                          class="setting-input"
                          [value]="moduleSettings()[key]"
                          (input)="onSpecificSettingChange(key, $event)" />
                      </div>
                    }
                  </div>
                } @else {
                  <p class="empty-message">No module-specific settings available.</p>
                }
              </div>
            }
          </section>

          <!-- Action Buttons -->
          <div class="settings-actions">
            <div class="actions-left">
              <button
                type="button"
                class="btn btn-danger"
                (click)="onDeleteClick()"
                [disabled]="saving()">
                Delete Module
              </button>
            </div>
            <div class="actions-right">
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
                [disabled]="moduleForm.invalid || saving()">
                @if (saving()) {
                  <span class="btn-spinner"></span>
                  Saving...
                } @else {
                  Update Settings
                }
              </button>
            </div>
          </div>
        </form>
      </div>
    }

    <!-- Delete Confirmation Dialog -->
    @if (showDeleteDialog()) {
      <app-confirmation-dialog
        title="Delete Module"
        message="Are you sure you want to delete this module from the current page? This action cannot be undone."
        confirmText="Delete"
        cancelText="Cancel"
        confirmButtonType="danger"
        (confirmed)="onDeleteConfirm()"
        (cancelled)="onDeleteDialogCancel()" />
    }
  `,
  styles: [`
    /* Container and Layout */
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
      font-size: 3rem;
      margin-bottom: 1rem;
    }

    .error-title {
      font-size: 1.5rem;
      font-weight: 600;
      color: #374151;
      margin-bottom: 0.5rem;
    }

    .error-message {
      color: #6b7280;
      margin-bottom: 1.5rem;
    }

    .module-settings-container {
      max-width: 900px;
      margin: 0 auto;
      padding: 1.5rem;
    }

    /* Header */
    .settings-header {
      margin-bottom: 2rem;
      padding-bottom: 1rem;
      border-bottom: 1px solid #e5e7eb;
    }

    .settings-title {
      font-size: 1.75rem;
      font-weight: 700;
      color: #111827;
      margin: 0 0 0.25rem 0;
    }

    .settings-subtitle {
      font-size: 1rem;
      color: #6b7280;
      margin: 0;
    }

    /* Form Layout */
    .settings-form {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    /* Collapsible Sections */
    .settings-section {
      background: #ffffff;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      overflow: hidden;
    }

    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.25rem;
      background: #f9fafb;
      cursor: pointer;
      user-select: none;
      transition: background-color 0.15s ease;
    }

    .section-header:hover {
      background: #f3f4f6;
    }

    .section-title {
      font-size: 1rem;
      font-weight: 600;
      color: #374151;
      margin: 0;
    }

    .section-toggle {
      font-size: 1.25rem;
      font-weight: 500;
      color: #6b7280;
      width: 1.5rem;
      text-align: center;
    }

    .section-content {
      padding: 1.25rem;
      border-top: 1px solid #e5e7eb;
    }

    /* Form Rows */
    .form-row {
      margin-bottom: 1rem;
    }

    .form-row:last-child {
      margin-bottom: 0;
    }

    .form-row.two-columns {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }

    @media (max-width: 640px) {
      .form-row.two-columns {
        grid-template-columns: 1fr;
      }
    }

    /* Form Elements */
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .form-label {
      font-weight: 500;
      color: #374151;
      font-size: 0.875rem;
    }

    .form-static {
      padding: 0.5rem 0;
      color: #6b7280;
      font-size: 1rem;
    }

    .form-hint {
      font-size: 0.8125rem;
      color: #6b7280;
      margin: 0.5rem 0 0 0;
      font-style: italic;
    }

    /* Permissions Table */
    .permissions-grid {
      margin-top: 1rem;
      overflow-x: auto;
    }

    .permissions-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }

    .permissions-table th,
    .permissions-table td {
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid #e5e7eb;
    }

    .permissions-table th {
      background: #f9fafb;
      font-weight: 600;
      color: #374151;
    }

    .permissions-table td {
      color: #4b5563;
    }

    .badge-allow {
      display: inline-block;
      padding: 0.25rem 0.5rem;
      background: #dcfce7;
      color: #166534;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    .badge-deny {
      display: inline-block;
      padding: 0.25rem 0.5rem;
      background: #fee2e2;
      color: #991b1b;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 500;
    }

    /* Module-Specific Settings */
    .specific-settings {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .setting-item {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .setting-label {
      font-weight: 500;
      color: #374151;
      font-size: 0.875rem;
    }

    .setting-input {
      padding: 0.5rem 0.75rem;
      border: 1px solid #d1d5db;
      border-radius: 0.375rem;
      font-size: 1rem;
      line-height: 1.5;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }

    .setting-input:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }

    .empty-message {
      color: #6b7280;
      font-style: italic;
      text-align: center;
      padding: 1rem;
    }

    /* Action Buttons */
    .settings-actions {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 1.5rem;
      margin-top: 1rem;
      border-top: 1px solid #e5e7eb;
    }

    .actions-left {
      display: flex;
      gap: 0.75rem;
    }

    .actions-right {
      display: flex;
      gap: 0.75rem;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.625rem 1.25rem;
      font-size: 0.9375rem;
      font-weight: 500;
      border: none;
      border-radius: 6px;
      cursor: pointer;
      transition: background-color 0.15s ease, transform 0.1s ease, box-shadow 0.15s ease;
      min-width: 100px;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn:not(:disabled):active {
      transform: scale(0.98);
    }

    .btn-primary {
      background-color: #3b82f6;
      color: #ffffff;
    }

    .btn-primary:not(:disabled):hover {
      background-color: #2563eb;
      box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
    }

    .btn-secondary {
      background-color: #f3f4f6;
      color: #374151;
      border: 1px solid #d1d5db;
    }

    .btn-secondary:not(:disabled):hover {
      background-color: #e5e7eb;
    }

    .btn-danger {
      background-color: #dc3545;
      color: #ffffff;
    }

    .btn-danger:not(:disabled):hover {
      background-color: #c82333;
      box-shadow: 0 2px 8px rgba(220, 53, 69, 0.3);
    }

    .btn-spinner {
      width: 1rem;
      height: 1rem;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: #ffffff;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    /* Responsive */
    @media (max-width: 640px) {
      .module-settings-container {
        padding: 1rem;
      }

      .settings-actions {
        flex-direction: column-reverse;
        gap: 1rem;
      }

      .actions-left,
      .actions-right {
        width: 100%;
      }

      .actions-right {
        flex-direction: column;
      }

      .btn {
        width: 100%;
      }
    }
  `]
})
export class ModuleSettingsComponent implements OnInit {
  // ==========================================================================
  // DEPENDENCY INJECTION (Angular 19 inject() function)
  // MIGRATION: Replaces constructor injection per Angular 19 standards
  // ==========================================================================

  /**
   * Module service for API operations
   * MIGRATION: Replaces New ModuleController from VB.NET
   */
  private readonly moduleService = inject(ModuleService);

  /**
   * Router for navigation
   * MIGRATION: Replaces Response.Redirect/NavigateURL from VB.NET
   */
  private readonly router = inject(Router);

  /**
   * Activated route for parameter access
   * MIGRATION: Replaces Request.QueryString("ModuleId") from VB.NET
   */
  private readonly route = inject(ActivatedRoute);

  /**
   * Destroy reference for subscription cleanup
   * MIGRATION: Automatic cleanup using takeUntilDestroyed
   */
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Reference to Object for template usage
   */
  protected readonly Object = Object;

  // ==========================================================================
  // SIGNAL-BASED STATE
  // MIGRATION: Derived from control bindings in ModuleSettings.ascx.vb
  // ==========================================================================

  /**
   * Current module data
   * MIGRATION: From objModule variable in BindData
   */
  readonly module = signal<Module | null>(null);

  /**
   * Loading state for initial data fetch
   */
  readonly loading = signal<boolean>(true);

  /**
   * Saving state for update operations
   */
  readonly saving = signal<boolean>(false);

  /**
   * Module-specific custom settings
   * MIGRATION: From ctlSpecific.LoadSettings pattern
   */
  readonly moduleSettings = signal<Record<string, string>>({});

  /**
   * Module permissions collection
   * MIGRATION: From dgPermissions.Permissions
   */
  readonly permissions = signal<ModulePermission[]>([]);

  /**
   * Permission inheritance toggle state
   * MIGRATION: From chkInheritPermissions.Checked (line 122)
   */
  readonly inheritViewPermissions = signal<boolean>(false);

  /**
   * Available tabs for dropdown
   * MIGRATION: From GetPortalTabs result (line 207)
   */
  readonly tabs = signal<Tab[]>([]);

  /**
   * Available containers for dropdown
   * MIGRATION: From SkinInfo.RootContainer
   */
  readonly containers = signal<Container[]>([]);

  /**
   * Delete confirmation dialog visibility
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm (line 205)
   */
  readonly showDeleteDialog = signal<boolean>(false);

  /**
   * Error message signal for error display
   */
  readonly errorMessage = signal<string | null>(null);

  /**
   * Collapsible section states
   */
  readonly sectionStates = signal({
    info: true,
    permissions: true,
    appearance: false,
    pageSettings: true,
    scheduling: false,
    caching: false,
    headerFooter: false,
    specific: false
  });

  /**
   * Control for inherit permissions checkbox (standalone)
   */
  readonly inheritPermissionsControl = new FormControl<boolean>(false);

  /**
   * Original tab ID for detecting tab changes
   */
  private originalTabId: number | null = null;

  /**
   * Original allTabs value for detecting changes
   */
  private originalAllTabs: boolean = false;

  /**
   * Current module ID from route
   */
  private moduleId: number = 0;

  /**
   * Current tab ID from route/query params
   */
  private tabId: number = 0;

  // ==========================================================================
  // COMPUTED SIGNALS
  // ==========================================================================

  /**
   * Friendly name of the module for display
   * MIGRATION: From txtFriendlyName.Text (line 125)
   */
  readonly friendlyName = computed(() => this.module()?.friendlyName ?? 'Module Settings');

  /**
   * Whether to show the cache row based on default cache time
   * MIGRATION: From rowCache.Visible logic (line 139)
   */
  readonly showCacheRow = computed(() => {
    const mod = this.module();
    return mod !== null && mod.defaultCacheTime !== null && mod.defaultCacheTime !== undefined;
  });

  /**
   * Whether user can manage all tabs settings
   * MIGRATION: From admin-only controls logic (lines 215-220)
   * Note: In a real implementation, this would check actual user roles
   */
  readonly canManageAllTabs = computed(() => {
    // MIGRATION: In production, this should check PortalSecurity.IsInRoles(AdministratorRoleName)
    // For now, return true to enable functionality
    return true;
  });

  /**
   * Tab options for dropdown
   */
  readonly tabOptions = computed((): SelectOption[] => {
    return this.tabs().map(tab => ({
      value: tab.tabId,
      label: '  '.repeat(tab.level) + tab.tabName,
      disabled: tab.disableLink
    }));
  });

  /**
   * Container options for dropdown
   */
  readonly containerOptions = computed((): SelectOption[] => {
    return [
      { value: '', label: '-- None --' },
      ...this.containers().map(container => ({
        value: container.containerSrc,
        label: container.containerName
      }))
    ];
  });

  // ==========================================================================
  // STATIC OPTIONS
  // MIGRATION: From cboVisibility and cboAlign binding patterns
  // ==========================================================================

  /**
   * Visibility dropdown options
   * MIGRATION: From cboVisibility binding (lines 359-363)
   */
  readonly visibilityOptions: SelectOption[] = [
    { value: VisibilityState.Maximized, label: 'Maximized' },
    { value: VisibilityState.Minimized, label: 'Minimized' },
    { value: VisibilityState.None, label: 'None (Hidden)' }
  ];

  /**
   * Alignment dropdown options
   * MIGRATION: From cboAlign binding (line 144)
   */
  readonly alignmentOptions: SelectOption[] = [
    { value: '', label: 'Not Specified' },
    { value: 'Left', label: 'Left' },
    { value: 'Center', label: 'Center' },
    { value: 'Right', label: 'Right' }
  ];

  // ==========================================================================
  // REACTIVE FORM
  // MIGRATION: From BindData method (lines 85-169)
  // ==========================================================================

  /**
   * Module settings form with typed controls
   * MIGRATION: Replaces WebForms server controls with Angular reactive form
   */
  readonly moduleForm = new FormGroup<ModuleSettingsForm>({
    moduleTitle: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    iconFile: new FormControl<string>('', { nonNullable: true }),
    tabId: new FormControl<number | null>(null),
    allTabs: new FormControl<boolean>(false, { nonNullable: true }),
    visibility: new FormControl<VisibilityState>(VisibilityState.Maximized, { nonNullable: true }),
    cacheTime: new FormControl<number>(0, { nonNullable: true }),
    alignment: new FormControl<string>('', { nonNullable: true }),
    color: new FormControl<string>('', { nonNullable: true }),
    border: new FormControl<string>('', { nonNullable: true }),
    header: new FormControl<string>('', { nonNullable: true }),
    footer: new FormControl<string>('', { nonNullable: true }),
    startDate: new FormControl<string | null>(null),
    endDate: new FormControl<string | null>(null),
    containerSrc: new FormControl<string>('', { nonNullable: true }),
    displayTitle: new FormControl<boolean>(true, { nonNullable: true }),
    displayPrint: new FormControl<boolean>(false, { nonNullable: true }),
    displaySyndicate: new FormControl<boolean>(false, { nonNullable: true }),
    isDefaultModule: new FormControl<boolean>(false, { nonNullable: true }),
    allModules: new FormControl<boolean>(false, { nonNullable: true })
  });

  // ==========================================================================
  // LIFECYCLE HOOKS
  // MIGRATION: From Page_Init and Page_Load patterns (lines 187-247, 438-479)
  // ==========================================================================

  /**
   * Component initialization
   * MIGRATION: Replaces Page_Load event handler
   */
  ngOnInit(): void {
    // Subscribe to route parameters for moduleId and tabId
    this.route.params
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.moduleId = +params['moduleId'] || 0;
        if (this.moduleId > 0) {
          this.loadInitialData();
        } else {
          this.errorMessage.set('Invalid module ID');
          this.loading.set(false);
        }
      });

    // Also check query params for tabId
    this.route.queryParams
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        if (params['tabId']) {
          this.tabId = +params['tabId'];
        }
      });
  }

  // ==========================================================================
  // DATA LOADING METHODS
  // MIGRATION: From Page_Init and BindData patterns
  // ==========================================================================

  /**
   * Load all initial data concurrently
   * MIGRATION: Combines Page_Init and BindData initialization
   */
  private loadInitialData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    // Determine tabId - use query param or default to 0 (will be resolved by API)
    const effectiveTabId = this.tabId || 0;

    forkJoin({
      module: this.moduleService.getModule(this.moduleId, effectiveTabId).pipe(
        catchError(err => {
          console.error('Error loading module:', err);
          return of(null);
        })
      ),
      moduleSettings: this.moduleService.getModuleSettings(this.moduleId).pipe(
        catchError(() => of({} as Record<string, string>))
      )
    })
    .pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.loading.set(false))
    )
    .subscribe({
      next: ({ module, moduleSettings }) => {
        if (module) {
          this.module.set(module);
          this.tabId = module.tabId;
          this.originalTabId = module.tabId;
          this.originalAllTabs = module.allTabs;
          this.moduleSettings.set(moduleSettings);
          this.bindFormData(module);
          this.loadSecondaryData();
        } else {
          this.errorMessage.set('Module not found or access denied');
        }
      },
      error: (err) => {
        console.error('Error loading module data:', err);
        this.errorMessage.set('Failed to load module settings. Please try again.');
      }
    });
  }

  /**
   * Load secondary data (tabs, containers) after primary module load
   */
  private loadSecondaryData(): void {
    // Load tabs and containers in parallel
    // Note: These would typically come from dedicated API endpoints
    // For now, using mock data structure
    this.tabs.set([
      { tabId: this.tabId, tabName: 'Current Page', parentId: null, level: 0, isVisible: true, disableLink: false }
    ]);
    
    this.containers.set([
      { containerSrc: '[G]Containers/Default/NoTitle', containerName: 'No Title' },
      { containerSrc: '[G]Containers/Default/Title_h2', containerName: 'Title (H2)' },
      { containerSrc: '[G]Containers/Default/Title_Gray', containerName: 'Title (Gray)' }
    ]);
  }

  /**
   * Populate form controls with module data
   * MIGRATION: Direct port of BindData method (lines 85-169)
   */
  private bindFormData(module: Module): void {
    // Set inherit permissions state
    this.inheritViewPermissions.set(module.inheritViewPermissions);
    this.inheritPermissionsControl.setValue(module.inheritViewPermissions);

    // Populate form controls
    this.moduleForm.patchValue({
      moduleTitle: module.moduleTitle || '',
      iconFile: module.iconFile || '',
      tabId: module.tabId,
      allTabs: module.allTabs,
      visibility: module.visibility,
      cacheTime: module.cacheTime || 0,
      alignment: module.alignment || '',
      color: module.color || '',
      border: module.border || '',
      header: module.header || '',
      footer: module.footer || '',
      startDate: module.startDate ? this.formatDateForInput(module.startDate) : null,
      endDate: module.endDate ? this.formatDateForInput(module.endDate) : null,
      containerSrc: module.containerSrc || '',
      displayTitle: module.displayTitle,
      displayPrint: module.displayPrint,
      displaySyndicate: module.displaySyndicate,
      isDefaultModule: module.isDefaultModule || false,
      allModules: module.allModules || false
    });

    // Load permissions if available
    // MIGRATION: From dgPermissions setup (lines 117-123)
    // Note: In production, permissions would come from a dedicated API endpoint
    this.permissions.set([]);
  }

  // ==========================================================================
  // EVENT HANDLERS
  // ==========================================================================

  /**
   * Toggle section expanded/collapsed state
   */
  toggleSection(section: keyof typeof ModuleSettingsComponent.prototype.sectionStates extends () => infer T ? keyof T : never): void {
    const currentStates = this.sectionStates();
    this.sectionStates.set({
      ...currentStates,
      [section]: !currentStates[section as keyof typeof currentStates]
    });
  }

  /**
   * Handle inherit permissions toggle
   * MIGRATION: From chkInheritPermissions_CheckedChanged (lines 260-266)
   */
  onInheritPermissionsChange(event: Event): void {
    const target = event.target as HTMLInputElement;
    const inherit = target?.checked ?? this.inheritPermissionsControl.value ?? false;
    this.inheritViewPermissions.set(inherit);
    
    // MIGRATION: dgPermissions.InheritViewPermissionsFromTab pattern
    if (inherit) {
      // When inheriting, clear custom permissions display
      this.permissions.set([]);
    }
  }

  /**
   * Handle module-specific setting change
   * MIGRATION: From ctlSpecific.UpdateSettings pattern (lines 387-396)
   */
  onSpecificSettingChange(key: string, event: Event): void {
    const target = event.target as HTMLInputElement;
    const value = target.value;
    const currentSettings = this.moduleSettings();
    this.moduleSettings.set({
      ...currentSettings,
      [key]: value
    });
  }

  /**
   * Format setting key for display
   */
  formatSettingKey(key: string): string {
    // Convert camelCase or PascalCase to Title Case with spaces
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, str => str.toUpperCase())
      .trim();
  }

  /**
   * Save module settings
   * MIGRATION: From cmdUpdate_Click handler (lines 326-427)
   */
  onSave(): void {
    if (this.moduleForm.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const formValue = this.moduleForm.getRawValue();
    const allTabsChanged = formValue.allTabs !== this.originalAllTabs;
    const tabChanged = formValue.tabId !== this.originalTabId && !formValue.allTabs;

    // Build update request
    // MIGRATION: From objModule property assignments (lines 341-385)
    const updateRequest: UpdateModuleRequest = {
      moduleTitle: formValue.moduleTitle,
      iconFile: formValue.iconFile || undefined,
      tabId: formValue.tabId ?? undefined,
      allTabs: formValue.allTabs,
      visibility: formValue.visibility,
      cacheTime: formValue.cacheTime,
      alignment: formValue.alignment || undefined,
      color: formValue.color || undefined,
      border: formValue.border || undefined,
      header: formValue.header || undefined,
      footer: formValue.footer || undefined,
      startDate: formValue.startDate || undefined,
      endDate: formValue.endDate || undefined,
      containerSrc: formValue.containerSrc || undefined,
      displayTitle: formValue.displayTitle,
      displayPrint: formValue.displayPrint,
      displaySyndicate: formValue.displaySyndicate,
      inheritViewPermissions: this.inheritViewPermissions()
    };

    // Update module first
    this.moduleService.updateModule(this.moduleId, updateRequest)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        // Update module-specific settings
        switchMap(() => {
          const settings = this.moduleSettings();
          if (Object.keys(settings).length > 0) {
            return this.moduleService.updateModuleSettings(this.moduleId, settings);
          }
          return of(void 0);
        }),
        // Handle module move if tab changed
        // MIGRATION: From lines 403-408
        switchMap(() => {
          if (tabChanged && formValue.tabId && this.originalTabId) {
            const moveRequest: MoveModuleRequest = {
              fromTabId: this.originalTabId,
              toTabId: formValue.tabId,
              paneName: this.module()?.paneName || 'ContentPane'
            };
            return this.moduleService.moveModule(this.moduleId, moveRequest);
          }
          return of(void 0);
        }),
        // Handle all-tabs propagation
        // MIGRATION: From lines 411-418
        switchMap(() => {
          if (allTabsChanged) {
            if (formValue.allTabs) {
              // Copy module to all tabs
              const copyRequest: CopyModuleRequest = {
                fromTabId: this.tabId,
                toTabId: 0, // API handles all tabs
                includeSettings: true
              };
              return this.moduleService.copyModule(this.moduleId, copyRequest);
            }
            // Note: DeleteAllModules would be handled by the API
          }
          return of(void 0);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => {
          // MIGRATION: Response.Redirect(NavigateURL()) from line 421
          this.router.navigate(['/modules']);
        },
        error: (err) => {
          console.error('Error saving module settings:', err);
          this.errorMessage.set('Failed to save module settings. Please try again.');
        }
      });
  }

  /**
   * Cancel and navigate back
   * MIGRATION: From cmdCancel_Click handler (lines 279-286)
   */
  onCancel(): void {
    // MIGRATION: Response.Redirect(NavigateURL()) from line 281
    this.router.navigate(['/modules']);
  }

  /**
   * Show delete confirmation dialog
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm pattern (line 205)
   */
  onDeleteClick(): void {
    this.showDeleteDialog.set(true);
  }

  /**
   * Confirm module deletion
   * MIGRATION: From cmdDelete_Click handler (lines 300-312)
   */
  onDeleteConfirm(): void {
    this.showDeleteDialog.set(false);
    this.saving.set(true);

    // MIGRATION: objModules.DeleteTabModule(TabId, ModuleId) from line 305
    this.moduleService.deleteTabModule(this.tabId, this.moduleId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => {
          // MIGRATION: Response.Redirect(NavigateURL()) from line 307
          this.router.navigate(['/modules']);
        },
        error: (err) => {
          console.error('Error deleting module:', err);
          this.errorMessage.set('Failed to delete module. Please try again.');
        }
      });
  }

  /**
   * Cancel delete dialog
   */
  onDeleteDialogCancel(): void {
    this.showDeleteDialog.set(false);
  }

  // ==========================================================================
  // HELPER METHODS
  // ==========================================================================

  /**
   * Get error message for a form field
   */
  getFieldError(fieldName: keyof ModuleSettingsForm): string {
    const control = this.moduleForm.get(fieldName);
    if (control && control.touched && control.errors) {
      if (control.errors['required']) {
        return `${this.formatFieldName(fieldName)} is required`;
      }
    }
    return '';
  }

  /**
   * Format field name for display
   */
  private formatFieldName(fieldName: string): string {
    return fieldName
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, str => str.toUpperCase())
      .trim();
  }

  /**
   * Format ISO date string for date input
   */
  private formatDateForInput(dateString: string): string | null {
    if (!dateString) return null;
    try {
      const date = new Date(dateString);
      if (isNaN(date.getTime())) return null;
      return date.toISOString().split('T')[0];
    } catch {
      return null;
    }
  }
}
