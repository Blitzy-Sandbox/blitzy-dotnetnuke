// MIGRATION: Angular 19 standalone replacement for the legacy DNN admin user grid
// (Website/admin/Users/Users.ascx.vb [742] — class UserAccounts : PortalModuleBase, IActionable)
// plus orchestration/security from Website/admin/Users/ManageUsers.ascx.vb [973]. The Web Forms
// DataGrid + pager + letter strip + search dropdown + ViewState/postback are discarded; only the
// list / filter / paging / delete behavior is re-expressed over the shared DataTableComponent.
import {
  ChangeDetectionStrategy,
  Component,
  type OnInit,
  type TemplateRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService } from '../../../core/auth/auth.service';
// MIGRATION: [QA F4-006] canonical RFC 7807 parser so a failed DELETE surfaces a friendly message (role-assignment
// gold-standard pattern) instead of being silently swallowed.
import { parseProblemDetails } from '../../../core/interceptors/error.interceptor';
import type { User } from '../../../core/models';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { DataTableComponent, type ColumnDef } from '../../../shared/components/data-table/data-table.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { UserService } from '../user.service';

@Component({
  selector: 'app-user-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DataTableComponent, ConfirmationDialogComponent, LoadingSpinnerComponent],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
})
export class UserListComponent implements OnInit {
  // MIGRATION: components talk only to the feature service (never ApiService/HttpClient directly).
  protected readonly userService = inject(UserService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  // MIGRATION: legacy PageSize came from the Records_PerPage portal setting (default 10 —
  // UserModuleBase.vb L134-136). The migration standardizes the grid page size on 20.
  protected readonly pageSize = 20;

  // MIGRATION: zero-based paging. Legacy converted 1-based UI to 0-based via `CurrentPage - 1`
  // (Users.ascx.vb L265); that conversion is REMOVED — DataTable emits/consumes zero-based pages.
  readonly currentPage = signal(0);

  // MIGRATION: active filter token. Legacy default was Display_Mode = None (show nothing until a
  // filter/search was chosen, Users.ascx.vb); the migration defaults to 'All' for usability.
  readonly filter = signal<string>('All');

  // MIGRATION: legacy ddlSearchType offered Username / Email / profile-property (Users.ascx.vb
  // L577-582). The shared DataTable emits a single free-text term with NO field selector, so
  // free-text/letter search defaults to 'Username' (first-letter -> GetUsersByUserName). Email and
  // profile-property search would require a field selector the shared DataTable does not expose (gap).
  readonly searchField = signal<string>('Username');

  // The row pending deletion drives the confirmation dialog (null = no dialog).
  readonly pendingDelete = signal<User | null>(null);

  // MIGRATION: [QA F4-006] friendly delete-failure message (role-assignment gold-standard pattern). The previous
  // error callback only cleared pendingDelete (closed the dialog) but surfaced NO message -- a silent swallow.
  // Now the error callback also sets this signal, rendered as a role="alert" banner. Null = no error.
  readonly actionError = signal<string | null>(null);

  // MIGRATION: [QA F4-014] in-flight guard for the confirmed delete; bound to the dialog's [busy] input and
  // consulted by confirmDelete so rapid repeated Confirm clicks fire exactly one DELETE.
  readonly deleting = signal<boolean>(false);

  // MIGRATION: A-Z letter strip + status filters (Users.ascx.vb CreateLetterSearch L304-316:
  // Filter.Text A..Z + All + OnLine + Unauthorized). Typed as string[] (mutable) to satisfy the
  // DataTable `filters = input<string[]>([])` contract.
  protected readonly filters: string[] = [
    'All',
    ...Array.from({ length: 26 }, (_, i) => String.fromCharCode(65 + i)),
    'Online',
    'Unauthorized',
  ];

  // Per-row "Profile" action template — DataTable exposes only view/edit/delete row actions, so a
  // profile affordance is injected as a custom column cell.
  protected readonly profileCell = viewChild<TemplateRef<unknown>>('profileCell');

  // MIGRATION: canonical visible columns (Users.ascx.vb Page_Init L508-555 + UserModuleBase defaults
  // L110-124). Legacy Column_* portal settings toggled visibility (Email/LastLogin defaulted hidden);
  // those settings are not migrated, so the canonical set is always shown. The legacy ONLINE column
  // (user.Membership.IsOnLine, Users.ascx.vb L697-702) is OMITTED — the new User model carries no
  // membership/online field (documented gap). Boolean "Authorized" uses yesNo rendering.
  protected readonly columns = computed<ColumnDef<User>[]>(() => {
    const base: ColumnDef<User>[] = [
      { key: 'username', header: 'Username' },
      { key: 'displayName', header: 'Display Name' },
      { key: 'email', header: 'Email' },
      // MIGRATION: [QA F4-009] render the Created Date as a human-readable date (date pipe) not raw ISO.
      { key: 'createdDate', header: 'Created Date', date: true },
      { key: 'isApproved', header: 'Authorized', yesNo: true },
    ];
    const cell = this.profileCell();
    return cell ? [...base, { key: 'profile', header: 'Profile', cell }] : base;
  });

  ngOnInit(): void {
    // MIGRATION: initial bind (Users.ascx.vb Page_Load -> BindData L569-599) for the default filter.
    this.load();
  }

  onPageChange(pageIndex: number): void {
    this.currentPage.set(pageIndex);
    this.load();
  }

  onFilterChange(value: string): void {
    // MIGRATION: a new filter/search resets to the first page (legacy btnSearch_Click set CurrentPage = 1).
    this.currentPage.set(0);
    this.filter.set(value);
    this.load();
  }

  onView(user: User): void {
    void this.router.navigate(['/users', user.userId]);
  }

  onEdit(user: User): void {
    void this.router.navigate(['/users', user.userId, 'edit']);
  }

  onProfile(user: User): void {
    // MIGRATION: the legacy "User Roles" link (Users.ascx.vb L535-545) routes to role assignment,
    // which belongs to features/role (out of scope here). This profile action maps to the migrated
    // :id/profile route.
    void this.router.navigate(['/users', user.userId, 'profile']);
  }

  onAddUser(): void {
    // MIGRATION: legacy AddContent module action (Users.ascx.vb L725).
    void this.router.navigate(['/users', 'new']);
  }

  onDeleteRequest(user: User): void {
    // MIGRATION: protected-user rule (Users.ascx.vb L693-694 / User.ascx.vb L267). The shared
    // DataTable's showDelete is table-wide (no per-row hide), so protected rows are short-circuited
    // here instead of hidden at the column level.
    if (this.isProtectedUser(user)) {
      return;
    }
    // MIGRATION: delete REQUIRES confirmation (Users.ascx.vb L523 DeleteItem confirm +
    // grdUsers_DeleteCommand L646-669). Mounting the dialog via the pendingDelete signal replaces the
    // legacy client-side confirm() + server postback.
    // MIGRATION: [QA F4-006] clear any stale failure banner when opening a fresh delete dialog.
    this.actionError.set(null);
    this.pendingDelete.set(user);
  }

  confirmDelete(): void {
    const user = this.pendingDelete();
    if (user === null) {
      return;
    }
    // MIGRATION: [QA F4-014] re-entrancy guard -- ignore a confirm while a DELETE is already in flight so rapid
    // repeated Confirm clicks (the dialog stays mounted until the request resolves) fire exactly one request.
    if (this.deleting()) {
      return;
    }
    // MIGRATION: legacy DeleteUser(objUser, True, False) (Users.ascx.vb L660). On success the grid
    // rebinds; here we clear the dialog and reload the current page/filter. The protected DELETE /users/{id}
    // requires the tenant `portalId` query (AAP Section 0.7.1), sourced from the authenticated principal.
    // MIGRATION: [QA F4-006/F4-014] subscribe with next AND a message-surfacing error callback. deleting() gates
    // the dialog's [busy] input. On success: close the dialog + reload. On failure: close the dialog, clear busy,
    // and surface a friendly RFC 7807 message (was a silent swallow). The error is HANDLED, not thrown globally.
    this.actionError.set(null);
    this.deleting.set(true);
    const portalId = this.auth.currentUser()?.portalId ?? -1;
    this.userService.delete(user.userId, portalId).subscribe({
      next: () => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.actionError.set(this.firstMessage(err, 'The user could not be deleted. Please try again.'));
      },
    });
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  // MIGRATION: [QA F4-006] first user-facing message from a backend RFC 7807 failure (reuses the canonical
  // interceptor parser); falls back to the supplied default when the body carries no message (e.g. a status-0
  // transport failure whose err.error is a ProgressEvent). Mirrors role-assignment's firstMessage helper.
  private firstMessage(error: HttpErrorResponse, fallback: string): string {
    const parsed = parseProblemDetails(error.error);
    return parsed.messages.length > 0 ? parsed.messages[0] : fallback;
  }

  private load(): void {
    const page = this.currentPage();
    const filter = this.filter();

    // MIGRATION: BindData filter dispatch (Users.ascx.vb L248-291). PortalId scoping (legacy UsersPortalId
    // L144-152) is threaded as the REQUIRED `portalId` query the backend UsersController list endpoint demands
    // ([FromQuery, BindRequired], AAP Section 0.7.1), sourced from the authenticated principal. It is passed on
    // EVERY list call so tenant scoping is never silently dropped.
    const portalId = this.auth.currentUser()?.portalId ?? -1;

    if (filter === 'All') {
      // 'All' -> GetUsers (full paged list).
      this.userService.list(page, this.pageSize, undefined, undefined, portalId).subscribe();
      return;
    }
    if (filter === 'Online' || filter === 'Unauthorized') {
      // MIGRATION: legacy GetOnlineUsers / GetUnAuthorizedUsers were non-paged (Users.ascx.vb
      // L259-263, pager hidden). 'Unauthorized' maps to isApproved = false; 'Online' has NO User-model
      // field (gap) and depends on a dedicated backend filter/endpoint coordinated server-side. The
      // exact backend query is coordinated via the filter token.
      this.userService.list(page, this.pageSize, filter, undefined, portalId).subscribe();
      return;
    }
    // Single A-Z letter (first-letter username search -> GetUsersByUserName) or free-text term.
    this.userService.list(page, this.pageSize, filter, this.searchField(), portalId).subscribe();
  }

  private isProtectedUser(user: User): boolean {
    const current = this.auth.currentUser();
    // MIGRATION (Users.ascx.vb L693-694 / User.ascx.vb L267): legacy hid delete when
    //   user.UserID = PortalSettings.AdministratorId  OR  (user.UserID = Me.UserId AndAlso user.IsSuperUser).
    // PortalSettings.AdministratorId is NOT carried on the JWT principal (CurrentUser), so the
    // administrator clause is approximated by protecting superuser (host/admin) accounts. The second
    // clause — the row is the current user AND a superuser — is preserved explicitly. Under the
    // available data both clauses reduce to "the row is a superuser".
    const isCurrentUser = current !== null && user.userId === current.userId;
    return user.isSuperUser || (isCurrentUser && user.isSuperUser);
  }
}
