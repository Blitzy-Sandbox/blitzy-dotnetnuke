// MIGRATION: Angular 19 replacement for the legacy DNN "Admin -> Site Settings" editor
// (Website/admin/Portal/SiteSettings.ascx.vb, 896 lines, class SiteSettings : PortalModuleBase). Only the
// validation + orchestration logic is re-expressed here; ALL Web Forms postback/ViewState/PortalModuleBase
// machinery, skin/container pickers, the stylesheet editor, the SSL/ControlPanel/InlineEditor site-setting
// extras, desktop-module sync, file-upload controls, and the sitemap/search-engine/verification actions are
// discarded (out of scope per AAP Section 0.6.2). This single typed Reactive Form handles BOTH create
// ('new' route) and edit (':id/edit' route) modes.
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { PortalService, type CreatePortalRequest, type UpdatePortalRequest } from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
// MIGRATION: [QA F4-003] shared focus-first-invalid helper; [QA F4-013] shared error normaliser that
// converts a status-0/transport failure into a friendly ProblemDetails instead of a blind cast of the
// ProgressEvent body (which rendered no message).
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';
import { toProblemDetails as normalizeProblemDetails } from '../../../core/services/problem-details.util';
import type { Portal, ProblemDetails } from '../../../core/models';

// MIGRATION: typed form model. The editable site-settings controls (used by BOTH create and update) plus the
// create-only provisioning controls (email + admin* bootstrap, REQUIRED by the backend CreatePortalValidator and
// enabled/disabled per mode below). Numeric fields are non-null FormControl<number>; optional text fields are
// non-null FormControl<string> (defaulting to ''); expiryDate is the only nullable control
// (FormControl<string | null>). portalId/guid are NOT form fields (portalId comes from the route on update;
// guid is read-only). processorPassword remains a WRITE-only control (sent only on update; never patched from a
// read because the PortalDto projection omits it).
interface PortalFormModel {
  portalName: FormControl<string>;
  description: FormControl<string>;
  keyWords: FormControl<string>;
  footerText: FormControl<string>;
  logoFile: FormControl<string>;
  backgroundFile: FormControl<string>;
  userRegistration: FormControl<number>;
  bannerAdvertising: FormControl<number>;
  currency: FormControl<string>;
  administratorId: FormControl<number>;
  hostFee: FormControl<number>;
  hostSpace: FormControl<number>;
  pageQuota: FormControl<number>;
  userQuota: FormControl<number>;
  siteLogHistory: FormControl<number>;
  expiryDate: FormControl<string | null>;
  paymentProcessor: FormControl<string>;
  processorUserId: FormControl<string>;
  processorPassword: FormControl<string>;
  splashTabId: FormControl<number>;
  homeTabId: FormControl<number>;
  loginTabId: FormControl<number>;
  userTabId: FormControl<number>;
  defaultLanguage: FormControl<string>;
  timeZoneOffset: FormControl<number>;
  homeDirectory: FormControl<string>;
  // MIGRATION: create-only provisioning fields. `email` is the portal contact email (CreatePortalRequest.Email);
  // the admin* group provisions the portal's first administrator account (legacy Signup.ascx.vb). All are
  // REQUIRED by CreatePortalValidator on create and are disabled in edit mode (the UpdatePortalRequest contract
  // does not accept them).
  email: FormControl<string>;
  adminUsername: FormControl<string>;
  adminPassword: FormControl<string>;
  adminFirstName: FormControl<string>;
  adminLastName: FormControl<string>;
  adminEmail: FormControl<string>;
}

