// MIGRATION: This component re-expresses the orchestration + validation behavior of the legacy
// DNN Web Forms user-control Website/admin/Security/SecurityRoles.ascx.vb (668 lines). All postback /
// ViewState / PortalModuleBase / .resx / DataCache / ClientAPI machinery is intentionally discarded;
// only the orchestration and validation rules are preserved (see per-member MIGRATION notes + lines).
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { RoleService, type AssignUserRoleRequest } from '../role.service';
import {
  DataTableComponent,
  type ColumnDef,
} from '../../../shared/components/data-table/data-table.component';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import type { UserRole, Role } from '../../../core/models';

// MIGRATION: legacy assignment mode — Page_Init (SecurityRoles.ascx.vb L411-421) chose the mode by
// query string: a RoleId => role-focused (pick users for a fixed role); a UserId => user-focused
// (pick roles for a fixed user). The route roles/:id/assignments supplies a role id, so the component
// enters role-focused mode; the user-focused branch is preserved for behavioral parity.
type AssignmentMode = 'role' | 'user';

interface RoleAssignmentForm {
  userId: FormControl<number | null>;
  roleId: FormControl<number | null>;
  effectiveDate: FormControl<string>;
  expiryDate: FormControl<string>;
  notify: FormControl<boolean>;
}

@Component({
  selector: 'app-role-assignment',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DataTableComponent, ConfirmationDialogComponent],
  templateUrl: './role-assignment.component.html',
  styleUrl: './role-assignment.component.scss',
})
export class RoleAssignmentComponent implements OnInit {
  // inject() DI (NOT constructor injection) — Angular 19 convention.
  private readonly roleService = inject(RoleService);
  private readonly router = inject(Router);

  // MIGRATION: route input binding — the role id from roles/:id/assignments via app-wide
  // withComponentInputBinding(). Legacy read RoleId from the query string (L411-421); it arrives
  // here as a string URL segment.
  readonly id = input<string>();

  // MIGRATION: ADMIN-ACCOUNT identifiers. Legacy sourced these from PortalSettings.AdministratorId and
  // PortalSettings.AdministratorRoleId (cmdAdd_Click L523 / CanRemoveUserFromRole L360-363 [DNN-4285]).
  // No PortalSettings contract exists in this phase, so they are modeled as optional inputs supplied
  // by the portal context (and set in tests). Both default null => admin guard inactive until provided.
  readonly administratorId = input<number | null>(null);
  readonly administratorRoleId = input<number | null>(null);

  // MIGRATION: legacy PageSize-style cap; assignment lists are small, so the data-table pager is
  // auto-suppressed when pageSize >= totalRecords.
  readonly pageSize = 20;

  // Derived role id (role-focused mode) parsed from the string route input.
  readonly roleId = computed<number | null>(() => {
    const raw = this.id();
    if (raw === undefined || raw === null || raw === '') {
      return null;
    }
    const parsed = Number(raw);
    return Number.isNaN(parsed) ? null : parsed;
  });

  // MIGRATION: mode selection (L411-421) — a role id present => role-focused.
  readonly mode = computed<AssignmentMode>(() => (this.roleId() !== null ? 'role' : 'user'));

  // State signals.
  readonly role = signal<Role | null>(null);
  readonly userRoles = signal<UserRole[]>([]);
  readonly loading = signal<boolean>(false);
  readonly selectedUserId = signal<number | null>(null);
  readonly selectedRoleId = signal<number | null>(null);
  private readonly pendingDelete = signal<UserRole | null>(null);
  readonly showDeleteConfirm = signal<boolean>(false);

  // MIGRATION: typed reactive form { userId, roleId, effectiveDate, expiryDate, notify } replacing the
  // Web Forms server controls + chkNotify (L518-551). Empty date strings map to Null.NullDate => null.
  readonly form = new FormGroup<RoleAssignmentForm>({
    userId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    roleId: new FormControl<number | null>(null),
    effectiveDate: new FormControl<string>('', { nonNullable: true }),
    expiryDate: new FormControl<string>('', { nonNullable: true }),
    notify: new FormControl<boolean>(false, { nonNullable: true }),
  });

