// MIGRATION: Replaces the legacy DotNetNuke Web Forms "Edit Roles" admin control
// (Website/admin/Security/EditRoles.ascx.vb). The postback/ViewState code-behind
// (Page_Load + cmdUpdate_Click / cmdDelete_Click / cmdManage_Click / cmdCancel_Click) is
// reinterpreted as a stateless Angular 19 standalone reactive form. Create vs. edit mode is
// derived from the ':id' route param (legacy RoleID = -1 was the create sentinel).
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
  type FormControl,
  type FormGroup,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import type { CreateRole, Role, UpdateRole } from '../../models';
import { RoleService } from '../../services';
import type { ProblemDetails } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { FormControlsComponent } from '../../../../shared/components/form-controls';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/** Static option for the billing/trial frequency dropdowns. */
interface FrequencyOption {
  readonly value: string;
  readonly label: string;
}

/** Option for the role-group dropdown. */
interface RoleGroupOption {
  readonly value: number;
  readonly label: string;
}

/** Strongly-typed reactive form model (all controls non-nullable via NonNullableFormBuilder). */
interface RoleFormModel {
  roleName: FormControl<string>;
  description: FormControl<string>;
  roleGroupID: FormControl<number>;
  serviceFee: FormControl<number>;
  billingPeriod: FormControl<number>;
  billingFrequency: FormControl<string>;
  trialFee: FormControl<number>;
  trialPeriod: FormControl<number>;
  trialFrequency: FormControl<string>;
  isPublic: FormControl<boolean>;
  autoAssignment: FormControl<boolean>;
  rSVPCode: FormControl<string>;
  iconFile: FormControl<string>;
}

/** GlobalRoles sentinel — legacy BindGroups adds a ListItem with value "-1" (EditRoles.ascx.vb L75). */
const GLOBAL_ROLES_GROUP_ID = -1;

/** RoleName length parity with the DNN Roles.RoleName column (nvarchar(50)). */
const ROLE_NAME_MAX_LENGTH = 50;

/** RSVPCode length parity with the DNN Roles.RSVPCode column (nvarchar(50)); mirrors the
 *  backend FluentValidation RuleFor(x => x.RSVPCode).MaximumLength(50). */
const RSVP_CODE_MAX_LENGTH = 50;

const SYSTEM_ROLE_ADMINISTRATORS = 'Administrators';
const SYSTEM_ROLE_REGISTERED = 'Registered Users';

// MIGRATION: legacy cboBillingFrequency / cboTrialFrequency were data-bound from the DNN
// "Frequency" list via ListController.GetListEntryInfoCollection("Frequency","")
// (EditRoles.ascx.vb L116-125), defaulting the selection to "N". No list endpoint is in
// migration scope, so the known DNN frequency codes are hardcoded here (documented in
// MIGRATION_NOTES.md).
const FREQUENCY_OPTIONS: readonly FrequencyOption[] = [
  { value: 'N', label: 'None' },
  { value: 'O', label: 'One Time' },
  { value: 'D', label: 'Day' },
  { value: 'W', label: 'Week' },
  { value: 'M', label: 'Month' },
  { value: 'Y', label: 'Year' },
];

