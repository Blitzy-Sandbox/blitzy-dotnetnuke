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

import type { Role } from '../../models';
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
 * MIGRATION (membership window): the legacy screen captured a per-membership EffectiveDate/
 * ExpiryDate window (legacy also auto-computed defaults from the role's BillingPeriod/
 * BillingFrequency via DateAdd) passed to AddUserRole. The new REST API DOES persist this window:
 * POST /api/v1/roles/{roleId}/users/{userId} accepts an AssignUserRole body (effectiveDate/
 * expiryDate, both nullable), so this screen exposes optional Effective/Expiry date inputs and
 * sends them on add (see onAddUser). The legacy billing-derived auto-date defaults and the notify
 * flag are NOT reproduced (no billing/notify surface in the SPA); both dates simply default to
 * null (unbounded) when left blank. Removal (DELETE) still takes NO body. The assigned-users grid
 * shows Name/Username/Email because getUsersInRole returns the core User projection (no membership
 * dates). See MIGRATION_NOTES.md (D-026).
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
   * Optional membership-window inputs bound to the Effective/Expiry date pickers
   * (ISO-8601 'YYYY-MM-DD', or null when blank). MIGRATION: the legacy admin add path captured
   * EffectiveDate/ExpiryDate; these are sent to the API on add. Blank = null (unbounded window),
   * matching the server's empty-window allowance.
   */
  readonly effectiveDate = signal<string | null>(null);
  readonly expiryDate = signal<string | null>(null);

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
   * MIGRATION: re-adding an existing member would simply re-POST the assignment (the backend
   * overwrites the route identity and resets the membership window), so existing members are
   * filtered out of the picker to keep the "Add" affordance meaningful (use Remove then Add to
   * change an existing member's window).
   *
   * CONTRACT: the core User identity field is `userID` (capital ID) -- the C# UserDto.UserID
   * serializes to wire `userID` under JsonNamingPolicy.CamelCase (see core/models/user.model.ts).
   * Do NOT "correct" this to `userId`; that property does not exist and would break the build.
   */
  readonly availableCandidates = computed<User[]>(() => {
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

  /**
   * Stable empty filter set passed to the shared data-table so its default A-Z letter bar is
   * suppressed: the assigned-users grid is a small membership list with no letter filter (parity
   * with the legacy grdUserRoles, which had none). A stable reference avoids re-creating `[]` per
   * change-detection cycle.
   */
  readonly noFilters: readonly string[] = [];

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

  /** Add the selected user to the role (POST with the optional effective/expiry window), then refresh the grid. */
  onAddUser(): void {
    const userId = this.selectedUserId();
    if (userId === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    // MIGRATION: legacy cmdAdd_Click -> RoleController.AddUserRole(..., EffectiveDate, ExpiryDate,
    // UserId, notify). The new API persists the membership window, so assignUserToRole sends
    // (roleId, userId) PLUS the optional { effectiveDate, expiryDate } body (both null = unbounded).
    // The legacy notify flag and billing-derived auto-dates are not reproduced (no SPA surface).
    this.roleService
      .assignUserToRole(this.roleId(), userId, {
        effectiveDate: this.effectiveDate(),
        expiryDate: this.expiryDate(),
      })
      .subscribe({
        next: () => {
          this.selectedUserId.set(null);
          this.effectiveDate.set(null);
          this.expiryDate.set(null);
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

  /** Update the optional effective-date input ('' clears it to null = unbounded). */
  onEffectiveDateChange(value: string): void {
    this.effectiveDate.set(value === '' ? null : value);
  }

  /** Update the optional expiry-date input ('' clears it to null = unbounded). */
  onExpiryDateChange(value: string): void {
    this.expiryDate.set(value === '' ? null : value);
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
    // CONTRACT: `user.userID` (capital ID) is the wire identity field (see availableCandidates note).
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
   * equivalent).
   *
   * CONTRACT: the authenticated user's portal id is read from `currentUser()?.portalID` (capital
   * ID -- the wire field per core/models/user.model.ts). The local `portalId` variable and the
   * `{ portalId }` query-param KEY are intentionally lower-case `Id`: that is the backend query
   * parameter name (matches RoleService.getRoles' `{ portalId }`); only the User PROPERTY access
   * uses the capital-ID wire name.
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

  private handleError(problem: ProblemDetails): void {
    this.error.set(problem.detail ?? problem.title ?? 'An unexpected error occurred.');
    this.loading.set(false);
  }
}
