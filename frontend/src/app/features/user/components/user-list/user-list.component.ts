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
import { HasPermissionDirective } from '../../../../shared/directives/has-permission';
import type {
  ApiResponseMeta,
  PagedResponse,
  ProblemDetails,
} from '../../../../core/services/api.service';
import { summarizeProblem } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';

/**
 * Search type accepted by the user search box.
 *
 * MIGRATION: the legacy ddlSearchType (UserName / Email / profile property) had direct
 * server counterparts (GetUsersByUserName / GetUsersByEmail / GetUsersByProfileProperty).
 * The Phase-1 REST contract `UserSearchQuery` deliberately carries NO search-type field
 * (see ../../models/user.model.ts), so this union is defined LOCALLY rather than derived
 * from the query contract; it is used only to select the client-side search predicate.
 */
type UserSearchType = 'username' | 'email' | 'profile';

// MIGRATION: legacy Users.ascx.vb default PageSize (the legacy "Records_PerPage" portal
// setting). That portal-settings source is out of scope for Phase 1, so the default is
// pinned here.
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
 *
 * MIGRATION (Phase-1 contract reconciliation): the Phase-1 REST surface only supports plain server
 * paging — `UserService.getUsers` forwards `{ portalId, pageIndex, pageSize }` and nothing else. The
 * legacy server-side first-letter / Unauthorized / OnLine / free-text filtering has no REST counterpart
 * yet (the dedicated GetUnauthorizedUsers / GetOnlineUsers / GetUsersBy* listings are intentionally
 * DEFERRED — see ../../services/user.service.ts and the root MIGRATION_NOTES.md). To keep every legacy
 * filter affordance functional, those filters are reproduced as a client-side view filter over the
 * fetched page (see applyClientView); the data-table pager continues to reflect the server's
 * unfiltered totals.
 */
