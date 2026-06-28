// MIGRATION: Angular 19 standalone replacement for the legacy DNN "Admin -> Security -> Edit Roles"
// editor (Website/admin/Security/EditRoles.ascx.vb, 363 lines, class EditRoles : PortalModuleBase).
// MIGRATION: Only the validation + orchestration logic is re-expressed here. ALL Web Forms postback /
// ViewState / PortalModuleBase / .resx localization / DataCache / EventLog machinery is discarded
// (those are backend concerns now). This single typed Reactive Form serves BOTH create ('roles/new')
// and edit ('roles/:id/edit') modes, distinguished by the route-bound `id` input (legacy RoleID
// querystring, EditRoles.ascx.vb L42/L100; default -1 == create).
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
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { RoleService, type CreateRoleRequest } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { Role, ProblemDetails } from '../../../core/models';
// MIGRATION: [QA F4-003] shared focus-first-invalid helper; [QA F4-013] shared error normaliser (maps the
// status-0 transport case to a friendly message instead of the previous null/blind-cast).
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';
import { toProblemDetails as normalizeProblemDetails } from '../../../core/services/problem-details.util';

// MIGRATION: legacy system roles guarded by EditRoles.ascx.vb (L174-178) via RoleID ==
// PortalSettings.AdministratorRoleId / RegisteredRoleId. The Role model carries no IsSystemRole flag,
// so detection uses the canonical DNN role names (PortalController.vb L1390/L1393).
const SYSTEM_ROLE_NAMES: readonly string[] = ['Administrators', 'Registered Users'];

// MIGRATION: the "Registered Users" role additionally hid the "Manage Users" action (cmdManage,
// EditRoles.ascx.vb L180-182).
const REGISTERED_USERS_ROLE = 'Registered Users';

// MIGRATION: typed form model mapped to the RoleInfo fields persisted by cmdUpdate_Click ->
// RoleController.AddRole / UpdateRole (EditRoles.ascx.vb L234-248). roleId/portalId are NOT form fields
// (roleId comes from the route on update; portalId is resolved from the loaded role or the current user).
interface RoleFormModel {
  roleName: FormControl<string>;
  description: FormControl<string>;
  roleGroupId: FormControl<number>;
  serviceFee: FormControl<number>;
  billingPeriod: FormControl<number>;
  billingFrequency: FormControl<string>;
  trialFee: FormControl<number>;
  trialPeriod: FormControl<number>;
  trialFrequency: FormControl<string>;
  isPublic: FormControl<boolean>;
  autoAssignment: FormControl<boolean>;
  rsvpCode: FormControl<string>;
  iconFile: FormControl<string>;
}

