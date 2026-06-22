import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import type { Role } from '../../models';
import { RoleService } from '../../services';
import { type ProblemDetails, summarizeProblem } from '../../../../core/services/api.service';
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

// MIGRATION: Legacy Roles.ascx.vb BindGroups built a role-group dropdown
// ('AllRoles' = -2, 'GlobalRoles' = -1, then each RoleGroupInfo). There is no
// backend role-groups endpoint in the SPA, so the group filter is reconstructed
// client-side from the distinct roleGroupID values on the loaded roles. Group
// display names are unavailable, so real groups are labelled "Group {id}". The
// shared DataTable renders each filter token verbatim as its button label and
// emits it unchanged, so these human-readable strings double as token + label.
const FILTER_ALL_ROLES = 'All Roles';
const FILTER_GLOBAL_ROLES = 'Global Roles';
const GROUP_FILTER_PREFIX = 'Group ';

@Component({
  selector: 'app-role-list',
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoleListComponent implements OnInit {
  private readonly roleService = inject(RoleService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly roles = signal<Role[]>([]);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly activeFilter = signal<string>(FILTER_ALL_ROLES);
  readonly deleteDialogOpen = signal<boolean>(false);
  readonly roleToDelete = signal<Role | null>(null);

  // MIGRATION: Legacy PortalModuleBase.PortalId has no SPA equivalent; the
  // portal id is derived from the JWT-authenticated current user instead.
  // NOTE: the wire field is `portalID` (System.Text.Json camel-cases only the
  // first character of the C# `PortalID`); see core/models/user.model.ts.
  private readonly currentPortalId = computed<number | null>(
    () => this.authService.currentUser()?.portalID ?? null,
  );

  readonly filters = computed<readonly string[]>(() => {
    const groupIds = Array.from(
      new Set(
        this.roles()
          .map((role) => role.roleGroupID)
          .filter((id): id is number => id !== null && id >= 0),
      ),
    ).sort((a, b) => a - b);

    return [
      FILTER_ALL_ROLES,
      FILTER_GLOBAL_ROLES,
      ...groupIds.map((id) => `${GROUP_FILTER_PREFIX}${id}`),
    ];
  });

  readonly displayedRows = computed<Role[]>(() => {
    const filter = this.activeFilter();
    const roles = this.roles();

    if (filter === FILTER_GLOBAL_ROLES) {
      // MIGRATION: legacy GlobalRoles (-1) == ungrouped; the API maps the legacy
      // Null.NullInteger(-1) sentinel to null, so accept both null and -1.
      return roles.filter(
        (role) => role.roleGroupID === null || role.roleGroupID === -1,
      );
    }

    if (filter.startsWith(GROUP_FILTER_PREFIX)) {
      const groupId = Number.parseInt(filter.slice(GROUP_FILTER_PREFIX.length), 10);
      return roles.filter((role) => role.roleGroupID === groupId);
    }

    return roles;
  });

  readonly deleteMessage = computed<string>(() => {
    const role = this.roleToDelete();
    return role
      ? `Are you sure you want to delete the role "${role.roleName ?? ''}"?`
      : 'Are you sure you want to delete this role?';
  });

  readonly columns: DataTableColumn<Role>[] = [
    { key: 'roleName', header: 'Role' },
    { key: 'description', header: 'Description' },
    { key: 'isPublic', header: 'Public', type: 'boolean' },
    { key: 'autoAssignment', header: 'Auto', type: 'boolean' },
    {
      key: 'serviceFee',
      header: 'Fee',
      type: 'number',
      align: 'right',
      // MIGRATION: legacy FormatPrice rendered a real fee as ##0.00 and an empty
      // string for the null/sentinel fee. serviceFee is nullable on the wire
      // (physical [Roles].ServiceFee is NULL-able), so null renders as ''.
      value: (role: Role): string =>
        role.serviceFee === null ? '' : role.serviceFee.toFixed(2),
    },
  ];

  readonly actions: DataTableAction<Role>[] = [
    {
      id: 'edit',
      label: 'Edit',
      icon: 'edit',
      permission: 'EDIT',
      // MIGRATION: best-effort system-role guard (see isSystemRole).
      hidden: (row: Role): boolean => this.isSystemRole(row),
    },
    {
      id: 'assignments',
      label: 'Manage Users',
      icon: 'group',
      permission: 'EDIT',
    },
    {
      id: 'delete',
      label: 'Delete',
      icon: 'delete',
      permission: 'DELETE',
      hidden: (row: Role): boolean => this.isSystemRole(row),
    },
  ];

  ngOnInit(): void {
    this.loadRoles();
  }

  loadRoles(): void {
    const portalId = this.currentPortalId();
    // MIGRATION: getRoles requires a portalId (the backend returns 400 without
    // it); a missing JWT portal claim is surfaced as an error rather than a
    // bad request.
    if (portalId === null) {
      this.error.set('Unable to determine the current portal for the signed-in user.');
      this.roles.set([]);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.roleService.getRoles(portalId).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: (problem: ProblemDetails) => this.handleError(problem),
    });
  }

  onFilterChange(filter: string): void {
    // MIGRATION: legacy re-queried GetRolesByGroup per group; the SPA filters
    // the already-loaded roles client-side (no role-groups endpoint).
    this.activeFilter.set(filter);
  }

  onActionClick(event: DataTableActionEvent<Role>): void {
    switch (event.action.id) {
      case 'edit':
        void this.router.navigate(['/roles', event.row.roleID, 'edit']);
        break;
      case 'assignments':
        void this.router.navigate(['/roles', event.row.roleID, 'assignments']);
        break;
      case 'delete':
        this.roleToDelete.set(event.row);
        this.deleteDialogOpen.set(true);
        break;
    }
  }

  onConfirmDelete(): void {
    const role = this.roleToDelete();
    this.deleteDialogOpen.set(false);
    if (role === null) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    // MIGRATION: legacy Roles.ascx.vb deleted an EMPTY role GROUP here; this
    // screen deletes a ROLE (backend hard-delete with transactional cascade).
    this.roleService.deleteRole(role.roleID).subscribe({
      next: () => {
        this.successMessage.set(`Role "${role.roleName ?? ''}" was deleted.`);
        this.roleToDelete.set(null);
        this.loadRoles();
      },
      error: (problem: ProblemDetails) => {
        this.roleToDelete.set(null);
        this.handleDeleteError(problem);
      },
    });
  }

  onCancelDelete(): void {
    this.deleteDialogOpen.set(false);
    this.roleToDelete.set(null);
  }

  onAddRole(): void {
    void this.router.navigate(['/roles/new']);
  }

  dismissError(): void {
    this.error.set(null);
  }

  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  // MIGRATION: the legacy system-role guard relied on portal-config ids
  // (Administrator/Registered role ids) that are not exposed to the SPA, so
  // built-in roles are detected best-effort by name. Documented in MIGRATION_NOTES.md.
  private isSystemRole(role: Role): boolean {
    return role.roleName === 'Administrators' || role.roleName === 'Registered Users';
  }

  private handleError(problem: ProblemDetails): void {
    this.error.set(summarizeProblem(problem, 'Failed to load roles.'));
    this.roles.set([]);
    this.loading.set(false);
  }

  // MIGRATION (QA Finding C): a FAILED delete must surface the error WITHOUT
  // destroying the displayed grid. The delete did not mutate anything, so the
  // records still exist; blanking the list to the "No roles found." empty-state
  // would misrepresent server state. Unlike handleError (the LOAD path, where
  // clearing the grid is the correct empty/error treatment), this delete-error
  // handler leaves the current roles intact and only surfaces the banner.
  private handleDeleteError(problem: ProblemDetails): void {
    this.error.set(summarizeProblem(problem, 'Failed to delete the role.'));
    this.loading.set(false);
  }
}
