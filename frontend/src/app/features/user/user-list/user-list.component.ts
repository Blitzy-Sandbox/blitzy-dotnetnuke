import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import { User } from '../../../core/models';
import { QueryParams } from '../../../core/services/api.service';
import {
  ColumnDef,
  DataTableComponent,
  RowAction,
  RowActionEvent,
} from '../../../shared/components/data-table';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { UserService } from '../user.service';

/**
 * MIGRATION: Flat row view-model for the users grid.
 * The legacy grdUsers grid (Website/admin/Users/users.ascx) renders several columns
 * derived from the SAME nested objects (Address + Telephone from Profile;
 * CreatedDate + LastLogin + Authorized + Online from Membership). DataTableComponent
 * tracks columns by String(col.field), so every column requires a DISTINCT `keyof T`.
 * We therefore project `User` -> `UserListRow` (each display value a top-level scalar)
 * so each column maps to a unique key and per-column client sort/filter works.
 */
interface UserListRow {
  userID: number;
  username: string;
  firstName: string;
  lastName: string;
  displayName: string;
  address: string;
  telephone: string;
  email: string;
  createdDate: string;
  lastLoginDate: string;
  approved: boolean;
  isOnLine: boolean;
}

/** Search-field option mirroring the legacy ddlSearchType dropdown. */
interface SearchField {
  readonly value: string;
  readonly label: string;
}

