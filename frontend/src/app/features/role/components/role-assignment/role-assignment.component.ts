import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';

import type { AddUserRoleRequest, Role } from '../../models';
import { RoleService } from '../../services';
import { ApiService, type ProblemDetails } from '../../../../core/services/api.service';
import type { User } from '../../../../core/models/user.model';
import { AuthService } from '../../../../core/auth/auth.service';
import {
  DataTableComponent,
  type DataTableAction,
  type DataTableActionEvent,
  type DataTableColumn,
} from '../../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';

/**
 * RoleAssignmentComponent — the User<->Role membership screen.
 *
 * MIGRATION: reinterprets the legacy Web Forms control
 * `Website/admin/Security/SecurityRoles.ascx.vb`
 * (DotNetNuke.Modules.Admin.Security.SecurityRoles) as a standalone Angular 19 screen with
 * UI functional parity (AAP 0.3.4 / 0.7.1), in ROLE-FOCUSED mode only (the route `:id` is the
 * roleId). It lists the users assigned to a role and supports adding a user (picker) and
 * removing a user (confirmation). This screen is the DATA OWNER (injects RoleService +
 * ApiService + ActivatedRoute). User-focused mode (managing a user's roles) is OUT OF SCOPE
 * here and belongs to the features/user feature.
 *
 * MIGRATION (DEV-069 / Finding 5 — parity gap CLOSED): the legacy screen captured per-membership
 * EffectiveDate/ExpiryDate plus an optional "notify user" flag, all passed to
 * RoleController.AddUserRole. These inputs are now RESTORED: the add-user form exposes optional
 * EffectiveDate/ExpiryDate date fields and a notify checkbox, and onAddUser forwards them as the
 * optional AddUserRoleRequest body to assignUserToRole. When the operator leaves the dates blank
 * (and notify off) NO body is sent and the server falls back to the subscription-style assignment
 * (computing ExpiryDate from the role's trial/billing schedule); when a date is supplied the server
 * performs a direct operator-dated assignment. `notify` is forwarded but is a documented server-side
 * NO-OP (no mail subsystem in scope). removeUserFromRole still takes no body. The assigned-users grid
 * shows Name/Username/Email because the core User model returned by getUsersInRole carries no
 * membership dates. See MIGRATION_NOTES.md DEV-069.
 *
 * MIGRATION (wire-casing alignment): the core `User` model (core/models/user.model.ts) exposes
 * the wire-accurate PascalCase-acronym fields `userID`/`portalID` — the .NET 8 BFF serializes
 * with the default System.Text.Json camelCase policy, which lowercases ONLY the first character
 * of `UserID`/`PortalID`, yielding `userID`/`portalID` (NOT `userId`/`portalId`). See the CP1
 * remediation note in user.model.ts / role.model.ts. Member access in this component therefore
 * uses `userID`/`portalID` exactly as declared by the dependency contract.
 */
