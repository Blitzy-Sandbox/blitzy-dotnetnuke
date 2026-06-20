import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { Portal, UpdatePortalRequest } from '../../models';
import { PortalService } from '../../services';
import { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';

/**
 * Typed reactive-form model for the editable subset of Portal configuration.
 *
 * MIGRATION: mirrors the in-scope fields bound in SiteSettings.ascx.vb Page_Load (L232-521)
 * and saved in cmdUpdate_Click (L687-822). Out-of-scope legacy sections (skins/containers, SSL,
 * control-panel, premium desktop modules, stylesheet upload/restore, portal-alias management, and
 * search-engine/sitemap submit actions) are intentionally omitted (AAP §0.2.2).
 *
 * MIGRATION: the editable numeric controls are declared non-nullable (FormControl<number>) with
 * legacy parity defaults. The backing Portal / UpdatePortalRequest model exposes several of these
 * ids as `number | null`, so loaded values are coalesced to their numeric defaults on patch (see
 * patchForm) and flow back out as plain numbers (a `number` is assignable to the model's
 * `number | null` fields).
 */
interface PortalSettingsForm {
  portalName: FormControl<string | null>;
  description: FormControl<string | null>;
  keyWords: FormControl<string | null>;
  logoFile: FormControl<string | null>;
  backgroundFile: FormControl<string | null>;
  footerText: FormControl<string | null>;
  userRegistration: FormControl<number>;
  bannerAdvertising: FormControl<number>;
  administratorId: FormControl<number>;
  currency: FormControl<string | null>;
  defaultLanguage: FormControl<string | null>;
  timeZoneOffset: FormControl<number>;
  splashTabId: FormControl<number>;
  homeTabId: FormControl<number>;
  loginTabId: FormControl<number>;
  userTabId: FormControl<number>;
  paymentProcessor: FormControl<string | null>;
  processorUserId: FormControl<string | null>;
  processorPassword: FormControl<string | null>;
  hostFee: FormControl<number>;
  hostSpace: FormControl<number>;
  pageQuota: FormControl<number>;
  userQuota: FormControl<number>;
  siteLogHistory: FormControl<number>;
  expiryDate: FormControl<string | null>;
  homeDirectory: FormControl<string | null>;
}

/**
 * PortalSettingsComponent — the sectioned portal configuration editor.
 *
 * Reproduces the in-scope portions of the legacy DNN Web Forms control
 * `Website/admin/Portal/SiteSettings.ascx.vb` (Page_Load bind + cmdUpdate_Click save) with UI
 * functional parity (AAP §0.3.4 / §0.7.1). It is lazy-loaded by the parent `portal.routes.ts` at
 * `:id/settings` via `loadComponent`; the parent `'portals'` route already applies `authGuard`, so
 * it is intentionally NOT re-declared here.
 *
 * All component state is held in signals; the editable fields are a typed Reactive Form. All HTTP
 * is routed through `PortalService` (never `HttpClient`). RFC 7807 field errors returned by the API
 * are surfaced to the per-field `app-form-controls` wrappers through the `serverErrors` signal.
 */
@Component({
  selector: 'app-portal-settings',
  templateUrl: './portal-settings.component.html',
  styleUrl: './portal-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    HasPermissionDirective,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
  ],
})
export class PortalSettingsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly portalService = inject(PortalService);
  private readonly auth = inject(AuthService);

  // Public (no access modifier) so the template (strictTemplates) and the spec (dot-access under
  // strict TS) can read these members without a protected-access compile error.
  readonly portalId = signal(0);
  readonly portal = signal<Portal | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly loadError = signal<string | null>(null);
  /** RFC 7807 ProblemDetails.errors map (keyed by camelCase field == controlId) for field-level display. */
  readonly serverErrors = signal<Record<string, string[]> | null>(null);
  readonly showDeleteDialog = signal(false);

  /** Legacy lblGUID (SiteSettings.ascx.vb L273 `lblGUID.Text = objPortal.GUID.ToString.ToUpper`). */
  readonly guidDisplay = computed(() => (this.portal()?.guid ?? '').toUpperCase());

  // MIGRATION: legacy cmdDelete is host-only and hidden for the CURRENT portal
  // (SiteSettings.ascx.vb L503: `cmdDelete.Visible = (intPortalId <> PortalId)`). Reproduced here:
  // the delete affordance is hidden when an operator edits their own portal, and is additionally
  // gated in the template via *appHasPermission="'DELETE'". When the current user's portal is
  // unknown (no authenticated user resolved yet), the comparison collapses to equality and the
  // affordance stays hidden (fail-closed). Server-side authorization remains authoritative.
  // MIGRATION: `User.portalID` (camelCase wire name preserves the trailing `ID` acronym), not
  // `portalId` — read from the AuthService.currentUser signal.
  readonly canDelete = computed(
    () => this.portalId() !== (this.auth.currentUser()?.portalID ?? this.portalId()),
  );

  readonly deleteMessage = computed(
    () =>
      `Are you sure you want to delete the portal "${this.portal()?.portalName ?? ''}"? This action cannot be undone.`,
  );

  readonly portalNameMessages: Record<string, string> = {
    required: 'Portal name is required.',
  };

  readonly form = new FormGroup<PortalSettingsForm>({
    portalName: new FormControl<string | null>(null, { validators: [Validators.required] }),
    description: new FormControl<string | null>(null),
    keyWords: new FormControl<string | null>(null),
    logoFile: new FormControl<string | null>(null),
    backgroundFile: new FormControl<string | null>(null),
    footerText: new FormControl<string | null>(null),
    userRegistration: new FormControl(0, { nonNullable: true }),
    bannerAdvertising: new FormControl(0, { nonNullable: true }),
    administratorId: new FormControl(0, { nonNullable: true }),
    currency: new FormControl<string | null>('USD'),
    defaultLanguage: new FormControl<string | null>(null),
    timeZoneOffset: new FormControl(0, { nonNullable: true }),
    splashTabId: new FormControl(0, { nonNullable: true }),
    homeTabId: new FormControl(0, { nonNullable: true }),
    loginTabId: new FormControl(0, { nonNullable: true }),
    userTabId: new FormControl(0, { nonNullable: true }),
    paymentProcessor: new FormControl<string | null>(null),
    processorUserId: new FormControl<string | null>(null),
    processorPassword: new FormControl<string | null>(null),
    hostFee: new FormControl(0, { nonNullable: true }),
    hostSpace: new FormControl(0, { nonNullable: true }),
    pageQuota: new FormControl(0, { nonNullable: true }),
    userQuota: new FormControl(0, { nonNullable: true }),
    siteLogHistory: new FormControl(-1, { nonNullable: true }),
    expiryDate: new FormControl<string | null>(null),
    homeDirectory: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const id = Number(idParam);
    if (!idParam || Number.isNaN(id)) {
      this.loading.set(false);
      this.loadError.set('Invalid portal id.');
      return;
    }
    this.portalId.set(id);
    this.loadPortal(id);
  }

  /** Load the portal by id and bind it to the form. Legacy: SiteSettings.ascx.vb Page_Load bind. */
  private loadPortal(id: number): void {
    this.loading.set(true);
    this.portalService.getPortal(id).subscribe({
      next: (portal) => {
        this.portal.set(portal);
        this.patchForm(portal);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loading.set(false);
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load portal settings.');
      },
    });
  }

  // MIGRATION: explicit field-by-field patch (NOT a spread of `Portal`) — the read model carries
  // extra keys (portalID/guid/users/pages) that are not form controls, and several id fields are
  // `number | null` on the wire; those are coalesced to the legacy parity defaults so the
  // non-nullable numeric controls remain typed as `number` (siteLogHistory → -1, the legacy
  // Null.NullInteger sentinel at SiteSettings.ascx.vb L724; the others → 0).
  // MIGRATION (SECURITY): `processorPassword` is intentionally NOT patched — it is absent from the
  // read `Portal` (write-only payment-processor credential, never returned to the SPA), so its
  // control stays empty until an operator enters a new value.
  private patchForm(portal: Portal): void {
    this.form.patchValue({
      portalName: portal.portalName,
      description: portal.description,
      keyWords: portal.keyWords,
      logoFile: portal.logoFile,
      backgroundFile: portal.backgroundFile,
      footerText: portal.footerText,
      userRegistration: portal.userRegistration,
      bannerAdvertising: portal.bannerAdvertising,
      administratorId: portal.administratorId ?? 0,
      currency: portal.currency ?? 'USD',
      defaultLanguage: portal.defaultLanguage,
      timeZoneOffset: portal.timeZoneOffset,
      splashTabId: portal.splashTabId ?? 0,
      homeTabId: portal.homeTabId ?? 0,
      loginTabId: portal.loginTabId ?? 0,
      userTabId: portal.userTabId ?? 0,
      paymentProcessor: portal.paymentProcessor,
      processorUserId: portal.processorUserId,
      hostFee: portal.hostFee,
      hostSpace: portal.hostSpace,
      pageQuota: portal.pageQuota,
      userQuota: portal.userQuota,
      siteLogHistory: portal.siteLogHistory ?? -1,
      expiryDate: portal.expiryDate,
      homeDirectory: portal.homeDirectory,
    });
  }

  /** Persist the edited configuration. Legacy: SiteSettings.ascx.vb cmdUpdate_Click (L687-822). */
  onSubmit(): void {
    this.saved.set(false);
    this.serverErrors.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const portal = this.portal();
    if (!portal) {
      return;
    }
    const raw = this.form.getRawValue();
    // MIGRATION: UpdatePortalRequest = loaded Portal (carry-over read-only fields) + edited form
    // values. `portalID` is FORCED to the route id so request.portalID === id (the PortalService /
    // server contract). `guid` is intentionally absent from UpdatePortalRequest — [Portals].GUID is
    // server-managed (newid() default) and the backend Update DTO omits it. `processorPassword` is
    // write-only: it is sent from the form (the read model never echoes it back), so an untouched
    // form submits an empty credential. Carry-over fields not exposed as controls
    // (administrator/registered role id+name, email, adminTabId, superTabId, version) are preserved
    // verbatim from the loaded portal so a save never clears data the form does not edit.
    const request: UpdatePortalRequest = {
      portalID: this.portalId(),
      portalName: raw.portalName,
      logoFile: raw.logoFile,
      footerText: raw.footerText,
      expiryDate: raw.expiryDate,
      userRegistration: raw.userRegistration,
      bannerAdvertising: raw.bannerAdvertising,
      administratorId: raw.administratorId,
      currency: raw.currency,
      hostFee: raw.hostFee,
      hostSpace: raw.hostSpace,
      pageQuota: raw.pageQuota,
      userQuota: raw.userQuota,
      administratorRoleId: portal.administratorRoleId,
      administratorRoleName: portal.administratorRoleName,
      registeredRoleId: portal.registeredRoleId,
      registeredRoleName: portal.registeredRoleName,
      description: raw.description,
      keyWords: raw.keyWords,
      backgroundFile: raw.backgroundFile,
      paymentProcessor: raw.paymentProcessor,
      processorPassword: raw.processorPassword,
      processorUserId: raw.processorUserId,
      siteLogHistory: raw.siteLogHistory,
      email: portal.email,
      adminTabId: portal.adminTabId,
      superTabId: portal.superTabId,
      splashTabId: raw.splashTabId,
      homeTabId: raw.homeTabId,
      loginTabId: raw.loginTabId,
      userTabId: raw.userTabId,
      defaultLanguage: raw.defaultLanguage,
      timeZoneOffset: raw.timeZoneOffset,
      homeDirectory: raw.homeDirectory,
      version: portal.version,
    };
    this.saving.set(true);
    this.portalService.updatePortal(this.portalId(), request).subscribe({
      next: (updated) => {
        this.portal.set(updated);
        this.patchForm(updated);
        this.form.markAsPristine();
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (problem: ProblemDetails) => {
        this.serverErrors.set(problem.errors ?? null);
        this.saving.set(false);
      },
    });
  }

  onDeleteClick(): void {
    this.showDeleteDialog.set(true);
  }

  onCancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  /** Confirm deletion. Legacy: SiteSettings.ascx.vb cmdDelete_Click (L556) → server cascade. */
  onConfirmDelete(): void {
    this.showDeleteDialog.set(false);
    this.portalService.deletePortal(this.portalId()).subscribe({
      next: () => {
        void this.router.navigate(['/portals']);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to delete portal.');
      },
    });
  }

  onCancel(): void {
    void this.router.navigate(['/portals']);
  }
}
