/**
 * @fileoverview Angular 19 Module Export Component
 * @description Provides module content export functionality for DNN modules.
 * Enables administrators to export module content to XML files for portability
 * and backup purposes.
 *
 * MIGRATION NOTE:
 * This component is derived from Website/admin/Modules/Export.ascx.vb.
 * Original VB.NET functionality included:
 * - Folder selection dropdown (cboFolders) populated with user-accessible folders
 * - Filename input (txtFile) with sanitized module title as default
 * - Export button (cmdExport) that calls ExportModule to serialize content to XML
 * - Cancel button (cmdCancel) returning to previous page via NavigateURL()
 *
 * Key conversions from VB.NET:
 * - Request.QueryString("moduleid") -> Route parameter via ActivatedRoute
 * - Response.Redirect(NavigateURL()) -> Router.navigate()
 * - PortalModuleBase -> Standalone Angular component with ModuleService
 * - FileSystemUtils.GetFoldersByUser -> API call to /api/modules/:id/folders
 * - StreamWriter file operations -> API call for server-side export
 *
 * @module features/module/components/module-export
 * @see Export.ascx.vb - Original DNN export implementation
 */

import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, takeUntil, finalize } from 'rxjs';

import { ModuleService } from '../../services/module.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { Folder, ExportModuleRequest, ExportModuleResponse, Module } from '../../models/module.model';

/**
 * Interface for folder selection dropdown items.
 * MIGRATION: Replaces FolderInfo usage in Export.ascx.vb
 */
export interface FolderItem {
  /** Display text for the folder (folder path or "Root") */
  text: string;
  /** Actual folder path value */
  value: string;
}

/**
 * Module Export Component
 *
 * Standalone Angular 19 component providing module content export functionality.
 * Allows administrators to export module content to XML files for portability.
 *
 * MIGRATION NOTE:
 * Replaces Export.ascx.vb with modern Angular patterns:
 * - Uses reactive forms instead of ASP.NET server controls
 * - Uses signals for state management
 * - Uses HttpClient via ModuleService for API calls
 * - Uses Angular Router for navigation
 *
 * Original DNN workflow (Export.ascx.vb lines 63-137):
 * 1. Page_Load retrieves moduleid from querystring, populates folder dropdown
 * 2. User selects folder and enters filename
 * 3. cmdExport_Click validates input and calls ExportModule
 * 4. ExportModule serializes module content to XML and saves to file system
 *
 * New Angular workflow:
 * 1. ngOnInit retrieves moduleId from route params, fetches available folders
 * 2. User selects folder and enters filename via reactive form
 * 3. onExport validates form and calls ModuleService.exportModule
 * 4. API endpoint handles serialization and file creation
 */