@Component({
  selector: 'app-role-assignment',
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './role-assignment.component.html',
  styleUrl: './role-assignment.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleAssignmentComponent implements OnInit {
  private readonly roleService = inject(RoleService);
  private readonly apiService = inject(ApiService);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  /** The role being managed (drives the heading). */
  readonly role = signal<Role | null>(null);
  /** Users currently assigned to the role (grid rows; the app-wide core User model). */
  readonly users = signal<User[]>([]);
  /** True while the role/users (or a mutation) request is in flight. */
  readonly loading = signal<boolean>(false);
  /** RFC 7807 error message surfaced as a banner (never swallowed). */
  readonly error = signal<string | null>(null);
  /** Success message surfaced after an add/remove (parity with legacy module messages). */
  readonly successMessage = signal<string | null>(null);
  /** The user pending removal (set when the Remove action fires). */
  readonly userToRemove = signal<User | null>(null);
  /** Whether the remove-user confirmation dialog is open. */
  readonly removeDialogOpen = signal<boolean>(false);
  /** The user id selected in the add-user picker (null = none chosen). */
  readonly selectedUserId = signal<number | null>(null);
  /**
   * MIGRATION (DEV-069 / Finding 5): operator-supplied EffectiveDate (yyyy-MM-dd from <input type="date">,
   * null = effective immediately). Restored from the legacy SecurityRoles date field.
   */
  readonly effectiveDate = signal<string | null>(null);
  /**
   * MIGRATION (DEV-069 / Finding 5): operator-supplied ExpiryDate (yyyy-MM-dd, null = never expires).
   * Restored from the legacy SecurityRoles date field.
   */
  readonly expiryDate = signal<string | null>(null);
  /**
   * MIGRATION (DEV-069 / Finding 5): legacy "notify user" checkbox. Forwarded to the API for contract
   * parity but a documented server-side NO-OP (no mail subsystem in scope).
   */
  readonly notify = signal<boolean>(false);

  /** The roleId from the route (`:id`); role-focused mode only. */
  private readonly roleId = signal<number>(0);
  /** Candidate users for the picker (all portal users from GET /api/v1/users). */
  private readonly candidateUsers = signal<User[]>([]);

  /** Role name for the heading. */
  readonly roleName = computed<string>(() => this.role()?.roleName ?? '');

  /** Picker select value as a string ('' = placeholder) for the native <select> [value] binding. */
  readonly selectedUserValue = computed<string>(() => {
    const id = this.selectedUserId();
    return id === null ? '' : String(id);
  });

  /**
   * Picker candidates excluding users already assigned to the role.
   *
   * MIGRATION: the add-user picker targets NEW assignments. Re-dating an existing member is the
   * server's update-in-place path (assignUserToRole on an existing membership overwrites the dates),
   * but to keep this picker focused and unambiguous, already-assigned users are filtered out of it.
   */
  readonly availableCandidates = computed<User[]>(() => {
    // MIGRATION: member access uses the wire-accurate `userID` (capital acronym) declared by the
    // core User contract (System.Text.Json first-char camelCase; see the class-level note).
    const assigned = new Set(this.users().map((user) => user.userID));
    return this.candidateUsers().filter((candidate) => !assigned.has(candidate.userID));
  });

  /** Confirmation dialog message referencing the targeted user. */
  readonly removeMessage = computed<string>(() => {
    const user = this.userToRemove();
    return user
      ? `Are you sure you want to remove "${user.displayName}" from this role?`
      : 'Are you sure you want to remove this user from the role?';
  });

  /** Grid columns. The core User model has no membership dates, so no date columns exist. */
  readonly columns: DataTableColumn<User>[] = [
    { key: 'displayName', header: 'Name' },
    { key: 'username', header: 'Username' },
    { key: 'email', header: 'Email' },
  ];

  /** Per-row actions: a single destructive Remove gated by MANAGE_SETTINGS. */
  readonly actions: DataTableAction<User>[] = [
    {
      id: 'remove',
      label: 'Remove',
      permission: 'MANAGE_SETTINGS',
      // MIGRATION: legacy DeleteButtonVisible = RoleController.CanRemoveUserFromRole (DNN-4285)
      // prevents removing the last Administrator. The portal AdministratorId/AdministratorRoleId
      // are NOT exposed to the SPA, so this is a best-effort heuristic -- for an apparent
      // administrator role the final remaining member's Remove action is disabled. The
      // server-side CanRemoveUserFromRole check remains authoritative and surfaces any
      // violation as an RFC 7807 error banner ("RoleRemoveError" parity).
      disabled: (user: User): boolean => this.isLastAdministrator(user),
    },
  ];

  ngOnInit(): void {
    // MIGRATION: legacy Page_Init read RoleId from the query string; the SPA reads it from the
    // route param (`:id`). Role-focused mode only.
    const idParam = this.route.snapshot.paramMap.get('id');
    this.roleId.set(idParam !== null ? Number(idParam) : 0);
    this.loadRoleAndUsers();
    this.loadCandidates();
  }

  /**
   * Add the selected user to the role, then refresh the grid.
   *
   * MIGRATION (DEV-069 / Finding 5): legacy cmdAdd_Click -> RoleController.AddUserRole(PortalId, UserId,
   * RoleId, EffectiveDate, ExpiryDate) + notify. onAddUser now forwards the operator's optional
   * EffectiveDate/ExpiryDate/notify as the AddUserRoleRequest body; when nothing was entered NO body is
   * sent and the server uses the subscription-style (schedule-computed) assignment.
   */
  onAddUser(): void {
    const userId = this.selectedUserId();
    if (userId === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    this.roleService.assignUserToRole(this.roleId(), userId, this.buildAssignmentRequest()).subscribe({
      next: () => {
        this.resetAssignmentInputs();
        this.successMessage.set('User added to the role.');
        this.loadUsers();
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /** Update the picker selection from the native <select>. */
  onSelectUser(value: string): void {
    this.selectedUserId.set(value === '' ? null : Number(value));
  }

  /** Update the effective-date input ('' clears to null = effective immediately). */
  onEffectiveDateChange(value: string): void {
    this.effectiveDate.set(value === '' ? null : value);
  }

  /** Update the expiry-date input ('' clears to null = never expires). */
  onExpiryDateChange(value: string): void {
    this.expiryDate.set(value === '' ? null : value);
  }

  /** Update the notify checkbox (legacy "notify user"; forwarded but a server-side NO-OP). */
  onNotifyChange(checked: boolean): void {
    this.notify.set(checked);
  }

  /** DataTable actionClick handler: open the remove confirmation. */
  onActionClick(event: DataTableActionEvent<User>): void {
    if (event.action.id === 'remove') {
      this.userToRemove.set(event.row);
      this.removeDialogOpen.set(true);
    }
  }

  /** Confirmation confirm: remove the targeted user (DELETE, NO body), then refresh. */
  onConfirmRemove(): void {
    const user = this.userToRemove();
    this.removeDialogOpen.set(false);
    if (user === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    // MIGRATION: legacy grdUserRoles_Delete -> RoleController.DeleteUserRole; "RoleRemoveError" on
    // failure. The new API takes NO body, so removeUserFromRole sends ONLY (roleId, userId).
    this.roleService.removeUserFromRole(this.roleId(), user.userID).subscribe({
      next: () => {
        this.successMessage.set('User removed from the role.');
        this.userToRemove.set(null);
        this.loadUsers();
      },
      error: (problem: ProblemDetails) => {
        this.userToRemove.set(null);
        this.handleError(problem);
      },
    });
  }

  /** Confirmation cancel: close without removing. */
  onCancelRemove(): void {
    this.removeDialogOpen.set(false);
    this.userToRemove.set(null);
  }

  /** Dismiss the error banner. */
  dismissError(): void {
    this.error.set(null);
  }

  /** Dismiss the success banner. */
  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  /** Load the role (heading) and its assigned users in parallel. */
  private loadRoleAndUsers(): void {
    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: legacy bound grdUserRoles to GetUserRolesByRoleName(PortalId, Role.RoleName) with
    // DataKeyField="UserId". The SPA loads the role (for the heading) and the assigned users in
    // parallel via forkJoin.
    forkJoin({
      role: this.roleService.getRole(this.roleId()),
      users: this.roleService.getUsersInRole(this.roleId()),
    }).subscribe({
      next: (result) => {
        this.role.set(result.role);
        this.users.set(result.users);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /** Refresh only the assigned-users grid (after an add/remove). */
  private loadUsers(): void {
    this.loading.set(true);
    this.roleService.getUsersInRole(this.roleId()).subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /**
   * Load the candidate users for the picker.
   *
   * MIGRATION: the picker queries GET /api/v1/users?portalId= via the core ApiService to keep
   * this screen self-contained (no dependency on the not-yet-built features/user internals).
   * portalId comes from the authenticated user (legacy PortalModuleBase.PortalId has no SPA
   * equivalent). Member access uses the wire-accurate `portalID` (capital acronym) per the core
   * User contract; the query-string key remains `portalId` (the backend list-filter parameter).
   */
  private loadCandidates(): void {
    const portalId = this.authService.currentUser()?.portalID ?? 0;
    this.apiService.getList<User>(this.apiService.resourceUrl('users'), { portalId }).subscribe({
      next: (page) => this.candidateUsers.set(page.data),
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  /**
   * Best-effort reproduction of the legacy last-Administrator protection (see the `disabled`
   * predicate note above). The authoritative rule lives server-side.
   */
  private isLastAdministrator(_user: User): boolean {
    const name = this.role()?.roleName ?? '';
    const isAdminRole = name.toLowerCase().includes('administrator');
    return isAdminRole && this.users().length <= 1;
  }

  /**
   * Build the optional assignment body from the operator inputs.
   *
   * MIGRATION (DEV-069 / Finding 5): returns `undefined` when the operator entered NEITHER date AND left
   * notify off — that sends NO body, so the server uses the subscription-style assignment (it computes
   * ExpiryDate from the role's trial/billing schedule), preserving the prior default behavior. When any
   * field is set, the body is sent: blank dates serialize as null (effective-immediately / never-expires),
   * and notify is forwarded for contract parity (documented server-side NO-OP).
   */
  private buildAssignmentRequest(): AddUserRoleRequest | undefined {
    const effectiveDate = this.effectiveDate();
    const expiryDate = this.expiryDate();
    const notify = this.notify();
    if (effectiveDate === null && expiryDate === null && !notify) {
      return undefined;
    }
    return { effectiveDate, expiryDate, notify };
  }

  /** Clear the picker + date/notify inputs after a successful assignment. */
  private resetAssignmentInputs(): void {
    this.selectedUserId.set(null);
    this.effectiveDate.set(null);
    this.expiryDate.set(null);
    this.notify.set(false);
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'An unexpected error occurred.');
    this.loading.set(false);
  }
}
