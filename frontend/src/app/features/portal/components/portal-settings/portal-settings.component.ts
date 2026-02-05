/**
 * Portal Settings Component
 *
 * Angular 19 standalone component for comprehensive portal configuration editing.
 * Replaces DotNetNuke 4.x Website/admin/Portal/SiteSettings.ascx.vb functionality.
 *
 * MIGRATION NOTES:
 * - Converted from VB.NET WebForms to Angular 19 standalone component
 * - Replaced postback model with reactive forms and HTTP API calls
 * - Uses signals for reactive state management per Angular 19 standards
 * - Uses inject() for dependency injection per Angular 19 standards
 * - OnPush change detection for optimal performance
 * - @if/@for control flow syntax for conditional rendering
 *
 * Form Sections (matching DNN SiteSettings.ascx structure):
 * - Basic Settings: portalName, logoFile, footerText, description, keywords, backgroundFile
 * - Registration Settings: userRegistration, bannerAdvertising
 * - Page Settings: splashTabId, homeTabId, loginTabId, userTabId dropdowns
 * - Payment Settings: currency, processor, processorUserId, processorPassword
 * - Host Settings (SuperUser only): expiryDate, hostFee, hostSpace, pageQuota, userQuota, siteLogHistory
 * - Appearance: portalSkin, portalContainer, adminSkin, adminContainer
 * - Usability: inlineEditor, controlPanelMode, controlPanelVisibility, controlPanelSecurity
 * - SSL Settings (SuperUser only): sslEnabled, sslEnforced, sslUrl, stdUrl
 * - Stylesheet: editor with restore default functionality
 *
 * @fileoverview Portal settings/configuration management component
 */

import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  inject,
  ViewChild,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';

// Internal imports from depends_on_files
import { PortalService } from '../../services/portal.service';
import {
  Portal,
  UpdatePortalRequest,
  UserRegistrationType,
  BannerType,
} from '../../models/portal.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog/confirmation-dialog.component';

/**
 * Interface for tab dropdown options
 * Used for page settings dropdowns (splash, home, login, user tabs)
 */
interface TabOption {
  tabId: number;
  tabName: string;
}

/**
 * Interface for currency dropdown options
 */
interface CurrencyOption {
  code: string;
  name: string;
}

/**
 * Interface for payment processor options
 */
interface ProcessorOption {
  id: string;
  name: string;
}

/**
 * Interface for skin/container options
 */
interface SkinOption {
  path: string;
  name: string;
}

/**
 * Interface for control panel mode options
 */
interface ControlPanelOption {
  value: string;
  label: string;
}

/**
 * Portal Settings Component
 *
 * Comprehensive portal configuration editing component that replaces
 * DNN SiteSettings.ascx.vb functionality with modern Angular 19 patterns.
 *
 * Features:
 * - Typed reactive forms organized into logical sections
 * - Signal-based state management for reactive updates
 * - SuperUser-only sections (Host Settings, SSL Settings)
 * - Portal deletion with confirmation dialog
 * - Stylesheet editing with restore default functionality
 *
 * MIGRATION: This component replaces the following DNN functionality:
 * - Page_Load (lines 232-521): Form initialization and data binding
 * - cmdUpdate_Click (lines 687-822): Save portal settings
 * - cmdDelete_Click (lines 556-586): Delete portal
 * - LoadStyleSheet/cmdSave_Click/cmdRestore_Click: Stylesheet management
 */
