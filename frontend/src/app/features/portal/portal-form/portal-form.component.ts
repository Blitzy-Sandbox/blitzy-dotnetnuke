import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import {
  CreatePortalRequest,
  Portal,
  ProblemDetails,
  UpdatePortalRequest,
} from '../../../core/models';
import { PortalService } from '../portal.service';
import {
  FormFieldComponent,
  FormFieldOption,
} from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';

// MIGRATION: SiteSettings.ascx valExpiryDate — a CompareValidator with
// Operator="DataTypeCheck" Type="Date" whose ErrorMessage was "Invalid expiry date!".
// It validated that the entered value is a genuine date (it did NOT itself enforce
// presence). Re-expressed here as a reactive validator that flags a NON-EMPTY value which
// is not a parseable date; an empty value passes (optionality is a separate concern).
function expiryDateValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === undefined || value === '') {
    return null;
  }
  return Number.isNaN(Date.parse(String(value))) ? { invalidDate: true } : null;
}

// MIGRATION: SiteSettings.ascx valHostFee — a CompareValidator with
// Operator="DataTypeCheck" Type="Currency" whose ErrorMessage was
// "Invalid fee, needs to be a currency value!". It validated that the entered value is a
// valid currency amount. Re-expressed here as a reactive validator that flags a NON-EMPTY
// value which is not a finite numeric (currency) amount. Mirrors the legacy DataTypeCheck
// ONLY — it deliberately imposes no range / non-negative rule the legacy screen never had.
function hostFeeCurrencyValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === undefined || value === '') {
    return null;
  }
  return Number.isFinite(Number(value)) ? null : { invalidCurrency: true };
}

/**
 * PortalFormComponent — portal (site) create/edit screen.
 *
 * MIGRATION: replaces the legacy DNN Web Forms admin portal screens under
 * Website/admin/Portal/** — Signup.ascx (create a new portal + its administrator)
 * and SiteSettings.ascx (edit an existing portal's settings). Web Forms
 * postback/ViewState/IClientAPICallbackEventHandler navigation is eliminated
 * (AAP §0.6.3): create mode POSTs (-> 201) and edit mode GETs then PUTs
 * (-> 200) / DELETEs (-> 204) via the injected PortalService, which performs API
 * communication only (AAP §0.7.1 — no business logic in the Angular service).
 *
 * The create ('new') and edit (':id') routes reuse this one component
 * (portal.routes.ts); mode is detected from the presence of a numeric ':id'
 * route param, mirroring the sibling role-form / module-form components.
 *
 * MIGRATION: the two client-side data-type validators the legacy SiteSettings.ascx
 * enforced — valExpiryDate ("Invalid expiry date!") and valHostFee ("Invalid fee, needs
 * to be a currency value!") — are mirrored here (see expiryDateValidator /
 * hostFeeCurrencyValidator above) so the edit screen has functional validation parity.
 * The remaining per-control SiteSettings rules (host-only quota ranges, tab pickers) are
 * surfaced by the backend contract (RFC 7807); the create-administrator credential fields
 * carry the required/email validators the legacy Signup.ascx enforced client-side.
 */