@Component({
  selector: 'app-role-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormControlComponent, LoadingSpinnerComponent],
  templateUrl: './role-form.component.html',
  styleUrl: './role-form.component.scss',
})
export class RoleFormComponent {
  private readonly roleService = inject(RoleService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  // MIGRATION: [QA F4-003] host element used to locate the first invalid control on a failed submit.
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  // MIGRATION: route input binding (withComponentInputBinding() is enabled app-wide). Undefined on the
  // 'roles/new' (create) route; the :id param on the 'roles/:id/edit' (edit) route.
  readonly id = input<string>();
  readonly isEditMode = computed(() => this.id() != null);

  readonly loading = signal(false);
  readonly submitting = signal(false);
  readonly problem = signal<ProblemDetails | null>(null);

  // MIGRATION: the role being edited (RoleController.GetRole, EditRoles.ascx.vb L136). Drives the
  // system-role guard and the portalId used on submit (objRoleInfo.PortalID = PortalId, L234).
  private readonly loadedRole = signal<Role | null>(null);

  // MIGRATION: SYSTEM-ROLE GUARD (EditRoles.ascx.vb L174-178). Editing Administrators / Registered Users
  // hid Update/Delete and called ActivateControls(False). Detected here by the loaded role's name.
  readonly isSystemRole = computed(() => {
    const role = this.loadedRole();
    return role != null && SYSTEM_ROLE_NAMES.includes(role.roleName);
  });

  // MIGRATION: cmdManage "Manage Users" was edit-mode only and hidden for Registered Users (L180-188).
  readonly canManageUsers = computed(() => {
    const role = this.loadedRole();
    return this.isEditMode() && role != null && role.roleName !== REGISTERED_USERS_ROLE;
  });

  // MIGRATION: surface a flat string[] ProblemDetails.errors payload (Result.Errors shape) as a
  // form-level summary; <app-form-control> renders only the per-field Record<string,string[]> shape.
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

  // MIGRATION: the editable controls disabled by ActivateControls(False) (EditRoles.ascx.vb L47-59) when
  // editing a system role. roleName is handled separately (always read-only in edit mode, L132-134).
  private readonly systemRoleLockedKeys: readonly (keyof RoleFormModel)[] = [
    'description',
    'roleGroupId',
    'isPublic',
    'autoAssignment',
    'serviceFee',
    'billingPeriod',
    'billingFrequency',
    'trialFee',
    'trialPeriod',
    'trialFrequency',
    'rsvpCode',
  ];

  // MIGRATION: typed Reactive Form. Defaults mirror the legacy: billingFrequency/trialFrequency 'N'
  // (the DNN "Frequency" None code, EditRoles.ascx.vb L121/L125); roleGroupId -1 (GlobalRoles,
  // BindGroups L75); fees 0 and periods 1. roleName is required (legacy valRoleName, L134).
  readonly form = new FormGroup<RoleFormModel>({
    roleName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true }),
    roleGroupId: new FormControl(-1, { nonNullable: true }),
    serviceFee: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    billingPeriod: new FormControl(1, { nonNullable: true }),
    billingFrequency: new FormControl('N', { nonNullable: true }),
    trialFee: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
    trialPeriod: new FormControl(1, { nonNullable: true }),
    trialFrequency: new FormControl('N', { nonNullable: true }),
    isPublic: new FormControl(false, { nonNullable: true }),
    autoAssignment: new FormControl(false, { nonNullable: true }),
    rsvpCode: new FormControl('', { nonNullable: true }),
    iconFile: new FormControl('', { nonNullable: true }),
  });

  // MIGRATION: the legacy form parsed/populated billing & trial only when a (non-'N') frequency was
  // chosen (cmdUpdate_Click L216/L226; Page_Load L146/L154). The billingFrequency dropdown is the
  // explicit switch, so the Trial section is shown once a billing frequency is selected (a paid role).
  private readonly billingFrequencyValue = toSignal(this.form.controls.billingFrequency.valueChanges, {
    initialValue: this.form.controls.billingFrequency.value,
  });
  readonly showTrialSection = computed(() => this.billingFrequencyValue() !== 'N');

  // MIGRATION: the RSVP invitation link the legacy control built when RSVPCode was non-empty
  // (AddHTTP(GetDomainName) & "/" & glbDefaultPage & "?rsvp=" & RSVPCode, EditRoles.ascx.vb L166-168).
  // Re-expressed as the SPA origin + rsvp query param; encodeURIComponent + [href] sanitization keep it
  // XSS-safe. Shown only when an rsvpCode is present.
  private readonly rsvpCodeValue = toSignal(this.form.controls.rsvpCode.valueChanges, {
    initialValue: this.form.controls.rsvpCode.value,
  });
  readonly rsvpLink = computed(() => {
    const code = (this.rsvpCodeValue() ?? '').trim();
    return code.length > 0 ? `${window.location.origin}/?rsvp=${encodeURIComponent(code)}` : '';
  });

  constructor() {
    // MIGRATION: EditRoles Page_Load -> GetRole(RoleID, PortalId) population (L131-169). When the route
    // supplies an id (edit mode), load the role and patch the form; the 'roles/new' route keeps defaults.
    effect(() => {
      const roleId = this.id();
      if (roleId != null) {
        this.loadRole(roleId);
      }
    });
  }

  private loadRole(id: string): void {
    this.loading.set(true);
    // MIGRATION: GET /roles/{id} requires the tenant `portalId` query (AAP Section 0.7.1). On the LOAD
    // path it is sourced from the authenticated principal ONLY -- it must NOT read loadedRole() (which
    // loadRole writes) because loadRole runs inside the id() effect, and a read-write of the same signal
    // would create an infinite effect loop.
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    this.roleService.getById(Number(id), portalId).subscribe({
      next: (role) => {
        this.loadedRole.set(role);
        this.patchForm(role);
        // MIGRATION: ActivateControls(False) for system roles (EditRoles.ascx.vb L174-178).
        if (this.isSystemRole()) {
          this.disableSystemRoleControls();
        }
        this.loading.set(false);
      },
      error: (err: unknown) => {
        // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #21] previously this REDIRECTED to /roles on a failed
        // load (legacy EditRoles.ascx.vb L170-172), which lost the error context and was inconsistent with
        // the user-edit / profile gold-standard (those stay on the page and render a banner). It now STAYS on
        // the page and surfaces the RFC 7807 problem through the existing errorSummary banner (role="alert"),
        // standardizing the load-failure UX across all edit/detail forms. The legacy redirect is documented
        // here but not propagated.
        this.problem.set(this.toProblemDetails(err));
        this.loading.set(false);
      },
    });
  }

  // MIGRATION: EditRoles Page_Load field population (L138-169). roleName is patched too: the legacy
  // showed it read-only via lblRoleName (L132/L139) while txtRoleName stayed hidden/empty, so
  // cmdUpdate set RoleName = txtRoleName.Text = "" (a latent empty-name bug, L237). We PRESERVE the
  // intent (the name is immutable in edit mode, shown read-only) and carry the real name in the DTO;
  // the legacy bug is documented, not propagated. Billing is patched only when a service fee exists
  // (Format(ServiceFee,'#,##0.00')<>'0.00', L146) and trial only when TrialFrequency<>'N' (L154); the
  // defaults already hold otherwise.
  private patchForm(role: Role): void {
    this.form.patchValue({
      roleName: role.roleName,
      description: role.description ?? '',
      roleGroupId: role.roleGroupId ?? -1,
      isPublic: role.isPublic,
      autoAssignment: role.autoAssignment,
      rsvpCode: role.rsvpCode ?? '',
      iconFile: role.iconFile ?? '',
    });

    if (role.serviceFee !== 0) {
      this.form.patchValue({
        serviceFee: role.serviceFee,
        billingPeriod: role.billingPeriod,
        billingFrequency: role.billingFrequency ?? 'N',
      });
    }

    if ((role.trialFrequency ?? 'N') !== 'N') {
      this.form.patchValue({
        trialFee: role.trialFee,
        trialPeriod: role.trialPeriod,
        trialFrequency: role.trialFrequency ?? 'N',
      });
    }
  }

  // MIGRATION: ActivateControls(False) (EditRoles.ascx.vb L47-59, invoked at L177) disabled every
  // editable control for a system role.
  private disableSystemRoleControls(): void {
    for (const key of this.systemRoleLockedKeys) {
      this.form.controls[key].disable({ emitEvent: false });
    }
  }

  // MIGRATION: cmdUpdate_Click (EditRoles.ascx.vb L208-269). Legacy guarded on Page.IsValid, applied the
  // fee/trial default-and-parse rules (L212-230), built a RoleInfo (L233-248), then called AddRole
  // (create) or UpdateRole (edit). The duplicate-name check (GetRoleByName -> "DuplicateRole", L252-258)
  // and the ROLE_CREATED/ROLE_UPDATED EventLog writes + DataCache.RemoveCache("GetRoles") (L250-265) are
  // BACKEND concerns now; duplicate errors surface via the RFC 7807 errors mapped onto roleName.
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // MIGRATION: [QA F4-003] move focus + scroll to the first invalid control so an invalid submit gives
      // immediate, visible feedback (the shared <app-form-control> renders the per-field message, F4-002).
      focusFirstInvalidControl(this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.problem.set(null);

    const request = this.buildRequest();

    // MIGRATION: PUT /roles/{id} requires the tenant `portalId` query (AAP Section 0.7.1). In a button
    // handler (outside the load effect) it is safe to prefer the loaded role's portalId, falling back to
    // the authenticated principal's portal. Mirrors objRoleInfo.PortalID = PortalId (EditRoles.ascx.vb
    // L234). Create carries portalId in the body (CreateRoleRequest.portalId), so no query is needed.
    const portalId = this.loadedRole()?.portalId ?? this.auth.currentUser()?.portalId ?? 0;
    const request$ = this.isEditMode()
      ? this.roleService.update(Number(this.id()), portalId, request)
      : this.roleService.create(request);

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        // MIGRATION: legacy redirected to the roles list (NavigateURL(), L267).
        void this.router.navigate(['/roles']);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.problem.set(this.toProblemDetails(err));
      },
    });
  }

  // MIGRATION: cmdManage_Click (EditRoles.ascx.vb L336-342) navigated to the "User Roles" page with the
  // RoleId. Re-expressed as navigation to the role-assignment route.
  manageUsers(): void {
    void this.router.navigate(['/roles', this.id(), 'assignments']);
  }

  // MIGRATION: cmdCancel_Click (EditRoles.ascx.vb L316-323) redirected to the roles list (NavigateURL()).
  cancel(): void {
    void this.router.navigate(['/roles']);
  }

  // MIGRATION: cmdUpdate_Click fee/trial default-and-parse (EditRoles.ascx.vb L212-248) + RoleInfo build.
  // Billing defaults serviceFee=0 / billingPeriod=1 / billingFrequency='N', overridden only when a
  // (non-'N') billing frequency is chosen (L216). Trial defaults trialFee=0 / trialPeriod=1 /
  // trialFrequency='N', overridden only when the (defaulted) serviceFee is non-zero AND a (non-'N')
  // trial frequency is chosen (sglServiceFee<>0 ... cboTrialFrequency<>'N', L226 — the trial fee may be
  // 0). portalId mirrors objRoleInfo.PortalID = PortalId (L234), resolved from the loaded role or the
  // current user's portal.
  private buildRequest(): CreateRoleRequest {
    const raw = this.form.getRawValue();

    let serviceFee = 0;
    let billingPeriod = 1;
    let billingFrequency = 'N';
    if (raw.billingFrequency !== 'N') {
      serviceFee = raw.serviceFee;
      billingPeriod = raw.billingPeriod;
      billingFrequency = raw.billingFrequency;
    }

    let trialFee = 0;
    let trialPeriod = 1;
    let trialFrequency = 'N';
    if (serviceFee !== 0 && raw.trialFrequency !== 'N') {
      trialFee = raw.trialFee;
      trialPeriod = raw.trialPeriod;
      trialFrequency = raw.trialFrequency;
    }

    const portalId = this.loadedRole()?.portalId ?? this.auth.currentUser()?.portalId ?? 0;

    return {
      portalId,
      roleGroupId: raw.roleGroupId,
      roleName: raw.roleName,
      description: raw.description,
      serviceFee,
      billingFrequency,
      billingPeriod,
      trialFee,
      trialPeriod,
      trialFrequency,
      isPublic: raw.isPublic,
      autoAssignment: raw.autoAssignment,
      rsvpCode: raw.rsvpCode,
      iconFile: raw.iconFile,
    };
  }

  // MIGRATION: [QA F4-013] delegate to the shared normaliser so a status-0 transport failure surfaces the
  // friendly "Unable to reach the server." envelope (the previous blind cast returned a ProgressEvent-shaped
  // object with no title/detail, producing a silent failure) while structured RFC 7807 errors are preserved.
  private toProblemDetails(err: unknown): ProblemDetails {
    return normalizeProblemDetails(err);
  }
}
