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
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import {
  CreateRoleRequest,
  ProblemDetails,
  Role,
  UpdateRoleRequest,
} from '../../../core/models';
import { RoleService } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
  FormFieldComponent,
  FormFieldOption,
} from '../../../shared/components/form-controls';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';

// MIGRATION: reproduces the legacy asp:CompareValidator Operator="GreaterThan"
// ValueToCompare="0" (editroles.ascx valBillingPeriod2 / valTrialPeriod2). Like
// ASP.NET CompareValidator, blank input is skipped (not an error); only a present
// value that is not strictly greater than `min` fails.
function greaterThan(min: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    if (value === null || value === undefined || value === '') {
      return null;
    }
    const numeric = Number(value);
    if (Number.isNaN(numeric)) {
      return null;
    }
    return numeric > min ? null : { greaterThan: { min } };
  };
}

@Component({
  selector: 'app-role-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormFieldComponent,
    LoadingSpinnerComponent,
    ConfirmationDialogComponent,
  ],
  template: `
    <section class="role-form">
      <header class="role-form__header">
        <h1 class="role-form__title">{{ isEdit ? 'Edit Role' : 'Add Role' }}</h1>
      </header>

      <app-loading-spinner [loading]="loading()" />

      @if (error(); as message) {
        <p class="role-form__banner" role="alert">{{ message }}</p>
      }

      @if (!loading()) {
        <form class="role-form__form" [formGroup]="form" (ngSubmit)="save()" novalidate>
          <app-form-field
            [control]="form.controls.roleName"
            label="Role Name"
            controlType="text"
            [required]="true"
            [autofocus]="true"
            [errorMessages]="roleNameErrors"
          />

          <app-form-field
            [control]="form.controls.description"
            label="Description"
            controlType="textarea"
          />

          <app-form-field
            [control]="form.controls.roleGroupID"
            label="Role Group"
            controlType="select"
            [options]="roleGroupOptions"
          />

          <div class="role-form__checkbox">
            <label class="role-form__checkbox-label">
              <input type="checkbox" [formControl]="form.controls.isPublic" />
              <span>Public Role?</span>
            </label>
          </div>

          <div class="role-form__checkbox">
            <label class="role-form__checkbox-label">
              <input type="checkbox" [formControl]="form.controls.autoAssignment" />
              <span>Auto Assignment?</span>
            </label>
          </div>

          <app-form-field
            [control]="form.controls.serviceFee"
            label="Service Fee"
            controlType="number"
            step="any"
            [errorMessages]="serviceFeeErrors"
          />

          <app-form-field
            [control]="form.controls.billingPeriod"
            label="Billing Period"
            controlType="number"
            [errorMessages]="billingPeriodErrors"
          />

          <app-form-field
            [control]="form.controls.billingFrequency"
            label="Billing Frequency"
            controlType="select"
            [options]="frequencyOptions"
          />

          <app-form-field
            [control]="form.controls.trialFee"
            label="Trial Fee"
            controlType="number"
            step="any"
            [errorMessages]="trialFeeErrors"
          />

          <app-form-field
            [control]="form.controls.trialPeriod"
            label="Trial Period"
            controlType="number"
            [errorMessages]="trialPeriodErrors"
          />

          <app-form-field
            [control]="form.controls.trialFrequency"
            label="Trial Frequency"
            controlType="select"
            [options]="frequencyOptions"
          />

          <app-form-field
            [control]="form.controls.rsvpCode"
            label="RSVP Code"
            controlType="text"
          />

          <!-- MIGRATION: legacy read-only txtRSVPLink (a generated
               "?rsvp=<code>" URL built from txtRSVPCode) is a display-only
               convenience with no backend field; intentionally omitted. -->

          <!-- MIGRATION: legacy ctlIcon (dnn:Url picker) simplified to a URL/text field. -->
          <app-form-field
            [control]="form.controls.iconFile"
            label="Icon"
            controlType="text"
            hint="Enter an icon URL."
          />

          <div class="role-form__actions">
            <button
              type="submit"
              class="role-form__btn role-form__btn--primary"
              [disabled]="saving()"
            >
              {{ isEdit ? 'Update' : 'Create' }}
            </button>
            <button type="button" class="role-form__btn" (click)="cancel()">Cancel</button>
            @if (isEdit) {
              <button
                type="button"
                class="role-form__btn role-form__btn--danger"
                (click)="requestDelete()"
              >
                Delete
              </button>
            }
          </div>
          <!-- MIGRATION: legacy cmdManage "Manage Users" (SecurityRoles.ascx role↔user
               assignment) is OUT OF SCOPE — no endpoint surfaced; intentionally omitted. -->
        </form>
      }

      @if (isEdit) {
        <app-confirmation-dialog
          [(open)]="showDeleteDialog"
          title="Delete Role"
          message="Are you sure you want to delete this role?"
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
      .role-form {
        max-width: 640px;
        margin: 0 auto;
        padding: var(--space-4);
      }
      .role-form__title {
        margin: 0 0 var(--space-3);
        font-size: 1.5rem;
      }
      .role-form__banner {
        margin: 0 0 var(--space-3);
        padding: var(--space-2) var(--space-3);
        color: var(--color-danger);
        background: var(--color-danger-bg);
        border: 1px solid var(--color-danger);
        border-radius: var(--radius);
      }
      .role-form__checkbox {
        margin-bottom: var(--space-3);
      }
      .role-form__checkbox-label {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2);
        font-weight: 600;
      }
      .role-form__actions {
        display: flex;
        gap: var(--space-2);
        margin-top: var(--space-4);
      }
      .role-form__btn {
        padding: var(--space-2) var(--space-4);
        font: inherit;
        cursor: pointer;
        color: var(--color-text);
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: var(--radius);
      }
      .role-form__btn--primary {
        color: var(--color-primary-contrast);
        background: var(--color-primary);
        border-color: var(--color-primary);
      }
      .role-form__btn--danger {
        color: var(--color-danger-contrast);
        background: var(--color-danger);
        border-color: var(--color-danger);
      }
      .role-form__btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      /* QA F-J: hover states (buttons previously had only :disabled). Base is
         the neutral Cancel button; primary/danger overrides follow it.
         QA INFO: subtle :active press feedback. */
      .role-form__btn:hover:not(:disabled) {
        background: var(--color-surface-hover);
      }
      .role-form__btn--primary:hover:not(:disabled) {
        background: var(--color-primary-hover);
        border-color: var(--color-primary-hover);
      }
      .role-form__btn--danger:hover:not(:disabled) {
        background: var(--color-danger-hover);
        border-color: var(--color-danger-hover);
      }
      .role-form__btn:active:not(:disabled) {
        transform: translateY(1px);
      }
    `,
  ],
})
export class RoleFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly roleService = inject(RoleService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  // MIGRATION: legacy RoleID came from Request.QueryString("RoleID") (-1 = add mode).
  // Here the route param drives create vs edit: /roles/new (create) or /roles/:id (edit).
  private readonly idParam = this.route.snapshot.paramMap.get('id');
  readonly roleId: number | null =
    this.idParam !== null && /^\d+$/.test(this.idParam) ? Number(this.idParam) : null;
  readonly isEdit = this.roleId !== null;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly showDeleteDialog = signal(false);

  // MIGRATION: nonNullable typed reactive form replaces the ViewState-backed server
  // controls in editroles.ascx. Validators mirror the legacy asp:*Validator controls;
  // period fields default to 1 and fees to 0 so the form is valid on initial load.
  readonly form = this.fb.nonNullable.group({
    roleName: ['', [Validators.required, Validators.maxLength(50)]],
    description: ['', [Validators.maxLength(1000)]],
    roleGroupID: [-1],
    isPublic: [false],
    autoAssignment: [false],
    serviceFee: [0, [Validators.min(0)]],
    billingPeriod: [1, [greaterThan(0)]],
    billingFrequency: ['N'],
    trialFee: [0, [Validators.min(0)]],
    trialPeriod: [1, [greaterThan(0)]],
    trialFrequency: ['N'],
    rsvpCode: ['', [Validators.maxLength(50)]],
    iconFile: [''],
  });

  // MIGRATION: legacy cboRoleGroups was seeded with GlobalRoles(-1) then
  // RoleController.GetRoleGroups(PortalId). No /api/rolegroups endpoint exists, so
  // only the Global Roles(-1) option is offered.
  protected readonly roleGroupOptions: readonly FormFieldOption[] = [
    { value: -1, label: 'Global Roles' },
  ];

  // MIGRATION: legacy billing/trial frequency dropdowns were bound from the DNN
  // ListController "Frequency" list (default "N"). No lookup endpoint is surfaced,
  // so a static list is used.
  protected readonly frequencyOptions: readonly FormFieldOption[] = [
    { value: 'N', label: 'None' },
    { value: 'D', label: 'Day(s)' },
    { value: 'W', label: 'Week(s)' },
    { value: 'M', label: 'Month(s)' },
    { value: 'Y', label: 'Year(s)' },
  ];

  // MIGRATION: exact legacy validator ErrorMessage text (leading "<br>" stripped).
  // The two legacy "quirk" strings (billing "or Equal" with GreaterThan op; trial
  // "Greater Than Zero" with >= op) are PRESERVED verbatim for functional parity.
  protected readonly roleNameErrors: Record<string, string> = {
    required: 'You Must Enter a Valid Name',
  };
  protected readonly serviceFeeErrors: Record<string, string> = {
    min: 'Service Fee Must Be Greater Than or Equal to Zero',
    pattern: 'Service Fee Value Entered Is Not Valid',
  };
  protected readonly billingPeriodErrors: Record<string, string> = {
    greaterThan: 'Billing Period Must Be Greater Than or Equal to Zero',
    pattern: 'Billing Period Value Entered Is Not Valid',
  };
  protected readonly trialFeeErrors: Record<string, string> = {
    min: 'Trial Fee Must Be Greater Than Zero',
    pattern: 'Trial Fee Value Entered Is Not Valid',
  };
  protected readonly trialPeriodErrors: Record<string, string> = {
    greaterThan: 'Trial Period Must Be Greater Than Zero',
    pattern: 'Trial Period Value Entered Is Not Valid',
  };

  ngOnInit(): void {
    if (this.isEdit && this.roleId !== null) {
      this.loadRole(this.roleId);
    }
  }

  private loadRole(id: number): void {
    this.loading.set(true);
    this.roleService.getRole(id).subscribe({
      next: (role: Role) => {
        this.form.patchValue(role);
        // MIGRATION: editroles.ascx hid txtRoleName and disabled valRoleName in edit
        // mode — the role name is settable only on create. Disabled controls are still
        // included by getRawValue() so the name is submitted on update.
        this.form.controls.roleName.disable();
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
    // getRawValue() includes the disabled roleName control in edit mode.
    const raw = this.form.getRawValue();

    if (this.isEdit && this.roleId !== null) {
      // MIGRATION: UpdateRoleRequest deliberately OMITS roleID/portalID (id from URL).
      const body: UpdateRoleRequest = {
        roleName: raw.roleName,
        description: raw.description,
        roleGroupID: Number(raw.roleGroupID),
        isPublic: raw.isPublic,
        autoAssignment: raw.autoAssignment,
        serviceFee: Number(raw.serviceFee),
        billingFrequency: raw.billingFrequency,
        billingPeriod: Number(raw.billingPeriod),
        trialFee: Number(raw.trialFee),
        trialPeriod: Number(raw.trialPeriod),
        trialFrequency: raw.trialFrequency,
        rsvpCode: raw.rsvpCode,
        iconFile: raw.iconFile,
      };
      this.roleService.updateRole(this.roleId, body).subscribe({
        next: () => this.router.navigate(['/roles']),
        error: (problem: ProblemDetails) => {
          this.saving.set(false);
          this.error.set(this.extractError(problem));
        },
      });
      return;
    }

    // MIGRATION: legacy PortalId came from PortalModuleBase (the ambient portal of the
    // admin's request). The backend RolesController.Create calls RequirePortalAccess(
    // dto.PortalID), so the submitted portalID MUST match the caller's authenticated
    // portal context: a portal admin scoped to portal N is rejected (403) if the body
    // says portal 0. We therefore source it from the authenticated user's portalID
    // (superusers pass RequirePortalAccess for any value; the "?? 0" fallback only
    // applies to the host superuser whose portal context is null/0).
    // MIGRATION: the legacy cmdUpdate_Click fee-defaulting SAVE rules (apply
    // serviceFee/billingPeriod only when frequency !== 'N'; trial only when
    // serviceFee !== 0) are BACKEND concerns (AAP §0.7.1) — the form submits the
    // entered values as-is; no branching business logic is embedded here.
    const body: CreateRoleRequest = {
      portalID: this.authService.currentUser()?.portalID ?? 0,
      roleGroupID: Number(raw.roleGroupID),
      roleName: raw.roleName,
      description: raw.description,
      isPublic: raw.isPublic,
      autoAssignment: raw.autoAssignment,
      serviceFee: Number(raw.serviceFee),
      billingFrequency: raw.billingFrequency,
      billingPeriod: Number(raw.billingPeriod),
      trialFee: Number(raw.trialFee),
      trialPeriod: Number(raw.trialPeriod),
      trialFrequency: raw.trialFrequency,
      rsvpCode: raw.rsvpCode,
      iconFile: raw.iconFile,
    };
    this.roleService.createRole(body).subscribe({
      next: () => this.router.navigate(['/roles']),
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.error.set(this.extractError(problem));
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/roles']);
  }

  requestDelete(): void {
    this.showDeleteDialog.set(true);
  }

  confirmDelete(): void {
    if (this.roleId === null) {
      return;
    }
    this.saving.set(true);
    this.roleService.deleteRole(this.roleId).subscribe({
      next: () => this.router.navigate(['/roles']),
      error: (problem: ProblemDetails) => {
        this.saving.set(false);
        this.error.set(this.extractError(problem));
      },
    });
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
