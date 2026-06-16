// MIGRATION: This standalone Angular 19 component replaces the legacy DotNetNuke Web Forms editor
// MIGRATION: Website/admin/Portal/SiteSettings.ascx.vb (896 lines). The server-rendered
// MIGRATION: postback/ViewState model is reinterpreted as a stateless, client-side reactive form:
// MIGRATION:   - cmdUpdate_Click (L687) gated by `If Page.IsValid` (L688) -> onSubmit() gated by reactive validators
// MIGRATION:   - objPortalController.UpdatePortalInfo(...) (L772)          -> PortalService.updatePortal(...)
// MIGRATION:   - the legacy create path                                   -> PortalService.createPortal(...)
// MIGRATION:   - cboCurrency "USD" fallback (L324-327)                    -> the currency control defaults to 'USD'
// MIGRATION:   - txtExpiryDate "" -> Null.NullDate (L729-731)             -> a blank expiryDate is normalized to null
// MIGRATION: One class serves both the 'new' (create) and ':id/edit' (edit) routes; the parent route
// MIGRATION: (../../portal.routes.ts) owns lazy-loading via loadComponent and applies authGuard.
import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { CreatePortalRequest, Portal, UpdatePortalRequest } from '../../models';
import { PortalService } from '../../services';
import { ProblemDetails } from '../../../../core/services/api.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/**
 * Strongly-typed reactive form model for the CORE editable subset of a Portal (15 controls).
 * String fields are nullable (`string | null`) to mirror the `Portal` wire shape; numeric fields are
 * non-nullable (`number`) and built with `{ nonNullable: true }` so `getRawValue()` yields `number`
 * (never `null`) for direct, cast-free assignment into the request DTOs.
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
  administratorId: FormControl<number>;
  expiryDate: FormControl<string | null>;
  splashTabId: FormControl<number>;
  homeTabId: FormControl<number>;
  loginTabId: FormControl<number>;
  userTabId: FormControl<number>;
}

