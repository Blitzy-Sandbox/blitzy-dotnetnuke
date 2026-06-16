import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  type OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, type Observable } from 'rxjs';

import { UserService } from '../../services';
import type { UserListItem, UserSearchQuery } from '../../models';
import {
  DataTableComponent,
  type DataTableAction,
  type DataTableActionEvent,
  type DataTableColumn,
  type DataTableSearch,
} from '../../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner';
import type { ApiResponseMeta, PagedResponse } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';

/** Search type accepted by the user search API, derived from the feature query contract (avoids hardcoding). */
type UserSearchType = NonNullable<UserSearchQuery['searchType']>;

// MIGRATION: legacy Users.ascx.vb default PageSize.
const PAGE_SIZE = 10;

// MIGRATION: special (non-letter) filter modes mirrored from the legacy Users.ascx.vb filter strip.
const FILTER_ALL = 'All';
const FILTER_NONE = 'None';
const FILTER_UNAUTHORIZED = 'Unauthorized';
const FILTER_ONLINE = 'OnLine';

const LETTER_FILTERS: readonly string[] = [
  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
  'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
];

/**
 * UserListComponent — Admin > Users list screen.
 *
 * MIGRATION: reproduces the UI behaviour of the legacy DotNetNuke WebForms grid
 * Website/admin/Users/Users.ascx.vb (BindData) + ManageUsers.ascx.vb orchestration:
 * letter + special filters (All / None / Unauthorized / OnLine), search by Email / Username / Profile,
 * server paging, online/authorized indicators, and per-row edit/roles/delete commands. The grid itself
 * is the shared, presentation-only app-data-table; this component owns all data access via UserService
 * and re-queries the server in response to the grid's events (no HTTP in the grid).
 */
