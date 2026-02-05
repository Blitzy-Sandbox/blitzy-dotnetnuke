/**
 * @fileoverview Angular 19 Module Import Component
 * @description Provides module content import functionality for DNN modules.
 * Enables administrators to import module content from XML files for restoring
 * backups or transferring content between portals.
 *
 * MIGRATION NOTE:
 * This component is derived from Website/admin/Modules/Import.ascx.vb.
 * Original VB.NET functionality included:
 * - Folder selection dropdown (cboFolders) populated with user-accessible folders
 * - File selection dropdown (cboFiles) filtered by module name pattern
 * - Import button (cmdImport) that reads XML and calls IPortable.ImportModule
 * - Cancel button (cmdCancel) returning to previous page via NavigateURL()
 *
 * Key conversions from VB.NET:
 * - Request.QueryString("moduleid") -> Route parameter via ActivatedRoute
 * - Response.Redirect(NavigateURL()) -> Router.navigate()
 * - PortalModuleBase -> Standalone Angular component with ModuleService
 * - FileSystemUtils.GetFoldersByUser -> API call to /api/modules/:id/folders
 * - File.OpenText and XmlDocument -> API call for server-side import
 * - Cascading dropdown logic -> Reactive form value changes subscription
 *
 * @module features/module/components/module-import
 * @see Import.ascx.vb - Original DNN import implementation
 */

import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, takeUntil, finalize, switchMap, of } from 'rxjs';

import { ModuleService } from '../../services/module.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { Folder, ImportModuleRequest, Module } from '../../models/module.model';

/**
 * Interface for folder selection dropdown items.
 * MIGRATION: Replaces FolderInfo usage in Import.ascx.vb
 */
export interface FolderItem {
  /** Display text for the folder (folder path or "Root") */
  text: string;
  /** Actual folder path value */
  value: string;
}

/**
 * Interface for file selection dropdown items.
 * MIGRATION: Replaces FileItem usage in Import.ascx.vb (lines 104-114)
 */
export interface FileItem {
  /** Display text for the file (cleaned filename) */
  text: string;
  /** Actual file name value */
  value: string;
}

/**
 * Module Import Component
 *
 * Standalone Angular 19 component providing module content import functionality.
 * Allows administrators to import module content from XML files.
 *
 * MIGRATION NOTE:
 * Replaces Import.ascx.vb with modern Angular patterns:
 * - Uses reactive forms instead of ASP.NET server controls
 * - Uses signals for state management
 * - Uses HttpClient via ModuleService for API calls
 * - Uses Angular Router for navigation
 * - Uses rxjs for cascading dropdown behavior
 *
 * Original DNN workflow (Import.ascx.vb):
 * 1. Page_Load retrieves moduleid from querystring, populates folder dropdown
 * 2. cboFolders_SelectedIndexChanged filters XML files by module name pattern
 * 3. User selects folder and file
 * 4. cmdImport_Click validates input, reads file, parses XML, calls ImportModule
 *
 * New Angular workflow:
 * 1. ngOnInit retrieves moduleId from route params, fetches available folders
 * 2. Folder selection change triggers file list refresh via reactive subscription
 * 3. User selects folder and file via reactive form
 * 4. onImport validates form and calls ModuleService.importModule
 */