@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
})
export class UserListComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  // QA finding (Report 4, Issue 1): surface load failures instead of masking them as an
  // empty result set. Mirrors the accepted PortalListComponent error-signal pattern.
  protected readonly error = signal<string | null>(null);
  private readonly users = signal<User[]>([]);
  protected readonly searchTerm = signal('');
  protected readonly searchType = signal('query');

  private readonly pendingDelete = signal<UserListRow | null>(null);
  protected readonly confirmOpen = signal(false);

  // MIGRATION: legacy ddlSearchType (Username / Email / dynamic profile props) simplified
  // to the core searchable fields exposed by the API. 'query' == "All Fields" (free text).
  protected readonly searchFields: readonly SearchField[] = [
    { value: 'query', label: 'All Fields' },
    { value: 'Username', label: 'Username' },
    { value: 'Email', label: 'Email' },
  ];

  protected readonly rowKey: keyof UserListRow = 'userID';

  // MIGRATION: grdUsers <asp:DataGrid AutoGenerateColumns="false"> columns (users.ascx)
  // -> ColumnDef<UserListRow>[]. Address mirrors DisplayAddress(Profile.*); CreatedDate /
  // LastLogin mirror DisplayDate (type:'date' -> dateFormat pipe); Authorized / Online mirror
  // the checked/unchecked .gif (type:'boolean' -> yesNo pipe). DataTableComponent applies
  // those pipes internally, so no value() accessors and no pipe imports are needed here.
  protected readonly columns: ColumnDef<UserListRow>[] = [
    { field: 'username', header: 'Username', sortable: true },
    { field: 'firstName', header: 'First Name', sortable: true },
    { field: 'lastName', header: 'Last Name', sortable: true },
    { field: 'displayName', header: 'Display Name', sortable: true },
    { field: 'address', header: 'Address' },
    { field: 'telephone', header: 'Telephone' },
    { field: 'email', header: 'Email', sortable: true },
    { field: 'createdDate', header: 'Created Date', type: 'date', sortable: true },
    { field: 'lastLoginDate', header: 'Last Login', type: 'date', sortable: true },
    { field: 'approved', header: 'Authorized', type: 'boolean' },
    { field: 'isOnLine', header: 'Online', type: 'boolean' },
  ];

  // MIGRATION: dnn:imagecommandcolumn Edit / Delete (users.ascx) -> row actions.
  // The legacy "UserRoles" command (user<->role assignment) is OUT OF SCOPE: the
  // UsersInRoles junction is not part of the 14-entity target model (see
  // UserConfiguration Ignore(e => e.Roles)) and no user-role assignment endpoint
  // exists in the API surface (AAP §0.3.1). See MIGRATION_NOTES.md.
  protected readonly actions: RowAction[] = [
    { action: 'edit', label: 'Edit', tooltip: 'Edit user' },
    // QA finding F4: entry point to the change-password screen (route /users/:id/password +
    // ChangePasswordComponent already existed but were unreachable — no UI navigated to them).
    // MIGRATION: legacy ManageUsers.ascx "Manage Password" tab reached per user row.
    { action: 'changePassword', label: 'Change Password', tooltip: 'Change password' },
    { action: 'delete', label: 'Delete', tooltip: 'Delete user' },
  ];

  protected readonly rows = computed<UserListRow[]>(() =>
    this.users().map((user) => this.toRow(user)),
  );

  protected readonly deleteMessage = computed<string>(() => {
    const row = this.pendingDelete();
    if (row === null) {
      return '';
    }
    return `Are you sure you want to delete user "${row.username}"? This action cannot be undone.`;
  });

  ngOnInit(): void {
    this.loadUsers();
  }

  protected onSearch(term: string, field: string): void {
    this.searchTerm.set(term.trim());
    this.searchType.set(field);
    this.loadUsers();
  }

  protected onClearSearch(): void {
    this.searchTerm.set('');
    this.searchType.set('query');
    this.loadUsers();
  }

  protected onRowAction(event: RowActionEvent<UserListRow>): void {
    if (event.action === 'edit') {
      // MIGRATION: legacy Edit image-command (EditMode=URL, KeyField=UserID) -> route to editor.
      void this.router.navigate(['/users', event.row.userID]);
    } else if (event.action === 'changePassword') {
      // QA finding F4: route to the (previously unreachable) change-password screen. In-app
      // navigation preserves the memory-only JWT session (a full reload would clear it).
      void this.router.navigate(['/users', event.row.userID, 'password']);
    } else if (event.action === 'delete') {
      // MIGRATION: legacy Delete image-command (immediate postback) -> confirmation dialog first.
      this.pendingDelete.set(event.row);
      this.confirmOpen.set(true);
    }
  }

  protected onConfirmDelete(): void {
    const row = this.pendingDelete();
    if (row === null) {
      return;
    }
    // MIGRATION: UserController.DeleteUser postback -> DELETE /api/users/{id} (204), then refresh.
    this.loading.set(true);
    this.userService.deleteUser(row.userID).subscribe({
      next: () => {
        this.pendingDelete.set(null);
        this.loadUsers();
      },
      error: () => {
        this.pendingDelete.set(null);
        this.loading.set(false);
      },
    });
  }

  protected onCancelDelete(): void {
    this.pendingDelete.set(null);
  }

  protected onAddUser(): void {
    // MIGRATION: "Add New User" link -> route to the create form (UserId="new").
    void this.router.navigate(['/users', 'new']);
  }

  private loadUsers(): void {
    const term = this.searchTerm();
    const field = this.searchType();
    let params: QueryParams | undefined;
    if (term.length === 0) {
      params = undefined;
    } else if (field === 'Username' || field === 'Email') {
      // MIGRATION: GetUsersByUserName / GetUsersByEmail (Users.ascx.vb BindData) -> filterProperty/filter.
      params = { filterProperty: field, filter: term };
    } else {
      params = { query: term };
    }

    this.error.set(null);
    this.loading.set(true);
    this.userService.getUsers(params).subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      // QA finding (Report 4, Issue 1): a failed load previously reset the rows to [] and
      // fell through to the "No users found." empty state, masking the error. Surface the
      // RFC 7807 title (normalized/rethrown by ApiService) and keep any prior rows intact.
      error: (err) => {
        this.error.set(err?.title ?? 'Failed to load users');
        this.loading.set(false);
      },
    });
  }

  private toRow(user: User): UserListRow {
    return {
      userID: user.userID,
      username: user.username,
      firstName: user.firstName,
      lastName: user.lastName,
      displayName: user.displayName,
      address: this.formatAddress(user),
      telephone: user.profile.telephone,
      email: user.email,
      createdDate: user.membership.createdDate,
      lastLoginDate: user.membership.lastLoginDate,
      approved: user.membership.approved,
      isOnLine: user.membership.isOnLine,
    };
  }

  // MIGRATION: DisplayAddress(Profile.Unit, Street, City, Region, Country, PostalCode) (Users.ascx.vb).
  // All Profile fields are required non-null strings, so we trim, drop empties, and join.
  private formatAddress(user: User): string {
    const p = user.profile;
    return [p.unit, p.street, p.city, p.region, p.country, p.postalCode]
      .map((part) => part.trim())
      .filter((part) => part.length > 0)
      .join(', ');
  }
}
