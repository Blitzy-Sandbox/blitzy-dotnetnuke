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
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { PortalService, type PortalRequest } from '../portal.service';
import { AuthService } from '../../../core/auth/auth.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { Portal, ProblemDetails } from '../../../core/models';

// MIGRATION: typed form model. Controls map 1:1 to PortalRequest (the 26 editable fields persisted by the
// legacy cmdUpdate_Click -> PortalController.UpdatePortalInfo call, SiteSettings.ascx.vb L772-781). Numeric
// fields are non-null FormControl<number>; optional text fields are non-null FormControl<string> (defaulting
// to ''); expiryDate is the only nullable control (FormControl<string | null>). portalId/guid are NOT form
// fields (portalId comes from the route on update; guid is read-only).
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
    const errors = this.problem()?.errors;
    return Array.isArray(errors) ? errors : [];
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
      processorPassword: portal.processorPassword ?? '',
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
  // reactive form, build a PortalRequest from getRawValue() (which includes the disabled host controls so their
  // unchanged values are still sent -- mirroring the legacy non-superuser path), and call create or update.
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.problem.set(null);

    const raw = this.form.getRawValue();
    // MIGRATION: empty expiry -> Null.NullDate (null) (SiteSettings L729-732).
    const dto: PortalRequest = {
      ...raw,
      expiryDate: raw.expiryDate ? raw.expiryDate : null,
    };

    const request$ = this.isEditMode()
      ? this.portalService.update(this.id()!, dto)
      : this.portalService.create(dto);

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

  cancel(): void {
    void this.router.navigate(['/portals']);
  }

  // The global error interceptor re-throws the original HttpErrorResponse; the RFC 7807 body is on `error`.
  private toProblemDetails(err: unknown): ProblemDetails | null {
    if (err instanceof HttpErrorResponse) {
      const body: unknown = err.error;
      if (typeof body === 'object' && body !== null) {
        return body as ProblemDetails;
      }
    }
    return null;
  }
}