@Component({
  selector: 'app-portal-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormControlComponent, LoadingSpinnerComponent],
  templateUrl: './portal-form.component.html',
  styleUrl: './portal-form.component.scss',
})
export class PortalFormComponent {
  private readonly portalService = inject(PortalService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  // MIGRATION: [QA F4-003] host element used to locate the first invalid control on an invalid submit.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  // MIGRATION: route input binding (withComponentInputBinding() is enabled in app.config.ts). `id` is
  // undefined on the 'new' (create) route and the :id route param on the ':id/edit' (edit) route. The legacy
  // editor distinguished modes via the PortalId querystring read in Page_Load.
  readonly id = input<string>();
  readonly isEditMode = computed(() => this.id() != null);

  // MIGRATION: host-field gating source. SiteSettings showed/validated the host-only fields ONLY for a
  // superuser (UserInfo.IsSuperUser, L498-516 display, L759-770 throw-on-change).
  readonly isSuperUser = computed(() => this.auth.currentUser()?.isSuperUser ?? false);

  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly problem = signal<ProblemDetails | null>(null);

  // MIGRATION: surface a flat string[] ProblemDetails.errors payload (the ApiControllerBase Result.Errors
  // shape) as a form-level summary; <app-form-control> only renders the per-field Record<string,string[]> shape.
  readonly errorSummary = computed<string[]>(() => {
    const problem = this.problem();
    if (problem === null) {
      return [];
    }
    const errors = problem.errors;
    // ApiControllerBase Result.Errors -> flat business-error list: surfaced verbatim (unchanged behavior).
    if (Array.isArray(errors)) {
      return errors;
    }
    // MIGRATION: (QA Issue 1, secondary impact) a NON-array `errors` is the [ApiController] per-field
    // validation object (rendered inline by <app-form-control>) OR an empty object on a 500. The per-field map
    // is not itself a summary, so surface the RFC 7807 title + detail here so a 500 -- and any non-field error
    // -- is never silent (previously this returned [] and title/detail were never rendered). Mirrors the
    // parseProblemDetails pattern used by login/profile/role-assignment (AAP 0.1.1 / 0.7.5).
    const messages: string[] = [];
    if (typeof problem.title === 'string' && problem.title.length > 0) {
      messages.push(problem.title);
    }
    if (typeof problem.detail === 'string' && problem.detail.length > 0) {
      messages.push(problem.detail);
    }
    return messages;
  });

  // MIGRATION: the six host-only controls gated to superusers (SiteSettings tblHost, L498-516 / L759-770).
  private readonly hostFieldKeys = [
    'hostFee',
    'hostSpace',
    'pageQuota',
    'userQuota',
    'siteLogHistory',
    'expiryDate',
  ] as const;

  // MIGRATION: create-only provisioning controls (email + admin* bootstrap). REQUIRED by CreatePortalValidator
  // on create; disabled in edit mode so their required validators do not block the update form and they are not
  // sent on a PUT (the UpdatePortalRequest contract omits them).
  private readonly createOnlyFieldKeys = [
    'email',
    'adminUsername',
    'adminPassword',
    'adminFirstName',
    'adminLastName',
    'adminEmail',
  ] as const;

  // MIGRATION: typed Reactive Form. Defaults mirror the legacy: currency 'USD' (L324-328), siteLogHistory -1
  // (L724), tab IDs Null.NullInteger == -1 (L300-323), numeric fields 0 (L704-727). portalName is required
  // (legacy RequiredFieldValidator on the site name). The min(0) validators on the fee/quota fields are
  // inferred from the legacy RangeValidators (exact .ascx markup not migrated).
  readonly form = new FormGroup<PortalFormModel>({
    portalName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true }),
    keyWords: new FormControl('', { nonNullable: true }),
    footerText: new FormControl('', { nonNullable: true }),
    logoFile: new FormControl('', { nonNullable: true }),
    backgroundFile: new FormControl('', { nonNullable: true }),
    userRegistration: new FormControl(0, { nonNullable: true }),
    bannerAdvertising: new FormControl(0, { nonNullable: true }),
    currency: new FormControl('USD', { nonNullable: true }),
    administratorId: new FormControl(0, { nonNullable: true }),
    // MIGRATION: SiteSettings.ascx.vb L759-770 -- inferred RangeValidator min(0).
    hostFee: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    hostSpace: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    pageQuota: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    userQuota: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    // MIGRATION: SiteSettings.ascx.vb L724 -- siteLogHistory default -1 when empty.
    siteLogHistory: new FormControl(-1, { nonNullable: true }),
    expiryDate: new FormControl<string | null>(null),
    paymentProcessor: new FormControl('', { nonNullable: true }),
    processorUserId: new FormControl('', { nonNullable: true }),
    processorPassword: new FormControl('', { nonNullable: true }),
    // MIGRATION: Null.NullInteger (-1) sentinel when no tab is selected (SiteSettings L300-323).
    splashTabId: new FormControl(-1, { nonNullable: true }),
    homeTabId: new FormControl(-1, { nonNullable: true }),
    loginTabId: new FormControl(-1, { nonNullable: true }),
    userTabId: new FormControl(-1, { nonNullable: true }),
    defaultLanguage: new FormControl('', { nonNullable: true }),
    timeZoneOffset: new FormControl(0, { nonNullable: true }),
    homeDirectory: new FormControl('', { nonNullable: true }),
    // MIGRATION: create-only provisioning fields (REQUIRED by CreatePortalValidator). Toggled enabled/disabled
    // by mode in the constructor effect; only sent on create.
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    adminUsername: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    adminPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    adminFirstName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    adminLastName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    adminEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor() {
    // MIGRATION: host-field gating (SiteSettings.ascx.vb L498-516 display + L759-770 throw-on-change). A
    // non-superuser MUST NOT be able to change hostFee/hostSpace/pageQuota/userQuota/siteLogHistory/expiryDate;
    // disabling the controls enforces this (the template also hides them). Reacts to currentUser() loading.
    effect(() => {
      const superUser = this.isSuperUser();
      for (const key of this.hostFieldKeys) {
        const control = this.form.controls[key];
        if (superUser) {
          control.enable({ emitEvent: false });
        } else {
          control.disable({ emitEvent: false });
        }
      }
    });

    // MIGRATION: create-only provisioning gating. The backend create contract REQUIRES email + admin* fields
    // (CreatePortalValidator); the update contract accepts neither. Disabling these controls in edit mode keeps
    // them out of the update form's validity and out of the PUT payload, while enabling+requiring them on the
    // 'new' route. The template also only renders them in create mode.
    effect(() => {
      const editMode = this.isEditMode();
      for (const key of this.createOnlyFieldKeys) {
        const control = this.form.controls[key];
        if (editMode) {
          control.disable({ emitEvent: false });
        } else {
          control.enable({ emitEvent: false });
        }
      }
    });

    // MIGRATION: SiteSettings Page_Load -> GetPortal(intPortalId) field population (L266-419). When the route
    // supplies an id (edit mode), load the portal and patch the form; the 'new' route leaves defaults.
    effect(() => {
      const portalId = this.id();
      if (portalId != null) {
        this.loadPortal(portalId);
      }
    });
  }

  private loadPortal(id: string): void {
    this.loading.set(true);
    this.portalService.getById(id).subscribe({
      next: (portal) => {
        this.patchForm(portal);
        this.loading.set(false);
      },
      error: () => {
        // The global error interceptor surfaces/logs the ProblemDetails; just clear the loading state.
        this.loading.set(false);
      },
    });
  }

  // MIGRATION: SiteSettings Page_Load field population (L266-419). Nullable Portal model fields are coerced to
  // the form's non-null control types ('' for text, sentinel numbers preserved); currency falls back to 'USD'
  // (L324-328); the expiry datetime is reduced to a yyyy-MM-dd value for the date control.
  private patchForm(portal: Portal): void {
    this.form.patchValue({
      portalName: portal.portalName,
      description: portal.description ?? '',
      keyWords: portal.keyWords ?? '',
      footerText: portal.footerText ?? '',
      logoFile: portal.logoFile ?? '',
      backgroundFile: portal.backgroundFile ?? '',
      userRegistration: portal.userRegistration,
      bannerAdvertising: portal.bannerAdvertising,
      currency: portal.currency ?? 'USD',
      administratorId: portal.administratorId,
      hostFee: portal.hostFee,
      hostSpace: portal.hostSpace,
      pageQuota: portal.pageQuota,
      userQuota: portal.userQuota,
      siteLogHistory: portal.siteLogHistory,
      expiryDate: portal.expiryDate != null ? portal.expiryDate.substring(0, 10) : null,
      paymentProcessor: portal.paymentProcessor ?? '',
      processorUserId: portal.processorUserId ?? '',
      // MIGRATION: processorPassword is NOT patched from the read — the backend PortalDto omits it (write-only on
      // update). The control stays empty on load and is only sent on PUT if the operator types a new value.
      splashTabId: portal.splashTabId,
      homeTabId: portal.homeTabId,
      loginTabId: portal.loginTabId,
      userTabId: portal.userTabId,
      defaultLanguage: portal.defaultLanguage ?? '',
      timeZoneOffset: portal.timeZoneOffset,
      homeDirectory: portal.homeDirectory ?? '',
    });
  }

  // MIGRATION: cmdUpdate_Click (SiteSettings.ascx.vb L688-790). Legacy guarded on Page.IsValid, read every
  // field (parse/default rules L704-732), then called PortalController.UpdatePortalInfo. Here we validate the
  // reactive form, then build a CONTRACT-SPECIFIC DTO from getRawValue() (which includes disabled controls so
  // unchanged host values are still sent -- mirroring the legacy non-superuser path). Create and update use
  // DIFFERENT backend contracts: create REQUIRES email + admin* provisioning fields (CreatePortalValidator) and
  // rejects processorPassword/administratorId/tab-ids; update carries the write-only processorPassword and the
  // administrator/tab-id fields but NOT email. We therefore build the two payloads explicitly rather than
  // spreading a single shape.
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // MIGRATION: [QA F4-003] move focus + scroll to the first invalid control. The Create button sits
      // below seven required fields; without this an empty submit gave ZERO visible feedback (the invalid
      // controls were off-screen and focus stayed on the button).
      focusFirstInvalidControl(this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.problem.set(null);

    const raw = this.form.getRawValue();
    // MIGRATION: empty expiry -> Null.NullDate (null) (SiteSettings L729-732).
    const expiryDate = raw.expiryDate ? raw.expiryDate : null;

    const request$ = this.isEditMode()
      ? this.portalService.update(this.id()!, this.buildUpdateRequest(raw, expiryDate))
      : this.portalService.create(this.buildCreateRequest(raw, expiryDate));

    request$.subscribe({
      next: (portal) => {
        this.submitting.set(false);
        // MIGRATION: legacy redirected back to the admin referrer; the SPA navigates to the saved portal.
        void this.router.navigate(['/portals', portal.portalId]);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        // MIGRATION: server-side validation mapping -> RFC 7807 ProblemDetails surfaced per field via
        // <app-form-control> [errors] and (for flat-array errors) the form-level summary.
        this.problem.set(this.toProblemDetails(err));
      },
    });
  }

  // MIGRATION: build the POST /portals body (CreatePortalRequest). REQUIRES email + the admin* provisioning
  // group (CreatePortalValidator); excludes processorPassword/administratorId/tab-ids/siteLogHistory which are
  // update-only in the backend contract.
  private buildCreateRequest(
    raw: ReturnType<PortalFormComponent['form']['getRawValue']>,
    expiryDate: string | null,
  ): CreatePortalRequest {
    return {
      portalName: raw.portalName,
      description: raw.description,
      keyWords: raw.keyWords,
      logoFile: raw.logoFile,
      footerText: raw.footerText,
      expiryDate,
      userRegistration: raw.userRegistration,
      bannerAdvertising: raw.bannerAdvertising,
      currency: raw.currency,
      hostFee: raw.hostFee,
      hostSpace: raw.hostSpace,
      pageQuota: raw.pageQuota,
      userQuota: raw.userQuota,
      email: raw.email,
      defaultLanguage: raw.defaultLanguage,
      timeZoneOffset: raw.timeZoneOffset,
      homeDirectory: raw.homeDirectory,
      adminUsername: raw.adminUsername,
      adminPassword: raw.adminPassword,
      adminFirstName: raw.adminFirstName,
      adminLastName: raw.adminLastName,
      adminEmail: raw.adminEmail,
    };
  }

  // MIGRATION: build the PUT /portals/{id} body (UpdatePortalRequest). Carries portalId (echoed from the route),
  // the administrator/payment/tab-id fields, and the WRITE-only processorPassword (empty -> null so a blank
  // field does not transmit an empty credential). Excludes the create-only email + admin* group.
  private buildUpdateRequest(
    raw: ReturnType<PortalFormComponent['form']['getRawValue']>,
    expiryDate: string | null,
  ): UpdatePortalRequest {
    return {
      portalId: Number(this.id()),
      portalName: raw.portalName,
      logoFile: raw.logoFile,
      footerText: raw.footerText,
      expiryDate,
      userRegistration: raw.userRegistration,
      bannerAdvertising: raw.bannerAdvertising,
      currency: raw.currency,
      administratorId: raw.administratorId,
      hostFee: raw.hostFee,
      hostSpace: raw.hostSpace,
      pageQuota: raw.pageQuota,
      userQuota: raw.userQuota,
      paymentProcessor: raw.paymentProcessor,
      processorUserId: raw.processorUserId,
      processorPassword: raw.processorPassword ? raw.processorPassword : null,
      description: raw.description,
      keyWords: raw.keyWords,
      backgroundFile: raw.backgroundFile,
      siteLogHistory: raw.siteLogHistory,
      splashTabId: raw.splashTabId,
      homeTabId: raw.homeTabId,
      loginTabId: raw.loginTabId,
      userTabId: raw.userTabId,
      defaultLanguage: raw.defaultLanguage,
      timeZoneOffset: raw.timeZoneOffset,
      homeDirectory: raw.homeDirectory,
    };
  }

  cancel(): void {
    void this.router.navigate(['/portals']);
  }

  // MIGRATION: [QA F4-013] normalise the caught error via the shared helper. The previous implementation
  // blindly cast `err.error` to ProblemDetails whenever it was a non-null object -- but on a status-0
  // transport failure `err.error` is a ProgressEvent with no title/detail/errors, so errorSummary() was
  // empty and the form showed NO feedback. The shared helper recognises the RFC 7807 body, synthesises a
  // friendly "Unable to reach the server." envelope for status-0, and never leaks the request URL.
  private toProblemDetails(err: unknown): ProblemDetails {
    return normalizeProblemDetails(err);
  }
}