/**
 * PortalFormComponent - create/edit screen for the core Portal fields.
 *
 * Reproduces the in-scope create/edit workflow of the legacy DNN SiteSettings editor with UI
 * functional parity (AAP Section 0.3.4 / 0.7.1). It is lazy-loaded by `../../portal.routes.ts` for
 * two routes that share this class:
 *   - `'new'`      -> create mode (heading "New Portal")
 *   - `':id/edit'` -> edit mode  (heading "Edit Portal"); `:id` is read from `ActivatedRoute`.
 *
 * All HTTP flows through `PortalService` (never `HttpClient` directly). Component state is held in
 * signals; the sibling `portal-form.component.html` / `.scss` provide the template and styles.
 */
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

  /** The id of the portal being edited, or `null` in create mode. */
  readonly portalId = signal<number | null>(null);

  /** True when an existing portal id is present (edit mode); drives the heading and read-only chrome. */
  readonly isEditMode = computed<boolean>(() => this.portalId() !== null);

  /** Page heading, mirroring the route titles ("New Portal" / "Edit Portal"). */
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Portal' : 'New Portal'));

  /** True while fetching an existing portal in edit mode (drives `<app-loading-spinner>`). */
  readonly loading = signal<boolean>(false);

  /** True while a create/update request is in flight (used to disable the Save button). */
  readonly saving = signal<boolean>(false);

  /** RFC 7807 field errors (camelCase field name -> messages) surfaced to `<app-form-controls>`. */
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  /** Populated when an existing portal fails to load in edit mode. */
  readonly loadError = signal<string | null>(null);

  /**
   * The full portal loaded in edit mode, retained so the PUT body can be reconstructed with ALL
   * required fields (the form edits only the core 15-field subset). `null` in create mode.
   */
  private loadedPortal: Portal | null = null;

  /**
   * Typed reactive form exposing the core editable subset. `portalName` carries the only client
   * validator (`required`), matching the legacy `RequiredFieldValidator` on `txtPortalName` and the
   * backend `CreatePortalValidator`/`UpdatePortalValidator` (`PortalName` NotEmpty). No stricter client
   * rules are added (Minimal Change Clause / behavioral equivalence). `currency` defaults to 'USD'
   * (legacy cboCurrency fallback, L324-327); numeric controls default to 0.
   */
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
    administratorId: new FormControl<number>(0, { nonNullable: true }),
    expiryDate: new FormControl<string | null>(null),
    splashTabId: new FormControl<number>(0, { nonNullable: true }),
    homeTabId: new FormControl<number>(0, { nonNullable: true }),
    loginTabId: new FormControl<number>(0, { nonNullable: true }),
    userTabId: new FormControl<number>(0, { nonNullable: true }),
  });

  /** Typed control accessor so the template can reference `controls.portalName`, etc. */
  get controls(): PortalFormModel {
    return this.form.controls;
  }

  /**
   * Detect create vs edit mode from the route. In edit mode, load the existing portal and patch the
   * form; in create mode, leave the pristine defaults in place. Reads the route SNAPSHOT because the
   * component is re-created per navigation via `loadComponent`.
   */
  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam === null) {
      return; // create mode: pristine defaults
    }

    const id = Number(idParam);
    this.portalId.set(id);
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

  /**
   * Map a loaded portal onto the 15 editable controls. Nullable numeric fields on the read model
   * (`number | null`) are coalesced to 0 for the non-nullable numeric controls; `currency` falls back
   * to 'USD' (legacy L324-327); `expiryDate` passes through as `string | null`.
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
      administratorId: portal.administratorId ?? 0,
      expiryDate: portal.expiryDate,
      splashTabId: portal.splashTabId ?? 0,
      homeTabId: portal.homeTabId ?? 0,
      loginTabId: portal.loginTabId ?? 0,
      userTabId: portal.userTabId ?? 0,
    });
  }

  /**
   * Submit handler - mirrors `cmdUpdate_Click` gated by `Page.IsValid` (L687-688). An invalid form is
   * blocked and its messages surfaced (parity with the postback validation gate); a valid form branches
   * to update (edit mode) or create (create mode) through `PortalService`.
   */
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched(); // surface validation messages (parity with the Page.IsValid gate)
      return;
    }

    this.saving.set(true);
    this.serverErrors.set(null);

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
   * Build the PUT body. The form edits only the core subset, so the full request is reconstructed by
   * spreading the loaded portal and overriding the edited fields. Two reconciliations against the
   * actual model shapes are required:
   *   - `processorPassword` is write-only (absent from `Portal`), so it is supplied explicitly as null.
   *   - Carried-over fields that are nullable on `Portal` but required (non-null) on the request
   *     (`administratorRoleId`, `registeredRoleId`, `siteLogHistory`, `adminTabId`) are coalesced to 0.
   * The read-only metrics `users`/`pages` carried by the spread are tolerated (extra, ignored by the API).
   */
  private buildUpdateRequest(id: number, loaded: Portal): UpdatePortalRequest {
    const v = this.form.getRawValue();
    return {
      ...loaded,
      // Carried-over fields nullable on Portal but required on UpdatePortalRequest -> coalesce to 0.
      administratorRoleId: loaded.administratorRoleId ?? 0,
      registeredRoleId: loaded.registeredRoleId ?? 0,
      siteLogHistory: loaded.siteLogHistory ?? 0,
      adminTabId: loaded.adminTabId ?? 0,
      // MIGRATION: processorPassword is write-only (never present on Portal) -> send null (unchanged).
      processorPassword: null,
      // MIGRATION: ensure request.portalID === route :id (UpdatePortalValidator: PortalID > 0).
      portalID: id,
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
      // MIGRATION: blank expiry -> null (legacy txtExpiryDate "" -> Null.NullDate, L729-731).
      expiryDate: v.expiryDate || null,
      splashTabId: v.splashTabId,
      homeTabId: v.homeTabId,
      loginTabId: v.loginTabId,
      userTabId: v.userTabId,
    };
  }

  /**
   * Build the POST body. There is no loaded entity in create mode, so the 20 non-edited fields are
   * defaulted explicitly (the server assigns their real values). `CreatePortalRequest` = the 15 edited
   * fields + these 20 defaults = 35 fields total.
   */
  private buildCreateRequest(): CreatePortalRequest {
    const v = this.form.getRawValue();
    return {
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
      // MIGRATION: blank expiry -> null (legacy txtExpiryDate "" -> Null.NullDate, L729-731).
      expiryDate: v.expiryDate || null,
      splashTabId: v.splashTabId,
      homeTabId: v.homeTabId,
      loginTabId: v.loginTabId,
      userTabId: v.userTabId,
      // --- non-edited defaults (the server assigns the real values) ---
      hostFee: 0,
      hostSpace: 0,
      pageQuota: 0,
      userQuota: 0,
      siteLogHistory: 0,
      administratorRoleId: 0,
      administratorRoleName: null,
      registeredRoleId: 0,
      registeredRoleName: null,
      // MIGRATION: guid is a non-null string on the wire; the server assigns the real GUID on create.
      guid: '00000000-0000-0000-0000-000000000000',
      paymentProcessor: null,
      processorPassword: null,
      processorUserId: null,
      email: null,
      adminTabId: 0,
      superTabId: 0,
      defaultLanguage: null,
      timeZoneOffset: 0,
      homeDirectory: null,
      version: null,
    };
  }

  /** Navigate back to the portal list on a successful create/update. */
  private onSaveSuccess(): void {
    this.saving.set(false);
    void this.router.navigate(['/portals']);
  }

  /** Surface RFC 7807 field errors (keyed by camelCase control id) onto the form. */
  private onSaveError(problem: ProblemDetails): void {
    this.saving.set(false);
    this.serverErrors.set(problem.errors ?? null);
  }
}