@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DataTableComponent,
    ConfirmationDialogComponent,
    LoadingSpinnerComponent,
    HasPermissionDirective,
  ],
})
export class UserListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  /** Current grid rows (one server page, after any client-side view filter). */
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

  /**
   * MIGRATION (QA Finding D): RFC 7807 error message surfaced as a dismissible alert banner so a
   * server failure (e.g. a 503 when the database is unreachable) is NEVER silently rendered as the
   * "No users found." empty state. This mirrors the module-list / role-list error-handling pattern
   * and distinguishes "the server failed" from "there are genuinely no users". `null` => no banner.
   */
  readonly error = signal<string | null>(null);

  /** Confirmation message naming the targeted user (parity with the legacy "delete this item?" prompt). */
  readonly deleteMessage = computed<string>(() => {
    const target = this.deleteTarget();
    return target === null
      ? 'Are you sure you want to delete this user?'
      : `Are you sure you want to delete user '${target.username}'?`;
  });

  /** Mapped search type used for the next query (display string -> client predicate). */
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
    // MIGRATION: gate the Edit and Roles row commands behind the 'EDIT' permission so the
    //   user-list affordances are RBAC-gated consistently with the portal/module/role lists
    //   (each of which carries permission:'EDIT' on its edit/assignment actions). These are
    //   administrative mutations of a user, so 'EDIT' is the appropriate key; the API remains
    //   the authoritative authorization boundary (server 403). Superusers/Administrators are
    //   unaffected via the has-permission superuser bypass.
    { id: 'edit', label: 'Edit', icon: 'edit', permission: 'EDIT' },
    { id: 'roles', label: 'Roles', icon: 'roles', permission: 'EDIT' },
    {
      id: 'delete',
      label: 'Delete',
      icon: 'delete',
      permission: 'DELETE',
      // MIGRATION: legacy delImage.Visible = Not(user.UserID = PortalSettings.AdministratorId)
      //   AndAlso Not(user.UserID = Me.UserId And user.IsSuperUser). AdministratorId is not part of the
      //   client User model, so the administrator-protection branch is enforced server-side (403 ForbiddenException);
      //   only the self-superuser guard is reproduced client-side here.
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

  /** Dismiss the error banner (QA Finding D). */
  dismissError(): void {
    this.error.set(null);
  }

  private editUser(row: UserListItem): void {
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

  // MIGRATION: legacy UsersPortalId resolved PortalId (or Null.NullInteger for a superuser, to span all
  //   portals). The Phase-1 GET /api/v1/users contract REQUIRES a concrete portalId (it returns a 400
  //   ProblemDetails when absent), so the authenticated user's portalID is used; 0 (the default portal)
  //   is a defensive fallback when no user is cached.
  private currentPortalId(): number {
    return this.authService.currentUser()?.portalID ?? 0;
  }

  private buildRequest(): Observable<PagedResponse<UserListItem>> | null {
    // MIGRATION: the Phase-1 REST contract (UserSearchQuery) carries ONLY portalId + paging; the legacy
    //   letter / filterProperty / free-text / ddlSearchType parameters have no server counterpart and so
    //   are NOT sent (they are applied client-side in applyClientView).
    const query: UserSearchQuery = {
      portalId: this.currentPortalId(),
      pageIndex: this.pageIndex,
      pageSize: PAGE_SIZE,
    };

    // MIGRATION: free-text search takes precedence over the filter strip (legacy txtSearch + ddlSearchType
    //   -> GetUsersByEmail / GetUsersByUserName / GetUsersByProfileProperty). Those by-field listings are
    //   deferred in Phase 1, so the standard paged list is fetched and the term is matched client-side.
    if (this.searchText().trim().length > 0) {
      return this.userService.getUsers(query);
    }

    // MIGRATION: map the legacy filter strip onto the single Phase-1 paged list. Only "None" suppresses
    //   the query (empty grid, no request). All / Unauthorized / OnLine / single-letter all map to GET
    //   /api/v1/users; their distinguishing predicate (approved === false, isOnline === true, username
    //   startsWith <letter> + '%') is applied client-side in applyClientView because neither the
    //   UserSearchQuery contract nor a dedicated GetUnauthorizedUsers / GetOnlineUsers listing exists in
    //   Phase 1 (see ../../services/user.service.ts + MIGRATION_NOTES.md).
    if (this.activeFilter() === FILTER_NONE) {
      return null;
    }
    return this.userService.getUsers(query);
  }

  /**
   * MIGRATION: client-side reproduction of the legacy server-side filter/search, applied over the
   * fetched server page because the Phase-1 REST contract has no filter/search parameters and the
   * dedicated GetUnauthorizedUsers / GetOnlineUsers / GetUsersBy* listings are deferred. Free-text
   * search takes precedence (mirroring buildRequest); otherwise the active letter/special filter is
   * applied. `All` (and `None`, which never reaches here) pass through unfiltered.
   */
  private applyClientView(items: UserListItem[]): UserListItem[] {
    const text = this.searchText().trim().toLowerCase();
    if (text.length > 0) {
      return items.filter((item) => this.matchesSearch(item, text));
    }

    switch (this.activeFilter()) {
      case FILTER_UNAUTHORIZED:
        return items.filter((item) => item.approved === false);
      case FILTER_ONLINE:
        return items.filter((item) => item.isOnline === true);
      case FILTER_ALL:
      case FILTER_NONE:
        return items;
      default:
        // MIGRATION: single-letter filter -> legacy SearchText = <letter> + '%' over the username.
        return items.filter((item) =>
          item.username.toLowerCase().startsWith(this.activeFilter().toLowerCase()),
        );
    }
  }

  /** MIGRATION: maps the legacy ddlSearchType field selection onto the searchable client columns. */
  private matchesSearch(item: UserListItem, text: string): boolean {
    // The legacy "Profile" search spanned profile properties that are not projected onto UserListItem,
    // so it falls back to the username column here.
    const haystack = this.searchType === 'email' ? item.email : item.username;
    return haystack.toLowerCase().includes(text);
  }

  private loadUsers(): void {
    // MIGRATION (QA Finding D): clear any prior error before each (re-)load so a stale banner never
    // lingers across a successful retry.
    this.error.set(null);

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
          this.rows.set(this.applyClientView(response.data));
          this.meta.set(response.meta);
        },
        error: (problem: ProblemDetails) => {
          // MIGRATION (QA Finding D): surface the server failure as an error banner instead of
          // silently clearing the grid to the "No users found." empty state. Any rows already on
          // screen are intentionally PRESERVED so a 5xx/network failure is never misrepresented as
          // an empty data set; the template suppresses the empty-state text while this error shows.
          this.error.set(summarizeProblem(problem, 'Failed to load users.'));
        },
      });
  }
}
