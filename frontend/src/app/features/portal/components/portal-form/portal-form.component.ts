// MIGRATION: Replaces the legacy Web Forms portal editor Website/admin/Portal/SiteSettings.ascx.vb
// (postback / ViewState / code-behind) with a stateless, standalone Angular 19 reactive form.
//   - cmdUpdate_Click (L687)            -> onSubmit()
//   - `If Page.IsValid` gate (L688)     -> reactive validators (form.invalid guard + markAllAsTouched)
//   - PortalController.UpdatePortalInfo  -> PortalService.updatePortal (edit) / createPortal (new)
// The legacy host/fee/quota/site-log fields (txtHostFee/txtHostSpace/txtPageQuota/txtUserQuota/
// txtSiteLogHistory, L704-727), payment-processor credentials, skinning/SSL/control-panel and
// portal-alias/template settings are OUT OF SCOPE for this core create/edit screen (AAP §0.2.2);
// they are not part of the editable 15-field subset. Non-edited-but-required wire fields are
// defaulted in create mode and carried verbatim from the loaded entity (via spread) in edit mode.
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../../models';
import { PortalService } from '../../services';
import { ProblemDetails } from '../../../../core/services/api.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/**
 * Strongly-typed reactive form model exposing ONLY the core editable subset of a portal — the fields
 * the legacy cmdUpdate_Click maps (SiteSettings.ascx.vb L772-786) that also exist on the
 * Portal / Create / Update wire contract. String fields are nullable to mirror the model.
 *
 * MIGRATION: the nullable foreign-key / tab-id fields (administratorId, splashTabId, homeTabId,
 * loginTabId, userTabId) are modeled as `FormControl<number | null>` — NOT non-nullable — to match the
 * `Portal` model (`number | null`) and the legacy `Null.NullInteger` "unset" sentinel (L734-752). This
 * preserves a null round-trip (behavioral equivalence, AAP §0.7.1): an unset id stays null instead of
 * being coerced to 0. The genuinely non-nullable counters userRegistration/bannerAdvertising stay
 * `FormControl<number>` (nonNullable), matching the model.
 */
interface PortalFormModel {
  portalName: FormControl<string | null>;
  description: FormControl<string | null>;
  keyWords: FormControl<string | null>;
  logoFile: FormControl<string | null>;
  backgroundFile: FormControl<string | null>;
  footerText: FormControl<string | null>;
  userRegistration: FormControl<number>;
  bannerAdvertising: FormControl<number>;
  currency: FormControl<string | null>;
  administratorId: FormControl<number | null>;
  expiryDate: FormControl<string | null>;
  splashTabId: FormControl<number | null>;
  homeTabId: FormControl<number | null>;
  loginTabId: FormControl<number | null>;
  userTabId: FormControl<number | null>;
}

