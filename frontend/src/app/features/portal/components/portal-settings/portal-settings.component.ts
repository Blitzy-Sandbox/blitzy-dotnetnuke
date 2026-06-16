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
 * MIGRATION: mirrors the in-scope fields bound in SiteSettings.ascx.vb Page_Load (L232-521)
 * and saved in cmdUpdate_Click (L687-822). Out-of-scope legacy sections (skins/containers, SSL,
 * control-panel, desktop modules, stylesheet, alias management) are intentionally omitted (AAP 0.2.2).
 *
 * MIGRATION: the legacy combo-box / radio pickers are simplified to scalar numeric-id and text
 * inputs because the shared FormControlsComponent renders a single native <input> only and
 * PortalService exposes no admin-user, tab, or role list endpoints to populate a picker:
 *   - cboAdministratorId (user picker) -> administratorId numeric-id input
 *   - cboSplashTabId / cboHomeTabId / cboLoginTabId / cboUserTabId (tab pickers) -> numeric-id inputs
 *   - optUserRegistration / optBanners (radio/select) -> userRegistration / bannerAdvertising numeric-index inputs
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

  // Public (no modifier) so the template (strictTemplates) and the spec (dot-access) can read them.
  readonly portalId = signal(0);
  readonly portal = signal<Portal | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly loadError = signal<string | null>(null);
  /** RFC 7807 ProblemDetails.errors map (keyed by camelCase field == controlId) for field-level display. */
  readonly serverErrors = signal<Record<string, string[]> | null>(null);
  readonly showDeleteDialog = signal(false);

  /** Legacy lblGUID: GUID displayed read-only and uppercased. */
  readonly guidDisplay = computed(() => (this.portal()?.guid ?? '').toUpperCase());

  // MIGRATION: legacy cmdDelete is host-only and hidden for the CURRENT portal
  // (SiteSettings.ascx.vb L498-516 wraps the host affordances in `If UserInfo.IsSuperUser`, and
  // L503: `cmdDelete.Visible = (intPortalId <> PortalId)`). Reproduced here: the delete affordance
  // is hidden when editing one's own portal; the DELETE permission is additionally gated in the
  // template via *appHasPermission. Server authorization is authoritative.
  // NOTE: User.portalID (capital ID) is the camelCased C# `PortalID` wire field on the User DTO.
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

  // MIGRATION: host-field authorization. The host/quota/fee/expiry controls (hostFee, hostSpace,
  // pageQuota, userQuota, siteLogHistory, expiryDate) were rendered only to super (host) users in
  // the legacy control (`If UserInfo.IsSuperUser` gate around tblHost, SiteSettings.ascx.vb
  // L498-516). They are declared here unconditionally but the template restricts their section
  // behind *appHasPermission="'MANAGE_SETTINGS'"; the backend remains the authoritative enforcer
  // (a non-host caller's edits to these fields are rejected server-side).
  readonly form = new FormGroup<PortalSettingsForm>({
    portalName: new FormControl<string | null>(null, { validators: [Validators.required] }),
    description: new FormControl<string | null>(null),
    keyWords: new FormControl<string | null>(null),
    logoFile: new FormControl<string | null>(null),
    backgroundFile: new FormControl<string | null>(null),
    footerText: new FormControl<string | null>(null),
    // MIGRATION: numeric-index inputs replacing legacy optUserRegistration/optBanners radio/select.
    userRegistration: new FormControl(0, { nonNullable: true }),
    bannerAdvertising: new FormControl(0, { nonNullable: true }),
    // MIGRATION: numeric-id input replacing legacy cboAdministratorId user picker.
    administratorId: new FormControl(0, { nonNullable: true }),
    currency: new FormControl<string | null>('USD'),
    defaultLanguage: new FormControl<string | null>(null),
    timeZoneOffset: new FormControl(0, { nonNullable: true }),
    // MIGRATION: numeric-id inputs replacing legacy cbo*TabId tab pickers.
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
    // Legacy Null.NullInteger sentinel (-1) is the parity default for "keep all history".
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

  // MIGRATION: the Portal read projection models several legacy `int?` columns as `number | null`
  // (administratorId, *TabId, siteLogHistory). The non-nullable numeric form controls coalesce an
  // absent server value to the control's parity default (0 for id-style fields; -1 for
  // siteLogHistory == Null.NullInteger). processorPassword is intentionally ABSENT from the read
  // model (write-only credential, never returned by the API), so it is not patched and the control
  // keeps its empty default until the operator enters a new value to update it.
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
    // MIGRATION: UpdatePortalRequest = loaded Portal (carry-over read-only fields) + edited form values.
    // portalID is forced to the route id so request.portalID === id (PortalService contract).
    // Carry-over `int?` fields (administratorRoleId, registeredRoleId, adminTabId) are coalesced to 0
    // because the update contract requires non-null numbers while the read model exposes them as
    // `number | null`.
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
      administratorRoleId: portal.administratorRoleId ?? 0,
      administratorRoleName: portal.administratorRoleName,
      registeredRoleId: portal.registeredRoleId ?? 0,
      registeredRoleName: portal.registeredRoleName,
      description: raw.description,
      keyWords: raw.keyWords,
      backgroundFile: raw.backgroundFile,
      guid: portal.guid,
      paymentProcessor: raw.paymentProcessor,
      processorPassword: raw.processorPassword,
      processorUserId: raw.processorUserId,
      siteLogHistory: raw.siteLogHistory,
      email: portal.email,
      adminTabId: portal.adminTabId ?? 0,
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