  // MIGRATION: add-vs-update labeling (grdUserRoles_ItemDataBound L641-664). Default label depends on
  // mode (role-focused => "Add User", user-focused => "Add Role"); it flips to "Update Role" when the
  // currently-selected (user, role) pair already has an assignment in the grid.
  readonly addLabel = computed<string>(() => {
    const uid = this.selectedUserId();
    const rid = this.mode() === 'role' ? this.roleId() : this.selectedRoleId();
    const exists =
      uid !== null &&
      rid !== null &&
      this.userRoles().some((ur) => ur.userId === uid && ur.roleId === rid);
    if (exists) {
      return 'Update Role';
    }
    return this.mode() === 'role' ? 'Add User' : 'Add Role';
  });

  // MIGRATION: grid columns + row key differ by mode (BindGrid L239-257). Role-focused lists
  // users-in-role (key UserId); user-focused lists roles-for-user (key RoleId).
  readonly columns = computed<ColumnDef<UserRole>[]>(() => {
    if (this.mode() === 'role') {
      return [
        { key: 'userId', header: 'User' },
        { key: 'effectiveDate', header: 'Effective Date' },
        { key: 'expiryDate', header: 'Expiry Date' },
      ];
    }
    return [
      { key: 'roleId', header: 'Role' },
      { key: 'effectiveDate', header: 'Effective Date' },
      { key: 'expiryDate', header: 'Expiry Date' },
    ];
  });

  readonly rowKey = computed<keyof UserRole>(() => (this.mode() === 'role' ? 'userId' : 'roleId'));

  ngOnInit(): void {
    // MIGRATION: fix the form's roleId to the route role and load the role for billing defaults —
    // legacy GetRole(RoleId, PortalId) used by GetDates (L273-303) + Page_Init binding.
    const rid = this.roleId();
    if (rid !== null) {
      this.form.controls.roleId.setValue(rid);
      this.selectedRoleId.set(rid);
      this.roleService.getById(rid).subscribe((r) => this.role.set(r));
    }
  }

  // Wired to the user-selection control. Loads the user's assignments and computes default dates.
  onUserChange(rawUserId: number): void {
    const userId = Number.isNaN(rawUserId) ? null : rawUserId;
    this.form.controls.userId.setValue(userId);
    this.selectedUserId.set(userId);
    if (userId === null) {
      return;
    }
    this.loadAssignments(userId);
    const rid = this.mode() === 'role' ? this.roleId() : this.selectedRoleId();
    if (rid !== null) {
      this.computeDates(userId, rid);
    }
  }

  // MIGRATION: cmdAdd_Click (L518-551). Validates, applies the ADMIN-ACCOUNT-DATE GUARD, builds the
  // request, then submits. The write is PROVISIONAL — see the assignUserRole note below.
  onAdd(): void {
    // MIGRATION: legacy gate `If Page.IsValid AndAlso Role IsNot Nothing AndAlso User IsNot Nothing`.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const userId = this.form.controls.userId.value;
    const roleId = this.mode() === 'role' ? this.roleId() : this.form.controls.roleId.value;
    if (userId === null || roleId === null) {
      return;
    }

    // MIGRATION: ADMIN-ACCOUNT-DATE GUARD (L523). Do NOT set effective/expiry dates for the portal
    // Administrator account on the Administrator role — clear both. Legacy compared the integer RoleID
    // to PortalSettings.AdministratorRoleId.ToString (an int-vs-string comparison); the numeric-equality
    // intent is preserved here. // MIGRATION: documented-not-fixed legacy comparison quirk (MIGRATION_NOTES.md).
    if (userId === this.administratorId() && roleId === this.administratorRoleId()) {
      this.form.patchValue({ effectiveDate: '', expiryDate: '' });
    }

    const effective = this.form.controls.effectiveDate.value;
    const expiry = this.form.controls.expiryDate.value;
    const request: AssignUserRoleRequest = {
      userId,
      roleId,
      effectiveDate: effective === '' ? null : effective,
      expiryDate: expiry === '' ? null : expiry,
      notify: this.form.controls.notify.value,
    };

    // MIGRATION: PROVISIONAL WRITE — the backend RolesController does NOT implement user-role assignment
    // WRITE in this phase (DEFERRED; only the read-only getUserRoles lookup exists). Wired so the feature
    // compiles and is unit-tested against mocks; pending backend endpoint recorded in MIGRATION_NOTES.md.
    this.roleService.assignUserRole(request).subscribe(() => {
      this.loadAssignments(userId);
    });
  }