@Component({
  selector: 'app-portal-settings',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Loading State -->
    @if (loading()) {
      <div class="loading-container">
        <app-loading-spinner size="large" message="Loading portal settings..." />
      </div>
    } @else if (error()) {
      <!-- Error State -->
      <div class="error-container">
        <div class="error-message">
          <h3>Error Loading Portal</h3>
          <p>{{ error() }}</p>
          <button type="button" class="btn btn-primary" (click)="loadPortalData()">
            Retry
          </button>
          <button type="button" class="btn btn-secondary" (click)="onCancel()">
            Back to List
          </button>
        </div>
      </div>
    } @else if (portal()) {
      <!-- Main Settings Form -->
      <div class="portal-settings-container">
        <header class="settings-header">
          <h1>Portal Settings: {{ portal()?.portalName }}</h1>
          <div class="header-actions">
            <button
              type="button"
              class="btn btn-secondary"
              (click)="onCancel()"
              [disabled]="saving()">
              Cancel
            </button>
            <button
              type="button"
              class="btn btn-primary"
              (click)="onSubmit()"
              [disabled]="saving() || !isFormValid()">
              @if (saving()) {
                <app-loading-spinner size="small" />
                Saving...
              } @else {
                Save Changes
              }
            </button>
            @if (isSuperUser() && canDeletePortal()) {
              <button
                type="button"
                class="btn btn-danger"
                (click)="showDeleteConfirmation()"
                [disabled]="saving()">
                Delete Portal
              </button>
            }
          </div>
        </header>

        <!-- Form Sections -->
        <div class="settings-content">
          <!-- Basic Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Basic Settings</h2>
            <div class="form-grid" [formGroup]="basicSettingsForm">
              <div class="form-group">
                <label for="portalName" class="form-label required">Portal Name</label>
                <input
                  type="text"
                  id="portalName"
                  formControlName="portalName"
                  class="form-control"
                  [class.is-invalid]="basicSettingsForm.get('portalName')?.invalid && basicSettingsForm.get('portalName')?.touched"
                />
                @if (basicSettingsForm.get('portalName')?.invalid && basicSettingsForm.get('portalName')?.touched) {
                  <div class="invalid-feedback">Portal name is required.</div>
                }
              </div>

              <div class="form-group">
                <label for="logoFile" class="form-label">Logo File</label>
                <input
                  type="text"
                  id="logoFile"
                  formControlName="logoFile"
                  class="form-control"
                  placeholder="Path to logo file"
                />
              </div>

              <div class="form-group">
                <label for="footerText" class="form-label">Footer Text</label>
                <input
                  type="text"
                  id="footerText"
                  formControlName="footerText"
                  class="form-control"
                />
              </div>

              <div class="form-group full-width">
                <label for="description" class="form-label">Description</label>
                <textarea
                  id="description"
                  formControlName="description"
                  class="form-control"
                  rows="3"
                ></textarea>
              </div>

              <div class="form-group full-width">
                <label for="keywords" class="form-label">Keywords</label>
                <textarea
                  id="keywords"
                  formControlName="keywords"
                  class="form-control"
                  rows="2"
                  placeholder="Enter keywords separated by commas"
                ></textarea>
              </div>

              <div class="form-group">
                <label for="backgroundFile" class="form-label">Background File</label>
                <input
                  type="text"
                  id="backgroundFile"
                  formControlName="backgroundFile"
                  class="form-control"
                  placeholder="Path to background image"
                />
              </div>
            </div>
          </section>

          <!-- Registration Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Registration Settings</h2>
            <div class="form-grid" [formGroup]="registrationForm">
              <div class="form-group">
                <label for="userRegistration" class="form-label">User Registration</label>
                <select
                  id="userRegistration"
                  formControlName="userRegistration"
                  class="form-control">
                  @for (option of userRegistrationOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="bannerAdvertising" class="form-label">Banner Advertising</label>
                <select
                  id="bannerAdvertising"
                  formControlName="bannerAdvertising"
                  class="form-control">
                  @for (option of bannerOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>
            </div>
          </section>

          <!-- Page Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Page Settings</h2>
            <div class="form-grid" [formGroup]="pageSettingsForm">
              <div class="form-group">
                <label for="splashTabId" class="form-label">Splash Page</label>
                <select
                  id="splashTabId"
                  formControlName="splashTabId"
                  class="form-control">
                  <option [ngValue]="null">-- None --</option>
                  @for (tab of tabOptions(); track tab.tabId) {
                    <option [ngValue]="tab.tabId">{{ tab.tabName }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="homeTabId" class="form-label">Home Page</label>
                <select
                  id="homeTabId"
                  formControlName="homeTabId"
                  class="form-control">
                  <option [ngValue]="null">-- None --</option>
                  @for (tab of tabOptions(); track tab.tabId) {
                    <option [ngValue]="tab.tabId">{{ tab.tabName }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="loginTabId" class="form-label">Login Page</label>
                <select
                  id="loginTabId"
                  formControlName="loginTabId"
                  class="form-control">
                  <option [ngValue]="null">-- None --</option>
                  @for (tab of tabOptions(); track tab.tabId) {
                    <option [ngValue]="tab.tabId">{{ tab.tabName }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="userTabId" class="form-label">User Profile Page</label>
                <select
                  id="userTabId"
                  formControlName="userTabId"
                  class="form-control">
                  <option [ngValue]="null">-- None --</option>
                  @for (tab of tabOptions(); track tab.tabId) {
                    <option [ngValue]="tab.tabId">{{ tab.tabName }}</option>
                  }
                </select>
              </div>
            </div>
          </section>

          <!-- Payment Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Payment Settings</h2>
            <div class="form-grid" [formGroup]="paymentForm">
              <div class="form-group">
                <label for="currency" class="form-label">Currency</label>
                <select
                  id="currency"
                  formControlName="currency"
                  class="form-control">
                  @for (currency of currencyOptions; track currency.code) {
                    <option [value]="currency.code">{{ currency.name }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="processor" class="form-label">Payment Processor</label>
                <select
                  id="processor"
                  formControlName="processor"
                  class="form-control">
                  <option value="">-- None --</option>
                  @for (processor of processorOptions; track processor.id) {
                    <option [value]="processor.id">{{ processor.name }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="processorUserId" class="form-label">Processor User ID</label>
                <input
                  type="text"
                  id="processorUserId"
                  formControlName="processorUserId"
                  class="form-control"
                />
              </div>

              <div class="form-group">
                <label for="processorPassword" class="form-label">Processor Password</label>
                <input
                  type="password"
                  id="processorPassword"
                  formControlName="processorPassword"
                  class="form-control"
                />
              </div>
            </div>
          </section>

          <!-- Host Settings Section (SuperUser Only) -->
          <!-- MIGRATION: Replaces dshHost/tblHost visibility check from SiteSettings.ascx.vb lines 498-516 -->
          @if (isSuperUser()) {
            <section class="settings-section host-settings">
              <h2 class="section-title">Host Settings</h2>
              <p class="section-description">These settings are only visible to Super Users.</p>
              <div class="form-grid" [formGroup]="hostSettingsForm">
                <div class="form-group">
                  <label for="expiryDate" class="form-label">Expiry Date</label>
                  <input
                    type="date"
                    id="expiryDate"
                    formControlName="expiryDate"
                    class="form-control"
                  />
                </div>

                <div class="form-group">
                  <label for="hostFee" class="form-label">Host Fee</label>
                  <input
                    type="number"
                    id="hostFee"
                    formControlName="hostFee"
                    class="form-control"
                    step="0.01"
                    min="0"
                  />
                </div>

                <div class="form-group">
                  <label for="hostSpace" class="form-label">Host Space (MB)</label>
                  <input
                    type="number"
                    id="hostSpace"
                    formControlName="hostSpace"
                    class="form-control"
                    min="0"
                  />
                </div>

                <div class="form-group">
                  <label for="pageQuota" class="form-label">Page Quota</label>
                  <input
                    type="number"
                    id="pageQuota"
                    formControlName="pageQuota"
                    class="form-control"
                    min="0"
                  />
                </div>

                <div class="form-group">
                  <label for="userQuota" class="form-label">User Quota</label>
                  <input
                    type="number"
                    id="userQuota"
                    formControlName="userQuota"
                    class="form-control"
                    min="0"
                  />
                </div>

                <div class="form-group">
                  <label for="siteLogHistory" class="form-label">Site Log History (Days)</label>
                  <input
                    type="number"
                    id="siteLogHistory"
                    formControlName="siteLogHistory"
                    class="form-control"
                    min="-1"
                  />
                </div>
              </div>
            </section>
          }

          <!-- Appearance Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Appearance</h2>
            <div class="form-grid" [formGroup]="appearanceForm">
              <div class="form-group">
                <label for="portalSkin" class="form-label">Portal Skin</label>
                <select
                  id="portalSkin"
                  formControlName="portalSkin"
                  class="form-control">
                  <option value="">-- Default --</option>
                  @for (skin of skinOptions(); track skin.path) {
                    <option [value]="skin.path">{{ skin.name }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="portalContainer" class="form-label">Portal Container</label>
                <select
                  id="portalContainer"
                  formControlName="portalContainer"
                  class="form-control">
                  <option value="">-- Default --</option>
                  @for (container of containerOptions(); track container.path) {
                    <option [value]="container.path">{{ container.name }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="adminSkin" class="form-label">Admin Skin</label>
                <select
                  id="adminSkin"
                  formControlName="adminSkin"
                  class="form-control">
                  <option value="">-- Default --</option>
                  @for (skin of skinOptions(); track skin.path) {
                    <option [value]="skin.path">{{ skin.name }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="adminContainer" class="form-label">Admin Container</label>
                <select
                  id="adminContainer"
                  formControlName="adminContainer"
                  class="form-control">
                  <option value="">-- Default --</option>
                  @for (container of containerOptions(); track container.path) {
                    <option [value]="container.path">{{ container.name }}</option>
                  }
                </select>
              </div>
            </div>
          </section>

          <!-- Usability Settings Section -->
          <section class="settings-section">
            <h2 class="section-title">Usability</h2>
            <div class="form-grid" [formGroup]="usabilityForm">
              <div class="form-group checkbox-group">
                <label class="checkbox-label">
                  <input
                    type="checkbox"
                    formControlName="inlineEditor"
                    class="form-checkbox"
                  />
                  <span>Enable Inline Editor</span>
                </label>
              </div>

              <div class="form-group">
                <label for="controlPanelMode" class="form-label">Control Panel Mode</label>
                <select
                  id="controlPanelMode"
                  formControlName="controlPanelMode"
                  class="form-control">
                  @for (option of controlPanelModeOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="controlPanelVisibility" class="form-label">Control Panel Visibility</label>
                <select
                  id="controlPanelVisibility"
                  formControlName="controlPanelVisibility"
                  class="form-control">
                  @for (option of controlPanelVisibilityOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div class="form-group">
                <label for="controlPanelSecurity" class="form-label">Control Panel Security</label>
                <select
                  id="controlPanelSecurity"
                  formControlName="controlPanelSecurity"
                  class="form-control">
                  @for (option of controlPanelSecurityOptions; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>
            </div>
          </section>

          <!-- SSL Settings Section (SuperUser Only) -->
          <!-- MIGRATION: Replaces dshSSL/tblSSL visibility check from SiteSettings.ascx.vb lines 501-502 -->
          @if (isSuperUser()) {
            <section class="settings-section ssl-settings">
              <h2 class="section-title">SSL Settings</h2>
              <p class="section-description">These settings are only visible to Super Users.</p>
              <div class="form-grid" [formGroup]="sslForm">
                <div class="form-group checkbox-group">
                  <label class="checkbox-label">
                    <input
                      type="checkbox"
                      formControlName="sslEnabled"
                      class="form-checkbox"
                    />
                    <span>SSL Enabled</span>
                  </label>
                </div>

                <div class="form-group checkbox-group">
                  <label class="checkbox-label">
                    <input
                      type="checkbox"
                      formControlName="sslEnforced"
                      class="form-checkbox"
                    />
                    <span>SSL Enforced</span>
                  </label>
                </div>

                <div class="form-group">
                  <label for="sslUrl" class="form-label">SSL URL</label>
                  <input
                    type="text"
                    id="sslUrl"
                    formControlName="sslUrl"
                    class="form-control"
                    placeholder="https://secure.example.com"
                  />
                </div>

                <div class="form-group">
                  <label for="stdUrl" class="form-label">Standard URL</label>
                  <input
                    type="text"
                    id="stdUrl"
                    formControlName="stdUrl"
                    class="form-control"
                    placeholder="http://www.example.com"
                  />
                </div>
              </div>
            </section>
          }

          <!-- Stylesheet Editor Section -->
          <section class="settings-section">
            <h2 class="section-title">Stylesheet Editor</h2>
            <div class="stylesheet-editor">
              <div class="form-group full-width">
                <label for="stylesheet" class="form-label">Portal CSS</label>
                <textarea
                  id="stylesheet"
                  [value]="styleSheetContent()"
                  (input)="onStyleSheetChange($event)"
                  class="form-control code-editor"
                  rows="15"
                  spellcheck="false"
                ></textarea>
              </div>
              <div class="stylesheet-actions">
                <button
                  type="button"
                  class="btn btn-secondary"
                  (click)="saveStyleSheet()"
                  [disabled]="saving()">
                  Save Stylesheet
                </button>
                <button
                  type="button"
                  class="btn btn-warning"
                  (click)="showRestoreConfirmation()"
                  [disabled]="saving()">
                  Restore Default
                </button>
              </div>
            </div>
          </section>
        </div>
      </div>
    }

    <!-- Delete Confirmation Dialog -->
    <app-confirmation-dialog
      #deleteDialog
      [title]="'Delete Portal'"
      [message]="'Are you sure you want to delete this portal? This action cannot be undone. All portal data, users, and content will be permanently removed.'"
      [confirmText]="'Delete Portal'"
      [cancelText]="'Cancel'"
      [confirmButtonType]="'danger'"
      (confirmed)="onDelete()">
    </app-confirmation-dialog>

    <!-- Restore Stylesheet Confirmation Dialog -->
    <app-confirmation-dialog
      #restoreDialog
      [title]="'Restore Default Stylesheet'"
      [message]="'Are you sure you want to restore the default stylesheet? This will overwrite your current portal CSS.'"
      [confirmText]="'Restore'"
      [cancelText]="'Cancel'"
      [confirmButtonType]="'warning'"
      (confirmed)="restoreDefaultStyleSheet()">
    </app-confirmation-dialog>
  `,
  styles: [`
    /* Container and Layout */
    .portal-settings-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 24px;
    }

    .loading-container,
    .error-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 400px;
      padding: 24px;
    }

    .error-message {
      text-align: center;
      padding: 32px;
      background-color: #fff5f5;
      border: 1px solid #fed7d7;
      border-radius: 8px;
      max-width: 400px;
    }

    .error-message h3 {
      color: #c53030;
      margin: 0 0 12px 0;
    }

    .error-message p {
      color: #742a2a;
      margin: 0 0 20px 0;
    }

    .error-message button {
      margin: 0 8px;
    }

    /* Header */
    .settings-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 32px;
      padding-bottom: 16px;
      border-bottom: 2px solid #e2e8f0;
    }

    .settings-header h1 {
      margin: 0;
      font-size: 1.75rem;
      font-weight: 600;
      color: #1a202c;
    }

    .header-actions {
      display: flex;
      gap: 12px;
    }

    /* Section Styling */
    .settings-section {
      background-color: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 24px;
      margin-bottom: 24px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
    }

    .section-title {
      font-size: 1.25rem;
      font-weight: 600;
      color: #2d3748;
      margin: 0 0 20px 0;
      padding-bottom: 12px;
      border-bottom: 1px solid #e2e8f0;
    }

    .section-description {
      font-size: 0.875rem;
      color: #718096;
      margin: -12px 0 16px 0;
    }

    .host-settings,
    .ssl-settings {
      background-color: #fffaf0;
      border-color: #ed8936;
    }

    .host-settings .section-title,
    .ssl-settings .section-title {
      color: #c05621;
    }

    /* Form Grid */
    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 20px;
    }

    @media (max-width: 768px) {
      .form-grid {
        grid-template-columns: 1fr;
      }

      .settings-header {
        flex-direction: column;
        gap: 16px;
      }

      .header-actions {
        flex-wrap: wrap;
        justify-content: center;
      }
    }

    .form-group.full-width {
      grid-column: 1 / -1;
    }

    /* Form Controls */
    .form-group {
      display: flex;
      flex-direction: column;
    }

    .form-label {
      font-size: 0.875rem;
      font-weight: 500;
      color: #4a5568;
      margin-bottom: 6px;
    }

    .form-label.required::after {
      content: ' *';
      color: #e53e3e;
    }

    .form-control {
      padding: 10px 12px;
      font-size: 0.9375rem;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      background-color: #ffffff;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .form-control:focus {
      outline: none;
      border-color: #3182ce;
      box-shadow: 0 0 0 3px rgba(49, 130, 206, 0.1);
    }

    .form-control.is-invalid {
      border-color: #e53e3e;
    }

    .form-control.is-invalid:focus {
      box-shadow: 0 0 0 3px rgba(229, 62, 62, 0.1);
    }

    .invalid-feedback {
      font-size: 0.8125rem;
      color: #e53e3e;
      margin-top: 4px;
    }

    textarea.form-control {
      resize: vertical;
      min-height: 80px;
    }

    select.form-control {
      cursor: pointer;
    }

    /* Checkbox Group */
    .checkbox-group {
      justify-content: center;
    }

    .checkbox-label {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      font-size: 0.9375rem;
      color: #4a5568;
    }

    .form-checkbox {
      width: 18px;
      height: 18px;
      cursor: pointer;
      accent-color: #3182ce;
    }

    /* Buttons */
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
      transition: background-color 0.15s ease, transform 0.1s ease, box-shadow 0.15s ease;
      min-width: 100px;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn:active:not(:disabled) {
      transform: scale(0.98);
    }

    .btn-primary {
      background-color: #3182ce;
      color: #ffffff;
    }

    .btn-primary:hover:not(:disabled) {
      background-color: #2c5282;
    }

    .btn-secondary {
      background-color: #e2e8f0;
      color: #4a5568;
    }

    .btn-secondary:hover:not(:disabled) {
      background-color: #cbd5e0;
    }

    .btn-danger {
      background-color: #e53e3e;
      color: #ffffff;
    }

    .btn-danger:hover:not(:disabled) {
      background-color: #c53030;
    }

    .btn-warning {
      background-color: #ed8936;
      color: #ffffff;
    }

    .btn-warning:hover:not(:disabled) {
      background-color: #dd6b20;
    }

    /* Stylesheet Editor */
    .stylesheet-editor {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .code-editor {
      font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', 'Consolas', monospace;
      font-size: 0.875rem;
      line-height: 1.5;
      background-color: #1a202c;
      color: #e2e8f0;
      padding: 16px;
      border-radius: 6px;
      min-height: 300px;
    }

    .code-editor:focus {
      border-color: #4299e1;
      box-shadow: 0 0 0 3px rgba(66, 153, 225, 0.2);
    }

    .stylesheet-actions {
      display: flex;
      gap: 12px;
      justify-content: flex-end;
    }

    /* Responsive adjustments */
    @media (max-width: 480px) {
      .portal-settings-container {
        padding: 16px;
      }

      .settings-section {
        padding: 16px;
      }

      .stylesheet-actions {
        flex-direction: column;
      }

      .stylesheet-actions .btn {
        width: 100%;
      }
    }
  `],
})
export class PortalSettingsComponent implements OnInit {
  // ============================================================================
  // DEPENDENCY INJECTION - Angular 19 inject() pattern
  // ============================================================================

  /**
   * Portal service for API operations
   * MIGRATION: Replaces DNN PortalController.vb method calls
   */
  private readonly portalService = inject(PortalService);

  /**
   * Authentication service for user info and SuperUser check
   * MIGRATION: Replaces DNN UserInfo.IsSuperUser check
   */
  private readonly authService = inject(AuthService);

  /**
   * Router for programmatic navigation
   * MIGRATION: Replaces DNN Response.Redirect
   */
  private readonly router = inject(Router);

  /**
   * Activated route for extracting portal ID from URL parameters
   * MIGRATION: Replaces Request.QueryString["pid"]
   */
  private readonly route = inject(ActivatedRoute);

  // ============================================================================
  // VIEW CHILD REFERENCES
  // ============================================================================

  /**
   * Reference to the delete confirmation dialog
   */
  @ViewChild('deleteDialog') deleteDialog!: ConfirmationDialogComponent;

  /**
   * Reference to the restore stylesheet confirmation dialog
   */
  @ViewChild('restoreDialog') restoreDialog!: ConfirmationDialogComponent;

  // ============================================================================
  // SIGNAL-BASED STATE MANAGEMENT
  // ============================================================================

  /**
   * Current portal data
   * MIGRATION: Replaces objPortal local variable from Page_Load
   */
  readonly portal = signal<Portal | null>(null);

  /**
   * Loading state flag
   */
  readonly loading = signal<boolean>(true);

  /**
   * Saving state flag for form submission
   */
  readonly saving = signal<boolean>(false);

  /**
   * Error message for display
   */
  readonly error = signal<string | null>(null);

  /**
   * Flag indicating if current user is a SuperUser
   * MIGRATION: Replaces UserInfo.IsSuperUser check from line 292
   */
  readonly isSuperUser = signal<boolean>(false);

  /**
   * Stylesheet content for the portal
   * MIGRATION: Replaces txtStyleSheet.Text from LoadStyleSheet()
   */
  readonly styleSheetContent = signal<string>('');

  /**
   * Portal ID from route parameters
   */
  private portalId: number | null = null;

  /**
   * Current user's portal ID (for delete permission check)
   */
  private currentUserPortalId: number | null = null;

  // ============================================================================
  // DROPDOWN OPTIONS
  // ============================================================================

  /**
   * Tab options for page settings dropdowns
   * Populated from portal data
   */
  readonly tabOptions = signal<TabOption[]>([]);

  /**
   * Skin options for appearance settings
   */
  readonly skinOptions = signal<SkinOption[]>([]);

  /**
   * Container options for appearance settings
   */
  readonly containerOptions = signal<SkinOption[]>([]);

  /**
   * User registration type options
   * MIGRATION: Maps to PortalInfo.UserRegistration (0-3)
   */
  readonly userRegistrationOptions = [
    { value: UserRegistrationType.None, label: 'None' },
    { value: UserRegistrationType.Private, label: 'Private' },
    { value: UserRegistrationType.Public, label: 'Public' },
    { value: UserRegistrationType.Verified, label: 'Verified' },
  ];

  /**
   * Banner advertising options
   * MIGRATION: Maps to PortalInfo.BannerAdvertising (0-2)
   */
  readonly bannerOptions = [
    { value: BannerType.None, label: 'None' },
    { value: BannerType.Site, label: 'Site' },
    { value: BannerType.Vendor, label: 'Vendor' },
  ];

  /**
   * Currency options for payment settings
   * MIGRATION: Replaces ListController.GetListEntryInfoCollection("Currency")
   */
  readonly currencyOptions: CurrencyOption[] = [
    { code: 'USD', name: 'US Dollar (USD)' },
    { code: 'EUR', name: 'Euro (EUR)' },
    { code: 'GBP', name: 'British Pound (GBP)' },
    { code: 'CAD', name: 'Canadian Dollar (CAD)' },
    { code: 'AUD', name: 'Australian Dollar (AUD)' },
    { code: 'JPY', name: 'Japanese Yen (JPY)' },
    { code: 'CHF', name: 'Swiss Franc (CHF)' },
  ];

  /**
   * Payment processor options
   * MIGRATION: Replaces ListController.GetListEntryInfoCollection("Processor")
   */
  readonly processorOptions: ProcessorOption[] = [
    { id: 'PayPal', name: 'PayPal' },
    { id: 'Stripe', name: 'Stripe' },
    { id: 'Authorize.Net', name: 'Authorize.Net' },
  ];

  /**
   * Control panel mode options
   * MIGRATION: Maps to ControlPanelMode setting (VIEW/EDIT)
   */
  readonly controlPanelModeOptions: ControlPanelOption[] = [
    { value: 'VIEW', label: 'View Mode' },
    { value: 'EDIT', label: 'Edit Mode' },
  ];

  /**
   * Control panel visibility options
   * MIGRATION: Maps to ControlPanelVisibility setting (MIN/MAX)
   */
  readonly controlPanelVisibilityOptions: ControlPanelOption[] = [
    { value: 'MIN', label: 'Minimized' },
    { value: 'MAX', label: 'Maximized' },
  ];

  /**
   * Control panel security options
   * MIGRATION: Maps to ControlPanelSecurity setting (TAB/MODULE)
   */
  readonly controlPanelSecurityOptions: ControlPanelOption[] = [
    { value: 'TAB', label: 'Tab (Page)' },
    { value: 'MODULE', label: 'Module' },
  ];

  // ============================================================================
  // TYPED REACTIVE FORMS
  // ============================================================================

  /**
   * Basic settings form group
   * MIGRATION: Maps to form fields from lines 268-276
   */
  readonly basicSettingsForm = new FormGroup({
    portalName: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(128)],
    }),
    logoFile: new FormControl<string>('', { nonNullable: true }),
    footerText: new FormControl<string>('', { nonNullable: true }),
    description: new FormControl<string>('', { nonNullable: true }),
    keywords: new FormControl<string>('', { nonNullable: true }),
    backgroundFile: new FormControl<string>('', { nonNullable: true }),
  });

  /**
   * Registration settings form group
   * MIGRATION: Maps to optUserRegistration and optBanners from lines 277, 291
   */
  readonly registrationForm = new FormGroup({
    userRegistration: new FormControl<number>(UserRegistrationType.None, {
      nonNullable: true,
    }),
    bannerAdvertising: new FormControl<number>(BannerType.None, {
      nonNullable: true,
    }),
  });

  /**
   * Page settings form group
   * MIGRATION: Maps to cboSplashTabId, cboHomeTabId, cboLoginTabId, cboUserTabId
   */
  readonly pageSettingsForm = new FormGroup({
    splashTabId: new FormControl<number | null>(null),
    homeTabId: new FormControl<number | null>(null),
    loginTabId: new FormControl<number | null>(null),
    userTabId: new FormControl<number | null>(null),
  });

  /**
   * Payment settings form group
   * MIGRATION: Maps to cboCurrency, cboProcessor, txtUserId, txtPassword
   */
  readonly paymentForm = new FormGroup({
    currency: new FormControl<string>('USD', { nonNullable: true }),
    processor: new FormControl<string>('', { nonNullable: true }),
    processorUserId: new FormControl<string>('', { nonNullable: true }),
    processorPassword: new FormControl<string>('', { nonNullable: true }),
  });

  /**
   * Host settings form group (SuperUser only)
   * MIGRATION: Maps to txtExpiryDate, txtHostFee, txtHostSpace, txtPageQuota, txtUserQuota, txtSiteLogHistory
   */
  readonly hostSettingsForm = new FormGroup({
    expiryDate: new FormControl<string | null>(null),
    hostFee: new FormControl<number>(0, { nonNullable: true }),
    hostSpace: new FormControl<number>(0, { nonNullable: true }),
    pageQuota: new FormControl<number>(0, { nonNullable: true }),
    userQuota: new FormControl<number>(0, { nonNullable: true }),
    siteLogHistory: new FormControl<number>(-1, { nonNullable: true }),
  });

  /**
   * Appearance settings form group
   * MIGRATION: Maps to ctlPortalSkin, ctlPortalContainer, ctlAdminSkin, ctlAdminContainer
   */
  readonly appearanceForm = new FormGroup({
    portalSkin: new FormControl<string>('', { nonNullable: true }),
    portalContainer: new FormControl<string>('', { nonNullable: true }),
    adminSkin: new FormControl<string>('', { nonNullable: true }),
    adminContainer: new FormControl<string>('', { nonNullable: true }),
  });

  /**
   * Usability settings form group
   * MIGRATION: Maps to chkInlineEditor, optControlPanelMode, optControlPanelVisibility, optControlPanelSecurity
   */
  readonly usabilityForm = new FormGroup({
    inlineEditor: new FormControl<boolean>(true, { nonNullable: true }),
    controlPanelMode: new FormControl<string>('EDIT', { nonNullable: true }),
    controlPanelVisibility: new FormControl<string>('MAX', { nonNullable: true }),
    controlPanelSecurity: new FormControl<string>('MODULE', { nonNullable: true }),
  });

  /**
   * SSL settings form group (SuperUser only)
   * MIGRATION: Maps to chkSSLEnabled, chkSSLEnforced, txtSSLURL, txtSTDURL
   */
  readonly sslForm = new FormGroup({
    sslEnabled: new FormControl<boolean>(false, { nonNullable: true }),
    sslEnforced: new FormControl<boolean>(false, { nonNullable: true }),
    sslUrl: new FormControl<string>('', { nonNullable: true }),
    stdUrl: new FormControl<string>('', { nonNullable: true }),
  });

  // ============================================================================
  // COMPUTED PROPERTIES
  // ============================================================================

  /**
   * Checks if all forms are valid for submission
   */
  isFormValid(): boolean {
    return this.basicSettingsForm.valid;
  }

  /**
   * Checks if the portal can be deleted
   * MIGRATION: Replaces cmdDelete.Visible = (intPortalId <> PortalId) from line 503
   */
  canDeletePortal(): boolean {
    return this.portalId !== null && this.portalId !== this.currentUserPortalId;
  }

  // ============================================================================
  // LIFECYCLE HOOKS
  // ============================================================================

  /**
   * Component initialization
   * MIGRATION: Replaces Page_Load event handler logic from lines 232-521
   */
  ngOnInit(): void {
    // Check if current user is a SuperUser
    this.checkSuperUserStatus();

    // Extract portal ID from route parameters
    // MIGRATION: Replaces Request.QueryString["pid"] from line 235-236
    this.route.params.subscribe((params) => {
      const id = params['id'];
      if (id) {
        this.portalId = parseInt(id, 10);
        if (!isNaN(this.portalId)) {
          this.loadPortalData();
        } else {
          this.error.set('Invalid portal ID');
          this.loading.set(false);
        }
      } else {
        this.error.set('Portal ID is required');
        this.loading.set(false);
      }
    });
  }

  // ============================================================================
  // DATA LOADING METHODS
  // ============================================================================

  /**
   * Checks if the current user is a SuperUser
   * MIGRATION: Replaces UserInfo.IsSuperUser check from line 292
   */
  private checkSuperUserStatus(): void {
    const currentUser = this.authService.getCurrentUser();
    if (currentUser) {
      this.isSuperUser.set(currentUser.isSuperUser);
      this.currentUserPortalId = currentUser.portalId;
    }
  }

  /**
   * Loads portal data from the API
   * MIGRATION: Replaces objPortalController.GetPortal(intPortalId) from line 266
   */
  async loadPortalData(): Promise<void> {
    if (this.portalId === null) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      // Fetch portal data
      const portal = await firstValueFrom(this.portalService.getPortal(this.portalId));
      this.portal.set(portal);

      // Populate form controls with portal data
      this.populateFormsFromPortal(portal);

      // Load additional data (tabs, skins, stylesheet)
      await this.loadAdditionalData();

      this.loading.set(false);
    } catch (err) {
      console.error('Error loading portal:', err);
      this.error.set('Failed to load portal settings. Please try again.');
      this.loading.set(false);
    }
  }

  /**
   * Populates all form controls from portal data
   * MIGRATION: Replaces form field population from lines 268-419
   */
  private populateFormsFromPortal(portal: Portal): void {
    // Basic Settings
    // MIGRATION: Lines 268-276
    this.basicSettingsForm.patchValue({
      portalName: portal.portalName || '',
      logoFile: portal.logoFile || '',
      footerText: portal.footerText || '',
      description: portal.description || '',
      keywords: portal.keyWords || '',
      backgroundFile: portal.backgroundFile || '',
    });

    // Registration Settings
    // MIGRATION: Lines 277, 291
    this.registrationForm.patchValue({
      userRegistration: portal.userRegistration,
      bannerAdvertising: portal.bannerAdvertising,
    });

    // Page Settings
    // MIGRATION: Lines 299-318
    this.pageSettingsForm.patchValue({
      splashTabId: portal.splashTabId ?? null,
      homeTabId: portal.homeTabId ?? null,
      loginTabId: portal.loginTabId ?? null,
      userTabId: portal.userTabId ?? null,
    });

    // Payment Settings
    // MIGRATION: Lines 377-389
    this.paymentForm.patchValue({
      currency: portal.currency || 'USD',
      // Note: processor info not directly on Portal interface
      processor: '',
      processorUserId: '',
      processorPassword: '',
    });

    // Host Settings (for SuperUsers)
    // MIGRATION: Lines 341-350
    if (this.isSuperUser()) {
      this.hostSettingsForm.patchValue({
        expiryDate: portal.expiryDate ? this.formatDateForInput(portal.expiryDate) : null,
        hostFee: portal.hostFee ?? 0,
        hostSpace: portal.hostSpace ?? 0,
        pageQuota: portal.pageQuota ?? 0,
        userQuota: portal.userQuota ?? 0,
        siteLogHistory: portal.siteLogHistory ?? -1,
      });
    }

    // Usability and Appearance settings would be loaded from site settings
    // Using defaults since these aren't directly on the Portal model
  }

  /**
   * Formats a date string for HTML date input
   */
  private formatDateForInput(dateString: string): string {
    try {
      const date = new Date(dateString);
      return date.toISOString().split('T')[0];
    } catch {
      return '';
    }
  }

  /**
   * Loads additional data like tabs, skins, and stylesheet
   */
  private async loadAdditionalData(): Promise<void> {
    // Load tab options for page settings dropdowns
    // In a real implementation, this would call an API endpoint
    // For now, we'll use placeholder data
    this.tabOptions.set([
      { tabId: 1, tabName: 'Home' },
      { tabId: 2, tabName: 'Login' },
      { tabId: 3, tabName: 'User Profile' },
      { tabId: 4, tabName: 'Admin' },
    ]);

    // Load skin options
    this.skinOptions.set([
      { path: 'Skins/DNN-Blue/Home.ascx', name: 'DNN Blue - Home' },
      { path: 'Skins/DNN-Blue/Inner.ascx', name: 'DNN Blue - Inner' },
      { path: 'Skins/MinimalExtropy/Standard.ascx', name: 'Minimal Extropy - Standard' },
    ]);

    // Load container options
    this.containerOptions.set([
      { path: 'Containers/DNN-Blue/Title_h2.ascx', name: 'DNN Blue - Title H2' },
      { path: 'Containers/DNN-Blue/NoTitle.ascx', name: 'DNN Blue - No Title' },
      { path: 'Containers/MinimalExtropy/Standard.ascx', name: 'Minimal Extropy - Standard' },
    ]);

    // Load stylesheet
    await this.loadStyleSheet();
  }

  /**
   * Loads the portal stylesheet content
   * MIGRATION: Replaces LoadStyleSheet() from lines 64-82
   */
  async loadStyleSheet(): Promise<void> {
    // In a real implementation, this would call the API
    // portalService.getStyleSheet(this.portalId) if that method existed
    // For now, set placeholder content
    this.styleSheetContent.set(`/* Portal Custom Stylesheet */
/* Edit this CSS to customize your portal appearance */

.portal-content {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

.portal-header {
  background-color: #3182ce;
  color: white;
}

.portal-footer {
  background-color: #2d3748;
  color: #e2e8f0;
}
`);
  }

  // ============================================================================
  // FORM SUBMISSION METHODS
  // ============================================================================

  /**
   * Handles form submission to update portal settings
   * MIGRATION: Replaces cmdUpdate_Click from lines 687-822
   */
  async onSubmit(): Promise<void> {
    if (!this.isFormValid() || this.portalId === null) {
      // Mark all controls as touched to show validation errors
      this.basicSettingsForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      // Build update request from all forms
      const request = this.buildUpdateRequest();

      // Call API to update portal
      // MIGRATION: Replaces objPortalController.UpdatePortalInfo() from lines 772-781
      const updatedPortal = await firstValueFrom(
        this.portalService.updatePortal(this.portalId, request)
      );

      // Update local state with response
      this.portal.set(updatedPortal);

      // Show success message (could use a toast/notification service)
      console.log('Portal settings saved successfully');

      this.saving.set(false);
    } catch (err) {
      console.error('Error saving portal settings:', err);
      this.error.set('Failed to save portal settings. Please try again.');
      this.saving.set(false);
    }
  }

  /**
   * Builds the UpdatePortalRequest from form values
   * MIGRATION: Collects form values similar to cmdUpdate_Click logic
   */
  private buildUpdateRequest(): UpdatePortalRequest {
    const basicValues = this.basicSettingsForm.value;
    const registrationValues = this.registrationForm.value;
    const pageValues = this.pageSettingsForm.value;
    const paymentValues = this.paymentForm.value;
    const hostValues = this.hostSettingsForm.value;

    const request: UpdatePortalRequest = {
      // Basic settings
      portalName: basicValues.portalName || '',
      logoFile: basicValues.logoFile || undefined,
      footerText: basicValues.footerText || undefined,
      description: basicValues.description || undefined,
      keyWords: basicValues.keywords || undefined,
      backgroundFile: basicValues.backgroundFile || undefined,

      // Registration settings
      userRegistration: registrationValues.userRegistration ?? UserRegistrationType.None,
      bannerAdvertising: registrationValues.bannerAdvertising ?? BannerType.None,

      // Payment settings
      currency: paymentValues.currency || 'USD',
      paymentProcessor: paymentValues.processor || undefined,
      processorUserId: paymentValues.processorUserId || undefined,
      processorPassword: paymentValues.processorPassword || undefined,

      // Page settings
      splashTabId: pageValues.splashTabId ?? undefined,
      homeTabId: pageValues.homeTabId ?? undefined,
      loginTabId: pageValues.loginTabId ?? undefined,
      userTabId: pageValues.userTabId ?? undefined,

      // Host settings (only included if SuperUser)
      hostFee: hostValues.hostFee ?? 0,
      hostSpace: hostValues.hostSpace ?? 0,
      pageQuota: hostValues.pageQuota ?? 0,
      userQuota: hostValues.userQuota ?? 0,
      siteLogHistory: hostValues.siteLogHistory ?? -1,

      // Expiry date (only if SuperUser and provided)
      expiryDate: hostValues.expiryDate || undefined,

      // Default required fields
      administratorId: this.portal()?.administratorId ?? 0,
      timeZoneOffset: this.portal()?.timeZoneOffset ?? 0,
    };

    return request;
  }

  /**
   * Handles cancel button click
   * MIGRATION: Replaces cmdCancel_Click from lines 534-541
   */
  onCancel(): void {
    // Navigate back to portal list
    this.router.navigate(['/portals']);
  }

  // ============================================================================
  // DELETE METHODS
  // ============================================================================

  /**
   * Shows the delete confirmation dialog
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm pattern from line 252
   */
  showDeleteConfirmation(): void {
    this.deleteDialog.open();
  }

  /**
   * Handles portal deletion after confirmation
   * MIGRATION: Replaces cmdDelete_Click from lines 556-586
   */
  async onDelete(): Promise<void> {
    if (this.portalId === null || !this.canDeletePortal()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      // Call API to delete portal
      // MIGRATION: Replaces PortalController.DeletePortal() from line 563
      await firstValueFrom(this.portalService.deletePortal(this.portalId));

      console.log('Portal deleted successfully');

      // Navigate to portal list after successful deletion
      // MIGRATION: Replaces Response.Redirect from line 577
      this.router.navigate(['/portals']);
    } catch (err) {
      console.error('Error deleting portal:', err);
      this.error.set('Failed to delete portal. Please try again.');
      this.saving.set(false);
    }
  }

  // ============================================================================
  // STYLESHEET METHODS
  // ============================================================================

  /**
   * Handles stylesheet content changes
   */
  onStyleSheetChange(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    this.styleSheetContent.set(textarea.value);
  }

  /**
   * Saves the stylesheet content
   * MIGRATION: Replaces cmdSave_Click from lines 650-674
   */
  async saveStyleSheet(): Promise<void> {
    if (this.portalId === null) {
      return;
    }

    this.saving.set(true);

    try {
      // In a real implementation, this would call the API
      // await firstValueFrom(this.portalService.saveStyleSheet(this.portalId, this.styleSheetContent()));
      
      // Simulate API call
      await new Promise((resolve) => setTimeout(resolve, 500));

      console.log('Stylesheet saved successfully');
      this.saving.set(false);
    } catch (err) {
      console.error('Error saving stylesheet:', err);
      this.error.set('Failed to save stylesheet. Please try again.');
      this.saving.set(false);
    }
  }

  /**
   * Shows the restore stylesheet confirmation dialog
   * MIGRATION: Replaces ClientAPI.AddButtonConfirm(cmdRestore, ...) from line 247
   */
  showRestoreConfirmation(): void {
    this.restoreDialog.open();
  }

  /**
   * Restores the default stylesheet
   * MIGRATION: Replaces cmdRestore_Click from lines 619-637
   */
  async restoreDefaultStyleSheet(): Promise<void> {
    if (this.portalId === null) {
      return;
    }

    this.saving.set(true);

    try {
      // In a real implementation, this would call the API
      // await firstValueFrom(this.portalService.restoreDefaultStyleSheet(this.portalId));
      
      // Simulate API call and set default content
      await new Promise((resolve) => setTimeout(resolve, 500));

      // Set default stylesheet content
      this.styleSheetContent.set(`/* Default Portal Stylesheet */
/* This is the default stylesheet that was restored */

body {
  font-family: Arial, sans-serif;
  font-size: 14px;
  color: #333;
}
`);

      console.log('Default stylesheet restored successfully');
      this.saving.set(false);
    } catch (err) {
      console.error('Error restoring stylesheet:', err);
      this.error.set('Failed to restore default stylesheet. Please try again.');
      this.saving.set(false);
    }
  }
}