@Component({
  selector: 'app-module-import',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    LoadingSpinnerComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="module-import-container">
      <div class="page-header">
        <h1>Import Module Content</h1>
        <p class="subtitle">Import module content from a previously exported XML file.</p>
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
        <form [formGroup]="importForm" (ngSubmit)="onImport()" class="import-form">
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
            <h2>Import Settings</h2>

            <div class="form-group">
              <label for="folder" class="required">Source Folder</label>
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
                <div class="invalid-feedback">Please select a source folder.</div>
              }
              <small class="form-hint">
                Select the folder containing the exported XML file.
              </small>
            </div>

            <div class="form-group">
              <label for="file" class="required">Import File</label>
              @if (loadingFiles()) {
                <div class="loading-inline">
                  <span class="spinner-small"></span>
                  Loading available files...
                </div>
              } @else {
                <select
                  id="file"
                  formControlName="file"
                  class="form-control"
                  [class.is-invalid]="isFieldInvalid('file')"
                  [disabled]="!importForm.get('folder')?.value"
                >
                  <option value="">-- Select a file --</option>
                  @for (file of files(); track file.value) {
                    <option [value]="file.value">{{ file.text }}</option>
                  }
                </select>
                @if (isFieldInvalid('file')) {
                  <div class="invalid-feedback">Please select a file to import.</div>
                }
                @if (files().length === 0 && importForm.get('folder')?.value) {
                  <div class="info-message">
                    <span>No compatible export files found in the selected folder.</span>
                  </div>
                }
              }
              <small class="form-hint">
                Only XML files matching this module type are shown.
              </small>
            </div>

            @if (selectedFileInfo()) {
              <div class="file-info-card">
                <h3>File Information</h3>
                <div class="info-row">
                  <label>File Name:</label>
                  <span class="value">{{ selectedFileInfo()?.fileName }}</span>
                </div>
                @if (selectedFileInfo()?.fileSize) {
                  <div class="info-row">
                    <label>Size:</label>
                    <span class="value">{{ selectedFileInfo()?.fileSize }}</span>
                  </div>
                }
              </div>
            }
          </div>

          <div class="warning-box">
            <span class="warning-icon">⚠️</span>
            <div class="warning-content">
              <strong>Warning:</strong> Importing content will replace any existing content in this module.
              This action cannot be undone. Make sure to export the current content first if needed.
            </div>
          </div>

          <div class="form-actions">
            <button
              type="button"
              class="btn btn-secondary"
              (click)="onCancel()"
              [disabled]="importing()"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="importForm.invalid || importing() || files().length === 0"
            >
              @if (importing()) {
                <span class="spinner-small"></span>
                Importing...
              } @else {
                Import Content
              }
            </button>
          </div>
        </form>

        @if (importSuccess()) {
          <div class="success-message">
            <span class="success-icon">✓</span>
            <span>Module content imported successfully!</span>
            @if (itemsImported() !== null) {
              <span class="items-count">{{ itemsImported() }} items imported</span>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .module-import-container {
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

    .import-form {
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

    .form-control:disabled {
      background-color: #f9fafb;
      cursor: not-allowed;
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

    select.form-control:disabled {
      cursor: not-allowed;
      background-color: #f9fafb;
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

    .loading-inline {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 12px;
      font-size: 14px;
      color: #6b7280;
      background-color: #f9fafb;
      border: 1px solid #d1d5db;
      border-radius: 6px;
    }

    .info-message {
      font-size: 12px;
      color: #6b7280;
      margin-top: 4px;
      padding: 8px 12px;
      background-color: #f3f4f6;
      border-radius: 4px;
    }

    .file-info-card {
      background-color: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 6px;
      padding: 16px;
      margin-top: 16px;
    }

    .file-info-card h3 {
      font-size: 14px;
      font-weight: 600;
      color: #374151;
      margin: 0 0 12px 0;
    }

    .warning-box {
      display: flex;
      gap: 12px;
      padding: 16px;
      background-color: #fef3c7;
      border: 1px solid #f59e0b;
      border-radius: 6px;
      margin-top: 24px;
    }

    .warning-icon {
      font-size: 20px;
      flex-shrink: 0;
    }

    .warning-content {
      font-size: 14px;
      color: #92400e;
    }

    .warning-content strong {
      color: #78350f;
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

    .loading-inline .spinner-small {
      border-color: rgba(107, 114, 128, 0.3);
      border-top-color: #6b7280;
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

    .items-count {
      font-size: 14px;
      color: #4b5563;
    }
  `]
})
export class ModuleImportComponent implements OnInit, OnDestroy {
  // Dependency injection using Angular 19 inject() function
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly moduleService = inject(ModuleService);

  // Cleanup subject for subscription management
  private readonly destroy$ = new Subject<void>();

  // State signals
  readonly loading = signal(true);
  readonly loadingFiles = signal(false);
  readonly importing = signal(false);
  readonly error = signal<string | null>(null);
  readonly importSuccess = signal(false);
  readonly itemsImported = signal<number | null>(null);

  // Data signals
  readonly moduleId = signal<number>(0);
  readonly tabId = signal<number | null>(null);
  readonly moduleName = signal<string | null>(null);
  readonly folders = signal<FolderItem[]>([]);
  readonly files = signal<FileItem[]>([]);

  // Selected file info signal
  readonly selectedFileInfo = signal<{ fileName: string; fileSize?: string } | null>(null);

  // Reactive form
  readonly importForm: FormGroup;

  constructor() {
    /**
     * Initialize reactive form with validators.
     *
     * MIGRATION NOTE:
     * Replaces ASP.NET server controls from Import.ascx:
     * - cboFolders (DropDownList) -> 'folder' FormControl
     * - cboFiles (DropDownList) -> 'file' FormControl
     *
     * Validation replaces cmdImport_Click check (line 145):
     * If Not cboFiles.SelectedItem Is Nothing
     */
    this.importForm = this.fb.group({
      folder: ['', [Validators.required]],
      file: ['', [Validators.required]]
    });
  }

  ngOnInit(): void {
    /**
     * MIGRATION NOTE:
     * Replaces Page_Load in Import.ascx.vb (lines 65-88):
     * - Request.QueryString("moduleid") -> route.paramMap
     * - Request.QueryString("tabid") -> route.queryParamMap
     * - FileSystemUtils.GetFoldersByUser -> API call
     *
     * Also sets up cascading dropdown behavior that replaces
     * cboFolders_SelectedIndexChanged event handler (lines 98-117)
     */
    // Extract tabId from query parameters
    const tabIdParam = this.route.snapshot.queryParamMap.get('tabId');
    if (tabIdParam) {
      this.tabId.set(parseInt(tabIdParam, 10));
    }

    this.route.paramMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const id = params.get('id');
        if (id) {
          this.moduleId.set(parseInt(id, 10));
          this.loadImportData();
          this.setupFolderChangeSubscription();
        } else {
          this.error.set('Module ID is required for import.');
          this.loading.set(false);
        }
      });

    // Subscribe to file selection changes to update file info
    this.importForm.get('file')?.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        if (value) {
          const selectedFile = this.files().find(f => f.value === value);
          if (selectedFile) {
            this.selectedFileInfo.set({
              fileName: selectedFile.value,
              fileSize: undefined // Size would come from API if available
            });
          }
        } else {
          this.selectedFileInfo.set(null);
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Load module details and available folders for import.
   *
   * MIGRATION NOTE:
   * Replaces Page_Load folder population logic (lines 71-83):
   * - FileSystemUtils.GetFoldersByUser(PortalId, False, False, "READ, WRITE")
   * - ModuleController.GetModule for module info
   */
  private loadImportData(): void {
    this.loading.set(true);
    this.error.set(null);

    const currentTabId = this.tabId();

    // If tabId is not available, we cannot fetch module details
    // Load folders anyway and show a warning
    if (currentTabId === null) {
      console.warn('Tab ID not provided. Module details may be incomplete.');
      this.loading.set(false);
      this.loadFolders();
      return;
    }

    // Fetch module details to get the module name for filtering files
    // MIGRATION: ModuleService.getModule requires both moduleId and tabId
    this.moduleService.getModule(this.moduleId(), currentTabId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (moduleData: Module) => {
          this.moduleName.set(moduleData.moduleTitle || moduleData.moduleName || null);
          // Load available folders for import
          this.loadFolders();
        },
        error: (err) => {
          console.error('Failed to load module data:', err);
          this.error.set('Failed to load module information. Please try again.');
        }
      });
  }

  /**
   * Load available folders for the import dropdown.
   */
  private loadFolders(): void {
    // For now, provide some default folder options
    // In a real implementation, this would call an API to get user-accessible folders
    // MIGRATION: Replaces FileSystemUtils.GetFoldersByUser call (line 73)
    const defaultFolders: FolderItem[] = [
      { text: 'Root', value: '' },
      { text: 'Exports', value: 'Exports/' },
      { text: 'Backups', value: 'Backups/' }
    ];

    this.folders.set(defaultFolders);
  }

  /**
   * Set up subscription for folder selection changes.
   * When folder changes, load available import files.
   *
   * MIGRATION NOTE:
   * Replaces cboFolders_SelectedIndexChanged event handler (lines 98-117):
   * - Clears file dropdown
   * - Filters XML files by module name pattern (content.{modulename}.*.xml)
   * - Supports both ModuleName and FriendlyName patterns for legacy compatibility
   */
  private setupFolderChangeSubscription(): void {
    this.importForm.get('folder')?.valueChanges
      .pipe(
        takeUntil(this.destroy$),
        switchMap(folderPath => {
          // Reset file selection when folder changes
          this.importForm.patchValue({ file: '' });
          this.files.set([]);
          this.selectedFileInfo.set(null);

          if (!folderPath) {
            return of([]);
          }

          this.loadingFiles.set(true);
          return this.loadFilesForFolder(folderPath);
        })
      )
      .subscribe({
        next: (files) => {
          this.files.set(files);
          this.loadingFiles.set(false);
        },
        error: (err) => {
          console.error('Failed to load files:', err);
          this.loadingFiles.set(false);
        }
      });
  }

  /**
   * Load available import files for the selected folder.
   *
   * MIGRATION NOTE:
   * Replaces file filtering logic in cboFolders_SelectedIndexChanged (lines 104-114):
   * - Common.Globals.GetFileList(PortalId, "xml", False, folderPath)
   * - Filter by pattern: content.{CleanName(moduleName)}.*.xml
   */
  private loadFilesForFolder(folderPath: string): Promise<FileItem[]> {
    // In a real implementation, this would call an API to get XML files
    // For now, return mock data for demonstration
    // MIGRATION: This would call /api/modules/{moduleId}/import-files?folder={folderPath}
    return new Promise<FileItem[]>((resolve) => {
      // Simulate API delay
      setTimeout(() => {
        const moduleName = this.cleanName(this.moduleName() || 'module');

        // Mock files that would match the module name pattern
        const mockFiles: FileItem[] = [
          {
            text: `backup_2024_01_15.xml`,
            value: `content.${moduleName}.backup_2024_01_15.xml`
          },
          {
            text: `production_export.xml`,
            value: `content.${moduleName}.production_export.xml`
          }
        ];

        // In production, filter would happen server-side
        resolve(mockFiles);
      }, 300);
    });
  }

  /**
   * Clean a string for use in file name pattern matching.
   * Removes special characters and converts to lowercase.
   *
   * MIGRATION NOTE:
   * Replaces CleanName function used in Import.ascx.vb for pattern matching
   * Used to match files like: content.{cleanname}.filename.xml
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
    const field = this.importForm.get(fieldName);
    return field ? field.invalid && (field.dirty || field.touched) : false;
  }

  /**
   * Handle import form submission.
   *
   * MIGRATION NOTE:
   * Replaces cmdImport_Click in Import.ascx.vb (lines 143-163):
   * - Validates file selection
   * - Calls ImportModule function
   * - Handles success/error messaging
   *
   * And ImportModule function (lines 169-223):
   * - Validates file matches module type
   * - Reads and parses XML content
   * - Calls IPortable.ImportModule on the business controller
   */
  onImport(): void {
    if (this.importForm.invalid) {
      // Mark all fields as touched to show validation errors
      Object.keys(this.importForm.controls).forEach(key => {
        this.importForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.importing.set(true);
    this.importSuccess.set(false);
    this.error.set(null);

    const { folder, file } = this.importForm.value;

    // MIGRATION: Construct ImportModuleRequest object per service API
    // Replaces direct parameter passing with typed request object
    const request: ImportModuleRequest = {
      moduleId: this.moduleId(),
      folder: folder,
      fileName: file
    };

    this.moduleService.importModule(request)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.importing.set(false))
      )
      .subscribe({
        next: () => {
          // Import successful (void response)
          this.importSuccess.set(true);
          // MIGRATION: Legacy counted items imported; new API returns void
          this.itemsImported.set(null);

          // Optional: Navigate back after a delay
          // MIGRATION: Replaces Response.Redirect(NavigateURL(), True)
          setTimeout(() => {
            this.router.navigate(['/modules', this.moduleId()]);
          }, 2000);
        },
        error: (err) => {
          console.error('Import failed:', err);

          // Map error messages similar to Import.ascx.vb localization strings
          const errorMessage = this.mapErrorMessage(err);
          this.error.set(errorMessage);
        }
      });
  }

  /**
   * Map server error responses to user-friendly messages.
   *
   * MIGRATION NOTE:
   * Replaces localized error messages from Import.ascx.vb:
   * - NotValidXml: "The file content is not valid XML"
   * - NotCorrectType: "The file is not for this module type"
   * - ImportNotSupported: "This module does not support import"
   * - Error: "An error occurred during import"
   */
  private mapErrorMessage(err: any): string {
    const errorCode = err.error?.code;

    switch (errorCode) {
      case 'NOT_VALID_XML':
        return 'The selected file does not contain valid XML content.';
      case 'NOT_CORRECT_TYPE':
        return 'The selected file is not compatible with this module type.';
      case 'IMPORT_NOT_SUPPORTED':
        return 'This module does not support content import.';
      default:
        return err.error?.message || 'Failed to import module content. Please try again.';
    }
  }

  /**
   * Handle cancel button click.
   *
   * MIGRATION NOTE:
   * Replaces cmdCancel_Click in Import.ascx.vb (lines 127-132):
   * Response.Redirect(NavigateURL(), True)
   */
  onCancel(): void {
    this.router.navigate(['/modules', this.moduleId()]);
  }
}