  // MIGRATION: DeleteButtonVisible / RoleController.CanRemoveUserFromRole (L360-363) [DNN-4285] —
  // the portal Administrator cannot be removed from the Administrator role (prevents admin lockout).
  canRemove(userRole: UserRole): boolean {
    const adminId = this.administratorId();
    const adminRoleId = this.administratorRoleId();
    if (adminId === null || adminRoleId === null) {
      return true;
    }
    return !(userRole.userId === adminId && userRole.roleId === adminRoleId);
  }

  // MIGRATION: grdUserRoles_Delete (L565-589) — permission-guarded, then confirmation-gated.
  onDelete(userRole: UserRole): void {
    if (!this.canRemove(userRole)) {
      return;
    }
    this.pendingDelete.set(userRole);
    this.showDeleteConfirm.set(true);
  }

  onConfirmDelete(): void {
    const userRole = this.pendingDelete();
    if (userRole === null) {
      return;
    }
    // MIGRATION: PROVISIONAL WRITE — removeUserRole targets a backend sub-path that is DEFERRED this
    // phase (see assignUserRole note). Wired for compile + mock unit tests (MIGRATION_NOTES.md).
    this.roleService.removeUserRole(userRole.userRoleId).subscribe(() => {
      this.closeDeleteConfirm();
      const userId = this.form.controls.userId.value;
      if (userId !== null) {
        this.loadAssignments(userId);
      }
    });
  }

  onCancelDelete(): void {
    this.closeDeleteConfirm();
  }

  // MIGRATION: legacy cmdCancel returned to the roles list (NavigateURL).
  onCancel(): void {
    void this.router.navigate(['/roles']);
  }

  // MIGRATION: GetDates (L273-303). For the selected (user, role): if an assignment already exists,
  // show its stored dates; otherwise compute a DEFAULT EXPIRY from the role's billing settings
  // (effective stays empty for new). Frequency: D=days, W=weeks(*7), M=months, Y=years; only when
  // billingPeriod > 0.
  private computeDates(userId: number, roleId: number): void {
    const existing = this.userRoles().find((ur) => ur.userId === userId && ur.roleId === roleId);
    if (existing !== undefined) {
      this.form.patchValue({
        effectiveDate: existing.effectiveDate ?? '',
        expiryDate: existing.expiryDate ?? '',
      });
      return;
    }
    let expiry = '';
    const role = this.role();
    if (role !== null && role.billingPeriod > 0) {
      const now = new Date();
      switch (role.billingFrequency) {
        case 'D':
          now.setDate(now.getDate() + role.billingPeriod);
          expiry = this.toDateInput(now);
          break;
        case 'W':
          now.setDate(now.getDate() + role.billingPeriod * 7);
          expiry = this.toDateInput(now);
          break;
        case 'M':
          now.setMonth(now.getMonth() + role.billingPeriod);
          expiry = this.toDateInput(now);
          break;
        case 'Y':
          now.setFullYear(now.getFullYear() + role.billingPeriod);
          expiry = this.toDateInput(now);
          break;
        default:
          break;
      }
    }
    // MIGRATION: legacy left strEffectiveDate empty for new assignments (L273-303).
    this.form.patchValue({ effectiveDate: '', expiryDate: expiry });
  }

  // Format a Date as yyyy-MM-dd for <input type="date"> binding.
  private toDateInput(value: Date): string {
    const year = value.getFullYear();
    const month = `${value.getMonth() + 1}`.padStart(2, '0');
    const day = `${value.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  // MIGRATION: load the assignment grid via the confirmed read endpoint getUserRoles(userId)
  // (GET roles/user/{userId}). NOTE: the legacy role-focused grid used GetUserRolesByRoleName (all
  // users-in-role); that endpoint is NOT implemented this phase (DEFERRED), so the grid is populated
  // from the selected user's assignments. See MIGRATION_NOTES.md.
  private loadAssignments(userId: number): void {
    this.loading.set(true);
    this.roleService.getUserRoles(userId).subscribe({
      next: (rows) => {
        this.userRoles.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private closeDeleteConfirm(): void {
    this.showDeleteConfirm.set(false);
    this.pendingDelete.set(null);
  }
}