@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
})
export class UserListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** Current grid rows (one server page). */
  readonly rows = signal<UserListItem[]>([]);
  /** Paging metadata from the last PagedResponse (drives the data-table pager auto-hide). */
  readonly meta = signal<ApiResponseMeta | null>(null);
  /** True while a request is in flight (drives app-loading-spinner). */
  readonly loading = signal(false);
  /** Active filter mode (All / a letter / None / Unauthorized / OnLine). */
  readonly activeFilter = signal<string>(FILTER_ALL);
  /** Current free-text search value (bound to the data-table search box; cleared on filter change). */
  readonly searchText = signal('');
  /** The row pending deletion; non-null opens the confirmation dialog. */
  readonly deleteTarget = signal<UserListItem | null>(null);

  /** Confirmation message naming the targeted user (parity with the legacy "delete this item?" prompt). */
  readonly deleteMessage = computed<string>(() => {
    const target = this.deleteTarget();
    return target === null
      ? 'Are you sure you want to delete this user?'
      : `Are you sure you want to delete user '${target.username}'?`;
  });

  /** Mapped search type used for the next query (display string -> API contract value). */
  private searchType: UserSearchType = 'username';
  // MIGRATION: zero-based page index requested from the server (legacy CurrentPage-1).
  private pageIndex = 0;

  /** Filter strip: All + A–Z + the legacy special filters. */
  readonly filters: readonly string[] = [
    FILTER_ALL,
    ...LETTER_FILTERS,
    FILTER_NONE,
    FILTER_UNAUTHORIZED,
    FILTER_ONLINE,
  ];

  // MIGRATION: ddlSearchType options (legacy UserName / Email / profile property). Mutable for the data-table input<string[]>.
  readonly searchTypes: string[] = ['Username', 'Email', 'Profile'];

  /**
   * Grid columns over UserListItem (in-scope legacy Users.ascx columns).
   * MIGRATION: the legacy Address/Telephone profile columns are out of scope (not present on UserListItem);
   * per-column `visible` is available for Column_* visibility parity if a settings source is later wired.
   */
  readonly columns: DataTableColumn<UserListItem>[] = [
    { key: 'username', header: 'Username' },
    { key: 'firstName', header: 'First Name' },
    { key: 'lastName', header: 'Last Name' },
    { key: 'displayName', header: 'Display Name' },
    { key: 'email', header: 'Email' },
    { key: 'isOnline', header: 'Online', type: 'boolean', align: 'center' },
    { key: 'approved', header: 'Authorized', type: 'boolean', align: 'center' },
    { key: 'lockedOut', header: 'Locked Out', type: 'boolean', align: 'center' },
    { key: 'createdDate', header: 'Created', type: 'date' },
    { key: 'lastLoginDate', header: 'Last Login', type: 'date' },
  ];

  /** Per-row commands (legacy Edit / UserRoles / Delete command columns). */
  readonly actions: DataTableAction<UserListItem>[] = [
    { id: 'edit', label: 'Edit', icon: 'edit' },
    { id: 'roles', label: 'Roles', icon: 'roles' },
    {
      id: 'delete',
      label: 'Delete',
      icon: 'delete',
      permission: 'DELETE',
      // MIGRATION: legacy delImage.Visible = Not(user.UserID = PortalSettings.AdministratorId)
      //   AndAlso Not(user.UserID = Me.UserId And user.IsSuperUser). AdministratorId is not part of the
      //   client User model, so the administrator-protection branch is enforced server-side (403 ForbiddenException);
      //   only the self-superuser guard is reproduced client-side here.
      // NOTE: the client User/UserListItem model exposes the PK as `userID` (capital ID) — the
      //   System.Text.Json camelCase serialization of the C# `UserID` property — NOT `userId`.
      hidden: (row) => {
        const current = this.authService.currentUser();
        return current !== null && current.userID === row.userID && row.isSuperUser;
      },
    },
  ];

  ngOnInit(): void {
    this.loadUsers();
  }

  onFilterChange(filter: string): void {
    this.activeFilter.set(filter);
    this.searchText.set('');
    this.pageIndex = 0;
    this.loadUsers();
  }

  onSearchChange(search: DataTableSearch): void {
    this.searchText.set(search.text);
    this.searchType = this.mapSearchType(search.type);
    // MIGRATION: a free-text search clears any active letter/special filter (legacy txtSearch path overrides the letter strip).
    this.activeFilter.set(FILTER_ALL);
    this.pageIndex = 0;
    this.loadUsers();
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex = pageIndex;
    this.loadUsers();
  }

  onRowClick(row: UserListItem): void {
    this.editUser(row);
  }

  onActionClick(event: DataTableActionEvent<UserListItem>): void {
    switch (event.action.id) {
      case 'edit':
        this.editUser(event.row);
        break;
      case 'roles':
        // MIGRATION: legacy UserRoles command -> the per-user roles/profile sub-route.
        this.navigate([event.row.userID, 'profile']);
        break;
      case 'delete':
        this.deleteTarget.set(event.row);
        break;
      default:
        break;
    }
  }

  onCreate(): void {
    this.navigate(['new']);
  }

  onDeleteConfirm(): void {
    const target = this.deleteTarget();
    if (target === null) {
      return;
    }
    this.deleteTarget.set(null);
    this.loading.set(true);
    this.userService
      .deleteUser(target.userID)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: () => this.loadUsers(),
        error: () => this.loadUsers(),
      });
  }

  onDeleteCancel(): void {
    this.deleteTarget.set(null);
  }

  private editUser(row: UserListItem): void {
    // NOTE: `userID` (capital ID) is the canonical PK field name on the core User model.
    this.navigate([row.userID]);
  }

  private navigate(commands: (string | number)[]): void {
    // The list is mounted at the feature's empty-path route; navigate relative to the feature parent.
    const relativeTo = this.route.parent ?? this.route;
    void this.router.navigate(commands, { relativeTo });
  }

  private mapSearchType(type: string): UserSearchType {
    switch (type.toLowerCase()) {
      case 'email':
        return 'email';
      case 'profile':
        return 'profile';
      default:
        return 'username';
    }
  }

  private buildRequest(): Observable<PagedResponse<UserListItem>> | null {
    const query: UserSearchQuery = {
      pageIndex: this.pageIndex,
      pageSize: PAGE_SIZE,
    };

    const text = this.searchText().trim();
    if (text.length > 0) {
      // MIGRATION: legacy txtSearch + ddlSearchType -> GetUsersByEmail / GetUsersByUserName / GetUsersByProfileProperty.
      query.searchText = text;
      query.searchType = this.searchType;
      return this.userService.getUsers(query);
    }

    switch (this.activeFilter()) {
      case FILTER_NONE:
        // MIGRATION: legacy "None" filter shows an empty grid and issues no query.
        return null;
      case FILTER_UNAUTHORIZED:
        return this.userService.getUnauthorizedUsers(query);
      case FILTER_ONLINE:
        return this.userService.getOnlineUsers(query);
      case FILTER_ALL:
        return this.userService.getUsers(query);
      default:
        // MIGRATION: a single-letter filter maps to the legacy SearchText = <letter> + '%' query.
        query.filter = this.activeFilter();
        return this.userService.getUsers(query);
    }
  }

  private loadUsers(): void {
    const request = this.buildRequest();
    if (request === null) {
      this.rows.set([]);
      this.meta.set(null);
      return;
    }

    this.loading.set(true);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.rows.set(response.data);
          this.meta.set(response.meta);
        },
        error: () => {
          this.rows.set([]);
          this.meta.set(null);
        },
      });
  }
}