@Component({
  selector: 'app-portal-form',
  templateUrl: './portal-form.component.html',
  styleUrl: './portal-form.component.scss',
  imports: [ReactiveFormsModule, FormControlsComponent, LoadingSpinnerComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly portalService = inject(PortalService);

  /** Target portal id (edit mode); `null` in create mode. */
  readonly portalId = signal<number | null>(null);

  /** True when editing an existing portal (drives the heading and read-only affordances). */
  readonly isEditMode = computed<boolean>(() => this.portalId() !== null);

  /** True while fetching an existing portal (edit mode). */
  readonly loading = signal<boolean>(false);

  /** True while a create/update request is in flight (disables the submit affordance). */
  readonly saving = signal<boolean>(false);

  // MIGRATION: RFC 7807 Problem Details field errors (ProblemDetails.errors) replace the legacy
  // per-control ASP.NET validator ErrorMessage rendering; keyed by camelCase controlId (e.g. portalName).
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  // MIGRATION (QA Finding A): a general submit-error banner message for save failures that carry NO
  // ProblemDetails.errors dictionary (503/500/network errors). Mirrors the blessed user-form `submitError`
  // signal so a failed save is never swallowed silently — distinct from the per-field `serverErrors`
  // (FluentValidation 400 keyed by control) which app-form-controls renders inline.
  readonly submitError = signal<string | null>(null);

  /** Set when the edit-mode load fails, so the template can surface a load error instead of the form. */
  readonly loadError = signal<string | null>(null);

  /** The entity loaded in edit mode, retained so the full UpdatePortalRequest can be reconstructed on submit. */
  private loadedPortal: Portal | null = null;

  /** Screen heading; switches between create and edit wording. */
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Portal' : 'New Portal'));

  // MIGRATION: `portalName` is the only validated control (legacy txtPortalName RequiredFieldValidator +
  // `Page.IsValid` gate, mirrored by the backend FluentValidation Create/UpdatePortalValidator: PortalName
  // NotEmpty). No maxLength/pattern/range validators are added (the backend declares none). `currency`
  // defaults to 'USD' (legacy L324-327: when the stored currency is null/unmatched, "USD" is selected).
  readonly form = new FormGroup<PortalFormModel>({
    portalName: new FormControl<string | null>(null, { validators: [Validators.required] }),
    description: new FormControl<string | null>(null),
    keyWords: new FormControl<string | null>(null),
    logoFile: new FormControl<string | null>(null),
    backgroundFile: new FormControl<string | null>(null),
    footerText: new FormControl<string | null>(null),
    userRegistration: new FormControl<number>(0, { nonNullable: true }),
    bannerAdvertising: new FormControl<number>(0, { nonNullable: true }),
    currency: new FormControl<string | null>('USD'),
    administratorId: new FormControl<number | null>(null),
    expiryDate: new FormControl<string | null>(null),
    splashTabId: new FormControl<number | null>(null),
    homeTabId: new FormControl<number | null>(null),
    loginTabId: new FormControl<number | null>(null),
    userTabId: new FormControl<number | null>(null),
  });

  /** Typed access to the individual controls for the template (`[control]="controls.portalName"`). */
  get controls(): PortalFormModel {
    return this.form.controls;
  }

  /**
   * Read-only, server-managed portal GUID, surfaced for DISPLAY ONLY in edit mode; returns `null` in
   * create mode (no loaded portal) so the template's `@if (guid())` block is hidden. Implemented as a
   * METHOD (not a `computed`) because it reads the non-signal `loadedPortal` field, which must be
   * re-evaluated on each change-detection pass — the same rationale as FormControlsComponent.visibleErrors().
   * MIGRATION: reproduces the legacy read-only `lblGUID` label
   * (Website/admin/Portal/SiteSettings.ascx.vb L273: `lblGUID.Text = objPortal.GUID.ToString.ToUpper`),
   * preserving the upper-cased display form. The GUID is never editable and is intentionally absent from the
   * create/update wire contract (it is server-managed via the [Portals].GUID `newid()` default).
   */
  guid(): string | null {
    return this.loadedPortal?.guid?.toUpperCase() ?? null;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam === null) {
      return; // create mode: pristine defaults
    }
    const id = Number(idParam);
    this.portalId.set(id);
    this.loadPortal(id);
  }

  // MIGRATION (QA Finding B): the edit-mode load is extracted into a re-runnable method so the template can
  // offer a "Retry" affordance when it fails. CRITICAL: the template hides the form entirely while the load
  // is in flight OR has failed (see portal-form.component.html), so a failed edit-load can never fall through
  // to the create (POST) branch of onSubmit() and silently CREATE a duplicate portal.
  private loadPortal(id: number): void {
    this.loadError.set(null);
    this.loading.set(true);
    this.portalService.getPortal(id).subscribe({
      next: (portal) => {
        this.loadedPortal = portal;
        this.patchForm(portal);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load the portal.');
        this.loading.set(false);
      },
    });
  }

  /** Retries a failed edit-mode load (template "Retry" affordance). No-op in create mode. */
  retryLoad(): void {
    const id = this.portalId();
    if (id !== null) {
      this.loadPortal(id);
    }
  }

  /**
   * Populates the form from a loaded portal (edit mode), mapping the 15 editable fields.
   * Reproduces the legacy bind semantics: currency null -> 'USD' (L324-327); expiryDate passes through
   * as `string | null`; nullable FK/tab ids pass through unchanged (preserving the "unset" null).
   */
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
      currency: portal.currency ?? 'USD',
      administratorId: portal.administratorId,
      expiryDate: portal.expiryDate,
      splashTabId: portal.splashTabId,
      homeTabId: portal.homeTabId,
      loginTabId: portal.loginTabId,
      userTabId: portal.userTabId,
    });
  }

  // MIGRATION: cmdUpdate_Click + `If Page.IsValid` (L687-688). The postback save becomes a stateless REST
  // call; invalid forms are gated client-side (markAllAsTouched surfaces the field messages) before issuing
  // a request, mirroring the legacy "save only when Page.IsValid" behavior.
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    // MIGRATION (QA Finding B): defense-in-depth guard. In edit mode, never fall through to the create
    // (POST) branch when the existing portal failed to load — without the loaded entity the full
    // UpdatePortalRequest cannot be reconstructed (buildUpdateRequest spreads ...loaded), and issuing a POST
    // would silently CREATE a duplicate instead of updating. The template already hides the form on a load
    // failure; this guard ensures onSubmit can never mis-route by HTTP method even if invoked directly.
    if (this.isEditMode() && this.loadedPortal === null) {
      return;
    }
    this.saving.set(true);
    this.serverErrors.set(null);
    this.submitError.set(null); // MIGRATION (QA Finding A): clear any prior general submit-error banner.

    const id = this.portalId();
    if (id !== null && this.loadedPortal !== null) {
      const request = this.buildUpdateRequest(id, this.loadedPortal);
      this.portalService.updatePortal(id, request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    } else {
      const request = this.buildCreateRequest();
      this.portalService.createPortal(request).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    }
  }

  /**
   * Builds the full CreatePortalRequest (every field is required on the wire contract). The form edits the
   * core 15 fields; the remaining non-edited fields are defaulted (server assigns the real values on create).
   * MIGRATION: `guid` is intentionally NOT sent — `[Portals].GUID` is server-managed (newid() default), so
   * the create contract omits it. `processorPassword` is a write-only credential not exposed by this screen
   * and is sent as null. Out-of-scope host/quota fields are defaulted (AAP §0.2.2).
   */
  private buildCreateRequest(): CreatePortalRequest {
    const v = this.form.getRawValue();
    return {
      // --- edited core fields (legacy SiteSettings.ascx.vb cmdUpdate_Click L772-786) ---
      portalName: v.portalName,
      description: v.description,
      keyWords: v.keyWords,
      logoFile: v.logoFile,
      backgroundFile: v.backgroundFile,
      footerText: v.footerText,
      userRegistration: v.userRegistration,
      bannerAdvertising: v.bannerAdvertising,
      currency: v.currency,
      administratorId: v.administratorId,
      expiryDate: v.expiryDate || null, // MIGRATION: blank -> null (legacy: blank -> NullDate, L729-731)
      splashTabId: v.splashTabId,
      homeTabId: v.homeTabId,
      loginTabId: v.loginTabId,
      userTabId: v.userTabId,
      // --- non-edited defaults (server assigns real values; host/quota fields out of scope, AAP §0.2.2) ---
      hostFee: 0,
      hostSpace: 0,
      pageQuota: 0,
      userQuota: 0,
      siteLogHistory: null,
      administratorRoleId: null,
      administratorRoleName: null,
      registeredRoleId: null,
      registeredRoleName: null,
      paymentProcessor: null,
      processorPassword: null, // MIGRATION: write-only credential, not edited here; sent null
      processorUserId: null,
      email: null,
      adminTabId: null,
      superTabId: 0,
      defaultLanguage: null,
      timeZoneOffset: 0,
      homeDirectory: null,
      version: null,
    };
  }

  /**
   * Builds the full UpdatePortalRequest by spreading the loaded entity (which carries the non-edited but
   * required fields — host/quota counters, role ids/names, payment-processor metadata, tab ids, language,
   * timezone, home directory, version) and overriding the route id plus the 15 editable fields. The spread
   * origin exempts the read-only Portal-only properties (guid, users, pages) from excess-property checks,
   * and the backend's System.Text.Json ignores unknown properties.
   * MIGRATION: `processorPassword` is absent from the read `Portal` (security-removed), so it is supplied
   * explicitly as null (write-only, not edited here). `portalID` is re-asserted to equal the route :id
   * (UpdatePortalValidator requires PortalID > 0 and a matching id).
   */
  private buildUpdateRequest(id: number, loaded: Portal): UpdatePortalRequest {
    const v = this.form.getRawValue();
    return {
      ...loaded,
      portalID: id, // MIGRATION: request.portalID must equal the route :id (server validates the match)
      processorPassword: null, // MIGRATION: write-only, absent from read Portal; sent null
      portalName: v.portalName,
      description: v.description,
      keyWords: v.keyWords,
      logoFile: v.logoFile,
      backgroundFile: v.backgroundFile,
      footerText: v.footerText,
      userRegistration: v.userRegistration,
      bannerAdvertising: v.bannerAdvertising,
      currency: v.currency,
      administratorId: v.administratorId,
      expiryDate: v.expiryDate || null, // MIGRATION: blank -> null (legacy: blank -> NullDate, L729-731)
      splashTabId: v.splashTabId,
      homeTabId: v.homeTabId,
      loginTabId: v.loginTabId,
      userTabId: v.userTabId,
    };
  }

  /**
   * Abandon the create/edit and return to the portal list WITHOUT saving.
   *
   * MIGRATION: provides the Cancel/back affordance for parity with the sibling
   * portal-settings form (portal-settings.component.ts onCancel) and the user-form /
   * role-form / module-settings forms, all of which navigate back to their list on cancel.
   * (QA F4 Finding #2: portal-form previously exposed only the submit button.) Navigation
   * mirrors onSaveSuccess so cancel and a successful save land on the same `/portals` list.
   */
  onCancel(): void {
    void this.router.navigate(['/portals']);
  }

  /** On a successful create/update, return to the portal list (parent feature route). */
  private onSaveSuccess(): void {
    this.saving.set(false);
    void this.router.navigate(['/portals']);
  }

  // MIGRATION: RFC 7807 error model — server-side FluentValidation failures arrive as ProblemDetails.errors
  // (field name -> messages) and are surfaced inline by app-form-controls, replacing the legacy per-validator
  // postback ErrorMessage display.
  private onSaveError(problem: ProblemDetails): void {
    this.saving.set(false);
    this.serverErrors.set(problem.errors ?? null);
    // MIGRATION (QA Finding A): also surface a general banner message so 503/500/network failures (which
    // carry NO `errors` dictionary) are not swallowed silently. Mirrors the blessed user-form pattern
    // (title ?? detail ?? fallback): a 503 shows its concise "Service Unavailable" title.
    this.submitError.set(problem.title ?? problem.detail ?? 'An error occurred while saving the portal.');
  }
}
