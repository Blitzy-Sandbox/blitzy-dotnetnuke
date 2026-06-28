// MIGRATION: This component re-expresses the orchestration + validation behavior of the legacy
// DNN Web Forms user-control Website/admin/Security/SecurityRoles.ascx.vb (668 lines). All postback /
// ViewState / PortalModuleBase / .resx / DataCache / ClientAPI machinery is intentionally discarded;
// only the orchestration and validation rules are preserved (see per-member MIGRATION notes + lines).
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { RoleService, type AssignUserRoleRequest } from '../role.service';
import { AuthService } from '../../../core/auth/auth.service';
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import { focusFirstInvalidControl } from '../../../shared/utils/focus-first-invalid.util';
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
  // MIGRATION: AuthService supplies the authenticated principal's portalId, the REQUIRED tenant query
  // for the protected role read endpoints (getById / getUserRoles, AAP Section 0.7.1).
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #19] host ElementRef so an invalid empty-submit can move
  // focus to the first invalid control (focusFirstInvalidControl), restoring the accessibility behavior
  // the long role-assignment form otherwise lost (focus previously stayed on the Add User button).
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

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
  // MIGRATION: [QA F10 - Issue 17] dedicated in-flight signal for the ADD/UPDATE assignment WRITE, mirroring the
  // `submitting`/`saving` signal used by every other form (login/portal/user/role/module/profile). It disables the
  // submit button and drives its "Saving…" progress label while the write is in flight, giving consistent
  // pending-submit UX across all forms. It is scoped to the write only (the grid reload uses `loading`).
  readonly submitting = signal<boolean>(false);
  readonly selectedUserId = signal<number | null>(null);
  readonly selectedRoleId = signal<number | null>(null);
  private readonly pendingDelete = signal<UserRole | null>(null);
  readonly showDeleteConfirm = signal<boolean>(false);

  // MIGRATION (CP-final review - role assignment workflow parity): user-role assignment WRITES (add / remove) are
  // now IMPLEMENTED against the backend assignment sub-resource (POST/DELETE /api/roles/assignments, see
  // RoleService.assignUserRole / removeUserRole). These signals carry the post-write feedback: actionError surfaces a
  // backend failure (e.g. the CanRemoveUserFromRole guard's "cannot remove the administrator" 400), actionSuccess
  // confirms a persisted change. They replace the former write-deferral notice.
  readonly actionError = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);

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
      // MIGRATION: GET /roles/{id} requires the tenant `portalId` query (AAP Section 0.7.1), sourced
      // from the authenticated principal (ngOnInit is not an effect, so this read is loop-safe).
      const portalId = this.auth.currentUser()?.portalId ?? -1;
      this.roleService.getById(rid, portalId).subscribe({
        next: (r) => this.role.set(r),
        // MIGRATION: [QA F10 FINAL ACCEPTANCE - additional fix, same defect class as Issue #11] the role
        // load previously had NO error handler, so a failed GET /roles/{id} (e.g. backend/DB unavailable)
        // propagated an UNCAUGHT HttpErrorResponse to Angular's global ErrorHandler. It now surfaces a
        // user-facing reason through the existing actionError banner (role="alert"), consistent with the
        // write-failure handlers above and Issue #21's "standardize load-failure UX" guidance.
        error: (err: HttpErrorResponse) =>
          this.actionError.set(this.firstMessage(err, 'The role could not be loaded.')),
      });
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
      // MIGRATION: [QA F10 FINAL ACCEPTANCE - Issue #19] move focus to the first invalid control instead
      // of leaving it on the Add User button. Without this, an empty submit produced no visible feedback
      // and screen-reader/keyboard users were not told WHY the submission failed.
      focusFirstInvalidControl(this.host.nativeElement);
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

    // MIGRATION: assignment WRITE — cmdAdd_Click -> RoleController.AddUserRole. The backend
    // POST /api/roles/assignments is an UPSERT (refreshes the dates when the (user, role) pair exists, else
    // inserts), so both the "Add User" and "Update Role" affordances map to this single call (parity with the
    // legacy single AddUserRole call). The portal scope travels in the body and is validated against the JWT
    // "portalId" claim server-side. Empty date strings map to Null.NullDate -> null.
    // MIGRATION: the chkNotify flag is collected for UI parity but NOT sent — the legacy notification email is the
    // Services.Mail/Messaging subsystem, OUT OF SCOPE per AAP §0.6.2 (no notification is dispatched this phase).
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    const request: AssignUserRoleRequest = {
      portalId,
      userId,
      roleId,
      effectiveDate: this.emptyToNull(this.form.controls.effectiveDate.value),
      expiryDate: this.emptyToNull(this.form.controls.expiryDate.value),
    };

    this.actionError.set(null);
    this.actionSuccess.set(null);
    // MIGRATION: [QA F10 - Issue 17] mark the WRITE in flight so the submit button disables + shows "Saving…".
    this.submitting.set(true);
    this.loading.set(true);
    this.roleService.assignUserRole(request).subscribe({
      next: () => {
        // The write succeeded; re-enable the submit affordance. The subsequent grid reload uses `loading`.
        this.submitting.set(false);
        this.actionSuccess.set('The role assignment was saved.');
        // Reload from the server so the grid reflects the persisted assignment (and its computed dates).
        this.loadAssignments(userId);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.loading.set(false);
        this.actionError.set(this.firstMessage(err, 'The role assignment could not be saved.'));
      },
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
    // MIGRATION: assignment-removal WRITE — grdUserRoles_Delete -> RoleController.DeleteUserRole. Calls
    // DELETE /api/roles/{roleId}/users/{userId}?portalId=. The server-side CanRemoveUserFromRole guard
    // (L741/L764) is authoritative: removing the portal Administrator from the Administrator role returns 400,
    // which is surfaced via actionError (the client-side canRemove() hides the button as a first line of defense).
    this.closeDeleteConfirm();
    this.actionError.set(null);
    this.actionSuccess.set(null);
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    this.loading.set(true);
    this.roleService.removeUserRole(portalId, userRole.roleId, userRole.userId).subscribe({
      next: () => {
        this.actionSuccess.set('The role assignment was removed.');
        this.loadAssignments(userRole.userId);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.actionError.set(this.firstMessage(err, 'The role assignment could not be removed.'));
      },
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

  // MIGRATION: load the assignment grid via getUserRoles(userId) (GET roles/user/{userId}) -- the
  // user-scoped read of the user-role sub-resource. The grid reflects the selected user's current
  // assignments, which is precisely what the assign / update / remove workflow acts on. The legacy
  // role-focused grid bound GetUserRolesByRoleName (every user in a role); a role-scoped users-in-role
  // listing is a distinct read projection that is not part of the AAP role resource surface (Section
  // 0.3.4 defines the Roles resource as CRUD), so the management workflow here is driven by selecting
  // the target user and acting on their membership. See MIGRATION_NOTES.md.
  private loadAssignments(userId: number): void {
    this.loading.set(true);
    // MIGRATION: GET /roles/user/{userId} requires the tenant `portalId` query (AAP Section 0.7.1),
    // sourced from the authenticated principal.
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    this.roleService.getUserRoles(userId, portalId).subscribe({
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

  // MIGRATION: empty date textbox -> Null.NullDate -> null (the backend DTO's nullable DateTime). A non-empty
  // yyyy-MM-dd string is forwarded as-is for server-side DateTime parsing.
  private emptyToNull(value: string): string | null {
    return value === '' ? null : value;
  }

  // Extracts the first user-facing message from a backend RFC 7807 failure (reuses the canonical interceptor
  // parser), falling back to the supplied default when the body carries no message.
  private firstMessage(error: HttpErrorResponse, fallback: string): string {
    const parsed = parseProblemDetails(error.error);
    return parsed.messages.length > 0 ? parsed.messages[0] : fallback;
  }
}