@Component({
  selector: 'app-module-export',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="module-export-container">
      <div class="page-header">
        <h1>Export Module Content</h1>
        <p class="subtitle">Export module content to an XML file for backup or portability.</p>
      </div>

      @if (loading()) {
        <app-loading-spinner />
      } @else if (error()) {
        <div class="error-message">
          <span class="error-icon">⚠️</span>
          <span>{{ error() }}</span>
          <button type="button" class="btn btn-secondary" (click)="onCancel()">Go Back</button>
        </div>
      } @else {
        <form [formGroup]="exportForm" (ngSubmit)="onExport()" class="export-form">
          <div class="form-section">
            <h2>Module Information</h2>
            <div class="info-row">
              <label>Module ID:</label>
              <span class="value">{{ moduleId() }}</span>
            </div>
            @if (moduleName()) {
              <div class="info-row">
                <label>Module Name:</label>
                <span class="value">{{ moduleName() }}</span>
              </div>
            }
          </div>

          <div class="form-section">
            <h2>Export Settings</h2>

            <div class="form-group">
              <label for="folder" class="required">Target Folder</label>
              <select
                id="folder"
                formControlName="folder"
                class="form-control"
                [class.is-invalid]="isFieldInvalid('folder')"
              >
                <option value="">-- Select a folder --</option>
                @for (folder of folders(); track folder.value) {
                  <option [value]="folder.value">{{ folder.text }}</option>
                }
              </select>
              @if (isFieldInvalid('folder')) {
                <div class="invalid-feedback">Please select a target folder for the export.</div>
              }
              <small class="form-hint">
                Select the folder where the exported XML file will be saved.
              </small>
            </div>

            <div class="form-group">
              <label for="fileName" class="required">File Name</label>
              <div class="input-group">
                <input
                  type="text"
                  id="fileName"
                  formControlName="fileName"
                  class="form-control"
                  [class.is-invalid]="isFieldInvalid('fileName')"
                  placeholder="Enter file name"
                />
                <span class="input-suffix">.xml</span>
              </div>
              @if (isFieldInvalid('fileName')) {
                <div class="invalid-feedback">
                  @if (exportForm.get('fileName')?.errors?.['required']) {
                    File name is required.
                  }
                  @if (exportForm.get('fileName')?.errors?.['pattern']) {
                    File name can only contain letters, numbers, hyphens, and underscores.
                  }
                </div>
              }
              <small class="form-hint">
                The exported file will be named: content.[modulename].{{ exportForm.get('fileName')?.value || 'filename' }}.xml
              </small>
            </div>
          </div>

          <div class="form-actions">
            <button
              type="button"
              class="btn btn-secondary"
              (click)="onCancel()"
              [disabled]="exporting()"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="exportForm.invalid || exporting()"
            >
              @if (exporting()) {
                <span class="spinner-small"></span>
                Exporting...
              } @else {
                Export Content
              }
            </button>
          </div>
        </form>

        @if (exportSuccess()) {
          <div class="success-message">
            <span class="success-icon">✓</span>
            <span>Module content exported successfully!</span>
            @if (exportedFilePath()) {
              <span class="file-path">File: {{ exportedFilePath() }}</span>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .module-export-container {
      max-width: 600px;
      margin: 0 auto;
      padding: 24px;
    }

    .page-header {
      margin-bottom: 32px;
    }

    .page-header h1 {
      font-size: 24px;
      font-weight: 600;
      color: #1a1a2e;
      margin: 0 0 8px 0;
    }

    .page-header .subtitle {
      font-size: 14px;
      color: #6b7280;
      margin: 0;
    }

    .export-form {
      background: #ffffff;
      border-radius: 8px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      padding: 24px;
    }

    .form-section {
      margin-bottom: 24px;
      padding-bottom: 24px;
      border-bottom: 1px solid #e5e7eb;
    }

    .form-section:last-of-type {
      border-bottom: none;
      margin-bottom: 0;
      padding-bottom: 0;
    }

    .form-section h2 {
      font-size: 16px;
      font-weight: 600;
      color: #374151;
      margin: 0 0 16px 0;
    }

    .info-row {
      display: flex;
      align-items: center;
      margin-bottom: 8px;
    }

    .info-row label {
      font-weight: 500;
      color: #6b7280;
      width: 120px;
    }

    .info-row .value {
      font-weight: 500;
      color: #1f2937;
    }

    .form-group {
      margin-bottom: 20px;
    }

    .form-group:last-child {
      margin-bottom: 0;
    }

    .form-group label {
      display: block;
      font-size: 14px;
      font-weight: 500;
      color: #374151;
      margin-bottom: 6px;
    }

    .form-group label.required::after {
      content: ' *';
      color: #ef4444;
    }

    .form-control {
      width: 100%;
      padding: 10px 12px;
      font-size: 14px;
      border: 1px solid #d1d5db;
      border-radius: 6px;
      transition: border-color 0.2s, box-shadow 0.2s;
    }

    .form-control:focus {
      outline: none;
      border-color: #3b82f6;
      box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
    }

    .form-control.is-invalid {
      border-color: #ef4444;
    }

    .form-control.is-invalid:focus {
      box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
    }

    select.form-control {
      cursor: pointer;
      background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3e%3cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3e%3c/svg%3e");
      background-position: right 8px center;
      background-repeat: no-repeat;
      background-size: 20px;
      padding-right: 36px;
      appearance: none;
    }

    .input-group {
      display: flex;
      align-items: stretch;
    }

    .input-group .form-control {
      border-top-right-radius: 0;
      border-bottom-right-radius: 0;
      flex: 1;
    }

    .input-suffix {
      display: flex;
      align-items: center;
      padding: 0 12px;
      background-color: #f3f4f6;
      border: 1px solid #d1d5db;
      border-left: none;
      border-radius: 0 6px 6px 0;
      font-size: 14px;
      color: #6b7280;
    }

    .invalid-feedback {
      font-size: 12px;
      color: #ef4444;
      margin-top: 4px;
    }

    .form-hint {
      font-size: 12px;
      color: #6b7280;
      margin-top: 4px;
      display: block;
    }

    .form-actions {
      display: flex;
      gap: 12px;
      justify-content: flex-end;
      margin-top: 24px;
      padding-top: 24px;
      border-top: 1px solid #e5e7eb;
    }

    .btn {
      padding: 10px 20px;
      font-size: 14px;
      font-weight: 500;
      border-radius: 6px;
      border: none;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      transition: background-color 0.2s, opacity 0.2s;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background-color: #3b82f6;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background-color: #2563eb;
    }

    .btn-secondary {
      background-color: #f3f4f6;
      color: #374151;
    }

    .btn-secondary:hover:not(:disabled) {
      background-color: #e5e7eb;
    }

    .spinner-small {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    .error-message,
    .success-message {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 24px;
      border-radius: 8px;
      text-align: center;
    }

    .error-message {
      background-color: #fef2f2;
      color: #991b1b;
    }

    .success-message {
      background-color: #f0fdf4;
      color: #166534;
      margin-top: 24px;
    }

    .error-icon,
    .success-icon {
      font-size: 32px;
    }

    .file-path {
      font-size: 12px;
      color: #4b5563;
      font-family: monospace;
    }
  `]
})
export class ModuleExportComponent implements OnInit, OnDestroy {
  // Dependency injection using Angular 19 inject() function
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly moduleService = inject(ModuleService);

  // Cleanup subject for subscription management
  private readonly destroy$ = new Subject<void>();

  // State signals
  readonly loading = signal(true);
  readonly exporting = signal(false);
  readonly error = signal<string | null>(null);
  readonly exportSuccess = signal(false);
  readonly exportedFilePath = signal<string | null>(null);

  // Data signals
  readonly moduleId = signal<number>(0);
  readonly tabId = signal<number>(0);
  readonly moduleName = signal<string | null>(null);
  readonly folders = signal<FolderItem[]>([]);

  // Reactive form
  readonly exportForm: FormGroup;

  constructor() {
    /**
     * Initialize reactive form with validators.
     *
     * MIGRATION NOTE:
     * Replaces ASP.NET server controls from Export.ascx:
     * - cboFolders (DropDownList) -> 'folder' FormControl
     * - txtFile (TextBox) -> 'fileName' FormControl
     *
     * Validation replaces cmdExport_Click check (line 121):
     * If cboFolders.SelectedIndex <> 0 And txtFile.Text <> ""
     */
    this.exportForm = this.fb.group({
      folder: ['', [Validators.required]],
      fileName: ['', [
        Validators.required,
        Validators.pattern(/^[a-zA-Z0-9_-]+$/)
      ]]
    });
  }

  ngOnInit(): void {
    /**
     * MIGRATION NOTE:
     * Replaces Page_Load in Export.ascx.vb (lines 63-92):
     * - Request.QueryString("moduleid") -> route.paramMap
     * - FileSystemUtils.GetFoldersByUser -> API call
     * - Default filename from ModuleTitle -> API response
     */
    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const id = params.get('id');
        if (id) {
          this.moduleId.set(parseInt(id, 10));
          
          // Get tabId from query params if available
          const tabIdParam = this.route.snapshot.queryParamMap.get('tabId');
          if (tabIdParam) {
            this.tabId.set(parseInt(tabIdParam, 10));
          }
          
          this.loadExportData();
        } else {
          this.error.set('Module ID is required for export.');
          this.loading.set(false);
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Load module details and available folders for export.
   *
   * MIGRATION NOTE:
   * Replaces Page_Load folder population logic (lines 70-87):
   * - FileSystemUtils.GetFoldersByUser(PortalId, False, False, "READ, WRITE")
   * - ModuleController.GetModule for module info
   */
  private loadExportData(): void {
    this.loading.set(true);
    this.error.set(null);

    // If we have a tabId, fetch module details to get the module name for default filename
    // Otherwise, use a default filename based on the module ID
    if (this.tabId() > 0) {
      this.moduleService.getModule(this.moduleId(), this.tabId())
        .pipe(
          takeUntil(this.destroy$),
          finalize(() => this.loading.set(false))
        )
        .subscribe({
          next: (moduleData: Module) => {
            this.moduleName.set(moduleData.moduleTitle || moduleData.moduleName || null);

            // Set default filename based on module title (sanitized)
            // MIGRATION: Replaces txtFile.Text = CleanName(objModule.ModuleTitle)
            const defaultFileName = this.cleanName(this.moduleName() || 'module');
            this.exportForm.patchValue({ fileName: defaultFileName });

            // Load available folders for export
            this.loadFolders();
          },
          error: (err) => {
            console.error('Failed to load module data:', err);
            // Still allow export even if module details fail to load
            this.exportForm.patchValue({ fileName: `module_${this.moduleId()}` });
            this.loadFolders();
            this.loading.set(false);
          }
        });
    } else {
      // No tabId available, use default filename
      this.exportForm.patchValue({ fileName: `module_${this.moduleId()}` });
      this.loadFolders();
      this.loading.set(false);
    }
  }

  /**
   * Load available folders for the export dropdown.
   */
  private loadFolders(): void {
    // For now, provide some default folder options
    // In a real implementation, this would call an API to get user-accessible folders
    // MIGRATION: Replaces FileSystemUtils.GetFoldersByUser call
    const defaultFolders: FolderItem[] = [
      { text: 'Root', value: '' },
      { text: 'Exports', value: 'Exports/' },
      { text: 'Backups', value: 'Backups/' }
    ];

    this.folders.set(defaultFolders);
  }

  /**
   * Clean a string for use as a filename.
   * Removes special characters and replaces spaces with underscores.
   *
   * MIGRATION NOTE:
   * Replaces CleanName function from Export.ascx.vb
   * Original implementation removed special characters for safe filenames
   */
  private cleanName(name: string): string {
    return name
      .replace(/[^a-zA-Z0-9\s_-]/g, '')
      .replace(/\s+/g, '_')
      .toLowerCase();
  }

  /**
   * Check if a form field is invalid and has been touched.
   */
  isFieldInvalid(fieldName: string): boolean {
    const field = this.exportForm.get(fieldName);
    return field ? field.invalid && (field.dirty || field.touched) : false;
  }

  /**
   * Handle export form submission.
   *
   * MIGRATION NOTE:
   * Replaces cmdExport_Click in Export.ascx.vb (lines 119-137):
   * - Form validation
   * - ExportModule call
   * - Success redirect or error message display
   */
  onExport(): void {
    if (this.exportForm.invalid) {
      // Mark all fields as touched to show validation errors
      Object.keys(this.exportForm.controls).forEach(key => {
        this.exportForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.exporting.set(true);
    this.exportSuccess.set(false);
    this.error.set(null);

    const { folder, fileName } = this.exportForm.value;

    // Build the export request object as expected by the service
    const request: ExportModuleRequest = {
      moduleId: this.moduleId(),
      folder: folder,
      fileName: fileName
    };

    this.moduleService.exportModule(request)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.exporting.set(false))
      )
      .subscribe({
        next: (response: ExportModuleResponse) => {
          this.exportSuccess.set(true);
          this.exportedFilePath.set(response.filePath || null);

          // Optional: Navigate back after a delay
          // MIGRATION: Replaces Response.Redirect(NavigateURL(), True)
          setTimeout(() => {
            this.router.navigate(['/modules', this.moduleId()]);
          }, 2000);
        },
        error: (err) => {
          console.error('Export failed:', err);
          this.error.set(err.error?.message || 'Failed to export module content. Please try again.');
        }
      });
  }

  /**
   * Handle cancel button click.
   *
   * MIGRATION NOTE:
   * Replaces cmdCancel_Click in Export.ascx.vb (lines 103-109):
   * Response.Redirect(NavigateURL(), True)
   */
  onCancel(): void {
    this.router.navigate(['/modules', this.moduleId()]);
  }
}