@Component({
  selector: 'app-portal-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormFieldComponent,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent,
  ],
  template: `
    <section class="portal-form">
      <header class="portal-form__header">
        <h1 class="portal-form__title">{{ isEdit ? 'Edit Portal' : 'Create Portal' }}</h1>
      </header>

      <app-loading-spinner [loading]="loading()" />

      @if (error(); as message) {
        <p class="portal-form__banner" role="alert">{{ message }}</p>
      }

      @if (!loading()) {
        <form class="portal-form__form" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <app-form-field
            [control]="form.controls.portalName"
            label="Portal Name"
            controlType="text"
            [required]="true"
            [errorMessages]="portalNameErrors"
          />

          <!-- MIGRATION: Signup.ascx create-a-portal fields (portal alias + the
               initial administrator account). Rendered in create mode only. -->
          @if (!isEdit) {
            <app-form-field
              [control]="form.controls.portalAlias"
              label="Portal Alias"
              controlType="text"
              [required]="true"
            />
            <app-form-field
              [control]="form.controls.firstName"
              label="First Name"
              controlType="text"
              [required]="true"
            />
            <app-form-field
              [control]="form.controls.lastName"
              label="Last Name"
              controlType="text"
              [required]="true"
            />
            <app-form-field
              [control]="form.controls.username"
              label="Username"
              controlType="text"
              [required]="true"
            />
            <app-form-field
              [control]="form.controls.password"
              label="Password"
              controlType="password"
              [required]="true"
            />
            <app-form-field
              [control]="form.controls.email"
              label="Email"
              controlType="email"
              [required]="true"
              [errorMessages]="emailErrors"
            />
          }

          <!-- MIGRATION: SiteSettings.ascx portal-settings fields. Rendered in edit mode only. -->
          @if (isEdit) {
            <div class="portal-form__field">
              <label class="portal-form__label" for="portal-expiry">Expiry Date</label>
              <input
                id="portal-expiry"
                type="date"
                class="portal-form__control"
                [formControl]="form.controls.expiryDate"
                [attr.aria-invalid]="
                  form.controls.expiryDate.invalid &&
                  (form.controls.expiryDate.touched || form.controls.expiryDate.dirty)
                "
                [attr.aria-describedby]="
                  form.controls.expiryDate.invalid &&
                  (form.controls.expiryDate.touched || form.controls.expiryDate.dirty)
                    ? 'portal-expiry-error'
                    : null
                "
              />
              <!-- MIGRATION: SiteSettings.ascx valExpiryDate message (verbatim). -->
              @if (
                form.controls.expiryDate.hasError('invalidDate') &&
                (form.controls.expiryDate.touched || form.controls.expiryDate.dirty)
              ) {
                <p id="portal-expiry-error" class="portal-form__error" role="alert">
                  Invalid expiry date!
                </p>
              }
            </div>
            <app-form-field
              [control]="form.controls.userRegistration"
              label="User Registration"
              controlType="select"
              [options]="userRegistrationOptions"
            />
            <app-form-field
              [control]="form.controls.bannerAdvertising"
              label="Banner Advertising"
              controlType="select"
              [options]="bannerAdvertisingOptions"
            />
            <app-form-field
              [control]="form.controls.administratorId"
              label="Administrator (User Id)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.hostFee"
              label="Host Fee"
              controlType="number"
              [errorMessages]="hostFeeErrors"
            />
            <app-form-field
              [control]="form.controls.hostSpace"
              label="Host Space (MB)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.pageQuota"
              label="Page Quota"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.userQuota"
              label="User Quota"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.siteLogHistory"
              label="Site Log History (days)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.currency"
              label="Currency"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.defaultLanguage"
              label="Default Language"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.timeZoneOffset"
              label="Time Zone Offset (minutes)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.logoFile"
              label="Logo File"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.footerText"
              label="Footer Text"
              controlType="textarea"
            />
            <app-form-field
              [control]="form.controls.backgroundFile"
              label="Background File"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.splashTabId"
              label="Splash Page (Tab Id)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.homeTabId"
              label="Home Page (Tab Id)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.loginTabId"
              label="Login Page (Tab Id)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.userTabId"
              label="User Page (Tab Id)"
              controlType="number"
            />
            <app-form-field
              [control]="form.controls.paymentProcessor"
              label="Payment Processor"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.processorUserId"
              label="Processor User Id"
              controlType="text"
            />
            <app-form-field
              [control]="form.controls.processorPassword"
              label="Processor Password"
              controlType="password"
            />
          }

          <!-- MIGRATION: fields common to both Signup and SiteSettings. -->
          <app-form-field
            [control]="form.controls.description"
            label="Description"
            controlType="textarea"
          />
          <app-form-field
            [control]="form.controls.keyWords"
            label="Keywords"
            controlType="text"
          />
          <app-form-field
            [control]="form.controls.homeDirectory"
            label="Home Directory"
            controlType="text"
          />

          <div class="portal-form__actions">
            <button
              type="submit"
              class="portal-form__btn portal-form__btn--primary"
              [disabled]="saving()"
            >
              {{ isEdit ? 'Update' : 'Create' }}
            </button>
            <button type="button" class="portal-form__btn" (click)="cancel()">Cancel</button>
            @if (isEdit) {
              <button
                type="button"
                class="portal-form__btn portal-form__btn--danger"
                (click)="requestDelete()"
              >
                Delete
              </button>
            }
          </div>
        </form>
      }

      @if (isEdit) {
        <app-confirmation-dialog
          [(open)]="showDeleteDialog"
          title="Delete Portal"
          message="Are you sure you want to delete this portal? This cannot be undone."
          confirmLabel="Delete"
          cancelLabel="Cancel"
          [danger]="true"
          (confirm)="confirmDelete()"
        />
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .portal-form {
        max-width: 720px;
        margin: 0 auto;
        padding: var(--space-4, 1rem);
      }
      .portal-form__title {
        margin: 0 0 var(--space-3, 0.75rem);
        font-size: 1.5rem;
      }
      .portal-form__banner {
        margin: 0 0 var(--space-3, 0.75rem);
        padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
        color: var(--color-danger, #dc3545);
        background: var(--color-danger-bg, #f8d7da);
        border: 1px solid var(--color-danger, #dc3545);
        border-radius: var(--radius, 4px);
      }
      .portal-form__field {
        margin-bottom: var(--space-3, 0.75rem);
      }
      .portal-form__label {
        display: block;
        margin-bottom: var(--space-1, 0.25rem);
        font-weight: 600;
      }
      .portal-form__control {
        width: 100%;
        padding: var(--space-2, 0.5rem);
        font: inherit;
        border: 1px solid var(--color-border, #ced4da);
        border-radius: var(--radius, 4px);
      }
      .portal-form__error {
        margin: var(--space-1, 0.25rem) 0 0;
        font-size: 0.875rem;
        color: var(--color-danger, #dc3545);
      }
      .portal-form__actions {
        display: flex;
        gap: var(--space-2, 0.5rem);
        margin-top: var(--space-4, 1rem);
      }
      .portal-form__btn {
        padding: var(--space-2, 0.5rem) var(--space-4, 1rem);
        font: inherit;
        cursor: pointer;
        color: var(--color-text, #1a1a1a);
        background: var(--color-surface, #fff);
        border: 1px solid var(--color-border, #ced4da);
        border-radius: var(--radius, 4px);
      }
      .portal-form__btn--primary {
        color: #fff;
        background: var(--color-primary, #0d6efd);
        border-color: var(--color-primary, #0d6efd);
      }
      .portal-form__btn--danger {
        color: #fff;
        background: var(--color-danger, #dc3545);
        border-color: var(--color-danger, #dc3545);
      }
      .portal-form__btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      /* QA F-J: hover states (buttons previously had only :disabled). Base is
         the neutral Cancel button; primary/danger overrides follow it.
         QA INFO: subtle :active press feedback. */
      .portal-form__btn:hover:not(:disabled) {
        background: var(--color-surface-hover, #f1f5f9);
      }
      .portal-form__btn--primary:hover:not(:disabled) {
        background: var(--color-primary-hover, #1d4ed8);
        border-color: var(--color-primary-hover, #1d4ed8);
      }
      .portal-form__btn--danger:hover:not(:disabled) {
        background: var(--color-danger-hover, #b91c1c);
        border-color: var(--color-danger-hover, #b91c1c);
      }
      .portal-form__btn:active:not(:disabled) {
        transform: translateY(1px);
      }
    `,
  ],
})
export class PortalFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly portalService = inject(PortalService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // MIGRATION: legacy PortalId came from the querystring; here the route param
  // drives create vs edit: /portals/new (create) or /portals/:id (edit).
  private readonly idParam = this.route.snapshot.paramMap.get('id');
  readonly portalId: number | null =
    this.idParam !== null && /^\d+$/.test(this.idParam) ? Number(this.idParam) : null;
  readonly isEdit = this.portalId !== null;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly showDeleteDialog = signal(false);

  // MIGRATION: nonNullable typed reactive form. Create-only administrator credential
  // fields are validator-free at construction and gain required/email validators in
  // create mode only (ngOnInit) so edit mode — where they are not rendered — stays valid.
  readonly form = this.fb.nonNullable.group({
    portalName: ['', [Validators.required, Validators.maxLength(128)]],
    // Create-only (Signup.ascx) — administrator account + initial alias.
    portalAlias: [''],
    firstName: [''],
    lastName: [''],
    username: [''],
    password: [''],
    email: [''],
    // Shared optional fields (both DTOs).
    description: [''],
    keyWords: [''],
    homeDirectory: [''],
    // Edit-only (SiteSettings.ascx) settings.
    logoFile: [''],
    footerText: [''],
    // MIGRATION: SiteSettings.ascx valExpiryDate (CompareValidator Type=Date DataTypeCheck).
    expiryDate: ['', [expiryDateValidator]],
    userRegistration: [0],
    bannerAdvertising: [0],
    currency: ['USD'],
    administratorId: [0],
    // MIGRATION: SiteSettings.ascx valHostFee (CompareValidator Type=Currency DataTypeCheck).
    hostFee: [0, [hostFeeCurrencyValidator]],
    hostSpace: [0],
    pageQuota: [0],
    userQuota: [0],
    paymentProcessor: [''],
    processorUserId: [''],
    processorPassword: [''],
    backgroundFile: [''],
    siteLogHistory: [0],
    splashTabId: [-1],
    homeTabId: [-1],
    loginTabId: [-1],
    userTabId: [-1],
    defaultLanguage: ['en-US'],
    timeZoneOffset: [0],
  });

  // MIGRATION: legacy Integer UserRegistration (backend UserRegistrationType enum) codes.
  protected readonly userRegistrationOptions: readonly FormFieldOption[] = [
    { value: 0, label: 'None' },
    { value: 1, label: 'Private' },
    { value: 2, label: 'Public' },
    { value: 3, label: 'Verified' },
  ];

  // MIGRATION: legacy Integer BannerAdvertising (backend BannerType enum) codes.
  protected readonly bannerAdvertisingOptions: readonly FormFieldOption[] = [
    { value: 0, label: 'None' },
    { value: 1, label: 'Site' },
    { value: 2, label: 'Host' },
  ];

  protected readonly portalNameErrors: Record<string, string> = {
    required: 'Portal Name is required.',
    maxlength: 'Portal Name is too long.',
  };
  protected readonly emailErrors: Record<string, string> = {
    required: 'Email is required.',
    email: 'Enter a valid email address.',
  };
  // MIGRATION: SiteSettings.ascx valHostFee ErrorMessage (verbatim).
  protected readonly hostFeeErrors: Record<string, string> = {
    invalidCurrency: 'Invalid fee, needs to be a currency value!',
  };

  ngOnInit(): void {
    if (this.isEdit && this.portalId !== null) {
      this.loadPortal(this.portalId);
      return;
    }
    // CREATE MODE — MIGRATION: Signup.ascx required/email validators apply only here.
    this.form.controls.portalAlias.addValidators(Validators.required);
    this.form.controls.firstName.addValidators(Validators.required);
    this.form.controls.lastName.addValidators(Validators.required);
    this.form.controls.username.addValidators(Validators.required);
    this.form.controls.password.addValidators(Validators.required);
    this.form.controls.email.addValidators([Validators.required, Validators.email]);
    for (const name of [
      'portalAlias',
      'firstName',
      'lastName',
      'username',
      'password',
      'email',
    ] as const) {
      this.form.controls[name].updateValueAndValidity();
    }
  }

  private loadPortal(id: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.portalService.getById(id).subscribe({
      next: (portal: Portal) => {
        // Patch the edit-relevant fields; the create-only credential fields and the
        // write-only processor secrets are intentionally left untouched.
        this.form.patchValue({
          portalName: portal.portalName,
          logoFile: portal.logoFile,
          footerText: portal.footerText,
          expiryDate: this.toDateInput(portal.expiryDate),
          userRegistration: portal.userRegistration,
          bannerAdvertising: portal.bannerAdvertising,
          currency: portal.currency,
          administratorId: portal.administratorId,
          hostFee: portal.hostFee,
          hostSpace: portal.hostSpace,
          pageQuota: portal.pageQuota,
          userQuota: portal.userQuota,
          description: portal.description,
          keyWords: portal.keyWords,
          backgroundFile: portal.backgroundFile,
          siteLogHistory: portal.siteLogHistory,
          splashTabId: portal.splashTabId,
          homeTabId: portal.homeTabId,
          loginTabId: portal.loginTabId,
          userTabId: portal.userTabId,
          defaultLanguage: portal.defaultLanguage,
          timeZoneOffset: portal.timeZoneOffset,
          homeDirectory: portal.homeDirectory,
        });
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.error.set(this.extractError(problem));
        this.loading.set(false);
      },
    });
  }

  save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const raw = this.form.getRawValue();

    if (this.isEdit && this.portalId !== null) {
      // MIGRATION: SiteSettings.ascx update -> PUT /api/portals/{id} (200).
      const body: UpdatePortalRequest = {
        portalName: raw.portalName,
        logoFile: raw.logoFile,
        footerText: raw.footerText,
        expiryDate: raw.expiryDate,
        userRegistration: Number(raw.userRegistration),
        bannerAdvertising: Number(raw.bannerAdvertising),
        currency: raw.currency,
        administratorId: Number(raw.administratorId),
        hostFee: Number(raw.hostFee),
        hostSpace: Number(raw.hostSpace),
        pageQuota: Number(raw.pageQuota),
        userQuota: Number(raw.userQuota),
        paymentProcessor: raw.paymentProcessor,
        processorUserId: raw.processorUserId,
        processorPassword: raw.processorPassword,
        description: raw.description,
        keyWords: raw.keyWords,
        backgroundFile: raw.backgroundFile,
        siteLogHistory: Number(raw.siteLogHistory),
        splashTabId: Number(raw.splashTabId),
        homeTabId: Number(raw.homeTabId),
        loginTabId: Number(raw.loginTabId),
        userTabId: Number(raw.userTabId),
        defaultLanguage: raw.defaultLanguage,
        timeZoneOffset: Number(raw.timeZoneOffset),
        homeDirectory: raw.homeDirectory,
      };
      this.portalService.update(this.portalId, body).subscribe({
        next: () => this.router.navigate(['/portals']),
        error: (problem: ProblemDetails) => {
          this.saving.set(false);
          this.error.set(this.extractError(problem));
        },
      });
      return;
    }

    // MIGRATION: Signup.ascx create -> POST /api/portals (201).
    const body: CreatePortalRequest = {
      portalName: raw.portalName,
      firstName: raw.firstName,
      lastName: raw.lastName,
      username: raw.username,
      password: raw.password,
      email: raw.email,
      description: raw.description,
      keyWords: raw.keyWords,
      homeDirectory: raw.homeDirectory,
      portalAlias: raw.portalAlias,
    };
    this.portalService.create(body).subscribe({
      next: () => this.router.navigate(['/portals']),
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.error.set(this.extractError(problem));
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/portals']);
  }

  requestDelete(): void {
    this.showDeleteDialog.set(true);
  }

  confirmDelete(): void {
    if (this.portalId === null) {
      return;
    }
    this.saving.set(true);
    this.portalService.remove(this.portalId).subscribe({
      next: () => this.router.navigate(['/portals']),
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.error.set(this.extractError(problem));
      },
    });
  }

  /** ISO 'yyyy-MM-ddTHH:mm:ss' -> 'yyyy-MM-dd' for a native <input type="date">; '' when empty. */
  private toDateInput(iso: string): string {
    return iso ? iso.substring(0, 10) : '';
  }

  // Reads RFC 7807 fields (ApiService already normalized the error to ProblemDetails).
  private extractError(problem: ProblemDetails): string {
    if (problem.errors) {
      const messages = Object.values(problem.errors).flat();
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }
    return problem.detail ?? problem.title ?? 'An unexpected error occurred.';
  }
}