@Component({
  selector: 'app-role-form',
  templateUrl: './role-form.component.html',
  styleUrl: './role-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormControlsComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
})
export class RoleFormComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roleService = inject(RoleService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** Route ':id' parsed to a number, or null in create mode (legacy RoleID = -1). */
  readonly roleId = signal<number | null>(null);
  readonly isEditMode = computed<boolean>(() => this.roleId() !== null);
  readonly heading = computed<string>(() => (this.isEditMode() ? 'Edit Role' : 'Add Role'));

  readonly loadingRole = signal<boolean>(false);
  readonly submitting = signal<boolean>(false);
  readonly loadError = signal<string | null>(null);
  readonly serverErrors = signal<Record<string, string[]> | null>(null);

  /** Billing/trial conditional reveal (driven by the frequency master toggles). */
  readonly billingEnabled = signal<boolean>(false);
  readonly trialEnabled = signal<boolean>(false);

  /** System-role guard flags (best-effort by role name — see applySystemRoleGuard). */
  readonly isSystemRole = signal<boolean>(false);
  readonly isRegisteredRole = signal<boolean>(false);

  /** Delete confirmation dialog visibility (edit mode only). */
  readonly showDeleteDialog = signal<boolean>(false);

  readonly frequencyOptions = FREQUENCY_OPTIONS;
  readonly roleGroupOptions = signal<readonly RoleGroupOption[]>([
    { value: GLOBAL_ROLES_GROUP_ID, label: 'Global Roles' },
  ]);

  /** Loaded role retained in edit mode for full UpdateRole reconstruction. */
  private loadedRole: Role | null = null;

  readonly form: FormGroup<RoleFormModel> = this.fb.group({
    roleName: this.fb.control('', {
      validators: [Validators.required, Validators.maxLength(ROLE_NAME_MAX_LENGTH)],
    }),
    description: this.fb.control(''),
    roleGroupID: this.fb.control(GLOBAL_ROLES_GROUP_ID),
    serviceFee: this.fb.control(0),
    billingPeriod: this.fb.control(1),
    billingFrequency: this.fb.control('N'),
    trialFee: this.fb.control(0),
    trialPeriod: this.fb.control(1),
    trialFrequency: this.fb.control('N'),
    isPublic: this.fb.control(false),
    autoAssignment: this.fb.control(false),
    // MIGRATION (F2-RSVP-001): mirror the backend RSVPCode MaximumLength(50) rule client-side
    // (defense-in-depth, same pattern as roleName above) so over-length input is caught before
    // submit; the server-side error (key "rsvpCode") still renders inline via app-form-controls.
    rSVPCode: this.fb.control('', {
      validators: [Validators.maxLength(RSVP_CODE_MAX_LENGTH)],
    }),
    iconFile: this.fb.control(''),
  });

  get controls(): RoleFormModel {
    return this.form.controls;
  }

  ngOnInit(): void {
    // Reproduce the legacy conditional billing/trial reveal reactively.
    this.applyConditionalState();
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.applyConditionalState());

    // MIGRATION: legacy RoleID came from the QueryString (default -1 = create); here the mode
    // is derived from the presence of the ':id' route param (param name "id").
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam === null) {
      return;
    }
    const id = Number(idParam);
    this.roleId.set(id);
    this.loadRole(id);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.serverErrors.set(null);
    this.submitting.set(true);

    const id = this.roleId();
    if (id !== null && this.loadedRole !== null) {
      this.roleService.updateRole(id, this.buildUpdateRole(id, this.loadedRole)).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    } else {
      this.roleService.createRole(this.buildCreateRole()).subscribe({
        next: () => this.onSaveSuccess(),
        error: (problem: ProblemDetails) => this.onSaveError(problem),
      });
    }
  }

  requestDelete(): void {
    this.showDeleteDialog.set(true);
  }

  cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }

  confirmDelete(): void {
    const id = this.roleId();
    if (id === null) {
      return;
    }
    this.showDeleteDialog.set(false);
    this.submitting.set(true);
    // MIGRATION: cmdDelete_Click -> RoleController.DeleteRole (EditRoles.ascx.vb L287-303).
    // Role delete is a HARD delete + cascade per the RoleService contract.
    this.roleService.deleteRole(id).subscribe({
      next: () => this.onSaveSuccess(),
      error: (problem: ProblemDetails) => this.onSaveError(problem),
    });
  }

  manageUsers(): void {
    const id = this.roleId();
    if (id === null) {
      return;
    }
    // MIGRATION: cmdManage_Click navigated to "User Roles" for the RoleId (EditRoles.ascx.vb L338).
    void this.router.navigate(['/roles', id, 'assignments']);
  }

  cancel(): void {
    // MIGRATION: cmdCancel_Click redirected back to the role list (EditRoles.ascx.vb L316-323).
    void this.router.navigate(['/roles']);
  }

  private loadRole(id: number): void {
    this.loadingRole.set(true);
    this.roleService.getRole(id).subscribe({
      next: (role) => {
        this.loadedRole = role;
        this.patchForm(role);
        this.applySystemRoleGuard(role.roleName);
        this.loadingRole.set(false);
      },
      error: (problem: ProblemDetails) => {
        this.loadError.set(problem.detail ?? problem.title ?? 'Failed to load the role.');
        this.loadingRole.set(false);
      },
    });
  }

  private patchForm(role: Role): void {
    // MIGRATION: legacy Page_Load only populated billing fields when ServiceFee <> 0.00 and trial
    // fields when TrialFrequency <> "N" (EditRoles.ascx.vb L146-161); the valueChanges subscription
    // re-derives the billing/trial reveal after patching.
    // MIGRATION: the Role wire contract (role.model.ts) types serviceFee/billingPeriod/trialFee/
    // trialPeriod as nullable (`number | null`); null-coalesce them to the same defaults the form
    // controls were created with (fees -> 0, periods -> 1) so the non-nullable typed controls are
    // patched safely with no behavioral change. `role.rsvpCode` is the System.Text.Json wire name
    // (the legacy RSVPCode property); it feeds the internal `rSVPCode` control.
    this.form.patchValue({
      roleName: role.roleName ?? '',
      description: role.description ?? '',
      roleGroupID: role.roleGroupID ?? GLOBAL_ROLES_GROUP_ID,
      serviceFee: role.serviceFee ?? 0,
      billingPeriod: role.billingPeriod ?? 1,
      billingFrequency: role.billingFrequency ?? 'N',
      trialFee: role.trialFee ?? 0,
      trialPeriod: role.trialPeriod ?? 1,
      trialFrequency: role.trialFrequency ?? 'N',
      isPublic: role.isPublic,
      autoAssignment: role.autoAssignment,
      rSVPCode: role.rsvpCode ?? '',
      iconFile: role.iconFile ?? '',
    });
    this.ensureRoleGroupOption(role.roleGroupID);
  }

  private ensureRoleGroupOption(roleGroupId: number | null): void {
    // MIGRATION: legacy BindGroups loaded groups via RoleController.GetRoleGroups(PortalId)
    // (EditRoles.ascx.vb L71-81). No role-groups endpoint is in scope, so the dropdown offers
    // GlobalRoles (-1) plus the loaded role's own group id (name unavailable -> "Group {id}").
    // Documented in MIGRATION_NOTES.md.
    if (roleGroupId === null || roleGroupId === GLOBAL_ROLES_GROUP_ID) {
      return;
    }
    if (!this.roleGroupOptions().some((option) => option.value === roleGroupId)) {
      this.roleGroupOptions.update((options) => [
        ...options,
        { value: roleGroupId, label: `Group ${roleGroupId}` },
      ]);
    }
  }

  private applySystemRoleGuard(roleName: string | null): void {
    // MIGRATION: legacy disabled the Administrators/Registered roles by comparing RoleID to
    // PortalSettings.AdministratorRoleId / RegisteredRoleId (EditRoles.ascx.vb L174-182). The SPA
    // never receives those ids, so detection is best-effort by role name; documented in
    // MIGRATION_NOTES.md.
    const administrators = roleName === SYSTEM_ROLE_ADMINISTRATORS;
    const registered = roleName === SYSTEM_ROLE_REGISTERED;
    this.isSystemRole.set(administrators || registered);
    this.isRegisteredRole.set(registered);
    if (administrators || registered) {
      this.form.disable({ emitEvent: false });
    }
  }

  private applyConditionalState(): void {
    if (this.isSystemRole()) {
      return;
    }
    const value = this.form.getRawValue();
    // MIGRATION: billing is meaningful only when a billing frequency other than "N" is chosen
    // (legacy submit gate cboBillingFrequency.Value <> "N", EditRoles.ascx.vb L216); trial only
    // when a trial frequency other than "N" is chosen (L226). The frequency dropdowns act as the
    // master toggles so the dependent fee/period inputs can be revealed and enabled.
    const billing = value.billingFrequency !== 'N';
    const trial = value.trialFrequency !== 'N';
    this.billingEnabled.set(billing);
    this.trialEnabled.set(trial);
    this.toggleControl(this.controls.serviceFee, billing);
    this.toggleControl(this.controls.billingPeriod, billing);
    this.toggleControl(this.controls.trialFee, trial);
    this.toggleControl(this.controls.trialPeriod, trial);
  }

  private toggleControl(control: FormControl<number>, enabled: boolean): void {
    if (enabled && control.disabled) {
      control.enable({ emitEvent: false });
    } else if (!enabled && control.enabled) {
      control.disable({ emitEvent: false });
    }
  }

  private resolveFeeFields(): Pick<
    Role,
    'serviceFee' | 'billingPeriod' | 'billingFrequency' | 'trialFee' | 'trialPeriod' | 'trialFrequency'
  > {
    const v = this.form.getRawValue();
    // MIGRATION: cmdUpdate_Click defaulting (EditRoles.ascx.vb L212-230). Billing values are used
    // only when the billing frequency is not "N"; trial values only when the (computed) service
    // fee is non-zero AND the trial frequency is not "N".
    let serviceFee = 0;
    let billingPeriod = 1;
    let billingFrequency = 'N';
    if (v.billingFrequency !== 'N') {
      serviceFee = v.serviceFee;
      billingPeriod = v.billingPeriod;
      billingFrequency = v.billingFrequency;
    }
    let trialFee = 0;
    let trialPeriod = 1;
    let trialFrequency = 'N';
    if (serviceFee !== 0 && v.trialFrequency !== 'N') {
      trialFee = v.trialFee;
      trialPeriod = v.trialPeriod;
      trialFrequency = v.trialFrequency;
    }
    return { serviceFee, billingPeriod, billingFrequency, trialFee, trialPeriod, trialFrequency };
  }

  private buildCreateRole(): CreateRole {
    const v = this.form.getRawValue();
    const fees = this.resolveFeeFields();
    // MIGRATION: legacy set objRoleInfo.PortalID = PortalId (PortalSettings.PortalId); the SPA
    // sources the current portal from the authenticated user. The User wire contract
    // (core/models/user.model.ts) exposes the portal id as `portalID` (System.Text.Json CamelCase
    // of UserDto.PortalID), so read currentUser()?.portalID.
    const portalID = this.auth.currentUser()?.portalID ?? 0;
    return {
      portalID,
      roleGroupID: v.roleGroupID,
      roleName: v.roleName,
      description: v.description,
      serviceFee: fees.serviceFee,
      billingFrequency: fees.billingFrequency,
      trialPeriod: fees.trialPeriod,
      trialFrequency: fees.trialFrequency,
      billingPeriod: fees.billingPeriod,
      trialFee: fees.trialFee,
      isPublic: v.isPublic,
      autoAssignment: v.autoAssignment,
      rsvpCode: v.rSVPCode,
      iconFile: v.iconFile,
    };
  }

  private buildUpdateRole(id: number, loaded: Role): UpdateRole {
    const v = this.form.getRawValue();
    const fees = this.resolveFeeFields();
    // MIGRATION: spread the loaded role then override edited fields; force roleID === route id
    // (RoleService.updateRole contract requires dto.roleID === id). `rsvpCode` is the wire name of
    // the internal `rSVPCode` control.
    return {
      ...loaded,
      roleID: id,
      portalID: loaded.portalID,
      roleGroupID: v.roleGroupID,
      roleName: v.roleName,
      description: v.description,
      serviceFee: fees.serviceFee,
      billingFrequency: fees.billingFrequency,
      trialPeriod: fees.trialPeriod,
      trialFrequency: fees.trialFrequency,
      billingPeriod: fees.billingPeriod,
      trialFee: fees.trialFee,
      isPublic: v.isPublic,
      autoAssignment: v.autoAssignment,
      rsvpCode: v.rSVPCode,
      iconFile: v.iconFile,
    };
  }

  private onSaveSuccess(): void {
    this.submitting.set(false);
    void this.router.navigate(['/roles']);
  }

  private onSaveError(problem: ProblemDetails): void {
    this.submitting.set(false);
    // MIGRATION: legacy showed a "DuplicateRole" RedError on duplicate name (EditRoles.ascx.vb
    // L256). The API returns RFC 7807 field errors; a duplicate-name 400/409 surfaces under the
    // "roleName" key and is rendered inline by <app-form-controls>.
    this.serverErrors.set(problem.errors ?? null);
  }
}
